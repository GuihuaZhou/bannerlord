using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Patches
{
    [HarmonyPatch(typeof(Kingdom), "CreateArmy")]
    public class Patch_CreateArmy
    {
        public static bool Prefix(Hero armyLeader)
        {
            if (!CampaignState.IsReady) return true;

            if (armyLeader != null && armyLeader != Hero.MainHero && armyLeader.IsFemale)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[ModifiedArmy] Blocked female lord {armyLeader.Name} from creating an army!",
                    Color.FromUint(0xFFFF5555)
                ));
                return false;
            }
            return true;
        }
    }
}