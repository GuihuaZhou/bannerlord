using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition.Distribution;
using ModifiedPolitics.Models.WarDisposition.Events;
using ModifiedPolitics.Models.WarDisposition.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using CampaignHero = TaleWorlds.CampaignSystem.Hero;

namespace ModifiedPolitics.Models.WarDisposition.Listeners.Hero
{
    /// <summary>
    /// 监听领主被俘、战死和处决事件。
    /// 每名 Hero 都作为一次独立事件处理，与所属 Party 的胜负和覆灭分别累计。
    /// </summary>
    public sealed class HeroWarEventBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(
                this,
                OnHeroPrisonerTaken);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(
                this,
                OnHeroKilled);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 本监听器不保存状态，所有结果均写入 WarDispositionManager。
        }

        private static void OnHeroPrisonerTaken(
            PartyBase capturer,
            CampaignHero prisoner)
        {
            if (!TryGetEligibleHeroClan(prisoner, out Clan prisonerClan)
                || !TryGetCapturerClan(capturer, out Clan capturerClan))
            {
                return;
            }

            // 交换俘虏、友军移交等行为不属于战争收益，也不应形成战争损失。
            if (capturerClan == prisonerClan
                || !FactionManager.IsAtWarAgainstFaction(
                    capturerClan.MapFaction,
                    prisonerClan.MapFaction))
            {
                ModLogger.Notice(
                    $"[战争倾向] 忽略英雄被俘 | 英雄={prisoner.Name} | " +
                    $"被俘家族={prisonerClan.Name} | 俘虏家族={capturerClan.Name} | " +
                    "原因=双方并非敌对政治势力");
                return;
            }

            ModLogger.Notice(
                $"[战争倾向] 识别英雄被俘 | 英雄={prisoner.Name} | " +
                $"被俘家族={prisonerClan.Name} | 俘虏家族={capturerClan.Name}");

            // 损失与收益是两个独立事件，分别向各自王国内部传播。
            WarDispositionEventDistributor.DistributeClanEvent(
                prisonerClan,
                WarDispositionEventType.ClanMemberCaptured);
            WarDispositionEventDistributor.DistributeClanEvent(
                capturerClan,
                WarDispositionEventType.EnemyCaptured);
        }

        private static void OnHeroKilled(
            CampaignHero victim,
            CampaignHero killer,
            KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification)
        {
            if (!TryGetEligibleHeroClan(victim, out Clan victimClan))
            {
                return;
            }

            WarDispositionEventType? eventType = GetDeathEventType(detail);

            if (!eventType.HasValue)
            {
                return;
            }

            ModLogger.Notice(
                $"[战争倾向] 识别英雄死亡 | 英雄={victim.Name} | " +
                $"家族={victimClan.Name} | 原因={detail} | " +
                $"执行者={(killer?.Name?.ToString() ?? "无/未知")} | " +
                $"事件={eventType.Value}");

            WarDispositionEventDistributor.DistributeClanEvent(
                victimClan,
                eventType.Value);
        }

        private static WarDispositionEventType? GetDeathEventType(
            KillCharacterAction.KillCharacterActionDetail detail)
        {
            switch (detail)
            {
                case KillCharacterAction.KillCharacterActionDetail.DiedInBattle:
                case KillCharacterAction.KillCharacterActionDetail.WoundedInBattle:
                    return WarDispositionEventType.ClanMemberKilled;

                case KillCharacterAction.KillCharacterActionDetail.Executed:
                case KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent:
                    return WarDispositionEventType.ClanMemberExecuted;

                default:
                    // 老死、难产、谋杀、失踪等均不属于战争倾向事件。
                    return null;
            }
        }

        private static bool TryGetEligibleHeroClan(
            CampaignHero hero,
            out Clan clan)
        {
            clan = hero?.Clan;

            // 只统计有 Clan 身份的领主；名人、士兵和强盗英雄不参与。
            return hero != null
                && hero.IsLord
                && WarDispositionClanEligibility.IsEligible(clan);
        }

        private static bool TryGetCapturerClan(
            PartyBase capturer,
            out Clan clan)
        {
            // Owner 同时适用于领主部队和定居点 Party；ActualClan 用于兼容
            // 部队所有者暂时不可用、但 Party 仍保留实际 Clan 的情况。
            clan = capturer?.MobileParty?.ActualClan
                ?? capturer?.Owner?.Clan;

            return WarDispositionClanEligibility.IsEligible(clan);
        }
    }
}
