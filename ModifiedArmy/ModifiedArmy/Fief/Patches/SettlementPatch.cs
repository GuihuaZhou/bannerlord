using HarmonyLib;
using ModifiedArmy.Models;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Patch
{
    /// <summary>
    /// 避免移除民兵部队中的封邑士兵
    /// </summary>
    [HarmonyPatch(typeof(Settlement), "RemoveMilitiasFromParty")]
    public static class SettlementPatch
    {
        // 判断是否为采邑部队
        private static bool IsFiefTroop(CharacterObject troop)
        {
            if (troop == null) return false;
            return SoldierTypeClassifier.IsFiefTroop(troop);
        }

        // Prefix：完全接管函数逻辑
        public static bool Prefix(MobileParty militiaParty, int numberToRemove)
        {
            if (militiaParty == null || militiaParty.MemberRoster == null)
                return false; // skip original

            var roster = militiaParty.MemberRoster;

            // 统计 **非采邑** 民兵总数
            int nonFiefCount = 0;
            var nonFiefIndices = new List<int>();
            for (int i = 0; i < roster.Count; i++)
            {
                var troop = roster.GetCharacterAtIndex(i);
                int count = roster.GetElementNumber(i);
                if (count <= 0) continue;

                if (!IsFiefTroop(troop))
                {
                    nonFiefCount += count;
                    nonFiefIndices.Add(i);
                }
            }

            // 如果没有非采邑部队，或要移除数量 ≤ 0，则什么都不做
            if (nonFiefCount <= 0 || numberToRemove <= 0)
                return false; // skip original

            // 如果要移除的数量 ≥ 非采邑总数，则清空所有非采邑
            if (numberToRemove >= nonFiefCount)
            {
                foreach (int i in nonFiefIndices)
                {
                    roster.AddToCountsAtIndex(i, -roster.GetElementNumber(i), 0, 0, false);
                }
                roster.RemoveZeroCounts();
                return false; // done
            }

            // 按比例移除非采邑部队（模仿原版逻辑）
            float ratio = (float)numberToRemove / nonFiefCount;
            int remainingToRemove = numberToRemove;

            foreach (int i in nonFiefIndices)
            {
                if (remainingToRemove <= 0) break;

                int currentCount = roster.GetElementNumber(i);
                if (currentCount <= 0) continue;

                int toRemove = MBRandom.RoundRandomized(currentCount * ratio);
                if (toRemove > remainingToRemove)
                    toRemove = remainingToRemove;

                roster.AddToCountsAtIndex(i, -toRemove, 0, 0, false);
                remainingToRemove -= toRemove;
            }

            roster.RemoveZeroCounts();
            return false; // skip original implementation
        }
    }
}
