// Module entry point: installs compatibility patches and registers campaign services.
using System;
using System.Reflection;
using HarmonyLib;
using ModifiedFamily.Campaign;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Marriage.Execution;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Localization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Bannerlord.UIExtenderEx;

namespace ModifiedFamily
{
    public sealed class Main : MBSubModuleBase
    {
        private UIExtender _uiExtender;
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            InitializeUserInterface();
            try
            {
                new Harmony("com.mod.ModifiedFamily").PatchAll(Assembly.GetExecutingAssembly());
                FamilyDiagnostics.Info(FamilyLogText.Simple("MF_Log_PatchesInstalled", "Vanilla automatic marriage entry points are now managed by ModifiedFamily."));
            }
            catch (Exception ex) { FamilyDiagnostics.DisableAi(FamilyLogText.Simple("MF_Log_PatchFailed", "Vanilla marriage patching failed; family AI is disabled.") + " " + ex); }
        }

        // UI initialization is isolated so a version-specific Harmony patch cannot hide player controls.
        private void InitializeUserInterface()
        {
            try
            {
                _uiExtender = new UIExtender("ModifiedFamily");
                _uiExtender.Register(Assembly.GetExecutingAssembly());
                _uiExtender.Enable();
                FamilyDiagnostics.Info(FamilyLogText.Simple("MF_Log_UiReady", "The family interface extension has been enabled."));
            }
            catch (Exception ex)
            {
                FamilyDiagnostics.Error(FamilyLogText.Simple("MF_Log_UiFailed", "The family interface extension could not be enabled.") + " " + ex);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter starter)
        {
            base.OnGameStart(game, starter);
            MarriageExecutionScope.Clear();
            if (game.GameType is TaleWorlds.CampaignSystem.Campaign && starter is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddModel(new ModifiedMarriageModel());
                campaignStarter.AddBehavior(new FamilyCampaignBehavior());
            }
        }
    }
}
