using System;
using ModifiedPolitics.Models.WarDisposition.Events;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Models.WarDisposition.Calculation
{
    /// <summary>
    /// 战争倾向的无状态计算器。
    /// 该类型不读写存档，只负责把一次事件转换为实际变化值。
    /// </summary>
    public static class WarDispositionCalculator
    {
        public const float OwnClanMultiplier = 1f;
        public const float OtherClanMultiplier = 0.2f;
        public const float MinimumPersonalityModifier = -0.5f;
        public const float MaximumPersonalityModifier = 2f;
        public const float MinimumDisposition = -100f;
        public const float MaximumDisposition = 100f;

        /// <summary>
        /// 计算一次事件对目标 Clan 的战争倾向影响。
        /// 财富变化等动态事件可通过 baseValueOverride 覆盖目录基础值。
        /// </summary>
        public static WarDispositionCalculationResult Calculate(
            Clan targetClan,
            WarDispositionEventType eventType,
            bool isOwnClanEvent,
            float? baseValueOverride = null)
        {
            WarDispositionEventDefinition definition =
                WarDispositionEventCatalog.Get(eventType);

            float baseValue = baseValueOverride ?? definition.BaseValue;
            float relationMultiplier = isOwnClanEvent
                ? OwnClanMultiplier
                : OtherClanMultiplier;

            float traitReaction = CalculateTraitReaction(
                targetClan?.Leader,
                definition.TraitWeights);

            float personalityModifier = Clamp(
                1f + traitReaction,
                MinimumPersonalityModifier,
                MaximumPersonalityModifier);

            float calculatedDelta = baseValue
                * relationMultiplier
                * personalityModifier;

            return new WarDispositionCalculationResult
            {
                EventType = eventType,
                Category = definition.Category,
                EventBaseValue = baseValue,
                ClanRelationMultiplier = relationMultiplier,
                TraitReaction = traitReaction,
                PersonalityModifier = personalityModifier,
                CalculatedDelta = calculatedDelta,
                AppliedDelta = calculatedDelta
            };
        }

        /// <summary>
        /// 将累计战争倾向限制在设计范围 [-100, 100]。
        /// </summary>
        public static float ClampDisposition(float value)
        {
            return Clamp(value, MinimumDisposition, MaximumDisposition);
        }

        /// <summary>
        /// 按设计区间取得五档战争倾向等级。
        /// </summary>
        public static WarDispositionLevel GetLevel(float value)
        {
            float clamped = ClampDisposition(value);

            if (clamped <= -61f)
            {
                return WarDispositionLevel.StronglyFavorPeace;
            }

            if (clamped <= -21f)
            {
                return WarDispositionLevel.FavorPeace;
            }

            if (clamped <= 20f)
            {
                return WarDispositionLevel.Neutral;
            }

            if (clamped <= 60f)
            {
                return WarDispositionLevel.FavorWar;
            }

            return WarDispositionLevel.StronglyFavorWar;
        }

        /// <summary>
        /// 返回当前等级的中文 UI 文本。
        /// </summary>
        public static string GetLevelText(WarDispositionLevel level)
        {
            switch (level)
            {
                case WarDispositionLevel.StronglyFavorPeace:
                    return Localize("{=ModifiedPolitics_StronglyFavorPeace}Strongly Favor Peace");
                case WarDispositionLevel.FavorPeace:
                    return Localize("{=ModifiedPolitics_FavorPeace}Favor Peace");
                case WarDispositionLevel.Neutral:
                    return Localize("{=ModifiedPolitics_Neutral}Neutral");
                case WarDispositionLevel.FavorWar:
                    return Localize("{=ModifiedPolitics_FavorWar}Favor War");
                case WarDispositionLevel.StronglyFavorWar:
                    return Localize("{=ModifiedPolitics_StronglyFavorWar}Strongly Favor War");
                default:
                    return Localize("{=ModifiedPolitics_Neutral}Neutral");
            }
        }

        private static string Localize(string taggedText)
        {
            return new TextObject(taggedText).ToString();
        }

        private static float CalculateTraitReaction(
            Hero leader,
            WarDispositionTraitWeights weights)
        {
            if (leader == null || weights == null)
            {
                return 0f;
            }

            return leader.GetTraitLevel(DefaultTraits.Valor) * weights.Valor
                + leader.GetTraitLevel(DefaultTraits.Mercy) * weights.Mercy
                + leader.GetTraitLevel(DefaultTraits.Honor) * weights.Honor
                + leader.GetTraitLevel(DefaultTraits.Generosity) * weights.Generosity
                + leader.GetTraitLevel(DefaultTraits.Calculating) * weights.Calculating;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
