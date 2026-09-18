namespace ModifiedPolitics.Models.WarDisposition.Events
{
    /// <summary>
    /// UI 汇总使用的战争倾向影响分类。
    /// 分类只记录已经计算完成的结果，不参与二次计算。
    /// </summary>
    public enum WarDispositionInfluenceCategory
    {
        Battle,
        Territory,
        WarGain,
        Economy
    }
}
