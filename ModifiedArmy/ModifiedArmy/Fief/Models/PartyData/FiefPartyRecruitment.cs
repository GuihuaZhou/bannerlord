using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.PartyFinance.Models;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using static System.Collections.Specialized.BitVector32;
using static ModifiedArmy.common.CommonConstants;

namespace ModifiedArmy.Models.Fief
{
    public partial class FiefPartyData
    {
        private void RemoveTroopsFromParty(TroopRoster roster, CharacterObject troop, int countToRemove)
        {
            if (roster == null || troop == null || countToRemove <= 0) return;
            int currentCount = roster.GetElementNumber(troop);
            if (currentCount <= 0) return;
            int actualRemove = Math.Min(countToRemove, currentCount);
            if (actualRemove > 0)
            {
                roster.RemoveTroop(troop, actualRemove, default(UniqueTroopDescriptor), 0);
            }
        }

        /// <summary>
        /// 从封邑军队中按比例招募士兵到目标部队。
        /// - 从 _fiefParty 移除已征召士兵（因为他们已离营）
        /// - 添加到 RecruitedTroopDetachmentList（标记为已征召）
        /// - 不修改 RetinueCount/SergeantCount/MilitiaCount（兵力仍属封邑）
        ///
        /// 经济规则：
        /// - 玩家征召：产生 Prosperity / Hearth 动员成本
        /// - AI 征召：不产生 Prosperity / Hearth 成本
        /// </summary>
        /// <param name="targetParty">目标部队</param>
        /// <returns>总招募人数（必为 10 的倍数）</returns>
        public int RecruitTroopsToParty(MobileParty targetParty)
        {
            if (targetParty == null || _fiefParty == null)
                return 0;

            if (targetParty.LeaderHero == null)
                return 0;

            if (_settlement.OwnerClan != targetParty.LeaderHero.Clan)
                return 0;

            int currentMembers = targetParty.Party.NumberOfAllMembers;
            int partySizeLimit = targetParty.Party.PartySizeLimit;
            int remainSize = partySizeLimit - currentMembers;

            // A full party cannot receive another fief detachment.
            if (remainSize <= 0)
                return 0;

            if (_soldierTypeWeights == null || _totalWeight <= 0)
            {
                ModLogger.Error(
                    $"[Recruit] The soldier type weights are unavailable. The total weight is {_totalWeight}.");

                return 0;
            }

            // The fief template remains the upstream supply policy. The
            // unified AI model may further reduce these offers, but it never
            // changes which fief troop identities the settlement produces.
            Dictionary<SoldierType, int> offeredSoldierTypeSize = new();

            // 记录招募的士兵类型及数量
            Dictionary<SoldierType, int> tmpRecruitSoldierTypeSize = new();

            // 按权重分配剩余空间
            foreach (var kvp in _soldierTypeWeights)
            {
                offeredSoldierTypeSize[kvp.Key] =
                    (remainSize * _soldierTypeWeights[kvp.Key]) / _totalWeight;

                tmpRecruitSoldierTypeSize[kvp.Key] = 0;
            }

            // 记录招募的士兵和数量
            Dictionary<CharacterObject, int> tmpRecruitTroops = new();

            float prosperityCost = 0;
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取
            //
            // 若无模板（无 fallback），则不扣除繁荣度/户数。
            // ============================================================
            int tmpProsperityCostPerTroop =
                _fiefPartyTemplate?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                _fiefPartyTemplate?.HearthCostPerTroop ?? 0;

            List<RecruitmentCandidate> candidates = new();

            foreach (var element in _fiefParty.GetTroopRoster())
            {
                var troop = element.Character;
                var count = element.Number;

                if (troop == null || count <= 0)
                    continue;

                if (!SoldierTypeClassifier.IsFiefTroop(troop))
                    continue;

                var type =
                    SoldierTypeClassifier.GetSoldierType(troop);

                int offered = Math.Min(
                    count,
                    offeredSoldierTypeSize[type]);

                if (offered <= 0)
                {
                    continue;
                }

                candidates.Add(
                    new RecruitmentCandidate(
                        troop,
                        offered,
                        RecruitmentSource.Fief));

                offeredSoldierTypeSize[type] -= offered;
            }

            RecruitmentPlan aiPlan = null;
            Dictionary<CharacterObject, int> approvedTroops =
                targetParty.LeaderHero.Clan == Clan.PlayerClan
                    ? BuildPlayerFiefRecruitment(
                        targetParty,
                        candidates)
                    : BuildAiFiefRecruitment(
                        targetParty,
                        candidates,
                        out aiPlan);

            // Apply the completed decision only after all candidates have
            // been evaluated. This prevents earlier roster mutations from
            // changing the model inputs for later candidates.
            foreach (var approval in approvedTroops)
            {
                CharacterObject troop = approval.Key;
                int taken = approval.Value;

                if (troop == null || taken <= 0)
                {
                    continue;
                }

                SoldierType type =
                    SoldierTypeClassifier.GetSoldierType(troop);

                RemoveTroopsFromParty(_fiefParty, troop, taken);
                targetParty.MemberRoster.AddToCounts(
                    troop,
                    taken,
                    false,
                    0,
                    0,
                    true,
                    -1);

                // Fief troops have no immediate denar purchase price, but
                // their post-exemption wage still consumes the clan's shared
                // thirty-day recruitment commitment.
                if (ClanRecruitmentBudgetManager
                    .GetSupportingClan(targetParty) != Clan.PlayerClan)
                {
                    ClanRecruitmentBudgetManager.CommitRecruitment(
                        targetParty,
                        taken,
                        0f,
                        AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                            targetParty,
                            troop));
                }

                tmpRecruitTroops[troop] = taken;
                tmpRecruitSoldierTypeSize[type] += taken;
                hearthCost += taken * tmpHearthCostPerTroop;
            }

