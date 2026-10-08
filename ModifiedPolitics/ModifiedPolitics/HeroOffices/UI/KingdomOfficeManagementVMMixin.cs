using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;
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
        private MBBindingList<KingdomOfficeItemVM> _offices;
        private KingdomOfficeItemVM _currentOffice;
        private bool _hasOfficeSelection;

        public KingdomOfficeManagementVMMixin(KingdomManagementVM vm)
            : base(vm)
        {
            _vm = vm;
            Instances.Remove(vm);
            Instances.Add(vm, this);
            _offices = new MBBindingList<KingdomOfficeItemVM>();
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

        [DataSourceProperty]
        public MBBindingList<KingdomOfficeItemVM> Offices
        {
            get => _offices;
            private set
            {
                if (value == _offices)
                    return;

                _offices = value;
                _vm.OnPropertyChangedWithValue(value, nameof(Offices));
            }
        }

        [DataSourceProperty]
        public KingdomOfficeItemVM CurrentOffice
        {
            get => _currentOffice;
            private set
            {
                if (value == _currentOffice)
                    return;

                _currentOffice = value;
                _vm.OnPropertyChangedWithValue(value, nameof(CurrentOffice));
            }
        }

        [DataSourceProperty]
        public bool HasOfficeSelection
        {
            get => _hasOfficeSelection;
            private set
            {
                if (value == _hasOfficeSelection)
                    return;

                _hasOfficeSelection = value;
                _vm.OnPropertyChangedWithValue(value, nameof(HasOfficeSelection));
            }
        }

        public override void OnRefresh()
        {
            RefreshText();
            RefreshOfficeList();
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
            RefreshOfficeList();
            OfficePageVisible = true;
        }

        private void RefreshText()
        {
            OfficesText = new TextObject("{=MP_KingdomOfficesTab}Offices").ToString();
            OfficePageTitle = new TextObject("{=MP_KingdomOfficesTitle}Kingdom Offices").ToString();
            OfficePageTestText = new TextObject(
                "{=MP_KingdomOfficesPageTest}The kingdom offices page is ready.").ToString();
        }

        private void RefreshOfficeList()
        {
            OfficeType? selectedType = CurrentOffice?.OfficeType;
            var items = new MBBindingList<KingdomOfficeItemVM>();
            Kingdom kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom == null || kingdom.IsEliminated
                || !OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config))
            {
                Offices = items;
                SelectOffice(null);
                return;
            }

            foreach (OfficeType officeType in Enum.GetValues(typeof(OfficeType)))
            {
                if (!config.IsEnabled(officeType))
                    continue;

                IEnumerable<OfficeAssignment> officeAssignments =
                    HeroOfficeBehavior.Current?.GetAssignments(kingdom, officeType)
                    ?? Enumerable.Empty<OfficeAssignment>();
                List<OfficeAssignment> assignments = officeAssignments
                    .Where(assignment => assignment?.Hero != null)
                    .ToList();
                int limit = OfficeRules.GetOfficeLimit(kingdom, officeType);
                items.Add(CreateOfficeItem(officeType, assignments, limit));
            }

            Offices = items;
            KingdomOfficeItemVM selected = selectedType.HasValue
                ? items.FirstOrDefault(item => item.OfficeType == selectedType.Value)
                : null;
            SelectOffice(selected ?? items.FirstOrDefault());
        }

        private KingdomOfficeItemVM CreateOfficeItem(
            OfficeType officeType,
            IReadOnlyCollection<OfficeAssignment> assignments,
            int limit)
        {
            TextObject seatText = new TextObject("{=MP_OfficeSeatCount}{USED}/{TOTAL}");
            seatText.SetTextVariable("USED", assignments.Count);
            seatText.SetTextVariable("TOTAL", limit);

            return new KingdomOfficeItemVM(
                officeType,
                OfficeText.GetName(officeType).ToString(),
                seatText.ToString(),
                GetOfficeDescription(officeType).ToString(),
                GetOfficeEffects(officeType).ToString(),
                FormatHolders(assignments),
                SelectOffice);
        }

        private void SelectOffice(KingdomOfficeItemVM office)
        {
            foreach (KingdomOfficeItemVM item in Offices)
                item.IsSelected = item == office;

            CurrentOffice = office;
            HasOfficeSelection = office != null;
        }

        private static string FormatHolders(IEnumerable<OfficeAssignment> assignments)
        {
            List<string> holders = assignments.Select(assignment =>
            {
                string clanName = assignment.Hero.Clan?.Name?.ToString() ?? string.Empty;
                if (!OfficeRules.IsLocal(assignment.OfficeType))
                    return assignment.Hero.Name + "  ·  " + clanName;

                string settlementName = assignment.Hero.GovernorOf?.Settlement?.Name?.ToString()
                                        ?? string.Empty;
                return assignment.Hero.Name + "  ·  " + clanName + "  ·  " + settlementName;
            }).ToList();

            return holders.Count == 0
                ? new TextObject("{=MP_OfficeNoHolders}There are currently no office holders.").ToString()
                : string.Join("\n", holders);
        }

        private static TextObject GetOfficeDescription(OfficeType officeType)
        {
            switch (officeType)
            {
                case OfficeType.Marshal:
                    return new TextObject("{=MP_OfficeMarshalDescription}The kingdom's supreme military office, responsible for leading and assembling armies.");
                case OfficeType.ChiefMinister:
                    return new TextObject("{=MP_OfficeChiefMinisterDescription}The senior civil office coordinating the kingdom's local administration.");
                case OfficeType.CourtSteward:
                    return new TextObject("{=MP_OfficeCourtStewardDescription}The court office responsible for easing political disputes within the kingdom.");
                case OfficeType.TaxOfficer:
                    return new TextObject("{=MP_OfficeTaxOfficerDescription}A local office assigned to a town governor to oversee taxation.");
                case OfficeType.AgricultureOfficer:
                    return new TextObject("{=MP_OfficeAgricultureOfficerDescription}A local office assigned to a town governor to oversee food production.");
                case OfficeType.MilitaryOfficer:
                    return new TextObject("{=MP_OfficeMilitaryOfficerDescription}A local office assigned to a castle governor to organize regular troops and garrison recruitment.");
                case OfficeType.SecurityOfficer:
                    return new TextObject("{=MP_OfficeSecurityOfficerDescription}A local office assigned to a town governor to maintain loyalty and public order.");
                default:
                    return TextObject.GetEmpty();
            }
        }

        private static TextObject GetOfficeEffects(OfficeType officeType)
        {
            switch (officeType)
            {
                case OfficeType.Marshal:
                    return new TextObject("{=MP_OfficeMarshalEffects}• Army call influence cost -25%\n• Army influence maintenance cost -20%\n• Holder's clan influence +5 per day");
                case OfficeType.ChiefMinister:
                    return new TextObject("{=MP_OfficeChiefMinisterEffects}• All local office effects +10%\n• Holder's clan influence +5 per day");
                case OfficeType.CourtSteward:
                    return new TextObject("{=MP_OfficeCourtStewardEffects}• Negative clan relation changes from kingdom votes -25%\n• Holder's clan influence +5 per day");
                case OfficeType.TaxOfficer:
                    return new TextObject("{=MP_OfficeTaxOfficerEffects}• Governed settlement tax income +20%\n• Holder's clan influence +1 per day");
                case OfficeType.AgricultureOfficer:
                    return new TextObject("{=MP_OfficeAgricultureOfficerEffects}• Governed settlement food production +20%\n• Holder's clan influence +1 per day");
                case OfficeType.MilitaryOfficer:
                    return new TextObject("{=MP_OfficeMilitaryOfficerEffects}• Regular troop generation speed +25%\n• Garrison recruitment speed +50%\n• Holder's clan influence +1 per day");
                case OfficeType.SecurityOfficer:
                    return new TextObject("{=MP_OfficeSecurityOfficerEffects}• Governed settlement loyalty +2 per day\n• Holder's clan influence +1 per day");
                default:
                    return TextObject.GetEmpty();
            }
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
