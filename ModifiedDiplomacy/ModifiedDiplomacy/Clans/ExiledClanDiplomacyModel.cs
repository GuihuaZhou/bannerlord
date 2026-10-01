using ModifiedPolitics.Diplomacy.Policies;
using ModifiedPolitics.Tool;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.Clans
{
    /// <summary>
    /// Replaces only the joining desire of landless independent noble clans.
    /// All ordinary defections and special faction types retain the native
    /// diplomacy score.
    /// </summary>
    public sealed class ExiledClanDiplomacyModel : DefaultDiplomacyModel
    {
        private const float BaseDesire = -40000f;
        private const float SameKingdomCultureBonus = 10000f;
        private const float SameCultureTownBonus = 12000f;
        private const float MaximumTownBonus = 36000f;
        private const float SameCultureCastleBonus = 4000f;
        private const float MaximumCastleBonus = 16000f;
        private const float MaximumTerritoryShareBonus = 30000f;
        private const float RelationMultiplier = 300f;
        private const float DailyExileBonus = 1000f;
        private const float MaximumExileBonus = 30000f;
        private const float ForbiddenScore = -100000000f;

        /// <summary>
        /// Calculates an exiled clan's willingness to join a kingdom using
        /// cultural territory, ruler relations and time spent in exile.
        /// </summary>
        public override float GetScoreOfClanToJoinKingdom(
            Clan clan,
            Kingdom kingdom)
        {
            if (!ExiledClanPolicy.IsEligibleExiledClan(clan))
            {
                return base.GetScoreOfClanToJoinKingdom(clan, kingdom);
            }

            if (!IsValidTarget(clan, kingdom))
            {
                LogInvalidTarget(clan, kingdom);
                return ForbiddenScore;
            }

            int towns = kingdom.Settlements.Count(settlement =>
                settlement.IsTown && settlement.Culture == clan.Culture);
            int castles = kingdom.Settlements.Count(settlement =>
                settlement.IsCastle && settlement.Culture == clan.Culture);
            int controlledFortifications = towns + castles;
            int worldFortifications = Campaign.Current.Settlements.Count(
                settlement =>
                    (settlement.IsTown || settlement.IsCastle) &&
                    settlement.Culture == clan.Culture);

            float townScore = Math.Min(
                towns * SameCultureTownBonus,
                MaximumTownBonus);
            float castleScore = Math.Min(
                castles * SameCultureCastleBonus,
                MaximumCastleBonus);
            float territoryShare = worldFortifications > 0
                ? controlledFortifications / (float)worldFortifications
                : 0f;
            float territoryShareScore = territoryShare *
                MaximumTerritoryShareBonus;
            float kingdomCultureScore = kingdom.Culture == clan.Culture
                ? SameKingdomCultureBonus
                : 0f;
            int rulerRelation = FactionManager.GetRelationBetweenClans(
                kingdom.RulingClan,
                clan);
            float relationScore = rulerRelation * RelationMultiplier;
            float exileDays = Math.Max(
                0f,
                (float)(CampaignTime.Now - clan.LastFactionChangeTime).ToDays);
            float exileScore = Math.Min(
                exileDays * DailyExileBonus,
                MaximumExileBonus);

            float result = BaseDesire +
                           kingdomCultureScore +
                           townScore +
                           castleScore +
                           territoryShareScore +
                           relationScore +
                           exileScore;

            LogScore(
                clan,
                kingdom,
                result,
                towns,
                castles,
                territoryShare,
                rulerRelation,
                exileDays);
            return result;
        }

        /// <summary>
        /// Applies the same hard exclusions as the realignment behavior before
        /// returning a meaningful willingness score.
        /// </summary>
        private static bool IsValidTarget(Clan clan, Kingdom kingdom)
        {
            return kingdom != null &&
                   !kingdom.IsEliminated &&
                   kingdom.RulingClan != null &&
                   !kingdom.RulingClan.IsEliminated &&
                   clan.MapFaction != kingdom &&
                   !clan.IsAtWarWith(kingdom) &&
                   !Campaign.Current.Models.DiplomacyModel
                       .IsAtConstantWar(clan, kingdom);
        }

        private static void LogInvalidTarget(Clan clan, Kingdom kingdom)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanTargetRejected}" +
                "[Clan Realignment] {CLAN_NAME} cannot join {KINGDOM_NAME} " +
                "because it is not currently a valid target.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            message.SetTextVariable(
                "KINGDOM_NAME",
                kingdom?.Name ?? new TextObject(string.Empty));
            ModLogger.Debug(message.ToString());
        }

        private static void LogScore(
            Clan clan,
            Kingdom kingdom,
            float score,
            int towns,
            int castles,
            float territoryShare,
            int rulerRelation,
            float exileDays)
        {
            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanJoinScore}" +
                "[Clan Realignment] {CLAN_NAME} has {SCORE} willingness to " +
                "join {KINGDOM_NAME}. It controls {TOWNS} same-culture towns " +
                "and {CASTLES} castles, or {TERRITORY_SHARE}% of all " +
                "same-culture fortifications. Ruler relation is {RELATION} " +
                "and exile has lasted {EXILE_DAYS} days.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            message.SetTextVariable("KINGDOM_NAME", kingdom.Name);
            message.SetTextVariable("SCORE", score.ToString("F0"));
            message.SetTextVariable("TOWNS", towns);
            message.SetTextVariable("CASTLES", castles);
            message.SetTextVariable(
                "TERRITORY_SHARE",
                (territoryShare * 100f).ToString("F0"));
            message.SetTextVariable("RELATION", rulerRelation);
            message.SetTextVariable("EXILE_DAYS", exileDays.ToString("F0"));
            ModLogger.Info(message.ToString());
        }
    }
}
