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

namespace ModifiedFamily
{
    public sealed class Main : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                new Harmony("com.mod.ModifiedFamily").PatchAll(Assembly.GetExecutingAssembly());
                FamilyDiagnostics.Info(FamilyLogText.Simple("MF_Log_PatchesInstalled", "Vanilla automatic marriage entry points are now managed by ModifiedFamily."));
            }
            catch (Exception ex) { FamilyDiagnostics.DisableAi(FamilyLogText.Simple("MF_Log_PatchFailed", "Vanilla marriage patching failed; family AI is disabled.") + " " + ex); }
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
