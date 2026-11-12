using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    internal static class PlayerFiefDiagnostics
    {
        internal static bool ShouldTracePlayerOwnedFief(
            Settlement sourceFief,
            Settlement village = null)
        {
            Clan playerClan = Clan.PlayerClan;
            if (playerClan == null || sourceFief?.IsFortification != true ||
                sourceFief.OwnerClan != playerClan)
                return false;

            if (village == null) return true;
            return village.IsVillage &&
                village.Village?.Bound == sourceFief &&
                sourceFief.BoundVillages?.Any(item =>
                    item?.Settlement == village) == true;
        }
    }
}
