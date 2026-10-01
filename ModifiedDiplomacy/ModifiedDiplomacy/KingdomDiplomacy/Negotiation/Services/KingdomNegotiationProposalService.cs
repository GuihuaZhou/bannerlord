using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Decisions;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Services
{
    /// <summary>
    /// Coordinates foreign acceptance checking, the proposing kingdom's
    /// decision, and execution of the approved proposal.
    /// </summary>
    public static class KingdomNegotiationProposalService
    {
        // The native unresolved-decision list can briefly change while the
        // kingdom screen moves a decision into or out of its voting popup.
        // Keep a proposal-level lock until the proposal is rejected or
        // executed so a duplicate cannot be submitted in that gap.
        private static readonly HashSet<string> ActiveProposalKeys =
            new HashSet<string>();
        public static KingdomNegotiationDraft GetCounterOffer(
            Kingdom playerKingdom,
            Kingdom targetKingdom)
        {
            return KingdomDiplomacyManager.Current?.GetCounterOffer(
                playerKingdom,
                targetKingdom);
        }

        public static void RemoveCounterOffer(
            Kingdom playerKingdom,
            Kingdom targetKingdom)
        {
            KingdomDiplomacyManager.Current?.RemoveCounterOffer(
                playerKingdom,
                targetKingdom);
        }

        public static bool SubmitPlayerProposal(
            KingdomNegotiationDraft draft,
            Action beforeDecisionAdded,
            out TextObject reason)
        {
            if (!KingdomNegotiationDraftValidator.TryValidate(
                    draft,
                    out reason))
            {
                return false;
            }

            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            if (playerKingdom == null
                || playerKingdom != draft.PlayerKingdom
                || Clan.PlayerClan.IsUnderMercenaryService)
            {
                reason = new TextObject(
                    "{=MP_NegotiationPlayerCannotPropose}Only a full member clan may submit a kingdom proposal.");
                return false;
            }

            string proposalKey = MakePairKey(
                playerKingdom,
                draft.TargetKingdom);
            if (ActiveProposalKeys.Contains(proposalKey)
                || HasPendingProposalBetween(
                    playerKingdom,
                    draft.TargetKingdom))
            {
                reason = new TextObject(
                    "{=MP_NegotiationAlreadyPending}A compound proposal with this kingdom is already pending.");
                return false;
            }

            // Native bilateral proposals simulate the queried kingdom's
            // election before enabling the proposing kingdom's decision. A
            // compound proposal follows the same rule: the foreign council as
            // a whole must be expected to accept, not merely one foreign clan.
            KingdomNegotiationEvaluation foreignEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    draft.TargetKingdom);
            if (!foreignEvaluation.WouldAccept)
            {
                reason = BuildForeignRejectionReason(foreignEvaluation);
                return false;
            }

            KingdomNegotiationDecision decision =
                new KingdomNegotiationDecision(
                    Clan.PlayerClan,
                    draft.TargetKingdom,
                    draft.Terms.Select(
                        KingdomNegotiationTermRecord.FromDraftTerm),
                    false);
            ActiveProposalKeys.Add(proposalKey);
            try
            {
                // The kingdom decision screen may open synchronously from
                // AddDecision. Close the modal negotiation layer first so it
                // cannot survive underneath the vote and later expose a
                // stale copy of the successfully executed proposal.
                beforeDecisionAdded?.Invoke();
                playerKingdom.AddDecision(decision, true);
            }
            catch
            {
                ActiveProposalKeys.Remove(proposalKey);
                throw;
            }
            LogFlow(
                playerKingdom,
                draft.TargetKingdom,
                "{=MP_NegotiationDebugProposalCreated}The proposing council decision was created",
                draft.Terms.Count);
            AddPlayerNotice(
                playerKingdom,
                decision,
                "{=MP_NegotiationOwnCouncilNotice}Your diplomatic proposal is ready for council review.");
            reason = TextObject.GetEmpty();
            return true;
        }

        /// <summary>
        /// Submits a compound proposal initiated by an AI clan. AI-to-AI
        /// diplomacy follows the same foreign pre-evaluation used by player
        /// proposals. When the receiving kingdom belongs to the player, the
        /// foreign pre-evaluation is replaced by a real player-council vote so
        /// the AI can never accept a bilateral agreement on the player's
        /// behalf.
        /// </summary>
        public static bool SubmitAiProposal(
            Clan proposerClan,
            KingdomNegotiationDraft draft,
            out TextObject reason)
        {
            if (!KingdomNegotiationDraftValidator.TryValidate(
                    draft,
                    out reason)
                || proposerClan?.Kingdom != draft.PlayerKingdom
                || proposerClan.IsEliminated
                || proposerClan.IsUnderMercenaryService)
            {
                if (reason == null || reason.IsEmpty())
                {
                    reason = new TextObject(
                        "{=MP_NegotiationAiProposerInvalid}The proposing clan can no longer submit this diplomatic proposal.");
                }

                return false;
            }

            Kingdom proposingKingdom = draft.PlayerKingdom;
            Kingdom receivingKingdom = draft.TargetKingdom;
            string proposalKey = MakePairKey(
                proposingKingdom,
                receivingKingdom);
            if (ActiveProposalKeys.Contains(proposalKey)
                || HasPendingProposalBetween(
                    proposingKingdom,
                    receivingKingdom))
            {
                reason = new TextObject(
                    "{=MP_NegotiationAlreadyPending}A compound proposal with this kingdom is already pending.");
                return false;
            }

            KingdomNegotiationEvaluation proposingEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    proposingKingdom);
            KingdomNegotiationClanEvaluation sponsorEvaluation =
                proposingEvaluation.ClanEvaluations.FirstOrDefault(x =>
                    x.Clan == proposerClan);
            int proposalCost = 150 + Math.Min(
                150,
                (draft.Terms.Count - 1) * 25);
            if ((sponsorEvaluation?.RawScore ?? 0f) <= 0f
                || proposerClan.Influence < proposalCost)
            {
                reason = new TextObject(
                    "{=MP_NegotiationAiCouncilUnwilling}The proposing kingdom does not support this diplomatic package.");
                return false;
            }

            bool playerReceives = receivingKingdom
                == Clan.PlayerClan?.Kingdom;
            if (!playerReceives)
            {
                KingdomNegotiationEvaluation foreignEvaluation =
                    KingdomNegotiationEvaluationService.Evaluate(
                        draft,
                        receivingKingdom);
                if (!foreignEvaluation.WouldAccept)
                {
                    reason = BuildForeignRejectionReason(
                        foreignEvaluation);
                    return false;
                }
            }

            Clan decisionSponsor = playerReceives
                ? receivingKingdom.RulingClan
                : proposerClan;
            KingdomNegotiationDecision decision =
                new KingdomNegotiationDecision(
                    decisionSponsor,
                    playerReceives
                        ? proposingKingdom
                        : receivingKingdom,
                    draft.Terms.Select(
                        KingdomNegotiationTermRecord.FromDraftTerm),
                    playerReceives);
            ActiveProposalKeys.Add(proposalKey);
            try
            {
                (playerReceives
                    ? receivingKingdom
                    : proposingKingdom).AddDecision(
                        decision,
                        playerReceives);
            }
            catch
            {
                ActiveProposalKeys.Remove(proposalKey);
                throw;
            }

            if (playerReceives)
            {
                AddPlayerNotice(
                    receivingKingdom,
                    decision,
                    "{=MP_NegotiationForeignCouncilNotice}{KINGDOM} has sent your realm a compound diplomatic proposal.",
                    proposingKingdom);
            }

            LogFlow(
                proposingKingdom,
                receivingKingdom,
                playerReceives
                    ? "{=MP_NegotiationDebugReceivingProposalCreated}The receiving player council decision was created"
                    : "{=MP_NegotiationDebugProposalCreated}The proposing council decision was created",
                draft.Terms.Count);
            reason = TextObject.GetEmpty();
            return true;
        }

        public static void ResolveCouncilVote(
            KingdomNegotiationDecision decision,
            bool approved)
        {
            if (decision == null)
            {
                return;
            }

            LogFlow(
                decision.Kingdom,
                decision.OtherKingdom,
                decision.IsReceivingCouncil
                    ? "{=MP_NegotiationDebugReceivingVoteResolved}The receiving council vote was resolved"
                    : "{=MP_NegotiationDebugProposingVoteResolved}The proposing council vote was resolved",
                decision.Terms.Count,
                approved
                    ? "{=MP_NegotiationDebugApproved}approved"
                    : "{=MP_NegotiationDebugRejected}rejected");
            LogCouncilResult(decision, approved);
            if (!approved)
            {
                QueueCounterOffer(decision);
                ReleaseProposal(decision);
                return;
            }

            LogFlow(
                decision.Kingdom,
                decision.OtherKingdom,
                "{=MP_NegotiationDebugExecutionEntered}The final execution stage was entered",
                decision.Terms.Count);
            if (KingdomDiplomacyManager.Current?
                    .BeginNegotiationExecution(decision.CreateDraft())
                == true)
            {
                ReleaseProposal(decision);
                return;
            }

            TextObject incomplete = new TextObject(
                "{=MP_NegotiationExecutionUnavailable}[Kingdom negotiation] The approved proposal could not be executed because one or more terms are not supported yet.");
            ModLogger.Error(incomplete.ToString());
            ReleaseProposal(decision);
        }

        private static bool HasPendingProposalBetween(
            Kingdom firstKingdom,
            Kingdom secondKingdom,
            KingdomNegotiationDecision ignoredDecision = null)
        {
            return HasPendingProposal(
                    firstKingdom,
                    secondKingdom,
                    ignoredDecision)
                || HasPendingProposal(
                    secondKingdom,
                    firstKingdom,
                    ignoredDecision);
        }

        private static bool HasPendingProposal(
            Kingdom kingdom,
            Kingdom otherKingdom,
            KingdomNegotiationDecision ignoredDecision)
        {
            return kingdom?.UnresolvedDecisions
                    .OfType<KingdomNegotiationDecision>()
                    .Any(x => x != ignoredDecision
                        && x.OtherKingdom == otherKingdom)
                == true;
        }

        private static void ReleaseProposal(
            KingdomNegotiationDecision decision)
        {
            if (decision?.Kingdom == null || decision.OtherKingdom == null)
            {
                return;
            }

            ActiveProposalKeys.Remove(MakePairKey(
                decision.Kingdom,
                decision.OtherKingdom));
        }

        private static void QueueCounterOffer(
            KingdomNegotiationDecision decision)
        {
            if (decision?.IsReceivingCouncil != true
                || decision.Kingdom != Clan.PlayerClan?.Kingdom)
            {
                return;
            }

            KingdomNegotiationDraft draft = decision.CreateDraft();
            KingdomDiplomacyManager.Current?.StoreCounterOffer(
                decision.Kingdom,
                decision.OtherKingdom,
                draft.Terms);
            TextObject message = new TextObject(
                "{=MP_NegotiationCounterOfferReady}[Kingdom negotiation] The rejected terms were saved. Open negotiations with {KINGDOM} to prepare a counteroffer.");
            message.SetTextVariable(
                "KINGDOM",
                draft.PlayerKingdom?.Name ?? TextObject.GetEmpty());
            InformationManager.DisplayMessage(
                new InformationMessage(message.ToString()));
        }

        private static string MakePairKey(
            Kingdom firstKingdom,
            Kingdom secondKingdom)
        {
            string first = firstKingdom?.StringId ?? string.Empty;
            string second = secondKingdom?.StringId ?? string.Empty;
            return string.CompareOrdinal(first, second) <= 0
                ? first + ":" + second
                : second + ":" + first;
        }

        private static void AddPlayerNotice(
            Kingdom kingdom,
            KingdomNegotiationDecision decision,
            string source,
            Kingdom namedKingdom = null)
        {
            TextObject notice = new TextObject(source);
            notice.SetTextVariable(
                "KINGDOM",
                namedKingdom?.Name ?? TextObject.GetEmpty());
            Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(
                new KingdomDecisionMapNotification(
                    kingdom,
                    decision,
                    notice));
        }

        private static TextObject BuildForeignRejectionReason(
            KingdomNegotiationEvaluation evaluation)
        {
            KingdomNegotiationClanEvaluation mostOpposed = evaluation
                ?.ClanEvaluations
                .OrderBy(result => result.RawScore)
                .FirstOrDefault();
            string factors = mostOpposed == null
                ? string.Empty
                : string.Join(", ", mostOpposed.Components
                    .Where(component => component.Score < 0f)
                    .OrderBy(component => component.Score)
                    .Take(3)
                    .Select(component => string.Format(
                        "{0} {1:F1}",
                        component.Name,
                        component.Score)));
            TextObject reason = new TextObject(
                "{=MP_NegotiationForeignRejectedDetailed}The other kingdom's council rejected the complete package. Strongest opposition: {CLAN}. Main negative terms: {FACTORS}.");
            reason.SetTextVariable(
                "CLAN",
                mostOpposed?.Clan?.Name ?? TextObject.GetEmpty());
            reason.SetTextVariable(
                "FACTORS",
                string.IsNullOrEmpty(factors)
                    ? new TextObject(
                        "{=MP_NegotiationNoNegativeBreakdown}overall diplomatic support is insufficient")
                    : new TextObject("{=!}" + factors));
            return reason;
        }

        private static void LogCouncilResult(
            KingdomNegotiationDecision decision,
            bool approved)
        {
            TextObject message = new TextObject(
                approved
                    ? "{=MP_NegotiationCouncilApprovedLog}[Kingdom negotiation] {KINGDOM}'s council approved the compound proposal."
                    : "{=MP_NegotiationCouncilRejectedLog}[Kingdom negotiation] {KINGDOM}'s council rejected the compound proposal.");
            message.SetTextVariable("KINGDOM", decision.Kingdom.Name);
            ModLogger.Notice(message.ToString());
        }

        private static void LogFlow(
            Kingdom first,
            Kingdom second,
            string stageSource,
            int termCount,
            string resultSource = null)
        {
            TextObject message = new TextObject(
                "{=MP_NegotiationDebugFlow}[Kingdom negotiation debug] {FIRST} and {SECOND}. {STAGE}. Terms: {COUNT}. Result: {RESULT}.");
            message.SetTextVariable(
                "FIRST",
                first?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable(
                "SECOND",
                second?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("STAGE", new TextObject(stageSource));
            message.SetTextVariable("COUNT", termCount);
            message.SetTextVariable(
                "RESULT",
                string.IsNullOrEmpty(resultSource)
                    ? new TextObject(
                        "{=MP_NegotiationDebugNotApplicable}not applicable")
                    : new TextObject(resultSource));
            ModLogger.Info(message.ToString());
        }
    }
}
