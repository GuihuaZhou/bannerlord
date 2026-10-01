using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.UI
{
    /// <summary>
    /// Exposes direct subject relations for the selected kingdom and rebuilds
    /// its native war-banner cache from live faction state. Rebuilding avoids
    /// stale war icons after a peace action changes diplomacy in place.
    /// </summary>
    [ViewModelMixin("RefreshValues", true)]
    public sealed class KingdomDiplomacyItemRelationsVMMixin
        : BaseViewModelMixin<KingdomDiplomacyItemVM>
    {
        private readonly KingdomDiplomacyItemVM _vm;

        public KingdomDiplomacyItemRelationsVMMixin(
            KingdomDiplomacyItemVM vm)
            : base(vm)
        {
            _vm = vm;
            Faction2Overlords =
                new MBBindingList<KingdomDiplomacyFactionItemVM>();
            Faction2Vassals =
                new MBBindingList<KingdomDiplomacyFactionItemVM>();
            Faction2Puppets =
                new MBBindingList<KingdomDiplomacyFactionItemVM>();
            RefreshRelations();
        }

        [DataSourceProperty]
        public MBBindingList<KingdomDiplomacyFactionItemVM>
            Faction2Overlords { get; }

        [DataSourceProperty]
        public MBBindingList<KingdomDiplomacyFactionItemVM>
            Faction2Vassals { get; }

        [DataSourceProperty]
        public MBBindingList<KingdomDiplomacyFactionItemVM>
            Faction2Puppets { get; }

        [DataSourceProperty]
        public bool IsFaction2OverlordsVisible =>
            Faction2Overlords.Count > 0;

        [DataSourceProperty]
        public bool IsFaction2VassalsVisible =>
            Faction2Vassals.Count > 0;

        [DataSourceProperty]
        public bool IsFaction2PuppetsVisible =>
            Faction2Puppets.Count > 0;

        /// <summary>
        /// Uses the rebuilt live collection instead of Bannerlord's cached
        /// visibility flag, which can remain true after the final war ends.
        /// </summary>
        [DataSourceProperty]
        public bool IsLiveFaction2OtherWarsVisible =>
            _vm.Faction2OtherWars != null
            && _vm.Faction2OtherWars.Count > 0;

        [DataSourceProperty]
        public string OverlordShortText =>
            new TextObject(
                "{=ModifiedPolitics_DiplomacyRelationOverlordShort}O")
                .ToString();

        [DataSourceProperty]
        public string VassalShortText =>
            new TextObject(
                "{=ModifiedPolitics_DiplomacyRelationVassalShort}V")
                .ToString();

        [DataSourceProperty]
        public string PuppetShortText =>
            new TextObject(
                "{=ModifiedPolitics_DiplomacyRelationPuppetShort}P")
                .ToString();

        public override void OnRefresh()
        {
            RefreshRelations();
        }

        private void RefreshRelations()
        {
            RebuildCurrentWars();
            Faction2Overlords.Clear();
            Faction2Vassals.Clear();
            Faction2Puppets.Clear();

            Kingdom selectedKingdom = _vm.Faction2 as Kingdom;
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (selectedKingdom != null && manager != null)
            {
                Kingdom overlord = manager.GetOverlord(selectedKingdom);
                if (overlord != null && !overlord.IsEliminated)
                {
                    Faction2Overlords.Add(
                        new KingdomDiplomacyFactionItemVM(overlord));
                }

                foreach (SubjectRelationData relation in
                    manager.GetSubjects(selectedKingdom))
                {
                    Kingdom subject = relation?.SubjectKingdom;
                    if (subject == null || subject.IsEliminated)
                    {
                        continue;
                    }

                    if (relation.Type == SubjectType.Puppet)
                    {
                        Faction2Puppets.Add(
                            new KingdomDiplomacyFactionItemVM(subject));
                    }
                    else if (relation.Type == SubjectType.Vassal)
                    {
                        Faction2Vassals.Add(
                            new KingdomDiplomacyFactionItemVM(subject));
                    }
                }
            }

            NotifyRelationChanges();
        }

        private void RebuildCurrentWars()
        {
            Kingdom selectedKingdom = _vm.Faction2 as Kingdom;
            if (selectedKingdom == null || _vm.Faction2OtherWars == null)
            {
                return;
            }

            _vm.Faction2OtherWars.Clear();
            foreach (Kingdom other in Kingdom.All)
            {
                if (other == null
                    || other == selectedKingdom
                    || other == _vm.Faction1
                    || other.IsEliminated
                    || !FactionManager.IsAtWarAgainstFaction(
                        selectedKingdom,
                        other))
                {
                    continue;
                }

                _vm.Faction2OtherWars.Add(
                    new KingdomDiplomacyFactionItemVM(other));
            }

            _vm.OnPropertyChanged(nameof(_vm.Faction2OtherWars));
            _vm.OnPropertyChanged(nameof(_vm.IsFaction2OtherWarsVisible));
            _vm.OnPropertyChanged(nameof(IsLiveFaction2OtherWarsVisible));
        }

        private void NotifyRelationChanges()
        {
            _vm.OnPropertyChanged(nameof(Faction2Overlords));
            _vm.OnPropertyChanged(nameof(Faction2Vassals));
            _vm.OnPropertyChanged(nameof(Faction2Puppets));
            _vm.OnPropertyChanged(nameof(IsFaction2OverlordsVisible));
            _vm.OnPropertyChanged(nameof(IsFaction2VassalsVisible));
            _vm.OnPropertyChanged(nameof(IsFaction2PuppetsVisible));
            _vm.OnPropertyChanged(nameof(IsLiveFaction2OtherWarsVisible));
            _vm.OnPropertyChanged(nameof(OverlordShortText));
            _vm.OnPropertyChanged(nameof(VassalShortText));
            _vm.OnPropertyChanged(nameof(PuppetShortText));
        }
    }
}
