using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedDiplomacy.Barters.Patches
{
    /// <summary>
    /// Rebuilds the native fief barter list and lets a kingdom ruler negotiate
    /// settlements without requiring the personal trade perk.
    /// </summary>
    [HarmonyPatch(typeof(FiefBarterBehavior), "CheckForBarters")]
    public static class RulerFiefBarterPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(BarterData args)
        {
            Hero offerer = args?.OffererHero;
            Hero other = args?.OtherHero;
            if (!CanNegotiateFiefs(offerer, other))
            {
                return false;
            }

            foreach (Town town in Town.AllFiefs)
            {
                Hero owner = town?.OwnerClan?.Leader;
                if (owner == offerer)
                {
                    args.AddBarterable<FiefBarterGroup>(
                        new FiefBarterable(town.Settlement, offerer, other),
                        false);
                }
                else if (owner == other)
                {
                    args.AddBarterable<FiefBarterGroup>(
                        new FiefBarterable(town.Settlement, other, offerer),
                        false);
                }
            }

            // The complete list has been supplied, so the native method must
            // not append a second copy of the same barterables.
            return false;
        }

        private static bool CanNegotiateFiefs(Hero offerer, Hero other)
        {
            if (offerer?.Clan == null
                || other?.Clan == null
                || (other.Clan.IsMinorFaction
                    && other.Clan != Clan.PlayerClan)
                || other.Clan.IsUnderMercenaryService
                || offerer.Clan.IsUnderMercenaryService)
            {
                return false;
            }

            bool isRuler = offerer.Clan.Kingdom?.RulingClan == offerer.Clan;
            return isRuler
                || offerer.GetPerkValue(DefaultPerks.Trade.EverythingHasAPrice);
        }
    }
}
