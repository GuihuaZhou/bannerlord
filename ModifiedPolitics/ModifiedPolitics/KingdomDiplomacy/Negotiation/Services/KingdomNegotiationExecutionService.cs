using System;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Services
{
    /// <summary>
    /// Executes proposals that already have a naturally atomic native action.
    /// Asset packages deliberately remain disabled until the transactional
    /// executor can validate and apply the complete package together.
    /// </summary>
    public static class KingdomNegotiationExecutionService
    {
        public static bool TryExecuteSupportedProposal(
            KingdomNegotiationDraft draft)
        {
            if (draft?.Terms.Count != 1)
            {
                return false;
            }

            KingdomNegotiationDraftTerm term = draft.Terms[0];
            Kingdom first = draft.PlayerKingdom;
            Kingdom second = draft.TargetKingdom;
            LogExecution(
                first,
                second,
                term.Type,
                "{=MP_NegotiationDebugExecutionAttempt}execution requested");
            bool executed;
            try
            {
                switch (term.Type)
                {
                    case KingdomNegotiationTermType.Peace:
                        executed = ExecutePeace(first, second);
                        break;
                    case KingdomNegotiationTermType.TradeAgreement:
                        executed = ExecuteTrade(first, second);
                        break;
                    case KingdomNegotiationTermType.Alliance:
                        executed = ExecuteAlliance(first, second);
                        break;
                    case KingdomNegotiationTermType.Settlement:
                        executed = ExecuteSettlementTransfer(
                            first,
                            second,
                            term);
                        break;
                    default:
                        LogExecution(
                            first,
                            second,
                            term.Type,
                            "{=MP_NegotiationDebugUnsupportedTerm}term is not a directly executable single treaty");
                        return false;
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

            if (executed)
            {
                TextObject message = new TextObject(
                    "{=MP_NegotiationSingleTreatyExecuted}[Kingdom negotiation] The proposal was approved and the {TREATY} is now in effect.");
                message.SetTextVariable(
                    "TREATY",
                    TreatyText(term.Type));
                ModLogger.Notice(message.ToString());
            }
            else
            {
                LogExecution(
                    first,
                    second,
                    term.Type,
                    "{=MP_NegotiationDebugExecutionNotConfirmed}the native system did not confirm the treaty state");
            }

            return executed;
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
            if (settlement == null
                || !settlement.IsFortification
                || provider == null
                || receiver == null
                || provider != first && provider != second
                || settlement.OwnerClan?.Kingdom != provider
                || newOwner == null
                || !newOwner.IsAlive)
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
