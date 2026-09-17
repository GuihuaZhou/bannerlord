using System.Collections.Generic;
using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition.Distribution;
using ModifiedPolitics.Models.WarDisposition.Events;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedPolitics.Models.WarDisposition.Listeners.Battle
{
    /// <summary>
    /// 监听 Clan 领主部队的战斗胜负和覆灭事件。
    /// 战败与覆灭按设计独立计数，因此同一部队可能先获得战败，再获得覆灭影响。
    /// </summary>
    public sealed class PartyBattleEventBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(
                this,
                OnMapEventEnded);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(
                this,
                OnMobilePartyDestroyed);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 本监听器不保存状态，所有结果均写入 WarDispositionManager。
        }

        private static void OnMapEventEnded(MapEvent mapEvent)
        {
            if (!IsCountableBattle(mapEvent))
            {
                return;
            }

            ApplyToSide(
                mapEvent.GetMapEventSide(mapEvent.WinningSide),
                WarDispositionEventType.PartyVictory);
            ApplyToSide(
                mapEvent.GetMapEventSide(mapEvent.DefeatedSide),
                WarDispositionEventType.PartyDefeat);
        }

        private static void OnMobilePartyDestroyed(
            MobileParty destroyedParty,
            PartyBase destroyerParty)
        {
            // 正常解散等流程也会触发 MobilePartyDestroyed，但没有摧毁者。
            // 野怪不是政治势力：被野怪摧毁的部队不应改变战争倾向。
            if (!IsPoliticalParty(destroyerParty))
            {
                if (TryGetWarClan(destroyedParty, out Clan ignoredClan))
                {
                    ModLogger.Notice(
                        $"[战争倾向] 忽略部队覆灭 | 被摧毁={destroyedParty.Name} " +
                        $"({ignoredClan.Name}) | 摧毁者={GetPartyName(destroyerParty)} | " +
                        "原因=摧毁者不是政治势力");
                }

                return;
            }

            if (!TryGetWarClan(destroyedParty, out Clan clan))
            {
                return;
            }

            WarDispositionEventDistributor.DistributeClanEvent(
                clan,
                WarDispositionEventType.PartyDestroyed);
        }

        private static bool IsCountableBattle(MapEvent mapEvent)
        {
            if (mapEvent == null || !mapEvent.HasWinner)
            {
                return false;
            }

            // 劫掠与强征有各自的领地事件，不能同时伪装成普通部队胜负。
            if (mapEvent.IsRaid
                || mapEvent.IsForcingSupplies
                || mapEvent.IsForcingVolunteers)
            {
                return false;
            }

            // 只有政治势力之间的战斗才形成战争倾向。
            // 任意一方完全由强盗、野怪等非政治单位组成时，整场战斗忽略。
            bool attackerIsPolitical = HasPoliticalParty(mapEvent.AttackerSide);
            bool defenderIsPolitical = HasPoliticalParty(mapEvent.DefenderSide);

            if (!attackerIsPolitical || !defenderIsPolitical)
            {
                ModLogger.Notice(
                    $"[战争倾向] 忽略战斗胜负 | " +
                    $"进攻方={GetSideName(mapEvent.AttackerSide)} | " +
                    $"防守方={GetSideName(mapEvent.DefenderSide)} | " +
                    "原因=至少一方不是政治势力");
                return false;
            }

            return true;
        }

        private static bool HasPoliticalParty(MapEventSide side)
        {
            if (side == null)
            {
                return false;
            }

            foreach (MapEventParty mapEventParty in side.Parties)
            {
                if (IsPoliticalParty(mapEventParty?.Party))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyToSide(
            MapEventSide side,
            WarDispositionEventType eventType)
        {
            if (side == null)
            {
                return;
            }

            // 防止异常或兼容性 Mod 让同一 Party 重复出现在事件列表中。
            HashSet<PartyBase> handledParties = new HashSet<PartyBase>();

            foreach (MapEventParty mapEventParty in side.Parties)
            {
                PartyBase party = mapEventParty?.Party;

                if (party == null || !handledParties.Add(party))
                {
                    continue;
                }

                if (TryGetWarClan(party.MobileParty, out Clan clan))
                {
                    ModLogger.Notice(
                        $"[战争倾向] 识别战斗事件 | 部队={party.Name} | " +
                        $"家族={clan.Name} | 事件={eventType}");
                    WarDispositionEventDistributor.DistributeClanEvent(
                        clan,
                        eventType);
                }
            }
        }

        private static bool TryGetWarClan(
            MobileParty mobileParty,
            out Clan clan)
        {
            clan = mobileParty?.ActualClan;

            // IsLordParty 能排除商队、民兵、驻军和强盗等非 Clan 作战部队。
            return mobileParty != null
                && mobileParty.IsLordParty
                && clan != null
                && !clan.IsBanditFaction
                && !clan.IsEliminated;
        }

        private static bool IsPoliticalParty(PartyBase party)
        {
            if (party?.MapFaction is Kingdom kingdom)
            {
                return !kingdom.IsEliminated;
            }

            if (party?.MapFaction is Clan clan)
            {
                return !clan.IsBanditFaction && !clan.IsEliminated;
            }

            return false;
        }

        private static string GetSideName(MapEventSide side)
        {
            return GetPartyName(side?.LeaderParty);
        }

        private static string GetPartyName(PartyBase party)
        {
            return party?.Name?.ToString() ?? "无/未知";
        }
    }
}
