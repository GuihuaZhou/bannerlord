using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.Models.WarDisposition
{
    /// <summary>
    /// 单个 Clan 的战争倾向持久化数据。
    /// </summary>
    [SaveableRootClass(1)]
    public sealed class WarDispositionData
    {
        [SaveableProperty(1)]
        public float Value { get; set; }

        [SaveableProperty(2)]
        public float TodayBattleInfluence { get; set; }

        [SaveableProperty(3)]
        public float TodayTerritoryInfluence { get; set; }

        [SaveableProperty(4)]
        public float TodayWarGainInfluence { get; set; }

        [SaveableProperty(5)]
        public int PreviousWeeklyWealth { get; set; }

        [SaveableProperty(6)]
        public float WeeklyWealthChangePercent { get; set; }

        [SaveableProperty(7)]
        public float WeeklyWealthInfluence { get; set; }

        public WarDispositionData()
        {
        }

        public WarDispositionData(int initialWealth)
        {
            Value = 0f;
            PreviousWeeklyWealth = initialWealth;
        }
    }
}
