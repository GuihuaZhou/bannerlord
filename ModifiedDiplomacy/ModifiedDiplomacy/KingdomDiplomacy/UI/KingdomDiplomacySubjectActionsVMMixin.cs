using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.UI
{
    /// <summary>
    /// Exposes only the combined-negotiation entry. Individual native and
    /// subject actions are assembled as terms inside the negotiation screen.
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
            RefreshNegotiationAction();
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
            RefreshNegotiationAction();
        }

        private void RefreshNegotiationAction()
        {
            SubjectActions.Clear();

            // Keep the relation icons above the button synchronized with the
            // currently selected kingdom.
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

            TextObject explanation = new TextObject(
                "{=MP_KingdomBarterDescription}Assemble a combined diplomatic proposal for {KINGDOM}.");
            explanation.SetTextVariable("KINGDOM", target.Name);

            SubjectActions.Add(
                new KingdomDiplomacyProposalActionItemVM(
                    new TextObject(
                        "{=MP_KingdomBarterAction}Negotiate"),
                    explanation,
                    0,
                    true,
                    TextObject.GetEmpty(),
                    () => KingdomNegotiationScreenService.Open(target)));
        }
    }
}
