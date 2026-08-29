using HarmonyLib;
using ModifiedArmy.Models;
using System;
using System.Collections.Generic;
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
        /// 替换雇佣兵生成逻辑。
        /// 根据文化模板（MercenaryTemplate）配置刷新人数和概率。
        /// CaravanGuard 的生成由原版 RegularMercenariesSpawnChance 控制，不受影响。
        /// </summary>
        public static bool UpdateCurrentMercenaryTroopAndCountPrefix(
            RecruitmentCampaignBehavior __instance,
            Town town,
            bool forceUpdate)
        {
            var mercenaryData = __instance.GetMercenaryData(town);

            if (!forceUpdate && mercenaryData.HasAvailableMercenary(Occupation.NotAssigned))
            {
                return false;
            }

            // ============================================================
            // 1. 尝试生成雇佣兵（使用模板配置）
            // ============================================================
            var template = MercenaryTemplateManager.Instance.GetTemplateByCulture(town.Culture);
            float spawnChance = template?.SpawnChance ?? 0.3f;
            int minCount = template?.MinCount ?? 5;
            int maxCount = template?.MaxCount ?? 10;

            if (MBRandom.RandomFloat < spawnChance)
            {
                List<CharacterObject> basicMercenaries = town.Culture.BasicMercenaryTroops;
                if (basicMercenaries != null && basicMercenaries.Count > 0)
                {
                    CharacterObject selectedTroop = basicMercenaries[MBRandom.RandomInt(basicMercenaries.Count)];
                    int finalCount = MBRandom.RandomInt(minCount, maxCount);
                    mercenaryData.ChangeMercenaryType(selectedTroop, finalCount);
                    return false;
                }
            }

            // ============================================================
            // 2. 原版逻辑：CaravanGuard 的生成由 RegularMercenariesSpawnChance 控制
            // ============================================================
            if (MBRandom.RandomFloat < Campaign.Current.Models.TavernMercenaryTroopsModel.RegularMercenariesSpawnChance)
            {
                CharacterObject caravanGuard = town.Culture.CaravanGuard;
                if (caravanGuard != null)
                {
                    int count = MBRandom.RandomInt(3, 8);
                    mercenaryData.ChangeMercenaryType(caravanGuard, count);
                    return false;
                }
            }

            return false;
        }
    }
}