            int totalRecruited =
                tmpRecruitTroops.Values.Sum();

            if (totalRecruited <= 0)
            {
                LogAiFiefRecruitment(
                    targetParty,
                    aiPlan,
                    candidates.Sum(candidate => candidate.AvailableCount),
                    0);
                return 0;
            }

            LogAiFiefRecruitment(
                targetParty,
                aiPlan,
                candidates.Sum(candidate => candidate.AvailableCount),
                totalRecruited);

            prosperityCost =
                CalculateRecruitmentProsperityCost(totalRecruited);

            prosperityCost = UpdateProsperity(
                prosperityCost,
                false);
            hearthCost = UpdateHearth(
                hearthCost,
                false);

            // ============================================================
            // 记录招募的士兵
            //
            // 服役周期从模板读取（_fiefPartyTemplate.MaxServiceWeeks），
            // 替代原 CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE。
            // ============================================================
            int maxServiceWeeks =
                _fiefPartyTemplate?.MaxServiceWeeks
                ?? CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE;

            FiefTroopDetachment targetDetachment =
                RecruitedTroopDetachmentList
                    .FirstOrDefault(
                        d => d != null &&
                            d.WaitCycle ==
                            maxServiceWeeks);

            if (targetDetachment == null)
            {
                targetDetachment =
                    new FiefTroopDetachment(
                        maxServiceWeeks);

                RecruitedTroopDetachmentList.Add(
                    targetDetachment);
            }

            targetDetachment.AddTroops(
                tmpRecruitTroops);

            TextObject msg =
                GameTexts.FindText(
                    "str_modifiedarmy_fief_recruit_to_party");

            msg.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(targetParty));

            msg.SetTextVariable(
                "SETTLEMENT_NAME",
                _settlement.Name.ToString());

            msg.SetTextVariable(
                "RETINUE",
                tmpRecruitSoldierTypeSize[SoldierType.Retinue]);

