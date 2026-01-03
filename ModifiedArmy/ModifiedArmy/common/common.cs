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
        public const int TownProsperityCostPerTier = 12; 

        // 村庄招募：每级（Tier）消耗的户数（Hearths）基数
        public const int VillageHearthCostPerTier = 3;

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
        /// 繁荣度的权重系数
        /// </summary>
        public const float ProsperityWeight = 0.4f;

        /// <summary>
        /// 村庄户数的权重系数
        /// </summary>
        public const float HearthsWeight = 0.6f;
    }
}
