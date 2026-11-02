using ModifiedArmy.ForceManagement.Contracts;
using ModifiedPolitics.Models.WarDisposition;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.WarDisposition.Integration
{
    /// <summary>
    /// Exposes the current campaign's authoritative war-disposition behavior
    /// through ModifiedArmy's dependency-neutral contract.
    /// </summary>
    internal sealed class ModifiedPoliticsWarDispositionProvider :
        IWarDispositionProvider
    {
        public bool TryGet(
            Clan clan,
            out float disposition,
            out bool atFormalWar)
        {
            disposition = 0f;
            atFormalWar = false;

            WarDispositionManager manager = Campaign.Current?
                .GetCampaignBehavior<WarDispositionManager>();
            if (manager == null)
                return false;

            atFormalWar = manager.IsInFormalWar(clan);
            if (!manager.TryGetData(clan, out WarDispositionData data))
                return false;

            disposition = data.Value;
            return true;
        }
    }
}
