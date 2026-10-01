using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Decisions;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.AI
{
    /// <summary>
    /// Lets AI clans submit release and independence motions through the
    /// native election system. The behavior only decides whether a clan files
    /// a motion; voting and influence spending remain native responsibilities.
    /// </summary>
    public sealed class SubjectRelationAiBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(
                this,
                OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnWeeklyTick()
        {
            Clan[] clans = Kingdom.All
                .Where(x => x != null && !x.IsEliminated)
                .SelectMany(x => x.Clans)
                .ToArray();
            foreach (Clan clan in clans)
            {
                ConsiderRelationProposal(clan);
            }
        }

        private static void ConsiderRelationProposal(Clan clan)
        {
            if (!SubjectProposalAiBehavior.CanConsiderProposal(clan)
                || HasPendingRelationDecision(clan.Kingdom))
            {
                return;
            }

            float chance = SubjectProposalAiBehavior
                .CalculateNativeProposalChance(clan) * 0.25f;
            if (MBRandom.RandomFloat >= chance)
            {
                return;
            }

            SubjectRelationData ownRelation = KingdomDiplomacyManager.Current
                ?.GetSubjectRelation(clan.Kingdom);
            if (ownRelation != null)
            {
                TryProposeIndependence(clan, ownRelation);
                return;
            }

            SubjectRelationData[] subjects = KingdomDiplomacyManager.Current
                ?.GetSubjects(clan.Kingdom)
                .Where(x => x?.SubjectKingdom != null)
                .ToArray();
            if (subjects == null || subjects.Length == 0)
            {
                return;
            }

            SubjectRelationData relation = subjects[
                MBRandom.RandomInt(subjects.Length)];
            TryProposeRelease(clan, relation);
        }

        private static void TryProposeIndependence(
            Clan clan,
            SubjectRelationData relation)
        {
            SubjectIndependenceKingdomDecision decision =
                new SubjectIndependenceKingdomDecision(
                    clan,
                    relation.OverlordKingdom,
                    relation.Type);
            if (!CanSubmit(clan, decision))
            {
                return;
            }

            clan.Kingdom.AddDecision(decision, false);
            LogFiledMotion(
                "{=MP_AiIndependenceProposal}[Subject diplomacy] {CLAN} proposed that {KINGDOM} declare independence from {TARGET}.",
                clan,
                clan.Kingdom,
                relation.OverlordKingdom);
        }

        private static void TryProposeRelease(
            Clan clan,
            SubjectRelationData relation)
        {
            SubjectReleaseKingdomDecision decision =
                new SubjectReleaseKingdomDecision(
                    clan,
                    relation.SubjectKingdom,
                    relation.Type);
            if (!CanSubmit(clan, decision))
            {
                return;
            }

            clan.Kingdom.AddDecision(decision, false);
            LogFiledMotion(
                "{=MP_AiReleaseProposal}[Subject diplomacy] {CLAN} proposed that {KINGDOM} release {TARGET} from subject status.",
                clan,
                clan.Kingdom,
                relation.SubjectKingdom);
        }

        private static bool CanSubmit(Clan clan, KingdomDecision decision)
        {
            if (!decision.IsAllowed()
                || clan.Influence < decision.GetProposalInfluenceCost())
            {
                return false;
            }

            DecisionOutcome approval = decision
                .DetermineInitialCandidates()
                .FirstOrDefault();
            return approval != null
                && decision.DetermineSupport(clan, approval) > 0f;
        }

        private static bool HasPendingRelationDecision(Kingdom kingdom)
        {
            return kingdom.UnresolvedDecisions.Any(x =>
                x is SubjectReleaseKingdomDecision
                || x is SubjectIndependenceKingdomDecision);
        }

        private static void LogFiledMotion(
            string template,
            Clan clan,
            Kingdom kingdom,
            Kingdom target)
        {
            TextObject message = new TextObject(template);
            message.SetTextVariable("CLAN", clan.Name);
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("TARGET", target.Name);
            ModLogger.Info(message.ToString());
        }
    }
}
