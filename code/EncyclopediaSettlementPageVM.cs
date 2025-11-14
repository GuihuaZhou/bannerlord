using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Tutorial;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages
{
	// Token: 0x020000D6 RID: 214
	[EncyclopediaViewModel(typeof(Settlement))]
	public class EncyclopediaSettlementPageVM : EncyclopediaContentPageVM
	{
		// Token: 0x06001448 RID: 5192 RVA: 0x00050374 File Offset: 0x0004E574
		public EncyclopediaSettlementPageVM(EncyclopediaPageArgs args) : base(args)
		{
			this._settlement = (base.Obj as Settlement);
			this.NotableCharacters = new MBBindingList<HeroVM>();
			this.Settlements = new MBBindingList<EncyclopediaSettlementVM>();
			this.History = new MBBindingList<EncyclopediaHistoryEventVM>();
			this._isVisualTrackerSelected = Campaign.Current.VisualTrackerManager.CheckTracked(this._settlement);
			this.IsFortification = this._settlement.IsFortification;
			this.SettlementImageID = this._settlement.SettlementComponent.WaitMeshName;
			base.IsBookmarked = Campaign.Current.EncyclopediaManager.ViewDataTracker.IsEncyclopediaBookmarked(this._settlement);
			Game.Current.EventManager.RegisterEvent<TutorialNotificationElementChangeEvent>(new Action<TutorialNotificationElementChangeEvent>(this.OnTutorialNotificationElementIDChange));
			this.RefreshValues();
			TextObject textObject;
			if (CampaignUIHelper.IsSettlementInformationHidden(this._settlement, out textObject))
			{
				Game.Current.EventManager.TriggerEvent<EncyclopediaPageChangedEvent>(new EncyclopediaPageChangedEvent(EncyclopediaPages.Settlement, true));
			}
		}

		// Token: 0x06001449 RID: 5193 RVA: 0x00050464 File Offset: 0x0004E664
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.SettlementName = this._settlement.Name.ToString();
			this.SettlementsText = GameTexts.FindText("str_villages", null).ToString();
			this.NotableCharactersText = GameTexts.FindText("str_notable_characters", null).ToString();
			this.OwnerText = GameTexts.FindText("str_owner", null).ToString();
			this.TrackText = GameTexts.FindText("str_settlement_track", null).ToString();
			this.ShowInMapHint = new HintViewModel(GameTexts.FindText("str_show_on_map", null), null);
			this.InformationText = this._settlement.EncyclopediaText.ToString();
			base.UpdateBookmarkHintText();
			this.Refresh();
		}

		// Token: 0x0600144A RID: 5194 RVA: 0x00050520 File Offset: 0x0004E720
		public override void Refresh()
		{
			base.IsLoadingOver = false;
			SettlementComponent settlementComponent = this._settlement.SettlementComponent;
			this.NotableCharacters.Clear();
			this.Settlements.Clear();
			this.History.Clear();
			this.IsFortification = this._settlement.IsFortification;
			if (this._settlement.IsFortification)
			{
				this.SettlementType = 0;
				EncyclopediaPage pageOf = Campaign.Current.EncyclopediaManager.GetPageOf(typeof(Settlement));
				using (List<Village>.Enumerator enumerator = this._settlement.BoundVillages.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						Village village = enumerator.Current;
						if (pageOf.IsValidEncyclopediaItem(village.Owner.Settlement))
						{
							this.Settlements.Add(new EncyclopediaSettlementVM(village.Owner.Settlement));
						}
					}
					goto IL_F2;
				}
			}
			if (this._settlement.IsVillage)
			{
				this.SettlementType = 1;
			}
			IL_F2:
			if (!this._settlement.IsCastle)
			{
				EncyclopediaPage pageOf2 = Campaign.Current.EncyclopediaManager.GetPageOf(typeof(Hero));
				foreach (Hero hero in this._settlement.Notables)
				{
					if (pageOf2.IsValidEncyclopediaItem(hero))
					{
						this.NotableCharacters.Add(new HeroVM(hero, false));
					}
				}
			}
			GameTexts.SetVariable("STR1", GameTexts.FindText("str_enc_sf_culture", null).ToString());
			GameTexts.SetVariable("STR2", this._settlement.Culture.Name.ToString());
			this.CultureText = GameTexts.FindText("str_STR1_space_STR2", null).ToString();
			this.OwnerText = GameTexts.FindText("str_owner", null).ToString();
			this.Owner = new HeroVM(this._settlement.OwnerClan.Leader, false);
			this.OwnerBanner = new EncyclopediaFactionVM(this._settlement.OwnerClan);
			this.SettlementPath = settlementComponent.BackgroundMeshName;
			this.SettlementCropPosition = (double)settlementComponent.BackgroundCropPosition;
			this.HasBoundSettlement = this._settlement.IsVillage;
			this.BoundSettlement = (this.HasBoundSettlement ? new EncyclopediaSettlementVM(this._settlement.Village.Bound) : null);
			this.BoundSettlementText = "";
			if (this.HasBoundSettlement)
			{
				GameTexts.SetVariable("SETTLEMENT_LINK", this._settlement.Village.Bound.EncyclopediaLinkWithName);
				this.BoundSettlementText = GameTexts.FindText("str_bound_settlement_encyclopedia", null).ToString();
			}
			TextObject textObject;
			bool flag = CampaignUIHelper.IsSettlementInformationHidden(this._settlement, out textObject);
			string text = GameTexts.FindText("str_missing_info_indicator", null).ToString();
			string statText = flag ? text : ((int)this._settlement.Militia).ToString();
			if (this._settlement.IsFortification)
			{
				MBBindingList<EncyclopediaSettlementPageStatItemVM> mbbindingList = new MBBindingList<EncyclopediaSettlementPageStatItemVM>();
				mbbindingList.Add(new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetTownWallsTooltip(this._settlement.Town)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Wall, flag ? text : this._settlement.Town.GetWallLevel().ToString()));
				BasicTooltipViewModel basicTooltipViewModel = new BasicTooltipViewModel(() => CampaignUIHelper.GetTownGarrisonTooltip(this._settlement.Town));
				EncyclopediaSettlementPageStatItemVM.DescriptionType type = EncyclopediaSettlementPageStatItemVM.DescriptionType.Garrison;
				string statText2;
				if (!flag)
				{
					MobileParty garrisonParty = this._settlement.Town.GarrisonParty;
					statText2 = ((garrisonParty != null) ? garrisonParty.Party.NumberOfAllMembers.ToString() : null);
				}
				else
				{
					statText2 = text;
				}
				mbbindingList.Add(new EncyclopediaSettlementPageStatItemVM(basicTooltipViewModel, type, statText2));
				mbbindingList.Add(new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetTownMilitiaTooltip(this._settlement.Town)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Militia, statText));
				mbbindingList.Add(new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetTownFoodTooltip(this._settlement.Town)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Food, flag ? text : ((int)this._settlement.Town.FoodStocks).ToString()));
				this.LeftSideProperties = mbbindingList;
				this.RightSideProperties = new MBBindingList<EncyclopediaSettlementPageStatItemVM>
				{
					new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetTownProsperityTooltip(this._settlement.Town)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Prosperity, flag ? text : ((int)this._settlement.Town.Prosperity).ToString()),
					new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetTownLoyaltyTooltip(this._settlement.Town)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Loyalty, flag ? text : ((int)this._settlement.Town.Loyalty).ToString()),
					new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetTownSecurityTooltip(this._settlement.Town)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Security, flag ? text : ((int)this._settlement.Town.Security).ToString())
				};
			}
			else
			{
				this.LeftSideProperties = new MBBindingList<EncyclopediaSettlementPageStatItemVM>
				{
					new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetVillageMilitiaTooltip(this._settlement.Village)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Militia, statText)
				};
				this.RightSideProperties = new MBBindingList<EncyclopediaSettlementPageStatItemVM>
				{
					new EncyclopediaSettlementPageStatItemVM(new BasicTooltipViewModel(() => CampaignUIHelper.GetVillageProsperityTooltip(this._settlement.Village)), EncyclopediaSettlementPageStatItemVM.DescriptionType.Prosperity, flag ? text : ((int)this._settlement.Village.Hearth).ToString())
				};
			}
			this.NameText = this._settlement.Name.ToString();
			for (int i = Campaign.Current.LogEntryHistory.GameActionLogs.Count - 1; i >= 0; i--)
			{
				IEncyclopediaLog encyclopediaLog;
				if ((encyclopediaLog = (Campaign.Current.LogEntryHistory.GameActionLogs[i] as IEncyclopediaLog)) != null && encyclopediaLog.IsVisibleInEncyclopediaPageOf<Settlement>(this._settlement))
				{
					this.History.Add(new EncyclopediaHistoryEventVM(encyclopediaLog));
				}
			}
			this.IsVisualTrackerSelected = Campaign.Current.VisualTrackerManager.CheckTracked(this._settlement);
			base.IsLoadingOver = true;
		}

		// Token: 0x0600144B RID: 5195 RVA: 0x00050AF4 File Offset: 0x0004ECF4
		public override string GetName()
		{
			return this._settlement.Name.ToString();
		}

		// Token: 0x0600144C RID: 5196 RVA: 0x00050B08 File Offset: 0x0004ED08
		public void ExecuteTrack()
		{
			if (!this.IsVisualTrackerSelected)
			{
				Campaign.Current.VisualTrackerManager.RegisterObject(this._settlement);
				this.IsVisualTrackerSelected = true;
			}
			else
			{
				Campaign.Current.VisualTrackerManager.RemoveTrackedObject(this._settlement, false);
				this.IsVisualTrackerSelected = false;
			}
			Game.Current.EventManager.TriggerEvent<PlayerToggleTrackSettlementFromEncyclopediaEvent>(new PlayerToggleTrackSettlementFromEncyclopediaEvent(this._settlement, this.IsVisualTrackerSelected));
		}

		// Token: 0x0600144D RID: 5197 RVA: 0x00050B78 File Offset: 0x0004ED78
		public override string GetNavigationBarURL()
		{
			return HyperlinkTexts.GetGenericHyperlinkText("Home", GameTexts.FindText("str_encyclopedia_home", null).ToString()) + " \\ " + HyperlinkTexts.GetGenericHyperlinkText("ListPage-Settlements", GameTexts.FindText("str_encyclopedia_settlements", null).ToString()) + " \\ " + this.GetName();
		}

		// Token: 0x0600144E RID: 5198 RVA: 0x00050BDD File Offset: 0x0004EDDD
		public void ExecuteBoundSettlementLink()
		{
			if (this.HasBoundSettlement)
			{
				Campaign.Current.EncyclopediaManager.GoToLink(this._settlement.Village.Bound.EncyclopediaLink);
			}
		}

		// Token: 0x0600144F RID: 5199 RVA: 0x00050C0C File Offset: 0x0004EE0C
		public override void ExecuteSwitchBookmarkedState()
		{
			base.ExecuteSwitchBookmarkedState();
			if (base.IsBookmarked)
			{
				Campaign.Current.EncyclopediaManager.ViewDataTracker.AddEncyclopediaBookmarkToItem(this._settlement);
				return;
			}
			Campaign.Current.EncyclopediaManager.ViewDataTracker.RemoveEncyclopediaBookmarkFromItem(this._settlement);
		}

		// Token: 0x06001450 RID: 5200 RVA: 0x00050C5C File Offset: 0x0004EE5C
		private void OnTutorialNotificationElementIDChange(TutorialNotificationElementChangeEvent evnt)
		{
			this.IsTrackerButtonHighlightEnabled = (evnt.NewNotificationElementID == "EncyclopediaItemTrackButton");
		}

		// Token: 0x06001451 RID: 5201 RVA: 0x00050C74 File Offset: 0x0004EE74
		public override void OnFinalize()
		{
			base.OnFinalize();
			Game.Current.EventManager.UnregisterEvent<TutorialNotificationElementChangeEvent>(new Action<TutorialNotificationElementChangeEvent>(this.OnTutorialNotificationElementIDChange));
		}

		// Token: 0x170006B8 RID: 1720
		// (get) Token: 0x06001452 RID: 5202 RVA: 0x00050C97 File Offset: 0x0004EE97
		// (set) Token: 0x06001453 RID: 5203 RVA: 0x00050C9F File Offset: 0x0004EE9F
		[DataSourceProperty]
		public EncyclopediaFactionVM OwnerBanner
		{
			get
			{
				return this._ownerBanner;
			}
			set
			{
				if (value != this._ownerBanner)
				{
					this._ownerBanner = value;
					base.OnPropertyChangedWithValue<EncyclopediaFactionVM>(value, "OwnerBanner");
				}
			}
		}

		// Token: 0x170006B9 RID: 1721
		// (get) Token: 0x06001454 RID: 5204 RVA: 0x00050CBD File Offset: 0x0004EEBD
		// (set) Token: 0x06001455 RID: 5205 RVA: 0x00050CC5 File Offset: 0x0004EEC5
		[DataSourceProperty]
		public EncyclopediaSettlementVM BoundSettlement
		{
			get
			{
				return this._boundSettlement;
			}
			set
			{
				if (value != this._boundSettlement)
				{
					this._boundSettlement = value;
					base.OnPropertyChangedWithValue<EncyclopediaSettlementVM>(value, "BoundSettlement");
				}
			}
		}

		// Token: 0x170006BA RID: 1722
		// (get) Token: 0x06001456 RID: 5206 RVA: 0x00050CE3 File Offset: 0x0004EEE3
		// (set) Token: 0x06001457 RID: 5207 RVA: 0x00050CEB File Offset: 0x0004EEEB
		[DataSourceProperty]
		public bool IsFortification
		{
			get
			{
				return this._isFortification;
			}
			set
			{
				if (value != this._isFortification)
				{
					this._isFortification = value;
					base.OnPropertyChangedWithValue(value, "IsFortification");
				}
			}
		}

		// Token: 0x170006BB RID: 1723
		// (get) Token: 0x06001458 RID: 5208 RVA: 0x00050D09 File Offset: 0x0004EF09
		// (set) Token: 0x06001459 RID: 5209 RVA: 0x00050D11 File Offset: 0x0004EF11
		[DataSourceProperty]
		public bool IsTrackerButtonHighlightEnabled
		{
			get
			{
				return this._isTrackerButtonHighlightEnabled;
			}
			set
			{
				if (value != this._isTrackerButtonHighlightEnabled)
				{
					this._isTrackerButtonHighlightEnabled = value;
					base.OnPropertyChangedWithValue(value, "IsTrackerButtonHighlightEnabled");
				}
			}
		}

		// Token: 0x170006BC RID: 1724
		// (get) Token: 0x0600145A RID: 5210 RVA: 0x00050D2F File Offset: 0x0004EF2F
		// (set) Token: 0x0600145B RID: 5211 RVA: 0x00050D37 File Offset: 0x0004EF37
		[DataSourceProperty]
		public bool HasBoundSettlement
		{
			get
			{
				return this._hasBoundSettlement;
			}
			set
			{
				if (value != this._hasBoundSettlement)
				{
					this._hasBoundSettlement = value;
					base.OnPropertyChangedWithValue(value, "HasBoundSettlement");
				}
			}
		}

		// Token: 0x170006BD RID: 1725
		// (get) Token: 0x0600145C RID: 5212 RVA: 0x00050D55 File Offset: 0x0004EF55
		// (set) Token: 0x0600145D RID: 5213 RVA: 0x00050D5D File Offset: 0x0004EF5D
		[DataSourceProperty]
		public double SettlementCropPosition
		{
			get
			{
				return this._settlementCropPosition;
			}
			set
			{
				if (value != this._settlementCropPosition)
				{
					this._settlementCropPosition = value;
					base.OnPropertyChangedWithValue(value, "SettlementCropPosition");
				}
			}
		}

		// Token: 0x170006BE RID: 1726
		// (get) Token: 0x0600145E RID: 5214 RVA: 0x00050D7B File Offset: 0x0004EF7B
		// (set) Token: 0x0600145F RID: 5215 RVA: 0x00050D83 File Offset: 0x0004EF83
		[DataSourceProperty]
		public string BoundSettlementText
		{
			get
			{
				return this._boundSettlementText;
			}
			set
			{
				if (value != this._boundSettlementText)
				{
					this._boundSettlementText = value;
					base.OnPropertyChangedWithValue<string>(value, "BoundSettlementText");
				}
			}
		}

		// Token: 0x170006BF RID: 1727
		// (get) Token: 0x06001460 RID: 5216 RVA: 0x00050DA6 File Offset: 0x0004EFA6
		// (set) Token: 0x06001461 RID: 5217 RVA: 0x00050DAE File Offset: 0x0004EFAE
		[DataSourceProperty]
		public string TrackText
		{
			get
			{
				return this._trackText;
			}
			set
			{
				if (value != this._trackText)
				{
					this._trackText = value;
					base.OnPropertyChangedWithValue<string>(value, "TrackText");
				}
			}
		}

		// Token: 0x170006C0 RID: 1728
		// (get) Token: 0x06001462 RID: 5218 RVA: 0x00050DD1 File Offset: 0x0004EFD1
		// (set) Token: 0x06001463 RID: 5219 RVA: 0x00050DD9 File Offset: 0x0004EFD9
		[DataSourceProperty]
		public string SettlementPath
		{
			get
			{
				return this._settlementPath;
			}
			set
			{
				if (value != this._settlementPath)
				{
					this._settlementPath = value;
					base.OnPropertyChangedWithValue<string>(value, "SettlementPath");
				}
			}
		}

		// Token: 0x170006C1 RID: 1729
		// (get) Token: 0x06001464 RID: 5220 RVA: 0x00050DFC File Offset: 0x0004EFFC
		// (set) Token: 0x06001465 RID: 5221 RVA: 0x00050E04 File Offset: 0x0004F004
		[DataSourceProperty]
		public string SettlementName
		{
			get
			{
				return this._settlementName;
			}
			set
			{
				if (value != this._settlementName)
				{
					this._settlementName = value;
					base.OnPropertyChangedWithValue<string>(value, "SettlementName");
				}
			}
		}

		// Token: 0x170006C2 RID: 1730
		// (get) Token: 0x06001466 RID: 5222 RVA: 0x00050E27 File Offset: 0x0004F027
		// (set) Token: 0x06001467 RID: 5223 RVA: 0x00050E2F File Offset: 0x0004F02F
		[DataSourceProperty]
		public string InformationText
		{
			get
			{
				return this._informationText;
			}
			set
			{
				if (value != this._informationText)
				{
					this._informationText = value;
					base.OnPropertyChangedWithValue<string>(value, "InformationText");
				}
			}
		}

		// Token: 0x170006C3 RID: 1731
		// (get) Token: 0x06001468 RID: 5224 RVA: 0x00050E52 File Offset: 0x0004F052
		// (set) Token: 0x06001469 RID: 5225 RVA: 0x00050E5A File Offset: 0x0004F05A
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

		// Token: 0x170006C4 RID: 1732
		// (get) Token: 0x0600146A RID: 5226 RVA: 0x00050E78 File Offset: 0x0004F078
		// (set) Token: 0x0600146B RID: 5227 RVA: 0x00050E80 File Offset: 0x0004F080
		[DataSourceProperty]
		public string SettlementsText
		{
			get
			{
				return this._villagesText;
			}
			set
			{
				if (value != this._villagesText)
				{
					this._villagesText = value;
					base.OnPropertyChanged("VillagesText");
				}
			}
		}

		// Token: 0x170006C5 RID: 1733
		// (get) Token: 0x0600146C RID: 5228 RVA: 0x00050EA2 File Offset: 0x0004F0A2
		// (set) Token: 0x0600146D RID: 5229 RVA: 0x00050EAA File Offset: 0x0004F0AA
		[DataSourceProperty]
		public string SettlementImageID
		{
			get
			{
				return this._settlementImageID;
			}
			set
			{
				if (value != this._settlementImageID)
				{
					this._settlementImageID = value;
					base.OnPropertyChangedWithValue<string>(value, "SettlementImageID");
				}
			}
		}

		// Token: 0x170006C6 RID: 1734
		// (get) Token: 0x0600146E RID: 5230 RVA: 0x00050ECD File Offset: 0x0004F0CD
		// (set) Token: 0x0600146F RID: 5231 RVA: 0x00050ED5 File Offset: 0x0004F0D5
		[DataSourceProperty]
		public string NotableCharactersText
		{
			get
			{
				return this._notableCharactersText;
			}
			set
			{
				if (value != this._notableCharactersText)
				{
					this._notableCharactersText = value;
					base.OnPropertyChangedWithValue<string>(value, "NotableCharactersText");
				}
			}
		}

		// Token: 0x170006C7 RID: 1735
		// (get) Token: 0x06001470 RID: 5232 RVA: 0x00050EF8 File Offset: 0x0004F0F8
		// (set) Token: 0x06001471 RID: 5233 RVA: 0x00050F00 File Offset: 0x0004F100
		[DataSourceProperty]
		public int SettlementType
		{
			get
			{
				return this._settlementType;
			}
			set
			{
				if (value != this._settlementType)
				{
					this._settlementType = value;
					base.OnPropertyChangedWithValue(value, "SettlementType");
				}
			}
		}

		// Token: 0x170006C8 RID: 1736
		// (get) Token: 0x06001472 RID: 5234 RVA: 0x00050F1E File Offset: 0x0004F11E
		// (set) Token: 0x06001473 RID: 5235 RVA: 0x00050F26 File Offset: 0x0004F126
		[DataSourceProperty]
		public MBBindingList<EncyclopediaHistoryEventVM> History
		{
			get
			{
				return this._history;
			}
			set
			{
				if (value != this._history)
				{
					this._history = value;
					base.OnPropertyChangedWithValue<MBBindingList<EncyclopediaHistoryEventVM>>(value, "History");
				}
			}
		}

		// Token: 0x170006C9 RID: 1737
		// (get) Token: 0x06001474 RID: 5236 RVA: 0x00050F44 File Offset: 0x0004F144
		// (set) Token: 0x06001475 RID: 5237 RVA: 0x00050F4C File Offset: 0x0004F14C
		[DataSourceProperty]
		public MBBindingList<EncyclopediaSettlementVM> Settlements
		{
			get
			{
				return this._settlements;
			}
			set
			{
				if (value != this._settlements)
				{
					this._settlements = value;
					base.OnPropertyChanged("Villages");
				}
			}
		}

		// Token: 0x170006CA RID: 1738
		// (get) Token: 0x06001476 RID: 5238 RVA: 0x00050F69 File Offset: 0x0004F169
		// (set) Token: 0x06001477 RID: 5239 RVA: 0x00050F71 File Offset: 0x0004F171
		[DataSourceProperty]
		public MBBindingList<HeroVM> NotableCharacters
		{
			get
			{
				return this._notableCharacters;
			}
			set
			{
				if (value != this._notableCharacters)
				{
					this._notableCharacters = value;
					base.OnPropertyChangedWithValue<MBBindingList<HeroVM>>(value, "NotableCharacters");
				}
			}
		}

		// Token: 0x170006CB RID: 1739
		// (get) Token: 0x06001478 RID: 5240 RVA: 0x00050F8F File Offset: 0x0004F18F
		// (set) Token: 0x06001479 RID: 5241 RVA: 0x00050F97 File Offset: 0x0004F197
		[DataSourceProperty]
		public HintViewModel ShowInMapHint
		{
			get
			{
				return this._showInMapHint;
			}
			set
			{
				if (value != this._showInMapHint)
				{
					this._showInMapHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "ShowInMapHint");
				}
			}
		}

		// Token: 0x170006CC RID: 1740
		// (get) Token: 0x0600147A RID: 5242 RVA: 0x00050FB5 File Offset: 0x0004F1B5
		// (set) Token: 0x0600147B RID: 5243 RVA: 0x00050FBD File Offset: 0x0004F1BD
		[DataSourceProperty]
		public MBBindingList<EncyclopediaSettlementPageStatItemVM> LeftSideProperties
		{
			get
			{
				return this._leftSideProperties;
			}
			set
			{
				if (value != this._leftSideProperties)
				{
					this._leftSideProperties = value;
					base.OnPropertyChangedWithValue<MBBindingList<EncyclopediaSettlementPageStatItemVM>>(value, "LeftSideProperties");
				}
			}
		}

		// Token: 0x170006CD RID: 1741
		// (get) Token: 0x0600147C RID: 5244 RVA: 0x00050FDB File Offset: 0x0004F1DB
		// (set) Token: 0x0600147D RID: 5245 RVA: 0x00050FE3 File Offset: 0x0004F1E3
		[DataSourceProperty]
		public MBBindingList<EncyclopediaSettlementPageStatItemVM> RightSideProperties
		{
			get
			{
				return this._rightSideProperties;
			}
			set
			{
				if (value != this._rightSideProperties)
				{
					this._rightSideProperties = value;
					base.OnPropertyChangedWithValue<MBBindingList<EncyclopediaSettlementPageStatItemVM>>(value, "RightSideProperties");
				}
			}
		}

		// Token: 0x170006CE RID: 1742
		// (get) Token: 0x0600147E RID: 5246 RVA: 0x00051001 File Offset: 0x0004F201
		// (set) Token: 0x0600147F RID: 5247 RVA: 0x00051009 File Offset: 0x0004F209
		[DataSourceProperty]
		public string NameText
		{
			get
			{
				return this._nameText;
			}
			set
			{
				if (value != this._nameText)
				{
					this._nameText = value;
					base.OnPropertyChangedWithValue<string>(value, "NameText");
				}
			}
		}

		// Token: 0x170006CF RID: 1743
		// (get) Token: 0x06001480 RID: 5248 RVA: 0x0005102C File Offset: 0x0004F22C
		// (set) Token: 0x06001481 RID: 5249 RVA: 0x00051034 File Offset: 0x0004F234
		[DataSourceProperty]
		public string CultureText
		{
			get
			{
				return this._cultureText;
			}
			set
			{
				if (value != this._cultureText)
				{
					this._cultureText = value;
					base.OnPropertyChangedWithValue<string>(value, "CultureText");
				}
			}
		}

		// Token: 0x170006D0 RID: 1744
		// (get) Token: 0x06001482 RID: 5250 RVA: 0x00051057 File Offset: 0x0004F257
		// (set) Token: 0x06001483 RID: 5251 RVA: 0x0005105F File Offset: 0x0004F25F
		[DataSourceProperty]
		public string OwnerText
		{
			get
			{
				return this._ownerText;
			}
			set
			{
				if (value != this._ownerText)
				{
					this._ownerText = value;
					base.OnPropertyChangedWithValue<string>(value, "OwnerText");
				}
			}
		}

		// Token: 0x170006D1 RID: 1745
		// (get) Token: 0x06001484 RID: 5252 RVA: 0x00051082 File Offset: 0x0004F282
		// (set) Token: 0x06001485 RID: 5253 RVA: 0x0005108A File Offset: 0x0004F28A
		[DataSourceProperty]
		public bool IsVisualTrackerSelected
		{
			get
			{
				return this._isVisualTrackerSelected;
			}
			set
			{
				if (value != this._isVisualTrackerSelected)
				{
					this._isVisualTrackerSelected = value;
					base.OnPropertyChangedWithValue(value, "IsVisualTrackerSelected");
				}
			}
		}

		// Token: 0x04000946 RID: 2374
		protected readonly Settlement _settlement;

		// Token: 0x04000947 RID: 2375
		private int _settlementType;

		// Token: 0x04000948 RID: 2376
		private MBBindingList<EncyclopediaHistoryEventVM> _history;

		// Token: 0x04000949 RID: 2377
		private MBBindingList<EncyclopediaSettlementVM> _settlements;

		// Token: 0x0400094A RID: 2378
		private EncyclopediaSettlementVM _boundSettlement;

		// Token: 0x0400094B RID: 2379
		private MBBindingList<HeroVM> _notableCharacters;

		// Token: 0x0400094C RID: 2380
		private EncyclopediaFactionVM _ownerBanner;

		// Token: 0x0400094D RID: 2381
		private HintViewModel _showInMapHint;

		// Token: 0x0400094E RID: 2382
		private MBBindingList<EncyclopediaSettlementPageStatItemVM> _leftSideProperties;

		// Token: 0x0400094F RID: 2383
		private MBBindingList<EncyclopediaSettlementPageStatItemVM> _rightSideProperties;

		// Token: 0x04000950 RID: 2384
		private HeroVM _owner;

		// Token: 0x04000951 RID: 2385
		private string _ownerText;

		// Token: 0x04000952 RID: 2386
		private string _nameText;

		// Token: 0x04000953 RID: 2387
		private string _cultureText;

		// Token: 0x04000954 RID: 2388
		private string _villagesText;

		// Token: 0x04000955 RID: 2389
		private string _notableCharactersText;

		// Token: 0x04000956 RID: 2390
		private string _settlementPath;

		// Token: 0x04000957 RID: 2391
		private string _settlementName;

		// Token: 0x04000958 RID: 2392
		private string _informationText;

		// Token: 0x04000959 RID: 2393
		private string _settlementImageID;

		// Token: 0x0400095A RID: 2394
		private string _boundSettlementText;

		// Token: 0x0400095B RID: 2395
		private string _trackText;

		// Token: 0x0400095C RID: 2396
		private double _settlementCropPosition;

		// Token: 0x0400095D RID: 2397
		private bool _isFortification;

		// Token: 0x0400095E RID: 2398
		private bool _isVisualTrackerSelected;

		// Token: 0x0400095F RID: 2399
		private bool _hasBoundSettlement;

		// Token: 0x04000960 RID: 2400
		private bool _isTrackerButtonHighlightEnabled;

		// Token: 0x0200023A RID: 570
		private enum SettlementTypes
		{
			// Token: 0x040011FF RID: 4607
			Town,
			// Token: 0x04001200 RID: 4608
			LoneVillage,
			// Token: 0x04001201 RID: 4609
			VillageWithCastle
		}
	}
}
