
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// 表示采邑招募带来的wage豁免：在指定天数内免除指定人数的wage。
    /// </summary>
    [SaveableRootClass(3)]
    public class FiefWageExemption
    {
        [SaveableProperty(1)]
        public int ExemptedTroopCount { get; private set; }
        [SaveableProperty(2)] 
        public int DaysRemaining { get; private set; }

        // Parameterless constructor for TaleWorlds SaveSystem
        public FiefWageExemption() { }

        public FiefWageExemption(int count, int durationDays)
        {
            ExemptedTroopCount = Math.Max(0, count);
            DaysRemaining = Math.Max(0, durationDays);
        }

        /// <summary>
        /// 有效天数减 1。
        /// </summary>
        public void Tick()
        {
            if (DaysRemaining > 0)
                DaysRemaining--;
        }

        /// <summary>
        /// 消耗豁免额度。
        /// </summary>
        /// <param name="amount">请求消耗的人数</param>
        /// <returns>实际消耗的人数（不超过当前剩余豁免人数）</returns>
        public int Consume(int amount)
        {
            if (amount <= 0 || !IsValid)
                return 0;

            int actual = Math.Min(ExemptedTroopCount, amount);
            ExemptedTroopCount -= actual;
            return actual;
        }

        /// <summary>
        /// 豁免是否仍然有效（天数 > 0 且人数 > 0）。
        /// </summary>
        public bool IsValid => DaysRemaining > 0 && ExemptedTroopCount > 0;
    }

    /// <summary>
    /// 管理各 MobileParty 的采邑工资豁免。
    /// 仅提供两个公共接口，确保封装性和使用安全。
    /// </summary>
    [SaveableRootClass(4)]
    public class FiefWageExemptionManager : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private Dictionary<string, List<FiefWageExemption>> _exemptionMap = new();

        public override void RegisterEvents()
        {
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_exemptionMap", ref _exemptionMap);
        }

        private string GetPartyKey(MobileParty party)
        {
            return party?.StringId ?? string.Empty;
        }

        private void OnPartyRemoved(PartyBase partyBase)
        {
            if (partyBase != null && partyBase.IsMobile)
            {
                MobileParty mobileParty = partyBase.MobileParty;
                if (mobileParty.IsLordParty)
                {
                    string key = GetPartyKey(mobileParty);
                    if (!string.IsNullOrEmpty(key) && _exemptionMap.Remove(key))
                    {
                        ModLogger.Debug($"[FiefWage] Removed exemptions due to party removal: {mobileParty.Name} (ID: {key})");
                    }
                }
            }
        }

        /// <summary>
        /// 每日衰减：仅在 Campaign Daily Tick 时调用一次
        /// </summary>
        private void OnDailyTick()
        {
            if (_exemptionMap.Count == 0)
                return;

            var keysToRemove = new List<string>();

            foreach (var kvp in _exemptionMap)
            {
                string partyId = kvp.Key;
                var list = kvp.Value;

                if (list == null || list.Count == 0)
                {
                    keysToRemove.Add(partyId);
                    continue;
                }

                // 对该 party 的所有豁免条目执行 Tick
                int beforeTroops = 0;
                foreach (var exemption in list)
                {
                    if (exemption.IsValid)
                        beforeTroops += exemption.ExemptedTroopCount;
                    exemption.Tick(); // DaysRemaining--
                }

                // 清理失效条目
                list.RemoveAll(e => !e.IsValid);

                // 如果列表变空，标记删除
                if (list.Count == 0)
                {
                    keysToRemove.Add(partyId);
                }
                else
                {
                    int afterTroops = list.Sum(e => e.ExemptedTroopCount);
                }
            }

            // 批量移除空或无效条目
            foreach (string key in keysToRemove)
            {
                _exemptionMap.Remove(key);
            }
        }

        /// <summary>
        /// 立即清除指定 MobileParty 的所有工资豁免。
        /// </summary>
        /// <param name="party">目标队伍</param>
        public void ClearAllExemptions(MobileParty party)
        {
            if (party == null)
                return;

            string key = GetPartyKey(party);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (_exemptionMap.TryGetValue(key, out var list))
            {
                _exemptionMap.Remove(key);
                if (party.IsMainParty)
                    //ModLogger.Notice($"[FiefWage] Cleared all exemptions for {party.Name} (ID: {key})");
                {
                    TextObject msg = GameTexts.FindText("str_modifiedarmy_wage_exemption_cleared");
                    msg.SetTextVariable("PARTY_NAME", party.Name.ToString());
                    ModLogger.Notice(msg.ToString());
                }
                else
                    ModLogger.Debug($"[FiefWage] Cleared all exemptions for {party.Name} (ID: {key})");
            }
        }

        /// <summary>
        /// 添加豁免（供招募采邑兵后调用）
        /// </summary>
        /// <param name="party"></param>
        /// <param name="troopCount"></param>
        /// <param name="durationDays"></param>
        public void AddExemption(MobileParty party, int troopCount, int durationDays)
        {
            if (troopCount <= 0 || party == null)
            {
                return;
            }

            string key = GetPartyKey(party);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var list = GetOrCreateList(party);
            if (list == null)
            {
                return;
            }

            list.Add(new FiefWageExemption(troopCount, durationDays));
            if (party.IsMainParty)
            {
                //ModLogger.Notice($"[FiefWage] Added {troopCount} troops for {party.Name} (ID: {key}) for {durationDays} days");
                TextObject msg = GameTexts.FindText("str_modifiedarmy_wage_exemption_added");
                msg.SetTextVariable("PARTY_NAME", party.Name.ToString());
                msg.SetTextVariable("COUNT", troopCount);
                msg.SetTextVariable("DAYS", durationDays);
                ModLogger.Notice(msg.ToString());
            }
            else
                ModLogger.Debug($"[FiefWage] Added {troopCount} troops for {party.Name} (ID: {key}) for {durationDays} days");
        }

        /// <summary>
        /// 获取当前可豁免的总人数（不触发 Tick）
        /// </summary>
        public int GetExemptableTroopCount(MobileParty party)
        {
            if (party == null) return 0;
            string key = GetPartyKey(party);
            if (string.IsNullOrEmpty(key)) return 0;

            if (!_exemptionMap.TryGetValue(key, out var list) || list == null)
                return 0;

            int total = 0;
            foreach (var exemption in list)
            {
                if (exemption.IsValid)
                    total += exemption.ExemptedTroopCount;
            }
            return total;
        }

        /// <summary>
        /// 消耗 X 名豁免额度（按加入顺序 FIFO），并清理无效项。
        /// </summary>
        public void ConsumeExemption(MobileParty party, int dismissedCount)
        {
            if (dismissedCount <= 0 || party == null)
            {
                return;
            }

            string key = GetPartyKey(party);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!_exemptionMap.TryGetValue(key, out var list) || list == null)
            {
                ModLogger.Debug($"[FiefWage] No exemption record found for party: {party.Name} (ID: {key})");
                return;
            }

            if (list.Count == 0)
            {
                return;
            }

            int consumed = 0;
            for (int i = 0; i < list.Count && consumed < dismissedCount; i++)
            {
                var e = list[i];
                if (e.IsValid)
                {
                    consumed += e.Consume(dismissedCount - consumed);
                }
            }

            // 可选：立即清理失效条目（非必须，DailyTick 会处理）
            list.RemoveAll(e => !e.IsValid);
            if (list.Count == 0)
            {
                _exemptionMap.Remove(key);
            }

            if (party.IsMainParty)
            {
                //ModLogger.Notice($"[FiefWage] Consumed {consumed}/{dismissedCount} troops for {party.Name} (ID: {key})");
                TextObject msg = GameTexts.FindText("str_modifiedarmy_wage_exemption_consumed");
                msg.SetTextVariable("PARTY_NAME", party.Name.ToString());
                msg.SetTextVariable("CONSUMED", consumed);
                msg.SetTextVariable("REQUESTED", dismissedCount);
                ModLogger.Notice(msg.ToString());
            }
            else
                ModLogger.Debug($"[FiefWage] Consumed {consumed}/{dismissedCount} troops for {party.Name} (ID: {key})");
        }

        /// <summary>
        /// 获取或创建列表（用于添加豁免）。
        /// </summary>
        private List<FiefWageExemption> GetOrCreateList(MobileParty party)
        {
            string key = GetPartyKey(party);
            if (string.IsNullOrEmpty(key)) return null;

            if (!_exemptionMap.TryGetValue(key, out var list) || list == null)
            {
                list = new List<FiefWageExemption>();
                _exemptionMap[key] = list;
            }
            return list;
        }
    }
}
