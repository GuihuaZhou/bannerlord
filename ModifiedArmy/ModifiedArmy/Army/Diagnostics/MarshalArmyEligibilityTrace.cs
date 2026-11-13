using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ModifiedArmy.Army.Services;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ModifiedArmy.Army.Diagnostics
{
    internal sealed class MarshalArmyEligibilityTrace
    {
        private static readonly Dictionary<string, int> LastDetailDay = new Dictionary<string, int>();
        private static readonly Dictionary<string, HashSet<string>> SeenCandidateReasons =
            new Dictionary<string, HashSet<string>>();
        private static int _callIndex;
        private static int _modelActiveLogged;
        [ThreadStatic]
        private static EligibilityResult _completedResult;
        private int _candidateLines;
        private int _candidateObservations;

        private MarshalArmyEligibilityTrace(MobileParty party, bool enabled, bool detailed, string traceId, string sampleKey)
        {
            Party = party;
            Enabled = enabled;
            Detailed = detailed;
            TraceId = traceId;
            SampleKey = sampleKey;
        }

        internal MobileParty Party { get; }
        internal bool Enabled { get; }
        internal bool Detailed { get; }
        internal string TraceId { get; }
        private string SampleKey { get; }

        internal static MarshalArmyEligibilityTrace Begin(MobileParty party)
        {
            Kingdom kingdom = party?.MapFaction as Kingdom;
            Hero hero = party?.LeaderHero;
            bool relevant = kingdom != null && Clan.PlayerClan?.Kingdom == kingdom && hero != null &&
                (hero == kingdom.Leader ||
                    ArmyAuthorityProviderRegistry.IsEligibleMarshal(
                        hero, kingdom));
            int day = (int)CampaignTime.Now.ToDays;
            string key = (kingdom?.StringId ?? "none") + ":" + (hero?.StringId ?? "none");
            bool detailed = relevant && (!LastDetailDay.TryGetValue(key, out int oldDay) || oldDay != day);
            if (detailed) LastDetailDay[key] = day;
            string traceId = key + ":" + day + ":" +
                System.Threading.Interlocked.Increment(ref _callIndex);
            _completedResult = null;
            return new MarshalArmyEligibilityTrace(party, relevant, detailed, traceId, key);
        }

        internal void Entry(string modelName)
        {
            if (!Enabled) return;
            Write("phase=ENTRY traceId=" + TraceId + " model=" + modelName +
                  " kingdom=" + DescribeKingdom(Party?.MapFaction as Kingdom) +
                  " hero=" + DescribeHero(Party?.LeaderHero) + " party=" + DescribeParty(Party));
        }

        internal void Gate(string stage, string reason, string values)
        {
            if (!Enabled) return;
            Write("phase=" + stage + " traceId=" + TraceId + " reasonCode=" + reason + " " + values);
        }

        internal void Candidate(MobileParty candidate, bool accepted, string reason, string values)
        {
            if (!Enabled) return;
            _candidateObservations++;
            if (!SeenCandidateReasons.TryGetValue(SampleKey, out HashSet<string> reasons))
            {
                reasons = new HashSet<string>(StringComparer.Ordinal);
                SeenCandidateReasons[SampleKey] = reasons;
            }
            bool newReason = reasons.Add(reason ?? "Unknown");
            if ((!Detailed && !newReason) || _candidateLines >= 25) return;
            _candidateLines++;
            Write("phase=" + (accepted ? "CANDIDATE_ACCEPTED" : "CANDIDATE_REJECTED") +
                  " traceId=" + TraceId + " candidate=" + DescribeParty(candidate) +
                  " leader=" + DescribeHero(candidate?.LeaderHero) +
                  " clan=" + (candidate?.ActualClan?.StringId ?? "none") +
                  " reasonCode=" + reason + " " + values);
        }

        internal void Result(bool result, string reason, int beforeClear, int returned)
        {
            _completedResult = new EligibilityResult
            {
                PartyId = Party?.StringId,
                TraceId = TraceId,
                DayIndex = (int)CampaignTime.Now.ToDays,
                Result = result,
                ReasonCode = reason ?? "Unknown",
                CandidatesBeforeClear = beforeClear,
                CandidatesReturned = returned
            };
            if (!Enabled && result) return;
            Write("phase=RESULT path=ArmyEligibility traceId=" + TraceId + " result=" + result +
                  " reasonCode=" + reason + " candidatesBeforeClear=" + beforeClear +
                  " candidatesReturned=" + returned + " omittedCandidateDetails=" +
                  Math.Max(0, _candidateObservations - _candidateLines));
        }

        internal static bool TryConsumeResult(
            MobileParty party, out string reasonCode, out string modelTraceId,
            out int beforeClear, out int returned)
        {
            EligibilityResult result = _completedResult;
            _completedResult = null;
            bool matches = result != null && party != null &&
                string.Equals(result.PartyId, party.StringId,
                    StringComparison.Ordinal) &&
                result.DayIndex == (int)CampaignTime.Now.ToDays;
            reasonCode = matches ? result.ReasonCode : "UninstrumentedModel";
            modelTraceId = matches ? result.TraceId : "none";
            beforeClear = matches ? result.CandidatesBeforeClear : -1;
            returned = matches ? result.CandidatesReturned : -1;
            return matches;
        }

        private sealed class EligibilityResult
        {
            public string PartyId { get; set; }
            public string TraceId { get; set; }
            public int DayIndex { get; set; }
            public bool Result { get; set; }
            public string ReasonCode { get; set; }
            public int CandidatesBeforeClear { get; set; }
            public int CandidatesReturned { get; set; }
        }

        internal static void Audit(string values) => Write(values);

        internal static void ModelActive(MobileParty party)
        {
            if (System.Threading.Interlocked.Exchange(ref _modelActiveLogged, 1) != 0) return;
            Write("phase=MODEL_ACTIVE model=" +
                  typeof(ModifiedArmy.Army.Models.DiagnosticArmyManagementCalculationModel).FullName +
                  " party=" + DescribeParty(party));
        }

        internal static void AuditRuntimeModelChain()
        {
            try
            {
                AuditChain("ARMY_MANAGEMENT",
                    Campaign.Current?.Models?.ArmyManagementCalculationModel,
                    typeof(ModifiedArmy.Army.Models
                        .DiagnosticArmyManagementCalculationModel));
                AuditChain("TARGET_SCORE",
                    Campaign.Current?.Models?.TargetScoreCalculatingModel,
                    typeof(ModifiedArmy.Army.Models
                        .DiagnosticTargetScoreCalculatingModel));
                Audit("phase=AUTHORITY_PROVIDER provider=" +
                    ArmyAuthorityProviderRegistry.ProviderTypeName +
                    " fallback=" +
                    (ArmyAuthorityProviderRegistry.ProviderTypeName == "none"
                        ? "VANILLA_AUTHORITY" : "REGISTERED_POLICY"));
                AuditModelPatch(typeof(DefaultArmyManagementCalculationModel),
                    nameof(DefaultArmyManagementCalculationModel.CanLordCreateArmy));
                AuditModelPatch(typeof(DefaultTargetScoreCalculatingModel),
                    nameof(DefaultTargetScoreCalculatingModel
                        .GetTargetScoreForFaction));
            }
            catch (Exception exception)
            {
                Audit("phase=MODEL_CHAIN_AUDIT_FAILED exception=" +
                    exception.GetType().Name);
            }
        }

        private static void AuditChain(
            string kind, object current, Type expectedType)
        {
            var names = new List<string>();
            var seen = new HashSet<object>();
            bool cycle = false;
            for (int depth = 0; current != null && depth < 12; depth++)
            {
                if (!seen.Add(current)) { cycle = true; break; }
                names.Add(current.GetType().FullName);
                PropertyInfo property = current.GetType().GetProperty(
                    "BaseModel", BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                current = property?.GetValue(current);
            }
            Audit("phase=MODEL_CHAIN kind=" + kind +
                " activeModel=" + (names.FirstOrDefault() ?? "none") +
                " chain=\"" + string.Join(" -> ", names) + "\" depth=" +
                names.Count + " cycleDetected=" + cycle +
                " diagnosticPresent=" + names.Contains(expectedType.FullName) +
                " navalPresent=" + names.Any(item => item?.Contains(
                    "NavalDLC") == true));
        }

        private static void AuditModelPatch(Type type, string methodName)
        {
            MethodInfo method = AccessTools.Method(type, methodName);
            HarmonyLib.Patches patches = method == null
                ? null
                : Harmony.GetPatchInfo(method);
            string owners = patches == null ? "none" : string.Join(",",
                patches.Owners.Where(owner => owner == "com.mod.ModifiedArmy" ||
                    owner == "com.mod.ModifiedPolitics"));
            Audit("phase=MODEL_PATCH_AUDIT target=" + type.FullName + "." +
                methodName + " armyPoliticsPatchOwners=" +
                (string.IsNullOrEmpty(owners) ? "none" : owners));
        }

        internal static string F(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        internal static string DescribeParty(MobileParty party) => party == null ? "none" :
            Quote(party.Name?.ToString()) + "(" + (party.StringId ?? "none") + ")";
        private static string DescribeHero(Hero hero) => hero == null ? "none" :
            Quote(hero.Name?.ToString()) + "(" + (hero.StringId ?? "none") + ")";
        private static string DescribeKingdom(Kingdom kingdom) => kingdom == null ? "none" :
            Quote(kingdom.Name?.ToString()) + "(" + (kingdom.StringId ?? "none") + ")";
        private static string Quote(string value) => "\"" + (value ?? "unknown").Replace("\"", "'") + "\"";

        private static void Write(string fields)
        {
            try
            {
                string prefix = new TextObject(
                    "{=MP_ArmyEligibilityModelPrefix}[ModifiedArmy][Army eligibility model]").ToString();
                string map = new TextObject(
                    "{=MP_ArmyEligibilityFieldMap}phase=phase|traceId=traceId|model=model|kingdom=kingdom|hero=hero|party=party|candidate=candidate|leader=leader|clan=clan|result=result|reasonCode=reasonCode|influence=influence|foodDays=foodDays|threshold=threshold|partyRatio=partyRatio|distance=distance|maximum=maximum|influenceCost=influenceCost|estimatedStrength=estimatedStrength|score=score|candidateCountBeforeStrengthCheck=candidateCountBeforeStrengthCheck|leaderSiegeStrength=leaderSiegeStrength|candidatesSiegeStrength=candidatesSiegeStrength|totalSiegeStrength=totalSiegeStrength|strengthThreshold=strengthThreshold|candidatesBeforeClear=candidatesBeforeClear|candidatesReturned=candidatesReturned|eligibleBeforeSort=eligibleBeforeSort|kingdomWarPartyCount=kingdomWarPartyCount|existingArmyPartyCount=existingArmyPartyCount|maxNewMembers=maxNewMembers|afterTruncationCount=afterTruncationCount|removedCandidateIds=removedCandidateIds|sortedCandidates=sortedCandidates|omitted=omitted|omittedCandidateDetails=omittedCandidateDetails").ToString();
                string localized = fields ?? string.Empty;
                foreach (string pair in map.Split('|'))
                {
                    int separator = pair.IndexOf('=');
                    if (separator <= 0) continue;
                    localized = localized.Replace(pair.Substring(0, separator) + "=",
                        pair.Substring(separator + 1) + "=");
                }
                ModLogger.FileDebug(prefix + " " + localized);
            }
            catch
            {
                // File diagnostics must not affect native eligibility execution.
            }
        }
    }
}
