using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace LipottyTroops
{
	// Token: 0x02000008 RID: 8
	public class SubModule : MBSubModuleBase
	{
		// Token: 0x17000024 RID: 36
		// (get) Token: 0x0600005F RID: 95 RVA: 0x0000450E File Offset: 0x0000270E
		// (set) Token: 0x06000060 RID: 96 RVA: 0x00004515 File Offset: 0x00002715
		public static Settings ModSettings { get; private set; }

		// Token: 0x17000025 RID: 37
		// (get) Token: 0x06000061 RID: 97 RVA: 0x0000451D File Offset: 0x0000271D
		// (set) Token: 0x06000062 RID: 98 RVA: 0x00004524 File Offset: 0x00002724
		public static string ModulePath { get; private set; }

		// Token: 0x06000063 RID: 99 RVA: 0x0000452C File Offset: 0x0000272C
		protected override void OnSubModuleLoad()
		{
			base.OnSubModuleLoad();
			SubModule.ModulePath = Path.GetDirectoryName(typeof(SubModule).Assembly.Location);
			string path = Path.Combine(SubModule.ModulePath, "..");
			SubModule.ModulePath = Path.Combine(path, "..");
			SubModule.ModSettings = GlobalSettings<Settings>.Instance;
			bool shouldLoadPatches = this._shouldLoadPatches;
			if (shouldLoadPatches)
			{
				new Harmony("LipottyTroops").PatchAll(Assembly.GetExecutingAssembly());
				this._shouldLoadPatches = false;
			}
		}

		// Token: 0x06000064 RID: 100 RVA: 0x000045B4 File Offset: 0x000027B4
		protected override void OnGameStart(Game game, IGameStarter gameStarter)
		{
			base.OnGameStart(game, gameStarter);
			bool flag = game.GameType is Campaign;
			if (flag)
			{
				CampaignGameStarter campaignGameStarter = (CampaignGameStarter)gameStarter;
				campaignGameStarter.AddBehavior(new SoldierLimitBehavior());
			}
		}

		// Token: 0x06000065 RID: 101 RVA: 0x000045F4 File Offset: 0x000027F4
		protected override void OnBeforeInitialModuleScreenSetAsRoot()
		{
			base.OnBeforeInitialModuleScreenSetAsRoot();
			bool flag = SubModule.ModSettings == null;
			if (flag)
			{
				SubModule.ModSettings = GlobalSettings<Settings>.Instance;
			}
			InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=LRM_MOD_010}LRM has loaded.", null).ToString()));
		}

		// Token: 0x04000026 RID: 38
		private bool _shouldLoadPatches = true;
	}
}
