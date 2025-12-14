using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Armies;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement
{
	// Token: 0x02000065 RID: 101
	public class KingdomManagementVM : ViewModel
	{
		// Token: 0x17000218 RID: 536
		// (get) Token: 0x0600077A RID: 1914 RVA: 0x00023287 File Offset: 0x00021487
		// (set) Token: 0x0600077B RID: 1915 RVA: 0x0002328F File Offset: 0x0002148F
		public Kingdom Kingdom { get; private set; }

		// Token: 0x0600077C RID: 1916 RVA: 0x00023298 File Offset: 0x00021498
		public KingdomManagementVM(Action onClose, Action onManageArmy, Action<Army> onShowArmyOnMap)
		{
			this._onClose = onClose;
			this._onShowArmyOnMap = onShowArmyOnMap;
			this.Army = new KingdomArmyVM(onManageArmy, new Action(this.OnRefreshDecision), this._onShowArmyOnMap);
			this.Settlement = this.CreateSettlementVM(new Action<KingdomDecision>(this.ForceDecideDecision), new Action<Settlement>(this.OnGrantFief));
			this.Clan = new KingdomClanVM(new Action<KingdomDecision>(this.ForceDecideDecision));
			this.Policy = new KingdomPoliciesVM(new Action<KingdomDecision>(this.ForceDecideDecision));
			this.Diplomacy = new KingdomDiplomacyVM(new Action<KingdomDecision>(this.ForceDecideDecision));
			this.GiftFief = new KingdomGiftFiefPopupVM(new Action(this.OnSettlementGranted));
			this.Decision = new KingdomDecisionsVM(new Action(this.OnRefresh));
			this._categoryCount = 5;
			this._leaveKingdomPermissionEvent = new LeaveKingdomPermissionEvent(new Action<bool, TextObject>(this.OnLeaveKingdomRequest));
			this.SetSelectedCategory(1);
			this.ChangeKingdomNameHint = new HintViewModel();
			this.RefreshValues();
		}

		// Token: 0x0600077D RID: 1917 RVA: 0x000233A4 File Offset: 0x000215A4
		protected virtual KingdomSettlementVM CreateSettlementVM(Action<KingdomDecision> forceDecision, Action<Settlement> onGrantFief)
		{
			return new KingdomSettlementVM(forceDecision, onGrantFief);
		}

		// Token: 0x0600077E RID: 1918 RVA: 0x000233B0 File Offset: 0x000215B0
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.LeaderText = GameTexts.FindText("str_sort_by_leader_name_label", null).ToString();
			this.ClansText = GameTexts.FindText("str_encyclopedia_clans", null).ToString();
			this.FiefsText = GameTexts.FindText("str_fiefs", null).ToString();
			this.PoliciesText = GameTexts.FindText("str_policies", null).ToString();
			this.ArmiesText = GameTexts.FindText("str_armies", null).ToString();
			this.DiplomacyText = GameTexts.FindText("str_diplomatic_group", null).ToString();
			this.DoneText = GameTexts.FindText("str_done", null).ToString();
			this.RefreshDynamicKingdomProperties();
			this.Army.RefreshValues();
			this.Policy.RefreshValues();
			this.Clan.RefreshValues();
			this.Settlement.RefreshValues();
			this.Diplomacy.RefreshValues();
		}

		// Token: 0x0600077F RID: 1919 RVA: 0x0002349C File Offset: 0x0002169C
		private void RefreshDynamicKingdomProperties()
		{
			this.Name = ((Hero.MainHero.MapFaction == null) ? new TextObject("{=kQsXUvgO}You are not under a kingdom.", null).ToString() : Hero.MainHero.MapFaction.Name.ToString());
			this.PlayerHasKingdom = (Hero.MainHero.MapFaction is Kingdom);
			if (this.PlayerHasKingdom)
			{
				this.Kingdom = (Hero.MainHero.MapFaction as Kingdom);
				this.Leader = new HeroVM(this.Kingdom.Leader, false);
				this.KingdomBanner = new BannerImageIdentifierVM(this.Kingdom.Banner, true);
				this._isPlayerTheRuler = (this.Kingdom.Leader == Hero.MainHero);
				this.KingdomActionText = (this._isPlayerTheRuler ? GameTexts.FindText("str_abdicate_leadership", null).ToString() : GameTexts.FindText("str_leave_kingdom", null).ToString());
			}
			else
			{
				this.Kingdom = null;
				this.Leader = null;
				this.KingdomBanner = null;
				this._isPlayerTheRuler = false;
				this.KingdomActionText = string.Empty;
			}
			TextObject hintText;
			this.PlayerCanChangeKingdomName = this.GetCanChangeKingdomNameWithReason(out hintText);
			this.ChangeKingdomNameHint.HintText = hintText;
			List<TextObject> kingdomActionDisabledReasons;
			this.IsKingdomActionEnabled = this.GetIsKingdomActionEnabledWithReason(this._isPlayerTheRuler, out kingdomActionDisabledReasons);
			this.KingdomActionHint = new BasicTooltipViewModel(() => CampaignUIHelper.MergeTextObjectsWithNewline(kingdomActionDisabledReasons));
		}

		// Token: 0x06000780 RID: 1920 RVA: 0x00023608 File Offset: 0x00021808
		private bool GetCanChangeKingdomNameWithReason(out TextObject disabledReason)
		{
			if (!this.PlayerHasKingdom)
			{
				disabledReason = new TextObject("{=kQsXUvgO}You are not under a kingdom.", null);
				return false;
			}
			if (!this._isPlayerTheRuler)
			{
				disabledReason = new TextObject("{=HFZdseH9}Only the ruler of the kingdom can change its name.", null);
				return false;
			}
			TextObject textObject;
			if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x06000781 RID: 1921 RVA: 0x0002365C File Offset: 0x0002185C
		private bool GetIsKingdomActionEnabledWithReason(bool isPlayerTheRuler, out List<TextObject> disabledReasons)
		{
			disabledReasons = new List<TextObject>();
			if (!this.PlayerHasKingdom)
			{
				disabledReasons.Add(new TextObject("{=kQsXUvgO}You are not under a kingdom.", null));
				return false;
			}
			List<TextObject> collection;
			if (isPlayerTheRuler && !Campaign.Current.Models.KingdomCreationModel.IsPlayerKingdomAbdicationPossible(out collection))
			{
				disabledReasons.AddRange(collection);
				return false;
			}
			if (!isPlayerTheRuler && MobileParty.MainParty.Army != null)
			{
				disabledReasons.Add(new TextObject("{=4Y8u4JKO}You can't leave the kingdom while in an army", null));
				return false;
			}
			Game.Current.EventManager.TriggerEvent<LeaveKingdomPermissionEvent>(this._leaveKingdomPermissionEvent);
			if (this._mostRecentLeaveKingdomPermission != null && !this._mostRecentLeaveKingdomPermission.GetValueOrDefault().Item1)
			{
				disabledReasons.Add((this._mostRecentLeaveKingdomPermission != null) ? this._mostRecentLeaveKingdomPermission.GetValueOrDefault().Item2 : null);
				return false;
			}
			TextObject item;
			if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out item))
			{
				disabledReasons.Add(item);
				return false;
			}
			return true;
		}

		// Token: 0x06000782 RID: 1922 RVA: 0x00023743 File Offset: 0x00021943
		public void OnRefresh()
		{
			this.RefreshDynamicKingdomProperties();
			this.Army.RefreshArmyList();
			this.Policy.RefreshPolicyList();
			this.Clan.RefreshClan();
			this.Settlement.RefreshSettlementList();
			this.Diplomacy.RefreshDiplomacyList();
		}

		// Token: 0x06000783 RID: 1923 RVA: 0x00023782 File Offset: 0x00021982
		public void OnFrameTick()
		{
			KingdomDecisionsVM decision = this.Decision;
			if (decision == null)
			{
				return;
			}
			decision.OnFrameTick();
		}

		// Token: 0x06000784 RID: 1924 RVA: 0x00023794 File Offset: 0x00021994
		private void OnRefreshDecision()
		{
			this.Decision.HandleNextDecision();
		}

		// Token: 0x06000785 RID: 1925 RVA: 0x000237A1 File Offset: 0x000219A1
		private void ForceDecideDecision(KingdomDecision decision)
		{
			this.Decision.RefreshWith(decision);
		}

		// Token: 0x06000786 RID: 1926 RVA: 0x000237B0 File Offset: 0x000219B0
		private void OnGrantFief(Settlement settlement)
		{
			if (this.Kingdom.Leader == Hero.MainHero)
			{
				this.GiftFief.OpenWith(settlement);
				return;
			}
			string titleText = new TextObject("{=eIGFuGOx}Give Settlement", null).ToString();
			string text = new TextObject("{=rkubGa4K}Are you sure want to give this settlement back to your kingdom?", null).ToString();
			InformationManager.ShowInquiry(new InquiryData(titleText, text, true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate()
			{
				Campaign.Current.KingdomManager.RelinquishSettlementOwnership(settlement);
				this.ForceDecideDecision(this.Kingdom.UnresolvedDecisions[this.Kingdom.UnresolvedDecisions.Count - 1]);
			}, null, "", 0f, null, null, null), false, false);
		}

		// Token: 0x06000787 RID: 1927 RVA: 0x0002385F File Offset: 0x00021A5F
		private void OnSettlementGranted()
		{
			this.Settlement.RefreshSettlementList();
		}

		// Token: 0x06000788 RID: 1928 RVA: 0x0002386C File Offset: 0x00021A6C
		public void ExecuteClose()
		{
			this._onClose();
		}

		// Token: 0x06000789 RID: 1929 RVA: 0x00023879 File Offset: 0x00021A79
		private void ExecuteShowClan()
		{
			this.SetSelectedCategory(0);
		}

		// Token: 0x0600078A RID: 1930 RVA: 0x00023882 File Offset: 0x00021A82
		private void ExecuteShowFiefs()
		{
			this.SetSelectedCategory(1);
		}

		// Token: 0x0600078B RID: 1931 RVA: 0x0002388B File Offset: 0x00021A8B
		private void ExecuteShowPolicies()
		{
			if (this.PlayerHasKingdom)
			{
				this.SetSelectedCategory(2);
			}
		}

		// Token: 0x0600078C RID: 1932 RVA: 0x0002389C File Offset: 0x00021A9C
		private void ExecuteShowDiplomacy()
		{
			if (this.PlayerHasKingdom)
			{
				this.SetSelectedCategory(4);
			}
		}

		// Token: 0x0600078D RID: 1933 RVA: 0x000238AD File Offset: 0x00021AAD
		private void ExecuteShowArmy()
		{
			this.SetSelectedCategory(3);
		}

		// Token: 0x0600078E RID: 1934 RVA: 0x000238B8 File Offset: 0x00021AB8
		private void ExecuteKingdomAction()
		{
			if (this.IsKingdomActionEnabled)
			{
				if (this._mostRecentLeaveKingdomPermission != null && this._mostRecentLeaveKingdomPermission.GetValueOrDefault().Item1 && ((this._mostRecentLeaveKingdomPermission != null) ? this._mostRecentLeaveKingdomPermission.GetValueOrDefault().Item2 : null) != null)
				{
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=3sxtCWPe}Leaving Kingdom", null).ToString(), (this._mostRecentLeaveKingdomPermission != null) ? this._mostRecentLeaveKingdomPermission.GetValueOrDefault().Item2.ToString() : null, true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), new Action(this.OnConfirmLeaveKingdom), null, "", 0f, null, null, null), false, false);
					return;
				}
				if (this._isPlayerTheRuler)
				{
					GameTexts.SetVariable("WILL_DESTROY", (this.Kingdom.Clans.Count == 1) ? 1 : 0);
					InformationManager.ShowInquiry(new InquiryData(GameTexts.FindText("str_abdicate_leadership", null).ToString(), GameTexts.FindText("str_abdicate_leadership_question", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), new Action(this.OnConfirmAbdicateLeadership), null, "", 0f, null, null, null), false, false);
					return;
				}
				if (TaleWorlds.CampaignSystem.Clan.PlayerClan.Settlements.Count == 0)
				{
					if (TaleWorlds.CampaignSystem.Clan.PlayerClan.IsUnderMercenaryService)
					{
						TextObject textObject = new TextObject("{=b7muQ9mt}Are you sure you want to end your mercenary contract with the {KINGDOM_INFORMALNAME}?", null);
						textObject.SetTextVariable("KINGDOM_INFORMALNAME", this.Kingdom.InformalName);
						InformationManager.ShowInquiry(new InquiryData(new TextObject("{=3sxtCWPe}Leaving Kingdom", null).ToString(), textObject.ToString(), true, true, new TextObject("{=5Unqsx3N}Confirm", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(), new Action(this.OnConfirmLeaveKingdom), null, "", 0f, null, null, null), false, false);
						return;
					}
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=3sxtCWPe}Leaving Kingdom", null).ToString(), new TextObject("{=BgqZWbga}The nobles of the realm will dislike you for abandoning your fealty. Are you sure you want to leave the Kingdom?", null).ToString(), true, true, new TextObject("{=5Unqsx3N}Confirm", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(), new Action(this.OnConfirmLeaveKingdom), null, "", 0f, null, null, null), false, false);
					return;
				}
				else
				{
					List<InquiryElement> inquiryElements = new List<InquiryElement>
					{
						new InquiryElement("keep", new TextObject("{=z8h0BRAb}Keep all holdings", null).ToString(), null, true, new TextObject("{=lkJfq1ap}Owned settlements remain under your control but nobles will dislike this dishonorable act and the kingdom will declare war on you.", null).ToString()),
						new InquiryElement("dontkeep", new TextObject("{=JIr3Jc7b}Relinquish all holdings", null).ToString(), null, true, new TextObject("{=ZjaSde0X}Owned settlements are returned to the kingdom. This will avert a war and nobles will dislike you less for abandoning your fealty.", null).ToString())
					};
					MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(new TextObject("{=3sxtCWPe}Leaving Kingdom", null).ToString(), new TextObject("{=xtlIFKaa}Are you sure you want to leave the Kingdom?{newline}If so, choose how you want to leave the kingdom.", null).ToString(), inquiryElements, true, 1, 1, new TextObject("{=5Unqsx3N}Confirm", null).ToString(), string.Empty, new Action<List<InquiryElement>>(this.OnConfirmLeaveKingdomWithOption), null, "", false), false, false);
				}
			}
		}

		// Token: 0x0600078F RID: 1935 RVA: 0x00023BF4 File Offset: 0x00021DF4
		private void OnLeaveKingdomRequest(bool isPossible, TextObject disabledReasonOrWarning)
		{
			this._mostRecentLeaveKingdomPermission = new ValueTuple<bool, TextObject>?(new ValueTuple<bool, TextObject>(isPossible, disabledReasonOrWarning));
		}

		// Token: 0x06000790 RID: 1936 RVA: 0x00023C08 File Offset: 0x00021E08
		private void OnConfirmAbdicateLeadership()
		{
			Campaign.Current.KingdomManager.AbdicateTheThrone(this.Kingdom);
			KingdomDecision kingdomDecision = this.Kingdom.UnresolvedDecisions.LastOrDefault<KingdomDecision>();
			if (kingdomDecision != null)
			{
				this.ForceDecideDecision(kingdomDecision);
				return;
			}
			this.ExecuteClose();
		}

		// Token: 0x06000791 RID: 1937 RVA: 0x00023C4C File Offset: 0x00021E4C
		private void OnConfirmLeaveKingdomWithOption(List<InquiryElement> obj)
		{
			InquiryElement inquiryElement = obj.FirstOrDefault<InquiryElement>();
			if (inquiryElement != null)
			{
				string a = inquiryElement.Identifier as string;
				if (a == "keep")
				{
					ChangeKingdomAction.ApplyByLeaveWithRebellionAgainstKingdom(TaleWorlds.CampaignSystem.Clan.PlayerClan, true);
				}
				else if (a == "dontkeep")
				{
					ChangeKingdomAction.ApplyByLeaveKingdom(TaleWorlds.CampaignSystem.Clan.PlayerClan, true);
				}
				this.ExecuteClose();
			}
		}

		// Token: 0x06000792 RID: 1938 RVA: 0x00023CA7 File Offset: 0x00021EA7
		private void OnConfirmLeaveKingdom()
		{
			if (TaleWorlds.CampaignSystem.Clan.PlayerClan.IsUnderMercenaryService)
			{
				ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(TaleWorlds.CampaignSystem.Clan.PlayerClan, true);
			}
			else
			{
				ChangeKingdomAction.ApplyByLeaveKingdom(TaleWorlds.CampaignSystem.Clan.PlayerClan, true);
			}
			this.ExecuteClose();
		}

		// Token: 0x06000793 RID: 1939 RVA: 0x00023CD4 File Offset: 0x00021ED4
		private void ExecuteChangeKingdomName()
		{
			InformationManager.ShowTextInquiry(new TextInquiryData(GameTexts.FindText("str_change_kingdom_name", null).ToString(), string.Empty, true, true, GameTexts.FindText("str_done", null).ToString(), GameTexts.FindText("str_cancel", null).ToString(), new Action<string>(this.OnChangeKingdomNameDone), null, false, new Func<string, Tuple<bool, string>>(FactionHelper.IsKingdomNameApplicable), "", ""), false, false);
		}

		// Token: 0x06000794 RID: 1940 RVA: 0x00023D48 File Offset: 0x00021F48
		private void OnChangeKingdomNameDone(string newKingdomName)
		{
			TextObject variable = new TextObject(newKingdomName, null);
			TextObject textObject = GameTexts.FindText("str_generic_kingdom_name", null);
			TextObject textObject2 = GameTexts.FindText("str_generic_kingdom_short_name", null);
			textObject.SetTextVariable("KINGDOM_NAME", variable);
			textObject2.SetTextVariable("KINGDOM_SHORT_NAME", variable);
			this.Kingdom.ChangeKingdomName(textObject, textObject2);
			this.OnRefresh();
			this.RefreshValues();
		}

		// Token: 0x06000795 RID: 1941 RVA: 0x00023DA8 File Offset: 0x00021FA8
		public void SelectArmy(Army army)
		{
			this.SetSelectedCategory(3);
			this.Army.SelectArmy(army);
		}

		// Token: 0x06000796 RID: 1942 RVA: 0x00023DBD File Offset: 0x00021FBD
		public void SelectSettlement(Settlement settlement)
		{
			this.SetSelectedCategory(1);
			this.Settlement.SelectSettlement(settlement);
		}

		// Token: 0x06000797 RID: 1943 RVA: 0x00023DD2 File Offset: 0x00021FD2
		public void SelectClan(Clan clan)
		{
			this.SetSelectedCategory(0);
			this.Clan.SelectClan(clan);
		}

		// Token: 0x06000798 RID: 1944 RVA: 0x00023DE7 File Offset: 0x00021FE7
		public void SelectPolicy(PolicyObject policy)
		{
			this.SetSelectedCategory(2);
			this.Policy.SelectPolicy(policy);
		}

		// Token: 0x06000799 RID: 1945 RVA: 0x00023DFC File Offset: 0x00021FFC
		public void SelectKingdom(Kingdom kingdom)
		{
			this.SetSelectedCategory(4);
			this.Diplomacy.SelectKingdom(kingdom);
		}

		// Token: 0x0600079A RID: 1946 RVA: 0x00023E14 File Offset: 0x00022014
		public void SelectPreviousCategory()
		{
			int selectedCategory = (this._currentCategory == 0) ? (this._categoryCount - 1) : (this._currentCategory - 1);
			this.SetSelectedCategory(selectedCategory);
		}

		// Token: 0x0600079B RID: 1947 RVA: 0x00023E44 File Offset: 0x00022044
		public void SelectNextCategory()
		{
			int selectedCategory = (this._currentCategory + 1) % this._categoryCount;
			this.SetSelectedCategory(selectedCategory);
		}

		// Token: 0x0600079C RID: 1948 RVA: 0x00023E68 File Offset: 0x00022068
		private void SetSelectedCategory(int index)
		{
			this.Clan.Show = false;
			this.Settlement.Show = false;
			this.Policy.Show = false;
			this.Army.Show = false;
			this.Diplomacy.Show = false;
			this._currentCategory = index;
			if (index == 0)
			{
				this.Clan.Show = true;
				return;
			}
			if (index == 1)
			{
				this.Settlement.Show = true;
				return;
			}
			if (index == 2)
			{
				this.Policy.Show = true;
				return;
			}
			if (index == 3)
			{
				this.Army.Show = true;
				return;
			}
			this._currentCategory = 4;
			this.Diplomacy.Show = true;
		}

		// Token: 0x0600079D RID: 1949 RVA: 0x00023F0E File Offset: 0x0002210E
		public override void OnFinalize()
		{
			base.OnFinalize();
			this.DoneInputKey.OnFinalize();
			this.PreviousTabInputKey.OnFinalize();
			this.NextTabInputKey.OnFinalize();
			this.Decision.OnFinalize();
			this.Clan.OnFinalize();
		}

		// Token: 0x17000219 RID: 537
		// (get) Token: 0x0600079E RID: 1950 RVA: 0x00023F4D File Offset: 0x0002214D
		// (set) Token: 0x0600079F RID: 1951 RVA: 0x00023F55 File Offset: 0x00022155
		[DataSourceProperty]
		public BasicTooltipViewModel KingdomActionHint
		{
			get
			{
				return this._kingdomActionHint;
			}
			set
			{
				if (value != this._kingdomActionHint)
				{
					this._kingdomActionHint = value;
					base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "KingdomActionHint");
				}
			}
		}

		// Token: 0x1700021A RID: 538
		// (get) Token: 0x060007A0 RID: 1952 RVA: 0x00023F73 File Offset: 0x00022173
		// (set) Token: 0x060007A1 RID: 1953 RVA: 0x00023F7B File Offset: 0x0002217B
		[DataSourceProperty]
		public BannerImageIdentifierVM KingdomBanner
		{
			get
			{
				return this._kingdomBanner;
			}
			set
			{
				if (value != this._kingdomBanner)
				{
					this._kingdomBanner = value;
					base.OnPropertyChangedWithValue<BannerImageIdentifierVM>(value, "KingdomBanner");
				}
			}
		}

		// Token: 0x1700021B RID: 539
		// (get) Token: 0x060007A2 RID: 1954 RVA: 0x00023F99 File Offset: 0x00022199
		// (set) Token: 0x060007A3 RID: 1955 RVA: 0x00023FA1 File Offset: 0x000221A1
		[DataSourceProperty]
		public HeroVM Leader
		{
			get
			{
				return this._leader;
			}
			set
			{
				if (value != this._leader)
				{
					this._leader = value;
					base.OnPropertyChangedWithValue<HeroVM>(value, "Leader");
				}
			}
		}

		// Token: 0x1700021C RID: 540
		// (get) Token: 0x060007A4 RID: 1956 RVA: 0x00023FBF File Offset: 0x000221BF
		// (set) Token: 0x060007A5 RID: 1957 RVA: 0x00023FC7 File Offset: 0x000221C7
		[DataSourceProperty]
		public KingdomArmyVM Army
		{
			get
			{
				return this._army;
			}
			set
			{
				if (value != this._army)
				{
					this._army = value;
					base.OnPropertyChangedWithValue<KingdomArmyVM>(value, "Army");
				}
			}
		}

		// Token: 0x1700021D RID: 541
		// (get) Token: 0x060007A6 RID: 1958 RVA: 0x00023FE5 File Offset: 0x000221E5
		// (set) Token: 0x060007A7 RID: 1959 RVA: 0x00023FED File Offset: 0x000221ED
		[DataSourceProperty]
		public KingdomSettlementVM Settlement
		{
			get
			{
				return this._settlement;
			}
			set
			{
				if (value != this._settlement)
				{
					this._settlement = value;
					base.OnPropertyChangedWithValue<KingdomSettlementVM>(value, "Settlement");
				}
			}
		}

		// Token: 0x1700021E RID: 542
		// (get) Token: 0x060007A8 RID: 1960 RVA: 0x0002400B File Offset: 0x0002220B
		// (set) Token: 0x060007A9 RID: 1961 RVA: 0x00024013 File Offset: 0x00022213
		[DataSourceProperty]
		public KingdomClanVM Clan
		{
			get
			{
				return this._clan;
			}
			set
			{
				if (value != this._clan)
				{
					this._clan = value;
					base.OnPropertyChangedWithValue<KingdomClanVM>(value, "Clan");
				}
			}
		}

		// Token: 0x1700021F RID: 543
		// (get) Token: 0x060007AA RID: 1962 RVA: 0x00024031 File Offset: 0x00022231
		// (set) Token: 0x060007AB RID: 1963 RVA: 0x00024039 File Offset: 0x00022239
		[DataSourceProperty]
		public KingdomPoliciesVM Policy
		{
			get
			{
				return this._policy;
			}
			set
			{
				if (value != this._policy)
				{
					this._policy = value;
					base.OnPropertyChangedWithValue<KingdomPoliciesVM>(value, "Policy");
				}
			}
		}

		// Token: 0x17000220 RID: 544
		// (get) Token: 0x060007AC RID: 1964 RVA: 0x00024057 File Offset: 0x00022257
		// (set) Token: 0x060007AD RID: 1965 RVA: 0x0002405F File Offset: 0x0002225F
		[DataSourceProperty]
		public KingdomDiplomacyVM Diplomacy
		{
			get
			{
				return this._diplomacy;
			}
			set
			{
				if (value != this._diplomacy)
				{
					this._diplomacy = value;
					base.OnPropertyChangedWithValue<KingdomDiplomacyVM>(value, "Diplomacy");
				}
			}
		}

		// Token: 0x17000221 RID: 545
		// (get) Token: 0x060007AE RID: 1966 RVA: 0x0002407D File Offset: 0x0002227D
		// (set) Token: 0x060007AF RID: 1967 RVA: 0x00024085 File Offset: 0x00022285
		[DataSourceProperty]
		public KingdomGiftFiefPopupVM GiftFief
		{
			get
			{
				return this._giftFief;
			}
			set
			{
				if (value != this._giftFief)
				{
					this._giftFief = value;
					base.OnPropertyChangedWithValue<KingdomGiftFiefPopupVM>(value, "GiftFief");
				}
			}
		}

		// Token: 0x17000222 RID: 546
		// (get) Token: 0x060007B0 RID: 1968 RVA: 0x000240A3 File Offset: 0x000222A3
		// (set) Token: 0x060007B1 RID: 1969 RVA: 0x000240AB File Offset: 0x000222AB
		[DataSourceProperty]
		public KingdomDecisionsVM Decision
		{
			get
			{
				return this._decision;
			}
			set
			{
				if (value != this._decision)
				{
					this._decision = value;
					base.OnPropertyChangedWithValue<KingdomDecisionsVM>(value, "Decision");
				}
			}
		}

		// Token: 0x17000223 RID: 547
		// (get) Token: 0x060007B2 RID: 1970 RVA: 0x000240C9 File Offset: 0x000222C9
		// (set) Token: 0x060007B3 RID: 1971 RVA: 0x000240D1 File Offset: 0x000222D1
		[DataSourceProperty]
		public HintViewModel ChangeKingdomNameHint
		{
			get
			{
				return this._changeKingdomNameHint;
			}
			set
			{
				if (value != this._changeKingdomNameHint)
				{
					this._changeKingdomNameHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "ChangeKingdomNameHint");
				}
			}
		}

		// Token: 0x17000224 RID: 548
		// (get) Token: 0x060007B4 RID: 1972 RVA: 0x000240EF File Offset: 0x000222EF
		// (set) Token: 0x060007B5 RID: 1973 RVA: 0x000240F7 File Offset: 0x000222F7
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

		// Token: 0x17000225 RID: 549
		// (get) Token: 0x060007B6 RID: 1974 RVA: 0x0002411A File Offset: 0x0002231A
		// (set) Token: 0x060007B7 RID: 1975 RVA: 0x00024122 File Offset: 0x00022322
		[DataSourceProperty]
		public bool CanSwitchTabs
		{
			get
			{
				return this._canSwitchTabs;
			}
			set
			{
				if (value != this._canSwitchTabs)
				{
					this._canSwitchTabs = value;
					base.OnPropertyChangedWithValue(value, "CanSwitchTabs");
				}
			}
		}

		// Token: 0x17000226 RID: 550
		// (get) Token: 0x060007B8 RID: 1976 RVA: 0x00024140 File Offset: 0x00022340
		// (set) Token: 0x060007B9 RID: 1977 RVA: 0x00024148 File Offset: 0x00022348
		[DataSourceProperty]
		public bool PlayerHasKingdom
		{
			get
			{
				return this._playerHasKingdom;
			}
			set
			{
				if (value != this._playerHasKingdom)
				{
					this._playerHasKingdom = value;
					base.OnPropertyChangedWithValue(value, "PlayerHasKingdom");
				}
			}
		}

		// Token: 0x17000227 RID: 551
		// (get) Token: 0x060007BA RID: 1978 RVA: 0x00024166 File Offset: 0x00022366
		// (set) Token: 0x060007BB RID: 1979 RVA: 0x0002416E File Offset: 0x0002236E
		[DataSourceProperty]
		public bool IsKingdomActionEnabled
		{
			get
			{
				return this._isKingdomActionEnabled;
			}
			set
			{
				if (value != this._isKingdomActionEnabled)
				{
					this._isKingdomActionEnabled = value;
					base.OnPropertyChangedWithValue(value, "IsKingdomActionEnabled");
				}
			}
		}

		// Token: 0x17000228 RID: 552
		// (get) Token: 0x060007BC RID: 1980 RVA: 0x0002418C File Offset: 0x0002238C
		// (set) Token: 0x060007BD RID: 1981 RVA: 0x00024194 File Offset: 0x00022394
		[DataSourceProperty]
		public bool PlayerCanChangeKingdomName
		{
			get
			{
				return this._playerCanChangeKingdomName;
			}
			set
			{
				if (value != this._playerCanChangeKingdomName)
				{
					this._playerCanChangeKingdomName = value;
					base.OnPropertyChangedWithValue(value, "PlayerCanChangeKingdomName");
				}
			}
		}

		// Token: 0x17000229 RID: 553
		// (get) Token: 0x060007BE RID: 1982 RVA: 0x000241B2 File Offset: 0x000223B2
		// (set) Token: 0x060007BF RID: 1983 RVA: 0x000241BA File Offset: 0x000223BA
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

		// Token: 0x1700022A RID: 554
		// (get) Token: 0x060007C0 RID: 1984 RVA: 0x000241DD File Offset: 0x000223DD
		// (set) Token: 0x060007C1 RID: 1985 RVA: 0x000241E5 File Offset: 0x000223E5
		[DataSourceProperty]
		public string KingdomActionText
		{
			get
			{
				return this._kingdomActionText;
			}
			set
			{
				if (value != this._kingdomActionText)
				{
					this._kingdomActionText = value;
					base.OnPropertyChangedWithValue<string>(value, "KingdomActionText");
				}
			}
		}

		// Token: 0x1700022B RID: 555
		// (get) Token: 0x060007C2 RID: 1986 RVA: 0x00024208 File Offset: 0x00022408
		// (set) Token: 0x060007C3 RID: 1987 RVA: 0x00024210 File Offset: 0x00022410
		[DataSourceProperty]
		public string ClansText
		{
			get
			{
				return this._clansText;
			}
			set
			{
				if (value != this._clansText)
				{
					this._clansText = value;
					base.OnPropertyChangedWithValue<string>(value, "ClansText");
				}
			}
		}

		// Token: 0x1700022C RID: 556
		// (get) Token: 0x060007C4 RID: 1988 RVA: 0x00024233 File Offset: 0x00022433
		// (set) Token: 0x060007C5 RID: 1989 RVA: 0x0002423B File Offset: 0x0002243B
		[DataSourceProperty]
		public string DiplomacyText
		{
			get
			{
				return this._diplomacyText;
			}
			set
			{
				if (value != this._diplomacyText)
				{
					this._diplomacyText = value;
					base.OnPropertyChangedWithValue<string>(value, "DiplomacyText");
				}
			}
		}

		// Token: 0x1700022D RID: 557
		// (get) Token: 0x060007C6 RID: 1990 RVA: 0x0002425E File Offset: 0x0002245E
		// (set) Token: 0x060007C7 RID: 1991 RVA: 0x00024266 File Offset: 0x00022466
		[DataSourceProperty]
		public string DoneText
		{
			get
			{
				return this._doneText;
			}
			set
			{
				if (value != this._doneText)
				{
					this._doneText = value;
					base.OnPropertyChangedWithValue<string>(value, "DoneText");
				}
			}
		}

		// Token: 0x1700022E RID: 558
		// (get) Token: 0x060007C8 RID: 1992 RVA: 0x00024289 File Offset: 0x00022489
		// (set) Token: 0x060007C9 RID: 1993 RVA: 0x00024291 File Offset: 0x00022491
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

		// Token: 0x1700022F RID: 559
		// (get) Token: 0x060007CA RID: 1994 RVA: 0x000242B4 File Offset: 0x000224B4
		// (set) Token: 0x060007CB RID: 1995 RVA: 0x000242BC File Offset: 0x000224BC
		[DataSourceProperty]
		public string PoliciesText
		{
			get
			{
				return this._policiesText;
			}
			set
			{
				if (value != this._policiesText)
				{
					this._policiesText = value;
					base.OnPropertyChangedWithValue<string>(value, "PoliciesText");
				}
			}
		}

		// Token: 0x17000230 RID: 560
		// (get) Token: 0x060007CC RID: 1996 RVA: 0x000242DF File Offset: 0x000224DF
		// (set) Token: 0x060007CD RID: 1997 RVA: 0x000242E7 File Offset: 0x000224E7
		[DataSourceProperty]
		public string ArmiesText
		{
			get
			{
				return this._armiesText;
			}
			set
			{
				if (value != this._armiesText)
				{
					this._armiesText = value;
					base.OnPropertyChangedWithValue<string>(value, "ArmiesText");
				}
			}
		}

		// Token: 0x060007CE RID: 1998 RVA: 0x0002430A File Offset: 0x0002250A
		public void SetDoneInputKey(HotKey hotkey)
		{
			this.DoneInputKey = InputKeyItemVM.CreateFromHotKey(hotkey, true);
			this.Decision.SetDoneInputKey(hotkey);
			this.GiftFief.SetDoneInputKey(hotkey);
		}

		// Token: 0x060007CF RID: 1999 RVA: 0x00024331 File Offset: 0x00022531
		public void SetCancelInputKey(HotKey hotkey)
		{
			this.GiftFief.SetCancelInputKey(hotkey);
		}

		// Token: 0x060007D0 RID: 2000 RVA: 0x0002433F File Offset: 0x0002253F
		public void SetPreviousTabInputKey(HotKey hotkey)
		{
			this.PreviousTabInputKey = InputKeyItemVM.CreateFromHotKey(hotkey, true);
		}

		// Token: 0x060007D1 RID: 2001 RVA: 0x0002434E File Offset: 0x0002254E
		public void SetNextTabInputKey(HotKey hotkey)
		{
			this.NextTabInputKey = InputKeyItemVM.CreateFromHotKey(hotkey, true);
		}

		// Token: 0x17000231 RID: 561
		// (get) Token: 0x060007D2 RID: 2002 RVA: 0x0002435D File Offset: 0x0002255D
		// (set) Token: 0x060007D3 RID: 2003 RVA: 0x00024365 File Offset: 0x00022565
		public InputKeyItemVM DoneInputKey
		{
			get
			{
				return this._doneInputKey;
			}
			set
			{
				if (value != this._doneInputKey)
				{
					this._doneInputKey = value;
					base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "DoneInputKey");
				}
			}
		}

		// Token: 0x17000232 RID: 562
		// (get) Token: 0x060007D4 RID: 2004 RVA: 0x00024383 File Offset: 0x00022583
		// (set) Token: 0x060007D5 RID: 2005 RVA: 0x0002438B File Offset: 0x0002258B
		public InputKeyItemVM PreviousTabInputKey
		{
			get
			{
				return this._previousTabInputKey;
			}
			set
			{
				if (value != this._previousTabInputKey)
				{
					this._previousTabInputKey = value;
					base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "PreviousTabInputKey");
				}
			}
		}

		// Token: 0x17000233 RID: 563
		// (get) Token: 0x060007D6 RID: 2006 RVA: 0x000243A9 File Offset: 0x000225A9
		// (set) Token: 0x060007D7 RID: 2007 RVA: 0x000243B1 File Offset: 0x000225B1
		public InputKeyItemVM NextTabInputKey
		{
			get
			{
				return this._nextTabInputKey;
			}
			set
			{
				if (value != this._nextTabInputKey)
				{
					this._nextTabInputKey = value;
					base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "NextTabInputKey");
				}
			}
		}

		// Token: 0x0400033E RID: 830
		private readonly Action _onClose;

		// Token: 0x0400033F RID: 831
		private readonly Action<Army> _onShowArmyOnMap;

		// Token: 0x04000340 RID: 832
		private readonly int _categoryCount;

		// Token: 0x04000341 RID: 833
		private readonly LeaveKingdomPermissionEvent _leaveKingdomPermissionEvent;

		// Token: 0x04000342 RID: 834
		private ValueTuple<bool, TextObject>? _mostRecentLeaveKingdomPermission;

		// Token: 0x04000343 RID: 835
		private int _currentCategory;

		// Token: 0x04000344 RID: 836
		private bool _isPlayerTheRuler;

		// Token: 0x04000346 RID: 838
		private KingdomArmyVM _army;

		// Token: 0x04000347 RID: 839
		private KingdomSettlementVM _settlement;

		// Token: 0x04000348 RID: 840
		private KingdomClanVM _clan;

		// Token: 0x04000349 RID: 841
		private KingdomPoliciesVM _policy;

		// Token: 0x0400034A RID: 842
		private KingdomDiplomacyVM _diplomacy;

		// Token: 0x0400034B RID: 843
		private KingdomGiftFiefPopupVM _giftFief;

		// Token: 0x0400034C RID: 844
		private BannerImageIdentifierVM _kingdomBanner;

		// Token: 0x0400034D RID: 845
		private HeroVM _leader;

		// Token: 0x0400034E RID: 846
		private KingdomDecisionsVM _decision;

		// Token: 0x0400034F RID: 847
		private HintViewModel _changeKingdomNameHint;

		// Token: 0x04000350 RID: 848
		private string _name;

		// Token: 0x04000351 RID: 849
		private bool _canSwitchTabs;

		// Token: 0x04000352 RID: 850
		private bool _playerHasKingdom;

		// Token: 0x04000353 RID: 851
		private bool _isKingdomActionEnabled;

		// Token: 0x04000354 RID: 852
		private bool _playerCanChangeKingdomName;

		// Token: 0x04000355 RID: 853
		private string _kingdomActionText;

		// Token: 0x04000356 RID: 854
		private string _leaderText;

		// Token: 0x04000357 RID: 855
		private string _clansText;

		// Token: 0x04000358 RID: 856
		private string _fiefsText;

		// Token: 0x04000359 RID: 857
		private string _policiesText;

		// Token: 0x0400035A RID: 858
		private string _armiesText;

		// Token: 0x0400035B RID: 859
		private string _diplomacyText;

		// Token: 0x0400035C RID: 860
		private string _doneText;

		// Token: 0x0400035D RID: 861
		private BasicTooltipViewModel _kingdomActionHint;

		// Token: 0x0400035E RID: 862
		private InputKeyItemVM _doneInputKey;

		// Token: 0x0400035F RID: 863
		private InputKeyItemVM _previousTabInputKey;

		// Token: 0x04000360 RID: 864
		private InputKeyItemVM _nextTabInputKey;
	}
}
