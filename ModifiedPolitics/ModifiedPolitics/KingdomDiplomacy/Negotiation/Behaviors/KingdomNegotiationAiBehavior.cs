using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Models;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Behaviors
{
    /// <summary>
    /// Lets AI rulers build conservative compound diplomatic offers. Native
    /// proposals remain responsible for agreements requiring no compensation;
    /// this behavior adds prisoner releases, ruler-funded payments, or a safe
    /// territorial concession when they make a rejected primary term viable.
    /// </summary>
    public sealed class KingdomNegotiationAiBehavior
        : CampaignBehaviorBase
    {
        private const float WeeklyProposalChance = 0.15f;
        private const float MaximumWealthShare = 0.25f;
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

                TryProposePackage(proposer);
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

        private static void TryProposePackage(Kingdom proposer)
        {
            Kingdom[] targets = Kingdom.All.Where(target =>
                    target != null
                    && target != proposer
                    && !target.IsEliminated)
                .OrderByDescending(target => proposer.IsAtWarWith(target))
                .ThenBy(_ => MBRandom.RandomFloat)
                .ToArray();
            foreach (Kingdom target in targets)
            {
                KingdomNegotiationDraft draft = proposer.IsAtWarWith(target)
                    ? BuildCompensatedDraft(
                        proposer,
                        target,
                        new KingdomNegotiationDraftTerm(
                            KingdomNegotiationTermType.Peace,
                            proposer,
                            proposer,
                            0),
                        true)
                    : BuildPeacetimeDraft(proposer, target);
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

        private static KingdomNegotiationDraft BuildPeacetimeDraft(
            Kingdom proposer,
            Kingdom target)
        {
            KingdomNegotiationTermType[] priorities =
            {
                KingdomNegotiationTermType.TradeAgreement,
                KingdomNegotiationTermType.Alliance
            };
            foreach (KingdomNegotiationTermType type in priorities)
            {
                KingdomNegotiationDraft result = BuildCompensatedDraft(
                    proposer,
                    target,
                    new KingdomNegotiationDraftTerm(
                        type,
                        proposer,
                        proposer,
                        0),
                    false);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        internal static KingdomNegotiationDraft
            BuildCompensatedSubjectDraft(
                Kingdom proposer,
                Kingdom target,
                SubjectType subjectType,
                bool proposerSubmits)
        {
            KingdomNegotiationTermType type;
            if (proposerSubmits)
            {
                type = subjectType == SubjectType.Puppet
                    ? KingdomNegotiationTermType.PlayerBecomesPuppet
                    : KingdomNegotiationTermType.PlayerBecomesVassal;
            }
            else
            {
                type = subjectType == SubjectType.Puppet
                    ? KingdomNegotiationTermType.TargetBecomesPuppet
                    : KingdomNegotiationTermType.TargetBecomesVassal;
            }

            Kingdom provider = proposerSubmits ? proposer : target;
            return BuildCompensatedDraft(
                proposer,
                target,
                new KingdomNegotiationDraftTerm(
                    type,
                    provider,
                    provider,
                    0),
                false);
        }

        private static KingdomNegotiationDraft BuildCompensatedDraft(
            Kingdom proposer,
            Kingdom target,
            KingdomNegotiationDraftTerm primary,
            bool allowTerritory)
        {
            KingdomNegotiationDraft baseDraft =
                new KingdomNegotiationDraft(
                    proposer,
                    target,
                    new[] { primary });
            KingdomNegotiationEvaluation proposerEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    baseDraft,
                    proposer);
            KingdomNegotiationEvaluation targetEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    baseDraft,
                    target);

            // Ordinary mutually acceptable diplomacy belongs to the native
            // decision system. An unwilling proposer never tries to buy an
            // agreement merely because the other side would accept it.
            if (!proposerEvaluation.WouldAccept
                || targetEvaluation.WouldAccept)
            {
                return null;
            }

            List<KingdomNegotiationDraftTerm> terms =
                new List<KingdomNegotiationDraftTerm> { primary };

            // Releasing the negotiating opponent's heroes is the least
            // destructive concession, so it is considered before treasury or
            // territory. Third-party prisoners remain excluded.
            foreach (Hero prisoner in Hero.AllAliveHeroes.Where(hero =>
                hero.IsPrisoner
                && hero.MapFaction == target
                && hero.PartyBelongedToAsPrisoner?.MapFaction == proposer)
                .OrderByDescending(hero => hero.Clan?.Leader == hero)
                .ThenByDescending(hero => hero.Clan?.Tier ?? 0)
                .Take(3))
            {
                terms.Add(new KingdomNegotiationDraftTerm(
                    KingdomNegotiationTermType.PrisonerHero,
                    proposer,
                    prisoner,
                    1));
                KingdomNegotiationDraft prisonerDraft = CreateDraft(
                    proposer,
                    target,
                    terms);
                if (BothWouldAccept(prisonerDraft))
                {
                    return prisonerDraft;
                }
            }

            Hero payer = proposer.Leader;
            int availableGold = Math.Max(0, payer?.Gold ?? 0);
            int requiredReserve = Math.Max(
                100000,
                proposer.Fiefs.Count * 50000);
            int maximumPayment = Math.Min(
                (int)(availableGold * MaximumWealthShare),
                Math.Max(0, availableGold - requiredReserve));
            if (payer != null && maximumPayment >= 1000)
            {
                int step = Math.Max(
                    1000,
                    maximumPayment / PaymentSteps);
                for (int amount = step;
                    amount <= maximumPayment;
                    amount += step)
                {
                    List<KingdomNegotiationDraftTerm> paidTerms = terms
                        .Where(term => term.Type
                            != KingdomNegotiationTermType.Gold)
                        .ToList();
                    paidTerms.Add(new KingdomNegotiationDraftTerm(
                        KingdomNegotiationTermType.Gold,
                        proposer,
                        payer,
                        amount));
                    KingdomNegotiationDraft candidate = CreateDraft(
                        proposer,
                        target,
                        paidTerms);
                    if (BothWouldAccept(candidate))
                    {
                        return candidate;
                    }
                }

                terms.Add(new KingdomNegotiationDraftTerm(
                    KingdomNegotiationTermType.Gold,
                    proposer,
                    payer,
                    maximumPayment));
            }

            if (allowTerritory)
            {
                KingdomNegotiationDraft territorial =
                    TryAddSafeTerritorialConcession(
                        proposer,
                        target,
                        terms);
                if (territorial != null)
                {
                    return territorial;
                }
            }

            return null;
        }

        private static KingdomNegotiationDraft
            TryAddSafeTerritorialConcession(
                Kingdom proposer,
                Kingdom target,
                List<KingdomNegotiationDraftTerm> existingTerms)
        {
            // Never negotiate away a realm close to extinction. The offered
            // fief must also not be its owner's last personal holding.
            if (proposer.Fiefs.Count <= 4)
            {
                return null;
            }

            foreach (Town town in proposer.Fiefs
                .Where(fief => fief?.Settlement?.IsFortification == true
                    && fief.Settlement.SiegeEvent == null
                    && fief.OwnerClan != null
                    && fief.OwnerClan.Fiefs.Count > 1)
                .OrderBy(fief => fief.IsTown)
                .ThenBy(fief => fief.Prosperity))
            {
                List<KingdomNegotiationDraftTerm> terms =
                    existingTerms.ToList();
                terms.Add(new KingdomNegotiationDraftTerm(
                    KingdomNegotiationTermType.Settlement,
                    proposer,
                    town.Settlement,
                    1));
                KingdomNegotiationDraft candidate = CreateDraft(
                    proposer,
                    target,
                    terms);
                if (BothWouldAccept(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static KingdomNegotiationDraft CreateDraft(
            Kingdom proposer,
            Kingdom target,
            IEnumerable<KingdomNegotiationDraftTerm> terms)
        {
            return new KingdomNegotiationDraft(proposer, target, terms);
        }

        private static bool BothWouldAccept(
            KingdomNegotiationDraft draft)
        {
            return KingdomNegotiationDraftValidator.TryValidate(
                    draft,
                    out _)
                && KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    draft.PlayerKingdom).WouldAccept
                && KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    draft.TargetKingdom).WouldAccept;
        }

        private static void LogProposal(
            Kingdom proposer,
            Kingdom target,
            KingdomNegotiationDraft draft)
        {
            int payment = draft.Terms.FirstOrDefault(term =>
                term.Type == KingdomNegotiationTermType.Gold)?.Amount ?? 0;
            TextObject message = new TextObject(
                "{=MP_NegotiationAiPeaceProposal}[Kingdom negotiation] {PROPOSER} sent {TARGET} a compound proposal with {COUNT} terms and {GOLD} denars in compensation.");
            message.SetTextVariable("PROPOSER", proposer.Name);
            message.SetTextVariable("TARGET", target.Name);
            message.SetTextVariable("COUNT", draft.Terms.Count);
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
