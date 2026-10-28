using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Behaviors;
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
using TaleWorlds.Localization;

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
            if (!CampaignRecoveryCoordinator.CanMutateCampaignState())
                return Failed("CampaignRecoveryNotReady", operationId);
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
                    return ExecuteMercenary(
                        party, settlement, requested, operationId);
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
            int requested,
            string operationId)
        {
            TransferOperationJournalBehavior journal = Campaign.Current?
                .GetCampaignBehavior<TransferOperationJournalBehavior>();
            string domain = party?.StringId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(operationId))
                return Failed("MissingApprovedPlan");
            if (journal?.Contains(operationId) == true)
                return Failed("OperationAlreadyCompleted", operationId);
            if (journal?.IsDomainFrozen(domain) == true)
                return Failed("TransferDomainFrozenForRecovery", operationId);
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
            CharacterObject troop = market.TroopType;
            int goldBefore = party.LeaderHero?.Gold ?? 0;
            int marketBefore = market.Number;
            int rosterBefore = party.MemberRoster.GetTroopRoster()
                .FirstOrDefault(item => item.Character == troop).Number;
            Clan clan = ClanRecruitmentBudgetManager.GetSupportingClan(party);
            ClanRecruitmentBudgetSnapshot budgetBefore =
                ClanRecruitmentBudgetManager.GetSnapshot(clan);
            float purchaseBudgetBefore = budgetBefore.RemainingPurchaseBudget;
            float commitmentBudgetBefore =
                budgetBefore.RemainingCommitmentBudget;
            bool goldChanged = false;
            bool marketChanged = false;
            bool rosterChanged = false;
            try
            {
                GiveGoldAction.ApplyBetweenCharacters(
                    party.LeaderHero, null, count * cost, true);
                goldChanged = true;
                market.ChangeMercenaryCount(-count);
                marketChanged = true;
                party.AddElementToMemberRoster(troop, count, false);
                rosterChanged = true;
                ClanRecruitmentBudgetManager.CommitRecruitment(
                    party, count, cost, approval.UnitDailyWage);
            }
            catch (Exception exception)
            {
                bool restored = TryRollbackMercenaryPurchase(
                    party,
                    market,
                    troop,
                    count,
                    count * cost,
                    goldBefore,
                    marketBefore,
                    rosterBefore,
                    budgetBefore,
                    purchaseBudgetBefore,
                    commitmentBudgetBefore,
                    goldChanged,
                    marketChanged,
                    rosterChanged);
                PartyExchangeExecutionResult failure = new PartyExchangeExecutionResult
                {
                    Status = restored
                        ? TransferCommitStatus.RolledBack
                        : TransferCommitStatus.RecoveryRequired,
                    ReasonCode = restored
                        ? "MercenaryPurchaseRolledBack:" +
                            exception.GetType().Name
                        : "MercenaryPurchaseRecoveryRequired:" +
                            exception.GetType().Name,
                    RequestedCount = requested,
                    ApprovedCount = count,
                    TransferredCount = restored ? 0 : Math.Max(
                        0,
                        party.MemberRoster.GetTroopRoster()
                            .FirstOrDefault(item => item.Character == troop)
                            .Number - rosterBefore),
                    OperationId = operationId,
                    SourceFingerprint = "market:" + market.Number,
                    DestinationFingerprint =
                        TroopTransferService.GetRosterFingerprint(party),
                    FreezeDomain = domain
                };
                if (failure.Status == TransferCommitStatus.RecoveryRequired)
                    journal?.Record(failure, CurrentDay());
                return failure;
            }
            PartyExchangeExecutionResult result = new PartyExchangeExecutionResult
            {
                Status = TransferCommitStatus.Committed,
                ReasonCode = "Executed",
                RequestedCount = requested,
                ApprovedCount = count,
                TransferredCount = count,
                OperationId = operationId,
                SourceFingerprint = "market:" + market.Number,
                DestinationFingerprint =
                    TroopTransferService.GetRosterFingerprint(party),
                FreezeDomain = domain
            };
            journal?.Record(result, CurrentDay());
            try
            {
                CampaignEventDispatcher.Instance.OnTroopRecruited(
                    party.LeaderHero,
                    settlement,
                    null,
                    market.TroopType,
                    count);
                if (RecruitmentLogFilter.ShouldLog(party))
                {
                    TextObject message = GameTexts.FindText(
                        "str_modifiedarmy_unified_mercenary_recruited");
                    message.SetTextVariable(
                        "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
                    message.SetTextVariable(
                        "SETTLEMENT_NAME", settlement.Name);
                    message.SetTextVariable("COUNT", count);
                    message.SetTextVariable(
                        "TROOP_NAME", market.TroopType.Name);
                    message.SetTextVariable("COST", count * cost);
                    ModLogger.Info(message.ToString());
                }
            }
            catch (Exception notificationException)
            {
                ModLogger.Error(
                    "Mercenary purchase committed but notification failed: " +
                    notificationException);
            }
            return result;
        }

        private static bool TryRollbackMercenaryPurchase(
            MobileParty party,
            RecruitmentCampaignBehavior.TownMercenaryData market,
            CharacterObject troop,
            int count,
            int totalCost,
            int goldBefore,
            int marketBefore,
            int rosterBefore,
            ClanRecruitmentBudgetSnapshot budgetBefore,
            float purchaseBudgetBefore,
            float commitmentBudgetBefore,
            bool goldChanged,
            bool marketChanged,
            bool rosterChanged)
        {
            try
            {
                if (rosterChanged)
                    party.MemberRoster.AddToCounts(troop, -count);
                if (marketChanged)
                    market.ChangeMercenaryCount(count);
                if (goldChanged)
                    GiveGoldAction.ApplyBetweenCharacters(
                        null, party.LeaderHero, totalCost, true);
                ClanRecruitmentBudgetManager.RestoreRemainingBudget(
                    budgetBefore,
                    purchaseBudgetBefore,
                    commitmentBudgetBefore);
            }
            catch (Exception rollbackException)
            {
                ModLogger.Error("Mercenary rollback failed: " +
                    rollbackException);
                return false;
            }
            int rosterAfter = party.MemberRoster.GetTroopRoster()
                .FirstOrDefault(item => item.Character == troop).Number;
            return (party.LeaderHero?.Gold ?? 0) == goldBefore &&
                market.Number == marketBefore && rosterAfter == rosterBefore &&
                budgetBefore.RemainingPurchaseBudget == purchaseBudgetBefore &&
                budgetBefore.RemainingCommitmentBudget == commitmentBudgetBefore;
        }

        /// <summary>Submits the request through the pure model and guarded executor.</summary>
        private static PartyExchangeExecutionResult BuildAndExecute(
            PartyExchangeContext context)
        {
            Runner.PrepareContext(context);
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

        private static PartyExchangeExecutionResult Failed(
            string reason,
            string operationId) => new PartyExchangeExecutionResult
        {
            ReasonCode = reason,
            OperationId = operationId
        };

        /// <summary>Returns the current whole campaign day.</summary>
        private static int CurrentDay() => (int)CampaignTime.Now.ToDays;
    }
}
