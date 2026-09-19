using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using static System.Collections.Specialized.BitVector32;
using static ModifiedArmy.common.CommonConstants;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// 表示封邑部队中三类兵种的数量统计结果。
    /// </summary>
    public readonly struct FiefTroopCounts
    {
        /// <summary>
        /// 采邑部队总人数。
        /// </summary>
        public readonly int Total;

        /// <summary>
        /// 扈从（Retinue）数量。
        /// </summary>
        public readonly int Retinue;

        /// <summary>
        /// 军士（Sergeant）数量。
        /// </summary>
        public readonly int Sergeant;

        /// <summary>
        /// 民兵（Militia）数量。
        /// </summary>
        public readonly int Militia;

        /// <summary>
        /// 初始化一个新的 <see cref="FiefTroopCounts"/> 实例。
        /// </summary>
        /// <param name="retinue">扈从数量。</param>
        /// <param name="sergeant">军士数量。</param>
        /// <param name="militia">民兵数量。</param>
        public FiefTroopCounts(int retinue, int sergeant, int militia)
        {
            Retinue = retinue;
            Sergeant = sergeant;
            Militia = militia;
            Total = retinue + sergeant + militia;
        }
    }
}
