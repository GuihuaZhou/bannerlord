namespace ModifiedPolitics.Models.WarDisposition.Events
{
    /// <summary>
    /// 单个战争事件的不可变计算定义。
    /// </summary>
    public sealed class WarDispositionEventDefinition
    {
        public WarDispositionEventType EventType { get; }
        public WarDispositionInfluenceCategory Category { get; }
        public float BaseValue { get; }
        public WarDispositionTraitWeights TraitWeights { get; }

        public WarDispositionEventDefinition(
            WarDispositionEventType eventType,
            WarDispositionInfluenceCategory category,
            float baseValue,
            WarDispositionTraitWeights traitWeights)
        {
            EventType = eventType;
            Category = category;
            BaseValue = baseValue;
            TraitWeights = traitWeights ?? new WarDispositionTraitWeights();
        }
    }
}
