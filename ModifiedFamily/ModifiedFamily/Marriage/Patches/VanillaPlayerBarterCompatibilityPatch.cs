// Keeps the main hero's romance route while blocking instant arranged marriages for relatives.
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using ModifiedFamily.Marriage.Models;

namespace ModifiedFamily.Marriage.Patches
{
    [HarmonyPatch]
    internal static class VanillaPlayerBarterCompatibilityPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(typeof(RomanceCampaignBehavior), "conversation_lord_propose_marriage_to_clan_leader_confirm_consequences");
        static bool Prefix(object __instance)
        {
            var selected = AccessTools.Field(__instance.GetType(), "_playerProposalHero")?.GetValue(__instance) as Hero;
            // Keep the patch as a transport adapter; policy belongs to the model.
            return MarriagePolicyModel.CanUseVanillaMarriageBarter(selected);
        }
    }
}
