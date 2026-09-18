using System;
using System.Collections.Generic;

namespace ModifiedPolitics.Models.WarDisposition.Events
{
    /// <summary>
    /// 战争倾向事件的唯一数值目录。
    /// 所有基础值和 Trait 权重集中维护，避免监听器各自复制公式。
    /// </summary>
    public static class WarDispositionEventCatalog
    {
        private static readonly IReadOnlyDictionary<
            WarDispositionEventType,
            WarDispositionEventDefinition> Definitions =
            CreateDefinitions();

        /// <summary>
        /// 获取指定事件的计算定义。
        /// 未注册的枚举值视为程序错误，不使用静默默认值。
        /// </summary>
        public static WarDispositionEventDefinition Get(
            WarDispositionEventType eventType)
        {
            if (!Definitions.TryGetValue(
                eventType,
                out WarDispositionEventDefinition definition))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(eventType),
                    eventType,
                    "未注册的战争倾向事件类型.");
            }

            return definition;
        }

        private static IReadOnlyDictionary<
            WarDispositionEventType,
            WarDispositionEventDefinition> CreateDefinitions()
        {
            return new Dictionary<
                WarDispositionEventType,
                WarDispositionEventDefinition>
            {
                [WarDispositionEventType.PartyVictory] = Define(
                    WarDispositionEventType.PartyVictory,
                    WarDispositionInfluenceCategory.Battle,
                    3f,
                    valor: 0.15f,
                    calculating: 0.05f),

                [WarDispositionEventType.PartyDefeat] = Define(
                    WarDispositionEventType.PartyDefeat,
                    WarDispositionInfluenceCategory.Battle,
                    -3f,
                    valor: -0.15f,
                    calculating: 0.10f),

                [WarDispositionEventType.PartyDestroyed] = Define(
                    WarDispositionEventType.PartyDestroyed,
                    WarDispositionInfluenceCategory.Battle,
                    -6f,
                    valor: -0.10f,
                    mercy: 0.10f,
                    calculating: 0.15f),

                [WarDispositionEventType.ClanMemberCaptured] = Define(
                    WarDispositionEventType.ClanMemberCaptured,
                    WarDispositionInfluenceCategory.Battle,
                    -3f,
                    valor: -0.05f,
                    honor: 0.05f),

                [WarDispositionEventType.ClanMemberKilled] = Define(
                    WarDispositionEventType.ClanMemberKilled,
                    WarDispositionInfluenceCategory.Battle,
                    -4f,
                    valor: -0.10f,
                    mercy: 0.15f),

                [WarDispositionEventType.ClanMemberExecuted] = Define(
                    WarDispositionEventType.ClanMemberExecuted,
                    WarDispositionInfluenceCategory.Battle,
                    -10f,
                    valor: -0.15f,
                    mercy: 0.15f,
                    honor: -0.20f),

                [WarDispositionEventType.VillageRaided] = Define(
                    WarDispositionEventType.VillageRaided,
                    WarDispositionInfluenceCategory.Territory,
                    -3f,
                    mercy: 0.20f,
                    honor: 0.05f),

                [WarDispositionEventType.CastleLost] = Define(
                    WarDispositionEventType.CastleLost,
                    WarDispositionInfluenceCategory.Territory,
                    -6f,
                    valor: -0.10f,
                    calculating: 0.15f),

                [WarDispositionEventType.TownLost] = Define(
                    WarDispositionEventType.TownLost,
                    WarDispositionInfluenceCategory.Territory,
                    -8f,
                    valor: -0.10f,
                    calculating: 0.15f),

                [WarDispositionEventType.EnemyCaptured] = Define(
                    WarDispositionEventType.EnemyCaptured,
                    WarDispositionInfluenceCategory.WarGain,
                    2f),

                [WarDispositionEventType.CastleCaptured] = Define(
                    WarDispositionEventType.CastleCaptured,
                    WarDispositionInfluenceCategory.WarGain,
                    4f,
                    valor: 0.10f,
                    calculating: 0.10f),

                [WarDispositionEventType.TownCaptured] = Define(
                    WarDispositionEventType.TownCaptured,
                    WarDispositionInfluenceCategory.WarGain,
                    5f,
                    valor: 0.10f,
                    calculating: 0.10f),

                [WarDispositionEventType.CastleGranted] = Define(
                    WarDispositionEventType.CastleGranted,
                    WarDispositionInfluenceCategory.WarGain,
                    10f,
                    generosity: -0.10f,
                    calculating: 0.10f),

                [WarDispositionEventType.TownGranted] = Define(
                    WarDispositionEventType.TownGranted,
                    WarDispositionInfluenceCategory.WarGain,
                    15f,
                    generosity: -0.10f,
                    calculating: 0.10f),

                // 财富事件的基础值由每周财富变化区间决定，
                // 因此目录中的 0 会在调用计算器时被覆盖。
                [WarDispositionEventType.WeeklyWealthChange] = Define(
                    WarDispositionEventType.WeeklyWealthChange,
                    WarDispositionInfluenceCategory.Economy,
                    0f,
                    calculating: 0.20f)
            };
        }

        private static WarDispositionEventDefinition Define(
            WarDispositionEventType eventType,
            WarDispositionInfluenceCategory category,
            float baseValue,
            float valor = 0f,
            float mercy = 0f,
            float honor = 0f,
            float generosity = 0f,
            float calculating = 0f)
        {
            return new WarDispositionEventDefinition(
                eventType,
                category,
                baseValue,
                new WarDispositionTraitWeights(
                    valor,
                    mercy,
                    honor,
                    generosity,
                    calculating));
        }
    }
}
