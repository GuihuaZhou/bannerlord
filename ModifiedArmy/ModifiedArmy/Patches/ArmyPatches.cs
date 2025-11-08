using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Patches
{
    //[HarmonyPatch(typeof(Kingdom), "CreateArmy")]
    //public static class Patch_CreateArmy
    //{
    //    public static bool Prefix(Hero armyLeader)
    //    {
    //        if (!CampaignState.IsReady)
    //            return true;

    //        // 仅拦截：非主角 + 女性 + 不是其 Clan 的 Leader
    //        if (armyLeader != null
    //            && armyLeader != Hero.MainHero
    //            && armyLeader.IsFemale
    //            && armyLeader.Clan != null
    //            && armyLeader != armyLeader.Clan.Leader)
    //        {
    //            InformationManager.DisplayMessage(new InformationMessage(
    //                $"[ModifiedArmy] Blocked female non-leader {armyLeader.Name} from creating an army!",
    //                Color.FromUint(0xFFFF5555)
    //            ));
    //            return false; // 阻止创建军队
    //        }

    //        return true; // 允许创建
    //    }
    //}
}