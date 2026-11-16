// Feudal troop admission: FiefPartyData is the sole writer for genuinely new
// village supply obligations. Existing service/return state transitions remain
// owned by their established workflows and preserve save identities.
using System;
using System.Linq;
using ModifiedArmy.common;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>Destination for a genuinely new village supply obligation.</summary>
    internal enum FiefTroopAdmissionDestination
    {
        Ready,
        Cooling
    }

    /// <summary>Observed result of one live, non-persistent admission attempt.</summary>
    internal sealed class FiefTroopAdmissionResult
    {
        public int AddedCount { get; internal set; }
        public string ReasonCode { get; internal set; }
        public Settlement Village { get; internal set; }
        public CharacterObject Troop { get; internal set; }
        public CharacterObject BranchRoot { get; internal set; }
    }

    public partial class FiefPartyData
    {
        /// <summary>
        /// Revalidates live branch and aggregate Hearth capacity, then commits
        /// the only writes which create a new village supply obligation.
        /// When a source party is supplied, only accepted soldiers are removed.
        /// </summary>
        internal FiefTroopAdmissionResult TryAddFeudalTroops(
            Settlement sourceVillage, CharacterObject troop,
            int requestedCount, FiefTroopAdmissionDestination destination,
            MobileParty sourceParty = null, int requestedWounded = 0)
        {
            var result = new FiefTroopAdmissionResult
            {
                Village = sourceVillage,
                Troop = troop,
                ReasonCode = "InvalidRequest"
            };
            VillageFiefData village = GetVillageData(sourceVillage);
            if (!IsRuntimeReady || village == null || village.RecoveryRequired ||
                !IsCurrentVillage(sourceVillage) || troop == null ||
                requestedCount <= 0 || requestedWounded < 0 ||
                requestedWounded > requestedCount)
            {
                LogAdmission(result, requestedCount, destination, village,
                    null, 0, 0, true);
                return result;
            }
            if (!_fiefPartyTemplate.TryGetSupplyBranch(troop, out var branch))
            {
                result.ReasonCode = "SupplyBranchUnavailable";
                LogAdmission(result, requestedCount, destination, village,
                    null, 0, 0, true);
                return result;
            }
            result.BranchRoot = branch.Troop;
            if (destination == FiefTroopAdmissionDestination.Ready &&
                branch.RequiredBarracksLevel >
                    SettlementManpowerProductionBehavior.GetBarracksLevel(
                        _settlement))
            {
                result.ReasonCode = "ProductionBranchLocked";
                LogAdmission(result, requestedCount, destination, village,
                    branch, 0, 0, false);
                return result;
            }
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            int cost = doctrine.GetFeudalSupplyCost(troop);
            if (cost <= 0)
            {
                result.ReasonCode = "SupplyCostUnavailable";
                LogAdmission(result, requestedCount, destination, village,
                    branch, cost, 0, true);
                return result;
            }
            int available = sourceParty == null ? requestedCount : Math.Min(
                requestedCount, sourceParty.MemberRoster.GetTroopCount(troop));
            int capacity = Math.Max(0, Math.Min(
                village.GetFreeSupplyHearth(_fiefPartyTemplate, doctrine),
                Math.Min(village.GetFeudalBudgetRemainingHearth(
                        _fiefPartyTemplate, doctrine),
                    village.GetSupplyBranchRemainingHearth(
                        _fiefPartyTemplate, doctrine, branch))) / cost);
            int accepted = Math.Min(available, capacity);
            if (accepted <= 0)
            {
                result.ReasonCode = "HearthCapacityUnavailable";
                LogAdmission(result, requestedCount, destination, village,
                    branch, cost, capacity, false);
                return result;
            }
            int wounded = Math.Min(accepted, requestedWounded);
            if (destination == FiefTroopAdmissionDestination.Ready)
            {
                if (village.GetReadyRoster() == null)
                {
                    result.ReasonCode = "ReadyRosterUnavailable";
                    LogAdmission(result, requestedCount, destination, village,
                        branch, cost, capacity, true);
                    return result;
                }
                village.GetReadyRoster().AddToCounts(troop, accepted, false,
                    wounded, 0, true, -1);
            }
            else
            {
                int cooldown = Math.Max(0,
                    doctrine.Service.ReturnCooldownWeeks);
                FiefReturnBatch batch = village.ReturnBatches.FirstOrDefault(
                    item => item != null && item.WaitWeeks == cooldown);
                if (batch == null)
                {
                    batch = new FiefReturnBatch(cooldown);
                    village.ReturnBatches.Add(batch);
                }
                accepted = batch.AddTroops(new System.Collections.Generic
                    .Dictionary<CharacterObject, int> { [troop] = accepted });
            }
            if (sourceParty != null && accepted > 0)
                sourceParty.MemberRoster.AddToCounts(troop, -accepted, false,
                    -wounded, 0, true, -1);
            result.AddedCount = accepted;
            result.ReasonCode = accepted == requestedCount
                ? "Accepted" : "PartiallyAccepted";
            if (accepted != requestedCount)
                LogAdmission(result, requestedCount, destination, village,
                    branch, cost, capacity, false);
            SyncManpowerCounters();
            return result;
        }

        /// <summary>
        /// Emits bounded, localized diagnostics for rejected or partial
        /// admission. Routine full success is summarized by the owning caller.
        /// </summary>
        private void LogAdmission(FiefTroopAdmissionResult result,
            int requestedCount, FiefTroopAdmissionDestination destination,
            VillageFiefData village, TroopProductionEntry branch, int cost,
            int capacity, bool warning)
        {
            if (!RecruitmentLogFilter.ShouldLog(_settlement?.OwnerClan)) return;
            int quota = 0;
            int occupied = 0;
            try
            {
                if (village != null && branch != null)
                {
                    CulturalMilitaryDoctrine doctrine = MilitaryConfigManager
                        .Instance.RequireActiveConfig().GetDoctrine(_settlement);
                    quota = village.GetSupplyBranchQuotas(
                        _fiefPartyTemplate, doctrine)[branch];
                    occupied = village.GetOccupiedSupplyBranchHearth(
                        _fiefPartyTemplate, doctrine, branch);
                }
            }
            catch (Exception)
            {
                // Diagnostics must never turn an already determined business
                // result into a second failure or repeat resource reads.
            }
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_fief_admission_result");
            message.SetTextVariable("FIEF_NAME", _settlement?.Name ??
                new TextObject("{=ModifiedArmy_UnknownFief}Unknown fief"));
            message.SetTextVariable("VILLAGE_NAME",
                village?.VillageSettlement?.Name ??
                    new TextObject("{=ModifiedArmy_UnknownVillage}" +
                        "Unknown village"));
            message.SetTextVariable("TROOP_NAME", result.Troop?.Name ??
                new TextObject("{=ModifiedArmy_UnknownTroop}Unknown troop"));
            message.SetTextVariable("BRANCH_NAME", branch?.Troop?.Name ??
                new TextObject("{=ModifiedArmy_UnknownBranch}" +
                    "Unknown branch"));
            message.SetTextVariable("DESTINATION", GameTexts.FindText(
                destination == FiefTroopAdmissionDestination.Ready
                    ? "str_modifiedarmy_fief_admission_ready"
                    : "str_modifiedarmy_fief_admission_cooling"));
            message.SetTextVariable("REQUESTED", requestedCount);
            message.SetTextVariable("ACCEPTED", result.AddedCount);
            message.SetTextVariable("CAPACITY", capacity);
            message.SetTextVariable("UNIT_COST", cost);
            message.SetTextVariable("QUOTA", quota);
            message.SetTextVariable("OCCUPIED", occupied);
            message.SetTextVariable("REASON_CODE", result.ReasonCode);
            if (warning) ModLogger.Warn(message.ToString());
            else ModLogger.Debug(message.ToString());
        }
    }
}
