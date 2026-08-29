using System;
using System.Collections.Generic;
using System.Text;

namespace ModifiedArmy.common
{
    /// <summary>
    /// 士兵类型：扈从、军士、民兵、水兵、军事奴隶
    /// </summary>
    public enum SoldierType
    {
        Retinue,
        Sergeant,
        Militia,
        Marine,
        Slave,
        Other
    }

    public static class GarrisonConstants
    {
        /// <summary>
        /// 驻军基础人数
        /// </summary>
        public const float BaseGarrisonSize = 100f;

        /// <summary>
        /// 城镇为驻军提供的人数加成
        /// </summary>
        public const float TownGarrisonBonus = 50f;

        /// <summary>
        /// 军营建筑为驻军提供的人数加成
        /// </summary>
        public const float BarracksGarrisonBonus = 20f;

        /// <summary>
        /// 训练场建筑为驻军提供的人数加成
        /// </summary>
        public const float TrainingFieldGarrisonBonus = 20f;
    }

    /// <summary>
    /// 定义 Clan 在自动生成领主队伍时的经济与行为规则常量。
    /// </summary>
    public static class ClanPartySpawnConstants
    {
        /// <summary>
        /// Clan 必须至少拥有此金额的金币，才会在创建新队伍时扣除创建费用。
        /// </summary>
        public const int MinGoldToChargeCreationFee = 50000;

        /// <summary>
        /// 创建队伍时，从 Clan 金库中扣除的金币比例（例如 0.02f = 2%）。
        /// </summary>
        public const int CreationGoldCost = 5000;
    }

    /// <summary>
    /// 全局常量入口，全部委托给 ModConfig 实例。
    /// 保持向后兼容，所有代码无需修改。
    /// </summary>
    public static class CommonConstants
    {
        private static ModConfig Cfg => ModConfigManager.Instance.GetActiveConfig();

        public static float TOWN_POOR_THRESHOLD => Cfg?.TownPoorThreshold ?? 1000f;
        public static float TOWN_AVERAGE_THRESHOLD => Cfg?.TownAverageThreshold ?? 4000f;
        public static float TOWN_RICH_THRESHOLD => Cfg?.TownRichThreshold ?? 8000f;
        public static float TOWN_VERY_RICH_THRESHOLD => Cfg?.TownVeryRichThreshold ?? 12000f;

        public static float CASTLE_POOR_THRESHOLD => Cfg?.CastlePoorThreshold ?? 300f;
        public static float CASTLE_AVERAGE_THRESHOLD => Cfg?.CastleAverageThreshold ?? 800f;
        public static float CASTLE_RICH_THRESHOLD => Cfg?.CastleRichThreshold ?? 1500f;
        public static float CASTLE_VERY_RICH_THRESHOLD => Cfg?.CastleVeryRichThreshold ?? 2000f;

        public static float ProsperityWeight => Cfg?.ProsperityWeight ?? 0.4f;
        public static float HearthsWeight => Cfg?.HearthsWeight ?? 0.6f;

        public static float VillageMinHearthThreshold => Cfg?.VillageMinHearthThreshold ?? 100f;
        public static float VillageMaxReinforcementHearthThreshold => Cfg?.VillageMaxReinforcementHearthThreshold ?? 900f;

        public static int FIEF_WAGE_EXEMPTION_DAYS => Cfg?.FiefWageExemptionDays ?? 28;
        public static int RETURN_TROOP_WAIT_CYCLE => 3;
        public static int FIEF_TROOP_MAX_SERVICE_CYCLE => 5;

        public static float AI_FIEF_PROSPERITY_IMPACT_MULTIPLIER => Cfg?.AiFiefProsperityImpactMultiplier ?? 0.1f;
        public static float AI_FIEF_HEARTH_IMPACT_MULTIPLIER => Cfg?.AiFiefHearthImpactMultiplier ?? 0.1f;

        public static float VLANDIA_VOLUNTEER_GENERATION_MULTIPLIER => Cfg?.VlandiaVolunteerMultiplier ?? 0.2f;
        public static float EMPIRE_VOLUNTEER_GENERATION_MULTIPLIER => Cfg?.EmpireVolunteerMultiplier ?? 0.6f;
        public static float ASERAI_VOLUNTEER_GENERATION_MULTIPLIER => Cfg?.AseraiVolunteerMultiplier ?? 0.7f;

        public static float PlayerSettlementPrisonerEscapeChance => Cfg?.PlayerSettlementPrisonerEscapeChance ?? 0.01f;
    }
}
