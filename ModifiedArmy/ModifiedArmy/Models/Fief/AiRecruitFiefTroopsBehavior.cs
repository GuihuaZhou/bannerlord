using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    public class AiRecruitFiefTroopsBehavior : CampaignBehaviorBase
    {
        private FiefPartyManager _fiefPartyManager;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AiHourlyTick);
            ModLogger.Notice("[AI Fief Recruitment] AiRecruitFiefTroopsBehavior loaded and events registered.");
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        // ========== 阶段1：每日决策是否需要前往封邑 ==========
        private void OnDailyTick()
        {
            if (Campaign.Current == null) return;

            _fiefPartyManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (_fiefPartyManager == null) return;

            Dictionary<Clan, List<Settlement>> clanAvailableSettlements = new Dictionary<Clan, List<Settlement>>();

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;

                foreach (Clan clan in kingdom.Clans)
                {
                    if (clan.IsEliminated || clan == Clan.PlayerClan) continue;

                    List<Settlement> availableSettlements = new List<Settlement>();
                    foreach (Settlement settlement in clan.Settlements)
                    {
                        if (!settlement.IsTown && !settlement.IsCastle) continue;
                        if (_fiefPartyManager.GetAvailableRecruitCount(settlement) > 0)
                        {
                            availableSettlements.Add(settlement);
                        }
                    }

                    if (availableSettlements.Count == 0) continue;

                    clanAvailableSettlements[clan] = availableSettlements;
                    int settlementIndex = 0;
                    int totalSettlements = availableSettlements.Count;

                    foreach (WarPartyComponent warParty in clan.WarPartyComponents)
                    {
                        MobileParty party = warParty.MobileParty;
                        if (party == null || !party.IsLordParty || party.Army != null) continue;
                        if (IsPartyCurrentlyOnValidFiefRecruitmentTask(party, clan))
                        {
                            //ModLogger.Debug($"[AI Fief Recruit] Skipping {clan.Name}'s party: already en route to {party.TargetSettlement?.Name}");
                            continue;
                        }
                        if (!ShouldRecruitForParty(party)) continue;

                        Settlement targetSettlement = availableSettlements[settlementIndex % totalSettlements];
                        settlementIndex++;

                        party.SetMoveGoToSettlement(
                            targetSettlement,
                            MobileParty.NavigationType.Default,
                            isTargetingThePort: false
                        );

                        //ModLogger.Debug(
                        //    $"[AI Fief Recruitment] {clan.Name}'s party heading to {targetSettlement.Name} " +
                        //    $"(assignment #{settlementIndex})"
                        //);
                    }
                }
            }
        }

        // ========== 阶段2：AI 每小时检查是否在封邑内并招募 ==========
        private void AiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
        {
            // 跳过非领主或已在 army 中的部队
            if (!party.IsLordParty || party.Army != null)
                return;

            // 必须当前位于某个 settlement 内部
            if (party.CurrentSettlement is not Settlement settlement)
                return;

            // 必须是自有 Town 或 Castle
            if (settlement.OwnerClan != party.LeaderHero?.Clan ||
                (!settlement.IsTown && !settlement.IsCastle))
                return;

            // 获取 manager（复用字段）
            _fiefPartyManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (_fiefPartyManager == null)
                return;

            // 检查是否有可招募兵源
            int recruitableCount = _fiefPartyManager.GetAvailableRecruitCount(settlement);
            if (recruitableCount <= 0)
                return;

            // 再次检查招募条件
            if (!ShouldRecruitForParty(party))
                return;

            // 执行招募
            int recruited = _fiefPartyManager.RecruitFiefTroopsFromSettlement(settlement, party);
            if (recruited > 0)
            {
                // 安全日志：显式调用 .ToString()
                string partyName = party.Name?.ToString() ?? "UnknownParty";
                string settlementName = settlement.Name?.ToString() ?? "UnknownSettlement";
                ModLogger.Info(
                    $"[AI Fief Recruitment] {partyName} recruited {recruited} fief troops at {settlementName}."
                );
            }
        }

        /// <summary>
        /// 判断该部队是否正在前往本家族封邑执行招募任务（且尚未到达）。
        /// </summary>
        private bool IsPartyCurrentlyOnValidFiefRecruitmentTask(MobileParty party, Clan clan)
        {
            if (party == null || party.Ai == null)
                return false;

            var target = party.TargetSettlement;

            // 条件2: 有明确的目标定居点
            if (target == null)
                return false;

            // 条件3: 目标属于本家族，且是 Town 或 Castle
            if (target.OwnerClan != clan || (!target.IsTown && !target.IsCastle))
                return false;

            // 条件4: 尚未到达（仍在路上）
            float joinRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
            if (party.Position.DistanceSquared(target.GatePosition) <= joinRadius * joinRadius)
                return false; // 已到达，不算“执行中”

            return true;
        }

        /// <summary>
        /// 判断该领主部队是否应发起招募（金钱、兵力缺口等）。
        /// </summary>
        private bool ShouldRecruitForParty(MobileParty party)
        {
            if (party.Party.IsStarving) return false;
            if (party.PartySizeRatio >= 0.9f) return false;

            float minGoldNeeded = party.TotalWage * 0.3f;
            bool hasEnoughGold = party.PartyTradeGold >= minGoldNeeded ||
                                (party.LeaderHero?.Clan.Gold ?? 0) >= minGoldNeeded;

            return hasEnoughGold;
        }
    }
}
