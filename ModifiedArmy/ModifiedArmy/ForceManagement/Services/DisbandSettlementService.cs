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
    /// Runtime ledger for the irreversible settlement of a disbanding party.
    /// It is not persisted because the remaining roster and service batches
    /// are the authoritative idempotency state.
    /// </summary>
    public sealed class DisbandSettlementResult
    {
        public string PartyId { get; set; }
        public int FiefReturned { get; set; }
        public int FiefTransferredWithService { get; set; }
        public int OrdinaryAccepted { get; set; }
        public int FiefForcedLost { get; set; }
        public int OrdinaryForcedLost { get; set; }
        public string ReasonCode { get; set; }
        public IReadOnlyDictionary<CharacterObject, int> AcceptedTroops {
            get;
            set;
        } = new Dictionary<CharacterObject, int>();
    }

    /// <summary>
    /// Settles troop identities immediately before the base game destroys a
    /// formally disbanding party. This service never starts disbanding and
    /// never issues movement orders.
    /// </summary>
    public sealed class DisbandSettlementService
    {
        /// <summary>
        /// Returns eligible fief troops to their real sources, transfers
        /// remaining soldiers to a legal same-clan garrison when possible,
        /// and records every unplaceable service identity as an actual loss.
        /// The caller retains native hero/prisoner and final-clear duties.
        /// </summary>
        public DisbandSettlementResult SettleDisbandingParty(
            MobileParty party,
            Settlement disbandDestination)
        {
            DisbandSettlementResult result = new DisbandSettlementResult
            {
                PartyId = party?.StringId,
                ReasonCode = "InvalidParty"
            };
            if (party?.MemberRoster == null) return result;

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null)
            {
                result.ReasonCode = "FiefManagerUnavailable";
                result.OrdinaryForcedLost = CountRegulars(party);
                return result;
            }

            manager.AdvanceServiceDays(party);
            manager.ReconcilePartyFiefTroops(party);
            foreach (ServingDetachmentRef item in manager
                .GetServingDetachments(party)
                .OrderBy(entry => entry.SourceFief?.StringId)
                .ThenBy(entry => entry.Detachment?.BatchId)
                .ToList())
            {
                if (item.SourceFief?.OwnerClan != party.ActualClan ||
                    item.Detachment == null || item.Detachment.IsEmpty())
                {
                    continue;
                }
                FiefReturnResult returned = manager.TryReturnFiefTroops(
                    party,
                    item.SourceFief,
                    item.Detachment.BatchId,
                    item.Detachment.GetTotalCount());
                result.FiefReturned += returned.ActualReturnedCount;
            }

            MobileParty garrison = ResolveLegalGarrison(
                party,
                disbandDestination);
            Dictionary<CharacterObject, int> accepted = new();
            if (garrison != null)
            {
                Dictionary<CharacterObject, int> protectedCounts = manager
                    .GetServingTroopCounts(party);
                if (disbandDestination.OwnerClan == party.ActualClan)
                {
                    Dictionary<CharacterObject, int> serviceMoved =
                        TransferRoster(
                            party,
                            disbandDestination,
                            garrison,
                            protectedCounts);
                    AddCounts(accepted, serviceMoved);
                    result.FiefTransferredWithService = serviceMoved
                        .Values.Sum();
                    manager.TransferFiefService(
                        party,
                        garrison,
                        serviceMoved);
                }

                protectedCounts = manager.GetServingTroopCounts(party);
                Dictionary<CharacterObject, int> ordinaryAvailable = party
                    .MemberRoster.GetTroopRoster()
                    .Where(element => element.Character != null &&
                        !element.Character.IsHero && element.Number > 0)
                    .ToDictionary(
                        element => element.Character,
                        element => Math.Max(
                            0,
                            element.Number -
                                (protectedCounts.TryGetValue(
                                    element.Character,
                                    out int protectedCount)
                                        ? Math.Max(0, protectedCount)
                                        : 0)));
                Dictionary<CharacterObject, int> ordinaryMoved = TransferRoster(
                    party,
                    disbandDestination,
                    garrison,
                    ordinaryAvailable);
                AddCounts(accepted, ordinaryMoved);
                result.OrdinaryAccepted = ordinaryMoved.Values.Sum();
            }

            int remainingRegulars = CountRegulars(party);
            int remainingFief = manager.GetServingTroopCounts(party)
                .Sum(pair => Math.Max(0, Math.Min(
                    pair.Value,
                    party.MemberRoster.GetTroopCount(pair.Key))));
            result.FiefForcedLost = manager
                .SettleRemainingFiefServiceAsLoss(party);
            result.OrdinaryForcedLost = Math.Max(
                0,
                remainingRegulars - remainingFief);
            result.AcceptedTroops = accepted;
            result.ReasonCode = garrison == null
                ? "NoLegalGarrison"
                : "Completed";
            return result;
        }

        /// <summary>
        /// Resolves a destination that may inherit service identities. A
        /// A same-faction garrison may accept ordinary soldiers, while the
        /// caller separately requires same-clan ownership before moving fief
        /// service identities.
        /// </summary>
        private static MobileParty ResolveLegalGarrison(
            MobileParty party,
            Settlement destination)
        {
            if (party?.ActualClan == null ||
                destination?.IsFortification != true ||
                destination.IsUnderSiege ||
                destination.MapFaction != party.MapFaction)
            {
                return null;
            }
            if (destination.Town.GarrisonParty == null)
                destination.AddGarrisonParty();
            MobileParty garrison = destination.Town.GarrisonParty;
            return garrison?.IsActive == true &&
                garrison.MapFaction == party.MapFaction
                    ? garrison
                    : null;
        }

        /// <summary>
        /// Moves only the supplied per-character allowance. The recruitment
        /// model approves composition, but existing soldiers do not create a
        /// new-recruitment budget commitment.
        /// </summary>
        private static Dictionary<CharacterObject, int> TransferRoster(
            MobileParty source,
            Settlement settlement,
            MobileParty garrison,
            IReadOnlyDictionary<CharacterObject, int> allowance)
        {
            Dictionary<CharacterObject, int> moved = new();
            List<RecruitmentCandidate> candidates = allowance?
                .Where(pair => pair.Key != null && pair.Value > 0)
                .Select(pair => new RecruitmentCandidate(
                    pair.Key,
                    pair.Value,
                    RecruitmentSource.Transfer))
                .ToList() ?? new List<RecruitmentCandidate>();
            if (candidates.Count == 0) return moved;

            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                garrison,
                candidates);
            int reservedReturning = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(settlement);
            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
            {
                CharacterObject troop = evaluation.Troop;
                if (troop == null || !allowance.TryGetValue(
                        troop,
                        out int allowed))
                {
                    continue;
                }
                int count = Math.Min(
                    Math.Max(0, allowed),
                    Math.Min(
                        evaluation.RecruitableCount,
                        source.MemberRoster.GetTroopCount(troop)));
                count = Math.Min(
                    count,
                    GarrisonRecruitmentCapacity.GetTransferFreeSlots(
                        garrison,
                        reservedReturning));
                if (count <= 0) continue;

                TroopRosterElement element = source.MemberRoster
                    .GetTroopRoster()
                    .First(item => item.Character == troop);
                int healthy = Math.Max(
                    0,
                    element.Number - element.WoundedNumber);
                int wounded = Math.Max(0, count - healthy);
                source.MemberRoster.AddToCounts(
                    troop, -count, false, -wounded, 0, true, -1);
                garrison.MemberRoster.AddToCounts(
                    troop, count, false, wounded, 0, true, -1);
                moved[troop] = count;
            }
            return moved;
        }

        /// <summary>Adds positive troop counts into an aggregate ledger.</summary>
        private static void AddCounts(
            IDictionary<CharacterObject, int> destination,
            IReadOnlyDictionary<CharacterObject, int> source)
        {
            if (destination == null || source == null) return;
            foreach (var pair in source)
            {
                if (pair.Key == null || pair.Value <= 0) continue;
                destination[pair.Key] = destination.TryGetValue(
                    pair.Key,
                    out int old)
                        ? old + pair.Value
                        : pair.Value;
            }
        }

        /// <summary>Counts living regular members, excluding heroes.</summary>
        private static int CountRegulars(MobileParty party) => Math.Max(
            0,
            party?.MemberRoster?.TotalRegulars ?? 0);
    }
}
