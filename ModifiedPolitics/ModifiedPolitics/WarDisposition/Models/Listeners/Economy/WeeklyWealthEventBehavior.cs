using System;
using System.Collections.Generic;
using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition.Calculation;
using ModifiedPolitics.Models.WarDisposition.Distribution;
using ModifiedPolitics.Models.WarDisposition.Events;
using ModifiedPolitics.Models.WarDisposition.Rules;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.Models.WarDisposition.Listeners.Economy
{
    /// <summary>
    /// 每周比较 Clan.Gold, 并将财富变化转换为战争倾向事件.
    /// </summary>
    public sealed class WeeklyWealthEventBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(
                this,
                OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 本监听器不保存状态, 财富基准保存在 WarDispositionData 中.
        }

        private static void OnWeeklyTick()
        {
            WarDispositionManager manager = Campaign.Current?
                .GetCampaignBehavior<WarDispositionManager>();

            if (manager == null)
            {
                return;
            }

            List<WealthEvent> events = new List<WealthEvent>();

            // 第一阶段统一清空上周影响并计算变化, 避免擦除其他 Clan 的传播结果.
            foreach (Clan clan in Clan.All)
            {
                if (!WarDispositionClanEligibility.IsEligible(clan))
                {
                    continue;
                }

                WarDispositionData data = manager.GetOrCreateData(clan);
                int currentWealth = GetClampedWealth(clan);
                float changePercent = WarDispositionWealthCalculator
                    .CalculateChangePercent(
                        data.PreviousWeeklyWealth,
                        currentWealth);
                float eventBaseValue = WarDispositionWealthCalculator
                    .GetEventBaseValue(changePercent);

                data.WeeklyWealthChangePercent = changePercent;
                data.WeeklyWealthInfluence = 0f;
                data.PreviousWeeklyWealth = currentWealth;

                if (eventBaseValue != 0f)
                {
                    events.Add(new WealthEvent(
                        clan,
                        eventBaseValue));
                }
            }

            // 第二阶段应用所有事件, 此时不会再有后续清零覆盖传播值.
            foreach (WealthEvent wealthEvent in events)
            {
                WarDispositionEventDistributor.DistributeClanEvent(
                    wealthEvent.Clan,
                    WarDispositionEventType.WeeklyWealthChange,
                    wealthEvent.EventBaseValue,
                    false);
            }

            LogWeeklyResult(manager, events.Count);
        }

        private static void LogWeeklyResult(
            WarDispositionManager manager,
            int eventCount)
        {
            WarDispositionData playerData = manager.GetOrCreateData(
                Clan.PlayerClan);

            if (playerData != null)
            {
                ModLogger.Notice(
                    $"[战争倾向] 每周财富结算 | 家族={Clan.PlayerClan.Name} | " +
                    $"财富变化={playerData.WeeklyWealthChangePercent:+0.##;-0.##;0}% | " +
                    $"倾向影响={playerData.WeeklyWealthInfluence:+0.00;-0.00;0.00} | " +
                    $"当前={playerData.Value:0.00}");
            }

            ModLogger.Notice(
                $"[战争倾向] 每周财富结算完成 | 非零财富事件={eventCount}");
        }

        private static int GetClampedWealth(Clan clan)
        {
            long wealth = clan?.Gold ?? 0;

            if (wealth <= 0)
            {
                return 0;
            }

            return wealth >= int.MaxValue
                ? int.MaxValue
                : (int)wealth;
        }

        private sealed class WealthEvent
        {
            public WealthEvent(
                Clan clan,
                float eventBaseValue)
            {
                Clan = clan;
                EventBaseValue = eventBaseValue;
            }

            public Clan Clan { get; }
            public float EventBaseValue { get; }
        }
    }
}
