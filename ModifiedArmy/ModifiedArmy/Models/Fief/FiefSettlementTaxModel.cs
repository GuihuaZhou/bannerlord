using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.Fief
{
    public class FiefSettlementTaxModel : DefaultSettlementTaxModel
    {
        /// <summary>
        /// 开启封邑制度后，动态计算城镇的税收
        /// </summary>
        /// <param name="town"></param>
        /// <param name="includeDescriptions"></param>
        /// <returns></returns>
        public override ExplainedNumber CalculateTownTax(Town town, bool includeDescriptions = false)
        {
            // 获取原版计算结果
            ExplainedNumber original = base.CalculateTownTax(town, includeDescriptions);

            var manager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            float multiplier = manager.GetFiefTaxationMultiplier(town);

            float feudalTax = (float)(original.ResultNumber * multiplier);

            //if (town.OwnerClan == Clan.PlayerClan)
            //{
            //    string townName = town.Name?.ToString() ?? "Unknown Town";
            //    ModLogger.Debug(
            //        $"[Fief Tax Debug] Player's {townName}: " +
            //        $"Original tax = {original.ResultNumber:F2}, " +
            //        $"Multiplier = {multiplier:F2}, " +
            //        $"Final tax = {feudalTax:F2}"
            //    );
            //}

            if (!includeDescriptions)
                return new ExplainedNumber(feudalTax, false, null);

            // 如果需要保留解释（用于 UI 显示）
            ExplainedNumber result = new(feudalTax, true, null);
            return result;
        }
    }
}
