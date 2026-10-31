using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Load;

namespace SandBox
{
	// Token: 0x02000023 RID: 35
	public class SandBoxGameManager : MBGameManager
	{
		// Token: 0x1700000D RID: 13
		// (get) Token: 0x0600010F RID: 271 RVA: 0x00007252 File Offset: 0x00005452
		// (set) Token: 0x06000110 RID: 272 RVA: 0x0000725A File Offset: 0x0000545A
		public bool LoadingSavedGame { get; private set; }

		// Token: 0x1700000E RID: 14
		// (get) Token: 0x06000111 RID: 273 RVA: 0x00007263 File Offset: 0x00005463
		public MetaData MetaData
		{
			get
			{
				LoadResult loadedGameResult = this._loadedGameResult;
				if (loadedGameResult == null)
				{
					return null;
				}
				return loadedGameResult.MetaData;
			}
		}

		// Token: 0x06000112 RID: 274 RVA: 0x00007276 File Offset: 0x00005476
		public SandBoxGameManager(SandBoxGameManager.CampaignCreatorDelegate campaignCreator)
		{
			this.LoadingSavedGame = false;
			this._campaignCreator = campaignCreator;
		}

		// Token: 0x06000113 RID: 275 RVA: 0x0000728C File Offset: 0x0000548C
		public SandBoxGameManager(LoadResult loadedGameResult)
		{
			this.LoadingSavedGame = true;
			this._loadedGameResult = loadedGameResult;
		}

		// Token: 0x06000114 RID: 276 RVA: 0x000072A2 File Offset: 0x000054A2
		public override void OnGameEnd(Game game)
		{
			MBDebug.SetErrorReportScene(null);
			base.OnGameEnd(game);
		}

		// Token: 0x06000115 RID: 277 RVA: 0x000072B4 File Offset: 0x000054B4
		protected override void DoLoadingForGameManager(GameManagerLoadingSteps gameManagerLoadingStep, out GameManagerLoadingSteps nextStep)
		{
			nextStep = GameManagerLoadingSteps.None;
			switch (gameManagerLoadingStep)
			{
			case GameManagerLoadingSteps.PreInitializeZerothStep:
				nextStep = GameManagerLoadingSteps.FirstInitializeFirstStep;
				return;
			case GameManagerLoadingSteps.FirstInitializeFirstStep:
				MBGameManager.LoadModuleData(this.LoadingSavedGame);
				nextStep = GameManagerLoadingSteps.WaitSecondStep;
				return;
			case GameManagerLoadingSteps.WaitSecondStep:
				if (!this.LoadingSavedGame)
				{
					MBGameManager.StartNewGame();
				}
				nextStep = GameManagerLoadingSteps.SecondInitializeThirdState;
				return;
			case GameManagerLoadingSteps.SecondInitializeThirdState:
				MBGlobals.InitializeReferences();
				if (!this.LoadingSavedGame)
				{
					MBDebug.Print("Initializing new game begin...", 0, Debug.DebugColor.White, 17592186044416UL);
					Campaign campaign = this._campaignCreator();
					Game.CreateGame(campaign, this);
					campaign.SetLoadingParameters(Campaign.GameLoadingType.NewCampaign);
					MBDebug.Print("Initializing new game end...", 0, Debug.DebugColor.White, 17592186044416UL);
				}
				else
				{
					MBDebug.Print("Initializing saved game begin...", 0, Debug.DebugColor.White, 17592186044416UL);
					((Campaign)Game.LoadSaveGame(this._loadedGameResult, this).GameType).SetLoadingParameters(Campaign.GameLoadingType.SavedCampaign);
					this._loadedGameResult = null;
					Common.MemoryCleanupGC(false);
					MBDebug.Print("Initializing saved game end...", 0, Debug.DebugColor.White, 17592186044416UL);
				}
				Game.Current.DoLoading();
				nextStep = GameManagerLoadingSteps.PostInitializeFourthState;
				return;
			case GameManagerLoadingSteps.PostInitializeFourthState:
			{
				bool flag = true;
				foreach (MBSubModuleBase mbsubModuleBase in Module.CurrentModule.CollectSubModules())
				{
					flag = (flag && mbsubModuleBase.DoLoading(Game.Current));
				}
				nextStep = (flag ? GameManagerLoadingSteps.FinishLoadingFifthStep : GameManagerLoadingSteps.PostInitializeFourthState);
				return;
			}
			case GameManagerLoadingSteps.FinishLoadingFifthStep:
				nextStep = (Game.Current.DoLoading() ? GameManagerLoadingSteps.None : GameManagerLoadingSteps.FinishLoadingFifthStep);
				return;
			default:
				return;
			}
		}

