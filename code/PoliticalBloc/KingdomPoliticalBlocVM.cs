using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.PoliticalBloc
{
    public class KingdomPoliticalBlocVM : KingdomCategoryVM
    {
        public KingdomPoliticalBlocVM()
        {

        }


        public class PoliticalBlocVM : ViewModel
        {
            private MBBindingList<KingdomPoliticalBlocItemVM> _availableClans;
            private KingdomPoliticalBlocItemVM _currentSelectedClan;

            public MBBindingList<KingdomPoliticalBlocItemVM> AvailableClans
            {
                get => _availableClans;
                set
                {
                    _availableClans = value;
                    OnPropertyChangedWithValue(value, "AvailableClans");
                }
            }

            [DataSourceProperty]
            public KingdomPoliticalBlocItemVM CurrentSelectedClan
            {
                get => _currentSelectedClan;
                set
                {
                    if (_currentSelectedClan != value)
                    {
                        _currentSelectedClan = value;
                        //_currentSelectedClan?.OnSelect(); // Trigger IsSelected = true
                        OnPropertyChangedWithValue(value, "CurrentSelectedClan");
                        UpdateActionStates();
                    }
                }
            }

            [DataSourceProperty] public string InviteText { get; private set; } = "Invite to Bloc";
            [DataSourceProperty] public string RequestSupportText { get; private set; } = "Request Support";
            [DataSourceProperty] public string MembersText { get; private set; } = "Members";
            [DataSourceProperty] public string InfluenceText { get; private set; } = "Influence";
            [DataSourceProperty] public string NoItemSelectedText { get; private set; } = "Select a clan";

            [DataSourceProperty] public int InviteCost { get; private set; } = 50;
            [DataSourceProperty] public int SupportCost { get; private set; } = 30;

            [DataSourceProperty] public bool CanInviteToBloc { get; private set; }
            [DataSourceProperty] public bool CanRequestSupport { get; private set; }

            [DataSourceProperty] public bool IsAcceptableItemSelected => CurrentSelectedClan != null;

            [DataSourceProperty] public HintViewModel InviteHint { get; } = new HintViewModel();
            [DataSourceProperty] public HintViewModel RequestSupportHint { get; } = new HintViewModel();

            public PoliticalBlocVM()
            {
                RefreshValues();
                RefreshClanList();
            }

            public override void RefreshValues()
            {
                base.RefreshValues();
                // 可替换为 GameTexts.FindText(...) 支持本地化
                InviteText = "Invite to Bloc";
                RequestSupportText = "Request Support";
                MembersText = "Members";
                InfluenceText = "Influence";
                NoItemSelectedText = "Select a clan to interact";
            }

            private void RefreshClanList()
            {
                AvailableClans = new MBBindingList<KingdomPoliticalBlocItemVM>();
                var kingdom = Clan.PlayerClan?.Kingdom;
                if (kingdom == null) return;

                foreach (var clan in kingdom.Clans)
                {
                    if (clan == Clan.PlayerClan) continue;
                    AvailableClans.Add(new KingdomPoliticalBlocItemVM(clan, OnClanSelection));
                }

                if (AvailableClans.Count > 0)
                {
                    CurrentSelectedClan = AvailableClans[0];
                }
            }

            private void OnClanSelection(KingdomPoliticalBlocItemVM item)
            {
                CurrentSelectedClan = item;
            }

            private void UpdateActionStates()
            {
                bool canAfford = Hero.MainHero?.Clan?.Influence >= InviteCost;
                CanInviteToBloc = CurrentSelectedClan != null && canAfford;
                CanRequestSupport = CurrentSelectedClan != null;

                InviteHint.HintText = CanInviteToBloc
                    ? TextObject.Empty
                    : new TextObject("Not enough influence");
            }

            public void ExecuteInviteToBloc()
            {
                if (!CanInviteToBloc || CurrentSelectedClan == null) return;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Inviting {CurrentSelectedClan.Name} to your political bloc!"));
                // TODO: 实际逻辑（如添加到派系、触发事件等）
            }

            public void ExecuteRequestSupport()
            {
                if (!CanRequestSupport || CurrentSelectedClan == null) return;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Requesting support from {CurrentSelectedClan.Name}!"));
            }
        }
    }
}
