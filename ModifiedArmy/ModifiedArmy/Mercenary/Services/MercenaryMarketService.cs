using System.Reflection;
using HarmonyLib;
using ModifiedArmy.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Mercenary.Services
{
    internal static class MercenaryMarketService
    {
        public static void Refresh(RecruitmentCampaignBehavior behavior,
            Town town, bool forceUpdate)
        {
            var data = behavior.GetMercenaryData(town);
            if (!forceUpdate && data.HasAvailableMercenary(Occupation.NotAssigned))
                return;

            MercenaryTemplate template = MercenaryTemplateManager.Instance
                .GetTemplateByCulture(town.Culture);
            if (MBRandom.RandomFloat < (template?.SpawnChance ?? 0.3f))
            {
                MercenaryTroopEntry entry = template?.SelectWeightedTroop();
                CharacterObject troop = entry?.Troop;
                if (troop != null && entry != null)
                {
                    int count = MBRandom.RandomInt(entry.MinCount,
                        entry.MaxCount + 1);
                    data.ChangeMercenaryType(troop, count);
                    Log(town, troop, count);
                    return;
                }
            }

            // Preserve the existing independent random draw and no-op outcome.
            // Whether native caravan-guard state should be changed is tracked as
            // a gameplay defect, not altered by this architecture phase.
            if (MBRandom.RandomFloat < Campaign.Current.Models
                .TavernMercenaryTroopsModel.RegularMercenariesSpawnChance)
            {
                CharacterObject caravanGuard = town.Culture.CaravanGuard;
                if (caravanGuard != null && TrySelectNativeMercenary(
                        behavior, caravanGuard, out CharacterObject selected,
                        out int count))
                {
                    data.ChangeMercenaryType(selected, count);
                    Log(town, selected, count);
                }
            }
        }

        private static bool TrySelectNativeMercenary(
            RecruitmentCampaignBehavior behavior,
            CharacterObject root,
            out CharacterObject selected,
            out int count)
        {
            selected = null;
            count = 0;
            try
            {
                FieldInfo selectedField = AccessTools.Field(
                    typeof(RecruitmentCampaignBehavior), "_selectedTroop");
                MethodInfo totalMethod = AccessTools.Method(
                    typeof(RecruitmentCampaignBehavior),
                    "FindTotalMercenaryProbability");
                MethodInfo selectMethod = AccessTools.Method(
                    typeof(RecruitmentCampaignBehavior),
                    "FindRandomMercenaryTroop");
                MethodInfo countMethod = AccessTools.Method(
                    typeof(RecruitmentCampaignBehavior),
                    "FindNumberOfMercenariesWillBeAdded");
                if (selectedField == null || totalMethod == null ||
                    selectMethod == null || countMethod == null) return false;
                selectedField.SetValue(behavior, null);
                float total = (float)totalMethod.Invoke(
                    behavior, new object[] { root, 1f });
                selectMethod.Invoke(behavior, new object[]
                {
                    root, 1f, MBRandom.RandomFloat * total
                });
                selected = selectedField.GetValue(behavior) as CharacterObject;
                if (selected == null) return false;
                count = (int)countMethod.Invoke(
                    behavior, new object[] { selected, false });
                return count > 0;
            }
            catch (System.Exception exception)
            {
                ModLogger.Error(
                    "Caravan guard market refresh failed: " + exception);
                return false;
            }
        }

        private static void Log(Town town, CharacterObject troop, int count)
        {
            if (!RecruitmentLogFilter.ShouldLog(town.OwnerClan)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_mercenary_market_refreshed");
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("COUNT", count);
            message.SetTextVariable("TROOP_NAME", troop.Name);
            ModLogger.Debug(message.ToString());
        }
    }
}
