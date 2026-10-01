using System;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using ModifiedDiplomacy.KingdomDiplomacy.Decisions;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.UI
{
    /// <summary>
    /// Provides a separate action collection for subject diplomacy. The
    /// prefab renders this collection below Bannerlord's native diplomacy
    /// actions so the two categories never compete for space in one row.
    /// </summary>
    [ViewModelMixin("OnSetCurrentDiplomacyItem", true)]
    public sealed class KingdomDiplomacySubjectActionsVMMixin
        : BaseViewModelMixin<KingdomDiplomacyVM>
    {
        private readonly KingdomDiplomacyVM _vm;
        private readonly Action<KingdomDecision> _forceDecision;
        private MBBindingList<KingdomDiplomacyProposalActionItemVM>
            _subjectActions;

        public KingdomDiplomacySubjectActionsVMMixin(
            KingdomDiplomacyVM vm)
            : base(vm)
        {
            _vm = vm;
            _forceDecision = AccessTools
                .Field(typeof(KingdomDiplomacyVM), "_forceDecision")
                ?.GetValue(vm) as Action<KingdomDecision>;
            _subjectActions =
                new MBBindingList<KingdomDiplomacyProposalActionItemVM>();
            RefreshSubjectActions();
        }

        [DataSourceProperty]
        public MBBindingList<KingdomDiplomacyProposalActionItemVM>
            SubjectActions
        {
            get => _subjectActions;
            set
            {
                if (value == _subjectActions)
                {
                    return;
                }

                _subjectActions = value;
                _vm.OnPropertyChangedWithValue(
                    value,
                    nameof(SubjectActions));
            }
        }

        public override void OnRefresh()
        {
            RefreshSubjectActions();
        }

        private void RefreshSubjectActions()
        {
            SubjectActions.Clear();

            // Native relation-banner collections can retain an obsolete war
            // entry after peace. Refresh the selected item whenever the
            // diplomacy selection is applied so its live relation mixin can
            // rebuild those collections before the panel is rendered.
            _vm?.CurrentSelectedDiplomacyItem?.RefreshValues();

            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            Kingdom target = _vm?.CurrentSelectedDiplomacyItem
                ?.Faction2 as Kingdom;
            if (playerKingdom == null
                || target == null
                || target == playerKingdom)
            {
                return;
            }

            KingdomDecision pendingDecision = playerKingdom
                .UnresolvedDecisions
                .FirstOrDefault(x =>
                    ((x is SubjectProposalKingdomDecision proposal
                        && proposal.TargetKingdom == target)
                    || (x is SubjectReleaseKingdomDecision release
                        && release.SubjectKingdom == target)
                    || (x is SubjectIndependenceKingdomDecision independence
                        && independence.OverlordKingdom == target)
                    || (x is SubjectResponseKingdomDecision response
                        && response.ForeignKingdom == target))
                    && !x.ShouldBeCancelled());
            if (pendingDecision != null)
            {
                AddPendingDecisionAction(pendingDecision);
                return;
            }

            AddNegotiationAction(target);

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectRelationData targetRelation =
                manager?.GetSubjectRelation(target);
            if (targetRelation?.OverlordKingdom == playerKingdom)
            {
                AddReleaseAction(target, targetRelation.Type);
                return;
            }

            SubjectRelationData playerRelation =
                manager?.GetSubjectRelation(playerKingdom);
            if (playerRelation?.OverlordKingdom == target)
            {
                AddIndependenceAction(playerKingdom, target);
                return;
            }

            AddDemandAction(
                playerKingdom,
                target,
                SubjectType.Vassal,
                new TextObject(
                    "{=ModifiedPolitics_DemandVassal}Demand Vassalage"),
                new TextObject(
                    "{=ModifiedPolitics_DemandVassalDescription}Demand that {TARGET} become your vassal. Council support is {SUPPORT}%."));

            AddDemandAction(
                playerKingdom,
                target,
                SubjectType.Puppet,
                new TextObject(
                    "{=ModifiedPolitics_DemandPuppet}Demand Submission"),
                new TextObject(
                    "{=ModifiedPolitics_DemandPuppetDescription}Demand that {TARGET} become your puppet. Council support is {SUPPORT}%."));

            AddSubmissionAction(
                playerKingdom,
                target,
                SubjectType.Vassal,
                new TextObject(
                    "{=ModifiedPolitics_OfferVassalage}Offer Vassalage"),
                new TextObject(
                    "{=ModifiedPolitics_OfferVassalageDescription}Offer for your kingdom to become a vassal of {TARGET}. Council support is {SUPPORT}%."));

            AddSubmissionAction(
                playerKingdom,
                target,
                SubjectType.Puppet,
                new TextObject(
                    "{=ModifiedPolitics_OfferPuppetSubmission}Offer Submission"),
                new TextObject(
                    "{=ModifiedPolitics_OfferPuppetSubmissionDescription}Offer for your kingdom to become a puppet of {TARGET}. Council support is {SUPPORT}%."));
        }

        private void AddNegotiationAction(Kingdom target)
        {
            TextObject explanation = new TextObject(
                "{=MP_KingdomBarterDescription}Assemble a combined diplomatic proposal for {KINGDOM}.");
            explanation.SetTextVariable("KINGDOM", target.Name);
            SubjectActions.Add(CreateAction(
                new TextObject(
                    "{=MP_KingdomBarterAction}Negotiate"),
                explanation,
                0,
                true,
                TextObject.GetEmpty(),
                () => KingdomNegotiationScreenService.Open(target)));
        }

        private void AddDemandAction(
            Kingdom overlord,
            Kingdom target,
            SubjectType type,
            TextObject actionName,
            TextObject explanation)
        {
            explanation.SetTextVariable("TARGET", target.Name);

            SubjectProposalKingdomDecision decision =
                SubjectProposalService.CreateDecision(
                    target,
                    type,
                    false);
            explanation.SetTextVariable(
                "SUPPORT",
                CalculateSupport(decision));

            TextObject disabledReason;
            bool isEnabled = SubjectProposalService
                .CanDemandSubjectRelation(
                    overlord,
                    target,
                    type,
                    out disabledReason);
            if (isEnabled)
            {
                SubjectProposalEvaluation foreignEvaluation =
                    SubjectProposalEvaluationService.EvaluateDemand(
                        overlord,
                        target,
                        type);
                if (!foreignEvaluation.WouldAccept)
                {
                    isEnabled = false;
                    disabledReason = new TextObject(
                        "{=MP_SubjectNegotiationUnavailable}The other kingdom is unwilling to negotiate this subject relation.");
                }
            }
            isEnabled = ApplyDecisionRequirements(
                decision,
                isEnabled,
                ref disabledReason);

            SubjectActions.Add(
                CreateAction(
                    actionName,
                    explanation,
                    decision.GetProposalInfluenceCost(),
                    isEnabled,
                    disabledReason,
                    () => StartDecision(decision)));
        }

        private void AddSubmissionAction(
            Kingdom playerKingdom,
            Kingdom target,
            SubjectType type,
            TextObject actionName,
            TextObject explanation)
        {
            explanation.SetTextVariable("TARGET", target.Name);

            SubjectProposalKingdomDecision decision =
                SubjectProposalService.CreateDecision(
                    target,
                    type,
                    true);
            explanation.SetTextVariable(
                "SUPPORT",
                CalculateSupport(decision));

            TextObject disabledReason;
            bool isEnabled = SubjectProposalService.CanOfferSubmission(
                playerKingdom,
                target,
                type,
                out disabledReason);
            if (isEnabled)
            {
                // A submission offer is still a bilateral agreement. Do not
                // let the player spend influence when the proposed overlord
                // is unwilling to accept the subject kingdom.
                SubjectProposalEvaluation foreignEvaluation =
                    SubjectProposalEvaluationService
                        .EvaluateSubmissionOffer(
                            target,
                            playerKingdom,
                            type);
                if (!foreignEvaluation.WouldAccept)
                {
                    isEnabled = false;
                    disabledReason = new TextObject(
                        "{=MP_SubjectNegotiationUnavailable}The other kingdom is unwilling to negotiate this subject relation.");
                }
            }
            isEnabled = ApplyDecisionRequirements(
                decision,
                isEnabled,
                ref disabledReason);

            SubjectActions.Add(
                CreateAction(
                    actionName,
                    explanation,
                    decision.GetProposalInfluenceCost(),
                    isEnabled,
                    disabledReason,
                    () => StartDecision(decision)));
        }

        private void AddReleaseAction(
            Kingdom subject,
            SubjectType type)
        {
            TextObject name = type == SubjectType.Puppet
                ? new TextObject(
                    "{=ModifiedPolitics_ReleasePuppet}Release Puppet")
                : new TextObject(
                    "{=ModifiedPolitics_ReleaseVassal}Release Vassal");
            TextObject explanation = new TextObject(
                "{=ModifiedPolitics_ReleaseSubjectDescription}End the subject relation with {TARGET} peacefully. Council support is {SUPPORT}%.");
            explanation.SetTextVariable("TARGET", subject.Name);

            // Releasing a subject is unilateral, but it is still a kingdom
            // policy decision. It therefore skips foreign consent and goes
            // directly to the overlord's council.
            SubjectReleaseKingdomDecision decision =
                new SubjectReleaseKingdomDecision(
                    Clan.PlayerClan,
                    subject,
                    type);
            explanation.SetTextVariable(
                "SUPPORT",
                CalculateSupport(decision));

            TextObject disabledReason = TextObject.GetEmpty();
            bool isEnabled = ApplyDecisionRequirements(
                decision,
                true,
                ref disabledReason);

            SubjectActions.Add(
                CreateAction(
                    name,
                    explanation,
                    decision.GetProposalInfluenceCost(),
                    isEnabled,
                    disabledReason,
                    () => StartDecision(decision)));
        }

        private void AddIndependenceAction(
            Kingdom subject,
            Kingdom overlord)
        {
            TextObject explanation = new TextObject(
                "{=ModifiedPolitics_DeclareIndependenceDescription}End the subject relation with {TARGET} and enter a war of independence. Council support is {SUPPORT}%.");
            explanation.SetTextVariable("TARGET", overlord.Name);

            SubjectRelationData relation = KingdomDiplomacyManager.Current
                ?.GetSubjectRelation(subject);
            SubjectIndependenceKingdomDecision decision =
                new SubjectIndependenceKingdomDecision(
                    Clan.PlayerClan,
                    overlord,
                    relation?.Type ?? SubjectType.Vassal);
            explanation.SetTextVariable(
                "SUPPORT",
                CalculateSupport(decision));

            TextObject disabledReason = TextObject.GetEmpty();
            bool isEnabled = ApplyDecisionRequirements(
                decision,
                decision.IsAllowed(),
                ref disabledReason);

            SubjectActions.Add(
                CreateAction(
                    new TextObject(
                        "{=ModifiedPolitics_DeclareIndependence}Declare Independence"),
                    explanation,
                    decision.GetProposalInfluenceCost(),
                    isEnabled,
                    disabledReason,
                    () => StartDecision(decision)));
        }

        private void StartDecision(KingdomDecision decision)
        {
            if (decision is SubjectProposalKingdomDecision proposal)
            {
                proposal.LogInitialCouncilEvaluation();
            }

            decision.Kingdom.AddDecision(decision, false);
            _forceDecision?.Invoke(decision);
        }

        private static int CalculateSupport(KingdomDecision decision)
        {
            return MathF.Round(
                new KingdomElection(decision)
                    .GetLikelihoodForSponsor(Clan.PlayerClan)
                * 100f);
        }

        private void AddPendingDecisionAction(KingdomDecision decision)
        {
            SubjectActions.Add(
                CreateAction(
                    GameTexts.FindText("str_resolve", null),
                    GameTexts.FindText("str_resolve_explanation", null),
                    0,
                    _forceDecision != null,
                    TextObject.GetEmpty(),
                    () => _forceDecision?.Invoke(decision)));
        }

        private static bool ApplyDecisionRequirements(
            KingdomDecision decision,
            bool isEnabled,
            ref TextObject disabledReason)
        {
            if (!isEnabled)
            {
                return false;
            }

            if (Clan.PlayerClan.Influence
                < decision.GetProposalInfluenceCost())
            {
                disabledReason = GameTexts.FindText(
                    "str_warning_you_dont_have_enough_influence",
                    null);
                return false;
            }

            return true;
        }

        private void RefreshAndSelect(Kingdom target)
        {
            // Subject actions may make peace or declare war, moving the target
            // between Bannerlord's war and truce lists.
            _vm.RefreshDiplomacyList();
            _vm.SelectKingdom(target);
        }

        private static KingdomDiplomacyProposalActionItemVM CreateAction(
            TextObject name,
            TextObject explanation,
            int influenceCost,
            bool isEnabled,
            TextObject disabledReason,
            System.Action execute)
        {
            return new KingdomDiplomacyProposalActionItemVM(
                name,
                explanation,
                influenceCost,
                isEnabled,
                disabledReason,
                execute);
        }
    }
}
