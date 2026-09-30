using System.Linq;
using TaleWorlds.CampaignSystem;
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

            foreach (KingdomNegotiationDraftTerm term in draft.Terms)
            {
                if (!IsParticipant(draft, term.ProviderKingdom)
                    || term.Amount <= 0
                    || !IsTermStillOwned(term))
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
    }
}