            msg.SetTextVariable(
                "SERGEANT",
                tmpRecruitSoldierTypeSize[SoldierType.Sergeant]);

            msg.SetTextVariable(
                "MARINE",
                tmpRecruitSoldierTypeSize[SoldierType.Marine]);

            msg.SetTextVariable(
                "SLAVE",
                tmpRecruitSoldierTypeSize[SoldierType.Slave]);

            msg.SetTextVariable(
                "MILITIA",
                tmpRecruitSoldierTypeSize[SoldierType.Militia]);

            // 玩家显示实际成本；AI 显示 0
            msg.SetTextVariable(
                "PROSPERITY_COST",
                prosperityCost);

            msg.SetTextVariable(
                "HEARTH_COST",
                hearthCost);

            if (targetParty.LeaderHero.Clan == Clan.PlayerClan)
            {
                ModLogger.Notice(msg.ToString());
            }
            else if (RecruitmentLogFilter.ShouldLog(targetParty))
            {
                ModLogger.Info(msg.ToString());
            }

            // ============================================================
            // 给与工资减免
            // ============================================================
            var _fiefWageExemptionManager =
                Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();

            _fiefWageExemptionManager?.AddExemption(
                targetParty,
                totalRecruited,
                CommonConstants.FIEF_WAGE_EXEMPTION_DAYS);

