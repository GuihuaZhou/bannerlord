using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.CharacterCreationContent
{
	// Token: 0x02000208 RID: 520
	public class CharacterCreationManager
	{
		// Token: 0x170007E4 RID: 2020
		// (get) Token: 0x06001FE5 RID: 8165 RVA: 0x00090394 File Offset: 0x0008E594
		public MBReadOnlyList<NarrativeMenu> NarrativeMenus
		{
			get
			{
				return this._narrativeMenus;
			}
		}

		// Token: 0x170007E5 RID: 2021
		// (get) Token: 0x06001FE6 RID: 8166 RVA: 0x0009039C File Offset: 0x0008E59C
		// (set) Token: 0x06001FE7 RID: 8167 RVA: 0x000903A4 File Offset: 0x0008E5A4
		public CharacterCreationContent CharacterCreationContent { get; private set; }

		// Token: 0x170007E6 RID: 2022
		// (get) Token: 0x06001FE8 RID: 8168 RVA: 0x000903AD File Offset: 0x0008E5AD
		// (set) Token: 0x06001FE9 RID: 8169 RVA: 0x000903B5 File Offset: 0x0008E5B5
		public NarrativeMenu CurrentMenu { get; private set; }

		// Token: 0x170007E7 RID: 2023
		// (get) Token: 0x06001FEA RID: 8170 RVA: 0x000903BE File Offset: 0x0008E5BE
		public int CharacterCreationMenuCount
		{
			get
			{
				return this.NarrativeMenus.Count;
			}
		}

		// Token: 0x170007E8 RID: 2024
		// (get) Token: 0x06001FEB RID: 8171 RVA: 0x000903CB File Offset: 0x0008E5CB
		// (set) Token: 0x06001FEC RID: 8172 RVA: 0x000903D3 File Offset: 0x0008E5D3
		public CharacterCreationStageBase CurrentStage { get; private set; }

		// Token: 0x06001FED RID: 8173 RVA: 0x000903DC File Offset: 0x0008E5DC
		public CharacterCreationManager(CharacterCreationState state)
		{
			this._state = state;
			this._stages = new MBList<CharacterCreationStageBase>();
			this.FaceGenHistory = new FaceGenHistory(new List<UndoRedoKey>(100), new List<UndoRedoKey>(100), new Dictionary<string, float>());
			this._narrativeMenus = new MBList<NarrativeMenu>();
			this.SelectedOptions = new Dictionary<NarrativeMenu, NarrativeMenuOption>();
			this.CharacterCreationContent = new CharacterCreationContent();
			CampaignEventDispatcher.Instance.OnCharacterCreationInitialized(this);
			foreach (KeyValuePair<int, ICharacterCreationContentHandler> keyValuePair in this._handlers)
			{
				keyValuePair.Value.InitializeContent(this);
			}
			foreach (KeyValuePair<int, ICharacterCreationContentHandler> keyValuePair2 in this._handlers)
			{
				keyValuePair2.Value.AfterInitializeContent(this);
			}
		}

		// Token: 0x06001FEE RID: 8174 RVA: 0x000904E8 File Offset: 0x0008E6E8
		public void RegisterCharacterCreationContentHandler(ICharacterCreationContentHandler characterCreationContentHandler, int priority)
		{
			this._handlers.Add(priority, characterCreationContentHandler);
		}

		// Token: 0x06001FEF RID: 8175 RVA: 0x000904F7 File Offset: 0x0008E6F7
		public void AddStage(CharacterCreationStageBase stage)
		{
			this._stages.Add(stage);
		}

		// Token: 0x06001FF0 RID: 8176 RVA: 0x00090508 File Offset: 0x0008E708
		public bool RemoveStage<T>() where T : CharacterCreationStageBase
		{
			for (int i = 0; i < this._stages.Count; i++)
			{
				if (this._stages[i] is T)
				{
					this._stages.RemoveAt(i);
					return true;
				}
			}
			return false;
		}

		// Token: 0x06001FF1 RID: 8177 RVA: 0x00090550 File Offset: 0x0008E750
		public T GetStage<T>() where T : CharacterCreationStageBase
		{
			for (int i = 0; i < this._stages.Count; i++)
			{
				T result;
				if ((result = (this._stages[i] as T)) != null)
				{
					return result;
				}
			}
			return default(T);
		}

		// Token: 0x06001FF2 RID: 8178 RVA: 0x000905A0 File Offset: 0x0008E7A0
		public void NextStage()
		{
			this._stageIndex++;
			if (this.CurrentStage != null)
			{
				CharacterCreationStageBase currentStage = this.CurrentStage;
				if (currentStage != null)
				{
					currentStage.OnFinalize();
				}
				foreach (KeyValuePair<int, ICharacterCreationContentHandler> keyValuePair in this._handlers)
				{
					keyValuePair.Value.OnStageCompleted(this.CurrentStage);
				}
			}
			this._furthestStageIndex = MathF.Max(this._furthestStageIndex, this._stageIndex);
			if (this._stageIndex == this._stages.Count)
			{
				this.ApplyFinalEffects();
				this._state.FinalizeCharacterCreationState();
				return;
			}
			this.ActivateStage(this._stages[this._stageIndex]);
			this._state.Refresh();
		}

		// Token: 0x06001FF3 RID: 8179 RVA: 0x00090680 File Offset: 0x0008E880
		public void PreviousStage()
		{
			CharacterCreationStageBase currentStage = this.CurrentStage;
			if (currentStage != null)
			{
				currentStage.OnFinalize();
			}
			this._stageIndex--;
			this.ActivateStage(this._stages[this._stageIndex]);
			this._state.Refresh();
		}

		// Token: 0x06001FF4 RID: 8180 RVA: 0x000906D0 File Offset: 0x0008E8D0
		public void GoToStage(int stageIndex)
		{
			if (stageIndex >= 0 && stageIndex < this._stages.Count && stageIndex != this._stageIndex && stageIndex <= this._furthestStageIndex)
			{
				CharacterCreationStageBase currentStage = this.CurrentStage;
				if (currentStage != null)
				{
					currentStage.OnFinalize();
				}
				this._stageIndex = stageIndex;
				this.ActivateStage(this._stages[this._stageIndex]);
				this._state.Refresh();
			}
		}

		// Token: 0x06001FF5 RID: 8181 RVA: 0x0009073B File Offset: 0x0008E93B
		private void ActivateStage(CharacterCreationStageBase stage)
		{
			this.CurrentStage = stage;
			if (this._stageIndex == 0)
			{
				this.FaceGenHistory.ClearHistory();
			}
			this._state.OnStageActivated(this.CurrentStage);
		}

		// Token: 0x06001FF6 RID: 8182 RVA: 0x00090768 File Offset: 0x0008E968
		internal void OnStateActivated()
		{
			if (this._stageIndex == -1)
			{
				this.NextStage();
			}
		}

		// Token: 0x06001FF7 RID: 8183 RVA: 0x00090779 File Offset: 0x0008E979
		public int GetIndexOfCurrentStage()
		{
			return this._stageIndex;
		}

		// Token: 0x06001FF8 RID: 8184 RVA: 0x00090781 File Offset: 0x0008E981
		public int GetTotalStagesCount()
		{
			return this._stages.Count;
		}

		// Token: 0x06001FF9 RID: 8185 RVA: 0x0009078E File Offset: 0x0008E98E
		public int GetFurthestIndex()
		{
			return this._furthestStageIndex;
		}

		// Token: 0x06001FFA RID: 8186 RVA: 0x00090796 File Offset: 0x0008E996
		public void AddNewMenu(NarrativeMenu menu)
		{
			this._narrativeMenus.Add(menu);
		}

		// Token: 0x06001FFB RID: 8187 RVA: 0x000907A4 File Offset: 0x0008E9A4
		public NarrativeMenu GetCurrentMenu(int index)
		{
			if (index >= 0 && index < this.NarrativeMenus.Count)
			{
				return this.NarrativeMenus[index];
			}
			return null;
		}

		// Token: 0x06001FFC RID: 8188 RVA: 0x000907C6 File Offset: 0x0008E9C6
		public IEnumerable<NarrativeMenuOption> GetCurrentMenuOptions(int index)
		{
			NarrativeMenu currentMenu = this.GetCurrentMenu(index);
			if (currentMenu == null)
			{
				return null;
			}
			return currentMenu.CharacterCreationMenuOptions;
		}

		// Token: 0x06001FFD RID: 8189 RVA: 0x000907DC File Offset: 0x0008E9DC
		public NarrativeMenu GetNarrativeMenuWithId(string stringId)
		{
			return this.NarrativeMenus.FirstOrDefault((NarrativeMenu m) => m.StringId.Equals(stringId));
		}

		// Token: 0x06001FFE RID: 8190 RVA: 0x00090810 File Offset: 0x0008EA10
		public void DeleteNarrativeMenuWithId(string stringId)
		{
			NarrativeMenu narrativeMenu = null;
			foreach (NarrativeMenu narrativeMenu2 in this.NarrativeMenus)
			{
				if (narrativeMenu2.StringId.Equals(stringId))
				{
					narrativeMenu = narrativeMenu2;
					break;
				}
			}
			if (narrativeMenu != null)
			{
				this._narrativeMenus.Remove(narrativeMenu);
			}
		}

		// Token: 0x06001FFF RID: 8191 RVA: 0x00090880 File Offset: 0x0008EA80
		public void ResetNarrativeMenus()
		{
			this._narrativeMenus.Clear();
			this.ResetMenuOptions();
		}

		// Token: 0x06002000 RID: 8192 RVA: 0x00090893 File Offset: 0x0008EA93
		public void ResetMenuOptions()
		{
			this.SelectedOptions.Clear();
		}

		// Token: 0x06002001 RID: 8193 RVA: 0x000908A0 File Offset: 0x0008EAA0
		public void StartNarrativeStage()
		{
			NarrativeMenu currentMenu = this.NarrativeMenus.FirstOrDefault((NarrativeMenu m) => m.InputMenuId == "start");
			this.CurrentMenu = currentMenu;
			this.ModifyMenuCharacters();
		}

		// Token: 0x06002002 RID: 8194 RVA: 0x000908E8 File Offset: 0x0008EAE8
		public bool TrySwitchToNextMenu()
		{
			string stringId = this.CurrentMenu.StringId;
			this.SelectedOptions[this.CurrentMenu].OnConsequence(this);
			foreach (NarrativeMenu narrativeMenu in this.NarrativeMenus)
			{
				if (narrativeMenu.InputMenuId.Equals(stringId))
				{
					this.CurrentMenu = narrativeMenu;
					this.ModifyMenuCharacters();
					return true;
				}
			}
			return false;
		}

		// Token: 0x06002003 RID: 8195 RVA: 0x0009097C File Offset: 0x0008EB7C
		private void ModifyMenuCharacters()
		{
			List<NarrativeMenuCharacter> characters = this.CurrentMenu.Characters;
			foreach (NarrativeMenuCharacterArgs narrativeMenuCharacterArgs in this.CurrentMenu.GetNarrativeMenuCharacterArgs(this.CharacterCreationContent.SelectedCulture, this.CharacterCreationContent.SelectedTitleType, this))
			{
				foreach (NarrativeMenuCharacter narrativeMenuCharacter in characters)
				{
					if (narrativeMenuCharacter.StringId == narrativeMenuCharacterArgs.CharacterId)
					{
						if (narrativeMenuCharacterArgs.IsHuman)
						{
							MBEquipmentRoster @object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>(narrativeMenuCharacterArgs.EquipmentId);
							if (@object == null)
							{
								Debug.FailedAssert("character creation menu character equipment should not be null! Equipment id: " + narrativeMenuCharacterArgs.EquipmentId, "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CharacterCreationContent\\CharacterCreationManager.cs", "ModifyMenuCharacters", 305);
								@object = Game.Current.ObjectManager.GetObject<MBEquipmentRoster>("player_char_creation_default");
							}
							narrativeMenuCharacter.SetEquipment(@object);
							narrativeMenuCharacter.SetLeftHandItem(narrativeMenuCharacterArgs.LeftHandItemId);
							narrativeMenuCharacter.SetRightHandItem(narrativeMenuCharacterArgs.RightHandItemId);
							narrativeMenuCharacter.ChangeAge((float)narrativeMenuCharacterArgs.Age);
							narrativeMenuCharacter.IsFemale = narrativeMenuCharacterArgs.IsFemale;
						}
						else
						{
							narrativeMenuCharacter.SetMountCreationKey(narrativeMenuCharacterArgs.MountCreationKey);
							narrativeMenuCharacter.SetHorseItemId(narrativeMenuCharacterArgs.LeftHandItemId);
							narrativeMenuCharacter.SetHarnessItemId(narrativeMenuCharacterArgs.RightHandItemId);
						}
						narrativeMenuCharacter.SetAnimationId(narrativeMenuCharacterArgs.AnimationId);
						narrativeMenuCharacter.SetSpawnPointEntityId(narrativeMenuCharacterArgs.SpawnPointEntityId);
						break;
					}
				}
			}
		}

		// Token: 0x06002004 RID: 8196 RVA: 0x00090B4C File Offset: 0x0008ED4C
		public bool TrySwitchToPreviousMenu()
		{
			string inputMenuId = this.CurrentMenu.InputMenuId;
			foreach (NarrativeMenu narrativeMenu in this.NarrativeMenus)
			{
				if (narrativeMenu.StringId.Equals(inputMenuId))
				{
					this.CurrentMenu = narrativeMenu;
					this.ModifyMenuCharacters();
					return true;
				}
			}
			return false;
		}

		// Token: 0x06002005 RID: 8197 RVA: 0x00090BC8 File Offset: 0x0008EDC8
		public void OnNarrativeMenuOptionSelected(NarrativeMenuOption option)
		{
			this.SelectedOptions[this.CurrentMenu] = option;
			option.OnSelect(this);
		}

		// Token: 0x06002006 RID: 8198 RVA: 0x00090BE3 File Offset: 0x0008EDE3
		public IEnumerable<NarrativeMenuOption> GetSuitableNarrativeMenuOptions()
		{
			return from o in this.CurrentMenu.CharacterCreationMenuOptions
			where o.OnCondition(this)
			select o;
		}

		// Token: 0x06002007 RID: 8199 RVA: 0x00090C04 File Offset: 0x0008EE04
		public void ApplyFinalEffects()
		{
			Clan.PlayerClan.Renown = 0f;
			this.CharacterCreationContent.ApplyCulture(this);
			foreach (KeyValuePair<NarrativeMenu, NarrativeMenuOption> keyValuePair in this.SelectedOptions)
			{
				keyValuePair.Value.ApplyFinalEffects(this.CharacterCreationContent);
			}
			TraitLevelingHelper.UpdateTraitXPAccordingToTraitLevels();
			CultureObject culture = CharacterObject.PlayerCharacter.Culture;
			if (culture.StartingPoint.IsNonZero())
			{
				if (NavigationHelper.IsPositionValidForNavigationType(culture.StartingPoint, MobileParty.MainParty.NavigationCapability))
				{
					MobileParty.MainParty.Position = culture.StartingPoint;
				}
				else
				{
					Debug.FailedAssert("Selected culture start pos is invalid!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\CharacterCreationContent\\CharacterCreationManager.cs", "ApplyFinalEffects", 382);
					CampaignVec2 closestNavMeshFaceCenterPositionForPosition = NavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(culture.StartingPoint, Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.MainParty.NavigationCapability));
					MobileParty.MainParty.Position = closestNavMeshFaceCenterPositionForPosition;
				}
			}
			MapState mapState;
			if ((mapState = (GameStateManager.Current.ActiveState as MapState)) != null)
			{
				mapState.Handler.ResetCamera(true, true);
				mapState.Handler.TeleportCameraToMainParty();
			}
			foreach (KeyValuePair<int, ICharacterCreationContentHandler> keyValuePair2 in this._handlers)
			{
				keyValuePair2.Value.OnCharacterCreationFinalize(this);
			}
		}

		// Token: 0x0400095E RID: 2398
		private readonly MBList<CharacterCreationStageBase> _stages;

		// Token: 0x0400095F RID: 2399
		private readonly MBList<NarrativeMenu> _narrativeMenus;

		// Token: 0x04000960 RID: 2400
		public readonly Dictionary<NarrativeMenu, NarrativeMenuOption> SelectedOptions;

		// Token: 0x04000963 RID: 2403
		private SortedList<int, ICharacterCreationContentHandler> _handlers = new SortedList<int, ICharacterCreationContentHandler>();

		// Token: 0x04000964 RID: 2404
		private readonly CharacterCreationState _state;

		// Token: 0x04000965 RID: 2405
		private int _stageIndex = -1;

		// Token: 0x04000967 RID: 2407
		public readonly FaceGenHistory FaceGenHistory;

		// Token: 0x04000968 RID: 2408
		private int _furthestStageIndex;
	}
}
