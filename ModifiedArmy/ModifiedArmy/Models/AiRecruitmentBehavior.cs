using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.LinQuick;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// AI 招募决策控制器。
    ///
    /// 控制AI领主在三种兵源之间的招募偏好：封邑兵（Fief）、志愿兵（Volunteer）、雇佣兵（Mercenary）。
    /// 偏好与可用性分离：偏好由文化权重和情境修正决定，可用性在执行阶段检查。
    /// 决策流程：
    ///   1. 重派检查 — 已在前往招募据点途中则跳过
    ///   2. 随机概率 — 廉价门控，过滤部分部队
    ///   3. NeedScore — 计算招兵紧迫度，低于阈值则不招
    ///   4. 偏好→可行性→执行/回退 — 按偏好得分降序尝试各兵源，
    ///      找到可用据点则派兵前往，不可用则回退到下一偏好源
    ///
    /// 所有参数通过 ModConfig（modConfigs.xml → AiRecruitment 节点）配置，无硬编码数值。
    /// </summary>
    public class AiRecruitmentBehavior : CampaignBehaviorBase
    {
        private FiefPartyManager _fiefPartyManager;
        private ModConfig _config;

        // ========== 事件注册 ==========

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            var msg = GameTexts.FindText("str_modifiedarmy_ai_recruit_behavior_loaded");
            ModLogger.Notice(msg.ToString());
        }

        public override void SyncData(IDataStore dataStore)
        {
        }
        
        private static string GetPartyDisplayName(MobileParty party)
        {
            Kingdom kingdom = party.LeaderHero?.Clan?.Kingdom;
            if (kingdom != null)
                return $"[{kingdom.Name}] {party.Name}";
            return party.Name.ToString();
        }


        // ================================================================
        //  每日决策 — 重派检查→概率→NeedScore→偏好→可行性→执行/回退
        // ================================================================

        private void OnDailyTick()
        {
            if (Campaign.Current == null) return;

            _fiefPartyManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (_fiefPartyManager == null) return;

            _config = ModConfigManager.Instance?.GetActiveConfig();
            if (_config == null) return;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;

                foreach (Clan clan in kingdom.Clans)
                {
                    if (clan.IsEliminated || clan == Clan.PlayerClan) continue;

                    foreach (WarPartyComponent warParty in clan.WarPartyComponents)
                    {
                        MobileParty party = warParty.MobileParty;

                        if (party == null || !party.IsLordParty || party.Army != null) continue;
                        if (party.Party.IsStarving) continue;

                        // 1. 重派检查：已在前往招募据点途中则跳过
                        if (IsPartyCurrentlyOnRecruitmentTask(party, clan))
                            continue;

                        // 2. 随机概率门控（廉价检查，先过滤部分部队）
                        float roll = MBRandom.RandomFloat;
                        if (roll > _config.AiRecruitRandomChance)
                            continue;

                        // 3. NeedScore
                        var (needScore, manpowerGap, warUrgency, economicCapacity) = CalculateNeedScore(party);
                        if (needScore < _config.AiNeedThreshold)
                            continue;

                        // 4. 偏好计算（纯偏好，不含可用性）
                        var (preferred, fiefScore, volScore, mercScore) = CalculatePreferredSource(party);

                        // 5. 可行性→执行/回退：按偏好得分降序尝试各兵源
                        var candidates = new List<(RecruitSource source, float score)>
                        {
                            (RecruitSource.Fief, fiefScore),
                            (RecruitSource.Volunteer, volScore),
                            (RecruitSource.Mercenary, mercScore)
                        };
                        candidates.Sort((a, b) => b.score.CompareTo(a.score));

                        string preferences = string.Join(", ", candidates.Select(c => $"{c.source}={c.score:F3}"));
                        ModLogger.Info($"[AI招兵] {GetPartyDisplayName(party)} | Need={needScore:F2} | 偏好=[{preferences}]");

                        string displayName = GetPartyDisplayName(party);

                        bool dispatched = false;
                        foreach (var (source, _) in candidates)
                        {
                            Settlement target;
                            if (source == RecruitSource.Fief)
                                target = FindBestFiefSettlement(party, clan);
                            else if (source == RecruitSource.Mercenary)
                                target = FindBestMercenarySettlement(party);
                            else
                                target = FindBestVolunteerSettlement(party, clan);

                            if (target == null)
                            {
                                ModLogger.Info($"[AI招兵] {displayName} | {source}无据点→回退");
                                continue;
                            }

                            ModLogger.Info($"[AI招兵] {displayName} 决定前往 {target.Name} 招募 {source}");
                            party.SetMoveGoToSettlement(
                                target,
                                MobileParty.NavigationType.Default,
                                isTargetingThePort: false
                            );
                            dispatched = true;
                            break;
                        }

                        if (!dispatched)
                        {
                            ModLogger.Info($"[AI招兵] {displayName} | 所有兵源无据点，放弃招募");
                        }
                    }
                }
            }
        }

        // 
        public RecruitmentDecision GetRecruitmentDecision(MobileParty party)
        {
            var config = ModConfigManager.Instance?.GetActiveConfig();
            if (config == null)
                return new RecruitmentDecision(false, 0f, RecruitSource.Fief, 0f, 0f, 0f);
            
            // 在此处return，不会抛异常
            var (needScore, _, _, _) = CalculateNeedScore(party);
            // 在此处return，会抛异常
            if (needScore < config.AiNeedThreshold)
                return new RecruitmentDecision(false, needScore, RecruitSource.Fief, 0f, 0f, 0f);

            var (preferred, fief, vol, merc) = CalculatePreferredSource(party);
            return new RecruitmentDecision(true, needScore, preferred, fief, vol, merc);
        }

        // ================================================================
        //  第一层：NeedScore（招兵紧迫度）
        //
        //  NeedScore = w1·ManpowerGap + w2·WarUrgency + w3·EconomicCapacity
        //
        //  ManpowerGap       = 1 - PartySizeRatio，兵力缺口越大越紧迫
        //  WarUrgency        = 和平/战争/被围攻三档，由 AiNeedPeaceUrgency/WarUrgency/SiegeUrgency 配置
        //  EconomicCapacity  = min(1, Gold / (Wage × EconomicWeeks))，金库能撑几周工资
        // ================================================================

        private (float score, float manpowerGap, float warUrgency, float economicCapacity) CalculateNeedScore(MobileParty party)
        {
            if (_config == null)
                _config = ModConfigManager.Instance.GetActiveConfig();

            if (_fiefPartyManager == null)
                _fiefPartyManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();

            // 兵力缺口：PartySizeRatio=1 → gap=0（满员），Ratio=0.3 → gap=0.7（严重缺兵）
            float manpowerGap = Math.Max(0f, 1f - party.PartySizeRatio);

            // 战争紧迫度：被围攻 > 战争中 > 和平期
            float warUrgency;
            if (IsUnderThreat(party))
                warUrgency = _config.AiNeedSiegeUrgency;
            else if (IsAtWar(party))
                warUrgency = _config.AiNeedWarUrgency;
            else
                warUrgency = _config.AiNeedPeaceUrgency;
                
            // 经济承受力：金库 / (周工资 × 周数)，衡量能撑多久
            float gold = party.PartyTradeGold + (party.LeaderHero?.Clan.Gold ?? 0);
            float economicCapacity = party.TotalWage > 0
                ? Math.Min(1f, gold / (party.TotalWage * _config.AiNeedEconomicWeeks))
                : 1f;

            float score = _config.AiNeedManpowerWeight * manpowerGap
                        + _config.AiNeedWarUrgencyWeight * warUrgency
                        + _config.AiNeedEconomicWeight * economicCapacity;

            return (score, manpowerGap, warUrgency, economicCapacity);
        }

        // ================================================================
        //  偏好计算（纯偏好，不含可用性）
        //
        //  Preference = BaseWeight × SituationalModifier
        //
        //  BaseWeight          — 文化基础偏好，如 Vlandia 重封邑、Empire 重雇佣兵
        //  SituationalModifier — 战争状态/围攻/经济/兵力等情境修正（乘法叠加）
        //
        //  可用性在执行阶段检查（FindBestXSettlement），不影响偏好得分。
        //  文化身份修正（matchFiefBonus 等）下沉到 settlement 查找阶段。
        // ================================================================

        /// <summary>
        /// 计算三兵源偏好得分并返回最优兵源。
        /// 纯偏好计算：文化基础权重 × 情境修正。不考虑可用性。
        /// </summary>
        private (RecruitSource source, float fiefScore, float volScore, float mercScore) CalculatePreferredSource(MobileParty party)
        {
            string cultureId = party.LeaderHero?.Culture?.StringId ?? "null";
            var (fiefBase, volBase, mercBase) = _config.GetAiCulturalPreference(party.LeaderHero?.Culture);

            float fiefMod = 1f, volMod = 1f, mercMod = 1f;
            ApplySituationalModifiers(party, ref fiefMod, ref volMod, ref mercMod);

            float fiefScore = fiefBase * fiefMod;
            float volScore = volBase * volMod;
            float mercScore = mercBase * mercMod;

            // 归一化处理 (确保总和为 1.0)
            float totalScore = fiefScore + volScore + mercScore;
            if (totalScore > 0.0001f) // 防止除以 0
            {
                fiefScore /= totalScore;
                volScore  /= totalScore;
                mercScore /= totalScore;
            }
            else
            {
                // 兜底逻辑：如果所有权重都被压制到 0 (例如极端异常情况)，则平均分配
                fiefScore = 1f / 3f;
                volScore  = 1f / 3f;
                mercScore = 1f / 3f;
            }

            RecruitSource chosen;
            if (fiefScore >= volScore && fiefScore >= mercScore)
                chosen = RecruitSource.Fief;
            else if (mercScore >= volScore)
                chosen = RecruitSource.Mercenary;
            else
                chosen = RecruitSource.Volunteer;

            string displayName = GetPartyDisplayName(party);
            ModLogger.Info($"[AI招兵-偏好] {displayName} | {cultureId} | Fief={fiefScore:F3}, Volunteer={volScore:F3}, Mercenary={mercScore:F3} | chosen={chosen}");

            return (chosen, fiefScore, volScore, mercScore);
        }

        /// <summary>
        /// 乘法叠加所有适用的情境修正到三种兵源的偏好乘数上。
        /// </summary>
        private void ApplySituationalModifiers(MobileParty party, ref float fiefMod, ref float volMod, ref float mercMod)
        {
            var active = new List<string>();

            // 和平 / 战争（互斥）
            if (IsAtWar(party))
            {
                var (f, v, m) = _config.GetAiSituationalModifier("war");
                fiefMod *= f; volMod *= v; mercMod *= m;
                active.Add($"war");
            }
            else
            {
                var (f, v, m) = _config.GetAiSituationalModifier("peace");
                fiefMod *= f; volMod *= v; mercMod *= m;
                active.Add($"peace");
            }

            // 被围攻 / 敌军临近
            if (IsUnderThreat(party))
            {
                var (f, v, m) = _config.GetAiSituationalModifier("siege");
                fiefMod *= f; volMod *= v; mercMod *= m;
                active.Add($"siege");
            }

            // 经济紧张（Gold < brokeThreshold × TotalWage）
            float gold = party.PartyTradeGold + (party.LeaderHero?.Clan.Gold ?? 0);
            float brokeThreshold = _config.GetAiSituationalThreshold("broke");
            if (party.TotalWage > 0 && gold < party.TotalWage * brokeThreshold)
            {
                var (f, v, m) = _config.GetAiSituationalModifier("broke");
                fiefMod *= f; volMod *= v; mercMod *= m;
                active.Add($"broke");
            }

            // 兵力严重不足（PartySizeRatio < criticalThreshold）
            float criticalThreshold = _config.GetAiSituationalThreshold("critical");
            if (party.PartySizeRatio < criticalThreshold)
            {
                var (f, v, m) = _config.GetAiSituationalModifier("critical");
                fiefMod *= f; volMod *= v; mercMod *= m;
                active.Add($"critical");
            }
            
            string displayName = GetPartyDisplayName(party);
            ModLogger.Info($"[AI招兵-情境] {displayName} | [{string.Join("+", active)}] mul=({fiefMod:F3},{volMod:F3},{mercMod:F3})");
        }

        /// <summary>
        /// 计算封邑兵可用性修正值 = 可招募数 / 总上限。无封邑数据时返回 0。
        /// </summary>
        private float CalculateFiefAvailability(Settlement settlement)
        {
            int maxTroops = _fiefPartyManager.GetFiefTroopLimit(settlement);
            int availTroops = _fiefPartyManager.GetAvailableTroopCount(settlement);
            if (maxTroops <= 0) return 0f;
            return (float)availTroops / maxTroops;
        }

        // ================================================================
        //  辅助判断方法
        // ================================================================

        /// <summary>
        /// 找到 Fief 偏好得分最高的自有定居点（有可招募封邑兵）。
        /// 综合考虑文化偏好、文化身份匹配、封邑兵可用比例。
        /// </summary>
        private Settlement FindBestFiefSettlement(MobileParty party, Clan clan)
        {
            Settlement best = null;
            float bestScore = 0f;

            foreach (Settlement settlement in clan.Settlements)
            {
                if (!settlement.IsTown && !settlement.IsCastle) continue;

                int availTroops = _fiefPartyManager.GetAvailableTroopCount(settlement);
                if (availTroops <= 0) continue;

                var (fiefBase, _, _) = _config.GetAiCulturalPreference(party.LeaderHero?.Culture);
                bool cultureMatch = party.LeaderHero?.Culture == settlement.Culture;
                float fiefMod = cultureMatch ? _config.AiCultMatchFiefBonus : 1f;
                float avail = CalculateFiefAvailability(settlement);
                float score = fiefBase * fiefMod * avail;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = settlement;
                }
            }

            return best;
        }

        /// <summary>
        /// 找到最近的城镇中拥有可用雇佣兵的定居点。
        /// 搜索范围：本国 town（不限距离）或搜索半径内的非敌对 town。
        /// </summary>
        private Settlement FindBestMercenarySettlement(MobileParty party)
        {
            Settlement best = null;
            float bestDistance = float.MaxValue;
            IFaction mapFaction = party.MapFaction;

            foreach (Settlement settlement in Settlement.All)
            {
                if (!settlement.IsTown) continue;

                float distance = party.Position.Distance(settlement.GatePosition);
                if (distance >= bestDistance) continue;

                IFaction settlementFaction = settlement.OwnerClan?.MapFaction;

                // 排除交战阵营的城镇
                if (mapFaction != null && settlementFaction != null &&
                    FactionManager.IsAtWarAgainstFaction(mapFaction, settlementFaction))
                    continue;

                // 搜索范围限制：本国 town 不限距离，非本国 town 限搜索半径
                bool isOwnFaction = settlementFaction == mapFaction;
                bool isNearby = distance <= _config.AiMercenarySearchRadius;
                if (!isOwnFaction && !isNearby) continue;

                // 必须有雇佣兵可用
                if (!HasAvailableMercenaries(settlement.Town)) continue;

                bestDistance = distance;
                best = settlement;
            }

            return best;
        }

        /// <summary>
        /// 检查城镇是否有可用的雇佣兵。
        /// 通过 RecruitmentCampaignBehavior 内部数据判断（Harmony Traverse）。
        /// </summary>
        private bool HasAvailableMercenaries(Town town)
        {
            if (town == null) return false;

            var behavior = Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>();
            if (behavior == null) return false;

            try
            {
                var mercenaryData = Traverse.Create(behavior).Method("GetMercenaryData", town).GetValue();
                if (mercenaryData == null) return false;

                return Traverse.Create(mercenaryData)
                    .Method("HasAvailableMercenary", Occupation.NotAssigned)
                    .GetValue<bool>();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 找到本 clan 拥有的 town/village 中有志愿兵的最近定居点。
        /// 按 文化匹配 × 距离 排序。
        /// </summary>
        private Settlement FindBestVolunteerSettlement(MobileParty party, Clan clan)
        {
            Settlement best = null;
            float bestScore = 0f;
            CultureObject leaderCulture = party.LeaderHero?.Culture;

            foreach (Settlement settlement in clan.Settlements)
            {
                if (!settlement.IsTown && !settlement.IsVillage) continue;

                if (!HasAvailableVolunteers(settlement)) continue;

                float distance = party.Position.Distance(settlement.GatePosition);
                bool cultureMatch = leaderCulture == settlement.Culture;
                float cultureMod = cultureMatch ? 1f : _config.AiCultMismatchVolunteerPenalty;
                float score = cultureMod / (1f + distance * 0.01f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = settlement;
                }
            }

            return best;
        }

        /// <summary>
        /// 检查定居点的 notable 是否有可招募的志愿兵。
        /// </summary>
        private bool HasAvailableVolunteers(Settlement settlement)
        {
            if (settlement == null) return false;
            foreach (Hero notable in settlement.Notables)
            {
                if (!notable.CanHaveRecruits || !notable.IsAlive) continue;
                foreach (CharacterObject vol in notable.VolunteerTypes)
                {
                    if (vol != null)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断部队所属阵营是否处于战争状态。
        /// 遍历所有王国，检查是否与任一王国交战。
        /// </summary>
        private bool IsAtWar(MobileParty party)
        {
            IFaction mapFaction = party.MapFaction;
            if (mapFaction == null) return false;

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated || kingdom == mapFaction) continue;
                if (FactionManager.IsAtWarAgainstFaction(mapFaction, kingdom))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断是否受威胁：家族定居点被围攻 或 附近有敌对强军。
        /// </summary>
        private bool IsUnderThreat(MobileParty party)
        {
            return IsClanSettlementUnderSiege(party) || IsEnemyNearby(party);
        }

        /// <summary>
        /// 判断家族的城镇/城堡是否有被围攻的。
        /// </summary>
        private bool IsClanSettlementUnderSiege(MobileParty party)
        {
            Clan clan = party.LeaderHero?.Clan;
            if (clan == null) return false;

            foreach (Settlement s in clan.Settlements)
            {
                if ((s.IsTown || s.IsCastle) && s.IsUnderSiege)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断附近是否有敌对强军。
        /// 条件：距离 ≤ AiEnemyDetectRadius 且敌方人数 > 己方人数 × AiEnemyStrengthRatio。
        /// </summary>
        private bool IsEnemyNearby(MobileParty party)
        {
            float radiusSq = _config.AiEnemyDetectRadius * _config.AiEnemyDetectRadius;
            int partyManCount = party.Party.MemberRoster.TotalManCount;
            float strengthThreshold = partyManCount * _config.AiEnemyStrengthRatio;
            IFaction mapFaction = party.MapFaction;
            if (mapFaction == null) return false;

            foreach (MobileParty other in MobileParty.All)
            {
                if (other == party || !other.IsLordParty) continue;
                if (other.MapFaction == null) continue;
                if (!FactionManager.IsAtWarAgainstFaction(mapFaction, other.MapFaction)) continue;
                if (other.Party.MemberRoster.TotalManCount <= strengthThreshold) continue;
                if (party.Position.DistanceSquared(other.Position) <= radiusSq)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断该部队是否正在前往招募据点执行任务（且尚未到达）。
        /// 检查 Fief（clan 拥有的 town/castle）和 Volunteer（clan 拥有的 village）目标。
        /// Mercenary 不检查（由 randomChance 控制，避免阻塞原版 AI 行为）。
        /// </summary>
        private bool IsPartyCurrentlyOnRecruitmentTask(MobileParty party, Clan clan)
        {
            if (party == null || party.Ai == null)
                return false;

            var target = party.TargetSettlement;
            if (target == null)
                return false;

            // 已到达目标（在遭遇半径内），不算"执行中"
            float joinRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
            if (party.Position.DistanceSquared(target.GatePosition) <= joinRadius * joinRadius)
                return false;

            // Fief: 前往 clan 拥有的 town/castle
            if (target.OwnerClan == clan && (target.IsTown || target.IsCastle))
                return true;

            // Volunteer: 前往 clan 拥有的 village
            if (target.OwnerClan == clan && target.IsVillage)
                return true;

            return false;
        }
    }
}