		// Token: 0x06000116 RID: 278 RVA: 0x00007434 File Offset: 0x00005634
		public override void OnAfterCampaignStart(Game game)
		{
		}

		// Token: 0x06000117 RID: 279 RVA: 0x00007438 File Offset: 0x00005638
		public override void OnLoadFinished()
		{
			if (!this.LoadingSavedGame)
			{
				MBDebug.Print("Switching to menu window...", 0, Debug.DebugColor.White, 17592186044416UL);
				if (!Game.Current.IsDevelopmentMode)
				{
					MBDebug.Print("OnLoadFinished Not DevelopmentMode", 0, Debug.DebugColor.White, 17592186044416UL);
					VideoPlaybackState videoPlaybackState = Game.Current.GameStateManager.CreateState<VideoPlaybackState>();
					string str = ModuleHelper.GetModuleFullPath("SandBox") + "Videos/CampaignIntro/";
					string subtitleFileBasePath = str + "campaign_intro";
					string videoPath = str + "campaign_intro.ivf";
					string audioPath = str + "campaign_intro.ogg";
					videoPlaybackState.SetStartingParameters(videoPath, audioPath, subtitleFileBasePath, 30f, true);
					videoPlaybackState.SetOnVideoFinisedDelegate(new Action(this.LaunchSandboxCharacterCreation));
					Game.Current.GameStateManager.CleanAndPushState(videoPlaybackState, 0);
				}
				else
				{
					MBDebug.Print("OnLoadFinished DevelopmentMode", 0, Debug.DebugColor.White, 17592186044416UL);
					MBDebug.Print("Launching Sandbox Character Creation", 0, Debug.DebugColor.White, 17592186044416UL);
					this.LaunchSandboxCharacterCreation();
				}
			}
			else
			{
				MBDebug.Print("Loading Save Game", 0, Debug.DebugColor.White, 17592186044416UL);
				Game.Current.GameStateManager.OnSavedGameLoadFinished();
				Game.Current.GameStateManager.CleanAndPushState(Game.Current.GameStateManager.CreateState<MapState>(), 0);
				MapState mapState = Game.Current.GameStateManager.ActiveState as MapState;
				string text = (mapState != null) ? mapState.GameMenuId : null;
				if (!string.IsNullOrEmpty(text))
				{
					if (Campaign.Current.GameMenuManager.GetGameMenu(text) != null)
					{
						PlayerEncounter playerEncounter = PlayerEncounter.Current;
						if (playerEncounter != null)
						{
							playerEncounter.OnLoad();
						}
						Campaign.Current.GameMenuManager.SetNextMenu(text);
					}
					else
					{
						PlayerEncounter.Finish(true);
					}
				}
				PartyBase.MainParty.SetVisualAsDirty();
				Campaign.Current.CampaignInformationManager.OnGameLoaded();
				foreach (Settlement settlement in Settlement.All)
				{
					settlement.Party.SetLevelMaskIsDirty();
				}
				CampaignEventDispatcher.Instance.OnGameLoadFinished();
				if (mapState != null)
				{
					mapState.OnLoadingFinished();
				}
			}
			base.IsLoaded = true;
		}

		// Token: 0x06000118 RID: 280 RVA: 0x0000766C File Offset: 0x0000586C
		private void LaunchSandboxCharacterCreation()
		{
			CharacterCreationState gameState = Game.Current.GameStateManager.CreateState<CharacterCreationState>();
			Game.Current.GameStateManager.CleanAndPushState(gameState, 0);
		}

		// Token: 0x04000053 RID: 83
		private LoadResult _loadedGameResult;

		// Token: 0x04000054 RID: 84
		private SandBoxGameManager.CampaignCreatorDelegate _campaignCreator;

		// Token: 0x0200012F RID: 303
		// (Invoke) Token: 0x06000DC8 RID: 3528
		public delegate Campaign CampaignCreatorDelegate();
	}
}
