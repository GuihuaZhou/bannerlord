using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Prisoners.Patches
{
    /// <summary>
    /// Keeps hero prisoners in prisons owned by the player's clan while
    /// preserving the native escape rules for every other captivity context.
    /// </summary>
    [HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior), "DailyHeroTick")]
    internal static class PlayerSettlementPrisonerEscapePatch
    {
        private static bool Prefix(Hero hero)
        {
            PartyBase captor = hero?.PartyBelongedToAsPrisoner;
            Settlement prison = captor?.IsSettlement == true
                ? captor.Settlement
                : null;

            bool isPlayerFortificationPrisoner =
                hero?.IsPrisoner == true &&
                hero != Hero.MainHero &&
                prison?.IsFortification == true &&
                prison.OwnerClan == Clan.PlayerClan;

            return !isPlayerFortificationPrisoner;
        }
    }
}
