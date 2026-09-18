using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;

namespace ModifiedPolitics.UI.KingdomClan
{
    /// <summary>
    /// Kingdom Management -> Clans 页面详情扩展的公共入口。
    /// 各内政功能的 UI 数据由同一 partial 类的独立文件提供。
    /// </summary>
    [ViewModelMixin("Refresh")]
    public partial class KingdomClanItemVMMixin
        : BaseViewModelMixin<KingdomClanItemVM>
    {
        private readonly KingdomClanItemVM _vm;

        public KingdomClanItemVMMixin(KingdomClanItemVM vm)
            : base(vm)
        {
            _vm = vm;

            RefreshWarPotentialData();
            RefreshWarDispositionUiData();
        }

        public override void OnRefresh()
        {
            RefreshWarPotentialData();
            RefreshWarDispositionUiData();
        }

        private Clan GetClan()
        {
            return _vm?.Clan;
        }
    }
}
