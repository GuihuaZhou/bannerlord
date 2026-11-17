using System;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Services
{
    internal sealed class ProfessionalRecruitmentCommit
    {
        public MobileParty Garrison { get; set; }
        public CharacterObject Troop { get; set; }
        public int Count { get; set; }
        public int RosterBefore { get; set; }
        public ProfessionalOfferConsumption Consumption { get; set; }
    }

    /// <summary>
    /// Commits one pre-approved professional offer into a garrison. A source
    /// consumption or roster mutation that has already succeeded is never
    /// reversed; callers reconcile the observed authoritative state instead.
    /// </summary>
    internal static class ProfessionalRecruitmentService
    {
        public static bool TryCommit(Settlement settlement, MobileParty garrison,
            CharacterObject troop, int count,
            SettlementManpowerProductionBehavior pools,
            out ProfessionalRecruitmentCommit commit)
        {
            return TryCommit(settlement, garrison, troop, count, pools,
                out commit, out _);
        }

        internal static bool TryCommit(Settlement settlement,
            MobileParty garrison, CharacterObject troop, int count,
            SettlementManpowerProductionBehavior pools,
            out ProfessionalRecruitmentCommit commit, out string reasonCode)
        {
            commit = null;
            reasonCode = "InvalidRequest";
            if (settlement == null || garrison == null || troop == null ||
                count <= 0 || pools == null)
            {
                return false;
            }
            int rosterBefore = garrison.MemberRoster.GetElementNumber(troop);
            if (!pools.TryConsumeProfessionalOffer(
                    settlement, troop, count,
                    out ProfessionalOfferConsumption consumption))
            {
                reasonCode = "OfferConsumptionRejected";
                return false;
            }
            try
            {
                garrison.MemberRoster.AddToCounts(
                    troop, count, false, 0, 0, true, -1);
                commit = new ProfessionalRecruitmentCommit
                {
                    Garrison = garrison,
                    Troop = troop,
                    Count = count,
                    RosterBefore = rosterBefore,
                    Consumption = consumption
                };
                reasonCode = "Committed";
                return true;
            }
            catch (Exception exception)
            {
                int observedAdded = Math.Max(0,
                    garrison.MemberRoster.GetElementNumber(troop) - rosterBefore);
                reasonCode = observedAdded > 0
                    ? "RosterCommitPartiallyObserved"
                    : "RosterCommitFailedAfterOfferConsumption";
                ModLogger.FileError(string.Format(
                    "Professional recruitment stopped after a committed step: " +
                    "settlement={0}, settlementId={1}, troop={2}, troopId={3}, " +
                    "requested={4}, rosterAdded={5}, reasonCode={6}, error={7}.",
                    settlement.Name, settlement.StringId, troop.Name,
                    troop.StringId, count, observedAdded, reasonCode,
                    exception.Message));
                return false;
            }
        }
    }
}
