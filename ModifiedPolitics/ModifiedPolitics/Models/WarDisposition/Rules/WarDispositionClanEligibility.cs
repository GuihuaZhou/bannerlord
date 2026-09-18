using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.Models.WarDisposition.Rules
{
    /// <summary>
    /// 统一定义哪些 Clan 参与战争倾向系统.
    /// 小家族(MinorFaction)主要作为雇佣兵派系活动, 不参与王国政治态度计算.
    /// </summary>
    public static class WarDispositionClanEligibility
    {
        public static bool IsEligible(Clan clan)
        {
            return clan != null
                && !clan.IsBanditFaction
                && !clan.IsMinorFaction
                && !clan.IsEliminated;
        }
    }
}
