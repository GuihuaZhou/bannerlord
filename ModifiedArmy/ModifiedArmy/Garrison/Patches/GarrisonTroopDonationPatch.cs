using HarmonyLib;
using Helpers;
using ModifiedArmy.Garrison.Behaviors;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Behaviors;
using System.Collections.Generic;
using System.Linq;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.Core;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// Restricts routine AI troop exchange to clan-owned settlements while
    /// preserving the native post-conquest donation path for the army that
    /// actually captured the fortification.
    /// </summary>
    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior))]
    [HarmonyPatch("OnSettlementEntered")]
    public static class GarrisonTroopDonationPatch
    {
        /// <summary>
        /// Uses the mod's explicit capture window instead of Bannerlord's
        /// private newly-conquered field, whose event timing is not reliable
        /// enough for this prefix.
        /// </summary>
        /// <param name="mobileParty">The party entering the settlement.</param>
        /// <param name="settlement">The entered settlement.</param>
        /// <param name="hero">The entering party's leader.</param>
        /// <returns>Whether the native handler may continue.</returns>
        public static bool Prefix(
            MobileParty mobileParty,
            Settlement settlement,
            Hero hero)
        {
            if (!Campaign.Current.GameStarted)
            {
                return false;
            }
            if (ManagedPartyPolicy.IsModManagedAiParty(mobileParty))
            {
                GarrisonCaptureSupplyBehavior capture = Campaign.Current
                    .GetCampaignBehavior<GarrisonCaptureSupplyBehavior>();
                bool explicitCaptureDonation = settlement?.IsFortification == true &&
                    capture?.CanDonateTroopsAfterCapture(
                        mobileParty,
                        settlement) == true;
                return explicitCaptureDonation;
            }
            if (mobileParty != null
                && mobileParty.IsLordParty
                && !mobileParty.IsDisbanding
                && mobileParty.LeaderHero != null
                && settlement.IsFortification
                && DiplomacyHelper.IsSameFactionAndNotEliminated(mobileParty.MapFaction, settlement.MapFaction))
            {
                bool ownsSettlement =
                    mobileParty.Owner?.Clan == settlement.OwnerClan;
                GarrisonCaptureSupplyBehavior captureBehavior =
                    Campaign.Current.GetCampaignBehavior<
                        GarrisonCaptureSupplyBehavior>();
                bool isCapturingArmy = captureBehavior?
                    .CanDonateTroopsAfterCapture(
                        mobileParty,
                        settlement) == true;
                if (!ownsSettlement && !isCapturingArmy)
                {
                    return false;
                }
            }

            return true;
        }

        public static void Postfix(
            MobileParty mobileParty,
            Settlement settlement)
        {
            // The independent PartyExchangeBehavior owns SettlementEntered.
            // Keeping this postfix empty avoids a second visit transaction.
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

        private static bool Prefix(
            MobileParty mobileParty,
            ref int numberOfTroopsToLeave,
            out TransferState __state)
        {
            Settlement current = mobileParty?.CurrentSettlement;
            bool explicitCaptureDonation = current?.IsFortification == true &&
                Campaign.Current?.GetCampaignBehavior<
                    GarrisonCaptureSupplyBehavior>()?
                .CanDonateTroopsAfterCapture(mobileParty, current) == true;
            if (ManagedPartyPolicy.IsModManagedAiParty(mobileParty) &&
                !explicitCaptureDonation)
            {
                __state = null;
                numberOfTroopsToLeave = 0;
                return false;
            }
            MobileParty garrison = mobileParty?.CurrentSettlement?.Town?.GarrisonParty;
            __state = new TransferState
            {
                Source = mobileParty,
                Target = garrison,
                SourceBefore = Snapshot(mobileParty),
                TargetBefore = Snapshot(garrison)
            };
            Settlement settlement = mobileParty?.CurrentSettlement;
            if (garrison == null || settlement == null)
            {
                return true;
            }

            if (mobileParty.ActualClan != settlement.OwnerClan)
            {
                numberOfTroopsToLeave = Math.Min(
                    GarrisonTransferPolicyModel.GetAllowedDonationCount(
                        mobileParty,
                        numberOfTroopsToLeave),
                    GarrisonRecruitmentCapacity.GetTransferFreeSlots(garrison));
                return true;
            }

            ForceManagementFacade.Current.TroopTransfers
                .TransferFieldToGarrison(
                    mobileParty,
                    settlement,
                    numberOfTroopsToLeave);
            numberOfTroopsToLeave = 0;
            // The service explicitly excludes fief-service troops, so the
            // legacy post-transfer identity migration must not run.
            __state = null;
            return false;
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

            if (ManagedPartyPolicy.IsModManagedAiParty(mobileParty))
                return false;

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
