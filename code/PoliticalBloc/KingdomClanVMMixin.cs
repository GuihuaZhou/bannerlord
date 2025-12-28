using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.PoliticalBloc
{
    [ViewModelMixin("RefreshValues")]
    public class KingdomClanVMMixin : BaseViewModelMixin<KingdomClanVM>
    {
        private bool _canRequestSupport = false;
        private bool _canInviteToBloc = false;
        public KingdomClanVMMixin(KingdomClanVM viewModel) : base(viewModel)
        {
        }

        [DataSourceProperty]
        public bool CanInviteToBloc
        {
            get
            {
                return _canInviteToBloc;
            }
            set
            {
                if (value != _canInviteToBloc)
                {
                    _canInviteToBloc = value;
                    ViewModel!.OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public bool CanRequestSupport
        {
            get
            {
                return _canRequestSupport;
            }
            set
            {
                if (value != _canRequestSupport)
                {
                    _canRequestSupport = value;
                    ViewModel!.OnPropertyChangedWithValue(value);
                }
            }
        }

        // 成本
        public int InviteCost => 50;
        public int SupportCost => 30;

        [DataSourceMethod]
        [UsedImplicitly]
        public void ExecuteInviteToBloc()
        {
            System.Diagnostics.Debug.WriteLine("ExecuteInviteToBloc CALLED!");
            InformationManager.DisplayMessage(new InformationMessage("Invite to Bloc clicked."));
        }

        [DataSourceMethod]
        [UsedImplicitly]
        public void ExecuteRequestSupport()
        {
            InformationManager.DisplayMessage(new InformationMessage("Request Support clicked."));
        }
    }
}
