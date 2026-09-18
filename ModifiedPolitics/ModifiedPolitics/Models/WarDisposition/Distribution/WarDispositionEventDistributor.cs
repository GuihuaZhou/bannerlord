using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition.Calculation;
using ModifiedPolitics.Models.WarDisposition.Events;
using ModifiedPolitics.Models.WarDisposition.Rules;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.Models.WarDisposition.Distribution
{
    /// <summary>
    /// 将一个 Clan 实际经历的事件分发给本 Clan 和同王国的其他 Clan。
    /// 数值倍率仍由 WarDispositionManager 统一计算，分发层不直接修改存档数据。
    /// </summary>
    public static class WarDispositionEventDistributor
    {
        /// <summary>
        /// 本 Clan 按 100% 接收，同王国其他有效 Clan 按 20% 接收。
        /// </summary>
        public static void DistributeClanEvent(
            Clan eventClan,
            WarDispositionEventType eventType)
        {
            if (!WarDispositionClanEligibility.IsEligible(eventClan)
                || Campaign.Current == null)
            {
                return;
            }

            WarDispositionManager manager = Campaign.Current
                .GetCampaignBehavior<WarDispositionManager>();

            if (manager == null)
            {
                return;
            }

            WarDispositionCalculationResult ownResult = manager.ApplyEvent(
                eventClan,
                eventType,
                true);

            WarDispositionData ownData = manager.GetOrCreateData(eventClan);
            ModLogger.Notice(
                $"[战争倾向] {eventType} | 家族={eventClan.Name} | " +
                $"变化={(ownResult?.AppliedDelta ?? 0f):+0.00;-0.00;0.00} | " +
                $"当前={(ownData?.Value ?? 0f):0.00}");

            Kingdom kingdom = eventClan.Kingdom;

            if (kingdom == null)
            {
                return;
            }

            int sharedClanCount = 0;

            foreach (Clan clan in kingdom.Clans)
            {
                if (clan != eventClan
                    && WarDispositionClanEligibility.IsEligible(clan))
                {
                    manager.ApplyEvent(clan, eventType, false);
                    sharedClanCount++;
                }
            }

            ModLogger.Notice(
                $"[战争倾向] 王国传播 | 来源家族={eventClan.Name} | " +
                $"事件={eventType} | 其他家族={sharedClanCount} | 倍率=20%");
        }

    }
}
