using System;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Services
{
    /// <summary>
    /// Calculates the value of an entire negotiation draft for every eligible
    /// clan. Native treaty decisions provide treaty scores, while tangible
    /// concessions and existing subject-diplomacy models provide the remaining
    /// components on a compatible, inspectable scale.
    /// </summary>
    public static class KingdomNegotiationEvaluationService
    {
        public static KingdomNegotiationEvaluation Evaluate(
            KingdomNegotiationDraft draft,
            Kingdom evaluatingKingdom)
        {
            KingdomNegotiationEvaluation result =
                new KingdomNegotiationEvaluation
                {
                    Draft = draft,
                    EvaluatingKingdom = evaluatingKingdom
                };

            if (draft == null || evaluatingKingdom == null)
            {
                return result;
            }

            foreach (Clan clan in evaluatingKingdom.Clans.Where(x =>
                x != null
                && !x.IsEliminated
                && !x.IsUnderMercenaryService))
            {
                KingdomNegotiationClanEvaluation clanResult =
                    new KingdomNegotiationClanEvaluation { Clan = clan };
                foreach (KingdomNegotiationDraftTerm term in draft.Terms)
                {
                    clanResult.Components.Add(EvaluateTerm(
                        draft,
                        evaluatingKingdom,
                        clan,
                        term));
                }

                result.ClanEvaluations.Add(clanResult);
            }

            return result;
        }

        public static void Log(KingdomNegotiationEvaluation evaluation)
        {
            if (evaluation?.EvaluatingKingdom == null)
            {
                return;
            }

            TextObject summary = new TextObject(
                "{=MP_NegotiationEvaluationSummary}[Kingdom negotiation] {KINGDOM} predicts {RESULT} for the compound proposal. Weighted support is {SUPPORT}%.");
            summary.SetTextVariable("KINGDOM", evaluation.EvaluatingKingdom.Name);
            summary.SetTextVariable(
                "RESULT",
                evaluation.WouldAccept
                    ? new TextObject("{=MP_NegotiationAccept}acceptance")
                    : new TextObject("{=MP_NegotiationReject}rejection"));
            summary.SetTextVariable(
                "SUPPORT",
                (evaluation.AcceptShare * 100f).ToString("F0"));
            ModLogger.Info(summary.ToString());

            foreach (KingdomNegotiationClanEvaluation clanResult
                in evaluation.ClanEvaluations)
            {
                string details = string.Join(", ", clanResult.Components
                    .Select(x => x.Name + " " + Format(x.Score)));
                TextObject line = new TextObject(
                    "{=MP_NegotiationClanEvaluation}[Kingdom negotiation] {CLAN} scores the proposal at {SCORE}. Components: {COMPONENTS}.");
                line.SetTextVariable("CLAN", clanResult.Clan.Name);
                line.SetTextVariable("SCORE", Format(clanResult.RawScore));
                line.SetTextVariable("COMPONENTS", details);
                ModLogger.Info(line.ToString());
            }
        }

        private static KingdomNegotiationScoreComponent EvaluateTerm(
            KingdomNegotiationDraft draft,
            Kingdom evaluatingKingdom,
            Clan clan,
            KingdomNegotiationDraftTerm term)
        {
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Gold:
                    return Component("{=MP_NegotiationScoreGold}gold",
                        EvaluateGold(evaluatingKingdom, clan, term));
                case KingdomNegotiationTermType.Settlement:
                    return EvaluateSettlementComponent(
                        draft,
                        evaluatingKingdom,
                        clan,
                        term);
                case KingdomNegotiationTermType.PrisonerHero:
                    return Component("{=MP_NegotiationScorePrisoner}prisoner",
                        EvaluatePrisoner(evaluatingKingdom, term));
                case KingdomNegotiationTermType.Peace:
                    return Component("{=MP_NegotiationScorePeace}peace",
                        EvaluatePeace(draft, evaluatingKingdom, clan));
                case KingdomNegotiationTermType.DeclareWar:
                    return Component(
                        "{=MP_NegotiationScoreDeclareWar}declare war",
                        EvaluateDeclareWar(draft, evaluatingKingdom, clan));
                case KingdomNegotiationTermType.TradeAgreement:
                    return Component("{=MP_NegotiationScoreTrade}trade",
                        EvaluateTrade(draft, evaluatingKingdom, clan));
                case KingdomNegotiationTermType.Alliance:
                    return Component("{=MP_NegotiationScoreAlliance}alliance",
                        EvaluateAlliance(draft, evaluatingKingdom, clan));
                case KingdomNegotiationTermType.EndTradeAgreement:
                    return Component(
                        "{=MP_NegotiationScoreEndTrade}end trade agreement",
                        -EvaluateTrade(draft, evaluatingKingdom, clan));
                case KingdomNegotiationTermType.EndAlliance:
                    return Component(
                        "{=MP_NegotiationScoreEndAlliance}end alliance",
                        -EvaluateAlliance(draft, evaluatingKingdom, clan));
                case KingdomNegotiationTermType.EndSubjectRelation:
                    return Component(
                        "{=MP_NegotiationScoreEndSubject}end subject agreement",
                        EvaluateEndSubject(
                            draft,
                            evaluatingKingdom,
                            clan));
                case KingdomNegotiationTermType.JoinWar:
                    return Component(
                        "{=MP_NegotiationScoreJoinWar}join war",
                        EvaluateJoinWar(
                            draft,
                            evaluatingKingdom,
                            clan,
                            term));
                default:
                    return Component("{=MP_NegotiationScoreSubject}subject status",
                        EvaluateSubject(draft, evaluatingKingdom, clan, term));
            }
        }

        private static float EvaluateGold(
            Kingdom evaluatingKingdom,
            Clan clan,
            KingdomNegotiationDraftTerm term)
        {
            float rulerWealth = Math.Max(
                10000f,
                (float)(evaluatingKingdom.Leader?.Gold ?? 0));
            float value = Clamp(term.Amount / rulerWealth * 80f, 0f, 80f);
            float clanExposure = clan == evaluatingKingdom.RulingClan
                ? 1f
                : 0.25f;
            return Direction(evaluatingKingdom, term) * value * clanExposure;
        }

        private static float EvaluateSettlement(
            KingdomNegotiationDraft draft,
            Kingdom evaluatingKingdom,
            Clan clan,
            KingdomNegotiationDraftTerm term)
        {
            Settlement settlement = term.Subject as Settlement;
            Kingdom provider = term.ProviderKingdom;
            Kingdom recipient = Other(draft, provider);
            if (settlement?.Town == null || provider == null)
            {
                return 0f;
            }

            return provider == evaluatingKingdom
                ? KingdomTerritoryNegotiationEvaluator
                    .EvaluateTerritorialCession(
                        provider,
                        recipient,
                        clan,
                        settlement)
                : KingdomTerritoryNegotiationEvaluator
                    .EvaluateTerritorialAcquisition(
                        evaluatingKingdom,
                        provider,
                        clan,
                        settlement);
        }

        /// <summary>
        /// Keeps the concrete settlement and the direction of transfer in the
        /// diagnostic component name. This makes balance logs useful when a
        /// proposal contains several territorial terms.
        /// </summary>
        private static KingdomNegotiationScoreComponent
            EvaluateSettlementComponent(
                KingdomNegotiationDraft draft,
                Kingdom evaluatingKingdom,
                Clan clan,
                KingdomNegotiationDraftTerm term)
        {
            Settlement settlement = term.Subject as Settlement;
            bool isCession = term.ProviderKingdom == evaluatingKingdom;
            TextObject name = new TextObject(isCession
                ? "{=MP_NegotiationScoreTerritorialCession}cession of {SETTLEMENT}"
                : "{=MP_NegotiationScoreTerritorialAcquisition}acquisition of {SETTLEMENT}");
            name.SetTextVariable(
                "SETTLEMENT",
                settlement?.Name ?? TextObject.GetEmpty());
            return Component(
                name.ToString(),
                EvaluateSettlement(draft, evaluatingKingdom, clan, term));
        }

        private static float EvaluatePrisoner(
            Kingdom evaluatingKingdom,
            KingdomNegotiationDraftTerm term)
        {
            Hero hero = term.Subject as Hero;
            if (hero == null)
            {
                return 0f;
            }

            float value = 15f + (hero.Clan?.Tier ?? 0) * 6f;
            if (hero.Clan?.Leader == hero)
            {
                value += 20f;
            }
            if (hero.Clan?.Kingdom?.Leader == hero)
            {
                value += 30f;
            }

            return Direction(evaluatingKingdom, term) * value;
        }

        private static float EvaluatePeace(
            KingdomNegotiationDraft draft,
            Kingdom kingdom,
            Clan clan)
        {
            Kingdom other = Other(draft, kingdom);
            if (other == null)
            {
                return 0f;
            }

            MakePeaceKingdomDecision decision =
                new MakePeaceKingdomDecision(clan, other);
            float peaceSupport = decision.DetermineSupport(
                clan,
                new MakePeaceKingdomDecision.MakePeaceDecisionOutcome(
                    true,
                    kingdom,
                    other));
            float continueWarSupport = decision.DetermineSupport(
                clan,
                new MakePeaceKingdomDecision.MakePeaceDecisionOutcome(
                    false,
                    kingdom,
                    other));
            return peaceSupport - continueWarSupport;
        }

        private static float EvaluateTrade(
            KingdomNegotiationDraft draft,
            Kingdom kingdom,
            Clan clan)
        {
            Kingdom other = Other(draft, kingdom);
            if (other == null)
            {
                return 0f;
            }

            TextObject hint;
            return new TradeAgreementDecision(clan, other)
                .CalculateSupport(clan, out hint);
        }

        private static float EvaluateDeclareWar(
            KingdomNegotiationDraft draft,
            Kingdom kingdom,
            Clan clan)
        {
            Kingdom other = Other(draft, kingdom);
            if (other == null)
            {
                return 0f;
            }

            DeclareWarDecision decision = new DeclareWarDecision(clan, other);
            float warSupport = decision.DetermineSupport(
                clan,
                new DeclareWarDecision.DeclareWarDecisionOutcome(
                    true,
                    kingdom,
                    other));
            float peaceSupport = decision.DetermineSupport(
                clan,
                new DeclareWarDecision.DeclareWarDecisionOutcome(
                    false,
                    kingdom,
                    other));
            return warSupport - peaceSupport;
        }

        private static float EvaluateAlliance(
            KingdomNegotiationDraft draft,
            Kingdom kingdom,
            Clan clan)
        {
            Kingdom other = Other(draft, kingdom);
            if (other == null)
            {
                return 0f;
            }

            TextObject hint;
            return new StartAllianceDecision(clan, other)
                .CalculateSupport(clan, out hint);
        }

        private static float EvaluateEndSubject(
            KingdomNegotiationDraft draft,
            Kingdom evaluatingKingdom,
            Clan clan)
        {
            SubjectRelationData relation = KingdomDiplomacyManager.Current?
                .GetSubjectRelation(draft.PlayerKingdom);
            if (relation?.OverlordKingdom != draft.TargetKingdom)
            {
                relation = KingdomDiplomacyManager.Current?
                    .GetSubjectRelation(draft.TargetKingdom);
            }
            if (relation == null)
            {
                return 0f;
            }

            bool isSubject = relation.SubjectKingdom == evaluatingKingdom;
            float sovereignty = relation.Type == SubjectType.Puppet
                ? 120f
                : 80f;
            float score = isSubject ? sovereignty : -sovereignty * 0.75f;
            if (clan == evaluatingKingdom.RulingClan)
            {
                score *= 1.2f;
            }

            return score;
        }

        private static float EvaluateJoinWar(
            KingdomNegotiationDraft draft,
            Kingdom evaluatingKingdom,
            Clan clan,
            KingdomNegotiationDraftTerm term)
        {
            Kingdom joining = term.ProviderKingdom;
            Kingdom caller = Other(draft, joining);
            Kingdom enemy = term.Subject as Kingdom;
            if (joining == null || caller == null || enemy == null)
            {
                return 0f;
            }

            TextObject reason;
            return evaluatingKingdom == joining
                ? Campaign.Current.Models.AllianceModel.GetScoreOfJoiningWar(
                    caller,
                    joining,
                    enemy,
                    clan,
                    out reason)
                : Campaign.Current.Models.AllianceModel.GetScoreOfCallingToWar(
                    caller,
                    joining,
                    enemy,
                    clan,
                    out reason);
        }

        private static float EvaluateSubject(
            KingdomNegotiationDraft draft,
            Kingdom evaluatingKingdom,
            Clan clan,
            KingdomNegotiationDraftTerm term)
        {
            bool playerBecomesSubject = term.Type
                == KingdomNegotiationTermType.PlayerBecomesVassal
                || term.Type
                == KingdomNegotiationTermType.PlayerBecomesPuppet;
            SubjectType type = term.Type
                    == KingdomNegotiationTermType.TargetBecomesPuppet
                || term.Type
                    == KingdomNegotiationTermType.PlayerBecomesPuppet
                ? SubjectType.Puppet
                : SubjectType.Vassal;
            Kingdom subject = playerBecomesSubject
                ? draft.PlayerKingdom
                : draft.TargetKingdom;
            Kingdom overlord = playerBecomesSubject
                ? draft.TargetKingdom
                : draft.PlayerKingdom;

            SubjectProposalEvaluation evaluation;
            if (evaluatingKingdom == subject)
            {
                evaluation = playerBecomesSubject
                    ? SubjectProposalEvaluationService
                        .EvaluateSubmissionIntent(overlord, subject, type)
                    : SubjectProposalEvaluationService
                        .EvaluateDemand(overlord, subject, type);
            }
            else
            {
                evaluation = SubjectProposalEvaluationService
                    .EvaluateSubmissionOffer(overlord, subject, type);
            }

            return evaluation.ClanEvaluations
                .FirstOrDefault(x => x.Clan == clan)?.RawScore ?? 0f;
        }

        private static Kingdom Other(
            KingdomNegotiationDraft draft,
            Kingdom kingdom)
        {
            return kingdom == draft.PlayerKingdom
                ? draft.TargetKingdom
                : draft.PlayerKingdom;
        }

        private static float Direction(
            Kingdom evaluatingKingdom,
            KingdomNegotiationDraftTerm term)
        {
            return term.ProviderKingdom == evaluatingKingdom ? -1f : 1f;
        }

        private static KingdomNegotiationScoreComponent Component(
            string name,
            float score)
        {
            return new KingdomNegotiationScoreComponent
            {
                Name = new TextObject(name).ToString(),
                Score = SafeNumber(score)
            };
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static float SafeNumber(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static string Format(float value)
        {
            return value >= 0f
                ? "+" + value.ToString("F1")
                : value.ToString("F1");
        }
    }
}
