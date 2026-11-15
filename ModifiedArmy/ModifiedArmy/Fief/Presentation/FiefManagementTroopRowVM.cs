// Presents one read-only troop/count row inside a fief batch group. The
// authoritative roster and batch records remain owned by the fief domain.
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.Fief.Presentation
{
    public sealed class FiefManagementTroopRowVM : ViewModel
    {
        internal FiefManagementTroopRowVM(CharacterObject troop, int count)
        {
            Troop = troop ?? throw new ArgumentNullException(nameof(troop));
            Name = troop.Name.ToString();
            Count = Math.Max(0, count);
            Visual = new CharacterImageIdentifierVM(
                CampaignUIHelper.GetCharacterCode(troop, false));
            TierIconData = CampaignUIHelper.GetCharacterTierData(troop, false);
            TypeIconData = CampaignUIHelper.GetCharacterTypeData(troop, false);
        }

        internal CharacterObject Troop { get; }

        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public int Count { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM Visual { get; }
        [DataSourceProperty] public StringItemWithHintVM TierIconData { get; }
        [DataSourceProperty] public StringItemWithHintVM TypeIconData { get; }
    }
}
