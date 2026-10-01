using ModifiedPolitics.Diplomacy.Policies;
using ModifiedPolitics.Tool;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.Clans
{
    /// <summary>
    /// Gives exiled noble clans a daily opportunity to seek a new liege. The
    /// target is selected by cultural-territory weight, then the native barter
    /// system evaluates the clan and kingdom scores and executes the agreement.
    /// </summary>
    public sealed class ExiledClanRealignmentBehavior : CampaignBehaviorBase
    {
        private const float DailyAttemptChance = 0.35f;
        private const float BaseWeight = 1f;
        private const float SameKingdomCultureWeight = 2f;
        private const float SameCultureTownWeight = 3f;
        private const float SameCultureCastleWeight = 1f;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(
                this,
                OnDailyTickClan);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnDailyTickClan(Clan clan)
        {
            if (!ExiledClanPolicy.IsEligibleExiledClan(clan))
            {
                return;
            }

            // Do not change allegiance while the clan is committed to a
            // kingdom or while one of its war parties is in a map event.
            if (!clan.ShouldStayInKingdomUntil.IsPast ||
                clan.WarPartyComponents.Any(component =>
                    component?.MobileParty?.MapEvent != null))
            {
                LogTemporarilyBlocked(clan);
                return;
            }

            if (MBRandom.RandomFloat >= DailyAttemptChance)
            {
                LogSkipped(clan);
                return;
            }

            List<KingdomCandidate> candidates = Kingdom.All
                .Where(kingdom => IsValidTarget(clan, kingdom))
                .Select(kingdom => new KingdomCandidate(
                    kingdom,
                    CalculateWeight(clan, kingdom)))
                .Where(candidate => candidate.Weight > 0f)
                .ToList();
            KingdomCandidate selected = SelectWeightedCandidate(candidates);
            if (selected == null)
            {
                LogNoTarget(clan);
                return;
            }

            JoinKingdomAsClanBarterable agreement =
                new JoinKingdomAsClanBarterable(
                    clan.Leader,
                    selected.Kingdom,
                    false);
            int clanScore = agreement.GetValueForFaction(clan);
            int kingdomScore = agreement.GetValueForFaction(
                selected.Kingdom);
            int combinedScore = clanScore + kingdomScore;
            LogCandidate(
                clan,
                selected,
                clanScore,
                kingdomScore,
                combinedScore);

            if (combinedScore <= 0)
            {
                return;
            }

            Kingdom target = selected.Kingdom;
            Campaign.Current.BarterManager.ExecuteAiBarter(
                clan,
                target,
                clan.Leader,
                target.Leader,
                agreement);

            if (clan.Kingdom == target)
            {
                LogJoined(clan, target);
            }
            else
            {
                LogBarterFailed(clan, target);
            }
        }

        private static bool IsValidTarget(Clan clan, Kingdom kingdom)
        {
            return kingdom != null &&
                   !kingdom.IsEliminated &&
                   kingdom.RulingClan != null &&
                   kingdom.Leader != null &&
                   clan.MapFaction != kingdom &&
                   !clan.IsAtWarWith(kingdom) &&
                   !Campaign.Current.Models.DiplomacyModel
                       .IsAtConstantWar(clan, kingdom);
        }

        /// <summary>
        /// A kingdom is more likely to be considered when it controls more of
        /// the clan culture's towns and castles.
        /// </summary>
        private static float CalculateWeight(Clan clan, Kingdom kingdom)
        {
            int towns = kingdom.Settlements.Count(settlement =>
                settlement.IsTown && settlement.Culture == clan.Culture);
            int castles = kingdom.Settlements.Count(settlement =>
                settlement.IsCastle && settlement.Culture == clan.Culture);

            return BaseWeight +
                   (kingdom.Culture == clan.Culture
                       ? SameKingdomCultureWeight
                       : 0f) +
                   towns * SameCultureTownWeight +
                   castles * SameCultureCastleWeight;
        }

        private static KingdomCandidate SelectWeightedCandidate(
            IReadOnlyList<KingdomCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float totalWeight = candidates.Sum(candidate => candidate.Weight);
            float roll = MBRandom.RandomFloat * totalWeight;
            foreach (KingdomCandidate candidate in candidates)
            {
                roll -= candidate.Weight;
                if (roll <= 0f)
                {
                    return candidate;
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static void LogSkipped(Clan clan)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanAttemptSkipped}" +
                "[Clan Realignment] {CLAN_NAME} did not seek a new kingdom " +
                "today.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            ModLogger.Debug(message.ToString());
        }

        private static void LogTemporarilyBlocked(Clan clan)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanTemporarilyBlocked}" +
                "[Clan Realignment] {CLAN_NAME} cannot seek a new kingdom " +
                "while committed to a faction or involved in battle.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            ModLogger.Debug(message.ToString());
        }

        private static void LogNoTarget(Clan clan)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanNoTarget}" +
                "[Clan Realignment] {CLAN_NAME} currently has no valid " +
                "kingdom to join.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            ModLogger.Info(message.ToString());
        }

        private static void LogCandidate(
            Clan clan,
            KingdomCandidate candidate,
            int clanScore,
            int kingdomScore,
            int combinedScore)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanCandidate}" +
                "[Clan Realignment] {CLAN_NAME} is negotiating with " +
                "{KINGDOM_NAME}. Target weight is {WEIGHT}, clan willingness " +
                "is {CLAN_SCORE}, kingdom acceptance is {KINGDOM_SCORE}, and " +
                "the combined score is {TOTAL_SCORE}.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            message.SetTextVariable("KINGDOM_NAME", candidate.Kingdom.Name);
            message.SetTextVariable("WEIGHT", candidate.Weight.ToString("F0"));
            message.SetTextVariable("CLAN_SCORE", clanScore);
            message.SetTextVariable("KINGDOM_SCORE", kingdomScore);
            message.SetTextVariable("TOTAL_SCORE", combinedScore);
            ModLogger.Info(message.ToString());
        }

        private static void LogJoined(Clan clan, Kingdom kingdom)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanJoined}" +
                "[Clan Realignment] {CLAN_NAME} has joined {KINGDOM_NAME}.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            message.SetTextVariable("KINGDOM_NAME", kingdom.Name);
            ModLogger.Notice(message.ToString());
        }

        private static void LogBarterFailed(Clan clan, Kingdom kingdom)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanBarterFailed}" +
                "[Clan Realignment] Negotiations between {CLAN_NAME} and " +
                "{KINGDOM_NAME} ended without an agreement.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            message.SetTextVariable("KINGDOM_NAME", kingdom.Name);
            ModLogger.Info(message.ToString());
        }

        private sealed class KingdomCandidate
        {
            public KingdomCandidate(Kingdom kingdom, float weight)
            {
                Kingdom = kingdom;
                Weight = weight;
            }

            public Kingdom Kingdom { get; }

            public float Weight { get; }
        }
    }
}
