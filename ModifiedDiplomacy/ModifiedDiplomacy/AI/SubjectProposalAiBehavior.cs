using System;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Decisions;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Services;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.AI
{
    /// <summary>
    /// Adds subject proposals to the native per-clan decision proposal cycle.
    /// Once added, Bannerlord owns voting, influence spending, ruler choice,
    /// and final resolution through its normal kingdom election system.
    /// </summary>
    public sealed class SubjectProposalAiBehavior : CampaignBehaviorBase
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
            // Work on a snapshot because a successful proposal changes a
            // kingdom's unresolved-decision collection during this pass.
            Clan[] clans = Kingdom.All
                .Where(kingdom => kingdom != null && !kingdom.IsEliminated)
                .SelectMany(kingdom => kingdom.Clans)
                .ToArray();
            foreach (Clan clan in clans)
            {
                ConsiderProposal(clan);
            }
        }

        private static void ConsiderProposal(Clan clan)
        {
            if (!CanConsiderProposal(clan)
                || clan.Kingdom.UnresolvedDecisions.Any(x =>
                    x is SubjectProposalKingdomDecision))
            {
                return;
            }

            // Use the same clan-count-based chance shape as native diplomacy,
            // but evaluate it only once per week to keep subject proposals
            // rarer than ordinary war, peace, trade, and alliance motions.
            float proposalChance = CalculateNativeProposalChance(clan) * 0.25f;
            if (MBRandom.RandomFloat >= proposalChance)
            {
                return;
            }

            Kingdom target = GetRandomEligibleTarget(clan.Kingdom);
            if (target == null)
            {
                return;
            }

            bool isSubmissionOffer = MBRandom.RandomFloat < 0.5f;
            SubjectType type = MBRandom.RandomFloat < 0.5f
                ? SubjectType.Vassal
                : SubjectType.Puppet;
            SubjectProposalKingdomDecision decision = TryCreateProposal(
                clan,
                target,
                type,
                isSubmissionOffer);
            if (decision == null)
            {
                return;
            }

            clan.Kingdom.AddDecision(decision, false);
            LogProposal(decision);
        }

        internal static bool CanConsiderProposal(Clan clan)
        {
            if ((int)Campaign.Current.Models.CampaignTimeModel
                    .CampaignStartTime.ElapsedDaysUntilNow < 5)
            {
                return false;
            }

            return clan != null
                && !clan.IsEliminated
                && clan != Clan.PlayerClan
                && clan.CurrentTotalStrength > 0f
                && !clan.IsBanditFaction
                && clan.Kingdom != null
                && !clan.IsUnderMercenaryService
                && clan.Influence >= 100f;
        }

        internal static float CalculateNativeProposalChance(Clan clan)
        {
            int influentialClanCount = clan.Kingdom.Clans.Count(x =>
                x.Influence > 100f);
            float chance = MathF.Min(
                0.33f,
                1f / (influentialClanCount + 2f));

            // Preserve the player-kingdom reduction used by the native
            // proposal behavior. Other AI clans may still propose normally.
            if (clan.Kingdom == Hero.MainHero.MapFaction
                && !Hero.MainHero.Clan.IsUnderMercenaryService)
            {
                chance *= clan.Kingdom.Leader == Hero.MainHero
                    ? 0.5f
                    : 0.75f;
            }

            return chance;
        }

        private static Kingdom GetRandomEligibleTarget(Kingdom proposer)
        {
            Kingdom[] candidates = Kingdom.All.Where(target =>
                target != null
                && target != proposer
                && !target.IsEliminated
                && !HasPendingSubjectProposal(target))
                .ToArray();
            return candidates.Length == 0
                ? null
                : candidates[MBRandom.RandomInt(candidates.Length)];
        }

        private static SubjectProposalKingdomDecision TryCreateProposal(
            Clan proposerClan,
            Kingdom target,
            SubjectType type,
            bool isSubmissionOffer)
        {
            Kingdom proposerKingdom = proposerClan.Kingdom;
            Kingdom overlord = isSubmissionOffer
                ? target
                : proposerKingdom;
            Kingdom subject = isSubmissionOffer
                ? proposerKingdom
                : target;
            if (KingdomDiplomacyManager.Current
                    ?.CanEstablishSubjectRelation(
                        overlord,
                        subject,
                        type) != true)
            {
                return null;
            }

            // Bilateral diplomacy has no vote when the foreign side refuses
            // to open negotiations.
            SubjectProposalEvaluation foreignEvaluation = isSubmissionOffer
                ? SubjectProposalEvaluationService
                    .EvaluateSubmissionOffer(overlord, subject, type)
                : SubjectProposalEvaluationService
                    .EvaluateDemand(overlord, subject, type);
            if (!foreignEvaluation.WouldAccept)
            {
                KingdomNegotiationDraft compensated =
                    KingdomNegotiationAiBehavior
                        .BuildCompensatedSubjectDraft(
                            proposerKingdom,
                            target,
                            type,
                            isSubmissionOffer);
                if (compensated != null)
                {
                    KingdomNegotiationProposalService.SubmitAiProposal(
                        proposerClan,
                        compensated,
                        out _);
                }

                return null;
            }

            SubjectProposalKingdomDecision decision =
                new SubjectProposalKingdomDecision(
                    proposerClan,
                    target,
                    type,
                    isSubmissionOffer);
            if (proposerClan.Influence
                < decision.GetProposalInfluenceCost())
            {
                return null;
            }

            // Like native proposals, the initiating clan must personally
            // favor its motion. Kingdom-wide support is not predicted here;
            // the native election is responsible for the actual result.
            DecisionOutcome approval = decision
                .DetermineInitialCandidates()
                .FirstOrDefault(x =>
                    (x as SubjectProposalKingdomDecision
                        .SubjectProposalDecisionOutcome)
                        ?.ShouldAllianceBeStarted == true);
            return approval != null
                && decision.DetermineSupport(proposerClan, approval) > 0f
                    ? decision
                    : null;
        }

        private static bool HasPendingSubjectProposal(Kingdom kingdom)
        {
            return Kingdom.All.Any(owner => owner != null
                && owner.UnresolvedDecisions
                    .OfType<SubjectProposalKingdomDecision>()
                    .Any(decision => decision.Kingdom == kingdom
                        || decision.TargetKingdom == kingdom));
        }

        private static void LogProposal(
            SubjectProposalKingdomDecision decision)
        {
            TextObject message = new TextObject(
                decision.IsSubmissionOffer
                    ? "{=MP_AiSubjectOffer}[Subject diplomacy] {SUBJECT} proposed becoming a {SUBJECT_TYPE} of {OVERLORD}. The proposal has entered the kingdom vote."
                    : "{=MP_AiSubjectDemand}[Subject diplomacy] {OVERLORD} proposed making {SUBJECT} a {SUBJECT_TYPE}. The proposal has entered the kingdom vote.");
            message.SetTextVariable("OVERLORD", decision.Overlord.Name);
            message.SetTextVariable("SUBJECT", decision.Subject.Name);
            message.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(
                    decision.SubjectType));
            ModLogger.Info(message.ToString());
        }
    }
}
