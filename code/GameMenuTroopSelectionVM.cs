using System;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TroopSelection
{
	// Token: 0x0200009F RID: 159
	public class GameMenuTroopSelectionVM : ViewModel
	{
		// Token: 0x06000F2F RID: 3887 RVA: 0x0003F090 File Offset: 0x0003D290
		public GameMenuTroopSelectionVM(TroopRoster fullRoster, TroopRoster initialSelections, Func<CharacterObject, bool> canChangeChangeStatusOfTroop, Action<TroopRoster> onDone, int maxSelectableTroopCount, int minSelectableTroopCount)
		{
			this._canChangeChangeStatusOfTroop = canChangeChangeStatusOfTroop;
			this._onDone = onDone;
			this._fullRoster = fullRoster;
			this._initialSelections = initialSelections;
			this._maxSelectableTroopCount = maxSelectableTroopCount;
			this._minSelectableTroopCount = minSelectableTroopCount;
			this.DoneHint = new HintViewModel();
			this.InitList();
			this.RefreshValues();
			this.OnCurrentSelectedAmountChange();
		}

		// Token: 0x06000F30 RID: 3888 RVA: 0x0003F110 File Offset: 0x0003D310
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.TitleText = this._titleTextObject.ToString();
			this.CurrentSelectedAmountTitle = this._chosenTitleTextObject.ToString();
			this.DoneText = GameTexts.FindText("str_done", null).ToString();
			this.CancelText = GameTexts.FindText("str_cancel", null).ToString();
			this.ClearSelectionText = new TextObject("{=QMNWbmao}Clear Selection", null).ToString();
			this.RefreshDoneHint();
		}

		// Token: 0x06000F31 RID: 3889 RVA: 0x0003F190 File Offset: 0x0003D390
		private void RefreshDoneHint()
		{
			if (this.IsDoneEnabled)
			{
				this.DoneHint.HintText = TextObject.GetEmpty();
				return;
			}
			if (this._currentTotalSelectedTroopCount < this._minSelectableTroopCount)
			{
				this.DoneHint.HintText = new TextObject("{=LlV29O9B}You must select at least {TROOP_COUNT} troops", null).SetTextVariable("TROOP_COUNT", this._minSelectableTroopCount);
				return;
			}
			this.DoneHint.HintText = new TextObject("{=TdWQM7QZ}You must select less than {TROOP_COUNT} troops", null).SetTextVariable("TROOP_COUNT", this._maxSelectableTroopCount);
		}

		// Token: 0x06000F32 RID: 3890 RVA: 0x0003F214 File Offset: 0x0003D414
		private void InitList()
		{
			this.Troops = new MBBindingList<TroopSelectionItemVM>();
			this._currentTotalSelectedTroopCount = 0;
			foreach (TroopRosterElement troopRosterElement in this._fullRoster.GetTroopRoster())
			{
				TroopSelectionItemVM troopSelectionItemVM = new TroopSelectionItemVM(troopRosterElement, new Action<TroopSelectionItemVM>(this.OnAddCount), new Action<TroopSelectionItemVM>(this.OnRemoveCount));
				troopSelectionItemVM.IsLocked = (!this._canChangeChangeStatusOfTroop(troopRosterElement.Character) || troopRosterElement.Number - troopRosterElement.WoundedNumber <= 0);
				this.Troops.Add(troopSelectionItemVM);
				int troopCount = this._initialSelections.GetTroopCount(troopRosterElement.Character);
				if (troopCount > 0)
				{
					troopSelectionItemVM.CurrentAmount = troopCount;
					this._currentTotalSelectedTroopCount += troopCount;
				}
			}
			this.Troops.Sort(new TroopItemComparer());
		}

		// Token: 0x06000F33 RID: 3891 RVA: 0x0003F314 File Offset: 0x0003D514
		private void OnRemoveCount(TroopSelectionItemVM troopItem)
		{
			if (troopItem.CurrentAmount > 0)
			{
				int num = 1;
				if (this.IsEntireStackModifierActive)
				{
					num = troopItem.CurrentAmount;
				}
				else if (this.IsFiveStackModifierActive)
				{
					num = MathF.Min(troopItem.CurrentAmount, 5);
				}
				troopItem.CurrentAmount -= num;
				this._currentTotalSelectedTroopCount -= num;
			}
			this.OnCurrentSelectedAmountChange();
		}

		// Token: 0x06000F34 RID: 3892 RVA: 0x0003F374 File Offset: 0x0003D574
		private void OnAddCount(TroopSelectionItemVM troopItem)
		{
			if (troopItem.CurrentAmount < troopItem.MaxAmount && this._currentTotalSelectedTroopCount < this._maxSelectableTroopCount)
			{
				int num = 1;
				if (this.IsEntireStackModifierActive)
				{
					num = MathF.Min(troopItem.MaxAmount - troopItem.CurrentAmount, this._maxSelectableTroopCount - this._currentTotalSelectedTroopCount);
				}
				else if (this.IsFiveStackModifierActive)
				{
					num = MathF.Min(MathF.Min(troopItem.MaxAmount - troopItem.CurrentAmount, this._maxSelectableTroopCount - this._currentTotalSelectedTroopCount), 5);
				}
				troopItem.CurrentAmount += num;
				this._currentTotalSelectedTroopCount += num;
			}
			this.OnCurrentSelectedAmountChange();
		}

		// Token: 0x06000F35 RID: 3893 RVA: 0x0003F41C File Offset: 0x0003D61C
		private void OnCurrentSelectedAmountChange()
		{
			foreach (TroopSelectionItemVM troopSelectionItemVM in this.Troops)
			{
				troopSelectionItemVM.IsRosterFull = (this._currentTotalSelectedTroopCount >= this._maxSelectableTroopCount);
			}
			GameTexts.SetVariable("LEFT", this._currentTotalSelectedTroopCount);
			GameTexts.SetVariable("RIGHT", this._maxSelectableTroopCount);
			this.CurrentSelectedAmountText = GameTexts.FindText("str_LEFT_over_RIGHT_in_paranthesis", null).ToString();
			this.IsDoneEnabled = (this._currentTotalSelectedTroopCount <= this._maxSelectableTroopCount && this._currentTotalSelectedTroopCount >= this._minSelectableTroopCount);
			this.RefreshDoneHint();
		}

		// Token: 0x06000F36 RID: 3894 RVA: 0x0003F4DC File Offset: 0x0003D6DC
		private void OnDone()
		{
			TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
			foreach (TroopSelectionItemVM troopSelectionItemVM in this.Troops)
			{
				if (troopSelectionItemVM.CurrentAmount > 0)
				{
					troopRoster.AddToCounts(troopSelectionItemVM.Troop.Character, troopSelectionItemVM.CurrentAmount, false, 0, 0, true, -1);
				}
			}
			this.IsEnabled = false;
			this._onDone.DynamicInvokeWithLog(new object[]
			{
				troopRoster
			});
		}

		// Token: 0x06000F37 RID: 3895 RVA: 0x0003F56C File Offset: 0x0003D76C
		public void ExecuteDone()
		{
			if (this.GetAvailableSelectableTroopCount() > 0)
			{
				string text = new TextObject("{=z2Slmx4N}There are still some room for more soldiers. Do you want to proceed?", null).ToString();
				InformationManager.ShowInquiry(new InquiryData(this.TitleText, text, true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), new Action(this.OnDone), null, "", 0f, null, null, null), false, false);
				return;
			}
			this.OnDone();
		}

		// Token: 0x06000F38 RID: 3896 RVA: 0x0003F5EC File Offset: 0x0003D7EC
		private int GetAvailableSelectableTroopCount()
		{
			int num = 0;
			foreach (TroopSelectionItemVM troopSelectionItemVM in this.Troops)
			{
				if (!troopSelectionItemVM.IsLocked && troopSelectionItemVM.CurrentAmount < troopSelectionItemVM.MaxAmount)
				{
					num += troopSelectionItemVM.MaxAmount - troopSelectionItemVM.CurrentAmount;
				}
			}
			if (this._currentTotalSelectedTroopCount + num > this._maxSelectableTroopCount)
			{
				num = this._maxSelectableTroopCount - this._currentTotalSelectedTroopCount;
			}
			return num;
		}

		// Token: 0x06000F39 RID: 3897 RVA: 0x0003F67C File Offset: 0x0003D87C
		public void ExecuteCancel()
		{
			this.IsEnabled = false;
		}

		// Token: 0x06000F3A RID: 3898 RVA: 0x0003F685 File Offset: 0x0003D885
		public void ExecuteReset()
		{
			this.InitList();
			this.OnCurrentSelectedAmountChange();
		}

		// Token: 0x06000F3B RID: 3899 RVA: 0x0003F693 File Offset: 0x0003D893
		public void ExecuteClearSelection()
		{
			this.Troops.ApplyActionOnAllItems(delegate(TroopSelectionItemVM troopItem)
			{
				if (this._canChangeChangeStatusOfTroop(troopItem.Troop.Character))
				{
					int currentAmount = troopItem.CurrentAmount;
					for (int i = 0; i < currentAmount; i++)
					{
						troopItem.ExecuteRemove();
					}
				}
			});
		}

		// Token: 0x06000F3C RID: 3900 RVA: 0x0003F6AC File Offset: 0x0003D8AC
		public override void OnFinalize()
		{
			base.OnFinalize();
			InputKeyItemVM cancelInputKey = this.CancelInputKey;
			if (cancelInputKey != null)
			{
				cancelInputKey.OnFinalize();
			}
			InputKeyItemVM doneInputKey = this.DoneInputKey;
			if (doneInputKey != null)
			{
				doneInputKey.OnFinalize();
			}
			InputKeyItemVM resetInputKey = this.ResetInputKey;
			if (resetInputKey == null)
			{
				return;
			}
			resetInputKey.OnFinalize();
		}

		// Token: 0x06000F3D RID: 3901 RVA: 0x0003F6E6 File Offset: 0x0003D8E6
		public void SetCancelInputKey(HotKey hotkey)
		{
			this.CancelInputKey = InputKeyItemVM.CreateFromHotKey(hotkey, true);
		}

		// Token: 0x06000F3E RID: 3902 RVA: 0x0003F6F5 File Offset: 0x0003D8F5
		public void SetDoneInputKey(HotKey hotkey)
		{
			this.DoneInputKey = InputKeyItemVM.CreateFromHotKey(hotkey, true);
		}

		// Token: 0x06000F3F RID: 3903 RVA: 0x0003F704 File Offset: 0x0003D904
		public void SetResetInputKey(HotKey hotkey)
		{
			this.ResetInputKey = InputKeyItemVM.CreateFromHotKey(hotkey, true);
		}

		// Token: 0x170004E8 RID: 1256
		// (get) Token: 0x06000F40 RID: 3904 RVA: 0x0003F713 File Offset: 0x0003D913
		// (set) Token: 0x06000F41 RID: 3905 RVA: 0x0003F71B File Offset: 0x0003D91B
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

		// Token: 0x170004E9 RID: 1257
		// (get) Token: 0x06000F42 RID: 3906 RVA: 0x0003F739 File Offset: 0x0003D939
		// (set) Token: 0x06000F43 RID: 3907 RVA: 0x0003F741 File Offset: 0x0003D941
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

		// Token: 0x170004EA RID: 1258
		// (get) Token: 0x06000F44 RID: 3908 RVA: 0x0003F75F File Offset: 0x0003D95F
		// (set) Token: 0x06000F45 RID: 3909 RVA: 0x0003F767 File Offset: 0x0003D967
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

		// Token: 0x170004EB RID: 1259
		// (get) Token: 0x06000F46 RID: 3910 RVA: 0x0003F785 File Offset: 0x0003D985
		// (set) Token: 0x06000F47 RID: 3911 RVA: 0x0003F78D File Offset: 0x0003D98D
		[DataSourceProperty]
		public bool IsEnabled
		{
			get
			{
				return this._isEnabled;
			}
			set
			{
				if (value != this._isEnabled)
				{
					this._isEnabled = value;
					base.OnPropertyChangedWithValue(value, "IsEnabled");
				}
			}
		}

		// Token: 0x170004EC RID: 1260
		// (get) Token: 0x06000F48 RID: 3912 RVA: 0x0003F7AB File Offset: 0x0003D9AB
		// (set) Token: 0x06000F49 RID: 3913 RVA: 0x0003F7B3 File Offset: 0x0003D9B3
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

		// Token: 0x170004ED RID: 1261
		// (get) Token: 0x06000F4A RID: 3914 RVA: 0x0003F7D1 File Offset: 0x0003D9D1
		// (set) Token: 0x06000F4B RID: 3915 RVA: 0x0003F7D9 File Offset: 0x0003D9D9
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

		// Token: 0x170004EE RID: 1262
		// (get) Token: 0x06000F4C RID: 3916 RVA: 0x0003F7F7 File Offset: 0x0003D9F7
		// (set) Token: 0x06000F4D RID: 3917 RVA: 0x0003F7FF File Offset: 0x0003D9FF
		[DataSourceProperty]
		public MBBindingList<TroopSelectionItemVM> Troops
		{
			get
			{
				return this._troops;
			}
			set
			{
				if (value != this._troops)
				{
					this._troops = value;
					base.OnPropertyChangedWithValue<MBBindingList<TroopSelectionItemVM>>(value, "Troops");
				}
			}
		}

		// Token: 0x170004EF RID: 1263
		// (get) Token: 0x06000F4E RID: 3918 RVA: 0x0003F81D File Offset: 0x0003DA1D
		// (set) Token: 0x06000F4F RID: 3919 RVA: 0x0003F825 File Offset: 0x0003DA25
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

		// Token: 0x170004F0 RID: 1264
		// (get) Token: 0x06000F50 RID: 3920 RVA: 0x0003F848 File Offset: 0x0003DA48
		// (set) Token: 0x06000F51 RID: 3921 RVA: 0x0003F850 File Offset: 0x0003DA50
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

		// Token: 0x170004F1 RID: 1265
		// (get) Token: 0x06000F52 RID: 3922 RVA: 0x0003F873 File Offset: 0x0003DA73
		// (set) Token: 0x06000F53 RID: 3923 RVA: 0x0003F87B File Offset: 0x0003DA7B
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

		// Token: 0x170004F2 RID: 1266
		// (get) Token: 0x06000F54 RID: 3924 RVA: 0x0003F89E File Offset: 0x0003DA9E
		// (set) Token: 0x06000F55 RID: 3925 RVA: 0x0003F8A6 File Offset: 0x0003DAA6
		[DataSourceProperty]
		public string ClearSelectionText
		{
			get
			{
				return this._clearSelectionText;
			}
			set
			{
				if (value != this._clearSelectionText)
				{
					this._clearSelectionText = value;
					base.OnPropertyChangedWithValue<string>(value, "ClearSelectionText");
				}
			}
		}

		// Token: 0x170004F3 RID: 1267
		// (get) Token: 0x06000F56 RID: 3926 RVA: 0x0003F8C9 File Offset: 0x0003DAC9
		// (set) Token: 0x06000F57 RID: 3927 RVA: 0x0003F8D1 File Offset: 0x0003DAD1
		[DataSourceProperty]
		public string CurrentSelectedAmountText
		{
			get
			{
				return this._currentSelectedAmountText;
			}
			set
			{
				if (value != this._currentSelectedAmountText)
				{
					this._currentSelectedAmountText = value;
					base.OnPropertyChangedWithValue<string>(value, "CurrentSelectedAmountText");
				}
			}
		}

		// Token: 0x170004F4 RID: 1268
		// (get) Token: 0x06000F58 RID: 3928 RVA: 0x0003F8F4 File Offset: 0x0003DAF4
		// (set) Token: 0x06000F59 RID: 3929 RVA: 0x0003F8FC File Offset: 0x0003DAFC
		[DataSourceProperty]
		public string CurrentSelectedAmountTitle
		{
			get
			{
				return this._currentSelectedAmountTitle;
			}
			set
			{
				if (value != this._currentSelectedAmountTitle)
				{
					this._currentSelectedAmountTitle = value;
					base.OnPropertyChangedWithValue<string>(value, "CurrentSelectedAmountTitle");
				}
			}
		}

		// Token: 0x040006ED RID: 1773
		private readonly Action<TroopRoster> _onDone;

		// Token: 0x040006EE RID: 1774
		private readonly TroopRoster _fullRoster;

		// Token: 0x040006EF RID: 1775
		private readonly TroopRoster _initialSelections;

		// Token: 0x040006F0 RID: 1776
		private readonly Func<CharacterObject, bool> _canChangeChangeStatusOfTroop;

		// Token: 0x040006F1 RID: 1777
		private readonly int _maxSelectableTroopCount;

		// Token: 0x040006F2 RID: 1778
		private readonly int _minSelectableTroopCount;

		// Token: 0x040006F3 RID: 1779
		private readonly TextObject _titleTextObject = new TextObject("{=uQgNPJnc}Manage Troops", null);

		// Token: 0x040006F4 RID: 1780
		private readonly TextObject _chosenTitleTextObject = new TextObject("{=InqmgBiF}Chosen Crew", null);

		// Token: 0x040006F5 RID: 1781
		private int _currentTotalSelectedTroopCount;

		// Token: 0x040006F6 RID: 1782
		public bool IsFiveStackModifierActive;

		// Token: 0x040006F7 RID: 1783
		public bool IsEntireStackModifierActive;

		// Token: 0x040006F8 RID: 1784
		private InputKeyItemVM _doneInputKey;

		// Token: 0x040006F9 RID: 1785
		private InputKeyItemVM _cancelInputKey;

		// Token: 0x040006FA RID: 1786
		private InputKeyItemVM _resetInputKey;

		// Token: 0x040006FB RID: 1787
		private bool _isEnabled;

		// Token: 0x040006FC RID: 1788
		private bool _isDoneEnabled;

		// Token: 0x040006FD RID: 1789
		private HintViewModel _doneHint;

		// Token: 0x040006FE RID: 1790
		private string _doneText;

		// Token: 0x040006FF RID: 1791
		private string _cancelText;

		// Token: 0x04000700 RID: 1792
		private string _titleText;

		// Token: 0x04000701 RID: 1793
		private string _clearSelectionText;

		// Token: 0x04000702 RID: 1794
		private string _currentSelectedAmountText;

		// Token: 0x04000703 RID: 1795
		private string _currentSelectedAmountTitle;

		// Token: 0x04000704 RID: 1796
		private MBBindingList<TroopSelectionItemVM> _troops;
	}
}
