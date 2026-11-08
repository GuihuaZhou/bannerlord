using System.Linq;
using HarmonyLib;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Diagnostics;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace ModifiedPolitics.HeroOffices.Patches
{
    /// <summary>
    /// Enforces marshal army authority and applies the marshal's influence discounts.
    /// </summary>
    [HarmonyPatch]
    internal static class MarshalArmyPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Kingdom), "CreateArmy")]
        private static bool RequireMarshalForArmy(
            Kingdom __instance,
            Hero armyLeader,
            Settlement targetSettlement,
            Army.ArmyTypes selectedArmyType)
        {
            bool relevant = Clan.PlayerClan?.Kingdom == __instance
                            && (armyLeader == __instance?.Leader || IsAppointedMarshal(armyLeader, __instance));
            if (__instance == null || armyLeader == null || armyLeader == __instance.Leader)
            {
                if (relevant)
                    TraceCreateArmy(__instance, armyLeader, targetSettlement, selectedArmyType, true);
                return true;
            }

            bool hasMarshal = HasAnyMarshal(__instance);
            if (!hasMarshal)
            {
                if (relevant)
                    TraceCreateArmy(__instance, armyLeader, targetSettlement, selectedArmyType, true);
                return true;
            }

            bool allowed = IsArmyAuthority(armyLeader, __instance);
            if (!allowed && armyLeader == Hero.MainHero)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=MP_MarshalRequired}Only the ruler or the kingdom marshal may create an army.").ToString()));
            }

            if (relevant || Clan.PlayerClan?.Kingdom == __instance)
                TraceCreateArmy(__instance, armyLeader, targetSettlement, selectedArmyType, allowed);

            return allowed;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AiMilitaryBehavior), nameof(AiMilitaryBehavior.FindBestTargetAndItsValueForFaction))]
        private static bool RestrictVanillaArmyGathering(PartyThinkParams p)
        {
            if (p == null || !p.WillGatherAnArmy)
                return true;

            MobileParty party = p.MobilePartyOf;
            Kingdom kingdom = party?.MapFaction as Kingdom;
            if (!HasAnyMarshal(kingdom))
                return true;

            Hero leader = party?.LeaderHero;
            return IsArmyAuthority(leader, kingdom);
        }

        private static bool HasAnyMarshal(Kingdom kingdom)
        {
            return kingdom != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(assignment => assignment?.Hero != null) == true;
        }

        private static bool IsAppointedMarshal(Hero hero, Kingdom kingdom)
        {
            return hero != null && kingdom != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(assignment => assignment?.Hero == hero) == true;
        }

        private static bool IsArmyAuthority(Hero hero, Kingdom kingdom)
        {
            return hero != null && kingdom != null
                   && (hero == kingdom.Leader || IsAppointedMarshal(hero, kingdom));
        }

        private static void TraceCreateArmy(
            Kingdom kingdom,
            Hero armyLeader,
            Settlement targetSettlement,
            Army.ArmyTypes selectedArmyType,
            bool allowed)
        {
            bool hasMarshal = HasAnyMarshal(kingdom);
            MarshalArmyDebugTrace.Write(
                "phase=CREATE_ARMY_ATTEMPT kingdom=" + MarshalArmyDebugTrace.DescribeKingdom(kingdom)
                + " leader=" + MarshalArmyDebugTrace.DescribeHero(armyLeader)
                + " party=" + MarshalArmyDebugTrace.DescribeParty(armyLeader?.PartyBelongedTo)
                + " armyType=" + selectedArmyType
                + " target=" + MarshalArmyDebugTrace.DescribeSettlement(targetSettlement)
                + " hasMarshal=" + hasMarshal
                + " isRuler=" + (armyLeader == kingdom?.Leader)
                + " isAppointedMarshal=" + IsAppointedMarshal(armyLeader, kingdom)
                + " allowedByMarshalPatch=" + allowed
                + " result=" + (allowed
                    ? "CREATE_ARMY_ALLOWED_BY_OFFICE_PATCH"
                    : "CREATE_ARMY_REJECTED_BY_OFFICE_PATCH"));
        }
    }
}