            return totalRecruited;
        }

        /// <summary>
        /// Keeps the legacy automatic player recruitment behavior separate
        /// from the AI composition model. The player remains constrained by
        /// party space and the existing additional-wage allowance only.
        /// </summary>
        private static Dictionary<CharacterObject, int>
            BuildPlayerFiefRecruitment(
                MobileParty targetParty,
                IReadOnlyList<RecruitmentCandidate> candidates)
        {
            Dictionary<CharacterObject, int> result = new();
            float remainingDailyWageBudget =
                AiRecruitmentFinancialModel
                    .GetAvailableAdditionalDailyWage(targetParty);

            foreach (RecruitmentCandidate candidate in candidates)
            {
                float unitDailyWage =
                    AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                        targetParty,
                        candidate.Troop);
                int approved = candidate.AvailableCount;

                if (unitDailyWage > 0f)
                {
                    approved = Math.Min(
                        approved,
                        (int)Math.Floor(
                            remainingDailyWageBudget / unitDailyWage));
                }

                if (approved <= 0)
                {
                    continue;
                }

                result[candidate.Troop] = approved;
                remainingDailyWageBudget = Math.Max(
                    0f,
                    remainingDailyWageBudget - approved * unitDailyWage);
            }

            return result;
        }

        /// <summary>
        /// Evaluates all fief offers together so role, quality, wage and
        /// thirty-day affordability limits see one consistent party state.
        /// </summary>
        private static Dictionary<CharacterObject, int>
            BuildAiFiefRecruitment(
                MobileParty targetParty,
                IReadOnlyList<RecruitmentCandidate> candidates,
                out RecruitmentPlan plan)
        {
            Dictionary<CharacterObject, int> result = new();
            plan = RecruitmentModelManager.Model.BuildPlan(
                targetParty,
                candidates);

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.Troop == null ||
                    evaluation.RecruitableCount <= 0)
                {
                    continue;
                }

                result[evaluation.Troop] = evaluation.RecruitableCount;
            }

            return result;
        }

        /// <summary>
        /// Emits one compact record for an AI fief recruitment attempt.
        /// Player recruitment already has its existing result notification.
        /// </summary>
        private void LogAiFiefRecruitment(
            MobileParty targetParty,
            RecruitmentPlan plan,
            int offeredCount,
            int approvedCount)
        {
            if (plan == null)
            {
                return;
            }

            if (!RecruitmentLogFilter.ShouldLog(targetParty))
            {
                return;
            }

            RecruitmentLimitReason mainLimit = RecruitmentLimitReason.None;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.RecruitableCount < evaluation.RequestedCount)
                {
                    mainLimit = evaluation.PrimaryLimit;
                    break;
                }
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_ai_recruitment_fief_plan");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(targetParty));
            message.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
            message.SetTextVariable("CULTURE_ID", plan.CultureId);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", approvedCount);
            message.SetTextVariable(
                "LIMIT",
                GetRecruitmentLimitText(mainLimit));
            if (approvedCount > 0)
            {
                ModLogger.Info(message.ToString());
            }
            else
            {
                ModLogger.Info(message.ToString());
            }
        }

        /// <summary>
        /// Resolves the same stable localization keys used by the other
        /// unified recruitment integrations.
        /// </summary>
        private static TextObject GetRecruitmentLimitText(
            RecruitmentLimitReason reason)
        {
            switch (reason)
            {
                case RecruitmentLimitReason.InvalidParty:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_invalid_party");
                case RecruitmentLimitReason.InvalidTroop:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_invalid_troop");
                case RecruitmentLimitReason.PartySize:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_party_size");
                case RecruitmentLimitReason.CombatRole:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_combat_role");
                case RecruitmentLimitReason.Quality:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_quality");
                case RecruitmentLimitReason.WageLimit:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_wage");
                case RecruitmentLimitReason.RecruitmentCost:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_cost");
                case RecruitmentLimitReason.MaintenanceFunds:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_maintenance");
                default:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_none");
            }
        }


        /// <summary>
        /// 从封邑就绪部队中手动招募指定的士兵，并记录到征召列表。
        /// 前提：selectedRoster 来自 FiefTroops，所有 troop 和数量均有效。
        /// </summary>
        /// <param name="selectedRoster">玩家从 _fiefParty 中选择的士兵</param>
        /// <returns>实际招募的总人数（用于工资豁免）</returns>
        public int RecruitManualSelection(TroopRoster selectedRoster, MobileParty targetParty)
        {
            if (selectedRoster == null || selectedRoster.TotalManCount <= 0 || _fiefParty == null)
                return 0;

            var tmpRecruitTroops = new Dictionary<CharacterObject, int>();
            // 记录招募的士兵类型及数量
            Dictionary<SoldierType, int> tmpRecruitSoldierTypeSize = new();
            // 按权重分配剩余空间
            foreach (var kvp in _soldierTypeWeights)
            {
                tmpRecruitSoldierTypeSize[kvp.Key] = 0;
            }

            float prosperityCost = 0;
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取（与 RecruitTroopsToParty 一致）
            // ============================================================
            int tmpProsperityCostPerTroop =
                _fiefPartyTemplate?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                _fiefPartyTemplate?.HearthCostPerTroop ?? 0;

            foreach (var element in selectedRoster.GetTroopRoster())
            {
                var troop = element.Character;
                int count = element.Number;
                if (troop == null || count <= 0) continue;

                if (!SoldierTypeClassifier.IsFiefTroop(troop))
                    continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);

                // 记录招募的士兵
                tmpRecruitTroops[troop] = count;
                tmpRecruitSoldierTypeSize[type] += count;
                // 从封邑party移除士兵
                RemoveTroopsFromParty(_fiefParty, troop, count);

                // ====================================================
                // Hearth 成本
                //
                // 每征召 1 名士兵，消耗 1 Hearth。
                // Prosperity 不在这里计算，统一在本次征召完成后计算。
                // ====================================================
                hearthCost +=
                    count * tmpHearthCostPerTroop;
            }

            int totalRecruited =
                tmpRecruitTroops.Values.Sum();

            if (totalRecruited <= 0)
                return 0;

            prosperityCost =
                CalculateRecruitmentProsperityCost(totalRecruited);

            prosperityCost = UpdateProsperity(
                prosperityCost,
                false);
            hearthCost = UpdateHearth(
                hearthCost,
                false);

            // 添加到 RecruitedTroopDetachmentList
            int maxServiceWeeks =
                _fiefPartyTemplate?.MaxServiceWeeks
                ?? CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE;

            FiefTroopDetachment targetDetachment = RecruitedTroopDetachmentList
                .FirstOrDefault(d => d != null && d.WaitCycle == maxServiceWeeks);

            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(maxServiceWeeks);
                RecruitedTroopDetachmentList.Add(targetDetachment);
            }
            targetDetachment.AddTroops(tmpRecruitTroops);

            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_recruit_to_party");
            msg.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(targetParty));
            msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msg.SetTextVariable("RETINUE", tmpRecruitSoldierTypeSize[SoldierType.Retinue]);
            msg.SetTextVariable("SERGEANT", tmpRecruitSoldierTypeSize[SoldierType.Sergeant]);
            msg.SetTextVariable("MARINE", tmpRecruitSoldierTypeSize[SoldierType.Marine]);
            msg.SetTextVariable("SLAVE", tmpRecruitSoldierTypeSize[SoldierType.Slave]);
            msg.SetTextVariable("MILITIA", tmpRecruitSoldierTypeSize[SoldierType.Militia]);
            msg.SetTextVariable("PROSPERITY_COST", prosperityCost);
            msg.SetTextVariable("HEARTH_COST", hearthCost);

            if (targetParty.LeaderHero.Clan == Clan.PlayerClan)
            {
                ModLogger.Notice(msg.ToString());
            }
            else
            {
                ModLogger.Debug(msg.ToString());
            }

            // 给予工资减免
            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager?.AddExemption(
                targetParty,
                totalRecruited,
                CommonConstants.FIEF_WAGE_EXEMPTION_DAYS);

            // 将士兵添加到目标party
            targetParty.MemberRoster.Add(selectedRoster);

            return totalRecruited;
        }


        /// <summary>
        /// 从定居点俘虏中直接招募指定数量的士兵到封建部队（无需冷却）。
        /// </summary>
        /// <param name="troop">要招募的士兵类型</param>
        /// <param name="num">要招募的数量</param>
        /// <returns>是否成功招募</returns>
        public bool RecruitFromPrisoners(CharacterObject troop, int count)
        {
            // 参数校验
            if (troop == null || count <= 0)
                return false;

            if (_fiefParty == null)
                return false;

            // 1. 检查是否为封建部队允许的兵种
            if (_fiefPartyTemplate == null || !_fiefPartyTemplate.IsEnableTroop(troop))
                return false;

            // 2. 获取士兵类型并检查剩余容量
            var type = SoldierTypeClassifier.GetSoldierType(troop);
            int currentCount = _soldierTypeCounts[type];
            int maxCount = _soldierTypeMaxCounts[type];
            int availableCapacity = Math.Max(0, maxCount - currentCount);

            if (availableCapacity < count)
                return false;


            // 3. 直接添加到封建部队并更新繁荣度
            _fiefParty.AddToCounts(troop, count, false, 0, 0, true, -1);

            // 消耗的繁荣度
            float prosperityCost = 0;
            // 消耗的户数
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取（与 RecruitTroopsToParty 一致）
            // ============================================================
            int tmpProsperityCostPerTroop =
                _fiefPartyTemplate?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                _fiefPartyTemplate?.HearthCostPerTroop ?? 0;

            hearthCost += count * tmpHearthCostPerTroop;
            prosperityCost += count * tmpProsperityCostPerTroop * troop.Tier;

            prosperityCost = UpdateProsperity(prosperityCost, true);
            hearthCost = UpdateHearth(hearthCost, true);

            // 4. 更新计数器
            _soldierTypeCounts[type] += count;
            _totalTroopCount = _soldierTypeCounts.Values.Sum();

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_fief_prisoner_recruited");
            message.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
            message.SetTextVariable("COUNT", count);
            message.SetTextVariable("TROOP_NAME", troop.Name);
            ModLogger.Notice(message.ToString());

            return true;
        }
    }
}
