using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes
{
	// Token: 0x02000083 RID: 131
	public class StartAllianceDecisionItemVM : DecisionItemBaseVM
	{
		// Token: 0x17000374 RID: 884
		// (get) Token: 0x06000B02 RID: 2818 RVA: 0x0002EDF5 File Offset: 0x0002CFF5
		private Kingdom _sourceFaction
		{
			get
			{
				return Hero.MainHero.Clan.Kingdom;
			}
		}

		// Token: 0x17000375 RID: 885
		// (get) Token: 0x06000B03 RID: 2819 RVA: 0x0002EE06 File Offset: 0x0002D006
		public IFaction TargetFaction
		{
			get
			{
				return (this._decision as StartAllianceDecision).KingdomToStartAllianceWith;
			}
		}

		// Token: 0x06000B04 RID: 2820 RVA: 0x0002EE18 File Offset: 0x0002D018
		public StartAllianceDecisionItemVM(StartAllianceDecision decision, Action onDecisionOver) : base(decision, onDecisionOver)
		{
			this._startAllianceDecision = decision;
			base.DecisionType = 7;
		}

		// Token: 0x06000B05 RID: 2821 RVA: 0x0002EE30 File Offset: 0x0002D030
		protected override void InitValues()
		{
			base.InitValues();
			TextObject textObject = GameTexts.FindText("str_kingdom_decision_start_alliance", null);
			this.NameText = textObject.ToString();
			TextObject textObject2 = GameTexts.FindText("str_kingdom_decision_start_alliance_desc", null);
			textObject2.SetTextVariable("FACTION", this.TargetFaction.Name);
			this.StartAllianceDescriptionText = textObject2.ToString();
			this.SourceFactionBanner = new BannerImageIdentifierVM(this._sourceFaction.Banner, true);
			this.TargetFactionBanner = new BannerImageIdentifierVM(this.TargetFaction.Banner, true);
			this.LeaderText = GameTexts.FindText("str_leader", null).ToString();
			this.SourceFactionLeader = new HeroVM(this._sourceFaction.Leader, false);
			this.TargetFactionLeader = new HeroVM(this.TargetFaction.Leader, false);
			this.ComparedStats = new MBBindingList<KingdomWarComparableStatVM>();
			Kingdom kingdom = this.TargetFaction as Kingdom;
			string faction1Color = Color.FromUint(this._sourceFaction.Color).ToString();
			string faction2Color = Color.FromUint(kingdom.Color).ToString();
			KingdomWarComparableStatVM item = new KingdomWarComparableStatVM((int)this._sourceFaction.CurrentTotalStrength, (int)kingdom.CurrentTotalStrength, GameTexts.FindText("str_strength", null), faction1Color, faction2Color, 10000, null, null);
			this.ComparedStats.Add(item);
			KingdomWarComparableStatVM item2 = new KingdomWarComparableStatVM(this._sourceFaction.Armies.Count, kingdom.Armies.Count, GameTexts.FindText("str_armies", null), faction1Color, faction2Color, 5, null, null);
			this.ComparedStats.Add(item2);
			int faction1Stat = this._sourceFaction.Settlements.Count((Settlement settlement) => settlement.IsTown);
			int faction2Stat = kingdom.Settlements.Count((Settlement settlement) => settlement.IsTown);
			KingdomWarComparableStatVM item3 = new KingdomWarComparableStatVM(faction1Stat, faction2Stat, GameTexts.FindText("str_towns", null), faction1Color, faction2Color, 50, null, null);
			this.ComparedStats.Add(item3);
			int faction1Stat2 = this._sourceFaction.Settlements.Count((Settlement settlement) => settlement.IsCastle);
			int faction2Stat2 = this.TargetFaction.Settlements.Count((Settlement settlement) => settlement.IsCastle);
			KingdomWarComparableStatVM item4 = new KingdomWarComparableStatVM(faction1Stat2, faction2Stat2, GameTexts.FindText("str_castles", null), faction1Color, faction2Color, 50, null, null);
			this.ComparedStats.Add(item4);
			this.TargetFactionOtherWars = new MBBindingList<KingdomDiplomacyFactionItemVM>();
			foreach (StanceLink stanceLink in FactionHelper.GetStances(this.TargetFaction))
			{
				if (stanceLink.IsAtWar && stanceLink.Faction1 != this._sourceFaction && stanceLink.Faction2 != this._sourceFaction && (stanceLink.Faction1.IsKingdomFaction || stanceLink.Faction1.Leader == Hero.MainHero) && (stanceLink.Faction2.IsKingdomFaction || stanceLink.Faction2.Leader == Hero.MainHero) && !stanceLink.Faction1.IsRebelClan && !stanceLink.Faction2.IsRebelClan && !stanceLink.Faction1.IsBanditFaction && !stanceLink.Faction2.IsBanditFaction)
				{
					this.TargetFactionOtherWars.Add(new KingdomDiplomacyFactionItemVM((stanceLink.Faction1 == this.TargetFaction) ? stanceLink.Faction2 : stanceLink.Faction1));
				}
			}
			this.IsTargetFactionOtherWarsVisible = (this.TargetFactionOtherWars.Count > 0);
		}

		// Token: 0x17000376 RID: 886
		// (get) Token: 0x06000B06 RID: 2822 RVA: 0x0002F21C File Offset: 0x0002D41C
		// (set) Token: 0x06000B07 RID: 2823 RVA: 0x0002F224 File Offset: 0x0002D424
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

		// Token: 0x17000377 RID: 887
		// (get) Token: 0x06000B08 RID: 2824 RVA: 0x0002F247 File Offset: 0x0002D447
		// (set) Token: 0x06000B09 RID: 2825 RVA: 0x0002F24F File Offset: 0x0002D44F
		[DataSourceProperty]
		public string StartAllianceDescriptionText
		{
			get
			{
				return this._startAllianceDescriptionText;
			}
			set
			{
				if (value != this._startAllianceDescriptionText)
				{
					this._startAllianceDescriptionText = value;
					base.OnPropertyChangedWithValue<string>(value, "StartAllianceDescriptionText");
				}
			}
		}

		// Token: 0x17000378 RID: 888
		// (get) Token: 0x06000B0A RID: 2826 RVA: 0x0002F272 File Offset: 0x0002D472
		// (set) Token: 0x06000B0B RID: 2827 RVA: 0x0002F27A File Offset: 0x0002D47A
		[DataSourceProperty]
		public BannerImageIdentifierVM SourceFactionBanner
		{
			get
			{
				return this._sourceFactionBanner;
			}
			set
			{
				if (value != this._sourceFactionBanner)
				{
					this._sourceFactionBanner = value;
					base.OnPropertyChangedWithValue<BannerImageIdentifierVM>(value, "SourceFactionBanner");
				}
			}
		}

		// Token: 0x17000379 RID: 889
		// (get) Token: 0x06000B0C RID: 2828 RVA: 0x0002F298 File Offset: 0x0002D498
		// (set) Token: 0x06000B0D RID: 2829 RVA: 0x0002F2A0 File Offset: 0x0002D4A0
		[DataSourceProperty]
		public BannerImageIdentifierVM TargetFactionBanner
		{
			get
			{
				return this._targetFactionBanner;
			}
			set
			{
				if (value != this._targetFactionBanner)
				{
					this._targetFactionBanner = value;
					base.OnPropertyChangedWithValue<BannerImageIdentifierVM>(value, "TargetFactionBanner");
				}
			}
		}

		// Token: 0x1700037A RID: 890
		// (get) Token: 0x06000B0E RID: 2830 RVA: 0x0002F2BE File Offset: 0x0002D4BE
		// (set) Token: 0x06000B0F RID: 2831 RVA: 0x0002F2C6 File Offset: 0x0002D4C6
		[DataSourceProperty]
		public MBBindingList<KingdomWarComparableStatVM> ComparedStats
		{
			get
			{
				return this._comparedStats;
			}
			set
			{
				if (value != this._comparedStats)
				{
					this._comparedStats = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomWarComparableStatVM>>(value, "ComparedStats");
				}
			}
		}

		// Token: 0x1700037B RID: 891
		// (get) Token: 0x06000B10 RID: 2832 RVA: 0x0002F2E4 File Offset: 0x0002D4E4
		// (set) Token: 0x06000B11 RID: 2833 RVA: 0x0002F2EC File Offset: 0x0002D4EC
		[DataSourceProperty]
		public string LeaderText
		{
			get
			{
				return this._leaderText;
			}
			set
			{
				if (value != this._leaderText)
				{
					this._leaderText = value;
					base.OnPropertyChangedWithValue<string>(value, "LeaderText");
				}
			}
		}

		// Token: 0x1700037C RID: 892
		// (get) Token: 0x06000B12 RID: 2834 RVA: 0x0002F30F File Offset: 0x0002D50F
		// (set) Token: 0x06000B13 RID: 2835 RVA: 0x0002F317 File Offset: 0x0002D517
		[DataSourceProperty]
		public HeroVM SourceFactionLeader
		{
			get
			{
				return this._sourceFactionLeader;
			}
			set
			{
				if (value != this._sourceFactionLeader)
				{
					this._sourceFactionLeader = value;
					base.OnPropertyChangedWithValue<HeroVM>(value, "SourceFactionLeader");
				}
			}
		}

		// Token: 0x1700037D RID: 893
		// (get) Token: 0x06000B14 RID: 2836 RVA: 0x0002F335 File Offset: 0x0002D535
		// (set) Token: 0x06000B15 RID: 2837 RVA: 0x0002F33D File Offset: 0x0002D53D
		[DataSourceProperty]
		public HeroVM TargetFactionLeader
		{
			get
			{
				return this._targetFactionLeader;
			}
			set
			{
				if (value != this._targetFactionLeader)
				{
					this._targetFactionLeader = value;
					base.OnPropertyChangedWithValue<HeroVM>(value, "TargetFactionLeader");
				}
			}
		}

		// Token: 0x1700037E RID: 894
		// (get) Token: 0x06000B16 RID: 2838 RVA: 0x0002F35B File Offset: 0x0002D55B
		// (set) Token: 0x06000B17 RID: 2839 RVA: 0x0002F363 File Offset: 0x0002D563
		[DataSourceProperty]
		public bool IsTargetFactionOtherWarsVisible
		{
			get
			{
				return this._isTargetFactionOtherWarsVisible;
			}
			set
			{
				if (value != this._isTargetFactionOtherWarsVisible)
				{
					this._isTargetFactionOtherWarsVisible = value;
					base.OnPropertyChangedWithValue(value, "IsTargetFactionOtherWarsVisible");
				}
			}
		}

		// Token: 0x1700037F RID: 895
		// (get) Token: 0x06000B18 RID: 2840 RVA: 0x0002F381 File Offset: 0x0002D581
		// (set) Token: 0x06000B19 RID: 2841 RVA: 0x0002F389 File Offset: 0x0002D589
		[DataSourceProperty]
		public MBBindingList<KingdomDiplomacyFactionItemVM> TargetFactionOtherWars
		{
			get
			{
				return this._targetFactionOtherWars;
			}
			set
			{
				if (value != this._targetFactionOtherWars)
				{
					this._targetFactionOtherWars = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomDiplomacyFactionItemVM>>(value, "TargetFactionOtherWars");
				}
			}
		}

		// Token: 0x040004E6 RID: 1254
		private readonly StartAllianceDecision _startAllianceDecision;

		// Token: 0x040004E7 RID: 1255
		private string _nameText;

		// Token: 0x040004E8 RID: 1256
		private string _startAllianceDescriptionText;

		// Token: 0x040004E9 RID: 1257
		private BannerImageIdentifierVM _sourceFactionBanner;

		// Token: 0x040004EA RID: 1258
		private BannerImageIdentifierVM _targetFactionBanner;

		// Token: 0x040004EB RID: 1259
		private string _leaderText;

		// Token: 0x040004EC RID: 1260
		private HeroVM _sourceFactionLeader;

		// Token: 0x040004ED RID: 1261
		private HeroVM _targetFactionLeader;

		// Token: 0x040004EE RID: 1262
		private MBBindingList<KingdomWarComparableStatVM> _comparedStats;

		// Token: 0x040004EF RID: 1263
		private bool _isTargetFactionOtherWarsVisible;

		// Token: 0x040004F0 RID: 1264
		private MBBindingList<KingdomDiplomacyFactionItemVM> _targetFactionOtherWars;
	}
}
