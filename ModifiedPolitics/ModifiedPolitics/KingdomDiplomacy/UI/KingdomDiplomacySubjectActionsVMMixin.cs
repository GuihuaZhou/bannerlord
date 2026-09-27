using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.UI
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
        private MBBindingList<KingdomDiplomacyProposalActionItemVM>
            _subjectActions;

        public KingdomDiplomacySubjectActionsVMMixin(
            KingdomDiplomacyVM vm)
            : base(vm)
        {
            _vm = vm;
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
                    "{=ModifiedPolitics_DemandVassalDescription}Demand that {TARGET} become your vassal. During development, every valid demand is accepted."));

            AddDemandAction(
                playerKingdom,
                target,
                SubjectType.Puppet,
                new TextObject(
                    "{=ModifiedPolitics_DemandPuppet}Demand Submission"),
                new TextObject(
                    "{=ModifiedPolitics_DemandPuppetDescription}Demand that {TARGET} become your puppet. During development, every valid demand is accepted."));

            AddSubmissionAction(
                playerKingdom,
                target,
                SubjectType.Vassal,
                new TextObject(
                    "{=ModifiedPolitics_OfferVassalage}Offer Vassalage"),
                new TextObject(
                    "{=ModifiedPolitics_OfferVassalageDescription}Offer for your kingdom to become a vassal of {TARGET}. During development, every valid offer is accepted."));

            AddSubmissionAction(
                playerKingdom,
                target,
                SubjectType.Puppet,
                new TextObject(
                    "{=ModifiedPolitics_OfferPuppetSubmission}Offer Submission"),
                new TextObject(
                    "{=ModifiedPolitics_OfferPuppetSubmissionDescription}Offer for your kingdom to become a puppet of {TARGET}. During development, every valid offer is accepted."));
        }

        private void AddDemandAction(
            Kingdom overlord,
            Kingdom target,
            SubjectType type,
            TextObject actionName,
            TextObject explanation)
        {
            explanation.SetTextVariable("TARGET", target.Name);

            TextObject disabledReason;
            bool isEnabled = SubjectProposalService
                .CanDemandSubjectRelation(
                    overlord,
                    target,
                    type,
                    out disabledReason);

            SubjectActions.Add(
                CreateAction(
                    actionName,
                    explanation,
                    isEnabled,
                    disabledReason,
                    () => ExecuteDemand(overlord, target, type)));
        }

        private void AddSubmissionAction(
            Kingdom playerKingdom,
            Kingdom target,
            SubjectType type,
            TextObject actionName,
            TextObject explanation)
        {
            explanation.SetTextVariable("TARGET", target.Name);

            TextObject disabledReason;
            bool isEnabled = SubjectProposalService.CanOfferSubmission(
                playerKingdom,
                target,
                type,
                out disabledReason);

            SubjectActions.Add(
                CreateAction(
                    actionName,
                    explanation,
                    isEnabled,
                    disabledReason,
                    () => ExecuteSubmission(
                        playerKingdom,
                        target,
                        type)));
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
                "{=ModifiedPolitics_ReleaseSubjectDescription}End the subject relation with {TARGET} peacefully.");
            explanation.SetTextVariable("TARGET", subject.Name);

            SubjectActions.Add(
                CreateAction(
                    name,
                    explanation,
                    true,
                    TextObject.GetEmpty(),
                    () =>
                    {
                        if (SubjectRelationAction.TryRelease(subject))
                        {
                            RefreshAndSelect(subject);
                        }
                    }));
        }

        private void AddIndependenceAction(
            Kingdom subject,
            Kingdom overlord)
        {
            TextObject explanation = new TextObject(
                "{=ModifiedPolitics_DeclareIndependenceDescription}End the subject relation with {TARGET} and enter a war of independence.");
            explanation.SetTextVariable("TARGET", overlord.Name);

            SubjectActions.Add(
                CreateAction(
                    new TextObject(
                        "{=ModifiedPolitics_DeclareIndependence}Declare Independence"),
                    explanation,
                    true,
                    TextObject.GetEmpty(),
                    () =>
                    {
                        if (SubjectIndependenceAction.TryApply(
                            subject,
                            SubjectIndependenceReason.VoluntaryDeclaration))
                        {
                            RefreshAndSelect(overlord);
                        }
                    }));
        }

        private void ExecuteDemand(
            Kingdom overlord,
            Kingdom target,
            SubjectType type)
        {
            if (SubjectProposalService.DemandSubjectRelation(
                overlord,
                target,
                type))
            {
                RefreshAndSelect(target);
            }
        }

        private void ExecuteSubmission(
            Kingdom playerKingdom,
            Kingdom target,
            SubjectType type)
        {
            if (SubjectProposalService.OfferSubmission(
                playerKingdom,
                target,
                type))
            {
                RefreshAndSelect(target);
            }
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
            bool isEnabled,
            TextObject disabledReason,
            System.Action execute)
        {
            return new KingdomDiplomacyProposalActionItemVM(
                name,
                explanation,
                0,
                isEnabled,
                disabledReason,
                execute);
        }
    }
}
