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

    public static class RecruitmentCosts
    {
        // 城堡招募：每级（Tier）消耗的繁荣度基数
        public const int CastleProsperityCostPerTier = 4;

        // 城镇招募：每级（Tier）消耗的繁荣度基数
        public const int TownProsperityCostPerTier = 8; 

        // 消耗的户数（Hearths）基数
        public const int VillageHearthCostPer = 1;

        // <summary>
        /// 城堡达到最大补员人数所需的繁荣度
        /// </summary>
        public const float CastleMaxReinforcementProsperityThreshold = 2000f;

        /// <summary>
        /// 城镇达到最大补员人数所需的繁荣度
        /// </summary>
        public const float TownMaxReinforcementProsperityThreshold = 12000f;

        /// <summary>
        /// 村庄达到最大补员人数所需的户数
        /// </summary>
        public const float VillageMaxReinforcementHearthThreshold = 900f;

        // <summary>
        /// 城堡最低繁荣度
        /// </summary>
        public const float CastleMinProsperityThreshold = 300f;

        /// <summary>
        /// 城镇最低繁荣度
        /// </summary>
        public const float TownMinProsperityThreshold = 1000f;

        /// <summary>
        /// 村庄最低户数
        /// </summary>
        public const float VillageMinHearthThreshold = 100f;

        // <summary>
        /// 繁荣度的权重系数
        /// </summary>
        public const float ProsperityWeight = 0.4f;

        /// <summary>
        /// 村庄户数的权重系数
        /// </summary>
        public const float HearthsWeight = 0.6f;
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
        public const int MinGoldToChargeCreationFee = 100000;

        /// <summary>
        /// 创建队伍时，从 Clan 金库中扣除的金币比例（例如 0.02f = 2%）。
        /// </summary>
        public const float CreationGoldFeeRate = 0.02f;
    }
}
