using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Armies;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.PoliticalBloc
{

    [ViewModelMixin("RefreshValues")]
    public class KingdomManagementVMMixin : BaseViewModelMixin<KingdomManagementVM>
    {
        private readonly KingdomManagementVM _kingdomVM;
        private readonly int _categoryCount = 6;
        private int _currentCategory = 0;

        public KingdomManagementVMMixin(KingdomManagementVM vm) : base(vm)
        {
            _kingdomVM = vm;
        }

        private void ExecuteShowPoliticalBloc()
        {
            InformationManager.DisplayMessage(new InformationMessage("Show Political Bloc."));
            SetSelectedCategory(5);
        }

        public void SelectPreviousCategory()
        {
            int selectedCategory = (_currentCategory == 0) ? (_categoryCount - 1) : (_currentCategory - 1);
            SetSelectedCategory(selectedCategory);
        }

        public void SelectNextCategory()
        {
            int selectedCategory = (_currentCategory + 1) % _categoryCount;
            SetSelectedCategory(selectedCategory);
        }


        private void SetSelectedCategory(int index)
        {
            _kingdomVM.Clan.Show = false;
            _kingdomVM.Settlement.Show = false;
            _kingdomVM.Policy.Show = false;
            _kingdomVM.Army.Show = false;
            _kingdomVM.Diplomacy.Show = false;

            _currentCategory = index;
            switch (index)
            {
                case 0: 
                    _kingdomVM.Clan.Show = true; 
                    break;
                case 1: 
                    _kingdomVM.Settlement.Show = true; 
                    break;
                case 2:
                    _kingdomVM.Policy.Show = true;
                    break;
                case 3:
                    _kingdomVM.Army.Show = true;
                    break;
                case 4:
                    _kingdomVM.Diplomacy.Show = true;
                    break;
                case 5:
                    // 政治阵营：目前不显示任何原生面板
                    // 未来可在此处注入自定义 VM 并显示
                    break;
            }
        }
    }
}
