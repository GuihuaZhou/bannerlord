using System;
using System.Reflection;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Population.Patches
{
    [HarmonyPatch]
    internal static class NativeRecruitmentPopulationPatch
    {
        private sealed class RecruitmentPopulationState
        {
            public Settlement Settlement;
            public MobileParty Party;
            public CharacterObject Troop;
            public int ExpectedCount;
            public int RosterBefore;
            public string OperationId;
            public bool Reserved;
            public string SourceKind;
        }

        private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(
            typeof(RecruitmentCampaignBehavior), "ApplyInternal", new[]
            {
                typeof(MobileParty), typeof(Settlement), typeof(Hero),
                typeof(CharacterObject), typeof(int), typeof(int),
                typeof(RecruitmentCampaignBehavior.RecruitingDetail)
            });

        private static bool Prefix(MobileParty side1Party,
            Settlement settlement, Hero individual, CharacterObject troop,
            int number, RecruitmentCampaignBehavior.RecruitingDetail detail,
            out RecruitmentPopulationState __state)
        {
            __state = new RecruitmentPopulationState
            {
                Settlement = settlement,
                Party = side1Party,
                Troop = troop,
                ExpectedCount = number,
                RosterBefore = side1Party?.MemberRoster?
                    .GetElementNumber(troop) ?? 0,
                SourceKind = detail.ToString()
            };
            if (detail == RecruitmentCampaignBehavior.RecruitingDetail
                    .MercenaryFromTavern)
                return true;
            if (detail == RecruitmentCampaignBehavior.RecruitingDetail
                    .VolunteerFromMap)
            {
                ModLogger.FileDebug(string.Format(
                    "Native recruitment population not charged: party={0}, " +
                    "partyId={1}, troop={2}, count={3}, source={4}, " +
                    "reason=MapSourceHasNoLocalSettlement.", side1Party?.Name,
                    side1Party?.StringId, troop?.Name, number, detail));
                return true;
            }
            if (individual == null || settlement == null || troop == null ||
                number <= 0 || troop.Occupation != Occupation.Soldier ||
                side1Party?.MemberRoster == null)
                return true;

            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (population == null) return Reject(__state,
                "PopulationServiceUnavailable");
            __state.OperationId = "native-recruitment:" +
                Guid.NewGuid().ToString("N");
            PopulationTransactionResult result;
            try
            {
                if (settlement.IsFortification)
                {
                    int burdenCost = MilitaryConfigManager.Instance
                        .RequireActiveConfig().GetDoctrine(settlement)
                        .GetProfessionalBurdenCost(troop);
                    result = population.TryRecruitProfessional(settlement,
                        number, burdenCost, __state.OperationId);
                }
                else if (settlement.IsVillage)
                    result = population.TryConsume(settlement, number,
                        __state.OperationId);
                else return Reject(__state,
                    "UnsupportedRecruitmentSettlement");
            }
            catch (Exception exception)
            {
                return Reject(__state, "PopulationReservationException:" +
                    exception.GetType().Name);
            }
            __state.Reserved = result?.IsCommitted == true;
            if (!__state.Reserved)
                return Reject(__state, result?.ReasonCode ??
                    "PopulationReservationRejected");
            ModLogger.FileDebug(string.Format(
                "Native recruitment population reserved: settlement={0}, " +
                "settlementId={1}, party={2}, partyId={3}, troop={4}, " +
                "tier={5}, count={6}, source={7}, operationId={8}.",
                settlement.Name, settlement.StringId, side1Party.Name,
                side1Party.StringId, troop.Name, troop.Tier, number, detail,
                __state.OperationId));
            return true;
        }

        private static void Postfix(RecruitmentPopulationState __state)
        {
            if (__state?.Reserved != true) return;
            int after = __state.Party.MemberRoster.GetElementNumber(
                __state.Troop);
            int added = Math.Max(0, after - __state.RosterBefore);
            if (added == __state.ExpectedCount) return;
            Compensate(__state, "NativeRosterDeltaMismatch", added);
        }

        private static Exception Finalizer(Exception __exception,
            RecruitmentPopulationState __state)
        {
            if (__exception != null && __state?.Reserved == true)
                Compensate(__state, "NativeRecruitmentException", -1);
            return __exception;
        }

        private static void Compensate(RecruitmentPopulationState state,
            string reason, int actualAdded)
        {
            PopulationTransactionResult result =
                SettlementPopulationServiceRegistry.Current?.TryCompensate(
                    state.Settlement, state.OperationId);
            ModLogger.FileError(string.Format(
                "Native recruitment population compensation: settlement={0}, " +
                "settlementId={1}, party={2}, partyId={3}, troop={4}, " +
                "expected={5}, actualAdded={6}, source={7}, operationId={8}, " +
                "reason={9}, compensationStatus={10}, compensationReason={11}.",
                state.Settlement?.Name, state.Settlement?.StringId,
                state.Party?.Name, state.Party?.StringId, state.Troop?.Name,
                state.ExpectedCount, actualAdded, state.SourceKind,
                state.OperationId, reason, result?.Status,
                result?.ReasonCode ?? "none"));
            state.Reserved = false;
        }

        private static bool Reject(RecruitmentPopulationState state,
            string reason)
        {
            ModLogger.FileWarn(string.Format(
                "Native recruitment blocked before mutation: settlement={0}, " +
                "settlementId={1}, party={2}, partyId={3}, troop={4}, " +
                "count={5}, source={6}, operationId={7}, reason={8}.",
                state.Settlement?.Name, state.Settlement?.StringId,
                state.Party?.Name, state.Party?.StringId, state.Troop?.Name,
                state.ExpectedCount, state.SourceKind,
                state.OperationId ?? "missing", reason));
            return false;
        }
    }
}
