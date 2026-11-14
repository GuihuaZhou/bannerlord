// Presents and edits the authoritative per-settlement tax policy in the native
// town-management screen. The campaign behavior remains the only state owner;
// this mixin forwards explicit player choices and refreshes native stat rows.
using System;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using ModifiedPolitics.SettlementEconomy.Behaviors;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.SettlementEconomy.Presentation
{
    /// <summary>
    /// Adds the fifth top-row management column without duplicating tax state.
    /// Player authority is rechecked by the behavior for every selection.
    /// </summary>
    [ViewModelMixin("RefreshValues", true)]
    public sealed class TownManagementTaxPolicyVMMixin
        : BaseViewModelMixin<TownManagementVM>
    {
        private static readonly MethodInfo RefreshNativeStatsMethod = AccessTools.Method(
            typeof(TownManagementVM), "RefreshTownManagementStats");

        private readonly TownManagementVM _vm;
        private readonly Settlement _settlement;
        private readonly SelectorVM<SelectorItemVM> _selector;
        private bool _isSynchronizing;
        private bool _canEdit;
        private string _title;

        public TownManagementTaxPolicyVMMixin(TownManagementVM vm) : base(vm)
        {
            _vm = vm;
            _settlement = Settlement.CurrentSettlement;
            // SelectorVM must start at an index valid for its then-empty item
            // list. The authoritative level is assigned only after options exist.
            _selector = new SelectorVM<SelectorItemVM>(0, OnSelectionChanged);
            AddOptions();
            RefreshPresentation();
        }

        [DataSourceProperty]
        public SelectorVM<SelectorItemVM> TownTaxPolicySelector => _selector;

        [DataSourceProperty]
        public bool CanEditTownTaxPolicy
        {
            get => _canEdit;
            private set
            {
                if (_canEdit == value) return;
                _canEdit = value;
                _vm.OnPropertyChangedWithValue(value, nameof(CanEditTownTaxPolicy));
            }
        }

        [DataSourceProperty]
        public string TownTaxPolicyTitle
        {
            get => _title;
            private set => SetText(ref _title, value, nameof(TownTaxPolicyTitle));
        }

        /// <summary>Synchronizes the fifth column whenever the native VM refreshes.</summary>
        public override void OnRefresh() => RefreshPresentation();

        /// <summary>Adds selector entries in the stable persisted enum order.</summary>
        private void AddOptions()
        {
            _selector.AddItem(CreateOption("MP_TaxLevelVeryLow", "Very Low"));
            _selector.AddItem(CreateOption("MP_TaxLevelLow", "Low"));
            _selector.AddItem(CreateOption("MP_TaxLevelNormal", "Normal"));
            _selector.AddItem(CreateOption("MP_TaxLevelHigh", "High"));
            _selector.AddItem(CreateOption("MP_TaxLevelVeryHigh", "Very High"));
        }

        /// <summary>Reads the authoritative level, effects, and current edit authority.</summary>
        private void RefreshPresentation()
        {
            ISettlementTaxPolicyService service = SettlementTaxPolicyRegistry.Current;
            SettlementTaxLevel level = service?.GetLevel(_settlement) ?? SettlementTaxLevel.Normal;
            _isSynchronizing = true;
            _selector.SelectedIndex = (int)level;
            _isSynchronizing = false;

            CanEditTownTaxPolicy = service != null &&
                _settlement?.IsFortification == true &&
                _settlement.OwnerClan == Clan.PlayerClan;
            TownTaxPolicyTitle = Localize("MP_TaxPolicyShort", "Tax Rate");
        }

        /// <summary>
        /// Applies one explicit player choice, then refreshes both the custom
        /// column and the native tax/loyalty rows from their existing models.
        /// </summary>
        private void OnSelectionChanged(SelectorVM<SelectorItemVM> selector)
        {
            if (_isSynchronizing) return;
            ISettlementTaxPolicyService service = SettlementTaxPolicyRegistry.Current;
            if (!CanEditTownTaxPolicy || service == null ||
                !service.TrySetLevel(_settlement,
                    (SettlementTaxLevel)selector.SelectedIndex, true, out _))
            {
                RefreshPresentation();
                return;
            }

            RefreshPresentation();
            try
            {
                RefreshNativeStatsMethod?.Invoke(_vm, null);
            }
            catch (Exception exception)
            {
                // The policy change is already authoritative; a UI refresh
                // failure must not repeat or roll back that campaign mutation.
                ModLogger.FileError(
                    "[TaxPolicy] Town-management native stat refresh failed after " +
                    "a player policy change: " + exception);
                _vm.RefreshValues();
            }
        }

        /// <summary>Creates one localized selector entry without redundant tooltip text.</summary>
        private static SelectorItemVM CreateOption(string id, string english) =>
            new SelectorItemVM(new TextObject("{=" + id + "}" + english));

        /// <summary>Resolves an English base string through the active language pack.</summary>
        private static string Localize(string id, string english) =>
            new TextObject("{=" + id + "}" + english).ToString();

        /// <summary>Raises mixin property changes on the native host VM.</summary>
        private void SetText(ref string field, string value, string propertyName)
        {
            if (field == value) return;
            field = value;
            _vm.OnPropertyChangedWithValue(value, propertyName);
        }
    }
}
