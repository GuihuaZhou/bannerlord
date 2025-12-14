using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace LipottyTroops
{
	// Token: 0x02000007 RID: 7
	public class Settings : AttributeGlobalSettings<Settings>
	{
		// Token: 0x1700000F RID: 15
		// (get) Token: 0x06000038 RID: 56 RVA: 0x0000431D File Offset: 0x0000251D
		public override string Id
		{
			get
			{
				return "LipottyTroops";
			}
		}

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x06000039 RID: 57 RVA: 0x00004324 File Offset: 0x00002524
		public override string DisplayName
		{
			get
			{
				return "LipottyTroops";
			}
		}

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x0600003A RID: 58 RVA: 0x0000432B File Offset: 0x0000252B
		public override string FolderName
		{
			get
			{
				return "LipottyTroops";
			}
		}

		// Token: 0x17000012 RID: 18
		// (get) Token: 0x0600003B RID: 59 RVA: 0x00004332 File Offset: 0x00002532
		public override string FormatType
		{
			get
			{
				return "json2";
			}
		}

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x0600003C RID: 60 RVA: 0x00004339 File Offset: 0x00002539
		// (set) Token: 0x0600003D RID: 61 RVA: 0x00004341 File Offset: 0x00002541
		[SettingPropertyGroup("{=LRM_SET_001}Recruitment Difficulty Settings", GroupOrder = 0)]
		[SettingPropertyInteger("{=LRM_SET_024}Recruitment Difficulty", 0, 6, "0", Order = 0, RequireRestart = false, HintText = "{=LRM_SET_002}The amount of extra troops that you can recruit from notables. Default is 0.")]
		public int RecruitDifficulty { get; set; } = 0;

		// Token: 0x17000014 RID: 20
		// (get) Token: 0x0600003E RID: 62 RVA: 0x0000434A File Offset: 0x0000254A
		// (set) Token: 0x0600003F RID: 63 RVA: 0x00004352 File Offset: 0x00002552
		[SettingPropertyGroup("{=LRM_SET_003}Regulars Limit Settings", GroupOrder = 1)]
		[SettingPropertyInteger("{=LRM_SET_004}Town Prosperity's Impact on Limit (Regulars)", 10, 2000, "0", Order = 1, RequireRestart = false, HintText = "{=LRM_SET_005}Town prosperity required per Regular: 100 (default).")]
		public int RegularsTownProsperity { get; set; } = 100;

		// Token: 0x17000015 RID: 21
		// (get) Token: 0x06000040 RID: 64 RVA: 0x0000435B File Offset: 0x0000255B
		// (set) Token: 0x06000041 RID: 65 RVA: 0x00004363 File Offset: 0x00002563
		[SettingPropertyGroup("{=LRM_SET_003}Regulars Limit Settings", GroupOrder = 1)]
		[SettingPropertyInteger("{=LRM_SET_006}Castle Prosperity's Impact on Limit (Regulars)", 10, 2000, "0", Order = 2, RequireRestart = false, HintText = "{=LRM_SET_007}Castle prosperity required per Regular: 20 (default).")]
		public int RegularsCastleProsperity { get; set; } = 20;

		// Token: 0x17000016 RID: 22
		// (get) Token: 0x06000042 RID: 66 RVA: 0x0000436C File Offset: 0x0000256C
		// (set) Token: 0x06000043 RID: 67 RVA: 0x00004374 File Offset: 0x00002574
		[SettingPropertyGroup("{=LRM_SET_003}Regulars Limit Settings", GroupOrder = 1)]
		[SettingPropertyInteger("{=LRM_SET_008}Village Hearth's Impact on Limit (Regulars)", 10, 2000, "0", Order = 3, RequireRestart = false, HintText = "{=LRM_SET_009}Village hearth required per Regular: 20 (default).")]
		public int RegularsVillageHouseholds { get; set; } = 20;

		// Token: 0x17000017 RID: 23
		// (get) Token: 0x06000044 RID: 68 RVA: 0x0000437D File Offset: 0x0000257D
		// (set) Token: 0x06000045 RID: 69 RVA: 0x00004385 File Offset: 0x00002585
		[SettingPropertyGroup("{=LRM_SET_010}Nobles Limit Settings", GroupOrder = 2)]
		[SettingPropertyInteger("{=LRM_SET_018}Town Prosperity's Impact on Limit (Nobles)", 10, 2000, "0", Order = 1, RequireRestart = false, HintText = "{=LRM_SET_011}Town prosperity required per Noble: 500 (default).")]
		public int NoblesTownProsperity { get; set; } = 500;

		// Token: 0x17000018 RID: 24
		// (get) Token: 0x06000046 RID: 70 RVA: 0x0000438E File Offset: 0x0000258E
		// (set) Token: 0x06000047 RID: 71 RVA: 0x00004396 File Offset: 0x00002596
		[SettingPropertyGroup("{=LRM_SET_010}Nobles Limit Settings", GroupOrder = 2)]
		[SettingPropertyInteger("{=LRM_SET_019}Castle Prosperity's Impact on Limit (Nobles)", 10, 2000, "0", Order = 2, RequireRestart = false, HintText = "{=LRM_SET_012}Castle prosperity required per Noble: 100 (default).")]
		public int NoblesCastleProsperity { get; set; } = 100;

		// Token: 0x17000019 RID: 25
		// (get) Token: 0x06000048 RID: 72 RVA: 0x0000439F File Offset: 0x0000259F
		// (set) Token: 0x06000049 RID: 73 RVA: 0x000043A7 File Offset: 0x000025A7
		[SettingPropertyGroup("{=LRM_SET_010}Nobles Limit Settings", GroupOrder = 2)]
		[SettingPropertyInteger("{=LRM_SET_020}Village Hearth's Impact on Limit (Nobles)", 10, 2000, "0", Order = 3, RequireRestart = false, HintText = "{=LRM_SET_013}Village hearth required per Noble: 100 (default).")]
		public int NoblesVillageHouseholds { get; set; } = 100;

		// Token: 0x1700001A RID: 26
		// (get) Token: 0x0600004A RID: 74 RVA: 0x000043B0 File Offset: 0x000025B0
		// (set) Token: 0x0600004B RID: 75 RVA: 0x000043B8 File Offset: 0x000025B8
		[SettingPropertyGroup("{=LRM_SET_014}Elites Limit Settings", GroupOrder = 3)]
		[SettingPropertyInteger("{=LRM_SET_021}Town Prosperity's Impact on Limit (Elites)", 10, 2000, "0", Order = 1, RequireRestart = false, HintText = "{=LRM_SET_015}Town prosperity required per Elite: 1000 (default).")]
		public int ElitesTownProsperity { get; set; } = 1000;

		// Token: 0x1700001B RID: 27
		// (get) Token: 0x0600004C RID: 76 RVA: 0x000043C1 File Offset: 0x000025C1
		// (set) Token: 0x0600004D RID: 77 RVA: 0x000043C9 File Offset: 0x000025C9
		[SettingPropertyGroup("{=LRM_SET_014}Elites Limit Settings", GroupOrder = 3)]
		[SettingPropertyInteger("{=LRM_SET_022}Castle Prosperity's Impact on Limit (Elites)", 10, 2000, "0", Order = 2, RequireRestart = false, HintText = "{=LRM_SET_016}Castle prosperity required per Elite: 200 (default).")]
		public int ElitesCastleProsperity { get; set; } = 200;

		// Token: 0x1700001C RID: 28
		// (get) Token: 0x0600004E RID: 78 RVA: 0x000043D2 File Offset: 0x000025D2
		// (set) Token: 0x0600004F RID: 79 RVA: 0x000043DA File Offset: 0x000025DA
		[SettingPropertyGroup("{=LRM_SET_014}Elites Limit Settings", GroupOrder = 3)]
		[SettingPropertyInteger("{=LRM_SET_023}Village Hearth's Impact on Limit (Elites)", 10, 2000, "0", Order = 3, RequireRestart = false, HintText = "{=LRM_SET_017}Village hearth required per Elite: 200 (default).")]
		public int ElitesVillageHouseholds { get; set; } = 200;

		// Token: 0x1700001D RID: 29
		// (get) Token: 0x06000050 RID: 80 RVA: 0x000043E3 File Offset: 0x000025E3
		// (set) Token: 0x06000051 RID: 81 RVA: 0x000043EB File Offset: 0x000025EB
		[SettingPropertyGroup("{=LRM_SET_025}Mercenary Settings", GroupOrder = 4)]
		[SettingPropertyInteger("{=LRM_SET_026}Base Mercenary Limit", 0, 100, "0", Order = 1, RequireRestart = false, HintText = "{=LRM_SET_027}Initial mercenary limit at clan tier 0. Default: 20")]
		public int MercenaryBaseLimit { get; set; } = 20;

		// Token: 0x1700001E RID: 30
		// (get) Token: 0x06000052 RID: 82 RVA: 0x000043F4 File Offset: 0x000025F4
		// (set) Token: 0x06000053 RID: 83 RVA: 0x000043FC File Offset: 0x000025FC
		[SettingPropertyGroup("{=LRM_SET_025}Mercenary Settings", GroupOrder = 4)]
		[SettingPropertyInteger("{=LRM_SET_028}Mercenary Per Clan Tier", 1, 100, "0", Order = 2, RequireRestart = false, HintText = "{=LRM_SET_029}Additional mercenaries per clan tier. Default: +10 per tier")]
		public int MercenaryPerTier { get; set; } = 10;

		// Token: 0x1700001F RID: 31
		// (get) Token: 0x06000054 RID: 84 RVA: 0x00004405 File Offset: 0x00002605
		// (set) Token: 0x06000055 RID: 85 RVA: 0x0000440D File Offset: 0x0000260D
		[SettingPropertyGroup("{=LRM_SET_025}Mercenary Settings", GroupOrder = 4)]
		[SettingPropertyFloatingInteger("{=LRM_SET_030}Leadership Bonus Factor (Mercenary)", 0f, 1f, "0.00", Order = 3, RequireRestart = false, HintText = "{=LRM_SET_031}Mercenary bonus per leadership point (actual = points * factor). Default: 0.5 (1 per 2 points)")]
		public float LeadershipBonusFactor { get; set; } = 0.5f;

		// Token: 0x17000020 RID: 32
		// (get) Token: 0x06000056 RID: 86 RVA: 0x00004416 File Offset: 0x00002616
		// (set) Token: 0x06000057 RID: 87 RVA: 0x0000441E File Offset: 0x0000261E
		[SettingPropertyGroup("{=LRM_SET_032}Bandit Settings", GroupOrder = 5)]
		[SettingPropertyInteger("{=LRM_SET_033}Bandit Base Limit", 0, 100, "0", Order = 1, RequireRestart = false, HintText = "{=LRM_SET_034}Initial bandit limit at leadership 0. Default: 0")]
		public int BanditBaseLimit { get; set; } = 0;

		// Token: 0x17000021 RID: 33
		// (get) Token: 0x06000058 RID: 88 RVA: 0x00004427 File Offset: 0x00002627
		// (set) Token: 0x06000059 RID: 89 RVA: 0x0000442F File Offset: 0x0000262F
		[SettingPropertyGroup("{=LRM_SET_032}Bandit Settings", GroupOrder = 5)]
		[SettingPropertyFloatingInteger("{=LRM_SET_035}Leadership Bonus Factor (Bandits)", 0f, 1f, "0.00", Order = 2, RequireRestart = false, HintText = "{=LRM_SET_036}Bandit bonus per leadership point (actual = points * factor). Default: 0.5 (1 per 2 points)")]
		public float BanditLeadershipFactor { get; set; } = 0.5f;

		// Token: 0x17000022 RID: 34
		// (get) Token: 0x0600005A RID: 90 RVA: 0x00004438 File Offset: 0x00002638
		// (set) Token: 0x0600005B RID: 91 RVA: 0x00004440 File Offset: 0x00002640
		[SettingPropertyGroup("{=LRM_SET_032}Bandit Settings", GroupOrder = 5)]
		[SettingPropertyFloatingInteger("{=LRM_SET_037}Base Event Chance", 0.01f, 1f, "0.00", Order = 3, RequireRestart = false, HintText = "{=LRM_SET_038}Base probability for bandit events (default: 1.0)")]
		public float BanditBaseEventChance { get; set; } = 1f;

		// Token: 0x17000023 RID: 35
		// (get) Token: 0x0600005C RID: 92 RVA: 0x00004449 File Offset: 0x00002649
		// (set) Token: 0x0600005D RID: 93 RVA: 0x00004451 File Offset: 0x00002651
		[SettingPropertyGroup("{=LRM_SET_032}Bandit Settings", GroupOrder = 5)]
		[SettingPropertyFloatingInteger("{=LRM_SET_039}Roguery Reduction Factor", 0.001f, 0.1f, "0.000", Order = 4, RequireRestart = false, HintText = "{=LRM_SET_040}Roguery skill impact per point (default: 0.004)")]
		public float BanditRogueryReduction { get; set; } = 0.004f;
	}
}
