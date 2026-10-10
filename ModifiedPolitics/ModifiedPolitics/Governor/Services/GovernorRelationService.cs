using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.Governor.Models;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Governor.Services
{
    /// <summary>
    /// Settles deliberate governor changes once per affected clan.
    /// </summary>
    public sealed class GovernorRelationService
    {
        public void Apply(GovernorAssignmentPlan plan, GovernorAssignmentResult result)
        {
            if (result.FailedTowns.Count > 0 || plan.Kingdom?.RulingClan?.Leader == null)
                return;

            var clans = result.Before.Values.Concat(result.After.Values)
                .Where(hero => hero?.Clan != null && hero.Clan != plan.Kingdom.RulingClan)
                .Select(hero => hero.Clan)
                .Distinct()
                .ToList();

            foreach (Clan clan in clans)
            {
                var oldPosts = new List<GovernorSettlementContext>();
                var newPosts = new List<GovernorSettlementContext>();
                foreach (Town town in plan.Targets.Keys)
                {
                    Clan oldClan = result.Before[town]?.Clan;
                    Clan newClan = result.After[town]?.Clan;
                    if (oldClan == clan && newClan == clan)
                        continue;
                    if (oldClan == clan)
                        oldPosts.Add(plan.Contexts[town]);
                    if (newClan == clan)
                        newPosts.Add(plan.Contexts[town]);
                }

                int delta = CalculateDelta(oldPosts, newPosts);
                if (delta == 0 || clan.Leader == null)
                    continue;

                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    plan.Kingdom.RulingClan.Leader,
                    clan.Leader,
                    delta,
                    true);
                LogRelation(plan.Kingdom, clan, delta);
            }
        }

        private static int CalculateDelta(
            IList<GovernorSettlementContext> oldPosts,
            IList<GovernorSettlementContext> newPosts)
        {
            List<GovernorSettlementContext> orderedOld = oldPosts
                .OrderByDescending(GetAppointmentRelation).ToList();
            List<GovernorSettlementContext> orderedNew = newPosts
                .OrderByDescending(GetAppointmentRelation).ToList();
            int paired = System.Math.Min(orderedOld.Count, orderedNew.Count);
            int delta = 0;
            for (int i = 0; i < paired; i++)
                delta += GetAppointmentRelation(orderedNew[i]) - GetAppointmentRelation(orderedOld[i]);
            for (int i = paired; i < orderedNew.Count; i++)
                delta += GetAppointmentRelation(orderedNew[i]);
            for (int i = paired; i < orderedOld.Count; i++)
                delta += GetDismissalRelation(orderedOld[i]);
            return delta;
        }

        private static int GetAppointmentRelation(GovernorSettlementContext context)
        {
            return (context.IsTown ? 12 : 5)
                   + System.Math.Min(System.Math.Max(context.ProsperityBand, 0), 5)
                   + (context.IsBorder ? 2 : 0);
        }

        private static int GetDismissalRelation(GovernorSettlementContext context)
        {
            return -((context.IsTown ? 18 : 8)
                     + System.Math.Min(System.Math.Max(context.ProsperityBand, 0), 5)
                     + (context.IsBorder ? 2 : 0));
        }

        private static void LogRelation(Kingdom kingdom, Clan clan, int delta)
        {
            if (Clan.PlayerClan?.Kingdom != kingdom)
                return;

            TextObject message = new TextObject(
                "{=MP_GovernorRelationChanged}[Governor Assignment] Relation between {RULING_CLAN} and {CLAN} changed by {DELTA} after governor assignments.");
            message.SetTextVariable("RULING_CLAN", kingdom.RulingClan.Name);
            message.SetTextVariable("CLAN", clan.Name);
            message.SetTextVariable("DELTA", delta);
            ModLogger.Info(message.ToString());
        }
    }
}
