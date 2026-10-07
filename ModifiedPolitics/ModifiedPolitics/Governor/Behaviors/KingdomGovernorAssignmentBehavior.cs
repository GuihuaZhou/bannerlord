using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.Governor.Config;
using ModifiedPolitics.Governor.Models;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Governor.Behaviors
{
    /// <summary>
    /// Fills governor vacancies by kingdom each week without replacing existing governors.
    /// </summary>
    public sealed class KingdomGovernorAssignmentBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, AssignGovernorsForAllKingdoms);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
        {
            // A new campaign receives its first assignment pass without waiting for a weekly tick.
            AssignGovernorsForAllKingdoms();
        }

        private void AssignGovernorsForAllKingdoms()
        {
            foreach (Kingdom kingdom in Kingdom.All.ToList())
            {
                if (!IsEligibleKingdom(kingdom))
                    continue;

                // Release invalid borrowed governors before filling vacancies in the same pass.
                RemoveInvalidBorrowedGovernors(kingdom);
                AssignVacancies(kingdom);
            }
        }

        private static bool IsEligibleKingdom(Kingdom kingdom)
        {
            return kingdom != null
                   && !kingdom.IsEliminated
                   && kingdom.Culture != null
                   && GovernorPolicyManager.Instance.IsCentralizedAssignment(kingdom.Culture);
        }

        private static void RemoveInvalidBorrowedGovernors(Kingdom kingdom)
        {
            foreach (Town town in Town.AllTowns.Concat(Town.AllCastles).ToList())
            {
                Hero governor = town?.Governor;
                Clan ownerClan = town?.OwnerClan;
                if (governor == null || ownerClan == null || ownerClan.Kingdom != kingdom)
                    continue;

                // Vanilla remains responsible for same-clan cases; this only cleans borrowed governors.
                if (governor.Clan != ownerClan && governor.Clan?.Kingdom != ownerClan.Kingdom)
                {
                    ChangeGovernorAction.RemoveGovernorOf(governor);

                    if (ShouldLogForKingdom(kingdom))
                    {
                        TextObject message = new TextObject(
                            "{=MP_GovernorBorrowedRemoved}[Governor Assignment] Removed {HERO} from {SETTLEMENT} because the borrowed governor no longer belongs to the owning kingdom.");
                        message.SetTextVariable("HERO", governor.Name);
                        message.SetTextVariable("SETTLEMENT", town.Settlement.Name);
                        ModLogger.Notice(message.ToString());
                    }
                }
            }
        }

        private static void AssignVacancies(Kingdom kingdom)
        {
            // Higher-value fiefs are handled first; StringId makes ties deterministic.
            List<Town> vacancies = Town.AllTowns
                .Concat(Town.AllCastles)
                .Where(town => town?.OwnerClan?.Kingdom == kingdom)
                .Where(town => town.Governor == null && !HasIncomingGovernor(town))
                .OrderByDescending(GovernorAssignmentModel.GetSettlementPriority)
                .ThenBy(town => town.Settlement.StringId)
                .ToList();

            // The kingdom-wide pool allows a hero from Clan A to govern a fief owned by Clan B.
            List<Hero> candidates = kingdom.Clans
                .Where(IsEligibleClan)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => IsEligibleHero(hero, kingdom))
                .ToList();

            foreach (Town vacancy in vacancies)
            {
                // Recalculate cultural fit per fief and use StringId to break score ties.
                Hero best = candidates
                    .OrderByDescending(hero => GovernorAssignmentModel.GetCandidateScore(hero, vacancy))
                    .ThenBy(hero => hero.StringId)
                    .FirstOrDefault();

                if (best == null)
                {
                    if (ShouldLogForKingdom(kingdom))
                    {
                        TextObject message = new TextObject(
                            "{=MP_GovernorNoCandidate}[Governor Assignment] No eligible governor is available for {SETTLEMENT} in {KINGDOM}.");
                        message.SetTextVariable("SETTLEMENT", vacancy.Settlement.Name);
                        message.SetTextVariable("KINGDOM", kingdom.Name);
                        ModLogger.Debug(message.ToString());
                    }
                    break;
                }

                ChangeGovernorAction.Apply(vacancy, best);

                if (ShouldLogForKingdom(kingdom))
                {
                    TextObject assignedMessage = new TextObject(
                        "{=MP_GovernorAssigned}[Governor Assignment] Appointed {HERO} of {CLAN} as governor of {SETTLEMENT} for {KINGDOM}.");
                    assignedMessage.SetTextVariable("HERO", best.Name);
                    assignedMessage.SetTextVariable("CLAN", best.Clan.Name);
                    assignedMessage.SetTextVariable("SETTLEMENT", vacancy.Settlement.Name);
                    assignedMessage.SetTextVariable("KINGDOM", kingdom.Name);
                    ModLogger.Notice(assignedMessage.ToString());
                }

                // Greedy assignment removes the selected hero so one hero cannot fill two vacancies.
                candidates.Remove(best);
            }
        }

        private static bool IsEligibleClan(Clan clan)
        {
            // Automatic AI assignment borrows neither player-clan nor mercenary-clan heroes.
            return clan != null
                   && !clan.IsEliminated
                   && clan != Clan.PlayerClan
                   && !clan.IsClanTypeMercenary;
        }

        private static bool IsEligibleHero(Hero hero, Kingdom kingdom)
        {
            // Party leaders, prisoners, existing governors, and traveling heroes are unavailable.
            return hero != null
                   && hero.IsAlive
                   && hero.IsActive
                   && hero.Clan != null
                   && hero.Clan.Kingdom == kingdom
                   && hero.GovernorOf == null
                   && hero.PartyBelongedTo == null
                   && !hero.IsPrisoner
                   && !hero.IsTraveling
                   && Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero);
        }

        private static bool HasIncomingGovernor(Town town)
        {
            // Vanilla may establish GovernorOf before travel finishes; do not fill that post twice.
            return Hero.AllAliveHeroes.Any(hero => hero != null
                                                   && hero.IsTraveling
                                                   && hero.GovernorOf == town);
        }

        private static bool ShouldLogForKingdom(Kingdom kingdom)
        {
            // Governor diagnostics are relevant only to the kingdom containing the player clan.
            return kingdom != null && Clan.PlayerClan?.Kingdom == kingdom;
        }
    }
}
