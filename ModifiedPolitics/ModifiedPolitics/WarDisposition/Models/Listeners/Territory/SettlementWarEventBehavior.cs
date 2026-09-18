using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition.Distribution;
using ModifiedPolitics.Models.WarDisposition.Events;
using ModifiedPolitics.Models.WarDisposition.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using CampaignHero = TaleWorlds.CampaignSystem.Hero;

namespace ModifiedPolitics.Models.WarDisposition.Listeners.Territory
{
    /// <summary>
    /// 监听村庄劫掠, 定居点攻占和王国封赏.
    /// 失守, 攻占与获封是三个独立事实, 满足条件时分别累计.
    /// </summary>
    public sealed class SettlementWarEventBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(
                this,
                OnRaidCompleted);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(
                this,
                OnSettlementOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 本监听器不保存状态, 所有结果均写入 WarDispositionManager.
        }

        private static void OnRaidCompleted(
            BattleSideEnum winnerSide,
            RaidEventComponent raidEvent)
        {
            // 只有进攻方真正完成劫掠才计算; 守军获胜或事件取消均忽略.
            if (winnerSide != BattleSideEnum.Attacker || raidEvent?.MapEvent == null)
            {
                return;
            }

            MapEvent mapEvent = raidEvent.MapEvent;
            Settlement villageSettlement = mapEvent.MapEventSettlement;
            Clan victimClan = villageSettlement?.OwnerClan;
            Clan raiderClan = ResolvePartyClan(mapEvent.AttackerSide?.LeaderParty);

            if (!WarDispositionClanEligibility.IsEligible(victimClan)
                || !WarDispositionClanEligibility.IsEligible(raiderClan)
                || victimClan == raiderClan
                || !FactionManager.IsAtWarAgainstFaction(
                    victimClan.MapFaction,
                    raiderClan.MapFaction))
            {
                return;
            }

            ModLogger.Notice(
                $"[战争倾向] 识别村庄被劫掠 | 村庄={villageSettlement.Name} | " +
                $"受损家族={victimClan.Name} | 劫掠家族={raiderClan.Name}");

            WarDispositionEventDistributor.DistributeClanEvent(
                victimClan,
                WarDispositionEventType.VillageRaided);
        }

        private static void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            CampaignHero newOwner,
            CampaignHero oldOwner,
            CampaignHero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement == null || !settlement.IsFortification)
            {
                return;
            }

            if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege)
            {
                HandleSettlementCaptured(
                    settlement,
                    oldOwner?.Clan,
                    capturerHero?.Clan);
                return;
            }

            if (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision)
            {
                HandleSettlementGranted(settlement, newOwner?.Clan);
            }
        }

        private static void HandleSettlementCaptured(
            Settlement settlement,
            Clan formerOwnerClan,
            Clan capturerClan)
        {
            WarDispositionEventType lossEvent = settlement.IsTown
                ? WarDispositionEventType.TownLost
                : WarDispositionEventType.CastleLost;
            WarDispositionEventType captureEvent = settlement.IsTown
                ? WarDispositionEventType.TownCaptured
                : WarDispositionEventType.CastleCaptured;

            if (WarDispositionClanEligibility.IsEligible(formerOwnerClan))
            {
                ModLogger.Notice(
                    $"[战争倾向] 识别定居点失守 | 定居点={settlement.Name} | " +
                    $"原家族={formerOwnerClan.Name} | 事件={lossEvent}");
                WarDispositionEventDistributor.DistributeClanEvent(
                    formerOwnerClan,
                    lossEvent);
            }

            if (WarDispositionClanEligibility.IsEligible(capturerClan))
            {
                ModLogger.Notice(
                    $"[战争倾向] 识别定居点攻占 | 定居点={settlement.Name} | " +
                    $"攻占家族={capturerClan.Name} | 事件={captureEvent}");
                WarDispositionEventDistributor.DistributeClanEvent(
                    capturerClan,
                    captureEvent);
            }
        }

        private static void HandleSettlementGranted(
            Settlement settlement,
            Clan grantedClan)
        {
            if (!WarDispositionClanEligibility.IsEligible(grantedClan))
            {
                return;
            }

            WarDispositionEventType grantedEvent = settlement.IsTown
                ? WarDispositionEventType.TownGranted
                : WarDispositionEventType.CastleGranted;

            ModLogger.Notice(
                $"[战争倾向] 识别定居点获封 | 定居点={settlement.Name} | " +
                $"获封家族={grantedClan.Name} | 事件={grantedEvent}");
            WarDispositionEventDistributor.DistributeClanEvent(
                grantedClan,
                grantedEvent);
        }

        private static Clan ResolvePartyClan(PartyBase party)
        {
            return party?.MobileParty?.ActualClan
                ?? party?.Owner?.Clan;
        }

    }
}
