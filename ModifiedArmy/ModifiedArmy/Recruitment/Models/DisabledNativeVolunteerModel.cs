using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Keeps native volunteer maintenance callable while preventing it from
    /// creating or upgrading notable volunteer slots.
    /// </summary>
    public sealed class DisabledNativeVolunteerModel : DefaultVolunteerModel
    {
        public override float GetDailyVolunteerProductionProbability(
            Hero hero,
            int index,
            Settlement settlement)
        {
            return 0f;
        }
    }
}
