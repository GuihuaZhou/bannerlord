using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedArmy.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Presentation
{
    [ViewModelMixin("RefreshValues", true)]
    public sealed class ClanPartyPrisonerRecruitmentVMMixin
        : BaseViewModelMixin<ClanPartyItemVM>
    {
        private readonly ClanPartyItemVM _vm;
        private readonly HintViewModel _prisonerRecruitmentHint;

        public ClanPartyPrisonerRecruitmentVMMixin(ClanPartyItemVM vm)
            : base(vm)
        {
            _vm = vm;
            _prisonerRecruitmentHint = new HintViewModel(new TextObject(
                "{=ModifiedArmy_PrisonerRecruitment_Hint}" +
                "Enable automatic recruitment of eligible prisoners " +
                "into this garrison."));
        }

        [DataSourceProperty]
        public bool IsPrisonerRecruitmentVisible =>
            GetGarrisonSettlement() != null;

        [DataSourceProperty]
        public bool PrisonerRecruitmentValue
        {
            get
            {
                Settlement settlement = GetGarrisonSettlement();
                GarrisonRecruitFromPrisonersBehavior behavior =
                    GetBehavior();
                return settlement != null &&
                    (behavior?.IsPrisonerRecruitmentEnabled(settlement) ??
                        true);
            }
        }

        [DataSourceProperty]
        public string PrisonerRecruitmentText => new TextObject(
            "{=ModifiedArmy_PrisonerRecruitment_Text}" +
            "Recruit prisoners").ToString();

        [DataSourceProperty]
        public HintViewModel PrisonerRecruitmentHint =>
            _prisonerRecruitmentHint;

        [DataSourceMethod]
        public void ExecuteTogglePrisonerRecruitment()
        {
            Settlement settlement = GetGarrisonSettlement();
            GarrisonRecruitFromPrisonersBehavior behavior =
                GetBehavior();
            if (settlement == null || behavior == null ||
                !_vm.CanUseActions)
            {
                return;
            }

            bool enabled = !behavior.IsPrisonerRecruitmentEnabled(
                settlement);
            behavior.SetPrisonerRecruitmentEnabled(settlement, enabled);
            _vm.OnPropertyChangedWithValue(
                enabled,
                nameof(PrisonerRecruitmentValue));
        }

        public override void OnRefresh()
        {
            _vm.OnPropertyChangedWithValue(
                IsPrisonerRecruitmentVisible,
                nameof(IsPrisonerRecruitmentVisible));
            _vm.OnPropertyChangedWithValue(
                PrisonerRecruitmentValue,
                nameof(PrisonerRecruitmentValue));
        }

        private Settlement GetGarrisonSettlement()
        {
            var party = _vm.Party?.MobileParty;
            if (party?.IsGarrison != true)
            {
                return null;
            }

            Settlement settlement = party.CurrentSettlement ??
                party.HomeSettlement;
            return settlement?.IsFortification == true &&
                settlement.Town?.GarrisonParty == party
                    ? settlement
                    : null;
        }

        private static GarrisonRecruitFromPrisonersBehavior GetBehavior() =>
            Campaign.Current?.GetCampaignBehavior<
                GarrisonRecruitFromPrisonersBehavior>();
    }
}
