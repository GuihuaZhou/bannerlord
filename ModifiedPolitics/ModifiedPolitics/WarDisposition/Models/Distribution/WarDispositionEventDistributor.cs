using ModifiedPolitics.Tool;
using ModifiedPolitics.Models.WarDisposition.Calculation;
using ModifiedPolitics.Models.WarDisposition.Events;
using ModifiedPolitics.Models.WarDisposition.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

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
            WarDispositionEventType eventType,
            float? baseValueOverride = null,
            bool writeNoticeLog = true)
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
                true,
                baseValueOverride);

            WarDispositionData ownData = manager.GetOrCreateData(eventClan);
            bool shouldLog = writeNoticeLog &&
                eventClan.Kingdom != null &&
                eventClan.Kingdom == Clan.PlayerClan?.Kingdom;

            if (shouldLog)
            {
                TextObject message = new TextObject(
                    "{=ModifiedPolitics_WarDispositionClanChanged}" +
                    "[War Disposition] {CLAN_NAME} experienced {EVENT_TYPE}. " +
                    "Its disposition changed by {DELTA} and is now {CURRENT}.");
                message.SetTextVariable("CLAN_NAME", eventClan.Name);
                message.SetTextVariable("EVENT_TYPE", eventType.ToString());
                message.SetTextVariable(
                    "DELTA",
                    (ownResult?.AppliedDelta ?? 0f)
                        .ToString("+0.00;-0.00;0.00"));
                message.SetTextVariable(
                    "CURRENT",
                    (ownData?.Value ?? 0f).ToString("0.00"));
                ModLogger.Notice(message.ToString());
            }

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
                    manager.ApplyEvent(
                        clan,
                        eventType,
                        false,
                        baseValueOverride);
                    sharedClanCount++;
                }
            }

            if (shouldLog)
            {
                TextObject message = new TextObject(
                    "{=ModifiedPolitics_WarDispositionKingdomSpread}" +
                    "[War Disposition] The event affecting {CLAN_NAME} also influenced " +
                    "{CLAN_COUNT} other clans in the kingdom at twenty percent strength.");
                message.SetTextVariable("CLAN_NAME", eventClan.Name);
                message.SetTextVariable("CLAN_COUNT", sharedClanCount);
                ModLogger.Notice(message.ToString());
            }
        }

    }
}
