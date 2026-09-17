namespace ModifiedPolitics.Models.WarDisposition.Events
{
    /// <summary>
    /// 一个事件对五项人物特性的反应权重。
    /// 未参与该事件的 Trait 权重保持为 0。
    /// </summary>
    public sealed class WarDispositionTraitWeights
    {
        public float Valor { get; }
        public float Mercy { get; }
        public float Honor { get; }
        public float Generosity { get; }
        public float Calculating { get; }

        public WarDispositionTraitWeights(
            float valor = 0f,
            float mercy = 0f,
            float honor = 0f,
            float generosity = 0f,
            float calculating = 0f)
        {
            Valor = valor;
            Mercy = mercy;
            Honor = honor;
            Generosity = generosity;
            Calculating = calculating;
        }
    }
}
