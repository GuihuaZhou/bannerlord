using HarmonyLib;
using Helpers;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ModifiedArmy.Recruitment;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// 补丁：严格限制 AI 领主向定居点捐赠/抽取士兵的行为。
    /// 
    /// 原始逻辑：只要同派系且非玩家封地（或刚被攻占），AI 领主进入定居点就会尝试优化兵力。
    /// 修改后逻辑：
    /// 1. 对于军队统帅 (Army Leader)，保留原有逻辑不变。
    /// 2. 对于单独行动的 AI 领主 (满足 !mobileParty.IsMainParty)，必须是定居点的拥有者 (OwnerClan) 才能进行兵力操作。
    /// 
    /// 此修改旨在防止 AI 在路过盟友城市时随意捐兵，导致其自身兵力枯竭。
    /// </summary>
    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior))]
    [HarmonyPatch("OnSettlementEntered")]
    public static class GarrisonTroopDonationPatch
    {
        /// <summary>
        /// Prefix 补丁，在原方法执行前运行。
        /// 我们将复用原方法的大部分判断，但对 "else if (!mobileParty.IsMainParty)" 分支增加 Clan 所有权检查。
        /// </summary>
        /// <param name="__instance">GarrisonTroopsCampaignBehavior 的实例</param>
        /// <param name="mobileParty">进入定居点的部队</param>
        /// <param name="settlement">被进入的定居点</param>
        /// <param name="hero">英雄（通常与 mobileParty.LeaderHero 相同）</param>
        /// <returns>如果返回 false，则跳过原方法；否则继续执行原方法。</returns>
        /// 
        public static bool Prefix(GarrisonTroopsCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            if (!Campaign.Current.GameStarted)
            {
                return false;
            }
            if (mobileParty != null
                && mobileParty.IsLordParty
                && !mobileParty.IsDisbanding
                && mobileParty.LeaderHero != null
                && settlement.IsFortification
                && DiplomacyHelper.IsSameFactionAndNotEliminated(mobileParty.MapFaction, settlement.MapFaction))
            {
                FieldInfo newlyConqueredField = AccessTools.Field(typeof(GarrisonTroopsCampaignBehavior), "_newlyConqueredFortification");
                Settlement _newlyConqueredFortification = (Settlement)newlyConqueredField.GetValue(__instance);

                // 如果不是新占定居点且不是所属clan的定居点，禁止捐兵
                if (settlement != _newlyConqueredFortification
                    && mobileParty.Owner.Clan != settlement.OwnerClan)
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Reduces only the garrison share used by native transfer calculations.
    /// Native hard floors, capacity and wage limits remain unchanged.
    /// </summary>
    [HarmonyPatch(
        typeof(GarrisonTroopsCampaignBehavior),
        "CalculateSettlementIdealPartySizeWithEffects")]
    public static class GarrisonExchangeIdealSizePatch
    {
        private static void Postfix(ref int __result)
        {
            __result = Math.Max(0, MBRandom.RoundRandomized(__result * 0.40f));
        }
    }

    /// <summary>
    /// Prevents both independent parties and army members from donating below
    /// the same wage-, food- and capacity-aware target used by native logic.
    /// </summary>
    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior), "LeaveTroopsToGarrison")]
    public static class GarrisonPartyPriorityPatch
    {
        private static void Prefix(
            MobileParty mobileParty,
            ref int numberOfTroopsToLeave)
        {
            if (mobileParty == null || numberOfTroopsToLeave <= 0)
            {
                return;
            }

            int sizeLimit = mobileParty.Party.PartySizeLimit;
            int wageLimit = MathF.Round(
                mobileParty.PaymentLimit / Campaign.Current.AverageWage);
            float food = mobileParty.Army == null
                ? mobileParty.Food
                : mobileParty.Army.Parties.Sum(party => party.Food);
            int foodLimit = MathF.Round(
                Campaign.Current.Models.MobilePartyFoodConsumptionModel
                    .NumberOfMenOnMapToEatOneFood * food / 2f);
            int reasonableTarget = Math.Max(
                30,
                Math.Min(sizeLimit, Math.Min(wageLimit, foodLimit)));
            int surplus = Math.Max(
                0,
                mobileParty.Party.NumberOfRegularMembers - reasonableTarget);

            numberOfTroopsToLeave = Math.Min(numberOfTroopsToLeave, surplus);
        }
    }

    /// <summary>
    /// Reports actual AI withdrawals from a garrison belonging to the
    /// player's kingdom. The before/after count avoids logging failed or
    /// partially fulfilled transfer requests as if they fully succeeded.
    /// </summary>
    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior), "TakeTroopsFromGarrison")]
    public static class PlayerKingdomGarrisonWithdrawalLogPatch
    {
        private static bool Prefix(
            MobileParty mobileParty,
            Settlement settlement,
            int numberOfTroopsToTake,
            out int __state)
        {
            __state = settlement?.Town?.GarrisonParty?.Party
                .NumberOfRegularMembers ?? 0;

            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (mobileParty == null || garrison == null ||
                numberOfTroopsToTake <= 0)
            {
                return false;
            }

            List<RecruitmentCandidate> candidates = garrison.MemberRoster
                .GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0)
                .Select(element => new RecruitmentCandidate(
                    element.Character,
                    element.Number,
                    RecruitmentSource.Transfer))
                .ToList();
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                mobileParty,
                candidates);
            int remainingRequest = numberOfTroopsToTake;

            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
            {
                CharacterObject troop = evaluation.Troop;
                int available = troop == null
                    ? 0
                    : garrison.MemberRoster.GetTroopCount(troop);
                int count = Math.Min(
                    remainingRequest,
                    Math.Min(evaluation.RecruitableCount, available));
                if (count <= 0)
                {
                    continue;
                }

                int woundedAvailable = garrison.MemberRoster
                    .GetTroopRoster()
                    .Where(element => element.Character == troop)
                    .Select(element => element.WoundedNumber)
                    .FirstOrDefault();
                int healthyAvailable = Math.Max(
                    0,
                    available - woundedAvailable);
                int wounded = Math.Max(0, count - healthyAvailable);

                mobileParty.MemberRoster.AddToCounts(
                    troop,
                    count,
                    false,
                    wounded,
                    0,
                    true,
                    -1);
                garrison.MemberRoster.AddToCounts(
                    troop,
                    -count,
                    false,
                    -wounded,
                    0,
                    true,
                    -1);
                remainingRequest -= count;
                if (remainingRequest <= 0)
                {
                    break;
                }
            }

            // The approved roster has already been transferred. Skipping the
            // native weighted loop prevents it from selecting a different,
            // more expensive troop type than the unified plan approved.
            return false;
        }

        private static void Postfix(
            MobileParty mobileParty,
            Settlement settlement,
            int __state)
        {
            if (mobileParty == null || mobileParty.IsMainParty ||
                settlement?.Town?.GarrisonParty == null ||
                Clan.PlayerClan?.Kingdom == null ||
                mobileParty.MapFaction != Clan.PlayerClan.Kingdom)
            {
                return;
            }

            int remaining = settlement.Town.GarrisonParty.Party
                .NumberOfRegularMembers;
            int withdrawn = Math.Max(0, __state - remaining);
            if (withdrawn <= 0)
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_garrison_withdrawal");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(mobileParty));
            message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
            message.SetTextVariable("COUNT", withdrawn);
            ModLogger.Notice(message.ToString());
        }
    }
}
