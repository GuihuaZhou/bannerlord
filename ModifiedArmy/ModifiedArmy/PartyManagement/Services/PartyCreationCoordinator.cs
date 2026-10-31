using Helpers;
using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.PartyManagement.Diagnostics;
using ModifiedArmy.PartyManagement.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.PartyManagement.Services
{
    /// <summary>Orchestrates creation; existing systems retain every ledger.</summary>
    public sealed class PartyCreationCoordinator
    {
        private readonly PartyCreationSettlementModel _sites =
            new PartyCreationSettlementModel();

        public MobileParty TryCreateManagedLordParty(Hero leader, bool isNewGame)
        {
            if (!ManagedPartyPolicy.IsModManagedAiLeader(leader)) return null;
            PartySettlementBindingBehavior bindings = Campaign.Current?
                .GetCampaignBehavior<PartySettlementBindingBehavior>();
            AiForcePlanningBehavior planning = Campaign.Current?
                .GetCampaignBehavior<AiForcePlanningBehavior>();
            PartyCreationSiteResult site = _sites.FindEligibleBase(
                leader, leader.Clan, 50);
            if (bindings == null || planning == null || site?.Settlement == null)
            {
                PartyCreationDiagnostics.Log(leader, null, null, "Rejected",
                    "NoEligibleCreationBase", 0, 0, 0);
                return null;
            }
            if (!bindings.TryReserveForCreation(
                    leader, leader.Clan, site.Settlement, out var token))
            {
                PartyCreationDiagnostics.Log(leader, null,
                    site.Settlement.StringId, "Rejected",
                    "CreationBaseReservationRejected",
                    site.EstimatedGarrisonRegulars,
                    site.EstimatedFiefRegulars,
                    site.EstimatedCombinedRegulars);
                return null;
            }

            MobileParty party = null;
            bool committed = false;
            try
            {
                if (leader.GovernorOf != null)
                    ChangeGovernorAction.RemoveGovernorOf(leader);
                using (PartyCreationScope.Begin(leader))
                    party = MobilePartyHelper.SpawnLordParty(
                        leader, site.Settlement);
                if (party == null) return null;
                if (party.Party.PartySizeLimit < 51)
                    return FailAfterCreation(bindings, planning, leader, party,
                        site, "PartyCapacityBelowFiftyRegulars");
                if (!bindings.TryCommitCreation(token, party))
                    return FailAfterCreation(bindings, planning, leader, party, site,
                        "BindingCommitRejected");
                committed = true;
                if (!planning.TryRegisterInitialRecruitTask(
                        party, site.Settlement, site.PreferredInitialSource))
                    return FailAfterCreation(bindings, planning, leader, party, site,
                        "InitialTaskRegistrationRejected");

                EnterSettlementAction.ApplyForParty(party, site.Settlement);
                PartyCreationDiagnostics.Log(leader, party,
                    site.Settlement.StringId, "Completed", site.ReasonCode,
                    site.EstimatedGarrisonRegulars,
                    site.EstimatedFiefRegulars,
                    site.EstimatedCombinedRegulars,
                    site.PreferredInitialSource.ToString());
                return party;
            }
            catch (Exception exception)
            {
                if (party?.IsActive == true)
                {
                    planning.CancelTask(party, "CreationTransactionException");
                    bindings.ReleasePartyBindings(
                        party, "CreationTransactionException");
                    DestroyPartyAction.Apply(null, party);
                }
                PartyCreationDiagnostics.Log(leader, party,
                    site.Settlement.StringId, "Failed",
                    "CreationTransactionException:" + exception.GetType().Name,
                    site.EstimatedGarrisonRegulars,
                    site.EstimatedFiefRegulars,
                    site.EstimatedCombinedRegulars);
                return null;
            }
            finally
            {
                if (!committed) bindings.CancelCreationReservation(token);
            }
        }

        private static MobileParty FailAfterCreation(
            PartySettlementBindingBehavior bindings,
            AiForcePlanningBehavior planning, Hero leader,
            MobileParty party, PartyCreationSiteResult site, string reason)
        {
            planning.CancelTask(party, reason);
            bindings.ReleasePartyBindings(party, reason);
            if (party?.IsActive == true) DestroyPartyAction.Apply(null, party);
            PartyCreationDiagnostics.Log(leader, party,
                site.Settlement.StringId, "Failed", reason,
                site.EstimatedGarrisonRegulars,
                site.EstimatedFiefRegulars,
                site.EstimatedCombinedRegulars);
            return null;
        }

    }
}
