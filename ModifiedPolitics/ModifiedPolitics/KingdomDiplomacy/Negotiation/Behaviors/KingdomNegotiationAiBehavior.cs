using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Models;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Behaviors
{
    /// <summary>
    /// Lets AI rulers build conservative compound peace offers. Native peace
    /// proposals remain responsible for agreements that need no compensation;
    /// this behavior only adds a ruler-funded payment when it changes a
    /// rejected peace proposal into a package both kingdoms can support.
    /// </summary>
    public sealed class KingdomNegotiationAiBehavior
        : CampaignBehaviorBase
    {
        private const float WeeklyProposalChance = 0.15f;
        private const float MaximumWealthShare = 0.5f;
        private const int PaymentSteps = 10;

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
            if ((int)Campaign.Current.Models.CampaignTimeModel
                    .CampaignStartTime.ElapsedDaysUntilNow < 5)
            {
                return;
            }

            foreach (Kingdom proposer in Kingdom.All
                .Where(IsEligibleKingdom)
                .ToList())
            {
                if (MBRandom.RandomFloat > WeeklyProposalChance)
                {
                    continue;
                }

                TryProposeCompensatedPeace(proposer);
            }
        }

        private static bool IsEligibleKingdom(Kingdom kingdom)
        {
            return kingdom != null
                && !kingdom.IsEliminated
                && kingdom.RulingClan != null
                && !kingdom.RulingClan.IsEliminated
                && kingdom.RulingClan.Influence >= 200f;
        }

        private static void TryProposeCompensatedPeace(Kingdom proposer)
        {
            Kingdom[] enemies = Kingdom.All.Where(target =>
                    target != null
                    && target != proposer
                    && !target.IsEliminated
                    && proposer.IsAtWarWith(target))
                .OrderBy(_ => MBRandom.RandomFloat)
                .ToArray();
            foreach (Kingdom target in enemies)
            {
                KingdomNegotiationDraft draft = BuildCompensatedPeaceDraft(
                    proposer,
                    target);
                if (draft == null)
                {
                    continue;
                }

                if (KingdomNegotiationProposalService.SubmitAiProposal(
                    proposer.RulingClan,
                    draft,
                    out TextObject reason))
                {
                    LogProposal(proposer, target, draft);
                    return;
                }

                LogSkipped(proposer, target, reason);
            }
        }

        private static KingdomNegotiationDraft BuildCompensatedPeaceDraft(
            Kingdom proposer,
            Kingdom target)
        {
            KingdomNegotiationDraftTerm peace =
                new KingdomNegotiationDraftTerm(
                    KingdomNegotiationTermType.Peace,
                    proposer,
                    proposer,
                    0);
            KingdomNegotiationDraft baseDraft =
                new KingdomNegotiationDraft(
                    proposer,
                    target,
                    new[] { peace });
            KingdomNegotiationEvaluation proposerEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    baseDraft,
                    proposer);
            KingdomNegotiationEvaluation targetEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    baseDraft,
                    target);

            // Ordinary mutually acceptable peace belongs to the native peace
            // decision system. A ruler unwilling to make peace will not try
            // to buy it merely because the other side is willing.
            if (!proposerEvaluation.WouldAccept
                || targetEvaluation.WouldAccept)
            {
                return null;
            }

            Hero payer = proposer.Leader;
            int availableGold = Math.Max(0, payer?.Gold ?? 0);
            int maximumPayment = (int)(availableGold
                * MaximumWealthShare);
            if (payer == null || maximumPayment < 1000)
            {
                return null;
            }

            int step = Math.Max(1000, maximumPayment / PaymentSteps);
            for (int amount = step;
                amount <= maximumPayment;
                amount += step)
            {
                KingdomNegotiationDraftTerm payment =
                    new KingdomNegotiationDraftTerm(
                        KingdomNegotiationTermType.Gold,
                        proposer,
                        payer,
                        amount);
                KingdomNegotiationDraft candidate =
                    new KingdomNegotiationDraft(
                        proposer,
                        target,
                        new[] { peace, payment });
                if (KingdomNegotiationEvaluationService.Evaluate(
                        candidate,
                        proposer).WouldAccept
                    && KingdomNegotiationEvaluationService.Evaluate(
                        candidate,
                        target).WouldAccept)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void LogProposal(
            Kingdom proposer,
            Kingdom target,
            KingdomNegotiationDraft draft)
        {
            int payment = draft.Terms.FirstOrDefault(term =>
                term.Type == KingdomNegotiationTermType.Gold)?.Amount ?? 0;
            TextObject message = new TextObject(
                "{=MP_NegotiationAiPeaceProposal}[Kingdom negotiation] {PROPOSER} proposed peace with {TARGET} and offered {GOLD} denars as compensation.");
            message.SetTextVariable("PROPOSER", proposer.Name);
            message.SetTextVariable("TARGET", target.Name);
            message.SetTextVariable("GOLD", payment);
            ModLogger.Info(message.ToString());
        }

        private static void LogSkipped(
            Kingdom proposer,
            Kingdom target,
            TextObject reason)
        {
            TextObject message = new TextObject(
                "{=MP_NegotiationAiProposalSkipped}[Kingdom negotiation] {PROPOSER}'s proposal to {TARGET} was not opened. {REASON}");
            message.SetTextVariable("PROPOSER", proposer.Name);
            message.SetTextVariable("TARGET", target.Name);
            message.SetTextVariable(
                "REASON",
                reason ?? TextObject.GetEmpty());
            ModLogger.Info(message.ToString());
        }
    }
}
