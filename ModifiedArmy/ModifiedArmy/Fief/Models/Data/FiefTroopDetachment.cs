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
    /// 表示一个封邑部队分遣队。
    /// 可用于：
    /// - 征召状态的分遣队（WaitCycle = -1，表示永久在外服役，不会自动回归）
    /// - 遣返状态的分遣队（WaitCycle > 0，表示正在等待回归封邑，倒计时结束后可重新征召）
    /// </summary>
    /// 
    [SaveableRootClass(1)]
    public class FiefTroopDetachment
    {
        /// <summary>
        /// 分遣队中的部队组成：CharacterObject -> 数量
        /// </summary>
        [SaveableProperty(1)]
        public Dictionary<CharacterObject, int> Troops { get; private set; }

        /// <summary>
        /// 等待周期（倒计时）：
        /// - >0：还需等待若干周才能回归封邑
        /// - <=0：已满足回归条件（仅对遣返分遣队有效）
        /// - -1：特殊值，表示该分遣队处于征召状态（如在玩家军队中）
        /// </summary>
        [SaveableProperty(2)]
        public int WaitCycle { get; set; }

        /// <summary>
        /// 构造函数：创建一个新的封邑部队分遣队，并设置初始等待周期
        /// </summary>
        /// <param name="initialWaitCycle">
        /// 初始等待周期。
        /// - 若用于遣返分遣队，传入正整数（如 2 表示 2 周后回归）
        /// - 若用于征召分遣队，应传入 -1
        /// </param>
        public FiefTroopDetachment()
        {
            Troops = new Dictionary<CharacterObject, int>();
        }
        public FiefTroopDetachment(int initialWaitCycle)
        {
            WaitCycle = initialWaitCycle;
            Troops = new Dictionary<CharacterObject, int>();
        }

        /// <summary>
        /// 向当前分遣队中添加部队
        /// </summary>
        /// <param name="newTroops">要添加的部队字典</param>
        public void AddTroops(Dictionary<CharacterObject, int> newTroops)
        {
            if (newTroops == null || newTroops.Count == 0)
            {
                ModLogger.Debug("[AddTroops] Input is null or empty. Skipping.");
                return;
            }

            foreach (var kvp in newTroops)
            {
                var troop = kvp.Key;
                var count = kvp.Value;

                if (count <= 0)
                {
                    continue;
                }

                if (Troops.ContainsKey(troop))
                {
                    Troops[troop] += count;
                }
                else
                {
                    Troops[troop] = count;
                }
            }
        }

        /// <summary>
        /// 获取该分遣队的总人数
        /// </summary>
        /// <returns>总人数</returns>
        public int GetTotalCount()
        {
            int total = 0;
            foreach (var count in Troops.Values)
            {
                total += count;
            }
            return total;
        }

        /// <summary>
        /// 判断该分遣队是否为空（无有效部队）
        /// </summary>
        public bool IsEmpty()
        {
            return Troops.Count == 0 || GetTotalCount() == 0;
        }

        /// <summary>
        /// 每周调用一次：若等待周期大于 0，则减 1
        /// </summary>
        public void Tick()
        {
            if (WaitCycle > 0)
            {
                WaitCycle--;
            }
        }

        /// <summary>
        /// 判断该分遣队是否已准备好回归封邑
        /// 条件：等待周期 ≤ 0 且分遣队非空
        /// </summary>
        public bool IsReadyToReturn()
        {
            return WaitCycle <= 0 && !IsEmpty();
        }


        /// <summary>
        /// 判断是否为最后一个周期
        /// 
        /// </summary>
        /// 
        public bool IsLastCycle()
        {
            if (WaitCycle == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空分遣队中的所有部队。
        /// </summary>
        public void Clear()
        {
            Troops.Clear();
        }

        /// <summary>
        /// 返回该分遣队的简要描述（用于调试）
        /// </summary>
        public override string ToString()
        {
            if (IsEmpty())
                return "[Empty]";

            return $"[WaitCycle={WaitCycle}, Total={GetTotalCount()}, Types={Troops.Count}]";
        }
    }
}
