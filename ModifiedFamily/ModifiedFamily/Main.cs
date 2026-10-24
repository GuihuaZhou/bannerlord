using System;
using System.Reflection;
using HarmonyLib;
using ModifiedFamily.Campaign;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Execution;
using ModifiedFamily.Models;
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
                FamilyDiagnostics.Info("Harmony patches installed.");
            }
            catch (Exception ex) { FamilyDiagnostics.DisableAi("Patch installation failed; family AI is disabled. " + ex); }
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
