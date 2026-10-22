using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>Executes the selected recruitment source only after arrival.</summary>
    public sealed class UnifiedRecruitmentExecutionService
    {
        /// <summary>
        /// Rebuilds all volatile approvals from live state, then executes one
        /// source. A failed approval never falls back to another algorithm.
        /// </summary>
        public PartyExchangeExecutionResult Execute(
            MobileParty party,
            Settlement settlement,
            PartyForceSourceKind sourceKind,
            string operationId)
        {
            if (party?.CurrentSettlement != settlement ||
                party.ActualClan == null || settlement == null)
                return Failed("NotAtPlannedSettlement");

            int requested = ForceManagementFacade.Current
                .GetPartyTarget(party).GapCount;
            if (requested <= 0) return Failed("ForceTargetSatisfied");
            switch (sourceKind)
            {
                case PartyForceSourceKind.Fief:
                    return ExecuteFief(party, settlement, requested, operationId);
                case PartyForceSourceKind.Professional:
                    return ExecuteGarrison(
                        party, settlement, requested, operationId);
                case PartyForceSourceKind.Mercenary:
                    return ExecuteMercenary(party, settlement, requested);
                default:
                    return Failed("UnsupportedRecruitmentSource");
            }
        }

        /// <summary>Builds and executes an authoritative fief mobilization.</summary>
        private static PartyExchangeExecutionResult ExecuteFief(
            MobileParty party,
            Settlement settlement,
            int requested,
            string operationId)
        {
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null) return Failed("MissingFiefManager");
            PartyTroopSource source = new PartyTroopSource
            {
                Kind = PartyTroopSourceKind.Fief,
                Settlement = settlement,
                Roster = manager.GetFiefTroopRoster(settlement)
            };
            PartyTroopSource destination = MobileSource(
                party, settlement, PartyTroopSourceKind.Mobile);
            return BuildAndExecute(new PartyExchangeContext
            {
                OperationId = operationId,
                Initiator = party,
                Operation = PartyExchangeOperation.FiefMobilization,
                Intent = PartyForceIntent.Recruit,
                Purpose = PartyExchangePurpose.NaturalVisit,
                Source = source,
                Destination = destination,
                TargetSettlement = settlement,
                SourceSnapshot = Runner.CaptureSnapshot(source),
                DestinationSnapshot = Runner.CaptureSnapshot(destination),
                RequestedCount = requested,
                Today = CurrentDay()
            });
        }

        /// <summary>Builds and executes a protected garrison withdrawal.</summary>
        private static PartyExchangeExecutionResult ExecuteGarrison(
            MobileParty party,
            Settlement settlement,
            int requested,
            string operationId)
        {
            MobileParty garrison = settlement.Town?.GarrisonParty;
            if (garrison == null) return Failed("MissingGarrison");
            PartyTroopSource source = MobileSource(
                garrison, settlement, PartyTroopSourceKind.Garrison);
            PartyTroopSource destination = MobileSource(
                party, settlement, PartyTroopSourceKind.Mobile);
            return BuildAndExecute(new PartyExchangeContext
            {
                OperationId = operationId,
                Initiator = party,
                Operation = PartyExchangeOperation.GarrisonToField,
                Intent = PartyForceIntent.Recruit,
                Purpose = PartyExchangePurpose.NaturalVisit,
                Source = source,
                Destination = destination,
                TargetSettlement = settlement,
                SourceSnapshot = Runner.CaptureSnapshot(source),
                DestinationSnapshot = Runner.CaptureSnapshot(destination),
                RequestedCount = requested,
                Today = CurrentDay()
            });
        }

        /// <summary>Revalidates and purchases the current town mercenary offer.</summary>
        private static PartyExchangeExecutionResult ExecuteMercenary(
            MobileParty party,
            Settlement settlement,
            int requested)
        {
            RecruitmentCampaignBehavior behavior = Campaign.Current?
                .GetCampaignBehavior<RecruitmentCampaignBehavior>();
            if (behavior == null || settlement?.Town == null)
                return Failed("MissingMercenaryMarket");
            var market = behavior.GetMercenaryData(settlement.Town);
            if (!market.HasAvailableMercenary(Occupation.Mercenary) ||
                market.TroopType == null)
                return Failed("NoMercenariesAvailable");
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                party,
                new[]
                {
                    new RecruitmentCandidate(
                        market.TroopType,
                        Math.Min(requested, market.Number),
                        RecruitmentSource.Mercenary)
                });
            RecruitmentEvaluationResult approval = plan.Evaluations
                .FirstOrDefault();
            int count = Math.Max(0, approval?.RecruitableCount ?? 0);
            if (count <= 0) return Failed("MercenaryAdmissionRejected");
            int cost = approval.UnitRecruitmentCost;
            GiveGoldAction.ApplyBetweenCharacters(
                party.LeaderHero, null, count * cost, true);
            market.ChangeMercenaryCount(-count);
            party.AddElementToMemberRoster(market.TroopType, count, false);
            CampaignEventDispatcher.Instance.OnTroopRecruited(
                party.LeaderHero,
                settlement,
                null,
                market.TroopType,
                count);
            ClanRecruitmentBudgetManager.CommitRecruitment(
                party, count, cost, approval.UnitDailyWage);
            ModLogger.FileNotice(string.Format(
                "[统一招兵] {0}在{1}招募了{2}名{3}，本次花费{4}第纳尔。",
                PartyLogFormatter.GetDisplayName(party),
                settlement.Name,
                count,
                market.TroopType.Name,
                count * cost));
            return new PartyExchangeExecutionResult
            {
                Executed = true,
                ReasonCode = "Executed",
                TransferredCount = count
            };
        }

        /// <summary>Submits the request through the pure model and guarded executor.</summary>
        private static PartyExchangeExecutionResult BuildAndExecute(
            PartyExchangeContext context)
        {
            PartyExchangePlan plan = ForceManagementFacade.Current
                .PartyExchange.BuildPlan(context);
            return Runner.Execute(plan);
        }

        /// <summary>Creates a live mobile-party source descriptor.</summary>
        private static PartyTroopSource MobileSource(
            MobileParty party,
            Settlement settlement,
            PartyTroopSourceKind kind) => new PartyTroopSource
        {
            Kind = kind,
            Party = party,
            Settlement = settlement,
            Roster = party?.MemberRoster
        };

        private static PartyExchangeService Runner => ForceManagementFacade
            .Current.PartyExchangeRunner;

        /// <summary>Creates a non-mutating failed result.</summary>
        private static PartyExchangeExecutionResult Failed(string reason) =>
            new PartyExchangeExecutionResult { ReasonCode = reason };

        /// <summary>Returns the current whole campaign day.</summary>
        private static int CurrentDay() => (int)CampaignTime.Now.ToDays;
    }
}
