using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TroopSelection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.Fief.Presentation
{
    [ViewModelMixin("RefreshValues", true)]
    public sealed class FiefManagementTroopSelectionVMMixin
        : BaseViewModelMixin<GameMenuTroopSelectionVM>
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo FullRosterField =
            typeof(GameMenuTroopSelectionVM).GetField("_fullRoster", PrivateInstance);
        private static readonly FieldInfo InitialSelectionsField =
            typeof(GameMenuTroopSelectionVM).GetField("_initialSelections", PrivateInstance);
        private static readonly FieldInfo CanChangeField =
            typeof(GameMenuTroopSelectionVM).GetField(
                "_canChangeChangeStatusOfTroop", PrivateInstance);
        private static readonly MethodInfo InitListMethod =
            typeof(GameMenuTroopSelectionVM).GetMethod("InitList", PrivateInstance);
        private static readonly MethodInfo UpdateMaximumMethod =
            typeof(GameMenuTroopSelectionVM).GetMethod(
                "UpdateMaxSelectableTroopCount", PrivateInstance);

        private readonly GameMenuTroopSelectionVM _vm;
        private readonly FiefManagementPresentationContext _context;
        private MBBindingList<FiefManagementSourceVM> _sources;
        private string _readyText;
        private string _servingText;
        private string _coolingText;
        private bool _readySelected;
        private bool _servingSelected;
        private bool _coolingSelected;

        public FiefManagementTroopSelectionVMMixin(
            GameMenuTroopSelectionVM vm) : base(vm)
        {
            _vm = vm;
            _context = FiefManagementPresentationContext.Claim();
            Sources = new MBBindingList<FiefManagementSourceVM>();
            if (IsFiefManagement)
            {
                BuildSources();
                RefreshManagementView(false);
            }
        }

        [DataSourceProperty]
        public bool IsFiefManagement => _context != null;

        [DataSourceProperty]
        public bool IsNotFiefManagement => _context == null;

        [DataSourceProperty]
        public MBBindingList<FiefManagementSourceVM> Sources
        {
            get => _sources;
            private set
            {
                _sources = value;
                _vm.OnPropertyChangedWithValue(value, nameof(Sources));
            }
        }

        [DataSourceProperty]
        public string SourceTitle => new TextObject(
            "{=ModifiedArmy_FiefManagement_Source}Source").ToString();

        [DataSourceProperty]
        public string ReadyText { get => _readyText; private set => SetText(ref _readyText, value, nameof(ReadyText)); }
        [DataSourceProperty]
        public string ServingText { get => _servingText; private set => SetText(ref _servingText, value, nameof(ServingText)); }
        [DataSourceProperty]
        public string CoolingText { get => _coolingText; private set => SetText(ref _coolingText, value, nameof(CoolingText)); }

        [DataSourceProperty]
        public bool ReadySelected { get => _readySelected; private set => SetBool(ref _readySelected, value, nameof(ReadySelected)); }
        [DataSourceProperty]
        public bool ServingSelected { get => _servingSelected; private set => SetBool(ref _servingSelected, value, nameof(ServingSelected)); }
        [DataSourceProperty]
        public bool CoolingSelected { get => _coolingSelected; private set => SetBool(ref _coolingSelected, value, nameof(CoolingSelected)); }

        public override void OnRefresh()
        {
            if (IsFiefManagement) RefreshLabels();
        }

        [DataSourceMethod]
        public void ExecuteSelectReady() => SelectState(FiefManagementState.Ready);
        [DataSourceMethod]
        public void ExecuteSelectServing() => SelectState(FiefManagementState.Serving);
        [DataSourceMethod]
        public void ExecuteSelectCooling() => SelectState(FiefManagementState.Cooling);

        private void BuildSources()
        {
            if (!_context.Interaction.IsVillage)
            {
                Sources.Add(new FiefManagementSourceVM(null,
                    new TextObject("{=ModifiedArmy_FiefManagement_AllVillages}All bound villages").ToString(),
                    _context.GetSourceCount(_context.State, null), SelectSource));
            }
            foreach (var village in _context.GetVillages())
            {
                Sources.Add(new FiefManagementSourceVM(village,
                    village.Name.ToString(),
                    _context.GetSourceCount(_context.State, village), SelectSource));
            }
            MarkSelectedSource();
        }

        private void SelectSource(FiefManagementSourceVM source)
        {
            _context.SelectVillage(source.Village);
            MarkSelectedSource();
            RefreshManagementView(true);
        }

        private void SelectState(FiefManagementState state)
        {
            if (_context.State == state) return;
            _context.SelectState(state);
            RefreshManagementView(true);
        }

        private void RefreshManagementView(bool rebuildNativeList)
        {
            RefreshLabels();
            foreach (FiefManagementSourceVM source in Sources)
                source.SetCount(_context.GetSourceCount(
                    _context.State, source.Village));
            if (!rebuildNativeList) return;

            _vm.ExecuteClearSelection();
            FullRosterField?.SetValue(_vm, _context.GetFullRoster());
            InitialSelectionsField?.SetValue(
                _vm, TroopRoster.CreateDummyTroopRoster());
            CanChangeField?.SetValue(_vm,
                new Func<CharacterObject, bool>(_context.CanSelect));
            UpdateMaximumMethod?.Invoke(_vm,
                new object[] { _context.GetMaximumSelection() });
            InitListMethod?.Invoke(_vm, null);
        }

        private void RefreshLabels()
        {
            _vm.TitleText = new TextObject(
                "{=ModifiedArmy_FiefManagement_Title}Troop Management").ToString();
            _vm.CurrentSelectedAmountTitle = new TextObject(
                "{=ModifiedArmy_FiefManagement_RecruitedCount}" +
                "Recruited troops").ToString();
            ReadyText = FormatTab("{=ModifiedArmy_FiefManagement_Ready}Ready",
                FiefManagementState.Ready);
            ServingText = FormatTab("{=ModifiedArmy_FiefManagement_Serving}Serving",
                FiefManagementState.Serving);
            CoolingText = FormatTab("{=ModifiedArmy_FiefManagement_Cooling}Returning",
                FiefManagementState.Cooling);
            ReadySelected = _context.State == FiefManagementState.Ready;
            ServingSelected = _context.State == FiefManagementState.Serving;
            CoolingSelected = _context.State == FiefManagementState.Cooling;
        }

        private string FormatTab(string text, FiefManagementState state)
        {
            return new TextObject(text).ToString() + " (" +
                _context.GetCount(state) + ")";
        }

        private void MarkSelectedSource()
        {
            foreach (FiefManagementSourceVM source in Sources)
                source.IsSelected = source.Village == _context.Village;
        }

        private void SetText(ref string field, string value, string name)
        {
            if (field == value) return;
            field = value;
            _vm.OnPropertyChangedWithValue(value, name);
        }

        private void SetBool(ref bool field, bool value, string name)
        {
            if (field == value) return;
            field = value;
            _vm.OnPropertyChangedWithValue(value, name);
        }
    }
}
