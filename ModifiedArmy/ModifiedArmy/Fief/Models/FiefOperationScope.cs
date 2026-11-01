using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    internal sealed class FiefOperationScope
    {
        public Settlement InteractionSettlement { get; set; }
        public Settlement SourceFief { get; set; }
        public Settlement VillageFilter { get; set; }
        public FiefPartyData FiefData { get; set; }
        public bool IsVillageOperation => VillageFilter != null;
    }
}
