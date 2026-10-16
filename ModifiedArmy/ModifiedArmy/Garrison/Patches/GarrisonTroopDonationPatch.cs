using HarmonyLib;
using Helpers;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Models.Fief;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.Core;

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
        private static void Postfix(Settlement settlement, ref int __result)
        {
            __result = GarrisonTransferPolicyModel
                .GetExchangeIdealGarrisonSize(settlement, __result);
        }
    }

    /// <summary>
    /// Prevents both independent parties and army members from donating below
    /// the same wage-, food- and capacity-aware target used by native logic.
    /// </summary>
    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior), "LeaveTroopsToGarrison")]
    public static class GarrisonPartyPriorityPatch
    {
        private sealed class TransferState
        {
            public MobileParty Source;
            public MobileParty Target;
            public Dictionary<CharacterObject, int> SourceBefore;
            public Dictionary<CharacterObject, int> TargetBefore;
        }

        private static void Prefix(
            MobileParty mobileParty,
            ref int numberOfTroopsToLeave,
            out TransferState __state)
        {
            MobileParty garrison = mobileParty?.CurrentSettlement?.Town?.GarrisonParty;
            __state = new TransferState
            {
                Source = mobileParty,
                Target = garrison,
                SourceBefore = Snapshot(mobileParty),
                TargetBefore = Snapshot(garrison)
            };
            numberOfTroopsToLeave = GarrisonTransferPolicyModel
                .GetAllowedDonationCount(
                    mobileParty,
                    numberOfTroopsToLeave);
        }

        private static void Postfix(TransferState __state)
        {
            if (__state?.Source == null || __state.Target == null) return;
            Dictionary<CharacterObject, int> sourceAfter = Snapshot(__state.Source);
            Dictionary<CharacterObject, int> targetAfter = Snapshot(__state.Target);
            Dictionary<CharacterObject, int> transferred = new();
            foreach (CharacterObject troop in __state.SourceBefore.Keys
                .Union(__state.TargetBefore.Keys))
            {
                int sourceLoss = GetCount(__state.SourceBefore, troop) -
                    GetCount(sourceAfter, troop);
                int targetGain = GetCount(targetAfter, troop) -
                    GetCount(__state.TargetBefore, troop);
                int moved = Math.Min(Math.Max(0, sourceLoss), Math.Max(0, targetGain));
                if (moved > 0) transferred[troop] = moved;
            }
            Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                .TransferFiefService(__state.Source, __state.Target, transferred);
        }

        private static Dictionary<CharacterObject, int> Snapshot(MobileParty party)
        {
            return party?.MemberRoster?.GetTroopRoster()
                .Where(e => e.Character != null && !e.Character.IsHero && e.Number > 0)
                .ToDictionary(e => e.Character, e => e.Number) ?? new();
        }

        private static int GetCount(
            IReadOnlyDictionary<CharacterObject, int> roster,
            CharacterObject troop)
        {
            return roster != null && roster.TryGetValue(troop, out int count)
                ? count
                : 0;
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

            GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison(
                mobileParty,
                settlement,
                numberOfTroopsToTake);

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
