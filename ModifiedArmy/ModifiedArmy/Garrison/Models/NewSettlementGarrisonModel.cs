using ModifiedArmy.Recruitment.Pools.Behaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Makes barracks capacity determine the shared daily recruitment quota
    /// used by professional troops and converted prisoners.
    /// </summary>
    public sealed class NewSettlementGarrisonModel
        : DefaultSettlementGarrisonModel
    {
        public override int GetMaximumDailyAutoRecruitmentCount(Town town)
        {
            return town == null
                ? 0
                : 1 + SettlementRecruitmentPoolBehavior.GetBarracksLevel(
                    town.Settlement);
        }
    }
}
