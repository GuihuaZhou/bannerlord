using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.PoliticalBloc
{
    public class KingdomPoliticalBlocItemVM : KingdomItemVM 
    {
        private readonly Clan _clan;
        private string _name;
        private BannerImageIdentifierVM _banner;
        private BannerImageIdentifierVM _banner_9;
        private int _influence;
        private int _memberCount;

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public BannerImageIdentifierVM Banner
        {
            get => _banner;
            set
            {
                if (_banner != value)
                {
                    _banner = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public BannerImageIdentifierVM Banner_9
        {
            get => _banner_9;
            set
            {
                if (_banner_9 != value)
                {
                    _banner_9 = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public int Influence
        {
            get => _influence;
            set
            {
                if (_influence != value)
                {
                    _influence = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public int MemberCount
        {
            get => _memberCount;
            set
            {
                if (_memberCount != value)
                {
                    _memberCount = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        public Clan Clan => _clan;

        public KingdomPoliticalBlocItemVM(Clan clan, Action<KingdomPoliticalBlocItemVM> onSelect)
        {
            _clan = clan;
            _onSelect = onSelect;

            // 初始化 Banner（和 KingdomClanItemVM 完全一致）
            Banner = new BannerImageIdentifierVM(clan.Banner, false);
            Banner_9 = new BannerImageIdentifierVM(clan.Banner, true);

            RefreshValues();
        }

        public void RefreshValues()
        {
            Name = _clan.Name.ToString();
            Influence = (int)_clan.Influence;
            //MemberCount = _clan.Heroes.Count(h => h.IsAlive && !h.IsDisabled && !h.IsNotSpawned);
            MemberCount = 1;
        }

        // 可选：如果你希望点击时触发选择（类似 Clan）
        protected override void OnSelect()
        {
            base.OnSelect(); // 设置 IsSelected = true
            _onSelect?.Invoke(this);
        }

        // 存储 onSelect 回调（模仿 KingdomClanItemVM）
        private readonly Action<KingdomPoliticalBlocItemVM> _onSelect;
    }
}
