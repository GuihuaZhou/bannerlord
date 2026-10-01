using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Services
{
    /// <summary>
    /// Executes packages composed exclusively of supported native campaign
    /// actions. Every term is preflighted before the first mutation, then the
    /// package is applied in a deterministic dependency order.
    /// </summary>
    public static class KingdomNegotiationExecutionService
    {
        public static bool TryExecuteSupportedProposal(
            KingdomNegotiationDraft draft,
            int startIndex = 0,
            Action<int> onTermCompleted = null)
        {
            TextObject validationReason = null;
            if (draft == null
                || draft.Terms.Count == 0)
            {
                LogProposalFailure(
                    new TextObject(
                        "{=MP_NegotiationDebugInvalidDraft}proposal validation failed"));
                return false;
            }

            Kingdom first = draft.PlayerKingdom;
            Kingdom second = draft.TargetKingdom;
            List<KingdomNegotiationDraftTerm> orderedTerms =
                GetOrderedTerms(draft);
            startIndex = Math.Max(0, Math.Min(startIndex, orderedTerms.Count));
            if (startIndex == 0
                && !KingdomNegotiationDraftValidator.TryValidate(
                    draft,
                    out validationReason))
            {
                LogProposalFailure(validationReason);
                return false;
            }

            if (first == null
                || second == null
                || first.IsEliminated
                || second.IsEliminated)
            {
                LogProposalFailure(new TextObject(
                    "{=MP_NegotiationDebugInvalidDraft}proposal validation failed"));
                return false;
            }

            if (draft.Terms.Any(term => !IsSupported(term.Type)))
            {
                LogProposalFailure(new TextObject(
                    "{=MP_NegotiationDebugUnsupportedPackage}the proposal contains terms that are not executable yet"));
                return false;
            }

            // Validate every term before changing campaign state. Native
            // diplomacy actions are not generally reversible, so no term may
            // execute until the complete supported package passes preflight.
            foreach (KingdomNegotiationDraftTerm term in orderedTerms
                .Skip(startIndex))
            {
                if (!IsTermSatisfied(first, second, term)
                    && !CanExecuteTerm(first, second, term, draft))
                {
                    LogExecution(
                        first,
                        second,
                        term.Type,
                        "{=MP_NegotiationDebugPreflightFailed}execution preflight failed");
                    return false;
                }
            }

            try
            {
                // Peace changes the validity of other diplomacy actions, and
                // settlement transfers are the most expensive world-state
                // mutation. Keep a stable, explicit execution order.
                for (int index = startIndex;
                    index < orderedTerms.Count;
                    index++)
                {
                    KingdomNegotiationDraftTerm term = orderedTerms[index];
                    if (!IsTermSatisfied(first, second, term))
                    {
                        LogExecution(
                            first,
                            second,
                            term.Type,
                            "{=MP_NegotiationDebugExecutionAttempt}execution requested");
                        if (!ExecuteTerm(first, second, term))
                        {
                            LogExecution(
                                first,
                                second,
                                term.Type,
                                "{=MP_NegotiationDebugExecutionNotConfirmed}the native system did not confirm the treaty state");
                            return false;
                        }

                        LogTermExecuted(term.Type);
                    }

                    onTermCompleted?.Invoke(index + 1);
                }
            }
            catch (Exception exception)
            {
                TextObject error = new TextObject(
                    "{=MP_NegotiationDebugExecutionException}[Kingdom negotiation debug] Treaty execution failed with an exception. {ERROR}");
                error.SetTextVariable("ERROR", exception.ToString());
                ModLogger.Error(error.ToString());
                return false;
            }

            TextObject completed = new TextObject(
                "{=MP_NegotiationProposalExecuted}[Kingdom negotiation] The compound proposal between {FIRST} and {SECOND} was fully executed with {COUNT} terms.");
            completed.SetTextVariable("FIRST", first.Name);
            completed.SetTextVariable("SECOND", second.Name);
            completed.SetTextVariable("COUNT", draft.Terms.Count);
            ModLogger.Notice(completed.ToString());
            return true;
        }

        private static List<KingdomNegotiationDraftTerm> GetOrderedTerms(
            KingdomNegotiationDraft draft)
        {
            KingdomNegotiationTermType[] order =
            {
                KingdomNegotiationTermType.Peace,
                KingdomNegotiationTermType.DeclareWar,
                KingdomNegotiationTermType.TargetBecomesVassal,
                KingdomNegotiationTermType.TargetBecomesPuppet,
                KingdomNegotiationTermType.PlayerBecomesVassal,
                KingdomNegotiationTermType.PlayerBecomesPuppet,
                KingdomNegotiationTermType.TradeAgreement,
                KingdomNegotiationTermType.Alliance,
                KingdomNegotiationTermType.JoinWar,
                KingdomNegotiationTermType.EndTradeAgreement,
                KingdomNegotiationTermType.EndAlliance,
                KingdomNegotiationTermType.EndSubjectRelation,
                KingdomNegotiationTermType.PrisonerHero,
                KingdomNegotiationTermType.Settlement,
                KingdomNegotiationTermType.Gold
            };
            return order.SelectMany(type => draft.Terms
                    .Where(term => term.Type == type))
                .ToList();
        }

        private static bool IsSupported(KingdomNegotiationTermType type)
        {
            return type == KingdomNegotiationTermType.Peace
                || type == KingdomNegotiationTermType.DeclareWar
                || type == KingdomNegotiationTermType.TradeAgreement
                || type == KingdomNegotiationTermType.Alliance
                || KingdomNegotiationTermRules.IsTerminationTerm(type)
                || type == KingdomNegotiationTermType.JoinWar
                || type == KingdomNegotiationTermType.Gold
                || type == KingdomNegotiationTermType.PrisonerHero
                || KingdomNegotiationTermRules.IsSubjectTerm(type)
                || type == KingdomNegotiationTermType.Settlement;
        }

        private static bool CanExecuteTerm(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term,
            KingdomNegotiationDraft draft)
        {
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Peace:
                    return first?.IsAtWarWith(second) == true;
                case KingdomNegotiationTermType.DeclareWar:
                    return first?.IsAtWarWith(second) == false;
                case KingdomNegotiationTermType.TradeAgreement:
                    if (Campaign.Current == null)
                    {
                        return false;
                    }

                    ITradeAgreementsCampaignBehavior tradeBehavior =
                        Campaign.Current.GetCampaignBehavior<
                            ITradeAgreementsCampaignBehavior>();
                    return tradeBehavior != null
                        && !tradeBehavior.HasTradeAgreement(
                            first,
                            second,
                            out _);
                case KingdomNegotiationTermType.Alliance:
                    IAllianceCampaignBehavior allianceBehavior =
                        Campaign.Current.GetCampaignBehavior<
                            IAllianceCampaignBehavior>();
                    return allianceBehavior != null
                        && !allianceBehavior.IsAllyWithKingdom(first, second);
                case KingdomNegotiationTermType.EndTradeAgreement:
                    return Campaign.Current.GetCampaignBehavior<
                            ITradeAgreementsCampaignBehavior>()?
                        .HasTradeAgreement(first, second, out _) == true;
                case KingdomNegotiationTermType.EndAlliance:
                    return Campaign.Current.GetCampaignBehavior<
                            IAllianceCampaignBehavior>()?
                        .IsAllyWithKingdom(first, second) == true;
                case KingdomNegotiationTermType.EndSubjectRelation:
                    return GetSubjectBetween(first, second) != null;
                case KingdomNegotiationTermType.JoinWar:
                    return CanJoinWar(first, second, term)
                        || draft.Terms.Any(item => item.Type
                            == KingdomNegotiationTermType.Alliance);
                case KingdomNegotiationTermType.Gold:
                    return CanTransferGold(first, second, term);
                case KingdomNegotiationTermType.PrisonerHero:
                    return CanTransferPrisoner(first, second, term);
                case KingdomNegotiationTermType.Settlement:
                    return CanTransferSettlement(first, second, term);
                default:
                    return KingdomNegotiationTermRules.IsSubjectTerm(
                            term.Type)
                        && CanEstablishSubjectRelation(first, second, term);
            }
        }

        /// <summary>
        /// Recognizes clauses that reached their requested world state before
        /// execution progress was saved. This makes retries idempotent for all
        /// non-monetary native actions.
        /// </summary>
        private static bool IsTermSatisfied(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Peace:
                    return first?.IsAtWarWith(second) == false;
                case KingdomNegotiationTermType.DeclareWar:
                    return first?.IsAtWarWith(second) == true;
                case KingdomNegotiationTermType.TradeAgreement:
                    ITradeAgreementsCampaignBehavior tradeBehavior =
                        Campaign.Current.GetCampaignBehavior<
                            ITradeAgreementsCampaignBehavior>();
                    return tradeBehavior?.HasTradeAgreement(
                        first,
                        second,
                        out _) == true;
                case KingdomNegotiationTermType.Alliance:
                    if (Campaign.Current == null)
                    {
                        return false;
                    }

                    return Campaign.Current.GetCampaignBehavior<
                            IAllianceCampaignBehavior>()?
                        .IsAllyWithKingdom(first, second) == true;
                case KingdomNegotiationTermType.EndTradeAgreement:
                    return Campaign.Current.GetCampaignBehavior<
                            ITradeAgreementsCampaignBehavior>()?
                        .HasTradeAgreement(first, second, out _) != true;
                case KingdomNegotiationTermType.EndAlliance:
                    return Campaign.Current.GetCampaignBehavior<
                            IAllianceCampaignBehavior>()?
                        .IsAllyWithKingdom(first, second) != true;
                case KingdomNegotiationTermType.EndSubjectRelation:
                    return GetSubjectBetween(first, second) == null;
                case KingdomNegotiationTermType.JoinWar:
                    return term.ProviderKingdom?.IsAtWarWith(
                        term.Subject as Kingdom) == true;
                case KingdomNegotiationTermType.PrisonerHero:
                    return (term.Subject as Hero)?.IsPrisoner == false;
                case KingdomNegotiationTermType.Settlement:
                    Settlement settlement = term.Subject as Settlement;
                    Kingdom settlementReceiver = term.ProviderKingdom == first
                        ? second
                        : first;
                    return settlement?.OwnerClan?.Kingdom
                        == settlementReceiver;
                case KingdomNegotiationTermType.Gold:
                    // Gold has no unique transaction marker. Its progress is
                    // advanced immediately after GiveGoldAction returns.
                    return false;
                default:
                    if (!KingdomNegotiationTermRules.IsSubjectTerm(term.Type))
                    {
                        return false;
                    }

                    GetSubjectRelationParties(
                        first,
                        second,
                        term.Type,
                        out Kingdom overlord,
                        out Kingdom subject,
                        out SubjectType subjectType);
                    return KingdomDiplomacyManager.Current?
                            .GetSubjectRelation(subject)?.OverlordKingdom
                            == overlord
                        && KingdomDiplomacyManager.Current
                            .GetSubjectType(subject) == subjectType;
            }
        }

        private static bool ExecuteTerm(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Peace:
                    return ExecutePeace(first, second);
                case KingdomNegotiationTermType.DeclareWar:
                    return ExecuteDeclareWar(first, second, term);
                case KingdomNegotiationTermType.TradeAgreement:
                    return ExecuteTrade(first, second);
                case KingdomNegotiationTermType.Alliance:
                    return ExecuteAlliance(first, second);
                case KingdomNegotiationTermType.EndTradeAgreement:
                    return ExecuteEndTrade(first, second);
                case KingdomNegotiationTermType.EndAlliance:
                    return ExecuteEndAlliance(first, second);
                case KingdomNegotiationTermType.EndSubjectRelation:
                    return ExecuteEndSubject(first, second);
                case KingdomNegotiationTermType.JoinWar:
                    return ExecuteJoinWar(first, second, term);
                case KingdomNegotiationTermType.Gold:
                    return ExecuteGoldTransfer(first, second, term);
                case KingdomNegotiationTermType.PrisonerHero:
                    return ExecutePrisonerTransfer(first, second, term);
                case KingdomNegotiationTermType.Settlement:
                    return ExecuteSettlementTransfer(first, second, term);
                default:
                    return KingdomNegotiationTermRules.IsSubjectTerm(
                            term.Type)
                        && ExecuteSubjectRelation(first, second, term);
            }
        }

        private static bool ExecuteSubjectRelation(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            GetSubjectRelationParties(
                first,
                second,
                term.Type,
                out Kingdom overlord,
                out Kingdom subject,
                out SubjectType subjectType);
            if (!CanEstablishSubjectRelation(first, second, term))
            {
                return false;
            }

            return SubjectRelationAction.TryEstablish(
                    overlord,
                    subject,
                    subjectType)
                && KingdomDiplomacyManager.Current?
                    .GetSubjectRelation(subject)?.OverlordKingdom
                    == overlord
                && KingdomDiplomacyManager.Current
                    .GetSubjectType(subject) == subjectType;
        }

        internal static bool CanEstablishSubjectRelation(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            GetSubjectRelationParties(
                first,
                second,
                term.Type,
                out Kingdom overlord,
                out Kingdom subject,
                out SubjectType subjectType);
            return overlord != null
                && subject != null
                && term.ProviderKingdom == subject
                && KingdomDiplomacyManager.Current?
                    .CanEstablishSubjectRelation(
                        overlord,
                        subject,
                        subjectType) == true;
        }

        private static void GetSubjectRelationParties(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationTermType type,
            out Kingdom overlord,
            out Kingdom subject,
            out SubjectType subjectType)
        {
            bool playerBecomesSubject = type
                    == KingdomNegotiationTermType.PlayerBecomesVassal
                || type == KingdomNegotiationTermType.PlayerBecomesPuppet;
            subject = playerBecomesSubject ? first : second;
            overlord = playerBecomesSubject ? second : first;
            subjectType = type == KingdomNegotiationTermType.PlayerBecomesPuppet
                    || type == KingdomNegotiationTermType.TargetBecomesPuppet
                ? SubjectType.Puppet
                : SubjectType.Vassal;
        }

        private static bool ExecutePeace(Kingdom first, Kingdom second)
        {
            if (first?.IsAtWarWith(second) != true)
            {
                return false;
            }

            MakePeaceAction.ApplyByKingdomDecision(first, second, 0, 0);
            return !first.IsAtWarWith(second);
        }

        private static bool ExecuteDeclareWar(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            Kingdom aggressor = term.ProviderKingdom;
            Kingdom defender = aggressor == first ? second : first;
            if (aggressor == null
                || defender == null
                || aggressor.IsAtWarWith(defender))
            {
                return false;
            }

            DeclareWarAction.ApplyByKingdomDecision(aggressor, defender);
            return aggressor.IsAtWarWith(defender);
        }

        private static bool ExecuteTrade(Kingdom first, Kingdom second)
        {
            ITradeAgreementsCampaignBehavior behavior = Campaign.Current
                .GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
            if (behavior == null
                || behavior.HasTradeAgreement(first, second, out _))
            {
                LogExecution(
                    first,
                    second,
                    KingdomNegotiationTermType.TradeAgreement,
                    behavior == null
                        ? "{=MP_NegotiationDebugTradeBehaviorMissing}trade-agreement behavior is unavailable"
                        : "{=MP_NegotiationDebugTradeAlreadyExists}a trade agreement already exists");
                return false;
            }

            CampaignTime duration = Campaign.Current.Models
                .TradeAgreementModel
                .GetTradeAgreementDurationInYears(first, second);
            behavior.MakeTradeAgreement(first, second, duration);
            return behavior.HasTradeAgreement(first, second, out _);
        }

        private static bool ExecuteAlliance(Kingdom first, Kingdom second)
        {
            IAllianceCampaignBehavior behavior = Campaign.Current
                .GetCampaignBehavior<IAllianceCampaignBehavior>();
            if (behavior == null
                || behavior.IsAllyWithKingdom(first, second))
            {
                LogExecution(
                    first,
                    second,
                    KingdomNegotiationTermType.Alliance,
                    behavior == null
                        ? "{=MP_NegotiationDebugAllianceBehaviorMissing}alliance behavior is unavailable"
                        : "{=MP_NegotiationDebugAllianceAlreadyExists}an alliance already exists");
                return false;
            }

            behavior.StartAlliance(first, second);
            return behavior.IsAllyWithKingdom(first, second);
        }

        private static bool ExecuteEndTrade(Kingdom first, Kingdom second)
        {
            ITradeAgreementsCampaignBehavior behavior = Campaign.Current
                .GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
            if (behavior?.HasTradeAgreement(first, second, out _) != true)
            {
                return false;
            }

            behavior.EndTradeAgreement(first, second);
            return !behavior.HasTradeAgreement(first, second, out _);
        }

        private static bool ExecuteEndAlliance(Kingdom first, Kingdom second)
        {
            IAllianceCampaignBehavior behavior = Campaign.Current
                .GetCampaignBehavior<IAllianceCampaignBehavior>();
            if (behavior?.IsAllyWithKingdom(first, second) != true)
            {
                return false;
            }

            behavior.EndAlliance(first, second);
            return !behavior.IsAllyWithKingdom(first, second);
        }

        private static bool ExecuteEndSubject(Kingdom first, Kingdom second)
        {
            Kingdom subject = GetSubjectBetween(first, second);
            return subject != null
                && SubjectRelationAction.TryRelease(subject)
                && GetSubjectBetween(first, second) == null;
        }

        private static Kingdom GetSubjectBetween(
            Kingdom first,
            Kingdom second)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager?.GetSubjectRelation(first)?.OverlordKingdom == second)
            {
                return first;
            }

            return manager?.GetSubjectRelation(second)?.OverlordKingdom
                == first
                    ? second
                    : null;
        }

        private static bool CanJoinWar(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            Kingdom joining = term.ProviderKingdom;
            Kingdom caller = joining == first ? second : first;
            Kingdom enemy = term.Subject as Kingdom;
            IAllianceCampaignBehavior behavior = Campaign.Current
                .GetCampaignBehavior<IAllianceCampaignBehavior>();
            return joining != null
                && caller != null
                && enemy != null
                && behavior?.IsAllyWithKingdom(caller, joining) == true
                && caller.IsAtWarWith(enemy)
                && !joining.IsAtWarWith(enemy);
        }

        private static bool ExecuteJoinWar(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            if (!CanJoinWar(first, second, term))
            {
                return false;
            }

            Kingdom joining = term.ProviderKingdom;
            Kingdom caller = joining == first ? second : first;
            Kingdom enemy = term.Subject as Kingdom;
            IAllianceCampaignBehavior behavior = Campaign.Current
                .GetCampaignBehavior<IAllianceCampaignBehavior>();
            behavior.StartCallToWarAgreement(
                caller,
                joining,
                enemy,
                0,
                false);
            return joining.IsAtWarWith(enemy);
        }

        /// <summary>
        /// Transfers an approved payment between the two ruling clan leaders.
        /// Gold clauses are defined against kingdom rulers in the negotiation
        /// UI, matching the actors that actually own the displayed balances.
        /// </summary>
        private static bool ExecuteGoldTransfer(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            if (!CanTransferGold(first, second, term))
            {
                return false;
            }

            Hero payer = term.ProviderKingdom.Leader;
            Kingdom receiverKingdom = term.ProviderKingdom == first
                ? second
                : first;
            Hero receiver = receiverKingdom.Leader;
            int payerGoldBefore = payer.Gold;
            int receiverGoldBefore = receiver.Gold;

            GiveGoldAction.ApplyBetweenCharacters(
                payer,
                receiver,
                term.Amount,
                true);
            return payer.Gold == payerGoldBefore - term.Amount
                && receiver.Gold == receiverGoldBefore + term.Amount;
        }

        private static bool CanTransferGold(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            Kingdom provider = term.ProviderKingdom;
            Kingdom receiver = provider == first ? second : first;
            Hero payer = term.Subject as Hero;
            return provider != null
                && receiver != null
                && (provider == first || provider == second)
                && payer != null
                && payer == provider.Leader
                && payer.IsAlive
                && receiver.Leader?.IsAlive == true
                && term.Amount > 0
                && payer.Gold >= term.Amount;
        }

        /// <summary>
        /// Uses the same ransom release action as Bannerlord's
        /// SetPrisonerFreeBarterable. A bilateral kingdom negotiation may
        /// release only a hero belonging to the other negotiating kingdom.
        /// </summary>
        private static bool ExecutePrisonerTransfer(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            if (!CanTransferPrisoner(first, second, term))
            {
                return false;
            }

            Hero prisoner = term.Subject as Hero;
            Kingdom receiverKingdom = term.ProviderKingdom == first
                ? second
                : first;
            EndCaptivityAction.ApplyByRansom(
                prisoner,
                receiverKingdom.Leader);
            return !prisoner.IsPrisoner;
        }

        internal static bool CanTransferPrisoner(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            Kingdom provider = term.ProviderKingdom;
            Kingdom receiver = provider == first ? second : first;
            Hero prisoner = term.Subject as Hero;
            return provider != null
                && receiver != null
                && (provider == first || provider == second)
                && prisoner?.IsPrisoner == true
                && prisoner.PartyBelongedToAsPrisoner?.MapFaction
                    == provider
                && prisoner.MapFaction == receiver
                && receiver.Leader != null;
        }

        /// <summary>
        /// Transfers an approved town or castle to the receiving kingdom's
        /// ruling clan. Kingdom negotiations target kingdoms rather than an
        /// individual recipient clan, so the ruling clan is the deterministic
        /// initial owner.
        /// </summary>
        private static bool ExecuteSettlementTransfer(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            Settlement settlement = term.Subject as Settlement;
            Kingdom provider = term.ProviderKingdom;
            Kingdom receiver = provider == first ? second : first;
            Hero newOwner = receiver?.RulingClan?.Leader;
            if (!CanTransferSettlement(first, second, term))
            {
                LogExecution(
                    first,
                    second,
                    term.Type,
                    "{=MP_NegotiationDebugSettlementInvalid}settlement transfer validation failed");
                return false;
            }

            TextObject before = new TextObject(
                "{=MP_NegotiationDebugSettlementTransfer}[Kingdom negotiation debug] Transferring {SETTLEMENT} from {PROVIDER} to the ruling clan of {RECEIVER}.");
            before.SetTextVariable("SETTLEMENT", settlement.Name);
            before.SetTextVariable("PROVIDER", provider.Name);
            before.SetTextVariable("RECEIVER", receiver.Name);
            ModLogger.Info(before.ToString());

            ChangeOwnerOfSettlementAction.ApplyByBarter(
                newOwner,
                settlement);
            return settlement.OwnerClan?.Kingdom == receiver;
        }

        private static bool CanTransferSettlement(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            Settlement settlement = term.Subject as Settlement;
            Kingdom provider = term.ProviderKingdom;
            Kingdom receiver = provider == first ? second : first;
            Hero newOwner = receiver?.RulingClan?.Leader;
            return settlement != null
                && settlement.IsFortification
                && provider != null
                && receiver != null
                && (provider == first || provider == second)
                && settlement.OwnerClan?.Kingdom == provider
                && newOwner != null
                && newOwner.IsAlive;
        }

        private static void LogTermExecuted(
            KingdomNegotiationTermType type)
        {
            TextObject message = new TextObject(
                "{=MP_NegotiationSingleTreatyExecuted}[Kingdom negotiation] The proposal was approved and the {TREATY} is now in effect.");
            message.SetTextVariable("TREATY", TreatyText(type));
            ModLogger.Notice(message.ToString());
        }

        private static void LogProposalFailure(TextObject reason)
        {
            TextObject message = new TextObject(
                "{=MP_NegotiationDebugProposalExecutionRejected}[Kingdom negotiation debug] Proposal execution was rejected. {REASON}.");
            message.SetTextVariable(
                "REASON",
                reason ?? TextObject.GetEmpty());
            ModLogger.Error(message.ToString());
        }

        private static void LogExecution(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationTermType type,
            string detailSource)
        {
            TextObject message = new TextObject(
                "{=MP_NegotiationDebugExecution}[Kingdom negotiation debug] {FIRST} and {SECOND}. Treaty: {TREATY}. State: {DETAIL}.");
            message.SetTextVariable(
                "FIRST",
                first?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable(
                "SECOND",
                second?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("TREATY", TreatyText(type));
            message.SetTextVariable("DETAIL", new TextObject(detailSource));
            ModLogger.Info(message.ToString());
        }

        private static TextObject TreatyText(
            KingdomNegotiationTermType type)
        {
            switch (type)
            {
                case KingdomNegotiationTermType.Peace:
                    return new TextObject(
                        "{=MP_NegotiationExecutedPeace}peace treaty");
                case KingdomNegotiationTermType.DeclareWar:
                    return new TextObject(
                        "{=MP_NegotiationExecutedDeclareWar}declaration of war");
                case KingdomNegotiationTermType.TradeAgreement:
                    return new TextObject(
                        "{=MP_NegotiationExecutedTrade}trade agreement");
                case KingdomNegotiationTermType.Settlement:
                    return new TextObject(
                        "{=MP_NegotiationExecutedSettlement}settlement transfer");
                case KingdomNegotiationTermType.Gold:
                    return new TextObject(
                        "{=MP_NegotiationExecutedGold}gold payment");
                case KingdomNegotiationTermType.PrisonerHero:
                    return new TextObject(
                        "{=MP_NegotiationExecutedPrisoner}hero prisoner release");
                case KingdomNegotiationTermType.EndTradeAgreement:
                    return new TextObject(
                        "{=MP_NegotiationExecutedEndTrade}termination of trade agreement");
                case KingdomNegotiationTermType.EndAlliance:
                    return new TextObject(
                        "{=MP_NegotiationExecutedEndAlliance}termination of alliance");
                case KingdomNegotiationTermType.EndSubjectRelation:
                    return new TextObject(
                        "{=MP_NegotiationExecutedEndSubject}termination of subject agreement");
                case KingdomNegotiationTermType.JoinWar:
                    return new TextObject(
                        "{=MP_NegotiationExecutedJoinWar}call to war");
                default:
                    return KingdomNegotiationTermRules.IsSubjectTerm(type)
                        ? new TextObject(
                            "{=MP_NegotiationExecutedSubject}subject relation")
                        : new TextObject(
                            "{=MP_NegotiationExecutedAlliance}alliance");
            }
        }
    }
}
