using ModifiedArmy.Garrison.Models;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.common;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyFinance.Models;
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

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Executes conservative one-way transfers of existing professional or
    /// mercenary troops. Fief-service batches remain with their field party.
    /// </summary>
    public sealed class TroopTransferService
    {
        private const float MinimumExchangeBenefit = 1f;

        public ExchangeExecutionResult TryExecuteNaturalVisit(
            MobileParty fieldParty,
            Settlement settlement)
        {
            if (MilitaryConfigManager.Instance.GetActiveConfig()?
                    .AiRecruitment.ForceManagement.EnableAutomaticSwap != true)
            {
                return new ExchangeExecutionResult
                {
                    ReasonCode = "AutomaticSwapDisabled"
                };
            }
            if (fieldParty == null || settlement == null ||
                fieldParty.CurrentSettlement != settlement ||
                fieldParty.ActualClan != settlement.OwnerClan)
            {
                return new ExchangeExecutionResult
                {
                    ReasonCode = "NotNaturalClanVisit"
                };
            }

            PartyForceTargetResult target = ForceManagementFacade.Current
                .GetPartyTarget(fieldParty);
            PartyForceSnapshot field = new PartyForceSnapshot
            {
                Party = fieldParty,
                CurrentCount = GetRegularCount(fieldParty),
                TargetCount = target.TargetCount,
                HardLimit = target.PartyLimit
            };
            GarrisonDefenseSnapshot garrison = ForceManagementFacade.Current
                .GarrisonDefense.Evaluate(settlement);
            ExchangePlan plan = ForceManagementFacade.Current.Exchanges
                .BuildPlan(field, garrison);
            return plan?.ShouldExecute == true
                ? ExecuteExchange(plan)
                : new ExchangeExecutionResult
                {
                    ReasonCode = plan?.ReasonCode ?? "NoPlan"
                };
        }

        public ExchangePlan PrepareExchangePlan(
            MobileParty fieldParty,
            Settlement settlement,
            int fieldTarget,
            float benefitScore,
            string reasonCode)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                .GarrisonDefense.Evaluate(settlement);
            return new ExchangePlan
            {
                ShouldExecute = true,
                ReasonCode = reasonCode,
                FieldParty = fieldParty,
                GarrisonParty = garrison,
                Settlement = settlement,
                Clan = fieldParty?.ActualClan,
                FieldRosterFingerprint = GetRosterFingerprint(fieldParty),
                GarrisonRosterFingerprint = GetRosterFingerprint(garrison),
                ExpectedFieldCount = GetRegularCount(fieldParty),
                ExpectedGarrisonCount = defense?.ResidentCount ?? 0,
                ExpectedFieldTarget = Math.Max(0, fieldTarget),
                ExpectedFieldHardLimit = Math.Max(
                    0,
                    fieldParty?.Party.PartySizeLimit ?? 0),
                ExpectedGarrisonMinimum = defense?.MinimumCount ?? 0,
                ExpectedGarrisonUpperTarget =
                    GarrisonRecruitmentCapacity.GetUpperTarget(
                        settlement,
                        defense?.HardLimit ?? 0),
                ExpectedGarrisonEscortCount =
                    defense?.ActiveSupplyEscortCount ?? 0,
                BenefitScore = benefitScore
            };
        }

        public ExchangeValidationResult ValidateExchangePlan(
            ExchangePlan plan)
        {
            if (plan == null || !plan.ShouldExecute)
            {
                return Invalid(plan?.ReasonCode ?? "MissingPlan");
            }
            MobileParty field = plan.FieldParty;
            MobileParty garrison = plan.GarrisonParty;
            Settlement settlement = plan.Settlement;
            if (field == null || garrison == null || settlement?.Town == null ||
                settlement.Town.GarrisonParty != garrison)
            {
                return Invalid("InvalidParties");
            }
            if (!field.IsActive || !garrison.IsActive || !field.IsLordParty ||
                field.ActualClan == null || field.ActualClan != plan.Clan ||
                settlement.OwnerClan != plan.Clan ||
                garrison.ActualClan != plan.Clan)
            {
                return Invalid("OwnershipChanged");
            }
            if (field.CurrentSettlement != settlement || field.MapEvent != null ||
                garrison.MapEvent != null || settlement.IsUnderSiege ||
                field.Army != null)
            {
                return Invalid("UnsafeOrNoLongerLocal");
            }
            if (plan.BenefitScore <= MinimumExchangeBenefit)
            {
                return Invalid("NoPositiveBenefit");
            }
            if (plan.FieldToGarrison.TotalCount <= 0 ||
                plan.GarrisonToField.TotalCount <= 0)
            {
                return Invalid("BidirectionalExchangeRequired");
            }
            if (GetRosterFingerprint(field) != plan.FieldRosterFingerprint ||
                GetRosterFingerprint(garrison) != plan.GarrisonRosterFingerprint ||
                GetRegularCount(field) != plan.ExpectedFieldCount ||
                GetRegularCount(garrison) != plan.ExpectedGarrisonCount)
            {
                return Invalid("RosterChanged");
            }

            ExchangeValidationResult manifestCheck = ValidateManifests(
                plan,
                GetProtectedCounts(field),
                GetProtectedCounts(garrison));
            if (!manifestCheck.IsValid)
            {
                return manifestCheck;
            }

            int finalField = GetRegularCount(field) -
                plan.FieldToGarrison.TotalCount +
                plan.GarrisonToField.TotalCount;
            int finalGarrisonResident = GetRegularCount(garrison) -
                plan.GarrisonToField.TotalCount +
                plan.FieldToGarrison.TotalCount;
            int finalGarrisonEffective = finalGarrisonResident +
                Math.Max(0, plan.ExpectedGarrisonEscortCount);
            int currentFieldLimit = Math.Max(0, field.Party.PartySizeLimit);
            int currentGarrisonLimit = Math.Max(
                0,
                garrison.Party.PartySizeLimit);
            int currentUpper = GarrisonRecruitmentCapacity.GetUpperTarget(
                settlement,
                currentGarrisonLimit);
            int currentMinimum = GarrisonRecruitmentCapacity.GetLowerTarget(
                settlement,
                currentGarrisonLimit);
            if (currentFieldLimit != plan.ExpectedFieldHardLimit ||
                currentUpper != plan.ExpectedGarrisonUpperTarget ||
                currentMinimum != plan.ExpectedGarrisonMinimum)
            {
                return Invalid("CapacityChanged");
            }
            if (finalField < 0 || finalField > currentFieldLimit ||
                finalField > plan.ExpectedFieldTarget)
            {
                return Invalid("FieldCapacityExceeded");
            }
            if (finalGarrisonEffective < currentMinimum ||
                finalGarrisonEffective > currentUpper ||
                finalGarrisonResident > currentGarrisonLimit)
            {
                return Invalid("GarrisonEstablishmentViolation");
            }
            if (!IsFinalWageAffordable(field,
                    plan.FieldToGarrison,
                    plan.GarrisonToField) ||
                !IsFinalWageAffordable(garrison,
                    plan.GarrisonToField,
                    plan.FieldToGarrison))
            {
                return Invalid("WageLimitExceeded");
            }
            return ExchangeValidationResult.Valid();
        }

        public ExchangeExecutionResult ExecuteExchange(ExchangePlan plan)
        {
            ExchangeValidationResult validation = ValidateExchangePlan(plan);
            if (!validation.IsValid)
            {
                LogExchange(plan, false, validation.ReasonCode);
                return new ExchangeExecutionResult
                {
                    ReasonCode = validation.ReasonCode
                };
            }

            var applied = new List<Action>();
            try
            {
                ApplyRemovals(plan.FieldParty, plan.FieldToGarrison, applied);
                ApplyRemovals(plan.GarrisonParty, plan.GarrisonToField, applied);
                ApplyAdditions(plan.GarrisonParty, plan.FieldToGarrison, applied);
                ApplyAdditions(plan.FieldParty, plan.GarrisonToField, applied);
                LogExchange(plan, true, "Executed");
                return new ExchangeExecutionResult
                {
                    Executed = true,
                    ReasonCode = "Executed",
                    FieldToGarrisonCount = plan.FieldToGarrison.TotalCount,
                    GarrisonToFieldCount = plan.GarrisonToField.TotalCount
                };
            }
            catch (Exception exception)
            {
                for (int index = applied.Count - 1; index >= 0; index--)
                {
                    try { applied[index](); } catch { }
                }
                LogExchange(
                    plan,
                    false,
                    "RolledBack:" + exception.GetType().Name);
                return new ExchangeExecutionResult
                {
                    ReasonCode = "ExecutionRolledBack"
                };
            }
        }

        public int TransferFieldToGarrison(
            MobileParty source,
            Settlement destination,
            int requestedCount)
        {
            MobileParty garrison = destination?.Town?.GarrisonParty;
            if (source == null || garrison == null || requestedCount <= 0 ||
                !source.IsActive || !garrison.IsActive ||
                source.ActualClan == null ||
                source.ActualClan != destination.OwnerClan ||
                garrison.ActualClan != destination.OwnerClan ||
                source.CurrentSettlement != destination ||
                source.MapEvent != null || garrison.MapEvent != null ||
                destination.IsUnderSiege)
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

            int allowed = GarrisonTransferPolicyModel.GetAllowedDonationCount(
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

        private static void LogOneWayTransfer(
            MobileParty source,
            Settlement destination,
            int requested,
            int actual,
            int protectedFiefCount,
            string reason)
        {
            ModLogger.FileNotice(string.Format(
                "One-way troop transfer: direction=FieldToGarrison, " +
                "party={0}, settlement={1}, requested={2}, actual={3}, " +
                "protectedFiefCount={4}, reason={5}",
                source?.StringId ?? "unknown",
                destination?.StringId ?? "unknown",
                Math.Max(0, requested),
                Math.Max(0, actual),
                Math.Max(0, protectedFiefCount),
                reason ?? "unknown"));
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

        private static ExchangeValidationResult ValidateManifests(
            ExchangePlan plan,
            IReadOnlyDictionary<CharacterObject, int> fieldProtected,
            IReadOnlyDictionary<CharacterObject, int> garrisonProtected)
        {
            var overlap = plan.FieldToGarrison.Troops.Keys.Intersect(
                plan.GarrisonToField.Troops.Keys);
            if (overlap.Any()) return Invalid("SameTroopInBothDirections");
            ExchangeValidationResult field = ValidateManifest(
                plan.FieldParty,
                plan.FieldToGarrison,
                fieldProtected);
            return field.IsValid
                ? ValidateManifest(
                    plan.GarrisonParty,
                    plan.GarrisonToField,
                    garrisonProtected)
                : field;
        }

        private static ExchangeValidationResult ValidateManifest(
            MobileParty source,
            TroopExchangeManifest manifest,
            IReadOnlyDictionary<CharacterObject, int> protectedCounts)
        {
            foreach (var pair in manifest.Troops)
            {
                CharacterObject troop = pair.Key;
                TroopExchangeAmount amount = pair.Value;
                if (troop == null || troop.IsHero || amount == null ||
                    amount.Count <= 0 || amount.WoundedCount < 0 ||
                    amount.WoundedCount > amount.Count)
                {
                    return Invalid("InvalidManifest");
                }
                TroopRosterElement element = source.MemberRoster
                    .GetTroopRoster()
                    .FirstOrDefault(item => item.Character == troop);
                int protectedCount = protectedCounts.TryGetValue(
                    troop,
                    out int value) ? Math.Max(0, value) : 0;
                int available = Math.Max(0, element.Number - protectedCount);
                if (amount.Count > available ||
                    amount.WoundedCount > element.WoundedNumber ||
                    amount.Count - amount.WoundedCount >
                        element.Number - element.WoundedNumber)
                {
                    return Invalid("TroopUnavailable");
                }
            }
            return ExchangeValidationResult.Valid();
        }

        private static Dictionary<CharacterObject, int> GetProtectedCounts(
            MobileParty party)
        {
            return Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                .GetServingTroopCounts(party) ?? new Dictionary<CharacterObject, int>();
        }

        private static bool IsFinalWageAffordable(
            MobileParty party,
            TroopExchangeManifest outgoing,
            TroopExchangeManifest incoming)
        {
            float wage = AiRecruitmentFinancialModel.GetLongTermDailyWage(party);
            foreach (var pair in outgoing.Troops)
            {
                wage -= AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                    party,
                    pair.Key) * pair.Value.Count;
            }
            foreach (var pair in incoming.Troops)
            {
                wage += AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                    party,
                    pair.Key) * pair.Value.Count;
            }
            return wage <= Math.Max(0, party.PaymentLimit) *
                RecruitmentBudget.WageLimitUsageRatio;
        }

        private static void ApplyRemovals(
            MobileParty party,
            TroopExchangeManifest manifest,
            ICollection<Action> rollback)
        {
            foreach (var pair in manifest.Troops)
            {
                CharacterObject troop = pair.Key;
                int count = pair.Value.Count;
                int wounded = pair.Value.WoundedCount;
                party.MemberRoster.AddToCounts(
                    troop, -count, false, -wounded, 0, true, -1);
                rollback.Add(() => party.MemberRoster.AddToCounts(
                    troop, count, false, wounded, 0, true, -1));
            }
        }

        private static void ApplyAdditions(
            MobileParty party,
            TroopExchangeManifest manifest,
            ICollection<Action> rollback)
        {
            foreach (var pair in manifest.Troops)
            {
                CharacterObject troop = pair.Key;
                int count = pair.Value.Count;
                int wounded = pair.Value.WoundedCount;
                party.MemberRoster.AddToCounts(
                    troop, count, false, wounded, 0, true, -1);
                rollback.Add(() => party.MemberRoster.AddToCounts(
                    troop, -count, false, -wounded, 0, true, -1));
            }
        }

        private static int GetRegularCount(MobileParty party) =>
            Math.Max(0, party?.MemberRoster?.TotalRegulars ?? 0);

        private static ExchangeValidationResult Invalid(string reason) =>
            ExchangeValidationResult.Invalid(reason);

        private static void LogExchange(
            ExchangePlan plan,
            bool executed,
            string reason)
        {
            ModLogger.FileNotice(string.Format(
                "Party-garrison exchange {0}: party={1}, settlement={2}, " +
                "fieldToGarrison={3}, garrisonToField={4}, benefit={5:0.##}, reason={6}",
                executed ? "executed" : "aborted",
                plan?.FieldParty?.StringId ?? "unknown",
                plan?.Settlement?.StringId ?? "unknown",
                FormatManifest(plan?.FieldToGarrison),
                FormatManifest(plan?.GarrisonToField),
                plan?.BenefitScore ?? 0f,
                reason ?? "unknown"));
        }

        private static string FormatManifest(TroopExchangeManifest manifest)
        {
            if (manifest == null || manifest.Troops.Count == 0)
            {
                return "none";
            }
            return string.Join(
                ",",
                manifest.Troops
                    .OrderBy(pair => pair.Key?.StringId)
                    .Select(pair => string.Format(
                        "{0}:{1}(wounded:{2})",
                        pair.Key?.StringId ?? "unknown",
                        pair.Value?.Count ?? 0,
                        pair.Value?.WoundedCount ?? 0)));
        }
    }
}
