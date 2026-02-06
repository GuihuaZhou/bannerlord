using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Patch
{
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "UpdateCurrentMercenaryTroopAndCount")]
    public static class RecruitmentPatches
    {
        /// <summary>
        /// 替换雇佣兵生成逻辑
        /// 只生成基础雇佣兵，且确保人数至少10人
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="town"></param>
        /// <param name="forceUpdate"></param>
        /// <returns></returns>
        public static bool UpdateCurrentMercenaryTroopAndCountPrefix(
            RecruitmentCampaignBehavior __instance,
            Town town,
            bool forceUpdate)
        {
            // 1. 获取该城镇的雇佣兵数据
            var mercenaryData = __instance.GetMercenaryData(town);

            if (!forceUpdate && mercenaryData.HasAvailableMercenary(Occupation.NotAssigned))
            {
                // 禁止升级
                return false;
            }

            if (MBRandom.RandomFloat < Campaign.Current.Models.TavernMercenaryTroopsModel.RegularMercenariesSpawnChance)
            {
                // 2. 从基础雇佣兵池中随机选择一个
                List<CharacterObject> basicMercenaries = town.Culture.BasicMercenaryTroops;
                if (basicMercenaries == null || basicMercenaries.Count == 0)
                    return false;

                CharacterObject selectedTroop = basicMercenaries[MBRandom.RandomInt(basicMercenaries.Count)];

                // 3. 计算数量，但确保至少10人
                int calculatedCount = FindNumberOfMercenariesWillBeAdded(selectedTroop, forceUpdate);
                int finalCount = MBRandom.RandomInt(5, 15);

                // 4. 更新雇佣兵数据
                mercenaryData.ChangeMercenaryType(selectedTroop, finalCount);

                return false;
            }

            CharacterObject caravanGuard = town.Culture.CaravanGuard;
            if (caravanGuard != null)
            {
                int count = FindNumberOfMercenariesWillBeAdded(caravanGuard, forceUpdate);

                int finalCount = MBRandom.RandomInt(5, 15);
                mercenaryData.ChangeMercenaryType(caravanGuard, finalCount);

                return false;
            }

            // 返回 false，跳过原版复杂的升级和概率逻辑
            return false;
        }

        private static int FindNumberOfMercenariesWillBeAdded(CharacterObject character, bool dailyUpdate = false)
        {
            int tier = Campaign.Current.Models.CharacterStatsModel.GetTier(character);
            int maxCharacterTier = Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier;
            int num = (maxCharacterTier - tier) * 2;
            int num2 = (maxCharacterTier - tier) * 5;
            float randomFloat = MBRandom.RandomFloat;
            float randomFloat2 = MBRandom.RandomFloat;
            return MBRandom.RoundRandomized(MBMath.ClampFloat((randomFloat * randomFloat2 * (float)(num2 - num) + (float)num) * (dailyUpdate ? 0.1f : 1f), 1f, (float)num2));
        }
    }
}
