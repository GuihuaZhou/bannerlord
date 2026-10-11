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

namespace ModifiedArmy.Mercenary.Patches
{
    /// <summary>
    /// Replaces tavern mercenary generation with the settlement culture's
    /// XML-configured probability and weighted troop pool. Each troop entry
    /// owns its exact generation range.
    /// Caravan guards retain their separate native refresh chance.
    /// </summary>
    [HarmonyPatch(
        typeof(RecruitmentCampaignBehavior),
        "UpdateCurrentMercenaryTroopAndCount")]
    public static class MercenaryMarketPatch
    {
        public static bool Prefix(
            RecruitmentCampaignBehavior __instance,
            Town town,
            bool forceUpdate = false)
        {
            var mercenaryData = __instance.GetMercenaryData(town);

            if (!forceUpdate && mercenaryData.HasAvailableMercenary(
                Occupation.NotAssigned))
            {
                return false;
            }

            MercenaryTemplate template = MercenaryTemplateManager.Instance
                .GetTemplateByCulture(town.Culture);
            float spawnChance = template?.SpawnChance ?? 0.3f;

            if (MBRandom.RandomFloat < spawnChance)
            {
                MercenaryTroopEntry selectedEntry = template?
                    .SelectWeightedTroop();
                CharacterObject selectedTroop = selectedEntry?.Troop;

                if (selectedTroop != null && selectedEntry != null)
                {
                    int finalCount = MBRandom.RandomInt(
                        selectedEntry.MinCount,
                        selectedEntry.MaxCount + 1);
                    mercenaryData.ChangeMercenaryType(
                        selectedTroop,
                        finalCount);

                    if (RecruitmentLogFilter.ShouldLog(town.OwnerClan))
                    {
                        TextObject message = GameTexts.FindText(
                            "str_modifiedarmy_mercenary_market_refreshed");
                        message.SetTextVariable(
                            "SETTLEMENT_NAME",
                            town.Name);
                        message.SetTextVariable("COUNT", finalCount);
                        message.SetTextVariable(
                            "TROOP_NAME",
                            selectedTroop.Name);
                        ModLogger.Debug(message.ToString());
                    }

                    return false;
                }
            }

            // Keep caravan guards on the native independent refresh chance.
            if (MBRandom.RandomFloat < Campaign.Current.Models
                .TavernMercenaryTroopsModel.RegularMercenariesSpawnChance)
            {
                CharacterObject caravanGuard = town.Culture.CaravanGuard;
                if (caravanGuard != null)
                {
                    return false;
                }
            }

            return false;
        }
    }
}
