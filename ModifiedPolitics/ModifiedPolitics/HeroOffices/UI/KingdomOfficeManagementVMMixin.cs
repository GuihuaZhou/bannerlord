using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.UI
{
    /// <summary>
    /// Provides only navigation and localized text for the offices page.
    /// Office assignments are intentionally not connected in this test stage.
    /// </summary>
    [ViewModelMixin("RefreshValues", true)]
    public sealed class KingdomOfficeManagementVMMixin
        : BaseViewModelMixin<KingdomManagementVM>
    {
        private static readonly ConditionalWeakTable<KingdomManagementVM, KingdomOfficeManagementVMMixin>
            Instances = new ConditionalWeakTable<KingdomManagementVM, KingdomOfficeManagementVMMixin>();

        private readonly KingdomManagementVM _vm;
        private string _officesText;
        private string _officePageTitle;
        private string _officePageTestText;
        private bool _officePageVisible;

        public KingdomOfficeManagementVMMixin(KingdomManagementVM vm)
            : base(vm)
        {
            _vm = vm;
            Instances.Remove(vm);
            Instances.Add(vm, this);
            RefreshText();
        }

        [DataSourceProperty]
        public string OfficesText
        {
            get => _officesText;
            private set
            {
                if (value == _officesText)
                    return;
                _officesText = value;
                _vm.OnPropertyChangedWithValue(value, nameof(OfficesText));
            }
        }

        [DataSourceProperty]
        public string OfficePageTitle
        {
            get => _officePageTitle;
            private set
            {
                if (value == _officePageTitle)
                    return;
                _officePageTitle = value;
                _vm.OnPropertyChangedWithValue(value, nameof(OfficePageTitle));
            }
        }

        [DataSourceProperty]
        public string OfficePageTestText
        {
            get => _officePageTestText;
            private set
            {
                if (value == _officePageTestText)
                    return;
                _officePageTestText = value;
                _vm.OnPropertyChangedWithValue(value, nameof(OfficePageTestText));
            }
        }

        [DataSourceProperty]
        public bool OfficePageVisible
        {
            get => _officePageVisible;
            private set
            {
                if (value == _officePageVisible)
                    return;
                _officePageVisible = value;
                _vm.OnPropertyChangedWithValue(value, nameof(OfficePageVisible));
            }
        }

        public override void OnRefresh()
        {
            RefreshText();
        }

        [DataSourceMethod]
        public void ExecuteShowOffices()
        {
            if (!_vm.PlayerHasKingdom)
                return;

            // Match native category switching without changing its private category fields.
            _vm.Clan.Show = false;
            _vm.Settlement.Show = false;
            _vm.Policy.Show = false;
            _vm.Army.Show = false;
            _vm.Diplomacy.Show = false;
            OfficePageVisible = true;
        }

        private void RefreshText()
        {
            OfficesText = new TextObject("{=MP_KingdomOfficesTab}Offices").ToString();
            OfficePageTitle = new TextObject("{=MP_KingdomOfficesTitle}Kingdom Offices").ToString();
            OfficePageTestText = new TextObject(
                "{=MP_KingdomOfficesPageTest}The kingdom offices page is ready.").ToString();
        }

        internal static void HideOfficePage(KingdomManagementVM vm)
        {
            if (vm != null && Instances.TryGetValue(vm, out KingdomOfficeManagementVMMixin mixin))
                mixin.OfficePageVisible = false;
        }
    }

    /// <summary>
    /// Hides the custom page whenever one of the five native categories is selected.
    /// </summary>
    [HarmonyPatch(typeof(KingdomManagementVM), "SetSelectedCategory")]
    internal static class KingdomOfficeNativeCategoryPatch
    {
        [HarmonyPostfix]
        private static void HideOfficePage(KingdomManagementVM __instance)
        {
            KingdomOfficeManagementVMMixin.HideOfficePage(__instance);
        }
    }
}
