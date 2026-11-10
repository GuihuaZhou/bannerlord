using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ModifiedArmy.Army.Services;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using CampaignArmy = TaleWorlds.CampaignSystem.Army;

namespace ModifiedArmy.Army.Diagnostics
{
    internal static class MarshalArmyDebugTrace
    {
        [ThreadStatic]
        private static TargetScoreContext _targetContext;

        private static readonly Dictionary<string, int> LastDetailedDay = new Dictionary<string, int>();
        private static readonly Dictionary<string, DecisionSample> CurrentSamples = new Dictionary<string, DecisionSample>();
        private static readonly Dictionary<string, string> LastDecisionStates = new Dictionary<string, string>();
        private static int _sequence;

        internal static bool IsRelevant(MobileParty party)
        {
            Kingdom kingdom = party?.MapFaction as Kingdom;
            Hero hero = party?.LeaderHero;
            return party?.IsActive == true && hero != null && kingdom != null && !kingdom.IsEliminated
                   && Clan.PlayerClan?.Kingdom == kingdom
                   && (hero == kingdom.Leader ||
                       ArmyAuthorityProviderRegistry.IsEligibleMarshal(
                           hero, kingdom));
        }

        internal static string BeginDecision(MobileParty party, out bool detailed)
        {
            int day = (int)CampaignTime.Now.ToDays;
            string heroId = party?.LeaderHero?.StringId ?? "none";
            string key = (party?.MapFaction?.StringId ?? "none") + ":" + heroId;
            detailed = !LastDetailedDay.TryGetValue(key, out int previousDay) || previousDay != day;
            if (detailed)
                LastDetailedDay[key] = day;
            int sequence = System.Threading.Interlocked.Increment(ref _sequence);
            string traceId = key + ":day" + day.ToString(CultureInfo.InvariantCulture)
                   + ":hour" + ((int)CampaignTime.Now.ToHours).ToString(CultureInfo.InvariantCulture)
                   + ":tick" + sequence.ToString(CultureInfo.InvariantCulture);
            CurrentSamples[key] = new DecisionSample(traceId, (int)CampaignTime.Now.ToHours, detailed);
            return traceId;
        }

        internal static IDisposable PushTargetContext(
            MobileParty party,
            CampaignArmy.ArmyTypes armyType,
            string traceId,
            bool sampled)
        {
            TargetScoreContext previous = _targetContext;
            _targetContext = new TargetScoreContext(party, armyType, traceId, sampled);
            return new ContextScope(previous);
        }

        internal static bool ShouldTraceState(MobileParty party, string reason)
        {
            string key = (party?.MapFaction?.StringId ?? "none") + ":" + (party?.LeaderHero?.StringId ?? "none");
            if (LastDecisionStates.TryGetValue(key, out string previous) && previous == reason)
                return false;
            LastDecisionStates[key] = reason;
            return true;
        }

        internal static void RecordTargetScore(
            Settlement target,
            CampaignArmy.ArmyTypes armyType,
            MobileParty party,
            float ourStrength,
            float score)
        {
            TargetScoreContext context = _targetContext;
            if (context == null || !context.Sampled || context.Party != party || context.ArmyType != armyType)
                return;

            bool positive = score > 0f && !float.IsNaN(score) && !float.IsInfinity(score);
            int limit = positive ? 5 : 12;
            if (positive ? context.PositiveLogged >= limit : context.ZeroLogged >= limit)
            {
                context.Omitted++;
                return;
            }

            if (positive)
                context.PositiveLogged++;
            else
                context.ZeroLogged++;

            Write("phase=TARGET_NATIVE traceId=" + context.TraceId
                  + " armyType=" + armyType
                  + " party=" + DescribeParty(party)
                  + " target=" + DescribeSettlement(target)
                  + " targetOwnerKingdom=" + DescribeKingdom(target?.MapFaction as Kingdom)
                  + " fortification=" + (target?.IsFortification == true)
                  + " activeSiege=" + (target?.SiegeEvent != null)
                  + " ourStrength=" + F(ourStrength)
                  + " nativeTargetScore=" + F(score)
                  + " reason=" + (score == 0f ? "NATIVE_TARGET_ZERO_REASON_UNKNOWN" : "NATIVE_TARGET_SCORE_OBSERVED")
                  + " scoreModel=" + (Campaign.Current?.Models?.TargetScoreCalculatingModel?.GetType().FullName ?? "none"));
        }

