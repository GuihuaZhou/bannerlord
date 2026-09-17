using ModifiedPolitics.Models.WarDisposition.Events;

namespace ModifiedPolitics.Models.WarDisposition.Calculation
{
    /// <summary>
    /// 单次战争倾向事件的完整计算结果。
    /// 保留中间值，便于以后记录日志和排查数值问题。
    /// </summary>
    public sealed class WarDispositionCalculationResult
    {
        public WarDispositionEventType EventType { get; set; }
        public WarDispositionInfluenceCategory Category { get; set; }
        public float EventBaseValue { get; set; }
        public float ClanRelationMultiplier { get; set; }
        public float TraitReaction { get; set; }
        public float PersonalityModifier { get; set; }
        /// <summary>
        /// 公式计算出的原始变化值，尚未考虑 [-100, 100] 边界。
        /// </summary>
        public float CalculatedDelta { get; set; }

        /// <summary>
        /// 应用边界限制后真正写入存档的变化值。
        /// </summary>
        public float AppliedDelta { get; set; }
    }
}
