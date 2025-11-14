using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Tutorial;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment
{
	// Token: 0x020000B0 RID: 176
	public class RecruitmentVM : ViewModel
	{
		// Token: 0x17000576 RID: 1398
		// (get) Token: 0x060010BE RID: 4286 RVA: 0x000432B2 File Offset: 0x000414B2
		// (set) Token: 0x060010BF RID: 4287 RVA: 0x000432BA File Offset: 0x000414BA
		public bool IsQuitting { get; private set; }

		// Token: 0x060010C0 RID: 4288 RVA: 0x000432C4 File Offset: 0x000414C4
		public RecruitmentVM()
		{
			this.VolunteerList = new MBBindingList<RecruitVolunteerVM>();
			this.TroopsInCart = new MBBindingList<RecruitVolunteerTroopVM>();
			this.RefreshValues();
			if (Settlement.CurrentSettlement != null)
			{
				this.RefreshScreen();
			}
			Game.Current.EventManager.RegisterEvent<TutorialNotificationElementChangeEvent>(new Action<TutorialNotificationElementChangeEvent>(this.OnTutorialNotificationElementIDChange));
			RecruitVolunteerTroopVM.OnFocused = (Action<RecruitVolunteerTroopVM>)Delegate.Combine(RecruitVolunteerTroopVM.OnFocused, new Action<RecruitVolunteerTroopVM>(this.OnVolunteerTroopFocusChanged));
			RecruitVolunteerOwnerVM.OnFocused = (Action<RecruitVolunteerOwnerVM>)Delegate.Combine(RecruitVolunteerOwnerVM.OnFocused, new Action<RecruitVolunteerOwnerVM>(this.OnVolunteerOwnerFocusChanged));
		}

		// Token: 0x060010C1 RID: 4289 RVA: 0x00043394 File Offset: 0x00041594
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.PartyWageHint = new HintViewModel(GameTexts.FindText("str_weekly_wage", null), null);
			this.TotalWealthHint = new HintViewModel(GameTexts.FindText("str_wealth", null), null);
			this.TotalCostHint = new HintViewModel(GameTexts.FindText("str_total_cost", null), null);
			this.PartyCapacityHint = new HintViewModel();
			this.PartySpeedHint = new BasicTooltipViewModel();
			this.RemainingFoodHint = new HintViewModel();
			this.DoneHint = new HintViewModel();
			this.ResetHint = new HintViewModel(GameTexts.FindText("str_reset", null), null);
			this.DoneText = GameTexts.FindText("str_done", null).ToString();
			this.TitleText = GameTexts.FindText("str_recruitment", null).ToString();
			this._recruitAllTextObject = GameTexts.FindText("str_recruit_all", null);
			this.ResetAllText = GameTexts.FindText("str_reset_all", null).ToString();
			this.CancelText = GameTexts.FindText("str_party_cancel", null).ToString();
			this._playerDoesntHaveEnoughMoneyStr = GameTexts.FindText("str_warning_you_dont_have_enough_money", null).ToString();
			this._playerIsOverPartyLimitStr = GameTexts.FindText("str_party_size_limit_exceeded", null).ToString();
			this.VolunteerList.ApplyActionOnAllItems(delegate(RecruitVolunteerVM x)
			{
				x.RefreshValues();
			});
			this.TroopsInCart.ApplyActionOnAllItems(delegate(RecruitVolunteerTroopVM x)
			{
				x.RefreshValues();
			});
			this.SetRecruitAllHint();
			this.UpdateRecruitAllProperties();
			if (Settlement.CurrentSettlement != null)
			{
				this.RefreshScreen();
			}
		}

		// Token: 0x060010C2 RID: 4290 RVA: 0x00043534 File Offset: 0x00041734
		public void RefreshScreen()
		{
			this.VolunteerList.Clear();
			this.TroopsInCart.Clear();
			int num = 0;
			this.InitialPartySize = PartyBase.MainParty.NumberOfAllMembers;
			this.RefreshPartyProperties();
			foreach (Hero hero in Settlement.CurrentSettlement.Notables)
			{
				if (hero.CanHaveRecruits)
				{
					MBTextManager.SetTextVariable("INDIVIDUAL_NAME", hero.Name, false);
					List<CharacterObject> volunteerTroopsOfHeroForRecruitment = HeroHelper.GetVolunteerTroopsOfHeroForRecruitment(hero);
					RecruitVolunteerVM item = new RecruitVolunteerVM(hero, volunteerTroopsOfHeroForRecruitment, new Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>(this.OnRecruit), new Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>(this.OnRemoveFromCart));
					this.VolunteerList.Add(item);
					num++;
				}
			}
			this.TotalWealth = Hero.MainHero.Gold;
			this.UpdateRecruitAllProperties();
		}

		// Token: 0x060010C3 RID: 4291 RVA: 0x0004361C File Offset: 0x0004181C
		private void OnRecruit(RecruitVolunteerVM recruitNotable, RecruitVolunteerTroopVM recruitTroop)
		{
			if (!recruitTroop.CanBeRecruited)
			{
				return;
			}
			recruitNotable.OnRecruitMoveToCart(recruitTroop);
			recruitTroop.CanBeRecruited = false;
			this.TroopsInCart.Add(recruitTroop);
			recruitTroop.IsInCart = true;
			CampaignEventDispatcher.Instance.OnPlayerStartRecruitment(recruitTroop.Character);
			this.RefreshPartyProperties();
		}

		// Token: 0x060010C4 RID: 4292 RVA: 0x0004366C File Offset: 0x0004186C
		private void RefreshPartyProperties()
		{
			int num = this.TroopsInCart.Sum((RecruitVolunteerTroopVM t) => t.Wage);
			this.PartyWage = MobileParty.MainParty.TotalWage;
			if (num > 0)
			{
				this.PartyWageText = CampaignUIHelper.GetValueChangeText((float)this.PartyWage, (float)num, "F0");
			}
			else
			{
				this.PartyWageText = this.PartyWage.ToString();
			}
			double num2 = 0.0;
			if (this.TroopsInCart.Count > 0)
			{
				int num3 = 0;
				int num4 = 0;
				using (IEnumerator<RecruitVolunteerTroopVM> enumerator = this.TroopsInCart.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						if (enumerator.Current.Character.IsMounted)
						{
							num4++;
						}
						else
						{
							num3++;
						}
					}
				}
				ExplainedNumber finalSpeed = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateBaseSpeed(MobileParty.MainParty, false, num3, num4);
				ExplainedNumber explainedNumber = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateFinalSpeed(MobileParty.MainParty, finalSpeed);
				ExplainedNumber finalSpeed2 = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateBaseSpeed(MobileParty.MainParty, false, 0, 0);
				ExplainedNumber explainedNumber2 = Campaign.Current.Models.PartySpeedCalculatingModel.CalculateFinalSpeed(MobileParty.MainParty, finalSpeed2);
				num2 = (double)(MathF.Round(explainedNumber.ResultNumber, 1) - MathF.Round(explainedNumber2.ResultNumber, 1));
			}
			this.PartySpeedText = MobileParty.MainParty.Speed.ToString("0.0");
			this.PartySpeedHint = new BasicTooltipViewModel(() => CampaignUIHelper.GetPartySpeedTooltip(false));
			if (num2 != 0.0)
			{
				this.PartySpeedText = CampaignUIHelper.GetValueChangeText(MobileParty.MainParty.Speed, (float)num2, "0.0");
			}
			int partySizeLimit = PartyBase.MainParty.PartySizeLimit;
			this.CurrentPartySize = PartyBase.MainParty.NumberOfAllMembers + this.TroopsInCart.Count;
			this.PartyCapacity = partySizeLimit;
			this.IsPartyCapacityWarningEnabled = (this.CurrentPartySize > this.PartyCapacity);
			GameTexts.SetVariable("LEFT", this.CurrentPartySize.ToString());
			GameTexts.SetVariable("RIGHT", partySizeLimit.ToString());
			this.PartyCapacityText = GameTexts.FindText("str_LEFT_over_RIGHT", null).ToString();
			this.PartyCapacityHint.HintText = new TextObject("{=!}" + PartyBase.MainParty.PartySizeLimitExplainer.ToString(), null);
			float food = MobileParty.MainParty.Food;
			this.RemainingFoodText = MathF.Round(food, 1).ToString();
			float foodChange = MobileParty.MainParty.FoodChange;
			int totalFoodAtInventory = MobileParty.MainParty.TotalFoodAtInventory;
			int numDaysForFoodToLast = MobileParty.MainParty.GetNumDaysForFoodToLast();
			MBTextManager.SetTextVariable("DAY_NUM", numDaysForFoodToLast);
			this.RemainingFoodHint.HintText = GameTexts.FindText("str_food_consumption_tooltip", null);
			this.RemainingFoodHint.HintText.SetTextVariable("DAILY_FOOD_CONSUMPTION", foodChange, 2);
			this.RemainingFoodHint.HintText.SetTextVariable("REMAINING_DAYS", GameTexts.FindText("str_party_food_left", null));
			this.RemainingFoodHint.HintText.SetTextVariable("TOTAL_FOOD_AMOUNT", ((double)totalFoodAtInventory + 0.01 * (double)PartyBase.MainParty.RemainingFoodPercentage).ToString("0.00"));
			this.RemainingFoodHint.HintText.SetTextVariable("TOTAL_FOOD", totalFoodAtInventory);
			int num5 = this.TroopsInCart.Sum((RecruitVolunteerTroopVM t) => t.Cost);
			this.TotalCostText = num5.ToString();
			bool flag = num5 <= Hero.MainHero.Gold;
			this.IsDoneEnabled = flag;
			this.DoneHint.HintText = new TextObject("{=!}" + this.GetDoneHint(flag), null);
			this.UpdateRecruitAllProperties();
		}

		// Token: 0x060010C5 RID: 4293 RVA: 0x00043A90 File Offset: 0x00041C90
		public void ExecuteDone()
		{
			if (this.CurrentPartySize <= this.PartyCapacity)
			{
				this.OnDone();
				return;
			}
			GameTexts.SetVariable("newline", "\n");
			string text = GameTexts.FindText("str_party_over_limit_troops", null).ToString();
			InformationManager.ShowInquiry(new InquiryData(new TextObject("{=uJro3Bua}Over Limit", null).ToString(), text, true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate()
			{
				this.OnDone();
			}, null, "", 0f, null, null, null), false, false);
		}

		// Token: 0x060010C6 RID: 4294 RVA: 0x00043B2C File Offset: 0x00041D2C
		private void OnDone()
		{
			this.RefreshPartyProperties();
			int num = this.TroopsInCart.Sum((RecruitVolunteerTroopVM t) => t.Cost);
			if (num > Hero.MainHero.Gold)
			{
				Debug.FailedAssert("Execution shouldn't come here. The checks should happen before", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\GameMenu\\Recruitment\\RecruitmentVM.cs", "OnDone", 229);
				return;
			}
			foreach (RecruitVolunteerTroopVM recruitVolunteerTroopVM in this.TroopsInCart)
			{
				recruitVolunteerTroopVM.Owner.OwnerHero.VolunteerTypes[recruitVolunteerTroopVM.Index] = null;
				MobileParty.MainParty.MemberRoster.AddToCounts(recruitVolunteerTroopVM.Character, 1, false, 0, 0, true, -1);
				CampaignEventDispatcher.Instance.OnUnitRecruited(recruitVolunteerTroopVM.Character, 1);
			}
			GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, num, true);
			if (num > 0)
			{
				MBTextManager.SetTextVariable("GOLD_AMOUNT", MathF.Abs(num));
				InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_gold_removed_with_icon", null).ToString(), "event:/ui/notification/coins_negative"));
			}
			this.Deactivate();
		}

		// Token: 0x060010C7 RID: 4295 RVA: 0x00043C58 File Offset: 0x00041E58
		public void ExecuteForceQuit()
		{
			if (!this.IsQuitting)
			{
				this.IsQuitting = true;
				if (this.TroopsInCart.Count > 0)
				{
					InformationManager.ShowInquiry(new InquiryData(GameTexts.FindText("str_quit", null).ToString(), GameTexts.FindText("str_quit_question", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate()
					{
						this.ExecuteReset();
						this.ExecuteDone();
						this.IsQuitting = false;
					}, delegate()
					{
						this.IsQuitting = false;
					}, "", 0f, null, null, null), true, false);
					return;
				}
				this.Deactivate();
			}
		}

		// Token: 0x060010C8 RID: 4296 RVA: 0x00043D00 File Offset: 0x00041F00
		public void ExecuteReset()
		{
			for (int i = this.TroopsInCart.Count - 1; i >= 0; i--)
			{
				this.TroopsInCart[i].ExecuteRemoveFromCart();
			}
		}

		// Token: 0x060010C9 RID: 4297 RVA: 0x00043D38 File Offset: 0x00041F38
		public void ExecuteRecruitAll()
		{
			foreach (RecruitVolunteerVM recruitVolunteerVM in this.VolunteerList.ToList<RecruitVolunteerVM>())
			{
				foreach (RecruitVolunteerTroopVM recruitVolunteerTroopVM in recruitVolunteerVM.Troops.ToList<RecruitVolunteerTroopVM>())
				{
					recruitVolunteerTroopVM.ExecuteRecruit();
				}
			}
		}

		// Token: 0x060010CA RID: 4298 RVA: 0x00043DCC File Offset: 0x00041FCC
		public void Deactivate()
		{
			this.ExecuteReset();
			this.Enabled = false;
		}

		// Token: 0x060010CB RID: 4299 RVA: 0x00043DDC File Offset: 0x00041FDC
		public override void OnFinalize()
		{
			base.OnFinalize();
			RecruitVolunteerTroopVM.OnFocused = (Action<RecruitVolunteerTroopVM>)Delegate.Remove(RecruitVolunteerTroopVM.OnFocused, new Action<RecruitVolunteerTroopVM>(this.OnVolunteerTroopFocusChanged));
			RecruitVolunteerOwnerVM.OnFocused = (Action<RecruitVolunteerOwnerVM>)Delegate.Remove(RecruitVolunteerOwnerVM.OnFocused, new Action<RecruitVolunteerOwnerVM>(this.OnVolunteerOwnerFocusChanged));
			Game.Current.EventManager.UnregisterEvent<TutorialNotificationElementChangeEvent>(new Action<TutorialNotificationElementChangeEvent>(this.OnTutorialNotificationElementIDChange));
			this.CancelInputKey.OnFinalize();
			this.DoneInputKey.OnFinalize();
			this.ResetInputKey.OnFinalize();
			this.RecruitAllInputKey.OnFinalize();
		}

		// Token: 0x060010CC RID: 4300 RVA: 0x00043E78 File Offset: 0x00042078
		private void OnRemoveFromCart(RecruitVolunteerVM recruitNotable, RecruitVolunteerTroopVM recruitTroop)
		{
			if (this.TroopsInCart.Any((RecruitVolunteerTroopVM r) => r == recruitTroop))
			{
				recruitNotable.OnRecruitRemovedFromCart(recruitTroop);
				recruitTroop.CanBeRecruited = true;
				recruitTroop.IsInCart = false;
				recruitTroop.IsHiglightEnabled = false;
				this.TroopsInCart.Remove(recruitTroop);
				this.RefreshPartyProperties();
			}
		}

		// Token: 0x060010CD RID: 4301 RVA: 0x00043EF3 File Offset: 0x000420F3
		private static bool IsBitSet(int num, int bit)
		{
			return 1 == (num >> bit & 1);
		}

		// Token: 0x060010CE RID: 4302 RVA: 0x00043F00 File Offset: 0x00042100
		private string GetDoneHint(bool doesPlayerHasEnoughMoney)
		{
			if (!doesPlayerHasEnoughMoney)
			{
				return this._playerDoesntHaveEnoughMoneyStr;
			}
			return null;
		}

		// Token: 0x060010CF RID: 4303 RVA: 0x00043F0D File Offset: 0x0004210D
		private void SetRecruitAllHint()
		{
			this.RecruitAllHint = new BasicTooltipViewModel(delegate()
			{
				GameTexts.SetVariable("HOTKEY", this.GetRecruitAllKey());
				GameTexts.SetVariable("TEXT", GameTexts.FindText("str_recruit_all", null));
				return GameTexts.FindText("str_hotkey_with_hint", null).ToString();
			});
		}

		// Token: 0x060010D0 RID: 4304 RVA: 0x00043F28 File Offset: 0x00042128
		private void UpdateRecruitAllProperties()
		{
			int numberOfAvailableRecruits = this.GetNumberOfAvailableRecruits();
			GameTexts.SetVariable("STR", numberOfAvailableRecruits);
			GameTexts.SetVariable("STR1", this._recruitAllTextObject);
			GameTexts.SetVariable("STR2", GameTexts.FindText("str_STR_in_parentheses", null));
			this.RecruitAllText = GameTexts.FindText("str_STR1_space_STR2", null).ToString();
			this.CanRecruitAll = (numberOfAvailableRecruits > 0);
		}

		// Token: 0x060010D1 RID: 4305 RVA: 0x00043F8C File Offset: 0x0004218C
		private int GetNumberOfAvailableRecruits()
		{
			int num = 0;
			foreach (RecruitVolunteerVM recruitVolunteerVM in this.VolunteerList)
			{
				foreach (RecruitVolunteerTroopVM recruitVolunteerTroopVM in recruitVolunteerVM.Troops)
				{
					if (!recruitVolunteerTroopVM.IsInCart && recruitVolunteerTroopVM.CanBeRecruited)
					{
						num++;
					}
				}
			}
			return num;
		}

		// Token: 0x060010D2 RID: 4306 RVA: 0x0004401C File Offset: 0x0004221C
		private void OnVolunteerTroopFocusChanged(RecruitVolunteerTroopVM volunteer)
		{
			this.FocusedVolunteerTroop = volunteer;
		}

		// Token: 0x060010D3 RID: 4307 RVA: 0x00044025 File Offset: 0x00042225
		private void OnVolunteerOwnerFocusChanged(RecruitVolunteerOwnerVM owner)
		{
			this.FocusedVolunteerOwner = owner;
		}

		// Token: 0x060010D4 RID: 4308 RVA: 0x00044030 File Offset: 0x00042230
		private void OnTutorialNotificationElementIDChange(TutorialNotificationElementChangeEvent obj)
		{
			if (obj.NewNotificationElementID != this._latestTutorialElementID)
			{
				if (this._latestTutorialElementID != null && this._isAvailableTroopsHighlightApplied)
				{
					this.SetAvailableTroopsHighlightState(false);
					this._isAvailableTroopsHighlightApplied = false;
				}
				this._latestTutorialElementID = obj.NewNotificationElementID;
				if (this._latestTutorialElementID != null && !this._isAvailableTroopsHighlightApplied && this._latestTutorialElementID == "AvailableTroops")
				{
					this.SetAvailableTroopsHighlightState(true);
					this._isAvailableTroopsHighlightApplied = true;
				}
			}
		}

		// Token: 0x060010D5 RID: 4309 RVA: 0x000440AC File Offset: 0x000422AC
		private void SetAvailableTroopsHighlightState(bool state)
		{
			foreach (RecruitVolunteerVM recruitVolunteerVM in this.VolunteerList)
			{
				foreach (RecruitVolunteerTroopVM recruitVolunteerTroopVM in recruitVolunteerVM.Troops)
				{
					if (recruitVolunteerTroopVM.Wage < Hero.MainHero.Gold && recruitVolunteerTroopVM.PlayerHasEnoughRelation && !recruitVolunteerTroopVM.IsTroopEmpty)
					{
						recruitVolunteerTroopVM.IsHiglightEnabled = state;
					}
				}
			}
		}

		// Token: 0x17000577 RID: 1399
		// (get) Token: 0x060010D6 RID: 4310 RVA: 0x00044150 File Offset: 0x00042350
		// (set) Token: 0x060010D7 RID: 4311 RVA: 0x00044158 File Offset: 0x00042358
		[DataSourceProperty]
		public HintViewModel ResetHint
		{
			get
			{
				return this._resetHint;
			}
			set
			{
				if (value != this._resetHint)
				{
					this._resetHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "ResetHint");
				}
			}
		}

		// Token: 0x17000578 RID: 1400
		// (get) Token: 0x060010D8 RID: 4312 RVA: 0x00044176 File Offset: 0x00042376
		// (set) Token: 0x060010D9 RID: 4313 RVA: 0x0004417E File Offset: 0x0004237E
		[DataSourceProperty]
		public RecruitVolunteerTroopVM FocusedVolunteerTroop
		{
			get
			{
				return this._focusedVolunteerTroop;
			}
			set
			{
				if (value != this._focusedVolunteerTroop)
				{
					this._focusedVolunteerTroop = value;
					base.OnPropertyChangedWithValue<RecruitVolunteerTroopVM>(value, "FocusedVolunteerTroop");
				}
			}
		}

		// Token: 0x17000579 RID: 1401
		// (get) Token: 0x060010DA RID: 4314 RVA: 0x0004419C File Offset: 0x0004239C
		// (set) Token: 0x060010DB RID: 4315 RVA: 0x000441A4 File Offset: 0x000423A4
		[DataSourceProperty]
		public RecruitVolunteerOwnerVM FocusedVolunteerOwner
		{
			get
			{
				return this._focusedVolunteerOwner;
			}
			set
			{
				if (value != this._focusedVolunteerOwner)
				{
					this._focusedVolunteerOwner = value;
					base.OnPropertyChangedWithValue<RecruitVolunteerOwnerVM>(value, "FocusedVolunteerOwner");
				}
			}
		}

		// Token: 0x1700057A RID: 1402
		// (get) Token: 0x060010DC RID: 4316 RVA: 0x000441C2 File Offset: 0x000423C2
		// (set) Token: 0x060010DD RID: 4317 RVA: 0x000441CA File Offset: 0x000423CA
		[DataSourceProperty]
		public HintViewModel PartyWageHint
		{
			get
			{
				return this._partyWageHint;
			}
			set
			{
				if (value != this._partyWageHint)
				{
					this._partyWageHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "PartyWageHint");
				}
			}
		}

		// Token: 0x1700057B RID: 1403
		// (get) Token: 0x060010DE RID: 4318 RVA: 0x000441E8 File Offset: 0x000423E8
		// (set) Token: 0x060010DF RID: 4319 RVA: 0x000441F0 File Offset: 0x000423F0
		[DataSourceProperty]
		public HintViewModel PartyCapacityHint
		{
			get
			{
				return this._partyCapacityHint;
			}
			set
			{
				if (value != this._partyCapacityHint)
				{
					this._partyCapacityHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "PartyCapacityHint");
				}
			}
		}

		// Token: 0x1700057C RID: 1404
		// (get) Token: 0x060010E0 RID: 4320 RVA: 0x0004420E File Offset: 0x0004240E
		// (set) Token: 0x060010E1 RID: 4321 RVA: 0x00044216 File Offset: 0x00042416
		[DataSourceProperty]
		public BasicTooltipViewModel PartySpeedHint
		{
			get
			{
				return this._partySpeedHint;
			}
			set
			{
				if (value != this._partySpeedHint)
				{
					this._partySpeedHint = value;
					base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "PartySpeedHint");
				}
			}
		}

		// Token: 0x1700057D RID: 1405
		// (get) Token: 0x060010E2 RID: 4322 RVA: 0x00044234 File Offset: 0x00042434
		// (set) Token: 0x060010E3 RID: 4323 RVA: 0x0004423C File Offset: 0x0004243C
		[DataSourceProperty]
		public HintViewModel RemainingFoodHint
		{
			get
			{
				return this._remainingFoodHint;
			}
			set
			{
				if (value != this._remainingFoodHint)
				{
					this._remainingFoodHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "RemainingFoodHint");
				}
			}
		}

		// Token: 0x1700057E RID: 1406
		// (get) Token: 0x060010E4 RID: 4324 RVA: 0x0004425A File Offset: 0x0004245A
		// (set) Token: 0x060010E5 RID: 4325 RVA: 0x00044262 File Offset: 0x00042462
		[DataSourceProperty]
		public HintViewModel TotalWealthHint
		{
			get
			{
				return this._totalWealthHint;
			}
			set
			{
				if (value != this._totalWealthHint)
				{
					this._totalWealthHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "TotalWealthHint");
				}
			}
		}

		// Token: 0x1700057F RID: 1407
		// (get) Token: 0x060010E6 RID: 4326 RVA: 0x00044280 File Offset: 0x00042480
		// (set) Token: 0x060010E7 RID: 4327 RVA: 0x00044288 File Offset: 0x00042488
		[DataSourceProperty]
		public HintViewModel TotalCostHint
		{
			get
			{
				return this._totalCostHint;
			}
			set
			{
				if (value != this._totalCostHint)
				{
					this._totalCostHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "TotalCostHint");
				}
			}
		}

		// Token: 0x17000580 RID: 1408
		// (get) Token: 0x060010E8 RID: 4328 RVA: 0x000442A6 File Offset: 0x000424A6
		// (set) Token: 0x060010E9 RID: 4329 RVA: 0x000442AE File Offset: 0x000424AE
		[DataSourceProperty]
		public HintViewModel DoneHint
		{
			get
			{
				return this._doneHint;
			}
			set
			{
				if (value != this._doneHint)
				{
					this._doneHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "DoneHint");
				}
			}
		}

		// Token: 0x17000581 RID: 1409
		// (get) Token: 0x060010EA RID: 4330 RVA: 0x000442CC File Offset: 0x000424CC
		// (set) Token: 0x060010EB RID: 4331 RVA: 0x000442D4 File Offset: 0x000424D4
		[DataSourceProperty]
		public BasicTooltipViewModel RecruitAllHint
		{
			get
			{
				return this._recruitAllHint;
			}
			set
			{
				if (value != this._recruitAllHint)
				{
					this._recruitAllHint = value;
					base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "RecruitAllHint");
				}
			}
		}

		// Token: 0x17000582 RID: 1410
		// (get) Token: 0x060010EC RID: 4332 RVA: 0x000442F2 File Offset: 0x000424F2
		// (set) Token: 0x060010ED RID: 4333 RVA: 0x000442FA File Offset: 0x000424FA
		[DataSourceProperty]
		public int PartyWage
		{
			get
			{
				return this._partyWage;
			}
			set
			{
				if (value != this._partyWage)
				{
					this._partyWage = value;
					base.OnPropertyChangedWithValue(value, "PartyWage");
				}
			}
		}

		// Token: 0x17000583 RID: 1411
		// (get) Token: 0x060010EE RID: 4334 RVA: 0x00044318 File Offset: 0x00042518
		// (set) Token: 0x060010EF RID: 4335 RVA: 0x00044320 File Offset: 0x00042520
		[DataSourceProperty]
		public string PartyCapacityText
		{
			get
			{
				return this._partyCapacityText;
			}
			set
			{
				if (value != this._partyCapacityText)
				{
					this._partyCapacityText = value;
					base.OnPropertyChangedWithValue<string>(value, "PartyCapacityText");
				}
			}
		}

		// Token: 0x17000584 RID: 1412
		// (get) Token: 0x060010F0 RID: 4336 RVA: 0x00044343 File Offset: 0x00042543
		// (set) Token: 0x060010F1 RID: 4337 RVA: 0x0004434B File Offset: 0x0004254B
		[DataSourceProperty]
		public string PartyWageText
		{
			get
			{
				return this._partyWageText;
			}
			set
			{
				if (value != this._partyWageText)
				{
					this._partyWageText = value;
					base.OnPropertyChangedWithValue<string>(value, "PartyWageText");
				}
			}
		}

		// Token: 0x17000585 RID: 1413
		// (get) Token: 0x060010F2 RID: 4338 RVA: 0x0004436E File Offset: 0x0004256E
		// (set) Token: 0x060010F3 RID: 4339 RVA: 0x00044376 File Offset: 0x00042576
		[DataSourceProperty]
		public string RecruitAllText
		{
			get
			{
				return this._recruitAllText;
			}
			set
			{
				if (value != this._recruitAllText)
				{
					this._recruitAllText = value;
					base.OnPropertyChangedWithValue<string>(value, "RecruitAllText");
				}
			}
		}

		// Token: 0x17000586 RID: 1414
		// (get) Token: 0x060010F4 RID: 4340 RVA: 0x00044399 File Offset: 0x00042599
		// (set) Token: 0x060010F5 RID: 4341 RVA: 0x000443A1 File Offset: 0x000425A1
		[DataSourceProperty]
		public string PartySpeedText
		{
			get
			{
				return this._partySpeedText;
			}
			set
			{
				if (value != this._partySpeedText)
				{
					this._partySpeedText = value;
					base.OnPropertyChangedWithValue<string>(value, "PartySpeedText");
				}
			}
		}

		// Token: 0x17000587 RID: 1415
		// (get) Token: 0x060010F6 RID: 4342 RVA: 0x000443C4 File Offset: 0x000425C4
		// (set) Token: 0x060010F7 RID: 4343 RVA: 0x000443CC File Offset: 0x000425CC
		[DataSourceProperty]
		public string ResetAllText
		{
			get
			{
				return this._resetAllText;
			}
			set
			{
				if (value != this._resetAllText)
				{
					this._resetAllText = value;
					base.OnPropertyChangedWithValue<string>(value, "ResetAllText");
				}
			}
		}

		// Token: 0x17000588 RID: 1416
		// (get) Token: 0x060010F8 RID: 4344 RVA: 0x000443EF File Offset: 0x000425EF
		// (set) Token: 0x060010F9 RID: 4345 RVA: 0x000443F7 File Offset: 0x000425F7
		[DataSourceProperty]
		public string CancelText
		{
			get
			{
				return this._cancelText;
			}
			set
			{
				if (value != this._cancelText)
				{
					this._cancelText = value;
					base.OnPropertyChangedWithValue<string>(value, "CancelText");
				}
			}
		}

		// Token: 0x17000589 RID: 1417
		// (get) Token: 0x060010FA RID: 4346 RVA: 0x0004441A File Offset: 0x0004261A
		// (set) Token: 0x060010FB RID: 4347 RVA: 0x00044422 File Offset: 0x00042622
		[DataSourceProperty]
		public string RemainingFoodText
		{
			get
			{
				return this._remainingFoodText;
			}
			set
			{
				if (value != this._remainingFoodText)
				{
					this._remainingFoodText = value;
					base.OnPropertyChangedWithValue<string>(value, "RemainingFoodText");
				}
			}
		}

		// Token: 0x1700058A RID: 1418
		// (get) Token: 0x060010FC RID: 4348 RVA: 0x00044445 File Offset: 0x00042645
		// (set) Token: 0x060010FD RID: 4349 RVA: 0x0004444D File Offset: 0x0004264D
		[DataSourceProperty]
		public string TotalCostText
		{
			get
			{
				return this._totalCostText;
			}
			set
			{
				if (value != this._totalCostText)
				{
					this._totalCostText = value;
					base.OnPropertyChangedWithValue<string>(value, "TotalCostText");
				}
			}
		}

		// Token: 0x1700058B RID: 1419
		// (get) Token: 0x060010FE RID: 4350 RVA: 0x00044470 File Offset: 0x00042670
		// (set) Token: 0x060010FF RID: 4351 RVA: 0x00044478 File Offset: 0x00042678
		[DataSourceProperty]
		public bool Enabled
		{
			get
			{
				return this._enabled;
			}
			set
			{
				if (value != this._enabled)
				{
					this._enabled = value;
					base.OnPropertyChangedWithValue(value, "Enabled");
				}
			}
		}

		// Token: 0x1700058C RID: 1420
		// (get) Token: 0x06001100 RID: 4352 RVA: 0x00044498 File Offset: 0x00042698
		// (set) Token: 0x06001101 RID: 4353 RVA: 0x000444A0 File Offset: 0x000426A0
		[DataSourceProperty]
		public bool IsDoneEnabled
		{
			get
			{
				return this._isDoneEnabled;
			}
			set
			{
				if (value != this._isDoneEnabled)
				{
					this._isDoneEnabled = value;
					base.OnPropertyChangedWithValue(value, "IsDoneEnabled");
				}
			}
		}

		// Token: 0x1700058D RID: 1421
		// (get) Token: 0x06001102 RID: 4354 RVA: 0x000444BE File Offset: 0x000426BE
		// (set) Token: 0x06001103 RID: 4355 RVA: 0x000444C6 File Offset: 0x000426C6
		[DataSourceProperty]
		public bool IsPartyCapacityWarningEnabled
		{
			get
			{
				return this._isPartyCapacityWarningEnabled;
			}
			set
			{
				if (value != this._isPartyCapacityWarningEnabled)
				{
					this._isPartyCapacityWarningEnabled = value;
					base.OnPropertyChangedWithValue(value, "IsPartyCapacityWarningEnabled");
				}
			}
		}

		// Token: 0x1700058E RID: 1422
		// (get) Token: 0x06001104 RID: 4356 RVA: 0x000444E4 File Offset: 0x000426E4
		// (set) Token: 0x06001105 RID: 4357 RVA: 0x000444EC File Offset: 0x000426EC
		[DataSourceProperty]
		public string TitleText
		{
			get
			{
				return this._titleText;
			}
			set
			{
				if (value != this._titleText)
				{
					this._titleText = value;
					base.OnPropertyChangedWithValue<string>(value, "TitleText");
				}
			}
		}

		// Token: 0x1700058F RID: 1423
		// (get) Token: 0x06001106 RID: 4358 RVA: 0x0004450F File Offset: 0x0004270F
		// (set) Token: 0x06001107 RID: 4359 RVA: 0x00044517 File Offset: 0x00042717
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

		// Token: 0x17000590 RID: 1424
		// (get) Token: 0x06001108 RID: 4360 RVA: 0x0004453A File Offset: 0x0004273A
		// (set) Token: 0x06001109 RID: 4361 RVA: 0x00044542 File Offset: 0x00042742
		[DataSourceProperty]
		public bool CanRecruitAll
		{
			get
			{
				return this._canRecruitAll;
			}
			set
			{
				if (value != this._canRecruitAll)
				{
					this._canRecruitAll = value;
					base.OnPropertyChangedWithValue(value, "CanRecruitAll");
				}
			}
		}

		// Token: 0x17000591 RID: 1425
		// (get) Token: 0x0600110A RID: 4362 RVA: 0x00044560 File Offset: 0x00042760
		// (set) Token: 0x0600110B RID: 4363 RVA: 0x00044568 File Offset: 0x00042768
		[DataSourceProperty]
		public int TotalWealth
		{
			get
			{
				return this._totalWealth;
			}
			set
			{
				if (value != this._totalWealth)
				{
					this._totalWealth = value;
					base.OnPropertyChangedWithValue(value, "TotalWealth");
				}
			}
		}

		// Token: 0x17000592 RID: 1426
		// (get) Token: 0x0600110C RID: 4364 RVA: 0x00044586 File Offset: 0x00042786
		// (set) Token: 0x0600110D RID: 4365 RVA: 0x0004458E File Offset: 0x0004278E
		[DataSourceProperty]
		public int PartyCapacity
		{
			get
			{
				return this._partyCapacity;
			}
			set
			{
				if (value != this._partyCapacity)
				{
					this._partyCapacity = value;
					base.OnPropertyChangedWithValue(value, "PartyCapacity");
				}
			}
		}

		// Token: 0x17000593 RID: 1427
		// (get) Token: 0x0600110E RID: 4366 RVA: 0x000445AC File Offset: 0x000427AC
		// (set) Token: 0x0600110F RID: 4367 RVA: 0x000445B4 File Offset: 0x000427B4
		[DataSourceProperty]
		public int InitialPartySize
		{
			get
			{
				return this._initialPartySize;
			}
			set
			{
				if (value != this._initialPartySize)
				{
					this._initialPartySize = value;
					base.OnPropertyChangedWithValue(value, "InitialPartySize");
				}
			}
		}

		// Token: 0x17000594 RID: 1428
		// (get) Token: 0x06001110 RID: 4368 RVA: 0x000445D2 File Offset: 0x000427D2
		// (set) Token: 0x06001111 RID: 4369 RVA: 0x000445DA File Offset: 0x000427DA
		[DataSourceProperty]
		public int CurrentPartySize
		{
			get
			{
				return this._currentPartySize;
			}
			set
			{
				if (value != this._currentPartySize)
				{
					this._currentPartySize = value;
					base.OnPropertyChangedWithValue(value, "CurrentPartySize");
				}
			}
		}

		// Token: 0x17000595 RID: 1429
		// (get) Token: 0x06001112 RID: 4370 RVA: 0x000445F8 File Offset: 0x000427F8
		// (set) Token: 0x06001113 RID: 4371 RVA: 0x00044600 File Offset: 0x00042800
		[DataSourceProperty]
		public MBBindingList<RecruitVolunteerVM> VolunteerList
		{
			get
			{
				return this._volunteerList;
			}
			set
			{
				if (value != this._volunteerList)
				{
					this._volunteerList = value;
					base.OnPropertyChangedWithValue<MBBindingList<RecruitVolunteerVM>>(value, "VolunteerList");
				}
			}
		}

		// Token: 0x17000596 RID: 1430
		// (get) Token: 0x06001114 RID: 4372 RVA: 0x0004461E File Offset: 0x0004281E
		// (set) Token: 0x06001115 RID: 4373 RVA: 0x00044626 File Offset: 0x00042826
		[DataSourceProperty]
		public MBBindingList<RecruitVolunteerTroopVM> TroopsInCart
		{
			get
			{
				return this._troopsInCart;
			}
			set
			{
				if (value != this._troopsInCart)
				{
					this._troopsInCart = value;
					base.OnPropertyChangedWithValue<MBBindingList<RecruitVolunteerTroopVM>>(value, "TroopsInCart");
				}
			}
		}

		// Token: 0x06001116 RID: 4374 RVA: 0x00044644 File Offset: 0x00042844
		public void SetGetKeyTextFromKeyIDFunc(Func<string, TextObject> getKeyTextFromKeyId)
		{
			this._getKeyTextFromKeyId = getKeyTextFromKeyId;
		}

		// Token: 0x06001117 RID: 4375 RVA: 0x0004464D File Offset: 0x0004284D
		private string GetRecruitAllKey()
		{
			if (this.RecruitAllInputKey == null || this._getKeyTextFromKeyId == null)
			{
				return string.Empty;
			}
			return this._getKeyTextFromKeyId(this.RecruitAllInputKey.KeyID).ToString();
		}

		// Token: 0x06001118 RID: 4376 RVA: 0x00044680 File Offset: 0x00042880
		public void SetCancelInputKey(HotKey hotKey)
		{
			this.CancelInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
		}

		// Token: 0x06001119 RID: 4377 RVA: 0x0004468F File Offset: 0x0004288F
		public void SetDoneInputKey(HotKey hotKey)
		{
			this.DoneInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
		}

		// Token: 0x0600111A RID: 4378 RVA: 0x0004469E File Offset: 0x0004289E
		public void SetRecruitAllInputKey(HotKey hotKey)
		{
			this.RecruitAllInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
			this.SetRecruitAllHint();
		}

		// Token: 0x0600111B RID: 4379 RVA: 0x000446B3 File Offset: 0x000428B3
		public void SetResetInputKey(HotKey hotKey)
		{
			this.ResetInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
		}

		// Token: 0x17000597 RID: 1431
		// (get) Token: 0x0600111C RID: 4380 RVA: 0x000446C2 File Offset: 0x000428C2
		// (set) Token: 0x0600111D RID: 4381 RVA: 0x000446CA File Offset: 0x000428CA
		[DataSourceProperty]
		public InputKeyItemVM CancelInputKey
		{
			get
			{
				return this._cancelInputKey;
			}
			set
			{
				if (value != this._cancelInputKey)
				{
					this._cancelInputKey = value;
					base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "CancelInputKey");
				}
			}
		}

		// Token: 0x17000598 RID: 1432
		// (get) Token: 0x0600111E RID: 4382 RVA: 0x000446E8 File Offset: 0x000428E8
		// (set) Token: 0x0600111F RID: 4383 RVA: 0x000446F0 File Offset: 0x000428F0
		[DataSourceProperty]
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

		// Token: 0x17000599 RID: 1433
		// (get) Token: 0x06001120 RID: 4384 RVA: 0x0004470E File Offset: 0x0004290E
		// (set) Token: 0x06001121 RID: 4385 RVA: 0x00044716 File Offset: 0x00042916
		[DataSourceProperty]
		public InputKeyItemVM ResetInputKey
		{
			get
			{
				return this._resetInputKey;
			}
			set
			{
				if (value != this._resetInputKey)
				{
					this._resetInputKey = value;
					base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "ResetInputKey");
				}
			}
		}

		// Token: 0x1700059A RID: 1434
		// (get) Token: 0x06001122 RID: 4386 RVA: 0x00044734 File Offset: 0x00042934
		// (set) Token: 0x06001123 RID: 4387 RVA: 0x0004473C File Offset: 0x0004293C
		[DataSourceProperty]
		public InputKeyItemVM RecruitAllInputKey
		{
			get
			{
				return this._recruitAllInputKey;
			}
			set
			{
				if (value != this._recruitAllInputKey)
				{
					this._recruitAllInputKey = value;
					base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "RecruitAllInputKey");
				}
			}
		}

		// Token: 0x040007A4 RID: 1956
		private TextObject _recruitAllTextObject;

		// Token: 0x040007A5 RID: 1957
		private string _playerDoesntHaveEnoughMoneyStr;

		// Token: 0x040007A6 RID: 1958
		private string _playerIsOverPartyLimitStr;

		// Token: 0x040007A7 RID: 1959
		private Func<string, TextObject> _getKeyTextFromKeyId;

		// Token: 0x040007A8 RID: 1960
		private bool _isAvailableTroopsHighlightApplied;

		// Token: 0x040007A9 RID: 1961
		private string _latestTutorialElementID;

		// Token: 0x040007AA RID: 1962
		private bool _enabled;

		// Token: 0x040007AB RID: 1963
		private bool _isDoneEnabled;

		// Token: 0x040007AC RID: 1964
		private bool _isPartyCapacityWarningEnabled;

		// Token: 0x040007AD RID: 1965
		private bool _canRecruitAll;

		// Token: 0x040007AE RID: 1966
		private string _titleText;

		// Token: 0x040007AF RID: 1967
		private string _doneText;

		// Token: 0x040007B0 RID: 1968
		private string _recruitAllText;

		// Token: 0x040007B1 RID: 1969
		private string _resetAllText;

		// Token: 0x040007B2 RID: 1970
		private string _cancelText;

		// Token: 0x040007B3 RID: 1971
		private int _totalWealth;

		// Token: 0x040007B4 RID: 1972
		private int _partyCapacity;

		// Token: 0x040007B5 RID: 1973
		private int _initialPartySize;

		// Token: 0x040007B6 RID: 1974
		private int _currentPartySize;

		// Token: 0x040007B7 RID: 1975
		private MBBindingList<RecruitVolunteerVM> _volunteerList;

		// Token: 0x040007B8 RID: 1976
		private MBBindingList<RecruitVolunteerTroopVM> _troopsInCart;

		// Token: 0x040007B9 RID: 1977
		private int _partyWage;

		// Token: 0x040007BA RID: 1978
		private string _partyCapacityText = "";

		// Token: 0x040007BB RID: 1979
		private string _partyWageText = "";

		// Token: 0x040007BC RID: 1980
		private string _partySpeedText = "";

		// Token: 0x040007BD RID: 1981
		private string _remainingFoodText = "";

		// Token: 0x040007BE RID: 1982
		private string _totalCostText = "";

		// Token: 0x040007BF RID: 1983
		private RecruitVolunteerTroopVM _focusedVolunteerTroop;

		// Token: 0x040007C0 RID: 1984
		private RecruitVolunteerOwnerVM _focusedVolunteerOwner;

		// Token: 0x040007C1 RID: 1985
		private HintViewModel _partyWageHint;

		// Token: 0x040007C2 RID: 1986
		private HintViewModel _partyCapacityHint;

		// Token: 0x040007C3 RID: 1987
		private BasicTooltipViewModel _partySpeedHint;

		// Token: 0x040007C4 RID: 1988
		private HintViewModel _remainingFoodHint;

		// Token: 0x040007C5 RID: 1989
		private HintViewModel _totalWealthHint;

		// Token: 0x040007C6 RID: 1990
		private HintViewModel _totalCostHint;

		// Token: 0x040007C7 RID: 1991
		private HintViewModel _resetHint;

		// Token: 0x040007C8 RID: 1992
		private HintViewModel _doneHint;

		// Token: 0x040007C9 RID: 1993
		private BasicTooltipViewModel _recruitAllHint;

		// Token: 0x040007CA RID: 1994
		private InputKeyItemVM _cancelInputKey;

		// Token: 0x040007CB RID: 1995
		private InputKeyItemVM _doneInputKey;

		// Token: 0x040007CC RID: 1996
		private InputKeyItemVM _resetInputKey;

		// Token: 0x040007CD RID: 1997
		private InputKeyItemVM _recruitAllInputKey;
	}
}