        internal static int CurrentOmitted => _targetContext?.Omitted ?? 0;

        internal static bool TryGetTargetContext(
            MobileParty party,
            CampaignArmy.ArmyTypes armyType,
            out string traceId)
        {
            TargetScoreContext context = _targetContext;
            bool matches = context != null && context.Sampled &&
                context.Party == party && context.ArmyType == armyType;
            traceId = matches ? context.TraceId : null;
            return matches;
        }

        internal static void RecordTargetResult(
            string traceId,
            Settlement target,
            MobileParty party,
            string fields)
        {
            TargetScoreContext context = _targetContext;
            if (context == null || context.TraceId != traceId ||
                context.Party != party)
                return;

            context.ScoredTargets++;
            string reason = ExtractField(fields, "zeroReasonCode") ??
                "ZERO_FINAL_SCORE_OTHER";
            context.ReasonCounts[reason] = context.ReasonCounts.TryGetValue(
                reason, out int oldCount) ? oldCount + 1 : 1;

            Write("phase=TARGET_RESULT traceId=" + traceId +
                " party=" + DescribeParty(party) +
                " target=" + DescribeSettlement(target) + " " + fields);
        }

        internal static int CurrentScoredTargets =>
            _targetContext?.ScoredTargets ?? 0;

        internal static string CurrentReasonSummary => _targetContext == null
            ? "none"
            : string.Join(",", _targetContext.ReasonCounts
                .OrderBy(item => item.Key)
                .Select(item => item.Key + ":" + item.Value));

        internal static void TraceFinalPool(MobileParty party, PartyThinkParams parameters)
        {
            if (!IsRelevant(party) || parameters?.AIBehaviorScores == null)
                return;

            string key = (party.MapFaction?.StringId ?? "none") + ":" + (party.LeaderHero?.StringId ?? "none");
            if (!CurrentSamples.TryGetValue(key, out DecisionSample sample)
                || !sample.Detailed
                || sample.PoolWritten
                || sample.Hour != (int)CampaignTime.Now.ToHours)
                return;
            sample.PoolWritten = true;

            var scores = parameters.AIBehaviorScores.ToList();
            float maxArmy = scores.Where(item => item.Item1.WillGatherArmy).Select(item => item.Item2).DefaultIfEmpty(0f).Max();
            float maxOther = scores.Where(item => !item.Item1.WillGatherArmy).Select(item => item.Item2).DefaultIfEmpty(0f).Max();
            string top = string.Join("|", scores.OrderByDescending(item => item.Item2).Take(5).Select(item =>
                item.Item1.AiBehavior + ":" + DescribeMapPoint(item.Item1.Party) + ":army="
                + item.Item1.WillGatherArmy + ":score=" + F(item.Item2)));
            Write("phase=FINAL_POOL_AFTER_EVENT traceId=" + sample.TraceId
                  + " party=" + DescribeParty(party)
                  + " poolCount=" + scores.Count
                  + " maxArmyScore=" + F(maxArmy)
                  + " maxNonArmyScore=" + F(maxOther)
                  + " armyTopIsHighest=" + (maxArmy >= maxOther && maxArmy > 0f)
                  + " doNotChangeBehavior=" + parameters.DoNotChangeBehavior
                  + " currentObjective=" + F(parameters.CurrentObjectiveValue)
                  + " defaultBehavior=" + party.DefaultBehavior
                  + " top5=[" + top + "]");
        }

        internal static void Write(string fields)
        {
            try
            {
                string prefix = new TextObject(
                    "{=MP_MarshalArmyDebugPrefix}[ModifiedArmy][Army debug]").ToString();
                ModLogger.FileDebug(prefix + " " + LocalizeFieldNames(fields));
            }
            catch
            {
                // Diagnostics must never affect campaign AI execution.
            }
        }

