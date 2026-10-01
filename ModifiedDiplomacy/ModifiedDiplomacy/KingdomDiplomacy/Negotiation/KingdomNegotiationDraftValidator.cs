using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.Negotiation
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
                    .IsBilateralTreaty(x.Type)
                    || KingdomNegotiationTermRules
                        .IsTerminationTerm(x.Type))
                .GroupBy(x => x.Type)
                .Any(x => x.Count() > 1))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationDuplicateTreaty}The same bilateral treaty cannot appear more than once in a proposal.");
                return false;
            }

            if ((Has(draft, KingdomNegotiationTermType.TradeAgreement)
                    && Has(draft,
                        KingdomNegotiationTermType.EndTradeAgreement))
                || (Has(draft, KingdomNegotiationTermType.Alliance)
                    && Has(draft, KingdomNegotiationTermType.EndAlliance))
                || (draft.Terms.Any(x =>
                        KingdomNegotiationTermRules.IsSubjectTerm(x.Type))
                    && Has(draft,
                        KingdomNegotiationTermType.EndSubjectRelation)))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationOppositeTerms}A proposal cannot create and end the same diplomatic relation.");
                return false;
            }

            if (Has(draft, KingdomNegotiationTermType.Peace)
                && Has(draft, KingdomNegotiationTermType.DeclareWar))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationPeaceWarConflict}A proposal cannot make peace and declare war at the same time.");
                return false;
            }

            if (Has(draft, KingdomNegotiationTermType.DeclareWar)
                && (Has(draft, KingdomNegotiationTermType.TradeAgreement)
                    || Has(draft, KingdomNegotiationTermType.Alliance)
                    || draft.Terms.Any(x =>
                        KingdomNegotiationTermRules.IsSubjectTerm(x.Type))))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationWarTreatyConflict}A declaration of war cannot be combined with a new treaty or subject relation between the same kingdoms.");
                return false;
            }

            if (Has(draft, KingdomNegotiationTermType.EndAlliance)
                && draft.Terms.Any(x =>
                    x.Type == KingdomNegotiationTermType.JoinWar))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationJoinWarNeedsAlliance}A call to war requires the alliance to remain in force.");
                return false;
            }

            if (draft.Terms.Any(x =>
                    KingdomNegotiationTermRules.IsSubjectTerm(x.Type))
                && draft.Terms.Any(x =>
                    x.Type == KingdomNegotiationTermType.Alliance
                        || x.Type == KingdomNegotiationTermType.Peace
                        || x.Type == KingdomNegotiationTermType.DeclareWar))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationSubjectAllianceConflict}A subject-status term cannot be combined with a separate peace, war or alliance term.");
                return false;
            }

            if (draft.Terms
                    .Where(x => x.Type == KingdomNegotiationTermType.Settlement
                        || x.Type == KingdomNegotiationTermType.PrisonerHero
                        || x.Type == KingdomNegotiationTermType.JoinWar)
                    .GroupBy(x => x.Subject)
                    .Any(x => x.Key == null || x.Count() > 1)
                || draft.Terms
                    .Where(x => x.Type == KingdomNegotiationTermType.Gold)
                    .GroupBy(x => x.ProviderKingdom)
                    .Any(x => x.Key == null || x.Count() > 1))
            {
                reason = new TextObject(
                    "{=MP_KingdomNegotiationDuplicateAsset}The same asset or payment direction cannot appear more than once in a proposal.");
                return false;
            }

            foreach (KingdomNegotiationDraftTerm term in draft.Terms)
            {
                if (!IsParticipant(draft, term.ProviderKingdom))
                {
                    reason = new TextObject(
                        "{=MP_KingdomNegotiationInvalidProvider}A proposal term belongs to a kingdom outside this negotiation.");
                    return false;
                }

                if (term.Amount <= 0)
                {
                    reason = new TextObject(
                        "{=MP_KingdomNegotiationInvalidAmount}A proposal term has an invalid amount.");
                    return false;
                }

                if (!IsTermStillOwned(term))
                {
                    TextObject staleAsset = new TextObject(
                        "{=MP_KingdomNegotiationAssetUnavailable}{TERM} is no longer available to its offering kingdom.");
                    staleAsset.SetTextVariable(
                        "TERM",
                        GetTermName(term));
                    reason = staleAsset;
                    return false;
                }

                if (term.Type == KingdomNegotiationTermType.PrisonerHero
                    && !Services.KingdomNegotiationExecutionService
                        .CanTransferPrisoner(
                            draft.PlayerKingdom,
                            draft.TargetKingdom,
                            term))
                {
                    reason = new TextObject(
                        "{=MP_KingdomNegotiationPrisonerTransferUnavailable}Only a hero belonging to the other negotiating kingdom can be released through this proposal.");
                    return false;
                }

                if (KingdomNegotiationTermRules.IsSubjectTerm(term.Type)
                    && !Services.KingdomNegotiationExecutionService
                        .CanEstablishSubjectRelation(
                            draft.PlayerKingdom,
                            draft.TargetKingdom,
                            term))
                {
                    reason = new TextObject(
                        "{=MP_KingdomNegotiationSubjectUnavailable}The proposed subject relation can no longer be established.");
                    return false;
                }

                if (!TryValidateTreaty(draft, term, out reason))
                {
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

                case KingdomNegotiationTermType.JoinWar:
                    Kingdom enemy = term.Subject as Kingdom;
                    return enemy != null
                        && !enemy.IsEliminated
                        && enemy != term.ProviderKingdom;

                default:
                    return term.Subject == term.ProviderKingdom;
            }
        }

        private static bool TryValidateTreaty(
            KingdomNegotiationDraft draft,
            KingdomNegotiationDraftTerm term,
            out TextObject reason)
        {
            reason = TextObject.GetEmpty();
            Kingdom first = draft.PlayerKingdom;
            Kingdom second = draft.TargetKingdom;
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Peace:
                    if (!first.IsAtWarWith(second))
                    {
                        reason = new TextObject(
                            "{=MP_KingdomNegotiationAlreadyAtPeace}The two kingdoms are already at peace.");
                        return false;
                    }

                    if (draft.Terms.Count == 1)
                    {
                        return new MakePeaceKingdomDecision(
                                first.RulingClan,
                                second)
                            .CanMakeDecision(
                                out reason,
                                false);
                    }

                    return true;

                case KingdomNegotiationTermType.DeclareWar:
                    if (first.IsAtWarWith(second))
                    {
                        reason = new TextObject(
                            "{=MP_KingdomNegotiationAlreadyAtWar}The two kingdoms are already at war.");
                        return false;
                    }

                    Kingdom declaringKingdom = term.ProviderKingdom;
                    Kingdom defendingKingdom = declaringKingdom == first
                        ? second
                        : first;
                    bool canDeclareWar = new DeclareWarDecision(
                            declaringKingdom.RulingClan,
                            defendingKingdom)
                        .IsAllowed();
                    if (!canDeclareWar)
                    {
                        reason = new TextObject(
                            "{=MP_KingdomNegotiationDeclareWarUnavailable}The proposed declaration of war is not currently permitted.");
                    }

                    return canDeclareWar;

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
                        reason = first.IsAtWarWith(second)
                            ? new TextObject(
                                "{=MP_KingdomNegotiationTradeAtWar}Trade agreements cannot be signed while the kingdoms are at war.")
                            : new TextObject(
                                "{=MP_KingdomNegotiationTradeExists}The two kingdoms already have a trade agreement.");
                        return false;
                    }

                    // For a standalone trade proposal, reproduce the native
                    // button/decision check exactly, including its simulated
                    // vote in the queried kingdom. A compound proposal uses
                    // the structural native checks here and applies its own
                    // package-wide foreign evaluation afterwards.
                    return Campaign.Current.Models.TradeAgreementModel
                        .CanMakeTradeAgreement(
                            first,
                            second,
                            draft.Terms.Count == 1,
                            out reason,
                            false);

                case KingdomNegotiationTermType.Alliance:
                    IAllianceCampaignBehavior allianceBehavior =
                        Campaign.Current.GetCampaignBehavior<
                            IAllianceCampaignBehavior>();
                    if (first.IsAtWarWith(second)
                        || allianceBehavior?.IsAllyWithKingdom(
                            first,
                            second) == true)
                    {
                        reason = first.IsAtWarWith(second)
                            ? new TextObject(
                                "{=MP_KingdomNegotiationAllianceAtWar}An alliance cannot be formed while the kingdoms are at war.")
                            : new TextObject(
                                "{=MP_KingdomNegotiationAllianceExists}The two kingdoms are already allied.");
                        return false;
                    }

                    if (draft.Terms.Count == 1)
                    {
                        return new StartAllianceDecision(
                                first.RulingClan,
                                second)
                            .CanMakeDecision(
                                out reason,
                                false);
                    }

                    return true;

                case KingdomNegotiationTermType.EndTradeAgreement:
                    ITradeAgreementsCampaignBehavior existingTrade =
                        Campaign.Current.GetCampaignBehavior<
                            ITradeAgreementsCampaignBehavior>();
                    if (existingTrade?.HasTradeAgreement(
                            first,
                            second,
                            out _) != true)
                    {
                        reason = new TextObject(
                            "{=MP_KingdomNegotiationNoTradeToEnd}The kingdoms have no trade agreement to end.");
                        return false;
                    }

                    return true;

                case KingdomNegotiationTermType.EndAlliance:
                    if (Campaign.Current.GetCampaignBehavior<
                            IAllianceCampaignBehavior>()?
                        .IsAllyWithKingdom(first, second) != true)
                    {
                        reason = new TextObject(
                            "{=MP_KingdomNegotiationNoAllianceToEnd}The kingdoms have no alliance to end.");
                        return false;
                    }

                    return true;

                case KingdomNegotiationTermType.EndSubjectRelation:
                    bool related = KingdomDiplomacyManager.Current?
                            .GetSubjectRelation(first)?.OverlordKingdom
                            == second
                        || KingdomDiplomacyManager.Current?
                            .GetSubjectRelation(second)?.OverlordKingdom
                            == first;
                    if (!related)
                    {
                        reason = new TextObject(
                            "{=MP_KingdomNegotiationNoSubjectToEnd}The kingdoms have no subject agreement to end.");
                        return false;
                    }

                    return true;

                case KingdomNegotiationTermType.JoinWar:
                    Kingdom enemy = term.Subject as Kingdom;
                    Kingdom caller = term.ProviderKingdom == first
                        ? second
                        : first;
                    bool allianceExists = Campaign.Current
                            .GetCampaignBehavior<IAllianceCampaignBehavior>()?
                            .IsAllyWithKingdom(first, second) == true
                        || Has(draft, KingdomNegotiationTermType.Alliance);
                    if (enemy == null
                        || enemy.IsEliminated
                        || !caller.IsAtWarWith(enemy)
                        || term.ProviderKingdom.IsAtWarWith(enemy)
                        || !allianceExists)
                    {
                        reason = new TextObject(
                            "{=MP_KingdomNegotiationJoinWarUnavailable}The requested call to war is no longer valid.");
                        return false;
                    }

                    return true;

                default:
                    return true;
            }
        }

        private static TextObject GetTermName(
            KingdomNegotiationDraftTerm term)
        {
            if (term.Subject is Settlement settlement)
            {
                return settlement.Name;
            }

            if (term.Subject is Hero hero)
            {
                return hero.Name;
            }

            if (term.Subject is Kingdom kingdom)
            {
                return kingdom.Name;
            }

            return new TextObject(
                "{=MP_KingdomNegotiationUnknownTerm}This proposal term");
        }

        private static bool Has(
            KingdomNegotiationDraft draft,
            KingdomNegotiationTermType type)
        {
            return draft.Terms.Any(term => term.Type == type);
        }
    }
}
