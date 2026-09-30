using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation
{
    /// <summary>
    /// Revalidates a draft against current campaign state. Negotiations may
    /// remain open while ownership, captivity or wealth changes, so execution
    /// must never trust the values captured when the screen first opened.
    /// </summary>
    public static class KingdomNegotiationDraftValidator
    {
        public static bool TryValidate(
            KingdomNegotiationDraft draft,
            out TextObject reason)
        {
            reason = TextObject.GetEmpty();
            if (draft?.PlayerKingdom == null
                || draft.TargetKingdom == null
                || draft.PlayerKingdom == draft.TargetKingdom)
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationInvalidParties}The negotiating kingdoms are no longer valid.");
                return false;
            }

            if (draft.Terms.Count == 0)
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationEmptyDraft}Add at least one term to the proposal.");
                return false;
            }

            if (draft.Terms.Count(x =>
                    KingdomNegotiationTermRules.IsSubjectTerm(x.Type)) > 1)
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationMultipleSubjectTerms}A proposal can contain only one subject-status term.");
                return false;
            }

            if (draft.Terms
                .Where(x => KingdomNegotiationTermRules
                    .IsBilateralTreaty(x.Type))
                .GroupBy(x => x.Type)
                .Any(x => x.Count() > 1))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationDuplicateTreaty}The same bilateral treaty cannot appear more than once in a proposal.");
                return false;
            }

            foreach (KingdomNegotiationDraftTerm term in draft.Terms)
            {
                if (!IsParticipant(draft, term.ProviderKingdom)
                    || term.Amount <= 0
                    || !IsTermStillOwned(term)
                    || !IsTreatyStillAvailable(draft, term))
                {
                    reason = new TextObject(
                        "{=MP_KingdomNegotiationStaleTerm}One or more proposal terms are no longer available.");
                    return false;
                }
            }

            return true;
        }

        private static bool IsParticipant(
            KingdomNegotiationDraft draft,
            Kingdom kingdom)
        {
            return kingdom == draft.PlayerKingdom
                || kingdom == draft.TargetKingdom;
        }

        private static bool IsTermStillOwned(
            KingdomNegotiationDraftTerm term)
        {
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Gold:
                    Hero payer = term.Subject as Hero;
                    return payer != null
                        && payer == term.ProviderKingdom.Leader
                        && payer.Gold >= term.Amount;

                case KingdomNegotiationTermType.Settlement:
                    Settlement settlement = term.Subject as Settlement;
                    return settlement != null
                        && settlement.IsFortification
                        && settlement.MapFaction == term.ProviderKingdom;

                case KingdomNegotiationTermType.PrisonerHero:
                    Hero prisoner = term.Subject as Hero;
                    return prisoner != null
                        && prisoner.IsPrisoner
                        && prisoner.PartyBelongedToAsPrisoner?.MapFaction
                            == term.ProviderKingdom;

                default:
                    return term.Subject == term.ProviderKingdom;
            }
        }

        private static bool IsTreatyStillAvailable(
            KingdomNegotiationDraft draft,
            KingdomNegotiationDraftTerm term)
        {
            Kingdom first = draft.PlayerKingdom;
            Kingdom second = draft.TargetKingdom;
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Peace:
                    return first.IsAtWarWith(second);

                case KingdomNegotiationTermType.TradeAgreement:
                    ITradeAgreementsCampaignBehavior tradeBehavior =
                        Campaign.Current.GetCampaignBehavior<
                            ITradeAgreementsCampaignBehavior>();
                    if (first.IsAtWarWith(second)
                        || tradeBehavior?.HasTradeAgreement(
                            first,
                            second,
                            out _) == true)
                    {
                        return false;
                    }

                    // For a standalone trade proposal, reproduce the native
                    // button/decision check exactly, including its simulated
                    // vote in the queried kingdom. A compound proposal uses
                    // the structural native checks here and applies its own
                    // package-wide foreign evaluation afterwards.
                    TextObject tradeReason;
                    return Campaign.Current.Models.TradeAgreementModel
                        .CanMakeTradeAgreement(
                            first,
                            second,
                            draft.Terms.Count == 1,
                            out tradeReason,
                            false);

                case KingdomNegotiationTermType.Alliance:
                    IAllianceCampaignBehavior allianceBehavior =
                        Campaign.Current.GetCampaignBehavior<
                            IAllianceCampaignBehavior>();
                    return !first.IsAtWarWith(second)
                        && (allianceBehavior == null
                            || !allianceBehavior.IsAllyWithKingdom(
                                first,
                                second));

                default:
                    return true;
            }
        }
    }
}