        private static string LocalizeFieldNames(string fields)
        {
            string map = new TextObject(
                "{=MP_MarshalArmyDebugFieldMap}phase=phase|traceId=traceId|kingdom=kingdom|hero=hero|party=party|leader=leader|reasonCode=reasonCode|reason=reason|result=result|armyType=armyType|target=target|source=source|candidates=candidates|candidateCount=candidateCount|members=members|influence=influence|foodDays=foodDays|averageFoodDays=averageFoodDays|minimumFoodDays=minimumFoodDays|sizeRatio=sizeRatio|gatheringStrength=gatheringStrength|siegeStrength=siegeStrength|nativeTargetScore=nativeTargetScore|rawScore=rawScore|finalScore=finalScore|multiplier=multiplier|poolCount=poolCount|top5=top5|allowedByMarshalPatch=allowedByMarshalPatch|isRuler=isRuler|isMarshal=isMarshal|atSea=atSea|mapEvent=mapEvent|settlementSiege=settlementSiege|boundParty=boundParty|reservationOwner=reservationOwner").ToString();
            string localized = fields ?? string.Empty;
            foreach (string pair in map.Split('|'))
            {
                int separator = pair.IndexOf('=');
                if (separator <= 0) continue;
                string key = pair.Substring(0, separator);
                string label = pair.Substring(separator + 1);
                localized = localized.Replace(key + "=", label + "=");
            }
            return localized;
        }

        internal static string DescribeParty(MobileParty party) => party == null
            ? "none"
            : Quote(party.Name?.ToString()) + "(" + (party.StringId ?? "none") + ")";

        internal static string DescribeHero(Hero hero) => hero == null
            ? "none"
            : Quote(hero.Name?.ToString()) + "(" + (hero.StringId ?? "none") + ")";

        internal static string DescribeKingdom(Kingdom kingdom) => kingdom == null
            ? "none"
            : Quote(kingdom.Name?.ToString()) + "(" + (kingdom.StringId ?? "none") + ")";

        internal static string DescribeSettlement(Settlement settlement) => settlement == null
            ? "none"
            : Quote(settlement.Name?.ToString()) + "(" + (settlement.StringId ?? "none") + ")";

        internal static string F(float value) => value.ToString("0.000000", CultureInfo.InvariantCulture);

        private static string DescribeMapPoint(object value)
        {
            if (value is Settlement settlement)
                return DescribeSettlement(settlement);
            if (value is MobileParty party)
                return DescribeParty(party);
            return value?.ToString() ?? "none";
        }

        private static string Quote(string value) => "\"" + (value ?? "unknown").Replace("\"", "'") + "\"";

        private static string ExtractField(string fields, string name)
        {
            string marker = name + "=";
            int start = (fields ?? string.Empty).IndexOf(
                marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            int end = fields.IndexOf(' ', start);
            return end < 0 ? fields.Substring(start) :
                fields.Substring(start, end - start);
        }

        private sealed class TargetScoreContext
        {
            internal TargetScoreContext(MobileParty party, CampaignArmy.ArmyTypes armyType, string traceId, bool sampled)
            {
                Party = party;
                ArmyType = armyType;
                TraceId = traceId;
                Sampled = sampled;
            }

            internal MobileParty Party { get; }
            internal CampaignArmy.ArmyTypes ArmyType { get; }
            internal string TraceId { get; }
            internal bool Sampled { get; }
            internal int ZeroLogged { get; set; }
            internal int PositiveLogged { get; set; }
            internal int Omitted { get; set; }
            internal int ScoredTargets { get; set; }
            internal Dictionary<string, int> ReasonCounts { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private sealed class ContextScope : IDisposable
        {
            private readonly TargetScoreContext _previous;
            internal ContextScope(TargetScoreContext previous) => _previous = previous;
            public void Dispose() => _targetContext = _previous;
        }

        private sealed class DecisionSample
        {
            internal DecisionSample(string traceId, int hour, bool detailed)
            {
                TraceId = traceId;
                Hour = hour;
                Detailed = detailed;
            }

            internal string TraceId { get; }
            internal int Hour { get; }
            internal bool Detailed { get; }
            internal bool PoolWritten { get; set; }
        }
    }
}
