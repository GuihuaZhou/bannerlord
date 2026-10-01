using HarmonyLib;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.Clans
{
    /// <summary>
    /// Prevents a normal noble clan from being deleted merely because it has
    /// remained landless and independent for 28 days. A clan that has only
    /// children left is not protected. Native rebel-clan rules are untouched.
    /// </summary>
    [HarmonyPatch(
        typeof(FactionDiscontinuationCampaignBehavior),
        "CanClanBeDiscontinued")]
    public static class ClanDiscontinuationPatch
    {
        private static void Postfix(Clan clan, ref bool __result)
        {
            if (!__result || !ExiledClanPolicy.IsProtectedNobleClan(clan))
            {
                return;
            }

            __result = false;

            TextObject message = new TextObject(
                "{=ModifiedPolitics_ExiledClanPreserved}" +
                "[Clan Survival] {CLAN_NAME} still has an adult member and " +
                "will not disappear because of prolonged exile.");
            message.SetTextVariable("CLAN_NAME", clan.Name);
            ModLogger.Info(message.ToString());
        }
    }
}
