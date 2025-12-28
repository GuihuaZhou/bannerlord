using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace TaleWorlds.ScreenSystem
{
	// Token: 0x02000009 RID: 9
	public static class ScreenManager
	{
		// Token: 0x1700001F RID: 31
		// (get) Token: 0x06000091 RID: 145 RVA: 0x00002FDE File Offset: 0x000011DE
		public static IScreenManagerEngineConnection EngineInterface
		{
			get
			{
				return ScreenManager._engineInterface;
			}
		}

		// Token: 0x17000020 RID: 32
		// (get) Token: 0x06000092 RID: 146 RVA: 0x00002FE5 File Offset: 0x000011E5
		// (set) Token: 0x06000093 RID: 147 RVA: 0x00002FEC File Offset: 0x000011EC
		public static float Scale { get; private set; } = 1f;

		// Token: 0x17000021 RID: 33
		// (get) Token: 0x06000094 RID: 148 RVA: 0x00002FF4 File Offset: 0x000011F4
		// (set) Token: 0x06000095 RID: 149 RVA: 0x00002FFB File Offset: 0x000011FB
		public static Vec2 UsableArea
		{
			get
			{
				return ScreenManager._usableArea;
			}
			private set
			{
				if (value != ScreenManager._usableArea)
				{
					ScreenManager._usableArea = value;
					ScreenManager.OnUsableAreaChanged(ScreenManager._usableArea);
				}
			}
		}

		// Token: 0x17000022 RID: 34
		// (get) Token: 0x06000096 RID: 150 RVA: 0x0000301A File Offset: 0x0000121A
		public static bool IsEnterButtonRDown
		{
			get
			{
				return ScreenManager._engineInterface.GetIsEnterButtonRDown();
			}
		}

		// Token: 0x17000023 RID: 35
		// (get) Token: 0x06000097 RID: 151 RVA: 0x00003026 File Offset: 0x00001226
		// (set) Token: 0x06000098 RID: 152 RVA: 0x0000302D File Offset: 0x0000122D
		public static bool IsLateTickInProgress { get; private set; }

		// Token: 0x14000004 RID: 4
		// (add) Token: 0x06000099 RID: 153 RVA: 0x00003038 File Offset: 0x00001238
		// (remove) Token: 0x0600009A RID: 154 RVA: 0x0000306C File Offset: 0x0000126C
		public static event ScreenManager.OnPushScreenEvent OnPushScreen;

		// Token: 0x14000005 RID: 5
		// (add) Token: 0x0600009B RID: 155 RVA: 0x000030A0 File Offset: 0x000012A0
		// (remove) Token: 0x0600009C RID: 156 RVA: 0x000030D4 File Offset: 0x000012D4
		public static event ScreenManager.OnPopScreenEvent OnPopScreen;

		// Token: 0x14000006 RID: 6
		// (add) Token: 0x0600009D RID: 157 RVA: 0x00003108 File Offset: 0x00001308
		// (remove) Token: 0x0600009E RID: 158 RVA: 0x0000313C File Offset: 0x0000133C
		public static event ScreenManager.OnControllerDisconnectedEvent OnControllerDisconnected;

		// Token: 0x17000024 RID: 36
		// (get) Token: 0x0600009F RID: 159 RVA: 0x00003170 File Offset: 0x00001370
		public static List<ScreenLayer> SortedLayers
		{
			get
			{
				if (!ScreenManager._isSortedActiveLayersDirty)
				{
					int count = ScreenManager._sortedLayers.Count;
					ScreenBase topScreen = ScreenManager.TopScreen;
					int? num = (topScreen != null) ? new int?(topScreen.Layers.Count) : null;
					ObservableCollection<GlobalLayer> globalLayers = ScreenManager._globalLayers;
					int? num2 = num + ((globalLayers != null) ? new int?(globalLayers.Count) : null);
					if (count == num2.GetValueOrDefault() & num2 != null)
					{
						goto IL_13F;
					}
				}
				ScreenManager._sortedLayers.Clear();
				if (ScreenManager.TopScreen != null)
				{
					for (int i = 0; i < ScreenManager.TopScreen.Layers.Count; i++)
					{
						ScreenLayer screenLayer = ScreenManager.TopScreen.Layers[i];
						if (screenLayer != null)
						{
							ScreenManager._sortedLayers.Add(screenLayer);
						}
					}
				}
				foreach (GlobalLayer globalLayer in ScreenManager._globalLayers)
				{
					ScreenManager._sortedLayers.Add(globalLayer.Layer);
				}
				ScreenManager._sortedLayers.Sort();
				ScreenManager._isSortedActiveLayersDirty = false;
				IL_13F:
				return ScreenManager._sortedLayers;
			}
		}

		// Token: 0x17000025 RID: 37
		// (get) Token: 0x060000A0 RID: 160 RVA: 0x000032D4 File Offset: 0x000014D4
		// (set) Token: 0x060000A1 RID: 161 RVA: 0x000032DB File Offset: 0x000014DB
		public static ScreenBase TopScreen { get; private set; }

		// Token: 0x17000026 RID: 38
		// (get) Token: 0x060000A2 RID: 162 RVA: 0x000032E3 File Offset: 0x000014E3
		// (set) Token: 0x060000A3 RID: 163 RVA: 0x000032EA File Offset: 0x000014EA
		public static ScreenLayer FocusedLayer { get; private set; }

		// Token: 0x17000027 RID: 39
		// (get) Token: 0x060000A4 RID: 164 RVA: 0x000032F2 File Offset: 0x000014F2
		// (set) Token: 0x060000A5 RID: 165 RVA: 0x000032F9 File Offset: 0x000014F9
		public static ScreenLayer FirstHitLayer { get; private set; }

		// Token: 0x17000028 RID: 40
		// (get) Token: 0x060000A6 RID: 166 RVA: 0x00003301 File Offset: 0x00001501
		public static bool IsWindowFocused
		{
			get
			{
				return ScreenManager._isWindowFocused;
			}
		}

		// Token: 0x060000A7 RID: 167 RVA: 0x00003308 File Offset: 0x00001508
		static ScreenManager()
		{
			ScreenManager._globalLayers = new ObservableCollection<GlobalLayer>();
			ScreenManager._screenList = new ObservableCollection<ScreenBase>();
			ScreenManager._lastMouseActiveKeys = new List<InputKey>();
			ScreenManager._screenList.CollectionChanged += ScreenManager.OnScreenListChanged;
			ScreenManager._globalLayers.CollectionChanged += ScreenManager.OnGlobalListChanged;
			ScreenLayer.OnLayerActiveStateChanged += ScreenManager.OnLayerActiveStateChanged;
			ScreenManager.FocusedLayer = null;
			ScreenManager.FirstHitLayer = null;
			ScreenManager._isWindowFocused = true;
		}

		// Token: 0x060000A8 RID: 168 RVA: 0x000033C4 File Offset: 0x000015C4
		private static void OnLayerActiveStateChanged(ScreenLayer layer)
		{
			ScreenManager.SetSortedLayersDirty();
		}

		// Token: 0x060000A9 RID: 169 RVA: 0x000033CB File Offset: 0x000015CB
		public static void Initialize(IScreenManagerEngineConnection engineInterface)
		{
			ScreenManager._engineInterface = engineInterface;
		}

		// Token: 0x060000AA RID: 170 RVA: 0x000033D4 File Offset: 0x000015D4
		internal static void RefreshGlobalOrder()
		{
			if (!ScreenManager._isRefreshActive)
			{
				ScreenManager._isRefreshActive = true;
				int num = -2000;
				int num2 = 10000;
				for (int i = 0; i < ScreenManager.SortedLayers.Count; i++)
				{
					if (ScreenManager.SortedLayers[i] != null)
					{
						if (!ScreenManager.SortedLayers[i].IsFinalized)
						{
							ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
							if (screenLayer != null && screenLayer.IsActive)
							{
								ScreenLayer screenLayer2 = ScreenManager.SortedLayers[i];
								if (screenLayer2 != null)
								{
									screenLayer2.RefreshGlobalOrder(ref num);
								}
							}
							else
							{
								ScreenLayer screenLayer3 = ScreenManager.SortedLayers[i];
								if (screenLayer3 != null)
								{
									screenLayer3.RefreshGlobalOrder(ref num2);
								}
							}
						}
						ScreenManager._globalOrderDirty = false;
					}
				}
				ScreenManager._isRefreshActive = false;
			}
		}

		// Token: 0x060000AB RID: 171 RVA: 0x0000348B File Offset: 0x0000168B
		public static void RemoveGlobalLayer(GlobalLayer layer)
		{
			Debug.Print("RemoveGlobalLayer", 0, Debug.DebugColor.White, 17592186044416UL);
			ScreenManager._globalLayers.Remove(layer);
			layer.Layer.HandleDeactivate();
			ScreenManager._globalOrderDirty = true;
		}

		// Token: 0x060000AC RID: 172 RVA: 0x000034C0 File Offset: 0x000016C0
		public static void AddGlobalLayer(GlobalLayer layer, bool isFocusable)
		{
			Debug.Print("AddGlobalLayer", 0, Debug.DebugColor.White, 17592186044416UL);
			int index = ScreenManager._globalLayers.Count;
			for (int i = 0; i < ScreenManager._globalLayers.Count; i++)
			{
				if (ScreenManager._globalLayers[i].Layer.InputRestrictions.Order >= layer.Layer.InputRestrictions.Order)
				{
					index = i;
					break;
				}
			}
			ScreenManager._globalLayers.Insert(index, layer);
			layer.Layer.HandleActivate();
			ScreenManager._globalOrderDirty = true;
		}

		// Token: 0x060000AD RID: 173 RVA: 0x00003550 File Offset: 0x00001750
		public static void OnConstrainStateChanged(bool isConstrained)
		{
			Debug.Print("OnConstrainStateChanged: " + isConstrained.ToString(), 0, Debug.DebugColor.White, 17592186044416UL);
			ScreenManager.OnGameWindowFocusChange(!isConstrained);
		}

		// Token: 0x060000AE RID: 174 RVA: 0x00003580 File Offset: 0x00001780
		public static bool ScreenTypeExistsAtList(ScreenBase screen)
		{
			Type type = screen.GetType();
			using (IEnumerator<ScreenBase> enumerator = ScreenManager._screenList.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (enumerator.Current.GetType() == type)
					{
						return true;
					}
				}
			}
			return false;
		}

		// Token: 0x060000AF RID: 175 RVA: 0x000035E0 File Offset: 0x000017E0
		public static void UpdateLayout()
		{
			foreach (GlobalLayer globalLayer in ScreenManager._globalLayers)
			{
				globalLayer.UpdateLayout();
			}
			foreach (ScreenBase screenBase in ScreenManager._screenList)
			{
				screenBase.UpdateLayout();
			}
		}

		// Token: 0x060000B0 RID: 176 RVA: 0x00003664 File Offset: 0x00001864
		public static void SetSuspendLayer(ScreenLayer layer, bool isSuspended)
		{
			if (isSuspended)
			{
				layer.HandleDeactivate();
			}
			else
			{
				layer.HandleActivate();
			}
			layer.LastActiveState = !isSuspended;
		}

		// Token: 0x060000B1 RID: 177 RVA: 0x00003684 File Offset: 0x00001884
		public static void OnFinalize()
		{
			ScreenManager.DeactivateAndFinalizeAllScreens();
			ScreenManager._screenList.CollectionChanged -= ScreenManager.OnScreenListChanged;
			ScreenManager._globalLayers.CollectionChanged -= ScreenManager.OnGlobalListChanged;
			ScreenLayer.OnLayerActiveStateChanged -= ScreenManager.OnLayerActiveStateChanged;
			ScreenManager._screenList = null;
			ScreenManager._globalLayers = null;
			ScreenManager.FocusedLayer = null;
		}

		// Token: 0x060000B2 RID: 178 RVA: 0x000036E8 File Offset: 0x000018E8
		private static void DeactivateAndFinalizeAllScreens()
		{
			Debug.Print("DeactivateAndFinalizeAllScreens", 0, Debug.DebugColor.White, 17592186044416UL);
			for (int i = ScreenManager._screenList.Count - 1; i >= 0; i--)
			{
				ScreenManager._screenList[i].HandlePause();
				ScreenManager._screenList[i].HandleDeactivate();
				ScreenManager._screenList[i].HandleFinalize();
				ScreenManager.OnPopScreenEvent onPopScreen = ScreenManager.OnPopScreen;
				if (onPopScreen != null)
				{
					onPopScreen(ScreenManager._screenList[i]);
				}
				ScreenManager._screenList.RemoveAt(i);
			}
			Common.MemoryCleanupGC(false);
		}

		// Token: 0x060000B3 RID: 179 RVA: 0x00003780 File Offset: 0x00001980
		public static void Tick(float dt)
		{
			for (int i = 0; i < ScreenManager._globalLayers.Count; i++)
			{
				GlobalLayer globalLayer = ScreenManager._globalLayers[i];
				if (globalLayer != null)
				{
					globalLayer.EarlyTick(dt);
				}
			}
			ScreenManager.Update();
			if (ScreenManager.TopScreen != null)
			{
				ScreenManager.TopScreen.FrameTick(dt);
				ScreenBase screenBase = ScreenManager.FindPredecessor(ScreenManager.TopScreen);
				if (screenBase != null)
				{
					screenBase.IdleTick(dt);
				}
			}
			for (int j = 0; j < ScreenManager.SortedLayers.Count; j++)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[j];
				if (screenLayer != null && screenLayer.IsActive && !screenLayer.IsFinalized)
				{
					screenLayer.Tick(dt);
				}
			}
			for (int k = 0; k < ScreenManager._globalLayers.Count; k++)
			{
				GlobalLayer globalLayer2 = ScreenManager._globalLayers[k];
				if (globalLayer2 != null)
				{
					globalLayer2.Tick(dt);
				}
			}
			ScreenManager.LateUpdate(dt);
			for (int l = 0; l < ScreenManager._globalLayers.Count; l++)
			{
				GlobalLayer globalLayer3 = ScreenManager._globalLayers[l];
				if (globalLayer3 != null)
				{
					globalLayer3.LateTick(dt);
				}
			}
			if (ScreenManager.TopScreen != null)
			{
				ScreenManager.TopScreen.PostFrameTick(dt);
			}
			ScreenManager.ShowScreenDebugInformation();
		}

		// Token: 0x060000B4 RID: 180 RVA: 0x000038A0 File Offset: 0x00001AA0
		public static void LateTick(float dt)
		{
			ScreenManager.IsLateTickInProgress = true;
			for (int i = 0; i < ScreenManager.SortedLayers.Count; i++)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
				if (screenLayer != null && screenLayer.IsActive && !screenLayer.IsFinalized)
				{
					screenLayer.RenderTick(dt);
				}
			}
			for (int j = 0; j < ScreenManager.SortedLayers.Count; j++)
			{
				ScreenLayer screenLayer2 = ScreenManager.SortedLayers[j];
				if (screenLayer2 != null && screenLayer2.IsFocusLayer)
				{
					screenLayer2.Input.UnregisterReleasedKeys();
				}
			}
			ScreenManager.IsLateTickInProgress = false;
		}

		// Token: 0x060000B5 RID: 181 RVA: 0x0000392B File Offset: 0x00001B2B
		public static bool OnPlatformScreenKeyboardRequested(string initialText, string descriptionText, int maxLength, int keyboardTypeEnum)
		{
			ScreenManager.OnPlatformTextRequestedDelegate platformTextRequested = ScreenManager.PlatformTextRequested;
			return platformTextRequested != null && platformTextRequested(initialText, descriptionText, maxLength, keyboardTypeEnum);
		}

		// Token: 0x060000B6 RID: 182 RVA: 0x00003941 File Offset: 0x00001B41
		public static void OnOnscreenKeyboardDone(string inputText)
		{
			Input.IsOnScreenKeyboardActive = false;
			ScreenLayer focusedLayer = ScreenManager.FocusedLayer;
			if (focusedLayer == null)
			{
				return;
			}
			focusedLayer.OnOnScreenKeyboardDone(inputText);
		}

		// Token: 0x060000B7 RID: 183 RVA: 0x00003959 File Offset: 0x00001B59
		public static void OnOnscreenKeyboardCanceled()
		{
			Input.IsOnScreenKeyboardActive = false;
			ScreenLayer focusedLayer = ScreenManager.FocusedLayer;
			if (focusedLayer == null)
			{
				return;
			}
			focusedLayer.OnOnScreenKeyboardCanceled();
		}

		// Token: 0x060000B8 RID: 184 RVA: 0x00003970 File Offset: 0x00001B70
		public static void OnGameWindowFocusChange(bool focusGained)
		{
			ScreenManager._isWindowFocused = focusGained;
			if (ScreenManager._isWindowFocused)
			{
				ScreenManager._activeMouseVisible = ScreenManager.EngineInterface.GetMouseVisible();
			}
			Debug.Print("OnGameWindowFocusChange: " + ScreenManager._isWindowFocused.ToString(), 0, Debug.DebugColor.White, 17592186044416UL);
			string str = "TopScreen: ";
			ScreenBase topScreen = ScreenManager.TopScreen;
			string str2;
			if (topScreen == null)
			{
				str2 = null;
			}
			else
			{
				Type type = topScreen.GetType();
				str2 = ((type != null) ? type.Name : null);
			}
			Debug.Print(str + str2, 0, Debug.DebugColor.White, 17592186044416UL);
			bool flag = false;
			if (!Debugger.IsAttached && !flag)
			{
				ScreenBase topScreen2 = ScreenManager.TopScreen;
				if (topScreen2 != null)
				{
					topScreen2.OnFocusChangeOnGameWindow(focusGained);
				}
			}
			if (focusGained)
			{
				Action focusGained2 = ScreenManager.FocusGained;
				if (focusGained2 != null)
				{
					focusGained2();
				}
			}
			ScreenLayer focusedLayer = ScreenManager.FocusedLayer;
			if (focusedLayer == null)
			{
				return;
			}
			focusedLayer.Input.ResetLastDownKeys();
		}

		// Token: 0x14000007 RID: 7
		// (add) Token: 0x060000B9 RID: 185 RVA: 0x00003A3C File Offset: 0x00001C3C
		// (remove) Token: 0x060000BA RID: 186 RVA: 0x00003A70 File Offset: 0x00001C70
		public static event Action FocusGained;

		// Token: 0x14000008 RID: 8
		// (add) Token: 0x060000BB RID: 187 RVA: 0x00003AA4 File Offset: 0x00001CA4
		// (remove) Token: 0x060000BC RID: 188 RVA: 0x00003AD8 File Offset: 0x00001CD8
		public static event ScreenManager.OnPlatformTextRequestedDelegate PlatformTextRequested;

		// Token: 0x060000BD RID: 189 RVA: 0x00003B0C File Offset: 0x00001D0C
		public static void ReplaceTopScreen(ScreenBase screen)
		{
			Debug.Print("ReplaceToTopScreen", 0, Debug.DebugColor.White, 17592186044416UL);
			if (ScreenManager._screenList.Count > 0)
			{
				ScreenManager.TopScreen.HandlePause();
				ScreenManager.TopScreen.HandleDeactivate();
				ScreenManager.TopScreen.HandleFinalize();
				ScreenManager.OnPopScreenEvent onPopScreen = ScreenManager.OnPopScreen;
				if (onPopScreen != null)
				{
					onPopScreen(ScreenManager.TopScreen);
				}
				ScreenManager._screenList.Remove(ScreenManager.TopScreen);
			}
			ScreenManager._screenList.Add(screen);
			screen.HandleInitialize();
			screen.HandleActivate();
			screen.HandleResume();
			ScreenManager._globalOrderDirty = true;
			ScreenManager.OnPushScreenEvent onPushScreen = ScreenManager.OnPushScreen;
			if (onPushScreen == null)
			{
				return;
			}
			onPushScreen(screen);
		}

		// Token: 0x060000BE RID: 190 RVA: 0x00003BB4 File Offset: 0x00001DB4
		public static List<ScreenLayer> GetPersistentInputRestrictions()
		{
			List<ScreenLayer> list = new List<ScreenLayer>();
			foreach (GlobalLayer globalLayer in ScreenManager._globalLayers)
			{
				list.Add(globalLayer.Layer);
			}
			return list;
		}

		// Token: 0x060000BF RID: 191 RVA: 0x00003C0C File Offset: 0x00001E0C
		public static void SetAndActivateRootScreen(ScreenBase screen)
		{
			Debug.Print("SetAndActivateRootScreen", 0, Debug.DebugColor.White, 17592186044416UL);
			if (ScreenManager.TopScreen != null)
			{
				throw new Exception("TopScreen is not null.");
			}
			ScreenManager._screenList.Add(screen);
			screen.HandleInitialize();
			screen.HandleActivate();
			screen.HandleResume();
			ScreenManager._globalOrderDirty = true;
			ScreenManager.OnPushScreenEvent onPushScreen = ScreenManager.OnPushScreen;
			if (onPushScreen == null)
			{
				return;
			}
			onPushScreen(screen);
		}

		// Token: 0x060000C0 RID: 192 RVA: 0x00003C74 File Offset: 0x00001E74
		public static void CleanAndPushScreen(ScreenBase screen)
		{
			Debug.Print("CleanAndPushScreen", 0, Debug.DebugColor.White, 17592186044416UL);
			ScreenManager.DeactivateAndFinalizeAllScreens();
			ScreenManager._screenList.Add(screen);
			screen.HandleInitialize();
			screen.HandleActivate();
			screen.HandleResume();
			ScreenManager._globalOrderDirty = true;
			ScreenManager.OnPushScreenEvent onPushScreen = ScreenManager.OnPushScreen;
			if (onPushScreen == null)
			{
				return;
			}
			onPushScreen(screen);
		}

		// Token: 0x060000C1 RID: 193 RVA: 0x00003CD0 File Offset: 0x00001ED0
		[CommandLineFunctionality.CommandLineArgumentFunction("cb_clear_siege_machine_selection", "ui")]
		public static string ClearSiegeMachineSelection(List<string> args)
		{
			ScreenBase screenBase = ScreenManager._screenList.FirstOrDefault((ScreenBase x) => x.GetType().GetMethod("ClearSiegeMachineSelections") != null);
			if (screenBase != null)
			{
				screenBase.GetType().GetMethod("ClearSiegeMachineSelections").Invoke(screenBase, null);
			}
			return "Siege machine selections have been cleared.";
		}

		// Token: 0x060000C2 RID: 194 RVA: 0x00003D28 File Offset: 0x00001F28
		[CommandLineFunctionality.CommandLineArgumentFunction("cb_copy_battle_layout_to_clipboard", "ui")]
		public static string CopyCustomBattle(List<string> args)
		{
			ScreenBase screenBase = ScreenManager._screenList.FirstOrDefault((ScreenBase x) => x.GetType().GetMethod("CopyBattleLayoutToClipboard") != null);
			if (screenBase != null)
			{
				screenBase.GetType().GetMethod("CopyBattleLayoutToClipboard").Invoke(screenBase, null);
				return "Custom battle layout has been copied to clipboard as text.";
			}
			return "Something went wrong";
		}

		// Token: 0x060000C3 RID: 195 RVA: 0x00003D88 File Offset: 0x00001F88
		[CommandLineFunctionality.CommandLineArgumentFunction("cb_apply_battle_layout_from_string", "ui")]
		public static string ApplyCustomBattleLayout(List<string> args)
		{
			ScreenBase screenBase = ScreenManager._screenList.FirstOrDefault((ScreenBase x) => x.GetType().GetMethod("ApplyCustomBattleLayout") != null);
			if (screenBase == null || args.Count <= 0)
			{
				return "Something went wrong.";
			}
			string text = args.Aggregate((string i, string j) => i + " " + j);
			if (text.Count<char>() > 5)
			{
				screenBase.GetType().GetMethod("ApplyCustomBattleLayout").Invoke(screenBase, new object[]
				{
					text
				});
				return "Applied new layout from text.";
			}
			return "Argument is not right.";
		}

		// Token: 0x060000C4 RID: 196 RVA: 0x00003E2C File Offset: 0x0000202C
		public static void PushScreen(ScreenBase screen)
		{
			Debug.Print("PushScreen", 0, Debug.DebugColor.White, 17592186044416UL);
			if (ScreenManager._screenList.Count > 0)
			{
				ScreenManager.TopScreen.HandlePause();
				if (ScreenManager.TopScreen.IsActive)
				{
					ScreenManager.TopScreen.HandleDeactivate();
				}
			}
			ScreenManager._screenList.Add(screen);
			screen.HandleInitialize();
			screen.HandleActivate();
			screen.HandleResume();
			ScreenManager._globalOrderDirty = true;
			ScreenManager.OnPushScreenEvent onPushScreen = ScreenManager.OnPushScreen;
			if (onPushScreen == null)
			{
				return;
			}
			onPushScreen(screen);
		}

		// Token: 0x060000C5 RID: 197 RVA: 0x00003EB0 File Offset: 0x000020B0
		public static void PopScreen()
		{
			Debug.Print("PopScreen", 0, Debug.DebugColor.White, 17592186044416UL);
			if (ScreenManager._screenList.Count > 0)
			{
				ScreenManager.TopScreen.HandlePause();
				ScreenManager.TopScreen.HandleDeactivate();
				ScreenManager.TopScreen.HandleFinalize();
				Debug.Print("PopScreen - " + ScreenManager.TopScreen.GetType().ToString(), 0, Debug.DebugColor.White, 17592186044416UL);
				ScreenManager.OnPopScreenEvent onPopScreen = ScreenManager.OnPopScreen;
				if (onPopScreen != null)
				{
					onPopScreen(ScreenManager.TopScreen);
				}
				ScreenManager._screenList.Remove(ScreenManager.TopScreen);
			}
			if (ScreenManager._screenList.Count > 0)
			{
				ScreenBase topScreen = ScreenManager.TopScreen;
				ScreenManager.TopScreen.HandleActivate();
				if (topScreen == ScreenManager.TopScreen)
				{
					ScreenManager.TopScreen.HandleResume();
				}
			}
			ScreenManager._globalOrderDirty = true;
		}

		// Token: 0x060000C6 RID: 198 RVA: 0x00003F80 File Offset: 0x00002180
		public static void CleanScreens()
		{
			Debug.Print("CleanScreens", 0, Debug.DebugColor.White, 17592186044416UL);
			while (ScreenManager._screenList.Count > 0)
			{
				ScreenManager.TopScreen.HandlePause();
				ScreenManager.TopScreen.HandleDeactivate();
				ScreenManager.TopScreen.HandleFinalize();
				ScreenManager.OnPopScreenEvent onPopScreen = ScreenManager.OnPopScreen;
				if (onPopScreen != null)
				{
					onPopScreen(ScreenManager.TopScreen);
				}
				ScreenManager._screenList.Remove(ScreenManager.TopScreen);
			}
			ScreenManager._globalOrderDirty = true;
		}

		// Token: 0x060000C7 RID: 199 RVA: 0x00003FFC File Offset: 0x000021FC
		private static ScreenBase FindPredecessor(ScreenBase screen)
		{
			ScreenBase result = null;
			int num = ScreenManager._screenList.IndexOf(screen);
			if (num > 0)
			{
				result = ScreenManager._screenList[num - 1];
			}
			return result;
		}

		// Token: 0x060000C8 RID: 200 RVA: 0x0000402C File Offset: 0x0000222C
		public static void Update(IReadOnlyList<int> lastKeysPressed)
		{
			ScreenManager._lastPressedKeys = lastKeysPressed;
			ScreenBase topScreen = ScreenManager.TopScreen;
			if (topScreen != null && topScreen.IsActive)
			{
				ScreenManager.TopScreen.Update(ScreenManager._lastPressedKeys);
			}
			for (int i = 0; i < ScreenManager._globalLayers.Count; i++)
			{
				GlobalLayer globalLayer = ScreenManager._globalLayers[i];
				if (globalLayer.Layer.IsActive)
				{
					globalLayer.Update(ScreenManager._lastPressedKeys);
				}
			}
		}

		// Token: 0x060000C9 RID: 201 RVA: 0x0000409C File Offset: 0x0000229C
		private static bool? GetMouseInput()
		{
			bool? result = null;
			List<InputKey> activeMouseKeys = ScreenManager.GetActiveMouseKeys();
			if (ScreenManager._lastMouseActiveKeys.Count != activeMouseKeys.Count || !ScreenManager._lastMouseActiveKeys.SequenceEqual(activeMouseKeys))
			{
				result = new bool?(activeMouseKeys.Count > 0);
			}
			ScreenManager._lastMouseActiveKeys = activeMouseKeys;
			return result;
		}

		// Token: 0x060000CA RID: 202 RVA: 0x000040F0 File Offset: 0x000022F0
		private static List<InputKey> GetActiveMouseKeys()
		{
			List<InputKey> list = new List<InputKey>();
			InputKey inputKey = ScreenManager.IsEnterButtonRDown ? InputKey.ControllerRDown : InputKey.ControllerRRight;
			InputKey[] array = new InputKey[6];
			RuntimeHelpers.InitializeArray(array, fieldof(<PrivateImplementationDetails>.D505EF22B665DA54B5D7389BADBF1AD79ED690B7).FieldHandle);
			array[5] = inputKey;
			InputKey[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (Input.IsKeyDown(array2[i]))
				{
					list.Add(array2[i]);
				}
			}
			return list;
		}

		// Token: 0x060000CB RID: 203 RVA: 0x00004150 File Offset: 0x00002350
		public static void EarlyUpdate(Vec2 usableArea)
		{
			ScreenManager.UsableArea = usableArea;
			ScreenManager.RefreshGlobalOrder();
			InputType inputType = InputType.None;
			bool? mouseInput = ScreenManager.GetMouseInput();
			bool? flag = mouseInput;
			bool flag2 = false;
			if (flag.GetValueOrDefault() == flag2 & flag != null)
			{
				ScreenManager._mouseDownLayer = null;
			}
			for (int i = ScreenManager.SortedLayers.Count - 1; i >= 0; i--)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
				if (screenLayer != null && screenLayer.IsActive && !screenLayer.IsFinalized)
				{
					InputType inputType2 = InputType.None;
					InputUsageMask inputUsageMask = screenLayer.InputUsageMask;
					screenLayer.ScreenOrderInLastFrame = i;
					bool isHitThisFrame = screenLayer.IsHitThisFrame;
					screenLayer.IsHitThisFrame = false;
					if (screenLayer.HitTest())
					{
						if (ScreenManager.FirstHitLayer == null)
						{
							ScreenManager.FirstHitLayer = screenLayer;
							ScreenManager._engineInterface.ActivateMouseCursor(screenLayer.ActiveCursor);
						}
						if (ScreenManager._mouseDownLayer == screenLayer || (ScreenManager._mouseDownLayer == null && !inputType.HasAnyFlag(InputType.MouseButton) && inputUsageMask.HasAnyFlag(InputUsageMask.MouseButtons)))
						{
							inputType2 |= InputType.MouseButton;
							inputType |= InputType.MouseButton;
							screenLayer.IsHitThisFrame = true;
							flag = mouseInput;
							flag2 = true;
							if (flag.GetValueOrDefault() == flag2 & flag != null)
							{
								ScreenManager._mouseDownLayer = screenLayer;
							}
						}
						if (!inputType.HasAnyFlag(InputType.MouseWheel) && inputUsageMask.HasAnyFlag(InputUsageMask.MouseWheels))
						{
							inputType2 |= InputType.MouseWheel;
							inputType |= InputType.MouseWheel;
							screenLayer.IsHitThisFrame = true;
						}
					}
					if (!inputType.HasAnyFlag(InputType.Key) && ScreenManager.FocusTest(screenLayer))
					{
						inputType2 |= InputType.Key;
						inputType |= InputType.Key;
					}
					screenLayer.EarlyProcessEvents(inputType2);
				}
				if (ScreenManager._mouseDownLayer == screenLayer)
				{
					screenLayer.IsHitThisFrame = true;
					screenLayer.Input.MouseOnMe = true;
				}
				else
				{
					screenLayer.Input.MouseOnMe = (screenLayer.IsActive && screenLayer.IsHitThisFrame);
				}
			}
			for (int j = ScreenManager._sortedLayers.Count - 1; j >= 0; j--)
			{
				ScreenLayer screenLayer2 = ScreenManager._sortedLayers[j];
				if (screenLayer2.IsFocusLayer)
				{
					screenLayer2.Input.RegisterDownKeys();
				}
				else
				{
					screenLayer2.Input.ResetLastDownKeys();
				}
			}
		}

		// Token: 0x060000CC RID: 204 RVA: 0x00004358 File Offset: 0x00002558
		private static void Update()
		{
			int num = 0;
			for (int i = 0; i < ScreenManager.SortedLayers.Count; i++)
			{
				if (ScreenManager.SortedLayers[i].IsActive)
				{
					num++;
				}
			}
			if (ScreenManager._sortedActiveLayersCopyForUpdate.Length < num)
			{
				ScreenManager._sortedActiveLayersCopyForUpdate = new ScreenLayer[num];
			}
			int num2 = 0;
			for (int j = 0; j < ScreenManager.SortedLayers.Count; j++)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[j];
				if (screenLayer.IsActive)
				{
					ScreenManager._sortedActiveLayersCopyForUpdate[num2] = screenLayer;
					num2++;
				}
			}
			for (int k = num2 - 1; k >= 0; k--)
			{
				ScreenLayer screenLayer2 = ScreenManager._sortedActiveLayersCopyForUpdate[k];
				if (!screenLayer2.IsFinalized)
				{
					screenLayer2.ProcessEvents();
				}
			}
			for (int l = 0; l < ScreenManager._sortedActiveLayersCopyForUpdate.Length; l++)
			{
				ScreenManager._sortedActiveLayersCopyForUpdate[l] = null;
			}
		}

		// Token: 0x060000CD RID: 205 RVA: 0x0000442C File Offset: 0x0000262C
		private static void LateUpdate(float dt)
		{
			for (int i = 0; i < ScreenManager.SortedLayers.Count; i++)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
				if (screenLayer != null && screenLayer.IsActive && !screenLayer.IsFinalized)
				{
					screenLayer.LateUpdate(dt);
				}
			}
			ScreenManager.FirstHitLayer = null;
			ScreenManager.UpdateMouseVisibility();
			if (ScreenManager._globalOrderDirty)
			{
				ScreenManager.RefreshGlobalOrder();
			}
		}

		// Token: 0x060000CE RID: 206 RVA: 0x0000448C File Offset: 0x0000268C
		internal static void UpdateMouseVisibility()
		{
			for (int i = 0; i < ScreenManager.SortedLayers.Count; i++)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
				if (screenLayer.IsActive && screenLayer.InputRestrictions.MouseVisibility)
				{
					if (!ScreenManager._activeMouseVisible)
					{
						ScreenManager.SetMouseVisible(true);
					}
					return;
				}
			}
			if (ScreenManager._activeMouseVisible)
			{
				ScreenManager.SetMouseVisible(false);
			}
		}

		// Token: 0x060000CF RID: 207 RVA: 0x000044EA File Offset: 0x000026EA
		public static bool IsControllerActive()
		{
			return Input.IsControllerConnected && Input.IsGamepadActive && !Input.IsMouseActive && ScreenManager._engineInterface.GetMouseVisible();
		}

		// Token: 0x060000D0 RID: 208 RVA: 0x0000450D File Offset: 0x0000270D
		public static bool IsMouseCursorHidden()
		{
			return !Input.IsMouseActive && ScreenManager._engineInterface.GetMouseVisible();
		}

		// Token: 0x060000D1 RID: 209 RVA: 0x00004522 File Offset: 0x00002722
		public static bool IsMouseCursorActive()
		{
			return Input.IsMouseActive && ScreenManager._engineInterface.GetMouseVisible();
		}

		// Token: 0x060000D2 RID: 210 RVA: 0x00004538 File Offset: 0x00002738
		public static bool IsLayerBlockedAtPosition(ScreenLayer layer, Vector2 position)
		{
			for (int i = ScreenManager.SortedLayers.Count - 1; i >= 0; i--)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
				if (layer == screenLayer)
				{
					return false;
				}
				if (screenLayer != null && screenLayer.IsActive && !screenLayer.IsFinalized && screenLayer.HitTest(position))
				{
					if (screenLayer.InputUsageMask.HasAnyFlag(InputUsageMask.MouseButtons))
					{
						return layer != ScreenManager.SortedLayers[i];
					}
					if (screenLayer.InputUsageMask.HasAnyFlag(InputUsageMask.MouseWheels))
					{
						return layer != ScreenManager.SortedLayers[i];
					}
				}
			}
			return false;
		}

		// Token: 0x060000D3 RID: 211 RVA: 0x000045CB File Offset: 0x000027CB
		private static void SetMouseVisible(bool value)
		{
			ScreenManager._activeMouseVisible = value;
			ScreenManager._engineInterface.SetMouseVisible(value);
		}

		// Token: 0x060000D4 RID: 212 RVA: 0x000045DE File Offset: 0x000027DE
		public static bool GetMouseVisibility()
		{
			return ScreenManager._activeMouseVisible;
		}

		// Token: 0x060000D5 RID: 213 RVA: 0x000045E8 File Offset: 0x000027E8
		public static void TrySetFocus(ScreenLayer layer)
		{
			if (ScreenManager.FocusedLayer != null && ScreenManager.FocusedLayer.InputRestrictions.Order > layer.InputRestrictions.Order && layer.IsActive)
			{
				return;
			}
			if (!layer.IsFocusLayer && !layer.FocusTest())
			{
				return;
			}
			if (ScreenManager.FocusedLayer != layer)
			{
				ScreenLayer focusedLayer = ScreenManager.FocusedLayer;
				if (focusedLayer != null)
				{
					focusedLayer.HandleLoseFocus();
				}
				ScreenManager.FocusedLayer = layer;
				ScreenLayer focusedLayer2 = ScreenManager.FocusedLayer;
				if (focusedLayer2 == null)
				{
					return;
				}
				focusedLayer2.HandleGainFocus();
			}
		}

		// Token: 0x060000D6 RID: 214 RVA: 0x00004660 File Offset: 0x00002860
		public static void TryLoseFocus(ScreenLayer layer)
		{
			if (ScreenManager.FocusedLayer != layer)
			{
				return;
			}
			ScreenLayer focusedLayer = ScreenManager.FocusedLayer;
			if (focusedLayer != null)
			{
				focusedLayer.HandleLoseFocus();
			}
			for (int i = ScreenManager.SortedLayers.Count - 1; i >= 0; i--)
			{
				ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
				if (screenLayer.IsActive && screenLayer.IsFocusLayer && layer != screenLayer)
				{
					ScreenLayer focusedLayer2 = ScreenManager.FocusedLayer;
					if (focusedLayer2 != null)
					{
						focusedLayer2.HandleGainFocus();
					}
					ScreenManager.FocusedLayer = screenLayer;
					return;
				}
			}
			ScreenManager.FocusedLayer = null;
		}

		// Token: 0x060000D7 RID: 215 RVA: 0x000046DA File Offset: 0x000028DA
		private static bool FocusTest(ScreenLayer layer)
		{
			return ScreenManager.FocusedLayer == layer;
		}

		// Token: 0x060000D8 RID: 216 RVA: 0x000046E4 File Offset: 0x000028E4
		public static void OnScaleChange(float newScale)
		{
			ScreenManager.Scale = newScale;
			foreach (GlobalLayer globalLayer in ScreenManager._globalLayers)
			{
				globalLayer.UpdateLayout();
			}
			foreach (ScreenBase screenBase in ScreenManager._screenList)
			{
				screenBase.UpdateLayout();
			}
		}

		// Token: 0x060000D9 RID: 217 RVA: 0x0000476C File Offset: 0x0000296C
		public static void OnControllerDisconnect()
		{
			ScreenManager.OnControllerDisconnectedEvent onControllerDisconnected = ScreenManager.OnControllerDisconnected;
			if (onControllerDisconnected == null)
			{
				return;
			}
			onControllerDisconnected();
		}

		// Token: 0x060000DA RID: 218 RVA: 0x0000477D File Offset: 0x0000297D
		private static void SetSortedLayersDirty()
		{
			ScreenManager._isSortedActiveLayersDirty = true;
		}

		// Token: 0x060000DB RID: 219 RVA: 0x00004788 File Offset: 0x00002988
		private static void OnScreenListChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			Debug.Print("OnScreenListChanged", 0, Debug.DebugColor.White, 17592186044416UL);
			ScreenManager.SetSortedLayersDirty();
			ObservableCollection<ScreenBase> screenList = ScreenManager._screenList;
			if (screenList != null && screenList.Count > 0)
			{
				if (ScreenManager.TopScreen != null)
				{
					ScreenManager.TopScreen.OnAddLayer -= ScreenManager.OnLayerAddedToTopLayer;
					ScreenManager.TopScreen.OnRemoveLayer -= ScreenManager.OnLayerRemovedFromTopLayer;
				}
				ScreenManager.TopScreen = ScreenManager._screenList[ScreenManager._screenList.Count - 1];
				if (ScreenManager.TopScreen != null)
				{
					ScreenManager.TopScreen.OnAddLayer += ScreenManager.OnLayerAddedToTopLayer;
					ScreenManager.TopScreen.OnRemoveLayer += ScreenManager.OnLayerRemovedFromTopLayer;
				}
			}
			else
			{
				if (ScreenManager.TopScreen != null)
				{
					ScreenManager.TopScreen.OnAddLayer -= ScreenManager.OnLayerAddedToTopLayer;
					ScreenManager.TopScreen.OnRemoveLayer -= ScreenManager.OnLayerRemovedFromTopLayer;
				}
				ScreenManager.TopScreen = null;
			}
			ScreenManager.SetSortedLayersDirty();
		}

		// Token: 0x060000DC RID: 220 RVA: 0x0000488A File Offset: 0x00002A8A
		private static void OnLayerAddedToTopLayer(ScreenLayer layer)
		{
			ScreenManager.SetSortedLayersDirty();
		}

		// Token: 0x060000DD RID: 221 RVA: 0x00004891 File Offset: 0x00002A91
		private static void OnLayerRemovedFromTopLayer(ScreenLayer layer)
		{
			ScreenManager.SetSortedLayersDirty();
		}

		// Token: 0x060000DE RID: 222 RVA: 0x00004898 File Offset: 0x00002A98
		private static void OnGlobalListChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			ScreenManager.SetSortedLayersDirty();
		}

		// Token: 0x060000DF RID: 223 RVA: 0x000048A0 File Offset: 0x00002AA0
		[CommandLineFunctionality.CommandLineArgumentFunction("set_screen_debug_information_enabled", "ui")]
		public static string SetScreenDebugInformationEnabled(List<string> args)
		{
			string result = "Usage: ui.set_screen_debug_information_enabled [True/False]";
			if (args.Count != 1)
			{
				return result;
			}
			bool screenDebugInformationEnabled;
			if (bool.TryParse(args[0], out screenDebugInformationEnabled))
			{
				ScreenManager.SetScreenDebugInformationEnabled(screenDebugInformationEnabled);
				return "Success.";
			}
			return result;
		}

		// Token: 0x060000E0 RID: 224 RVA: 0x000048DD File Offset: 0x00002ADD
		public static void SetScreenDebugInformationEnabled(bool isEnabled)
		{
			ScreenManager._isScreenDebugInformationEnabled = isEnabled;
		}

		// Token: 0x060000E1 RID: 225 RVA: 0x000048E8 File Offset: 0x00002AE8
		private static void ShowScreenDebugInformation()
		{
			if (ScreenManager._isScreenDebugInformationEnabled)
			{
				ScreenManager._engineInterface.BeginDebugPanel("Screen Debug Information");
				for (int i = 0; i < ScreenManager.SortedLayers.Count; i++)
				{
					ScreenLayer screenLayer = ScreenManager.SortedLayers[i];
					List<string> list = new List<string>();
					InputUsageMask inputUsageMask = screenLayer.InputRestrictions.InputUsageMask;
					if (screenLayer.IsFocusLayer && ScreenManager.FocusedLayer == screenLayer)
					{
						list.Add("(FocusLayer)");
					}
					list.Add(screenLayer.Name);
					if (screenLayer.InputRestrictions.MouseVisibility)
					{
						list.Add("MouseVisibile");
					}
					if (screenLayer.InputRestrictions.InputUsageMask != InputUsageMask.Invalid)
					{
						list.Add("Input");
					}
					string text = string.Join(" - ", list);
					if (ScreenManager._engineInterface.DrawDebugTreeNode(string.Format("{0}###{1}.{2}.{3}", new object[]
					{
						text,
						screenLayer.Name,
						i,
						screenLayer.Name.GetDeterministicHashCode()
					})))
					{
						screenLayer.DrawDebugInfo();
						ScreenManager._engineInterface.PopDebugTreeNode();
					}
				}
				ScreenManager._engineInterface.EndDebugPanel();
			}
		}

		// Token: 0x060000E2 RID: 226 RVA: 0x00004A09 File Offset: 0x00002C09
		private static void OnUsableAreaChanged(Vec2 newUsableArea)
		{
			ScreenManager.UpdateLayout();
		}

		// Token: 0x04000028 RID: 40
		private static IScreenManagerEngineConnection _engineInterface;

		// Token: 0x0400002A RID: 42
		private static Vec2 _usableArea = new Vec2(1f, 1f);

		// Token: 0x0400002F RID: 47
		private static ObservableCollection<ScreenBase> _screenList;

		// Token: 0x04000030 RID: 48
		private static ObservableCollection<GlobalLayer> _globalLayers;

		// Token: 0x04000031 RID: 49
		private static List<ScreenLayer> _sortedLayers = new List<ScreenLayer>(16);

		// Token: 0x04000032 RID: 50
		private static ScreenLayer[] _sortedActiveLayersCopyForUpdate = new ScreenLayer[16];

		// Token: 0x04000033 RID: 51
		private static bool _isSortedActiveLayersDirty = true;

		// Token: 0x04000034 RID: 52
		private static bool _isScreenDebugInformationEnabled;

		// Token: 0x04000035 RID: 53
		private static List<InputKey> _lastMouseActiveKeys;

		// Token: 0x04000037 RID: 55
		private static bool _activeMouseVisible;

		// Token: 0x04000038 RID: 56
		private static IReadOnlyList<int> _lastPressedKeys;

		// Token: 0x04000039 RID: 57
		private static bool _globalOrderDirty;

		// Token: 0x0400003C RID: 60
		private static ScreenLayer _mouseDownLayer;

		// Token: 0x0400003D RID: 61
		private static bool _isWindowFocused;

		// Token: 0x0400003E RID: 62
		private static bool _isRefreshActive = false;

		// Token: 0x0200000D RID: 13
		// (Invoke) Token: 0x060000EC RID: 236
		public delegate void OnPushScreenEvent(ScreenBase pushedScreen);

		// Token: 0x0200000E RID: 14
		// (Invoke) Token: 0x060000F0 RID: 240
		public delegate void OnPopScreenEvent(ScreenBase poppedScreen);

		// Token: 0x0200000F RID: 15
		// (Invoke) Token: 0x060000F4 RID: 244
		public delegate void OnControllerDisconnectedEvent();

		// Token: 0x02000010 RID: 16
		// (Invoke) Token: 0x060000F8 RID: 248
		public delegate bool OnPlatformTextRequestedDelegate(string initialText, string descriptionText, int maxLength, int keyboardTypeEnum);
	}
}
