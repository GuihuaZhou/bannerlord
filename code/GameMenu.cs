using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameMenus
{
	// Token: 0x020000E1 RID: 225
	public class GameMenu
	{
		// Token: 0x170005C9 RID: 1481
		// (get) Token: 0x06001500 RID: 5376 RVA: 0x00060381 File Offset: 0x0005E581
		// (set) Token: 0x06001501 RID: 5377 RVA: 0x00060389 File Offset: 0x0005E589
		public GameMenu.MenuAndOptionType Type { get; private set; }

		// Token: 0x170005CA RID: 1482
		// (get) Token: 0x06001502 RID: 5378 RVA: 0x00060392 File Offset: 0x0005E592
		// (set) Token: 0x06001503 RID: 5379 RVA: 0x0006039A File Offset: 0x0005E59A
		public string StringId { get; private set; }

		// Token: 0x170005CB RID: 1483
		// (get) Token: 0x06001504 RID: 5380 RVA: 0x000603A3 File Offset: 0x0005E5A3
		// (set) Token: 0x06001505 RID: 5381 RVA: 0x000603AB File Offset: 0x0005E5AB
		public object RelatedObject { get; private set; }

		// Token: 0x170005CC RID: 1484
		// (get) Token: 0x06001506 RID: 5382 RVA: 0x000603B4 File Offset: 0x0005E5B4
		// (set) Token: 0x06001507 RID: 5383 RVA: 0x000603BC File Offset: 0x0005E5BC
		public TextObject MenuTitle { get; private set; }

		// Token: 0x170005CD RID: 1485
		// (get) Token: 0x06001508 RID: 5384 RVA: 0x000603C5 File Offset: 0x0005E5C5
		// (set) Token: 0x06001509 RID: 5385 RVA: 0x000603CD File Offset: 0x0005E5CD
		public GameMenu.MenuOverlayType OverlayType { get; private set; }

		// Token: 0x170005CE RID: 1486
		// (get) Token: 0x0600150A RID: 5386 RVA: 0x000603D6 File Offset: 0x0005E5D6
		// (set) Token: 0x0600150B RID: 5387 RVA: 0x000603DE File Offset: 0x0005E5DE
		public bool IsReady { get; private set; }

		// Token: 0x170005CF RID: 1487
		// (get) Token: 0x0600150C RID: 5388 RVA: 0x000603E7 File Offset: 0x0005E5E7
		public int MenuItemAmount
		{
			get
			{
				return this._menuItems.Count;
			}
		}

		// Token: 0x170005D0 RID: 1488
		// (get) Token: 0x0600150D RID: 5389 RVA: 0x000603F4 File Offset: 0x0005E5F4
		// (set) Token: 0x0600150E RID: 5390 RVA: 0x000603FC File Offset: 0x0005E5FC
		public List<object> MenuRepeatObjects { get; private set; } = new List<object>();

		// Token: 0x170005D1 RID: 1489
		// (get) Token: 0x0600150F RID: 5391 RVA: 0x00060405 File Offset: 0x0005E605
		public object CurrentRepeatableObject
		{
			get
			{
				if (this.MenuRepeatObjects.Count <= this.CurrentRepeatableIndex)
				{
					return null;
				}
				return this.MenuRepeatObjects[this.CurrentRepeatableIndex];
			}
		}

		// Token: 0x170005D2 RID: 1490
		// (get) Token: 0x06001510 RID: 5392 RVA: 0x0006042D File Offset: 0x0005E62D
		// (set) Token: 0x06001511 RID: 5393 RVA: 0x00060435 File Offset: 0x0005E635
		public bool IsWaitMenu { get; private set; }

		// Token: 0x170005D3 RID: 1491
		// (get) Token: 0x06001512 RID: 5394 RVA: 0x0006043E File Offset: 0x0005E63E
		// (set) Token: 0x06001513 RID: 5395 RVA: 0x00060446 File Offset: 0x0005E646
		public bool IsWaitActive { get; private set; }

		// Token: 0x170005D4 RID: 1492
		// (get) Token: 0x06001514 RID: 5396 RVA: 0x0006044F File Offset: 0x0005E64F
		public bool IsEmpty
		{
			get
			{
				return this.MenuRepeatObjects.Count == 0 && this.MenuItemAmount == 0;
			}
		}

		// Token: 0x170005D5 RID: 1493
		// (get) Token: 0x06001515 RID: 5397 RVA: 0x00060469 File Offset: 0x0005E669
		// (set) Token: 0x06001516 RID: 5398 RVA: 0x00060471 File Offset: 0x0005E671
		public float Progress { get; private set; }

		// Token: 0x170005D6 RID: 1494
		// (get) Token: 0x06001517 RID: 5399 RVA: 0x0006047A File Offset: 0x0005E67A
		// (set) Token: 0x06001518 RID: 5400 RVA: 0x00060482 File Offset: 0x0005E682
		public float TargetWaitHours { get; private set; }

		// Token: 0x170005D7 RID: 1495
		// (get) Token: 0x06001519 RID: 5401 RVA: 0x0006048B File Offset: 0x0005E68B
		// (set) Token: 0x0600151A RID: 5402 RVA: 0x00060493 File Offset: 0x0005E693
		public OnTickDelegate OnTick { get; private set; }

		// Token: 0x170005D8 RID: 1496
		// (get) Token: 0x0600151B RID: 5403 RVA: 0x0006049C File Offset: 0x0005E69C
		// (set) Token: 0x0600151C RID: 5404 RVA: 0x000604A4 File Offset: 0x0005E6A4
		public OnConditionDelegate OnCondition { get; private set; }

		// Token: 0x170005D9 RID: 1497
		// (get) Token: 0x0600151D RID: 5405 RVA: 0x000604AD File Offset: 0x0005E6AD
		// (set) Token: 0x0600151E RID: 5406 RVA: 0x000604B5 File Offset: 0x0005E6B5
		public OnConsequenceDelegate OnConsequence { get; private set; }

		// Token: 0x170005DA RID: 1498
		// (get) Token: 0x0600151F RID: 5407 RVA: 0x000604BE File Offset: 0x0005E6BE
		// (set) Token: 0x06001520 RID: 5408 RVA: 0x000604C6 File Offset: 0x0005E6C6
		public int CurrentRepeatableIndex { get; set; }

		// Token: 0x170005DB RID: 1499
		// (get) Token: 0x06001521 RID: 5409 RVA: 0x000604CF File Offset: 0x0005E6CF
		public IEnumerable<GameMenuOption> MenuOptions
		{
			get
			{
				return this._menuItems;
			}
		}

		// Token: 0x06001522 RID: 5410 RVA: 0x000604D7 File Offset: 0x0005E6D7
		internal GameMenu(string idString)
		{
			this.StringId = idString;
			this._menuItems = new List<GameMenuOption>();
		}

		// Token: 0x06001523 RID: 5411 RVA: 0x000604FC File Offset: 0x0005E6FC
		internal void Initialize(TextObject text, OnInitDelegate initDelegate, GameMenu.MenuOverlayType overlay, GameMenu.MenuFlags flags = GameMenu.MenuFlags.None, object relatedObject = null)
		{
			this.CurrentRepeatableIndex = 0;
			this.LastSelectedMenuObject = null;
			this._defaultText = text;
			this.OnInit = initDelegate;
			this.OverlayType = overlay;
			this.AutoSelectFirst = ((flags & GameMenu.MenuFlags.AutoSelectFirst) > GameMenu.MenuFlags.None);
			this.RelatedObject = relatedObject;
			this.IsReady = true;
		}

		// Token: 0x06001524 RID: 5412 RVA: 0x00060548 File Offset: 0x0005E748
		internal void Initialize(TextObject text, OnInitDelegate initDelegate, OnConditionDelegate condition, OnConsequenceDelegate consequence, OnTickDelegate tick, GameMenu.MenuAndOptionType type, GameMenu.MenuOverlayType overlay, float targetWaitHours = 0f, GameMenu.MenuFlags flags = GameMenu.MenuFlags.None, object relatedObject = null)
		{
			this.CurrentRepeatableIndex = 0;
			this.LastSelectedMenuObject = null;
			this._defaultText = text;
			this.OnInit = initDelegate;
			this.OverlayType = overlay;
			this.AutoSelectFirst = ((flags & GameMenu.MenuFlags.AutoSelectFirst) > GameMenu.MenuFlags.None);
			this.RelatedObject = relatedObject;
			this.OnConsequence = consequence;
			this.OnCondition = condition;
			this.Type = type;
			this.OnTick = tick;
			this.TargetWaitHours = targetWaitHours;
			this.IsWaitMenu = (type > GameMenu.MenuAndOptionType.RegularMenuOption);
			this.IsReady = true;
		}

		// Token: 0x06001525 RID: 5413 RVA: 0x000605C7 File Offset: 0x0005E7C7
		public void SetMenuRepeatObjects(IEnumerable<object> list)
		{
			this.MenuRepeatObjects = list.ToList<object>();
		}

		// Token: 0x06001526 RID: 5414 RVA: 0x000605D5 File Offset: 0x0005E7D5
		private void AddOption(GameMenuOption newOption, int index = -1)
		{
			if (index >= 0 && this._menuItems.Count >= index)
			{
				this._menuItems.Insert(index, newOption);
				return;
			}
			this._menuItems.Add(newOption);
		}

		// Token: 0x06001527 RID: 5415 RVA: 0x00060603 File Offset: 0x0005E803
		public bool GetMenuOptionConditionsHold(Game game, MenuContext menuContext, int menuItemNumber)
		{
			if (this.IsWaitMenu)
			{
				return this._menuItems[menuItemNumber].GetConditionsHold(game, menuContext) && this.RunWaitMenuCondition(menuContext);
			}
			return this._menuItems[menuItemNumber].GetConditionsHold(game, menuContext);
		}

		// Token: 0x06001528 RID: 5416 RVA: 0x0006063F File Offset: 0x0005E83F
		public TextObject GetMenuOptionText(int menuItemNumber)
		{
			return this._menuItems[menuItemNumber].Text;
		}

		// Token: 0x06001529 RID: 5417 RVA: 0x00060652 File Offset: 0x0005E852
		public GameMenuOption GetGameMenuOption(int menuItemNumber)
		{
			return this._menuItems[menuItemNumber];
		}

		// Token: 0x0600152A RID: 5418 RVA: 0x00060660 File Offset: 0x0005E860
		public TextObject GetMenuOptionText2(int menuItemNumber)
		{
			return this._menuItems[menuItemNumber].Text2;
		}

		// Token: 0x0600152B RID: 5419 RVA: 0x00060673 File Offset: 0x0005E873
		public string GetMenuOptionIdString(int menuItemNumber)
		{
			return this._menuItems[menuItemNumber].IdString;
		}

		// Token: 0x0600152C RID: 5420 RVA: 0x00060686 File Offset: 0x0005E886
		public TextObject GetMenuOptionTooltip(int menuItemNumber)
		{
			return this._menuItems[menuItemNumber].Tooltip;
		}

		// Token: 0x0600152D RID: 5421 RVA: 0x00060699 File Offset: 0x0005E899
		public bool GetMenuOptionIsLeave(int menuItemNumber)
		{
			return this._menuItems[menuItemNumber].IsLeave;
		}

		// Token: 0x0600152E RID: 5422 RVA: 0x000606AC File Offset: 0x0005E8AC
		public void SetProgressOfWaitingInMenu(float progress)
		{
			this.Progress = progress;
		}

		// Token: 0x0600152F RID: 5423 RVA: 0x000606B5 File Offset: 0x0005E8B5
		public void SetTargetedWaitingTimeAndInitialProgress(float targetedWaitingTime, float initialProgress)
		{
			this.TargetWaitHours = targetedWaitingTime;
			this.SetProgressOfWaitingInMenu(initialProgress);
		}

		// Token: 0x06001530 RID: 5424 RVA: 0x000606C8 File Offset: 0x0005E8C8
		public GameMenuOption GetLeaveMenuOption(Game game, MenuContext menuContext)
		{
			for (int i = 0; i < this._menuItems.Count; i++)
			{
				if (this._menuItems[i].IsLeave && this._menuItems[i].IsEnabled && this._menuItems[i].GetConditionsHold(game, menuContext))
				{
					return this._menuItems[i];
				}
			}
			return null;
		}

		// Token: 0x06001531 RID: 5425 RVA: 0x00060734 File Offset: 0x0005E934
		public void RunOnTick(MenuContext menuContext, float dt)
		{
			if (this.IsWaitMenu && this.IsWaitActive)
			{
				if (this.OnTick != null)
				{
					MenuCallbackArgs args = new MenuCallbackArgs(menuContext, this.MenuTitle);
					this.OnTick(args, CampaignTime.Now - this._previousTickTime);
					this._previousTickTime = CampaignTime.Now;
				}
				if (this.Progress >= 1f)
				{
					this.EndWait();
					this.RunWaitMenuConsequence(menuContext);
				}
			}
		}

		// Token: 0x06001532 RID: 5426 RVA: 0x000607A8 File Offset: 0x0005E9A8
		public bool RunWaitMenuCondition(MenuContext menuContext)
		{
			if (this.OnCondition != null)
			{
				MenuCallbackArgs args = new MenuCallbackArgs(menuContext, this.MenuTitle);
				bool flag = this.OnCondition(args);
				if (flag && !this.IsWaitActive)
				{
					menuContext.GameMenu.StartWait();
				}
				return flag;
			}
			return true;
		}

		// Token: 0x06001533 RID: 5427 RVA: 0x000607F0 File Offset: 0x0005E9F0
		public void RunWaitMenuConsequence(MenuContext menuContext)
		{
			if (this.OnConsequence != null)
			{
				MenuCallbackArgs args = new MenuCallbackArgs(menuContext, this.MenuTitle);
				this.OnConsequence(args);
			}
		}

		// Token: 0x06001534 RID: 5428 RVA: 0x00060820 File Offset: 0x0005EA20
		public void RunMenuOptionConsequence(MenuContext menuContext, int menuItemNumber)
		{
			if (menuItemNumber >= this._menuItems.Count || menuItemNumber < 0)
			{
				Debug.FailedAssert("menuItemNumber out of bounds", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenu.cs", "RunMenuOptionConsequence", 263);
				menuItemNumber = this._menuItems.Count - 1;
			}
			GameMenuOption gameMenuOption = this._menuItems[menuItemNumber];
			if (gameMenuOption.IsLeave && this.IsWaitMenu)
			{
				this.EndWait();
			}
			gameMenuOption.RunConsequence(menuContext);
			if (Campaign.Current != null)
			{
				CampaignEventDispatcher.Instance.OnGameMenuOptionSelected(this, gameMenuOption);
			}
		}

		// Token: 0x06001535 RID: 5429 RVA: 0x000608A4 File Offset: 0x0005EAA4
		public void StartWait()
		{
			this._previousTickTime = CampaignTime.Now;
			this.IsWaitActive = true;
			Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
		}

		// Token: 0x06001536 RID: 5430 RVA: 0x000608C3 File Offset: 0x0005EAC3
		public void EndWait()
		{
			this.IsWaitActive = false;
			Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
		}

		// Token: 0x06001537 RID: 5431 RVA: 0x000608D7 File Offset: 0x0005EAD7
		private void ResetVariablesOnInit()
		{
			this.Progress = 0f;
			this.CurrentRepeatableIndex = 0;
			this.MenuRepeatObjects.Clear();
		}

		// Token: 0x06001538 RID: 5432 RVA: 0x000608F8 File Offset: 0x0005EAF8
		public void RunOnInit(Game game, MenuContext menuContext)
		{
			this.ResetVariablesOnInit();
			MenuCallbackArgs menuCallbackArgs = new MenuCallbackArgs(menuContext, this.MenuTitle);
			if (this.OnInit != null)
			{
				Debug.Print("[GAME MENU] " + menuContext.GameMenu.StringId, 0, Debug.DebugColor.White, 17592186044416UL);
				this.OnInit(menuCallbackArgs);
				this.MenuTitle = menuCallbackArgs.MenuTitle;
			}
			CampaignEventDispatcher.Instance.OnGameMenuOpened(menuCallbackArgs);
		}

		// Token: 0x06001539 RID: 5433 RVA: 0x0006096C File Offset: 0x0005EB6C
		public void PreInit(MenuContext menuContext)
		{
			MenuCallbackArgs args = new MenuCallbackArgs(menuContext, this.MenuTitle);
			CampaignEventDispatcher.Instance.BeforeGameMenuOpened(args);
		}

		// Token: 0x0600153A RID: 5434 RVA: 0x00060994 File Offset: 0x0005EB94
		public void AfterInit(MenuContext menuContext)
		{
			MenuCallbackArgs args = new MenuCallbackArgs(menuContext, this.MenuTitle);
			CampaignEventDispatcher.Instance.AfterGameMenuInitialized(args);
		}

		// Token: 0x0600153B RID: 5435 RVA: 0x000609B9 File Offset: 0x0005EBB9
		public TextObject GetText()
		{
			return this._defaultText;
		}

		// Token: 0x170005DC RID: 1500
		// (get) Token: 0x0600153C RID: 5436 RVA: 0x000609C1 File Offset: 0x0005EBC1
		// (set) Token: 0x0600153D RID: 5437 RVA: 0x000609C9 File Offset: 0x0005EBC9
		public bool AutoSelectFirst { get; private set; }

		// Token: 0x0600153E RID: 5438 RVA: 0x000609D4 File Offset: 0x0005EBD4
		public static void ActivateGameMenu(string menuId)
		{
			Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
			if (Campaign.Current.CurrentMenuContext == null)
			{
				Campaign.Current.GameMenuManager.SetNextMenu(menuId);
				MapState mapState = Game.Current.GameStateManager.LastOrDefault<MapState>();
				if (mapState != null)
				{
					mapState.EnterMenuMode();
				}
				bool flag;
				if (mapState == null)
				{
					flag = (null != null);
				}
				else
				{
					MenuContext menuContext = mapState.MenuContext;
					flag = (((menuContext != null) ? menuContext.GameMenu : null) != null);
				}
				if (flag)
				{
					GameMenu gameMenu = mapState.MenuContext.GameMenu;
					if (gameMenu != null && gameMenu.IsWaitMenu)
					{
						mapState.MenuContext.GameMenu.StartWait();
						return;
					}
				}
			}
			else
			{
				GameMenu.SwitchToMenu(menuId);
			}
		}

		// Token: 0x0600153F RID: 5439 RVA: 0x00060A6C File Offset: 0x0005EC6C
		public static void SwitchToMenu(string menuId)
		{
			Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
			MenuContext currentMenuContext = Campaign.Current.CurrentMenuContext;
			if (currentMenuContext != null)
			{
				currentMenuContext.SwitchToMenu(menuId);
				if (currentMenuContext.GameMenu.IsWaitMenu && Campaign.Current.TimeControlMode == CampaignTimeControlMode.Stop)
				{
					currentMenuContext.GameMenu.StartWait();
					return;
				}
			}
			else
			{
				Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenu.cs", "SwitchToMenu", 384);
			}
		}

		// Token: 0x06001540 RID: 5440 RVA: 0x00060AD7 File Offset: 0x0005ECD7
		public static void ExitToLast()
		{
			Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
			Campaign.Current.GameMenuManager.ExitToLast();
		}

		// Token: 0x06001541 RID: 5441 RVA: 0x00060AF4 File Offset: 0x0005ECF4
		internal void AddOption(string optionId, TextObject optionText, GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence, int index = -1, bool isLeave = false, bool isRepeatable = false, object relatedObject = null)
		{
			this.AddOption(new GameMenuOption(GameMenu.MenuAndOptionType.RegularMenuOption, optionId, optionText, optionText, condition, consequence, isLeave, isRepeatable, relatedObject), index);
		}

		// Token: 0x06001542 RID: 5442 RVA: 0x00060B1B File Offset: 0x0005ED1B
		internal void RemoveMenuOption(GameMenuOption option)
		{
			this._menuItems.Remove(option);
		}

		// Token: 0x040006EF RID: 1775
		private TextObject _defaultText;

		// Token: 0x040006F5 RID: 1781
		public OnInitDelegate OnInit;

		// Token: 0x040006F8 RID: 1784
		public object LastSelectedMenuObject;

		// Token: 0x04000700 RID: 1792
		private CampaignTime _previousTickTime;

		// Token: 0x04000701 RID: 1793
		private readonly List<GameMenuOption> _menuItems;

		// Token: 0x0200055C RID: 1372
		public enum MenuOverlayType
		{
			// Token: 0x04001690 RID: 5776
			None,
			// Token: 0x04001691 RID: 5777
			SettlementWithParties,
			// Token: 0x04001692 RID: 5778
			SettlementWithCharacters,
			// Token: 0x04001693 RID: 5779
			SettlementWithBoth,
			// Token: 0x04001694 RID: 5780
			Encounter
		}

		// Token: 0x0200055D RID: 1373
		public enum MenuFlags
		{
			// Token: 0x04001696 RID: 5782
			None,
			// Token: 0x04001697 RID: 5783
			AutoSelectFirst
		}

		// Token: 0x0200055E RID: 1374
		public enum MenuAndOptionType
		{
			// Token: 0x04001699 RID: 5785
			RegularMenuOption,
			// Token: 0x0400169A RID: 5786
			WaitMenuShowProgressAndHoursOption,
			// Token: 0x0400169B RID: 5787
			WaitMenuShowOnlyProgressOption,
			// Token: 0x0400169C RID: 5788
			WaitMenuHideProgressAndHoursOption
		}
	}
}
