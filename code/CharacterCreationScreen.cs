using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace SandBox.View.CharacterCreation
{
	// Token: 0x0200007B RID: 123
	[GameStateScreen(typeof(CharacterCreationState))]
	public class CharacterCreationScreen : ScreenBase, ICharacterCreationStateHandler, IGameStateListener
	{
		// Token: 0x0600054C RID: 1356 RVA: 0x00027F28 File Offset: 0x00026128
		public CharacterCreationScreen(CharacterCreationState characterCreationState)
		{
			this._characterCreationStateState = characterCreationState;
			characterCreationState.Handler = this;
			this._stageViews = new Dictionary<Type, Type>();
			this.CollectUnorderedStages();
			this._cultureAmbientSoundEvent = SoundEvent.CreateEventFromString("event:/mission/ambient/special/charactercreation", null);
			this._cultureAmbientSoundEvent.Play();
			this.CreateGenericScene();
		}

		// Token: 0x0600054D RID: 1357 RVA: 0x00027F80 File Offset: 0x00026180
		private void CreateGenericScene()
		{
			this._genericScene = Scene.CreateNewScene(true, false, DecalAtlasGroup.All, "mono_renderscene");
			SceneInitializationData sceneInitializationData = default(SceneInitializationData);
			sceneInitializationData.InitPhysicsWorld = false;
			this._genericScene.Read("character_menu_new", ref sceneInitializationData, "");
			this._agentRendererSceneController = MBAgentRendererSceneController.CreateNewAgentRendererSceneController(this._genericScene);
		}

		// Token: 0x0600054E RID: 1358 RVA: 0x00027FD8 File Offset: 0x000261D8
		private void StopSound()
		{
			SoundManager.SetGlobalParameter("MissionCulture", 0f);
			SoundEvent cultureAmbientSoundEvent = this._cultureAmbientSoundEvent;
			if (cultureAmbientSoundEvent != null)
			{
				cultureAmbientSoundEvent.Stop();
			}
			this._cultureAmbientSoundEvent = null;
		}

		// Token: 0x0600054F RID: 1359 RVA: 0x00028001 File Offset: 0x00026201
		void ICharacterCreationStateHandler.OnCharacterCreationFinalized()
		{
			LoadingWindow.EnableGlobalLoadingWindow();
		}

		// Token: 0x06000550 RID: 1360 RVA: 0x00028008 File Offset: 0x00026208
		void ICharacterCreationStateHandler.OnRefresh()
		{
			if (this._shownLayers != null)
			{
				foreach (ScreenLayer layer in this._shownLayers.ToArray<ScreenLayer>())
				{
					base.RemoveLayer(layer);
				}
			}
			if (this._currentStageView != null)
			{
				this._shownLayers = this._currentStageView.GetLayers();
				if (this._shownLayers != null)
				{
					foreach (ScreenLayer layer2 in this._shownLayers.ToArray<ScreenLayer>())
					{
						base.AddLayer(layer2);
					}
				}
			}
		}

		// Token: 0x06000551 RID: 1361 RVA: 0x00028088 File Offset: 0x00026288
		void ICharacterCreationStateHandler.OnStageCreated(CharacterCreationStageBase stage)
		{
			Type type;
			if (this._stageViews.TryGetValue(stage.GetType(), out type))
			{
				this._currentStageView = (Activator.CreateInstance(type, new object[]
				{
					this._characterCreationStateState.CharacterCreationManager,
					new ControlCharacterCreationStage(this._characterCreationStateState.CharacterCreationManager.NextStage),
					new TextObject("{=Rvr1bcu8}Next", null),
					new ControlCharacterCreationStage(this._characterCreationStateState.CharacterCreationManager.PreviousStage),
					new TextObject("{=WXAaWZVf}Previous", null),
					new ControlCharacterCreationStage(this._characterCreationStateState.Refresh),
					new ControlCharacterCreationStageReturnInt(this._characterCreationStateState.CharacterCreationManager.GetIndexOfCurrentStage),
					new ControlCharacterCreationStageReturnInt(this._characterCreationStateState.CharacterCreationManager.GetTotalStagesCount),
					new ControlCharacterCreationStageReturnInt(this._characterCreationStateState.CharacterCreationManager.GetFurthestIndex),
					new ControlCharacterCreationStageWithInt(this._characterCreationStateState.CharacterCreationManager.GoToStage)
				}) as CharacterCreationStageViewBase);
				stage.Listener = this._currentStageView;
				this._currentStageView.SetGenericScene(this._genericScene);
				return;
			}
			this._currentStageView = null;
		}

		// Token: 0x06000552 RID: 1362 RVA: 0x000281BF File Offset: 0x000263BF
		protected override void OnFrameTick(float dt)
		{
			base.OnFrameTick(dt);
			if (LoadingWindow.IsLoadingWindowActive)
			{
				LoadingWindow.DisableGlobalLoadingWindow();
			}
			CharacterCreationStageViewBase currentStageView = this._currentStageView;
			if (currentStageView == null)
			{
				return;
			}
			currentStageView.Tick(dt);
		}

		// Token: 0x06000553 RID: 1363 RVA: 0x000281E5 File Offset: 0x000263E5
		void IGameStateListener.OnActivate()
		{
			base.OnActivate();
		}

		// Token: 0x06000554 RID: 1364 RVA: 0x000281ED File Offset: 0x000263ED
		void IGameStateListener.OnDeactivate()
		{
			base.OnDeactivate();
		}

		// Token: 0x06000555 RID: 1365 RVA: 0x000281F5 File Offset: 0x000263F5
		void IGameStateListener.OnInitialize()
		{
			base.OnInitialize();
		}

		// Token: 0x06000556 RID: 1366 RVA: 0x00028200 File Offset: 0x00026400
		void IGameStateListener.OnFinalize()
		{
			base.OnFinalize();
			this.StopSound();
			MBAgentRendererSceneController.DestructAgentRendererSceneController(this._genericScene, this._agentRendererSceneController, false);
			this._agentRendererSceneController = null;
			this._genericScene.ClearAll();
			this._genericScene.ManualInvalidate();
			this._genericScene = null;
		}

		// Token: 0x06000557 RID: 1367 RVA: 0x00028250 File Offset: 0x00026450
		private void CollectUnorderedStages()
		{
			Assembly assembly = typeof(CharacterCreationStageViewAttribute).Assembly;
			Assembly[] activeReferencingGameAssembliesSafe = assembly.GetActiveReferencingGameAssembliesSafe();
			this.CollectStagesFromAssembly(assembly);
			foreach (Assembly assembly2 in activeReferencingGameAssembliesSafe)
			{
				this.CollectStagesFromAssembly(assembly2);
			}
		}

		// Token: 0x06000558 RID: 1368 RVA: 0x00028294 File Offset: 0x00026494
		private void CollectStagesFromAssembly(Assembly assembly)
		{
			foreach (Type type in assembly.GetTypesSafe(null))
			{
				CharacterCreationStageViewAttribute characterCreationStageViewAttribute;
				if (typeof(CharacterCreationStageViewBase).IsAssignableFrom(type) && (characterCreationStageViewAttribute = (type.GetCustomAttributesSafe(typeof(CharacterCreationStageViewAttribute), true).FirstOrDefault<object>() as CharacterCreationStageViewAttribute)) != null)
				{
					if (this._stageViews.ContainsKey(characterCreationStageViewAttribute.StageType))
					{
						this._stageViews[characterCreationStageViewAttribute.StageType] = type;
					}
					else
					{
						this._stageViews.Add(characterCreationStageViewAttribute.StageType, type);
					}
				}
			}
		}

		// Token: 0x04000270 RID: 624
		private const string CultureParameterId = "MissionCulture";

		// Token: 0x04000271 RID: 625
		private readonly CharacterCreationState _characterCreationStateState;

		// Token: 0x04000272 RID: 626
		private IEnumerable<ScreenLayer> _shownLayers;

		// Token: 0x04000273 RID: 627
		private CharacterCreationStageViewBase _currentStageView;

		// Token: 0x04000274 RID: 628
		private readonly Dictionary<Type, Type> _stageViews;

		// Token: 0x04000275 RID: 629
		private SoundEvent _cultureAmbientSoundEvent;

		// Token: 0x04000276 RID: 630
		private Scene _genericScene;

		// Token: 0x04000277 RID: 631
		private MBAgentRendererSceneController _agentRendererSceneController;
	}
}
