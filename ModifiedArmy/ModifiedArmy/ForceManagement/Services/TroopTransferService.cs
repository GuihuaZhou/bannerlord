using ModifiedArmy.Garrison.Models;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Executes conservative one-way transfers of existing professional or
    /// mercenary troops. Fief-service batches remain with their field party.
    /// </summary>
    public sealed class TroopTransferService
    {
        public int TransferFieldToGarrison(
            MobileParty source,
            Settlement destination,
            int requestedCount)
        {
            return TransferFieldToGarrison(
                source,
                destination,
                requestedCount,
                GarrisonDonationPurpose.NativeDonation);
        }

        /// <summary>
        /// Transfers ordinary soldiers for either native donation or an
        /// explicitly approved peace demobilization. Fief-service identities
        /// remain protected in both modes.
        /// </summary>
        public int TransferFieldToGarrison(
            MobileParty source,
            Settlement destination,
            int requestedCount,
            GarrisonDonationPurpose purpose)
        {
            MobileParty garrison = destination?.Town?.GarrisonParty;
            if (source == null || garrison == null || requestedCount <= 0 ||
                source.CurrentSettlement != destination ||
                !PartyTransferOwnershipPolicy.IsAuthorizedRosterPair(
                    source,
                    garrison,
                    destination,
                    purpose == GarrisonDonationPurpose.PeaceDemobilization
                        ? PartyExchangePurpose.PeaceDemobilization
                        : PartyExchangePurpose.NaturalVisit))
            {
                LogOneWayTransfer(
                    source,
                    destination,
                    requestedCount,
                    0,
                    0,
                    "InvalidOrUnsafeTransfer");
                return 0;
            }

            int allowed = purpose ==
                    GarrisonDonationPurpose.PeaceDemobilization
                ? Math.Min(
                    requestedCount,
                    ForceManagementFacade.Current.GetSafeFieldSurplus(source))
                : GarrisonTransferPolicyModel.GetAllowedDonationCount(
                    source,
                    requestedCount);
            allowed = Math.Min(
                allowed,
                GarrisonRecruitmentCapacity.GetTransferFreeSlots(garrison));
            if (allowed <= 0)
            {
                LogOneWayTransfer(
                    source,
                    destination,
                    requestedCount,
                    0,
                    0,
                    "NoApprovedCapacity");
                return 0;
            }

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            manager?.ReconcilePartyFiefTroops(source);
            Dictionary<CharacterObject, int> reserved = manager?
                .GetServingTroopCounts(source) ?? new();
            int protectedFiefCount = reserved.Values.Sum(value =>
                Math.Max(0, value));
            List<RecruitmentCandidate> candidates = source.MemberRoster
                .GetTroopRoster()
                .Where(item => item.Character != null &&
                    !item.Character.IsHero && item.Number > 0)
                .Select(item =>
                {
                    int protectedCount = reserved.TryGetValue(
                        item.Character,
                        out int fiefCount)
                            ? Math.Max(0, fiefCount)
                            : 0;
                    return new RecruitmentCandidate(
                        item.Character,
                        Math.Max(0, item.Number - protectedCount),
                        RecruitmentSource.Transfer);
                })
                .Where(candidate => candidate.AvailableCount > 0)
                .ToList();
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                garrison,
                candidates);
            int transferred = 0;

            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
            {
                CharacterObject troop = evaluation.Troop;
                int protectedCount = troop != null && reserved.TryGetValue(
                    troop,
                    out int fiefCount)
                        ? Math.Max(0, fiefCount)
                        : 0;
                int available = troop == null
                    ? 0
                    : Math.Max(
                        0,
                        source.MemberRoster.GetTroopCount(troop) -
                            protectedCount);
                int count = Math.Min(
                    allowed - transferred,
                    Math.Min(evaluation.RecruitableCount, available));
                count = Math.Min(
                    count,
                    GarrisonRecruitmentCapacity.GetTransferFreeSlots(garrison));
                if (count <= 0)
                {
                    continue;
                }

                TroopRosterElement rosterElement = source.MemberRoster
                    .GetTroopRoster()
                    .FirstOrDefault(item => item.Character == troop);
                int healthy = Math.Max(0, rosterElement.Number -
                    rosterElement.WoundedNumber);
                int wounded = Math.Max(0, count - healthy);
                source.MemberRoster.AddToCounts(
                    troop,
                    -count,
                    false,
                    -wounded,
                    0,
                    true,
                    -1);
                garrison.MemberRoster.AddToCounts(
                    troop,
                    count,
                    false,
                    wounded,
                    0,
                    true,
                    -1);
                transferred += count;
                if (transferred >= allowed)
                {
                    break;
                }
            }

            LogOneWayTransfer(
                source,
                destination,
                requestedCount,
                transferred,
                protectedFiefCount,
                transferred > 0 ? "Completed" : "NoTransferableOrdinaryTroops");
            return transferred;
        }

        /// <summary>
        /// Records the requested and actual ordinary-soldier transfer while
        /// exposing how many fief-service soldiers remained protected.
        /// </summary>
        private static void LogOneWayTransfer(
            MobileParty source,
            Settlement destination,
            int requested,
            int actual,
            int protectedFiefCount,
            string reason)
        {
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_one_way_troop_transfer");
            message.SetTextVariable(
                "PARTY",
                PartyLogFormatter.GetDisplayName(source));
            message.SetTextVariable(
                "SETTLEMENT",
                destination?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("REQUESTED", Math.Max(0, requested));
            message.SetTextVariable("ACTUAL", Math.Max(0, actual));
            message.SetTextVariable(
                "PROTECTED",
                Math.Max(0, protectedFiefCount));
            message.SetTextVariable("RESULT", GetTransferResultText(reason));
            ModLogger.Info(message.ToString());
        }

        /// <summary>
        /// Converts internal one-way transfer reason codes into localizable,
        /// player-readable result text.
        /// </summary>
        private static TextObject GetTransferResultText(string reason)
        {
            switch (reason)
            {
                case "InvalidOrUnsafeTransfer":
                    return new TextObject(
                        "{=ModifiedArmy_TransferUnsafe}" +
                        "the transfer conditions are currently unsafe");
                case "NoApprovedCapacity":
                    return new TextObject(
                        "{=ModifiedArmy_TransferNoCapacity}" +
                        "the garrison cannot receive more soldiers");
                case "Completed":
                    return new TextObject(
                        "{=ModifiedArmy_TransferCompleted}" +
                        "the transfer was completed");
                case "NoTransferableOrdinaryTroops":
                    return new TextObject(
                        "{=ModifiedArmy_TransferNoOrdinaryTroops}" +
                        "there are no ordinary soldiers available to transfer");
                default:
                    return new TextObject(
                        "{=ModifiedArmy_TransferRejected}"+
                        "the transfer was not completed");
            }
        }

        public static string GetRosterFingerprint(MobileParty party)
        {
            if (party?.MemberRoster == null) return string.Empty;
            StringBuilder builder = new StringBuilder();
            foreach (TroopRosterElement element in party.MemberRoster
                .GetTroopRoster()
                .Where(item => item.Character != null)
                .OrderBy(item => item.Character.StringId))
            {
                builder.Append(element.Character.StringId)
                    .Append(':').Append(element.Number)
                    .Append(':').Append(element.WoundedNumber)
                    .Append(';');
            }
            return builder.ToString();
        }

    }
}
