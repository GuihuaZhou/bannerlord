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

    public static class CommonConstants
    {
        // ========== 外交系统 - 封地繁荣度评估配置 ==========

        // ---- 城镇（Town）繁荣度范围：通常 0 ～ 10000+ ----
        public static readonly float TOWN_POOR_THRESHOLD = 1000f;   // 贫困
        public static readonly float TOWN_AVERAGE_THRESHOLD = 4000f;   // 一般
        public static readonly float TOWN_RICH_THRESHOLD = 8000f;   // 富有
        public static readonly float TOWN_VERY_RICH_THRESHOLD = 12000f;  // 非常富有

        // ---- 城堡（Castle）繁荣度范围：通常 0 ～ 2000 左右 ----
        public static readonly float CASTLE_POOR_THRESHOLD = 300f;    // 贫困
        public static readonly float CASTLE_AVERAGE_THRESHOLD = 800f;  // 一般
        public static readonly float CASTLE_RICH_THRESHOLD = 1500f;   // 富有
        public static readonly float CASTLE_VERY_RICH_THRESHOLD = 2000f;  // 非常富有

        // ---- 城镇与城堡的权重比例（用于加权平均）----
        // 说明：TOWN_WEIGHT座城镇 ≈ CASTLE_WEIGHT座城堡
        public static readonly float TOWN_WEIGHT = 2f;
        public static readonly float CASTLE_WEIGHT = 1f;

        // ---- 各繁荣等级对应的“战争倾向系数”----
        // 负值：倾向和平；正值：倾向战争
        public static readonly float PROSPERITY_SCORE_POOR = -0.8f;  // 贫困 → 强烈反战
        public static readonly float PROSPERITY_SCORE_AVERAGE = -0.4f;  // 一般 → 略微反战
        public static readonly float PROSPERITY_SCORE_RICH = +0.3f;  // 富有 → 倾向战争
        public static readonly float PROSPERITY_SCORE_VERY_RICH = +0.5f;  // 非常富有 → 强烈主战

        // ---- 无封地氏族（Landless Clan）的战争倾向 ----
        // 说明：没有家业可损失，更愿意通过战争获取利益
        public static readonly float LANDLESS_CLAN_WAR_PROPENSITY = +0.6f;

        // ---- 繁荣度倾向对最终外交评分的影响强度 ----
        // 说明：该值越大，封地繁荣度对宣战/议和决策的影响越强
        public static readonly float WAR_PROPENSITY_SCORE_MULTIPLIER = 5000000f;

        public static readonly float BASE_EXPOSURE_SCORE = 0.2f;

        // 封建部队工资豁免天数
        public static readonly int FIEF_WAGE_EXEMPTION_DAYS = 28;
        // 解散后的封建部队的冷却/等待周数
        public static readonly int RETURN_TROOP_WAIT_CYCLE = 3;
        // 封建部队最大使用周期，即服役的周数
        public static readonly int FIEF_TROOP_MAX_SERVICE_CYCLE = 5;

        // 征召时每名士兵消耗的基础繁荣度成本（与 Tier 无关）
        // Town 的默认值
        public static readonly int TownProsperityCostPerTroop = 8;

        // 村庄最低户数阈值
        public static readonly float VillageMinHearthThreshold = 100f;

        // 村庄满补员户数阈值
        public static readonly float VillageMaxReinforcementHearthThreshold = 900f;

        // 繁荣度在补员公式中的权重
        public static readonly float ProsperityWeight = 0.4f;

        // 户数在补员公式中的权重
        public static readonly float HearthsWeight = 0.6f;

        // Castle 的征召基础繁荣度成本
        public static readonly int CastleProsperityCostPerTroop = 4;

        public static readonly int VillageHearthCostPer = 1;

        public static readonly float PlayerSettlementPrisonerEscapeChance = 0.01f;

        // ---- AI 对封地繁荣度的影响系数 ----
        public static readonly float AI_FIEF_PROSPERITY_IMPACT_MULTIPLIER = 0.1f;

        public static readonly float AI_FIEF_HEARTH_IMPACT_MULTIPLIER = 0.1f;

        // ---- 不同文化的志愿兵生成概率倍率 ----
        // Vlandia：封建部队强，城镇志愿兵生成极低
        public const float VLANDIA_VOLUNTEER_GENERATION_MULTIPLIER = 0.2f;

        // Empire：封建部队较少，更依赖城镇志愿兵
        public const float EMPIRE_VOLUNTEER_GENERATION_MULTIPLIER = 0.6f;

        // Aserai：封建部队较少，同时依赖地方志愿兵
        public const float ASERAI_VOLUNTEER_GENERATION_MULTIPLIER = 0.7f;
    }
}
