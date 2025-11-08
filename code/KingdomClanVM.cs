using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans
{
	// Token: 0x02000088 RID: 136
	public class KingdomClanVM : KingdomCategoryVM
	{
		// Token: 0x06000B43 RID: 2883 RVA: 0x0002F500 File Offset: 0x0002D700
		public KingdomClanVM(Action<KingdomDecision> forceDecide)
		{
			this._forceDecide = forceDecide;
			this.SupportHint = new HintViewModel();
			this.ExpelHint = new HintViewModel();
			this._clans = new MBBindingList<KingdomClanItemVM>();
			base.IsAcceptableItemSelected = false;
			this.RefreshClanList();
			base.NotificationCount = 0;
			this.SupportCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfSupportingClan();
			this.ExpelCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfExpellingClan(Clan.PlayerClan);
			TextObject hintText;
			this.CanSupportCurrentClan = this.GetCanSupportCurrentClanWithReason(this.SupportCost, out hintText);
			this.SupportHint.HintText = hintText;
			TextObject hintText2;
			this.CanExpelCurrentClan = this.GetCanExpelCurrentClanWithReason(this._isThereAPendingDecisionToExpelThisClan, this.ExpelCost, out hintText2);
			this.ExpelHint.HintText = hintText2;
			this.ClanSortController = new KingdomClanSortControllerVM(ref this._clans);
			CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, new Action<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>(this.OnClanChangedKingdom));
			this.RefreshValues();
		}

		// Token: 0x06000B44 RID: 2884 RVA: 0x0002F5FC File Offset: 0x0002D7FC
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.SupportText = new TextObject("{=N63XYX2r}Support", null).ToString();
			this.NameText = GameTexts.FindText("str_scoreboard_header", "name").ToString();
			this.InfluenceText = GameTexts.FindText("str_influence", null).ToString();
			this.FiefsText = GameTexts.FindText("str_fiefs", null).ToString();
			this.MembersText = GameTexts.FindText("str_members", null).ToString();
			this.BannerText = GameTexts.FindText("str_banner", null).ToString();
			this.TypeText = GameTexts.FindText("str_sort_by_type_label", null).ToString();
			base.CategoryNameText = new TextObject("{=j4F7tTzy}Clan", null).ToString();
			base.NoItemSelectedText = GameTexts.FindText("str_kingdom_no_clan_selected", null).ToString();
			this.SupportActionExplanationText = GameTexts.FindText("str_support_clan_action_explanation", null).ToString();
			this.ExpelActionExplanationText = GameTexts.FindText("str_expel_clan_action_explanation", null).ToString();
		}

		// Token: 0x06000B45 RID: 2885 RVA: 0x0002F708 File Offset: 0x0002D908
		private void SetCurrentSelectedClan(KingdomClanItemVM clan)
		{
			if (clan != this.CurrentSelectedClan)
			{
				if (this.CurrentSelectedClan != null)
				{
					this.CurrentSelectedClan.IsSelected = false;
				}
				this.CurrentSelectedClan = clan;
				this.CurrentSelectedClan.IsSelected = true;
				this.SupportCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfSupportingClan();
				this._isThereAPendingDecisionToExpelThisClan = Clan.PlayerClan.Kingdom.UnresolvedDecisions.Any(delegate(KingdomDecision x)
				{
					ExpelClanFromKingdomDecision expelClanFromKingdomDecision;
					return (expelClanFromKingdomDecision = (x as ExpelClanFromKingdomDecision)) != null && expelClanFromKingdomDecision.ClanToExpel == this.CurrentSelectedClan.Clan && !x.ShouldBeCancelled();
				});
				TextObject hintText;
				this.CanExpelCurrentClan = this.GetCanExpelCurrentClanWithReason(this._isThereAPendingDecisionToExpelThisClan, this.ExpelCost, out hintText);
				this.ExpelHint.HintText = hintText;
				if (this._isThereAPendingDecisionToExpelThisClan)
				{
					this.ExpelActionText = GameTexts.FindText("str_resolve", null).ToString();
					this.ExpelActionExplanationText = GameTexts.FindText("str_resolve_explanation", null).ToString();
					this.ExpelCost = 0;
					return;
				}
				this.ExpelActionText = GameTexts.FindText("str_policy_propose", null).ToString();
				this.ExpelCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfExpellingClan(Clan.PlayerClan);
				TextObject hintText2;
				this.CanSupportCurrentClan = this.GetCanSupportCurrentClanWithReason(this.SupportCost, out hintText2);
				this.SupportHint.HintText = hintText2;
				this.ExpelActionExplanationText = GameTexts.FindText("str_expel_clan_action_explanation", null).SetTextVariable("SUPPORT", this.CalculateExpelLikelihood(this.CurrentSelectedClan)).ToString();
				base.IsAcceptableItemSelected = (this.CurrentSelectedClan != null);
			}
		}

		// Token: 0x06000B46 RID: 2886 RVA: 0x0002F87C File Offset: 0x0002DA7C
		private bool GetCanSupportCurrentClanWithReason(int supportCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			if (Hero.MainHero.Clan.Influence < (float)supportCost)
			{
				disabledReason = GameTexts.FindText("str_warning_you_dont_have_enough_influence", null);
				return false;
			}
			if (this.CurrentSelectedClan.Clan == Clan.PlayerClan)
			{
				disabledReason = GameTexts.FindText("str_cannot_support_your_clan", null);
				return false;
			}
			if (Hero.MainHero.Clan.IsUnderMercenaryService)
			{
				disabledReason = GameTexts.FindText("str_mercenaries_cannot_support_clans", null);
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x06000B47 RID: 2887 RVA: 0x0002F904 File Offset: 0x0002DB04
		private bool GetCanExpelCurrentClanWithReason(bool isThereAPendingDecision, int expelCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			if (Hero.MainHero.Clan.IsUnderMercenaryService)
			{
				disabledReason = GameTexts.FindText("str_mercenaries_cannot_expel_clans", null);
				return false;
			}
			if (!isThereAPendingDecision)
			{
				if (Hero.MainHero.Clan.Influence < (float)expelCost)
				{
					disabledReason = GameTexts.FindText("str_warning_you_dont_have_enough_influence", null);
					return false;
				}
				if (this.CurrentSelectedClan.Clan == Clan.PlayerClan)
				{
					disabledReason = GameTexts.FindText("str_cannot_expel_your_clan", null);
					return false;
				}
				Clan clan = this.CurrentSelectedClan.Clan;
				Kingdom kingdom = this.CurrentSelectedClan.Clan.Kingdom;
				if (clan == ((kingdom != null) ? kingdom.RulingClan : null))
				{
					disabledReason = GameTexts.FindText("str_cannot_expel_ruling_clan", null);
					return false;
				}
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x06000B48 RID: 2888 RVA: 0x0002F9C8 File Offset: 0x0002DBC8
		public void RefreshClan()
		{
			this.RefreshClanList();
			foreach (KingdomClanItemVM kingdomClanItemVM in this.Clans)
			{
				kingdomClanItemVM.Refresh();
			}
		}

		// Token: 0x06000B49 RID: 2889 RVA: 0x0002FA18 File Offset: 0x0002DC18
		public void SelectClan(Clan clan)
		{
			foreach (KingdomClanItemVM kingdomClanItemVM in this.Clans)
			{
				if (kingdomClanItemVM.Clan == clan)
				{
					this.OnClanSelection(kingdomClanItemVM);
					break;
				}
			}
		}

		// Token: 0x06000B4A RID: 2890 RVA: 0x0002FA70 File Offset: 0x0002DC70
		private void OnClanSelection(KingdomClanItemVM clan)
		{
			if (this._currentSelectedClan != clan)
			{
				this.SetCurrentSelectedClan(clan);
			}
		}

		// Token: 0x06000B4B RID: 2891 RVA: 0x0002FA84 File Offset: 0x0002DC84
		private void ExecuteExpelCurrentClan()
		{
			if (Hero.MainHero.Clan.Influence >= (float)this.ExpelCost)
			{
				KingdomDecision kingdomDecision = new ExpelClanFromKingdomDecision(Clan.PlayerClan, this._currentSelectedClan.Clan);
				Clan.PlayerClan.Kingdom.AddDecision(kingdomDecision, false);
				this._forceDecide(kingdomDecision);
			}
		}

		// Token: 0x06000B4C RID: 2892 RVA: 0x0002FADC File Offset: 0x0002DCDC
		private void ExecuteSupport()
		{
			if (Hero.MainHero.Clan.Influence >= (float)this.SupportCost)
			{
				this._currentSelectedClan.Clan.OnSupportedByClan(Hero.MainHero.Clan);
				Clan clan = this._currentSelectedClan.Clan;
				this.RefreshClan();
				this.SelectClan(clan);
			}
		}

		// Token: 0x06000B4D RID: 2893 RVA: 0x0002FB34 File Offset: 0x0002DD34
		private int CalculateExpelLikelihood(KingdomClanItemVM clan)
		{
			return MathF.Round(new KingdomElection(new ExpelClanFromKingdomDecision(Clan.PlayerClan, clan.Clan)).GetLikelihoodForSponsor(Clan.PlayerClan) * 100f);
		}

		// Token: 0x06000B4E RID: 2894 RVA: 0x0002FB60 File Offset: 0x0002DD60
		private void RefreshClanList()
		{
			this.Clans.Clear();
			if (Clan.PlayerClan.Kingdom != null)
			{
				foreach (Clan clan in Clan.PlayerClan.Kingdom.Clans)
				{
					this.Clans.Add(new KingdomClanItemVM(clan, new Action<KingdomClanItemVM>(this.OnClanSelection)));
				}
			}
			if (this.Clans.Count > 0)
			{
				this.SetCurrentSelectedClan(this.Clans.FirstOrDefault<KingdomClanItemVM>());
			}
			if (this.ClanSortController != null)
			{
				this.ClanSortController.SortByCurrentState();
			}
		}

		// Token: 0x06000B4F RID: 2895 RVA: 0x0002FC1C File Offset: 0x0002DE1C
		public override void OnFinalize()
		{
			base.OnFinalize();
			CampaignEvents.OnClanChangedKingdomEvent.ClearListeners(this);
		}

		// Token: 0x06000B50 RID: 2896 RVA: 0x0002FC2F File Offset: 0x0002DE2F
		private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification)
		{
			if (clan != Clan.PlayerClan && (oldKingdom == Clan.PlayerClan.Kingdom || newKingdom == Clan.PlayerClan.Kingdom))
			{
				this.RefreshClanList();
			}
		}

		// Token: 0x17000394 RID: 916
		// (get) Token: 0x06000B51 RID: 2897 RVA: 0x0002FC59 File Offset: 0x0002DE59
		// (set) Token: 0x06000B52 RID: 2898 RVA: 0x0002FC61 File Offset: 0x0002DE61
		[DataSourceProperty]
		public KingdomClanSortControllerVM ClanSortController
		{
			get
			{
				return this._clanSortController;
			}
			set
			{
				if (value != this._clanSortController)
				{
					this._clanSortController = value;
					base.OnPropertyChangedWithValue<KingdomClanSortControllerVM>(value, "ClanSortController");
				}
			}
		}

		// Token: 0x17000395 RID: 917
		// (get) Token: 0x06000B53 RID: 2899 RVA: 0x0002FC7F File Offset: 0x0002DE7F
		// (set) Token: 0x06000B54 RID: 2900 RVA: 0x0002FC87 File Offset: 0x0002DE87
		[DataSourceProperty]
		public KingdomClanItemVM CurrentSelectedClan
		{
			get
			{
				return this._currentSelectedClan;
			}
			set
			{
				if (value != this._currentSelectedClan)
				{
					this._currentSelectedClan = value;
					base.OnPropertyChangedWithValue<KingdomClanItemVM>(value, "CurrentSelectedClan");
				}
			}
		}

		// Token: 0x17000396 RID: 918
		// (get) Token: 0x06000B55 RID: 2901 RVA: 0x0002FCA5 File Offset: 0x0002DEA5
		// (set) Token: 0x06000B56 RID: 2902 RVA: 0x0002FCAD File Offset: 0x0002DEAD
		[DataSourceProperty]
		public string ExpelActionExplanationText
		{
			get
			{
				return this._expelActionExplanationText;
			}
			set
			{
				if (value != this._expelActionExplanationText)
				{
					this._expelActionExplanationText = value;
					base.OnPropertyChangedWithValue<string>(value, "ExpelActionExplanationText");
				}
			}
		}

		// Token: 0x17000397 RID: 919
		// (get) Token: 0x06000B57 RID: 2903 RVA: 0x0002FCD0 File Offset: 0x0002DED0
		// (set) Token: 0x06000B58 RID: 2904 RVA: 0x0002FCD8 File Offset: 0x0002DED8
		[DataSourceProperty]
		public string SupportActionExplanationText
		{
			get
			{
				return this._supportActionExplanationText;
			}
			set
			{
				if (value != this._supportActionExplanationText)
				{
					this._supportActionExplanationText = value;
					base.OnPropertyChangedWithValue<string>(value, "SupportActionExplanationText");
				}
			}
		}

		// Token: 0x17000398 RID: 920
		// (get) Token: 0x06000B59 RID: 2905 RVA: 0x0002FCFB File Offset: 0x0002DEFB
		// (set) Token: 0x06000B5A RID: 2906 RVA: 0x0002FD03 File Offset: 0x0002DF03
		[DataSourceProperty]
		public string BannerText
		{
			get
			{
				return this._bannerText;
			}
			set
			{
				if (value != this._bannerText)
				{
					this._bannerText = value;
					base.OnPropertyChangedWithValue<string>(value, "BannerText");
				}
			}
		}

		// Token: 0x17000399 RID: 921
		// (get) Token: 0x06000B5B RID: 2907 RVA: 0x0002FD26 File Offset: 0x0002DF26
		// (set) Token: 0x06000B5C RID: 2908 RVA: 0x0002FD2E File Offset: 0x0002DF2E
		[DataSourceProperty]
		public string TypeText
		{
			get
			{
				return this._typeText;
			}
			set
			{
				if (value != this._typeText)
				{
					this._typeText = value;
					base.OnPropertyChangedWithValue<string>(value, "TypeText");
				}
			}
		}

		// Token: 0x1700039A RID: 922
		// (get) Token: 0x06000B5D RID: 2909 RVA: 0x0002FD51 File Offset: 0x0002DF51
		// (set) Token: 0x06000B5E RID: 2910 RVA: 0x0002FD59 File Offset: 0x0002DF59
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

		// Token: 0x1700039B RID: 923
		// (get) Token: 0x06000B5F RID: 2911 RVA: 0x0002FD7C File Offset: 0x0002DF7C
		// (set) Token: 0x06000B60 RID: 2912 RVA: 0x0002FD84 File Offset: 0x0002DF84
		[DataSourceProperty]
		public string InfluenceText
		{
			get
			{
				return this._influenceText;
			}
			set
			{
				if (value != this._influenceText)
				{
					this._influenceText = value;
					base.OnPropertyChangedWithValue<string>(value, "InfluenceText");
				}
			}
		}

		// Token: 0x1700039C RID: 924
		// (get) Token: 0x06000B61 RID: 2913 RVA: 0x0002FDA7 File Offset: 0x0002DFA7
		// (set) Token: 0x06000B62 RID: 2914 RVA: 0x0002FDAF File Offset: 0x0002DFAF
		[DataSourceProperty]
		public string FiefsText
		{
			get
			{
				return this._fiefsText;
			}
			set
			{
				if (value != this._fiefsText)
				{
					this._fiefsText = value;
					base.OnPropertyChangedWithValue<string>(value, "FiefsText");
				}
			}
		}

		// Token: 0x1700039D RID: 925
		// (get) Token: 0x06000B63 RID: 2915 RVA: 0x0002FDD2 File Offset: 0x0002DFD2
		// (set) Token: 0x06000B64 RID: 2916 RVA: 0x0002FDDA File Offset: 0x0002DFDA
		[DataSourceProperty]
		public string MembersText
		{
			get
			{
				return this._membersText;
			}
			set
			{
				if (value != this._membersText)
				{
					this._membersText = value;
					base.OnPropertyChangedWithValue<string>(value, "MembersText");
				}
			}
		}

		// Token: 0x1700039E RID: 926
		// (get) Token: 0x06000B65 RID: 2917 RVA: 0x0002FDFD File Offset: 0x0002DFFD
		// (set) Token: 0x06000B66 RID: 2918 RVA: 0x0002FE05 File Offset: 0x0002E005
		[DataSourceProperty]
		public MBBindingList<KingdomClanItemVM> Clans
		{
			get
			{
				return this._clans;
			}
			set
			{
				if (value != this._clans)
				{
					this._clans = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomClanItemVM>>(value, "Clans");
				}
			}
		}

		// Token: 0x1700039F RID: 927
		// (get) Token: 0x06000B67 RID: 2919 RVA: 0x0002FE23 File Offset: 0x0002E023
		// (set) Token: 0x06000B68 RID: 2920 RVA: 0x0002FE2B File Offset: 0x0002E02B
		[DataSourceProperty]
		public bool CanSupportCurrentClan
		{
			get
			{
				return this._canSupportCurrentClan;
			}
			set
			{
				if (value != this._canSupportCurrentClan)
				{
					this._canSupportCurrentClan = value;
					base.OnPropertyChangedWithValue(value, "CanSupportCurrentClan");
				}
			}
		}

		// Token: 0x170003A0 RID: 928
		// (get) Token: 0x06000B69 RID: 2921 RVA: 0x0002FE49 File Offset: 0x0002E049
		// (set) Token: 0x06000B6A RID: 2922 RVA: 0x0002FE51 File Offset: 0x0002E051
		[DataSourceProperty]
		public bool CanExpelCurrentClan
		{
			get
			{
				return this._canExpelCurrentClan;
			}
			set
			{
				if (value != this._canExpelCurrentClan)
				{
					this._canExpelCurrentClan = value;
					base.OnPropertyChangedWithValue(value, "CanExpelCurrentClan");
				}
			}
		}

		// Token: 0x170003A1 RID: 929
		// (get) Token: 0x06000B6B RID: 2923 RVA: 0x0002FE6F File Offset: 0x0002E06F
		// (set) Token: 0x06000B6C RID: 2924 RVA: 0x0002FE77 File Offset: 0x0002E077
		[DataSourceProperty]
		public string SupportText
		{
			get
			{
				return this._supportText;
			}
			set
			{
				if (value != this._supportText)
				{
					this._supportText = value;
					base.OnPropertyChangedWithValue<string>(value, "SupportText");
				}
			}
		}

		// Token: 0x170003A2 RID: 930
		// (get) Token: 0x06000B6D RID: 2925 RVA: 0x0002FE9A File Offset: 0x0002E09A
		// (set) Token: 0x06000B6E RID: 2926 RVA: 0x0002FEA2 File Offset: 0x0002E0A2
		[DataSourceProperty]
		public string ExpelActionText
		{
			get
			{
				return this._expelActionText;
			}
			set
			{
				if (value != this._expelActionText)
				{
					this._expelActionText = value;
					base.OnPropertyChangedWithValue<string>(value, "ExpelActionText");
				}
			}
		}

		// Token: 0x170003A3 RID: 931
		// (get) Token: 0x06000B6F RID: 2927 RVA: 0x0002FEC5 File Offset: 0x0002E0C5
		// (set) Token: 0x06000B70 RID: 2928 RVA: 0x0002FECD File Offset: 0x0002E0CD
		[DataSourceProperty]
		public int SupportCost
		{
			get
			{
				return this._supportCost;
			}
			set
			{
				if (value != this._supportCost)
				{
					this._supportCost = value;
					base.OnPropertyChangedWithValue(value, "SupportCost");
				}
			}
		}

		// Token: 0x170003A4 RID: 932
		// (get) Token: 0x06000B71 RID: 2929 RVA: 0x0002FEEB File Offset: 0x0002E0EB
		// (set) Token: 0x06000B72 RID: 2930 RVA: 0x0002FEF3 File Offset: 0x0002E0F3
		[DataSourceProperty]
		public int ExpelCost
		{
			get
			{
				return this._expelCost;
			}
			set
			{
				if (value != this._expelCost)
				{
					this._expelCost = value;
					base.OnPropertyChangedWithValue(value, "ExpelCost");
				}
			}
		}

		// Token: 0x170003A5 RID: 933
		// (get) Token: 0x06000B73 RID: 2931 RVA: 0x0002FF11 File Offset: 0x0002E111
		// (set) Token: 0x06000B74 RID: 2932 RVA: 0x0002FF19 File Offset: 0x0002E119
		[DataSourceProperty]
		public HintViewModel ExpelHint
		{
			get
			{
				return this._expelHint;
			}
			set
			{
				if (value != this._expelHint)
				{
					this._expelHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "ExpelHint");
				}
			}
		}

		// Token: 0x170003A6 RID: 934
		// (get) Token: 0x06000B75 RID: 2933 RVA: 0x0002FF37 File Offset: 0x0002E137
		// (set) Token: 0x06000B76 RID: 2934 RVA: 0x0002FF3F File Offset: 0x0002E13F
		[DataSourceProperty]
		public HintViewModel SupportHint
		{
			get
			{
				return this._supportHint;
			}
			set
			{
				if (value != this._supportHint)
				{
					this._supportHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "SupportHint");
				}
			}
		}

		// Token: 0x04000504 RID: 1284
		private Action<KingdomDecision> _forceDecide;

		// Token: 0x04000505 RID: 1285
		private bool _isThereAPendingDecisionToExpelThisClan;

		// Token: 0x04000506 RID: 1286
		private MBBindingList<KingdomClanItemVM> _clans;

		// Token: 0x04000507 RID: 1287
		private HintViewModel _expelHint;

		// Token: 0x04000508 RID: 1288
		private HintViewModel _supportHint;

		// Token: 0x04000509 RID: 1289
		private string _bannerText;

		// Token: 0x0400050A RID: 1290
		private string _nameText;

		// Token: 0x0400050B RID: 1291
		private string _influenceText;

		// Token: 0x0400050C RID: 1292
		private string _membersText;

		// Token: 0x0400050D RID: 1293
		private string _fiefsText;

		// Token: 0x0400050E RID: 1294
		private string _typeText;

		// Token: 0x0400050F RID: 1295
		private string _expelActionText;

		// Token: 0x04000510 RID: 1296
		private string _expelActionExplanationText;

		// Token: 0x04000511 RID: 1297
		private string _supportActionExplanationText;

		// Token: 0x04000512 RID: 1298
		private int _expelCost;

		// Token: 0x04000513 RID: 1299
		private string _supportText;

		// Token: 0x04000514 RID: 1300
		private int _supportCost;

		// Token: 0x04000515 RID: 1301
		private bool _canSupportCurrentClan;

		// Token: 0x04000516 RID: 1302
		private bool _canExpelCurrentClan;

		// Token: 0x04000517 RID: 1303
		private KingdomClanItemVM _currentSelectedClan;

		// Token: 0x04000518 RID: 1304
		private KingdomClanSortControllerVM _clanSortController;
	}
}
