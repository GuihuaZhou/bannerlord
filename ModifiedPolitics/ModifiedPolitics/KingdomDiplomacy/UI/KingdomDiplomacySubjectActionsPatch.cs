using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.UI
{
    /// <summary>
    /// Adds subject demands to Bannerlord's existing diplomacy action list.
    /// Reusing the native action item keeps layout, hints and input behavior
    /// consistent with war, peace, alliance and trade proposals.
    /// </summary>
    [HarmonyPatch(typeof(KingdomDiplomacyVM), "OnSetCurrentDiplomacyItem")]
    public static class KingdomDiplomacySubjectActionsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            KingdomDiplomacyVM __instance,
            KingdomDiplomacyItemVM item)
        {
            Kingdom overlord = Clan.PlayerClan?.Kingdom;
            Kingdom target = item?.Faction2 as Kingdom;
            if (__instance?.Actions == null
                || overlord == null
                || target == null
                || target == overlord)
            {
                return;
            }

            AddDemandAction(
                __instance,
                overlord,
                target,
                SubjectType.Vassal,
                new TextObject(
                    "{=ModifiedPolitics_DemandVassal}Demand Vassalage"),
                new TextObject(
                    "{=ModifiedPolitics_DemandVassalDescription}Demand that {TARGET} become your vassal. During development, every valid demand is accepted."));

            AddDemandAction(
                __instance,
                overlord,
                target,
                SubjectType.Puppet,
                new TextObject(
                    "{=ModifiedPolitics_DemandPuppet}Demand Submission"),
                new TextObject(
                    "{=ModifiedPolitics_DemandPuppetDescription}Demand that {TARGET} become your puppet. During development, every valid demand is accepted."));
        }

        private static void AddDemandAction(
            KingdomDiplomacyVM diplomacyVm,
            Kingdom overlord,
            Kingdom target,
            SubjectType type,
            TextObject actionName,
            TextObject explanation)
        {
            explanation.SetTextVariable("TARGET", target.Name);

            TextObject disabledReason;
            bool isEnabled = SubjectProposalService
                .CanDemandSubjectRelation(
                    overlord,
                    target,
                    type,
                    out disabledReason);

            diplomacyVm.Actions.Add(
                new KingdomDiplomacyProposalActionItemVM(
                    actionName,
                    explanation,
                    0,
                    isEnabled,
                    disabledReason,
                    () => ExecuteDemand(
                        diplomacyVm,
                        overlord,
                        target,
                        type)));
        }

        private static void ExecuteDemand(
            KingdomDiplomacyVM diplomacyVm,
            Kingdom overlord,
            Kingdom target,
            SubjectType type)
        {
            if (!SubjectProposalService.DemandSubjectRelation(
                overlord,
                target,
                type))
            {
                return;
            }

            // The action may have ended a war, so rebuild both diplomacy lists
            // before selecting the same target again.
            diplomacyVm.RefreshDiplomacyList();
            diplomacyVm.SelectKingdom(target);
        }
    }
}
