using System;
using System.Linq;
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
            KingdomNegotiationDraft draft)
        {
            TextObject validationReason = null;
            if (draft == null
                || draft.Terms.Count == 0
                || !KingdomNegotiationDraftValidator.TryValidate(
                    draft,
                    out validationReason))
            {
                LogProposalFailure(
                    validationReason
                        ?? new TextObject(
                            "{=MP_NegotiationDebugInvalidDraft}proposal validation failed"));
                return false;
            }

            Kingdom first = draft.PlayerKingdom;
            Kingdom second = draft.TargetKingdom;
            if (draft.Terms.Any(term => !IsSupported(term.Type)))
            {
                LogProposalFailure(new TextObject(
                    "{=MP_NegotiationDebugUnsupportedPackage}the proposal contains terms that are not executable yet"));
                return false;
            }

            // Validate every term before changing campaign state. Native
            // diplomacy actions are not generally reversible, so no term may
            // execute until the complete supported package passes preflight.
            foreach (KingdomNegotiationDraftTerm term in draft.Terms)
            {
                if (!CanExecuteTerm(first, second, term))
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
                KingdomNegotiationTermType[] order =
                {
                    KingdomNegotiationTermType.Peace,
                    KingdomNegotiationTermType.TradeAgreement,
                    KingdomNegotiationTermType.Alliance,
                    KingdomNegotiationTermType.Settlement
                };
                foreach (KingdomNegotiationTermType type in order)
                {
                    foreach (KingdomNegotiationDraftTerm term in draft.Terms
                        .Where(x => x.Type == type))
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

            return true;
        }

        private static bool IsSupported(KingdomNegotiationTermType type)
        {
            return type == KingdomNegotiationTermType.Peace
                || type == KingdomNegotiationTermType.TradeAgreement
                || type == KingdomNegotiationTermType.Alliance
                || type == KingdomNegotiationTermType.Settlement;
        }

        private static bool CanExecuteTerm(
            Kingdom first,
            Kingdom second,
            KingdomNegotiationDraftTerm term)
        {
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Peace:
                    return first?.IsAtWarWith(second) == true;
                case KingdomNegotiationTermType.TradeAgreement:
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
                case KingdomNegotiationTermType.Settlement:
                    return CanTransferSettlement(first, second, term);
                default:
                    return false;
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
                case KingdomNegotiationTermType.TradeAgreement:
                    return ExecuteTrade(first, second);
                case KingdomNegotiationTermType.Alliance:
                    return ExecuteAlliance(first, second);
                case KingdomNegotiationTermType.Settlement:
                    return ExecuteSettlementTransfer(first, second, term);
                default:
                    return false;
            }
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
                case KingdomNegotiationTermType.TradeAgreement:
                    return new TextObject(
                        "{=MP_NegotiationExecutedTrade}trade agreement");
                case KingdomNegotiationTermType.Settlement:
                    return new TextObject(
                        "{=MP_NegotiationExecutedSettlement}settlement transfer");
                default:
                    return new TextObject(
                        "{=MP_NegotiationExecutedAlliance}alliance");
            }
        }
    }
}
