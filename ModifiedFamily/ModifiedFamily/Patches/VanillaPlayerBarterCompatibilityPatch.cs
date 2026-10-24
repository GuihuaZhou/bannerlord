using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedFamily.Patches
{
    [HarmonyPatch]
    internal static class VanillaPlayerBarterCompatibilityPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(typeof(RomanceCampaignBehavior), "conversation_lord_propose_marriage_to_clan_leader_confirm_consequences");
        static bool Prefix(object __instance)
        {
            var selected = AccessTools.Field(__instance.GetType(), "_playerProposalHero")?.GetValue(__instance) as Hero;
            return selected == null || selected == Hero.MainHero;
        }
    }
}
