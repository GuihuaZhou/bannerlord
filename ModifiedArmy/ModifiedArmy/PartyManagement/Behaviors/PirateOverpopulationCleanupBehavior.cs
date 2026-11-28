using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Behaviors
{
    /// <summary>Performs one conservative cleanup of pirate overpopulation in legacy saves.</summary>
    public sealed class PirateOverpopulationCleanupBehavior : CampaignBehaviorBase
    {
        private const int MaxCleanupPerClanPerDay = 20;
        private bool _cleanupCompleted;
        private bool _skipCleanup;

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_pirateOverpopulationCleanupCompleted", ref _cleanupCompleted);
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _skipCleanup = true;
            _cleanupCompleted = true;
        }

        private void OnGameLoaded(CampaignGameStarter starter) => _skipCleanup = false;

        private void OnDailyTick()
        {
            if (_skipCleanup || _cleanupCompleted || Campaign.Current == null) return;
            bool sawPirateClan = false;
            bool modelUsable = true;
            foreach (Clan clan in Clan.BanditFactions.ToList())
            {
                if (!IsPirateClan(clan)) continue;
                sawPirateClan = true;
                int target = Campaign.Current.Models.BanditDensityModel
                    .GetMaxSupportedNumberOfLootersForClan(clan);
                if (target <= 0)
                {
                    modelUsable = false;
                    ModLogger.Warn(string.Format(
                        "Pirate cleanup paused: density model returned {0} for clan {1}.",
                        target, clan.StringId));
                    break;
                }
                int current = clan.WarPartyComponents.Count;
                int excess = Math.Max(0, current - target);
                if (excess == 0) continue;
                List<MobileParty> candidates = clan.WarPartyComponents
                    .Select(component => component.MobileParty)
                    .Where(party => CanCleanupPirateParty(party, clan))
                    .OrderByDescending(party => party.MemberRoster.TotalManCount)
                    .ThenBy(party => party.StringId, StringComparer.Ordinal).ToList();
                int limit = Math.Min(excess,
                    Math.Min(candidates.Count, MaxCleanupPerClanPerDay));
                int removed = 0;
                for (int i = 0; i < limit; i++)
                {
                    MobileParty party = candidates[i];
                    if (!CanCleanupPirateParty(party, clan)) continue;
                    Settlement hideout = Settlement.FindFirst(x => x.IsHideout);
                    DestroyPartyAction.ApplyForDisbanding(party, hideout);
                    removed++;
                }
                ModLogger.Info(string.Format(
                    "Pirate cleanup: clan={0} current={1} target={2} excess={3} eligible={4} removed={5}.",
                    clan.StringId, current, target, excess, candidates.Count, removed));
            }
            if (modelUsable && sawPirateClan) _cleanupCompleted = true;
        }

        private static bool CanCleanupPirateParty(MobileParty party, Clan clan)
        {
            if (party == null || !party.IsActive || party.IsMainParty ||
                party.ActualClan != clan || !party.IsBandit || !IsPirateClan(clan) ||
                party.MapEvent != null || party.SiegeEvent != null || party.Army != null ||
                party.AttachedTo != null || party.IsCurrentlyUsedByAQuest)
                return false;
            if (!GetNavalBool(party, "HasNavalNavigationCapability") ||
                !GetNavalBool(party, "IsCurrentlyAtSea") || MobileParty.MainParty == null)
                return false;
            float safeDistance = MobileParty.MainParty.SeeingRange * 2f;
            return party.Position.DistanceSquared(MobileParty.MainParty.Position) >=
                safeDistance * safeDistance;
        }

        private static bool IsPirateClan(Clan clan) => clan != null && clan.Culture != null &&
            !clan.Culture.CanHaveSettlement && clan.IsBanditFaction &&
            GetNavalBool(clan, "HasNavalNavigationCapability");

        private static bool GetNavalBool(object instance, string propertyName)
        {
            var property = instance?.GetType().GetProperty(propertyName);
            return property?.PropertyType == typeof(bool) &&
                (bool)property.GetValue(instance, null);
        }
    }
}
