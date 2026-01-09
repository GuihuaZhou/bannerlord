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
        public const int MinGoldToChargeCreationFee = 100000;

        /// <summary>
        /// 创建队伍时，从 Clan 金库中扣除的金币比例（例如 0.02f = 2%）。
        /// </summary>
        public const float CreationGoldFeeRate = 0.02f;
    }
}
