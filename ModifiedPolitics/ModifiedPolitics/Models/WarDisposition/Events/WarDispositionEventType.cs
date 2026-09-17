namespace ModifiedPolitics.Models.WarDisposition.Events
{
    /// <summary>
    /// 所有能够改变 Clan 战争倾向的事件类型。
    /// 事件监听层只负责识别事件，具体数值统一由事件目录提供。
    /// </summary>
    public enum WarDispositionEventType
    {
        PartyVictory,
        PartyDefeat,
        PartyDestroyed,
        ClanMemberCaptured,
        ClanMemberKilled,
        ClanMemberExecuted,
        VillageRaided,
        CastleLost,
        TownLost,
        EnemyCaptured,
        CastleCaptured,
        TownCaptured,
        CastleGranted,
        TownGranted,
        WeeklyWealthChange
    }
}
