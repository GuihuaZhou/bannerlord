using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameMenus
{
	// Token: 0x020000E7 RID: 231
	public class GameMenuManager
	{
		// Token: 0x170005E2 RID: 1506
		// (get) Token: 0x06001563 RID: 5475 RVA: 0x00061061 File Offset: 0x0005F261
		// (set) Token: 0x06001564 RID: 5476 RVA: 0x00061069 File Offset: 0x0005F269
		public string NextGameMenuId { get; private set; }

		// Token: 0x06001565 RID: 5477 RVA: 0x00061072 File Offset: 0x0005F272
		public GameMenuManager()
		{
			this.NextGameMenuId = null;
			this._gameMenus = new Dictionary<string, GameMenu>();
		}

		// Token: 0x170005E3 RID: 1507
		// (get) Token: 0x06001566 RID: 5478 RVA: 0x000610A0 File Offset: 0x0005F2A0
		public GameMenu NextMenu
		{
			get
			{
				GameMenu result;
				this._gameMenus.TryGetValue(this.NextGameMenuId, out result);
				return result;
			}
		}

		// Token: 0x06001567 RID: 5479 RVA: 0x000610C2 File Offset: 0x0005F2C2
		public void SetNextMenu(string name)
		{
			this.NextGameMenuId = name;
		}

		// Token: 0x06001568 RID: 5480 RVA: 0x000610CB File Offset: 0x0005F2CB
		public void ExitToLast()
		{
			if (Campaign.Current.CurrentMenuContext != null)
			{
				Game.Current.GameStateManager.LastOrDefault<MapState>().ExitMenuMode();
			}
		}

		// Token: 0x06001569 RID: 5481 RVA: 0x000610ED File Offset: 0x0005F2ED
		internal object GetSelectedRepeatableObject(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.LastSelectedMenuObject;
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetSelectedObject");
			}
			return 0;
		}

		// Token: 0x0600156A RID: 5482 RVA: 0x0006111C File Offset: 0x0005F31C
		internal object ObjectGetCurrentRepeatableObject(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.CurrentRepeatableObject;
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not return CurrentRepeatableIndex");
			}
			return null;
		}

		// Token: 0x0600156B RID: 5483 RVA: 0x00061146 File Offset: 0x0005F346
		public void SetCurrentRepeatableIndex(MenuContext menuContext, int index)
		{
			if (menuContext.GameMenu != null)
			{
				menuContext.GameMenu.CurrentRepeatableIndex = index;
				return;
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run SetCurrentRepeatableIndex");
			}
		}

		// Token: 0x0600156C RID: 5484 RVA: 0x00061170 File Offset: 0x0005F370
		public bool GetMenuOptionConditionsHold(MenuContext menuContext, int menuItemNumber)
		{
			if (menuContext.GameMenu != null)
			{
				if (Game.Current == null)
				{
					throw new MBNullParameterException("Game");
				}
				return menuContext.GameMenu.GetMenuOptionConditionsHold(Game.Current, menuContext, menuItemNumber);
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetMenuOptionConditionsHold");
				}
				return false;
			}
		}

		// Token: 0x0600156D RID: 5485 RVA: 0x000611C0 File Offset: 0x0005F3C0
		public void RefreshMenuOptions(MenuContext menuContext)
		{
			if (menuContext.GameMenu == null)
			{
				Debug.FailedAssert("Current game menu empty, can not run RefreshMenuOptions", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenuManager.cs", "RefreshMenuOptions", 143);
				return;
			}
			if (Game.Current == null)
			{
				Debug.FailedAssert("Game is null during RefreshMenuOptions", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenuManager.cs", "RefreshMenuOptions", 148);
				return;
			}
			menuContext.Handler.OnMenuRefresh();
		}

		// Token: 0x0600156E RID: 5486 RVA: 0x0006121C File Offset: 0x0005F41C
		public void RefreshMenuOptionConditions(MenuContext menuContext)
		{
			if (menuContext.GameMenu == null)
			{
				Debug.FailedAssert("Current game menu empty, can not run RefreshMenuOptionConditions", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenuManager.cs", "RefreshMenuOptionConditions", 161);
				return;
			}
			if (Game.Current == null)
			{
				Debug.FailedAssert("Game is null during RefreshMenuOptionConditions", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenuManager.cs", "RefreshMenuOptionConditions", 166);
				return;
			}
			int virtualMenuOptionAmount = Campaign.Current.GameMenuManager.GetVirtualMenuOptionAmount(menuContext);
			for (int i = 0; i < virtualMenuOptionAmount; i++)
			{
				this.GetMenuOptionConditionsHold(menuContext, i);
			}
		}

		// Token: 0x0600156F RID: 5487 RVA: 0x00061292 File Offset: 0x0005F492
		public string GetMenuOptionIdString(MenuContext menuContext, int menuItemNumber)
		{
			if (menuContext.GameMenu == null)
			{
				Debug.FailedAssert("Current game menu empty, can not run GetMenuOptionIdString", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenuManager.cs", "GetMenuOptionIdString", 183);
				return "";
			}
			return menuContext.GameMenu.GetMenuOptionIdString(menuItemNumber);
		}

		// Token: 0x06001570 RID: 5488 RVA: 0x000612C7 File Offset: 0x0005F4C7
		internal bool GetMenuOptionIsLeave(MenuContext menuContext, int menuItemNumber)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.GetMenuOptionIsLeave(menuItemNumber);
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetMenuOptionText");
			}
			return false;
		}

		// Token: 0x06001571 RID: 5489 RVA: 0x000612F2 File Offset: 0x0005F4F2
		public void RunConsequencesOfMenuOption(MenuContext menuContext, int menuItemNumber)
		{
			if (menuContext.GameMenu != null)
			{
				if (Game.Current == null)
				{
					throw new MBNullParameterException("Game");
				}
				menuContext.GameMenu.RunMenuOptionConsequence(menuContext, menuItemNumber);
				return;
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run RunConsequencesOfMenuOption");
				}
				return;
			}
		}

		// Token: 0x06001572 RID: 5490 RVA: 0x0006132F File Offset: 0x0005F52F
		internal void SetRepeatObjectList(MenuContext menuContext, IEnumerable<object> list)
		{
			if (menuContext.GameMenu != null)
			{
				menuContext.GameMenu.SetMenuRepeatObjects(list);
				return;
			}
			Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameMenus\\GameMenuManager.cs", "SetRepeatObjectList", 237);
		}

		// Token: 0x06001573 RID: 5491 RVA: 0x00061360 File Offset: 0x0005F560
		public TextObject GetVirtualMenuOptionTooltip(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu != null && !menuContext.GameMenu.IsEmpty)
			{
				int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
				if (virtualMenuItemIndex < num)
				{
					return this.GetMenuOptionTooltip(menuContext, 0);
				}
				return this.GetMenuOptionTooltip(menuContext, virtualMenuItemIndex + 1 - num);
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
				}
				return null;
			}
		}

		// Token: 0x06001574 RID: 5492 RVA: 0x000613D7 File Offset: 0x0005F5D7
		public GameMenu.MenuOverlayType GetMenuOverlayType(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.OverlayType;
			}
			return GameMenu.MenuOverlayType.SettlementWithCharacters;
		}

		// Token: 0x06001575 RID: 5493 RVA: 0x000613F0 File Offset: 0x0005F5F0
		public TextObject GetVirtualMenuOptionText(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu != null && !menuContext.GameMenu.IsEmpty)
			{
				int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
				if (virtualMenuItemIndex < num)
				{
					return this.GetMenuOptionText(menuContext, 0);
				}
				return this.GetMenuOptionText(menuContext, virtualMenuItemIndex + 1 - num);
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
				}
				return null;
			}
		}

		// Token: 0x06001576 RID: 5494 RVA: 0x00061468 File Offset: 0x0005F668
		public GameMenuOption GetVirtualGameMenuOption(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetGameMenuOption");
			}
			int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
			if (virtualMenuItemIndex < num)
			{
				return menuContext.GameMenu.GetGameMenuOption(0);
			}
			return menuContext.GameMenu.GetGameMenuOption(virtualMenuItemIndex + 1 - num);
		}

		// Token: 0x06001577 RID: 5495 RVA: 0x000614D0 File Offset: 0x0005F6D0
		public TextObject GetVirtualMenuOptionText2(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu != null && !menuContext.GameMenu.IsEmpty)
			{
				int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
				if (virtualMenuItemIndex < num)
				{
					return this.GetMenuOptionText2(menuContext, 0);
				}
				return this.GetMenuOptionText2(menuContext, virtualMenuItemIndex + 1 - num);
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
				}
				return null;
			}
		}

		// Token: 0x06001578 RID: 5496 RVA: 0x00061547 File Offset: 0x0005F747
		public float GetVirtualMenuProgress(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.Progress;
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
			}
			return 0f;
		}

		// Token: 0x06001579 RID: 5497 RVA: 0x00061575 File Offset: 0x0005F775
		public GameMenu.MenuAndOptionType GetVirtualMenuAndOptionType(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.Type;
			}
			return GameMenu.MenuAndOptionType.RegularMenuOption;
		}

		// Token: 0x0600157A RID: 5498 RVA: 0x0006158C File Offset: 0x0005F78C
		public bool GetVirtualMenuIsWaitActive(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.IsWaitActive;
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
			}
			return false;
		}

		// Token: 0x0600157B RID: 5499 RVA: 0x000615B6 File Offset: 0x0005F7B6
		public float GetVirtualMenuTargetWaitHours(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.TargetWaitHours;
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
			}
			return 0f;
		}

		// Token: 0x0600157C RID: 5500 RVA: 0x000615E4 File Offset: 0x0005F7E4
		public bool GetVirtualMenuOptionIsEnabled(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu != null && !menuContext.GameMenu.IsEmpty)
			{
				int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
				if (virtualMenuItemIndex < num)
				{
					return menuContext.GameMenu.MenuOptions.ElementAt(0).IsEnabled;
				}
				return menuContext.GameMenu.MenuOptions.ElementAt(virtualMenuItemIndex + 1 - num).IsEnabled;
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
				}
				return false;
			}
		}

		// Token: 0x0600157D RID: 5501 RVA: 0x00061678 File Offset: 0x0005F878
		public int GetVirtualMenuOptionAmount(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				int count = menuContext.GameMenu.MenuRepeatObjects.Count;
				int menuItemAmount = menuContext.GameMenu.MenuItemAmount;
				if (count == 0)
				{
					return menuItemAmount;
				}
				return menuItemAmount - 1 + count;
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionAmount");
				}
				return 0;
			}
		}

		// Token: 0x0600157E RID: 5502 RVA: 0x000616CC File Offset: 0x0005F8CC
		public bool GetVirtualMenuOptionIsLeave(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu != null && !menuContext.GameMenu.IsEmpty)
			{
				int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
				if (virtualMenuItemIndex < num)
				{
					return this.GetMenuOptionIsLeave(menuContext, 0);
				}
				return this.GetMenuOptionIsLeave(menuContext, virtualMenuItemIndex + 1 - num);
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionText");
				}
				return false;
			}
		}

		// Token: 0x0600157F RID: 5503 RVA: 0x00061743 File Offset: 0x0005F943
		public GameMenuOption GetLeaveMenuOption(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.GetLeaveMenuOption(Game.Current, menuContext);
			}
			return null;
		}

		// Token: 0x06001580 RID: 5504 RVA: 0x00061760 File Offset: 0x0005F960
		internal void RunConsequenceOfVirtualMenuOption(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu != null)
			{
				int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
				if (virtualMenuItemIndex < num)
				{
					if (menuContext.GameMenu.MenuRepeatObjects.Count > 0)
					{
						menuContext.GameMenu.LastSelectedMenuObject = menuContext.GameMenu.MenuRepeatObjects[virtualMenuItemIndex];
					}
					this.RunConsequencesOfMenuOption(menuContext, 0);
					return;
				}
				this.RunConsequencesOfMenuOption(menuContext, virtualMenuItemIndex + 1 - num);
				return;
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run RunVirtualMenuItemConsequence");
				}
				return;
			}
		}

		// Token: 0x06001581 RID: 5505 RVA: 0x000617F8 File Offset: 0x0005F9F8
		public bool GetVirtualMenuOptionConditionsHold(MenuContext menuContext, int virtualMenuItemIndex)
		{
			if (menuContext.GameMenu != null && !menuContext.GameMenu.IsEmpty)
			{
				int num = (menuContext.GameMenu.MenuRepeatObjects.Count > 0) ? menuContext.GameMenu.MenuRepeatObjects.Count : 1;
				if (virtualMenuItemIndex < num)
				{
					return this.GetMenuOptionConditionsHold(menuContext, 0);
				}
				return this.GetMenuOptionConditionsHold(menuContext, virtualMenuItemIndex + 1 - num);
			}
			else
			{
				if (menuContext.GameMenu == null)
				{
					throw new MBMisuseException("Current game menu empty, can not run GetVirtualMenuOptionConditionsHold");
				}
				return false;
			}
		}

		// Token: 0x06001582 RID: 5506 RVA: 0x0006186F File Offset: 0x0005FA6F
		public void OnFrameTick(MenuContext menuContext, float dt)
		{
			if (menuContext.GameMenu != null)
			{
				menuContext.GameMenu.RunOnTick(menuContext, dt);
			}
		}

		// Token: 0x06001583 RID: 5507 RVA: 0x00061886 File Offset: 0x0005FA86
		public TextObject GetMenuText(MenuContext menuContext)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.GetText();
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetMenuText");
			}
			return null;
		}

		// Token: 0x06001584 RID: 5508 RVA: 0x000618B0 File Offset: 0x0005FAB0
		private TextObject GetMenuOptionText(MenuContext menuContext, int menuItemNumber)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.GetMenuOptionText(menuItemNumber);
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetMenuOptionText");
			}
			return null;
		}

		// Token: 0x06001585 RID: 5509 RVA: 0x000618DB File Offset: 0x0005FADB
		private TextObject GetMenuOptionText2(MenuContext menuContext, int menuItemNumber)
		{
			if (menuContext.GameMenu != null)
			{
				return menuContext.GameMenu.GetMenuOptionText2(menuItemNumber);
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetMenuOptionText");
			}
			return null;
		}

		// Token: 0x06001586 RID: 5510 RVA: 0x00061906 File Offset: 0x0005FB06
		private TextObject GetMenuOptionTooltip(MenuContext menuContext, int menuItemNumber)
		{
			if (menuContext.GameMenu != null && !menuContext.GameMenu.IsEmpty)
			{
				return menuContext.GameMenu.GetMenuOptionTooltip(menuItemNumber);
			}
			if (menuContext.GameMenu == null)
			{
				throw new MBMisuseException("Current game menu empty, can not run GetMenuOptionText");
			}
			return null;
		}

		// Token: 0x06001587 RID: 5511 RVA: 0x0006193E File Offset: 0x0005FB3E
		public void AddGameMenu(GameMenu gameMenu)
		{
			this._gameMenus.Add(gameMenu.StringId, gameMenu);
		}

		// Token: 0x06001588 RID: 5512 RVA: 0x00061954 File Offset: 0x0005FB54
		public void RemoveRelatedGameMenus(object relatedObject)
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, GameMenu> keyValuePair in this._gameMenus)
			{
				if (keyValuePair.Value.RelatedObject == relatedObject)
				{
					list.Add(keyValuePair.Key);
				}
			}
			foreach (string key in list)
			{
				this._gameMenus.Remove(key);
			}
		}

		// Token: 0x06001589 RID: 5513 RVA: 0x00061A08 File Offset: 0x0005FC08
		public void RemoveRelatedGameMenuOptions(object relatedObject)
		{
			foreach (KeyValuePair<string, GameMenu> keyValuePair in this._gameMenus.ToList<KeyValuePair<string, GameMenu>>())
			{
				foreach (GameMenuOption gameMenuOption in keyValuePair.Value.MenuOptions.ToList<GameMenuOption>())
				{
					if (gameMenuOption.RelatedObject == relatedObject)
					{
						keyValuePair.Value.RemoveMenuOption(gameMenuOption);
					}
				}
			}
		}

		// Token: 0x0600158A RID: 5514 RVA: 0x00061AB8 File Offset: 0x0005FCB8
		internal void UnregisterNonReadyObjects()
		{
			MBList<KeyValuePair<string, GameMenu>> mblist = this._gameMenus.ToMBList<KeyValuePair<string, GameMenu>>();
			for (int i = mblist.Count - 1; i >= 0; i--)
			{
				if (!mblist[i].Value.IsReady)
				{
					this._gameMenus.Remove(mblist[i].Key);
				}
			}
		}

		// Token: 0x0600158B RID: 5515 RVA: 0x00061B18 File Offset: 0x0005FD18
		public GameMenu GetGameMenu(string menuId)
		{
			GameMenu result;
			this._gameMenus.TryGetValue(menuId, out result);
			return result;
		}

		// Token: 0x0400070A RID: 1802
		private Dictionary<string, GameMenu> _gameMenus;

		// Token: 0x0400070C RID: 1804
		public int PreviouslySelectedGameMenuItem = -1;

		// Token: 0x0400070D RID: 1805
		public Location NextLocation;

		// Token: 0x0400070E RID: 1806
		public Location PreviousLocation;

		// Token: 0x0400070F RID: 1807
		public List<Location> MenuLocations = new List<Location>();

		// Token: 0x04000710 RID: 1808
		public object PreviouslySelectedGameMenuObject;
	}
}
