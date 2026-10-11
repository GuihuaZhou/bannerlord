using System;
using System.Linq;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedPolitics.HeroOffices.Models
{
    /// <summary>
    /// Provides the single appointment score used by every marshal selection
    /// path. Political reliability deliberately outranks military ability.
    /// </summary>
    public static class MarshalAppointmentScoreModel
    {
        private const float NativeMinimumArmyInfluence = 50f;

        public static MarshalAppointmentScore Evaluate(
            Hero hero,
            Kingdom kingdom)
        {
            if (hero == null || kingdom == null)
            {
                return MarshalAppointmentScore.Empty;
            }

            Clan clan = hero.Clan;
            MarshalScoreConfig config = GetConfig(kingdom);
            Hero clanLeader = clan?.Leader;
            int relation = clanLeader == null || kingdom.Leader == null
                ? -100
                : kingdom.Leader.GetRelation(clanLeader);
            relation = Math.Max(-100, Math.Min(100, relation));

            int politicalPriority = clan == kingdom.RulingClan
                ? 2
                : hero == Hero.MainHero ? 1 : 0;
            float influence = Math.Max(0f, clan?.Influence ?? 0f);
            float requiredInfluence = CalculateRequiredArmyInfluence(
                hero,
                kingdom,
                config.InfluenceTarget);
            bool canFundArmy = influence >= requiredInfluence;
            float lowInfluenceBonus = canFundArmy
                ? (1f - MathF.Clamp(
                    (influence - requiredInfluence) /
                    Math.Max(1f, config.InfluenceTarget - requiredInfluence),
                    0f,
                    1f)) * config.MaximumLowInfluenceBonus
                : 0f;
            int clanOfficeCount = CountClanOffices(kingdom, clan);
            float officeDistributionBonus = Math.Max(
                0f,
                config.MaximumOfficeDistributionBonus - clanOfficeCount * 10f);
            float abilityScore = GetAbilityScore(hero, kingdom);
            float traitScore = GetTraitScore(hero, kingdom);
            float competenceTieBreaker = MathF.Clamp(
                (abilityScore + traitScore) / config.CompetenceDivisor,
                0f,
                config.MaximumCompetenceTieBreaker);

            float finalScore = politicalPriority * config.PoliticalPriorityBand
                + (relation + 100) * config.RelationBand
                + (canFundArmy ? config.ArmyReadyBonus : 0f)
                + lowInfluenceBonus
                + Math.Max(0, clan?.Tier ?? 0) * config.ClanTierWeight
                + officeDistributionBonus
                + competenceTieBreaker;

            return new MarshalAppointmentScore(
                finalScore,
                politicalPriority,
                relation,
                influence,
                requiredInfluence,
                canFundArmy,
                lowInfluenceBonus,
                clanOfficeCount,
                abilityScore,
                traitScore,
                competenceTieBreaker);
        }

        public static float GetScore(Hero hero, Kingdom kingdom)
        {
            return Evaluate(hero, kingdom).FinalScore;
        }

        /// <summary>
        /// Keeps periodic reviews stable while still allowing a clearly more
        /// reliable clan to replace a stronger but politically weaker marshal.
        /// </summary>
        public static bool ShouldReplace(
            Hero incumbent,
            Hero challenger,
            Kingdom kingdom)
        {
            MarshalAppointmentScore current = Evaluate(incumbent, kingdom);
            MarshalAppointmentScore next = Evaluate(challenger, kingdom);

            if (next.PoliticalPriority != current.PoliticalPriority)
            {
                return next.PoliticalPriority > current.PoliticalPriority;
            }

            if (Math.Abs(next.RulerRelation - current.RulerRelation) >= 10)
            {
                return next.RulerRelation > current.RulerRelation;
            }

            if (next.CanFundArmy != current.CanFundArmy)
            {
                return next.CanFundArmy;
            }

            if (Math.Abs(next.LowInfluenceBonus - current.LowInfluenceBonus) >= 100f)
            {
                return next.LowInfluenceBonus > current.LowInfluenceBonus;
            }

            float currentCompetence = Math.Max(
                0f,
                current.AbilityScore + current.TraitScore);
            float nextCompetence = Math.Max(
                0f,
                next.AbilityScore + next.TraitScore);
            return currentCompetence > 0f
                ? nextCompetence >= currentCompetence * 1.25f
                : nextCompetence > 0f;
        }

        public static float GetAbilityScore(Hero hero, Kingdom kingdom = null)
        {
            if (hero == null)
            {
                return 0f;
            }

            MarshalScoreConfig config = GetConfig(
                kingdom ?? hero.Clan?.Kingdom);
            return hero.GetSkillValue(DefaultSkills.Tactics) * config.TacticsWeight
                + hero.GetSkillValue(DefaultSkills.Leadership) * config.LeadershipWeight
                + hero.GetSkillValue(DefaultSkills.Scouting) * config.ScoutingWeight
                + hero.GetSkillValue(DefaultSkills.Steward) * config.StewardWeight
                + hero.GetSkillValue(DefaultSkills.Engineering) * config.EngineeringWeight
                + hero.GetSkillValue(DefaultSkills.Charm) * config.CharmWeight
                + hero.GetSkillValue(DefaultSkills.Trade) * config.TradeWeight;
        }

        public static float GetTraitScore(Hero hero, Kingdom kingdom = null)
        {
            if (hero == null)
            {
                return 0f;
            }

            MarshalScoreConfig config = GetConfig(
                kingdom ?? hero.Clan?.Kingdom);
            return hero.GetTraitLevel(DefaultTraits.Calculating) * config.CalculatingWeight
                + hero.GetTraitLevel(DefaultTraits.Valor) * config.ValorWeight
                + hero.GetTraitLevel(DefaultTraits.Honor) * config.HonorWeight;
        }

        public static float CalculateRequiredArmyInfluence(
            Hero hero,
            Kingdom kingdom,
            float influenceTarget)
        {
            MobileParty leaderParty = hero?.PartyBelongedTo;
            if (leaderParty == null || kingdom == null || Campaign.Current == null)
            {
                return Math.Min(influenceTarget, NativeMinimumArmyInfluence);
            }

            float totalCost = 0f;
            OfficeAssignment assignment = HeroOfficeBehavior.Current?
                .GetAssignment(hero);
            float prospectiveMarshalDiscount = assignment != null &&
                assignment.Kingdom == kingdom &&
                assignment.OfficeType == OfficeType.Marshal
                ? 1f
                : 0.75f;
            foreach (Clan clan in kingdom.Clans)
            {
                if (clan == null || clan.IsEliminated || clan.IsClanTypeMercenary)
                {
                    continue;
                }

                foreach (WarPartyComponent component in clan.WarPartyComponents)
                {
                    MobileParty party = component?.MobileParty;
                    if (party == null || party == leaderParty || !party.IsActive ||
                        !party.IsLordParty || party.IsDisbanding ||
                        party.LeaderHero == null || party.Army != null)
                    {
                        continue;
                    }

                    try
                    {
                        int cost = Campaign.Current.Models
                            .ArmyManagementCalculationModel
                            .CalculatePartyInfluenceCost(leaderParty, party);
                        totalCost += Math.Max(
                            0,
                            MathF.Ceiling(cost * prospectiveMarshalDiscount));
                    }
                    catch
                    {
                        // A transient party state must not break office UI or
                        // invalidate the remaining army influence estimate.
                    }
                }
            }

            return Math.Min(
                influenceTarget,
                Math.Max(NativeMinimumArmyInfluence, totalCost));
        }

        private static int CountClanOffices(Kingdom kingdom, Clan clan)
        {
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            if (behavior == null || clan == null)
            {
                return 0;
            }

            return Enum.GetValues(typeof(OfficeType))
                .Cast<OfficeType>()
                .SelectMany(type => behavior.GetAssignments(kingdom, type))
                .Count(assignment => assignment?.Hero?.Clan == clan);
        }

        private static MarshalScoreConfig GetConfig(Kingdom kingdom)
        {
            return OfficeConfigManager.Instance.TryGet(
                kingdom,
                out OfficeCultureConfig cultureConfig)
                ? cultureConfig.MarshalScore
                : new MarshalScoreConfig();
        }
    }

    /// <summary>
    /// Exposes a complete score breakdown for UI and diagnostic logging.
    /// </summary>
    public sealed class MarshalAppointmentScore
    {
        public static readonly MarshalAppointmentScore Empty =
            new MarshalAppointmentScore(0f, 0, -100, 0f, 50f, false, 0f, 0, 0f, 0f, 0f);

        public MarshalAppointmentScore(
            float finalScore,
            int politicalPriority,
            int rulerRelation,
            float clanInfluence,
            float requiredInfluence,
            bool canFundArmy,
            float lowInfluenceBonus,
            int clanOfficeCount,
            float abilityScore,
            float traitScore,
            float competenceTieBreaker)
        {
            FinalScore = finalScore;
            PoliticalPriority = politicalPriority;
            RulerRelation = rulerRelation;
            ClanInfluence = clanInfluence;
            RequiredInfluence = requiredInfluence;
            CanFundArmy = canFundArmy;
            LowInfluenceBonus = lowInfluenceBonus;
            ClanOfficeCount = clanOfficeCount;
            AbilityScore = abilityScore;
            TraitScore = traitScore;
            CompetenceTieBreaker = competenceTieBreaker;
        }

        public float FinalScore { get; }
        public int PoliticalPriority { get; }
        public int RulerRelation { get; }
        public float ClanInfluence { get; }
        public float RequiredInfluence { get; }
        public bool CanFundArmy { get; }
        public float LowInfluenceBonus { get; }
        public int ClanOfficeCount { get; }
        public float AbilityScore { get; }
        public float TraitScore { get; }
        public float CompetenceTieBreaker { get; }
    }
}
