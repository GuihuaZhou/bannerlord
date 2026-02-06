using Helpers;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Library;

namespace ModifiedArmy.Models
{
    public class AIBuildingAutoBoostBehavior : CampaignBehaviorBase
    {
        private const int BOOST_AMOUNT = 10000;       // 固定投入金额
        private const int MIN_GOLD_REQUIRED = 50000;  // 需要至少5倍的金额才行动

        public override void RegisterEvents()
        {
            // 监听每周事件
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        private void OnWeeklyTick()
        {
            // 遍历所有定居点
            foreach (var settlement in Settlement.All)
            {
                if (settlement?.Town == null)
                    continue;

                // 跳过玩家领地
                if (settlement.OwnerClan == Clan.PlayerClan)
                    continue;

                Town town = settlement.Town;
                Clan ownerClan = settlement.OwnerClan;

                // 检查是否已经投入了加速金币
                if (town.BoostBuildingProcess > 0)
                    continue;

                // 判断建筑队列是否为空
                if (town.BuildingsInProgress.Count == 0)
                    continue;

                // 通过 Clan 获取金币
                if (ownerClan.Gold < MIN_GOLD_REQUIRED)
                    continue;

                var old_gold = ownerClan.Gold;

                // 投入10000金币进行加速
                GiveGoldAction.ApplyBetweenCharacters(ownerClan.Leader, null, BOOST_AMOUNT, false);

                string logMessage = $"[AI Auto Boost] {settlement.Name} ({ownerClan.Name}) 投入 {BOOST_AMOUNT} 金币加速建设。投资前金库: {old_gold}, 当前金库: {ownerClan.Gold}";
                ModLogger.Notice(logMessage);
            }
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
