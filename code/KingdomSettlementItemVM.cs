using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Armies;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements
{
	// Token: 0x02000067 RID: 103
	public class KingdomSettlementItemVM : KingdomItemVM
	{
		// Token: 0x17000235 RID: 565
		// (get) Token: 0x060007DB RID: 2011 RVA: 0x000243D3 File Offset: 0x000225D3
		// (set) Token: 0x060007DC RID: 2012 RVA: 0x000243DB File Offset: 0x000225DB
		public int Garrison { get; private set; }

		// Token: 0x17000236 RID: 566
		// (get) Token: 0x060007DD RID: 2013 RVA: 0x000243E4 File Offset: 0x000225E4
		// (set) Token: 0x060007DE RID: 2014 RVA: 0x000243EC File Offset: 0x000225EC
		public int Militia { get; private set; }

		// Token: 0x060007DF RID: 2015 RVA: 0x000243F8 File Offset: 0x000225F8
		public KingdomSettlementItemVM(Settlement settlement, Action<KingdomSettlementItemVM> onSelect)
		{
			this.Settlement = settlement;
			this._onSelect = onSelect;
			this.Name = settlement.Name.ToString();
			this.Villages = new MBBindingList<KingdomSettlementVillageItemVM>();
			SettlementComponent settlementComponent = settlement.SettlementComponent;
			this.SettlementImagePath = ((settlementComponent == null) ? "placeholder" : (settlementComponent.BackgroundMeshName + "_t"));
			this.ItemProperties = new MBBindingList<SelectableFiefItemPropertyVM>();
			this.ImageName = ((settlementComponent != null) ? settlementComponent.WaitMeshName : "");
			this.Owner = new HeroVM(settlement.OwnerClan.Leader, false);
			this.OwnerClanBanner = new BannerImageIdentifierVM(this.Settlement.OwnerClan.Banner, false);
			this.OwnerClanBanner_9 = new BannerImageIdentifierVM(this.Settlement.OwnerClan.Banner, true);
			Town town = settlement.Town;
			this.WallLevel = ((town == null) ? -1 : town.GetWallLevel());
			if (town != null)
			{
				this.Prosperity = MathF.Round(town.Prosperity);
				this.IconPath = town.BackgroundMeshName;
			}
			else if (settlement.IsCastle)
			{
				this.Prosperity = MathF.Round(settlement.Town.Prosperity);
				this.IconPath = "";
			}
			foreach (Village village in this.Settlement.BoundVillages)
			{
				this.Villages.Add(new KingdomSettlementVillageItemVM(village));
			}
			int defenders;
			if (!this.Settlement.IsFortification)
			{
				defenders = (int)this.Settlement.Militia;
			}
			else
			{
				MobileParty garrisonParty = this.Settlement.Town.GarrisonParty;
				defenders = ((garrisonParty != null) ? garrisonParty.Party.NumberOfAllMembers : 0);
			}
			this.Defenders = defenders;
			this.RefreshValues();
		}

		// Token: 0x060007E0 RID: 2016 RVA: 0x000245D0 File Offset: 0x000227D0
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.Villages.ApplyActionOnAllItems(delegate(KingdomSettlementVillageItemVM x)
			{
				x.RefreshValues();
			});
			this.UpdateProperties();
		}

		// Token: 0x060007E1 RID: 2017 RVA: 0x00024608 File Offset: 0x00022808
		protected virtual void UpdateProperties()
		{
			this.ItemProperties.Clear();
			if (this.Settlement.Town != null)
			{
				BasicTooltipViewModel hint = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownWallsTooltip(this.Settlement.Town));
				this.ItemProperties.Add(new SelectableFiefItemPropertyVM(GameTexts.FindText("str_walls", null).ToString(), this.Settlement.Town.GetWallLevel().ToString(), 0, SelectableItemPropertyVM.PropertyType.Wall, hint, false));
				BasicTooltipViewModel hint2 = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownGarrisonTooltip(this.Settlement.Town));
				int changeAmount = (int)SettlementHelper.GetGarrisonChangeExplainedNumber(this.Settlement.Town).ResultNumber;
				Collection<SelectableFiefItemPropertyVM> itemProperties = this.ItemProperties;
				string name = GameTexts.FindText("str_garrison", null).ToString();
				MobileParty garrisonParty = this.Settlement.Town.GarrisonParty;
				itemProperties.Add(new SelectableFiefItemPropertyVM(name, ((garrisonParty != null) ? garrisonParty.Party.NumberOfAllMembers.ToString() : null) ?? "0", changeAmount, SelectableItemPropertyVM.PropertyType.Garrison, hint2, false));
			}
			int num = (int)this.Settlement.Militia;
			List<TooltipProperty> militiaHint = this.Settlement.IsVillage ? CampaignUIHelper.GetVillageMilitiaTooltip(this.Settlement.Village) : CampaignUIHelper.GetTownMilitiaTooltip(this.Settlement.Town);
			int changeAmount2 = (this.Settlement.Town != null) ? ((int)this.Settlement.Town.MilitiaChange) : ((int)this.Settlement.Village.MilitiaChange);
			this.ItemProperties.Add(new SelectableFiefItemPropertyVM(GameTexts.FindText("str_militia", null).ToString(), num.ToString(), changeAmount2, SelectableItemPropertyVM.PropertyType.Militia, new BasicTooltipViewModel(() => militiaHint), false));
			if (this.Settlement.Town != null)
			{
				BasicTooltipViewModel hint3 = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownFoodTooltip(this.Settlement.Town));
				int changeAmount3 = (int)this.Settlement.Town.FoodChange;
				this.ItemProperties.Add(new SelectableFiefItemPropertyVM(GameTexts.FindText("str_food_stocks", null).ToString(), ((int)this.Settlement.Town.FoodStocks).ToString(), changeAmount3, SelectableItemPropertyVM.PropertyType.Food, hint3, false));
			}
			int changeAmount4 = (this.Settlement.Town != null) ? ((int)this.Settlement.Town.ProsperityChange) : ((int)this.Settlement.Village.HearthChange);
			if (this.Settlement.IsFortification)
			{
				BasicTooltipViewModel hint4;
				if (this.Settlement.Town != null)
				{
					hint4 = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownProsperityTooltip(this.Settlement.Town));
				}
				else
				{
					hint4 = new BasicTooltipViewModel(() => CampaignUIHelper.GetVillageProsperityTooltip(this.Settlement.Village));
				}
				this.ItemProperties.Add(new SelectableFiefItemPropertyVM(GameTexts.FindText("str_prosperity", null).ToString(), string.Format("{0:0.#}", this.Settlement.Town.Prosperity), changeAmount4, SelectableItemPropertyVM.PropertyType.Prosperity, hint4, false));
			}
			if (this.Settlement.Town != null)
			{
				BasicTooltipViewModel hint5 = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownLoyaltyTooltip(this.Settlement.Town));
				int changeAmount5 = (int)this.Settlement.Town.LoyaltyChange;
				bool isWarning = this.Settlement.IsTown && this.Settlement.Town.Loyalty < (float)Campaign.Current.Models.SettlementLoyaltyModel.RebelliousStateStartLoyaltyThreshold;
				this.ItemProperties.Add(new SelectableFiefItemPropertyVM(GameTexts.FindText("str_loyalty", null).ToString(), string.Format("{0:0.#}", this.Settlement.Town.Loyalty), changeAmount5, SelectableItemPropertyVM.PropertyType.Loyalty, hint5, isWarning));
				BasicTooltipViewModel hint6 = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownSecurityTooltip(this.Settlement.Town));
				int changeAmount6 = (int)this.Settlement.Town.SecurityChange;
				this.ItemProperties.Add(new SelectableFiefItemPropertyVM(GameTexts.FindText("str_security", null).ToString(), string.Format("{0:0.#}", this.Settlement.Town.Security), changeAmount6, SelectableItemPropertyVM.PropertyType.Security, hint6, false));
			}
			if (this.Settlement.IsTown)
			{
				BasicTooltipViewModel hint7 = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownPatrolTooltip(this.Settlement.Town));
				this.ItemProperties.Add(new SelectableFiefItemPropertyVM(GameTexts.FindText("str_patrol", null).ToString(), Campaign.Current.GetCampaignBehavior<IPatrolPartiesCampaignBehavior>().GetSettlementPatrolStatus(this.Settlement).ToString(), 0, SelectableItemPropertyVM.PropertyType.Patrol, hint7, false));
			}
		}

		// Token: 0x060007E2 RID: 2018 RVA: 0x00024A70 File Offset: 0x00022C70
		protected override void OnSelect()
		{
			base.OnSelect();
			this._onSelect(this);
		}

		// Token: 0x060007E3 RID: 2019 RVA: 0x00024A84 File Offset: 0x00022C84
		private void ExecuteBeginHint()
		{
			InformationManager.ShowTooltip(typeof(Settlement), new object[]
			{
				this.Settlement,
				true
			});
		}

		// Token: 0x060007E4 RID: 2020 RVA: 0x00024AAD File Offset: 0x00022CAD
		private void ExecuteEndHint()
		{
			MBInformationManager.HideInformations();
		}

		// Token: 0x060007E5 RID: 2021 RVA: 0x00024AB4 File Offset: 0x00022CB4
		public void ExecuteLink()
		{
			if (this.Settlement != null)
			{
				Campaign.Current.EncyclopediaManager.GoToLink(this.Settlement.EncyclopediaLink);
			}
		}

		// Token: 0x17000237 RID: 567
		// (get) Token: 0x060007E6 RID: 2022 RVA: 0x00024AD8 File Offset: 0x00022CD8
		// (set) Token: 0x060007E7 RID: 2023 RVA: 0x00024AE0 File Offset: 0x00022CE0
		[DataSourceProperty]
		public MBBindingList<SelectableFiefItemPropertyVM> ItemProperties
		{
			get
			{
				return this._itemProperties;
			}
			set
			{
				if (value != this._itemProperties)
				{
					this._itemProperties = value;
					base.OnPropertyChangedWithValue<MBBindingList<SelectableFiefItemPropertyVM>>(value, "ItemProperties");
				}
			}
		}

		// Token: 0x17000238 RID: 568
		// (get) Token: 0x060007E8 RID: 2024 RVA: 0x00024AFE File Offset: 0x00022CFE
		// (set) Token: 0x060007E9 RID: 2025 RVA: 0x00024B06 File Offset: 0x00022D06
		[DataSourceProperty]
		public MBBindingList<KingdomSettlementVillageItemVM> Villages
		{
			get
			{
				return this._villages;
			}
			set
			{
				if (value != this._villages)
				{
					this._villages = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomSettlementVillageItemVM>>(value, "Villages");
				}
			}
		}

		// Token: 0x17000239 RID: 569
		// (get) Token: 0x060007EA RID: 2026 RVA: 0x00024B24 File Offset: 0x00022D24
		// (set) Token: 0x060007EB RID: 2027 RVA: 0x00024B2C File Offset: 0x00022D2C
		[DataSourceProperty]
		public string IconPath
		{
			get
			{
				return this._iconPath;
			}
			set
			{
				if (value != this._iconPath)
				{
					this._iconPath = value;
					base.OnPropertyChangedWithValue<string>(value, "IconPath");
				}
			}
		}

		// Token: 0x1700023A RID: 570
		// (get) Token: 0x060007EC RID: 2028 RVA: 0x00024B4F File Offset: 0x00022D4F
		// (set) Token: 0x060007ED RID: 2029 RVA: 0x00024B57 File Offset: 0x00022D57
		[DataSourceProperty]
		public int Defenders
		{
			get
			{
				return this._defenders;
			}
			set
			{
				if (value != this._defenders)
				{
					this._defenders = value;
					base.OnPropertyChangedWithValue(value, "Defenders");
				}
			}
		}

		// Token: 0x1700023B RID: 571
		// (get) Token: 0x060007EE RID: 2030 RVA: 0x00024B75 File Offset: 0x00022D75
		// (set) Token: 0x060007EF RID: 2031 RVA: 0x00024B7D File Offset: 0x00022D7D
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

		// Token: 0x1700023C RID: 572
		// (get) Token: 0x060007F0 RID: 2032 RVA: 0x00024BA0 File Offset: 0x00022DA0
		// (set) Token: 0x060007F1 RID: 2033 RVA: 0x00024BA8 File Offset: 0x00022DA8
		[DataSourceProperty]
		public string ImageName
		{
			get
			{
				return this._imageName;
			}
			set
			{
				if (value != this._imageName)
				{
					this._imageName = value;
					base.OnPropertyChangedWithValue<string>(value, "ImageName");
				}
			}
		}

		// Token: 0x1700023D RID: 573
		// (get) Token: 0x060007F2 RID: 2034 RVA: 0x00024BCB File Offset: 0x00022DCB
		// (set) Token: 0x060007F3 RID: 2035 RVA: 0x00024BD3 File Offset: 0x00022DD3
		[DataSourceProperty]
		public string SettlementImagePath
		{
			get
			{
				return this._settlementImagePath;
			}
			set
			{
				if (value != this._settlementImagePath)
				{
					this._settlementImagePath = value;
					base.OnPropertyChangedWithValue<string>(value, "SettlementImagePath");
				}
			}
		}

		// Token: 0x1700023E RID: 574
		// (get) Token: 0x060007F4 RID: 2036 RVA: 0x00024BF6 File Offset: 0x00022DF6
		// (set) Token: 0x060007F5 RID: 2037 RVA: 0x00024BFE File Offset: 0x00022DFE
		[DataSourceProperty]
		public string GovernorName
		{
			get
			{
				return this._governorName;
			}
			set
			{
				if (value != this._governorName)
				{
					this._governorName = value;
					base.OnPropertyChangedWithValue<string>(value, "GovernorName");
				}
			}
		}

		// Token: 0x1700023F RID: 575
		// (get) Token: 0x060007F6 RID: 2038 RVA: 0x00024C21 File Offset: 0x00022E21
		// (set) Token: 0x060007F7 RID: 2039 RVA: 0x00024C29 File Offset: 0x00022E29
		[DataSourceProperty]
		public BannerImageIdentifierVM OwnerClanBanner
		{
			get
			{
				return this._ownerClanBanner;
			}
			set
			{
				if (value != this._ownerClanBanner)
				{
					this._ownerClanBanner = value;
					base.OnPropertyChangedWithValue<BannerImageIdentifierVM>(value, "OwnerClanBanner");
				}
			}
		}

		// Token: 0x17000240 RID: 576
		// (get) Token: 0x060007F8 RID: 2040 RVA: 0x00024C47 File Offset: 0x00022E47
		// (set) Token: 0x060007F9 RID: 2041 RVA: 0x00024C4F File Offset: 0x00022E4F
		[DataSourceProperty]
		public BannerImageIdentifierVM OwnerClanBanner_9
		{
			get
			{
				return this._ownerClanBanner_9;
			}
			set
			{
				if (value != this._ownerClanBanner_9)
				{
					this._ownerClanBanner_9 = value;
					base.OnPropertyChangedWithValue<BannerImageIdentifierVM>(value, "OwnerClanBanner_9");
				}
			}
		}

		// Token: 0x17000241 RID: 577
		// (get) Token: 0x060007FA RID: 2042 RVA: 0x00024C6D File Offset: 0x00022E6D
		// (set) Token: 0x060007FB RID: 2043 RVA: 0x00024C75 File Offset: 0x00022E75
		[DataSourceProperty]
		public HeroVM Owner
		{
			get
			{
				return this._owner;
			}
			set
			{
				if (value != this._owner)
				{
					this._owner = value;
					base.OnPropertyChangedWithValue<HeroVM>(value, "Owner");
				}
			}
		}

		// Token: 0x17000242 RID: 578
		// (get) Token: 0x060007FC RID: 2044 RVA: 0x00024C93 File Offset: 0x00022E93
		// (set) Token: 0x060007FD RID: 2045 RVA: 0x00024C9B File Offset: 0x00022E9B
		[DataSourceProperty]
		public int WallLevel
		{
			get
			{
				return this._wallLevel;
			}
			set
			{
				if (value != this._wallLevel)
				{
					this._wallLevel = value;
					base.OnPropertyChangedWithValue(value, "WallLevel");
				}
			}
		}

		// Token: 0x17000243 RID: 579
		// (get) Token: 0x060007FE RID: 2046 RVA: 0x00024CB9 File Offset: 0x00022EB9
		// (set) Token: 0x060007FF RID: 2047 RVA: 0x00024CC1 File Offset: 0x00022EC1
		[DataSourceProperty]
		public int Prosperity
		{
			get
			{
				return this._prosperity;
			}
			set
			{
				if (value != this._prosperity)
				{
					this._prosperity = value;
					base.OnPropertyChangedWithValue(value, "Prosperity");
				}
			}
		}

		// Token: 0x04000360 RID: 864
		private readonly Action<KingdomSettlementItemVM> _onSelect;

		// Token: 0x04000361 RID: 865
		public readonly Settlement Settlement;

		// Token: 0x04000364 RID: 868
		private string _iconPath;

		// Token: 0x04000365 RID: 869
		private string _name;

		// Token: 0x04000366 RID: 870
		private string _imageName;

		// Token: 0x04000367 RID: 871
		private string _settlementImagePath;

		// Token: 0x04000368 RID: 872
		private string _governorName;

		// Token: 0x04000369 RID: 873
		private BannerImageIdentifierVM _ownerClanBanner;

		// Token: 0x0400036A RID: 874
		private BannerImageIdentifierVM _ownerClanBanner_9;

		// Token: 0x0400036B RID: 875
		private HeroVM _owner;

		// Token: 0x0400036C RID: 876
		private MBBindingList<SelectableFiefItemPropertyVM> _itemProperties;

		// Token: 0x0400036D RID: 877
		private MBBindingList<KingdomSettlementVillageItemVM> _villages;

		// Token: 0x0400036E RID: 878
		private int _wallLevel;

		// Token: 0x0400036F RID: 879
		private int _prosperity;

		// Token: 0x04000370 RID: 880
		private int _defenders;
	}
}
