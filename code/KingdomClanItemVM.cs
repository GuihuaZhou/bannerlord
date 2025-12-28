using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans
{
	// Token: 0x02000086 RID: 134
	public class KingdomClanItemVM : KingdomItemVM
	{
		// Token: 0x06000B20 RID: 2848 RVA: 0x0002F1A8 File Offset: 0x0002D3A8
		public KingdomClanItemVM(Clan clan, Action<KingdomClanItemVM> onSelect)
		{
			this.Clan = clan;
			this._onSelect = onSelect;
			this.Banner = new BannerImageIdentifierVM(clan.Banner, false);
			this.Banner_9 = new BannerImageIdentifierVM(clan.Banner, true);
			this.RefreshValues();
			this.Refresh();
		}

		// Token: 0x06000B21 RID: 2849 RVA: 0x0002F200 File Offset: 0x0002D400
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.Name = this.Clan.Name.ToString();
			GameTexts.SetVariable("TIER", this.Clan.Tier);
			this.TierText = GameTexts.FindText("str_clan_tier", null).ToString();
		}

		// Token: 0x06000B22 RID: 2850 RVA: 0x0002F254 File Offset: 0x0002D454
		public void Refresh()
		{
			this.Members = new MBBindingList<HeroVM>();
			this.ClanType = 0;
			if (this.Clan.IsUnderMercenaryService)
			{
				this.ClanType = 2;
			}
			else if (this.Clan.Kingdom.RulingClan == this.Clan)
			{
				this.ClanType = 1;
			}
			foreach (Hero hero in from h in this.Clan.Heroes
			where !h.IsDisabled && !h.IsNotSpawned && h.IsAlive && !h.IsChild
			select h)
			{
				this.Members.Add(new HeroVM(hero, false));
			}
			this.NumOfMembers = this.Members.Count;
			this.Fiefs = new MBBindingList<KingdomClanFiefItemVM>();
			foreach (Settlement settlement in from s in this.Clan.Settlements
			where s.IsTown || s.IsCastle
			select s)
			{
				this.Fiefs.Add(new KingdomClanFiefItemVM(settlement));
			}
			this.NumOfFiefs = this.Fiefs.Count;
			this.Influence = (int)this.Clan.Influence;
		}

		// Token: 0x06000B23 RID: 2851 RVA: 0x0002F3CC File Offset: 0x0002D5CC
		protected override void OnSelect()
		{
			base.OnSelect();
			this._onSelect(this);
		}

		// Token: 0x17000386 RID: 902
		// (get) Token: 0x06000B24 RID: 2852 RVA: 0x0002F3E0 File Offset: 0x0002D5E0
		// (set) Token: 0x06000B25 RID: 2853 RVA: 0x0002F3E8 File Offset: 0x0002D5E8
		[DataSourceProperty]
		public string Name
		{
			get
			{
				return this._name;
			}
			set
			{
				if (value != this._name)
				{
					this._name = value;
					base.OnPropertyChangedWithValue<string>(value, "Name");
				}
			}
		}

		// Token: 0x17000387 RID: 903
		// (get) Token: 0x06000B26 RID: 2854 RVA: 0x0002F40B File Offset: 0x0002D60B
		// (set) Token: 0x06000B27 RID: 2855 RVA: 0x0002F413 File Offset: 0x0002D613
		[DataSourceProperty]
		public int ClanType
		{
			get
			{
				return this._clanType;
			}
			set
			{
				if (value != this._clanType)
				{
					this._clanType = value;
					base.OnPropertyChangedWithValue(value, "ClanType");
				}
			}
		}

		// Token: 0x17000388 RID: 904
		// (get) Token: 0x06000B28 RID: 2856 RVA: 0x0002F431 File Offset: 0x0002D631
		// (set) Token: 0x06000B29 RID: 2857 RVA: 0x0002F439 File Offset: 0x0002D639
		[DataSourceProperty]
		public int NumOfMembers
		{
			get
			{
				return this._numOfMembers;
			}
			set
			{
				if (value != this._numOfMembers)
				{
					this._numOfMembers = value;
					base.OnPropertyChangedWithValue(value, "NumOfMembers");
				}
			}
		}

		// Token: 0x17000389 RID: 905
		// (get) Token: 0x06000B2A RID: 2858 RVA: 0x0002F457 File Offset: 0x0002D657
		// (set) Token: 0x06000B2B RID: 2859 RVA: 0x0002F45F File Offset: 0x0002D65F
		[DataSourceProperty]
		public int NumOfFiefs
		{
			get
			{
				return this._numOfFiefs;
			}
			set
			{
				if (value != this._numOfFiefs)
				{
					this._numOfFiefs = value;
					base.OnPropertyChangedWithValue(value, "NumOfFiefs");
				}
			}
		}

		// Token: 0x1700038A RID: 906
		// (get) Token: 0x06000B2C RID: 2860 RVA: 0x0002F47D File Offset: 0x0002D67D
		// (set) Token: 0x06000B2D RID: 2861 RVA: 0x0002F485 File Offset: 0x0002D685
		[DataSourceProperty]
		public string TierText
		{
			get
			{
				return this._tierText;
			}
			set
			{
				if (value != this._tierText)
				{
					this._tierText = value;
					base.OnPropertyChangedWithValue<string>(value, "TierText");
				}
			}
		}

		// Token: 0x1700038B RID: 907
		// (get) Token: 0x06000B2E RID: 2862 RVA: 0x0002F4A8 File Offset: 0x0002D6A8
		// (set) Token: 0x06000B2F RID: 2863 RVA: 0x0002F4B0 File Offset: 0x0002D6B0
		[DataSourceProperty]
		public BannerImageIdentifierVM Banner
		{
			get
			{
				return this._banner;
			}
			set
			{
				if (value != this._banner)
				{
					this._banner = value;
					base.OnPropertyChangedWithValue<BannerImageIdentifierVM>(value, "Banner");
				}
			}
		}

		// Token: 0x1700038C RID: 908
		// (get) Token: 0x06000B30 RID: 2864 RVA: 0x0002F4CE File Offset: 0x0002D6CE
		// (set) Token: 0x06000B31 RID: 2865 RVA: 0x0002F4D6 File Offset: 0x0002D6D6
		[DataSourceProperty]
		public BannerImageIdentifierVM Banner_9
		{
			get
			{
				return this._banner_9;
			}
			set
			{
				if (value != this._banner_9)
				{
					this._banner_9 = value;
					base.OnPropertyChangedWithValue<BannerImageIdentifierVM>(value, "Banner_9");
				}
			}
		}

		// Token: 0x1700038D RID: 909
		// (get) Token: 0x06000B32 RID: 2866 RVA: 0x0002F4F4 File Offset: 0x0002D6F4
		// (set) Token: 0x06000B33 RID: 2867 RVA: 0x0002F4FC File Offset: 0x0002D6FC
		[DataSourceProperty]
		public MBBindingList<HeroVM> Members
		{
			get
			{
				return this._members;
			}
			set
			{
				if (value != this._members)
				{
					this._members = value;
					base.OnPropertyChangedWithValue<MBBindingList<HeroVM>>(value, "Members");
				}
			}
		}

		// Token: 0x1700038E RID: 910
		// (get) Token: 0x06000B34 RID: 2868 RVA: 0x0002F51A File Offset: 0x0002D71A
		// (set) Token: 0x06000B35 RID: 2869 RVA: 0x0002F522 File Offset: 0x0002D722
		[DataSourceProperty]
		public MBBindingList<KingdomClanFiefItemVM> Fiefs
		{
			get
			{
				return this._fiefs;
			}
			set
			{
				if (value != this._fiefs)
				{
					this._fiefs = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomClanFiefItemVM>>(value, "Fiefs");
				}
			}
		}

		// Token: 0x1700038F RID: 911
		// (get) Token: 0x06000B36 RID: 2870 RVA: 0x0002F540 File Offset: 0x0002D740
		// (set) Token: 0x06000B37 RID: 2871 RVA: 0x0002F548 File Offset: 0x0002D748
		[DataSourceProperty]
		public int Influence
		{
			get
			{
				return this._influence;
			}
			set
			{
				if (value != this._influence)
				{
					this._influence = value;
					base.OnPropertyChangedWithValue(value, "Influence");
				}
			}
		}

		// Token: 0x040004F0 RID: 1264
		private readonly Action<KingdomClanItemVM> _onSelect;

		// Token: 0x040004F1 RID: 1265
		public readonly Clan Clan;

		// Token: 0x040004F2 RID: 1266
		private string _name;

		// Token: 0x040004F3 RID: 1267
		private BannerImageIdentifierVM _banner;

		// Token: 0x040004F4 RID: 1268
		private BannerImageIdentifierVM _banner_9;

		// Token: 0x040004F5 RID: 1269
		private MBBindingList<HeroVM> _members;

		// Token: 0x040004F6 RID: 1270
		private MBBindingList<KingdomClanFiefItemVM> _fiefs;

		// Token: 0x040004F7 RID: 1271
		private int _influence;

		// Token: 0x040004F8 RID: 1272
		private int _numOfMembers;

		// Token: 0x040004F9 RID: 1273
		private int _numOfFiefs;

		// Token: 0x040004FA RID: 1274
		private string _tierText;

		// Token: 0x040004FB RID: 1275
		private int _clanType = -1;

		// Token: 0x020001E1 RID: 481
		private enum ClanTypes
		{
			// Token: 0x0400111B RID: 4379
			Normal,
			// Token: 0x0400111C RID: 4380
			Leader,
			// Token: 0x0400111D RID: 4381
			Mercenary
		}
	}
}
