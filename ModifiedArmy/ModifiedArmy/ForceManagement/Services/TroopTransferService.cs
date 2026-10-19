using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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
            MobileParty garrison = destination?.Town?.GarrisonParty;
            if (source == null || garrison == null || requestedCount <= 0 ||
                source.ActualClan == null ||
                source.ActualClan != destination.OwnerClan ||
                source.MapEvent != null || destination.IsUnderSiege)
            {
                return 0;
            }

            int allowed = GarrisonTransferPolicyModel.GetAllowedDonationCount(
                source,
                requestedCount);
            allowed = Math.Min(
                allowed,
                GarrisonRecruitmentCapacity.GetTransferFreeSlots(garrison));
            if (allowed <= 0)
            {
                return 0;
            }

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            Dictionary<CharacterObject, int> reserved = manager?
                .GetServingTroopCounts(source) ?? new();
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

            return transferred;
        }
    }
}
