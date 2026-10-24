using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// 俘虏业务自己的政策分组。它只用于保持既有归顺难度与留俘规则，
    /// 不能用于判断封邑身份、生产资格、兵力上限或招募构成。
    /// </summary>
    public enum PrisonerPolicyClass
    {
        Retinue,
        Sergeant,
        Militia,
        Marine,
        Slave,
        Other
    }

    /// <summary>
    /// 从基础兵配置建立俘虏政策映射，并沿升级树传播到后续兵种。
    /// 这是旧五类数据在俘虏领域的唯一消费者。
    /// </summary>
    public static class PrisonerTroopPolicy
    {
        private static readonly Dictionary<CharacterObject, PrisonerPolicyClass>
            _policyMap = new();

        public static PrisonerPolicyClass GetPolicy(CharacterObject troop)
        {
            return troop != null &&
                _policyMap.TryGetValue(troop, out PrisonerPolicyClass policy)
                    ? policy
                    : PrisonerPolicyClass.Other;
        }

        public static void InitializeAll()
        {
            _policyMap.Clear();
            foreach (CultureObject culture in
                MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
            {
                BasicTroopGroup group =
                    BasicTroopGroupManager.GetGroupForCulture(culture);
                if (group == null) continue;
                foreach (var pair in group.TroopsByPolicy)
                {
                    PrisonerPolicyClass policy = pair.Key;
                    foreach (BasicTroopEntry entry in pair.Value)
                        AddUpgradeTree(entry?.Troop, policy);
                }
            }

            ModLogger.Debug(
                $"[Prisoner policy] Initialized {_policyMap.Count} troop mappings.");
        }

        private static void AddUpgradeTree(
            CharacterObject troop,
            PrisonerPolicyClass policy)
        {
            if (troop == null) return;
            _policyMap[troop] = policy;
            foreach (CharacterObject upgrade in troop.UpgradeTargets ??
                Array.Empty<CharacterObject>())
            {
                AddUpgradeTree(upgrade, policy);
            }
        }
    }
}
