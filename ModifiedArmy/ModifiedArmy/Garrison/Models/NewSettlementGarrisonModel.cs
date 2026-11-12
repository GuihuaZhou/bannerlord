using ModifiedArmy.Recruitment.Pools.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Makes barracks capacity determine the shared daily recruitment quota
    /// used by professional troops and converted prisoners.
    /// </summary>
    public sealed class NewSettlementGarrisonModel
        : DefaultSettlementGarrisonModel
    {
        public override ExplainedNumber GetMaximumDailyAutoRecruitmentCount(
            Town town,
            bool includeDescriptions = false)
        {
            int maximum = town == null
                ? 0
                : 1 + SettlementManpowerProductionBehavior
                    .GetBarracksLevel(town.Settlement);
            return new ExplainedNumber(maximum, includeDescriptions,
                new TextObject(
                    "{=ModifiedArmy_BarracksRecruitmentLimit}" +
                    "Barracks recruitment capacity"));
        }
    }
}
