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
        private sealed class TroopMoveEntry
        {
            public CharacterObject Troop { get; set; }
            public int Healthy { get; set; }
            public int Wounded { get; set; }
            public int Total => Healthy + Wounded;
        }

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
            List<TroopMoveEntry> manifest = new List<TroopMoveEntry>();
            int planned = 0;
            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations
                .Where(item => item?.Troop != null)
                .OrderBy(item => item.Troop.StringId,
                    StringComparer.Ordinal))
            {
                CharacterObject troop = evaluation.Troop;
                int protectedCount = reserved.TryGetValue(troop,
                    out int fiefCount) ? Math.Max(0, fiefCount) : 0;
                TroopRosterElement rosterElement = source.MemberRoster
                    .GetTroopRoster().FirstOrDefault(item =>
                        item.Character == troop);
                int available = Math.Max(0,
                    rosterElement.Number - protectedCount);
                int count = Math.Min(allowed - planned,
                    Math.Min(evaluation.RecruitableCount, available));
                if (count <= 0) continue;
                int healthy = Math.Min(count, Math.Max(0,
                    rosterElement.Number - rosterElement.WoundedNumber));
                manifest.Add(new TroopMoveEntry
                {
                    Troop = troop,
                    Healthy = healthy,
                    Wounded = count - healthy
                });
                planned += count;
                if (planned >= allowed) break;
            }
            int transferred = 0;

            foreach (TroopMoveEntry entry in manifest)
            {
                int capacity = GarrisonRecruitmentCapacity
                    .GetTransferFreeSlots(garrison);
                if (entry.Total <= 0 || capacity < entry.Total)
                {
                    break;
                }
                if (purpose != GarrisonDonationPurpose.PeaceDemobilization)
                {
                    source.MemberRoster.AddToCounts(entry.Troop,
                        -entry.Total, false, -entry.Wounded, 0, true, -1);
                    garrison.MemberRoster.AddToCounts(entry.Troop,
                        entry.Total, false, entry.Wounded, 0, true, -1);
                    transferred += entry.Total;
                }
                else
                {
                    try
                    {
                        if (entry.Healthy > 0)
                        {
                            int index = garrison.MemberRoster.AddToCounts(
                                entry.Troop, entry.Healthy, false, 0, 0,
                                true, -1);
                            if (index < 0) break;
                            source.MemberRoster.AddToCounts(entry.Troop,
                                -entry.Healthy, false, 0, 0, true, -1);
                            transferred += entry.Healthy;
                        }
                        if (entry.Wounded > 0)
                        {
                            int index = garrison.MemberRoster.AddToCounts(
                                entry.Troop, entry.Wounded, false,
                                entry.Wounded, 0, true, -1);
                            if (index < 0) break;
                            source.MemberRoster.AddToCounts(entry.Troop,
                                -entry.Wounded, false, -entry.Wounded, 0,
                                true, -1);
                            transferred += entry.Wounded;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error(string.Format(
                            "Peace demobilization transfer failed: party={0}, settlement={1}, troop={2}, completed={3}, error={4}",
                            PartyLogFormatter.GetDisplayName(source),
                            destination?.Name?.ToString() ?? "<unknown>",
                            entry.Troop?.Name?.ToString() ?? "<unknown>",
                            transferred, ex));
                        break;
                    }
                }
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
