using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.UI
{
    /// <summary>
    /// Splits Bannerlord's native peace entries into ordinary kingdoms and
    /// direct subject-relation categories. The same item instances are reused,
    /// preserving native selection callbacks and diplomacy details.
    /// </summary>
    [ViewModelMixin("RefreshDiplomacyList", true)]
    public sealed class KingdomDiplomacyCategoryVMMixin
        : BaseViewModelMixin<KingdomDiplomacyVM>
    {
        private readonly KingdomDiplomacyVM _vm;

        public KingdomDiplomacyCategoryVMMixin(KingdomDiplomacyVM vm)
            : base(vm)
        {
            _vm = vm;
            OverlordKingdoms = new MBBindingList<KingdomTruceItemVM>();
            VassalKingdoms = new MBBindingList<KingdomTruceItemVM>();
            PuppetKingdoms = new MBBindingList<KingdomTruceItemVM>();
            OrdinaryTruces = new MBBindingList<KingdomTruceItemVM>();
            RefreshCategories();
        }

        [DataSourceProperty]
        public MBBindingList<KingdomTruceItemVM> OverlordKingdoms { get; }

        [DataSourceProperty]
        public MBBindingList<KingdomTruceItemVM> VassalKingdoms { get; }

        [DataSourceProperty]
        public MBBindingList<KingdomTruceItemVM> PuppetKingdoms { get; }

        [DataSourceProperty]
        public MBBindingList<KingdomTruceItemVM> OrdinaryTruces { get; }

        [DataSourceProperty]
        public string OverlordText =>
            new TextObject(
                "{=ModifiedPolitics_DiplomacyCategoryOverlord}Overlord")
                .ToString();

        [DataSourceProperty]
        public string VassalsText =>
            new TextObject(
                "{=ModifiedPolitics_DiplomacyCategoryVassals}Vassals")
                .ToString();

        [DataSourceProperty]
        public string PuppetsText =>
            new TextObject(
                "{=ModifiedPolitics_DiplomacyCategoryPuppets}Puppets")
                .ToString();

        [DataSourceProperty]
        public string NumOfOverlordsText => FormatCount(OverlordKingdoms.Count);

        [DataSourceProperty]
        public string NumOfVassalsText => FormatCount(VassalKingdoms.Count);

        [DataSourceProperty]
        public string NumOfPuppetsText => FormatCount(PuppetKingdoms.Count);

        public override void OnRefresh()
        {
            RefreshCategories();
        }

        private void RefreshCategories()
        {
            OverlordKingdoms.Clear();
            VassalKingdoms.Clear();
            PuppetKingdoms.Clear();
            OrdinaryTruces.Clear();

            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (playerKingdom == null || manager == null)
            {
                CopyAllNativeTruces();
                NotifyCategoryChanges();
                return;
            }

            Kingdom overlord = manager.GetOverlord(playerKingdom);
            var vassals = new HashSet<Kingdom>();
            var puppets = new HashSet<Kingdom>();
            foreach (SubjectRelationData relation in
                manager.GetSubjects(playerKingdom))
            {
                if (relation?.SubjectKingdom == null)
                {
                    continue;
                }

                if (relation.Type == SubjectType.Puppet)
                {
                    puppets.Add(relation.SubjectKingdom);
                }
                else if (relation.Type == SubjectType.Vassal)
                {
                    vassals.Add(relation.SubjectKingdom);
                }
            }

            foreach (KingdomTruceItemVM item in _vm.PlayerTruces)
            {
                Kingdom target = item?.Faction2 as Kingdom;
                if (target == overlord)
                {
                    OverlordKingdoms.Add(item);
                }
                else if (target != null && vassals.Contains(target))
                {
                    VassalKingdoms.Add(item);
                }
                else if (target != null && puppets.Contains(target))
                {
                    PuppetKingdoms.Add(item);
                }
                else
                {
                    OrdinaryTruces.Add(item);
                }
            }

            NotifyCategoryChanges();
        }

        private void CopyAllNativeTruces()
        {
            foreach (KingdomTruceItemVM item in _vm.PlayerTruces)
            {
                OrdinaryTruces.Add(item);
            }
        }

        private void NotifyCategoryChanges()
        {
            _vm.OnPropertyChanged(nameof(OverlordKingdoms));
            _vm.OnPropertyChanged(nameof(VassalKingdoms));
            _vm.OnPropertyChanged(nameof(PuppetKingdoms));
            _vm.OnPropertyChanged(nameof(OrdinaryTruces));
            _vm.OnPropertyChanged(nameof(OverlordText));
            _vm.OnPropertyChanged(nameof(VassalsText));
            _vm.OnPropertyChanged(nameof(PuppetsText));
            _vm.OnPropertyChanged(nameof(NumOfOverlordsText));
            _vm.OnPropertyChanged(nameof(NumOfVassalsText));
            _vm.OnPropertyChanged(nameof(NumOfPuppetsText));
        }

        private static string FormatCount(int count)
        {
            return "(" + count + ")";
        }
    }
}
