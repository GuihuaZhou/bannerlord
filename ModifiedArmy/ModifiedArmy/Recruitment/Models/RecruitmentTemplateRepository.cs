using ModifiedArmy.Recruitment.Classification;
using System;
using System.Collections.Generic;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Stores culture-specific composition templates and provides safe default
    /// templates for unknown cultures added by other modules.
    /// </summary>
    public sealed class RecruitmentTemplateRepository
    {
        private const string DefaultCultureId = "default";
        private readonly Dictionary<string, ArmyCompositionTemplate> _templates =
            new Dictionary<string, ArmyCompositionTemplate>(
                StringComparer.OrdinalIgnoreCase);

        public RecruitmentTemplateRepository()
        {
            RegisterFallbacks();
        }

        /// <summary>
        /// Finds an exact culture and party-type template, or returns the
        /// matching default template when another module adds a new culture.
        /// </summary>
        public ArmyCompositionTemplate GetTemplate(
            string cultureId,
            RecruitmentPartyType partyType)
        {
            string key = CreateKey(cultureId, partyType);

            if (_templates.TryGetValue(key, out ArmyCompositionTemplate template))
            {
                return template;
            }

            return _templates[CreateKey(DefaultCultureId, partyType)];
        }

        /// <summary>
        /// Registers or replaces one culture and party-type combination.
        /// </summary>
        public void Register(
            string cultureId,
            RecruitmentPartyType partyType,
            ArmyCompositionTemplate template)
        {
            if (string.IsNullOrWhiteSpace(cultureId) || template == null)
            {
                throw new ArgumentException("Recruitment template registration is invalid.");
            }

            _templates[CreateKey(cultureId, partyType)] = template;
        }

        /// <summary>
        /// Registers only culture-neutral fallbacks. Culture-specific values
        /// are loaded from ModuleData/aiRecruitmentTemplates.xml and replace
        /// these entries through Register.
        /// </summary>
        private void RegisterFallbacks()
        {
            RegisterCulture("default", 25, 55, 15, 40, 10, 35, 0, 20,
                45, 75, 25, 55, 0, 15, 0, 10);
        }

        /// <summary>
        /// Creates both party-type templates for one culture from percentage
        /// values, keeping culture registration compact and auditable.
        /// </summary>
        private void RegisterCulture(
            string cultureId,
            int mobileInfantryMin, int mobileInfantryMax,
            int mobileRangedMin, int mobileRangedMax,
            int mobileCavalryMin, int mobileCavalryMax,
            int mobileHorseArcherMin, int mobileHorseArcherMax,
            int garrisonInfantryMin, int garrisonInfantryMax,
            int garrisonRangedMin, int garrisonRangedMax,
            int garrisonCavalryMin, int garrisonCavalryMax,
            int garrisonHorseArcherMin, int garrisonHorseArcherMax)
        {
            Register(
                cultureId,
                RecruitmentPartyType.MobileParty,
                CreateTemplate(
                    mobileInfantryMin, mobileInfantryMax,
                    mobileRangedMin, mobileRangedMax,
                    mobileCavalryMin, mobileCavalryMax,
                    mobileHorseArcherMin, mobileHorseArcherMax,
                    15, 50, 30, 70, 5, 30));

            Register(
                cultureId,
                RecruitmentPartyType.Garrison,
                CreateTemplate(
                    garrisonInfantryMin, garrisonInfantryMax,
                    garrisonRangedMin, garrisonRangedMax,
                    garrisonCavalryMin, garrisonCavalryMax,
                    garrisonHorseArcherMin, garrisonHorseArcherMax,
                    40, 50, 35, 55, 10, 25));
        }

        private static ArmyCompositionTemplate CreateTemplate(
            int infantryMin, int infantryMax,
            int rangedMin, int rangedMax,
            int cavalryMin, int cavalryMax,
            int horseArcherMin, int horseArcherMax,
            int lowMin, int lowMax,
            int middleMin, int middleMax,
            int topMin, int topMax)
        {
            return new ArmyCompositionTemplate(
                new Dictionary<CombatRole, RatioRange>
                {
                    [CombatRole.Infantry] = Range(infantryMin, infantryMax),
                    [CombatRole.Ranged] = Range(rangedMin, rangedMax),
                    [CombatRole.Cavalry] = Range(cavalryMin, cavalryMax),
                    [CombatRole.HorseArcher] = Range(horseArcherMin, horseArcherMax)
                },
                new Dictionary<TroopQuality, RatioRange>
                {
                    [TroopQuality.LowTier] = Range(lowMin, lowMax),
                    [TroopQuality.MiddleTier] = Range(middleMin, middleMax),
                    [TroopQuality.TopTier] = Range(topMin, topMax)
                });
        }

        private static RatioRange Range(int minimumPercent, int maximumPercent)
        {
            return new RatioRange(minimumPercent / 100f, maximumPercent / 100f);
        }

        private static string CreateKey(
            string cultureId,
            RecruitmentPartyType partyType)
        {
            return $"{cultureId ?? DefaultCultureId}:{partyType}";
        }
    }
}
