using HarmonyLib;
using System.Reflection;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Patches
{
    // 修改troop和NPC的wage
    [HarmonyPatch(typeof(DefaultPartyWageModel), "GetCharacterWage")]
    public class GetCharacterWagePatch
    {
        public static bool Prefix(
            CharacterObject character,
            ref int __result)
        {
            int num;
            switch (character.Tier)
            {
                case 0:
                    num = 1;
                    break;
                case 1:
                    num = 2;
                    break;
                case 2:
                    num = 3;
                    break;
                case 3:
                    num = 5;
                    break;
                case 4:
                    num = 16; // 原来8
                    break;
                case 5:
                    num = 26; // 原来12
                    break;
                case 6:
                    num = 38; // 原来17
                    break;
                default:
                    num = 52; // 原来23
                    break;
            }
            if (character.Occupation == Occupation.Mercenary)
            {
                num = (int)((float)num * 1.5f);
            }

            __result = num;
            return false;
        }
    }
}