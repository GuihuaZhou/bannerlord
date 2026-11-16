// Feudal template domain. XML roots define immutable supply branches while
// CharacterObject upgrade trees provide runtime membership; ambiguous trees
// are rejected rather than inventing historical soldier origins.
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// 封邑部队模板，定义某类封邑可招募的兵种池。
    /// 模板通过 ID 标识，不包含文化/类型/港口等匹配信息——
    /// 这些由外部调用方根据命名约定或上下文决定。
    /// </summary>
    public class FiefPartyTemplate : MBObjectBase,
        ITroopProductionTemplate
    {
        /// <summary>
        /// 模板唯一标识符
        /// </summary>
        public string TemplateId => StringId;

        public CultureObject Culture => culture;

        public TroopProductionKind ProductionKind => TroopProductionKind.Fief;

        private readonly List<TroopProductionEntry> _troopEntries =
            new List<TroopProductionEntry>();

        public IReadOnlyList<TroopProductionEntry> TroopEntries =>
            _troopEntries;

        /// <summary>
        /// 所有基础兵种
        /// </summary>
        private readonly HashSet<CharacterObject> _allEnableTroops = new();
        private readonly Dictionary<CharacterObject, TroopProductionEntry>
            _supplyBranches = new();
        private readonly HashSet<CharacterObject> _ambiguousSupplyTroops = new();

        public CultureObject culture { get; private set; }


        /// <summary>
        /// 判断指定的 CharacterObject 是否为本模板中启用的兵种（即存在于升级树中）。
        /// </summary>
        /// <param name="troop">要检查的兵种</param>
        /// <returns>如果存在则返回 true，否则返回 false</returns>
        public bool IsEnableTroop(CharacterObject troop)
        {
            return troop != null && _allEnableTroops.Contains(troop);
        }

        /// <summary>
        /// Resolves a troop to its unique positive-weight XML root. A troop
        /// reachable from multiple roots is deliberately rejected because its
        /// historical supply branch cannot be inferred from its current type.
        /// </summary>
        public bool TryGetSupplyBranch(CharacterObject troop,
            out TroopProductionEntry branch)
        {
            branch = null;
            return troop != null && !_ambiguousSupplyTroops.Contains(troop) &&
                _supplyBranches.TryGetValue(troop, out branch);
        }

        /// <summary>Positive-weight XML roots in configured template order.</summary>
        public IReadOnlyList<TroopProductionEntry> SupplyBranches =>
            _troopEntries.Where(entry => entry?.Troop != null &&
                entry.Weight > 0).ToList();

        /// <summary>
        /// 递归获取兵种升级树上的全部士兵
        /// </summary>
        private void AddUpgradeTree(CharacterObject current)
        {
            if (current == null) return;
            if (!_allEnableTroops.Add(current)) return;

            if (current.UpgradeTargets?.Length > 0)
            {
                foreach (var upgrade in current.UpgradeTargets)
                {
                    if (upgrade != null)
                    {
                        AddUpgradeTree(upgrade);
                    }
                }
            }
        }

        /// <summary>
        /// 从 XML 节点反序列化模板数据
        /// </summary>
        /// <param name="objectManager">对象管理器</param>
        /// <param name="node">XML 节点</param>
        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            culture = MBObjectManager.Instance.ReadObjectReferenceFromXml<CultureObject>("culture", node);

            // 正式配置只接受平铺 troop，不再读取旧五类包装。
            var compNode = node.SelectSingleNode("TroopComposition");
            if (compNode != null)
            {
                foreach (XmlNode troopNode in compNode.SelectNodes("troop"))
                    ParseTroop(objectManager, troopNode);
            }

            FiefPartyTemplateManager.Instance.RegisterTemplate(this);
            TroopProductionTemplateRepository.Instance.Register(this);
        }


        /// <summary>
        /// 解析一个具体兵种及其生产权重，并读取统一兵营等级。
        /// </summary>
        private void ParseTroop(MBObjectManager objectManager, XmlNode troopNode)
        {
            CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("id", troopNode);
            if (troop == null)
            {
                string rawId = XmlHelper.ReadString(troopNode, "id");
                ModLogger.Warn($"[FiefPartyTemplate] Unknown troop ID: {rawId} in template '{TemplateId}'");
                return;
            }

            BasicTroopEntry basicTroop = BasicTroopGroupManager.FindBasicTroop(culture, troop);
            if (basicTroop == null) return;
            int troopWeight = troopNode.Attributes?["weight"] == null
                ? 1
                : XmlHelper.ReadInt(troopNode, "weight");
            _troopEntries.Add(new TroopProductionEntry(
                troop,
                troopWeight,
                basicTroop.RequiredBarracksLevel));
        }


        /// <summary>
        /// 遍历basic troop的升级树，获取全部使能的troop
        /// </summary>
        /// 
        public void InitAllEnableTroops()
        {
            _allEnableTroops.Clear();
            _supplyBranches.Clear();
            _ambiguousSupplyTroops.Clear();
            foreach (TroopProductionEntry entry in _troopEntries)
            {
                AddUpgradeTree(entry.Troop);
                if (entry?.Troop == null || entry.Weight <= 0) continue;
                RegisterSupplyTree(entry.Troop, entry,
                    new HashSet<CharacterObject>());
            }
        }

        /// <summary>
        /// Assigns one upgrade tree to one XML root. Cross-root reachability is
        /// reported once and removed from admission eligibility.
        /// </summary>
        private void RegisterSupplyTree(CharacterObject troop,
            TroopProductionEntry root, HashSet<CharacterObject> visited)
        {
            if (troop == null || !visited.Add(troop)) return;
            if (_supplyBranches.TryGetValue(troop,
                    out TroopProductionEntry existing) && existing != root)
            {
                _supplyBranches.Remove(troop);
                if (_ambiguousSupplyTroops.Add(troop))
                {
                    TextObject message = GameTexts.FindText(
                        "str_modifiedarmy_fief_supply_branch_ambiguous");
                    message.SetTextVariable("TROOP_NAME", troop.Name);
                    message.SetTextVariable("FIRST_ROOT_NAME",
                        existing.Troop.Name);
                    message.SetTextVariable("SECOND_ROOT_NAME",
                        root.Troop.Name);
                    message.SetTextVariable("TEMPLATE_ID", TemplateId);
                    message.SetTextVariable("TROOP_ID", troop.StringId);
                    ModLogger.Error(message.ToString());
                }
            }
            else if (!_ambiguousSupplyTroops.Contains(troop))
                _supplyBranches[troop] = root;
            foreach (CharacterObject target in troop.UpgradeTargets ??
                Array.Empty<CharacterObject>())
                RegisterSupplyTree(target, root, visited);
        }
    }


    /// <summary>
    /// 封邑部队模板管理器，用于通过模板 ID 快速查找兵种配置。
    /// 模板本身不包含匹配逻辑，调用方需自行决定使用哪个模板 ID。
    /// </summary>
    public class FiefPartyTemplateManager
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly FiefPartyTemplateManager Instance = new();

        /// <summary>
        /// 模板字典，键为模板 ID
        /// </summary>
        private readonly Dictionary<string, FiefPartyTemplate> _templates = new();

        // 私有构造函数，确保单例
        private FiefPartyTemplateManager() { }

        /// <summary>
        /// 注册一个模板到管理器
        /// </summary>
        /// <param name="template">要注册的模板</param>
        public void RegisterTemplate(FiefPartyTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.TemplateId)) return;

            _templates[template.TemplateId] = template;
            
            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_template_registered");
            msg.SetTextVariable("TEMPLATE_ID", template.TemplateId);
            // Template registration is startup diagnostics rather than a player
            // event. Keep the localized details available only at Debug level.
            ModLogger.Debug(msg.ToString());
        }

        /// <summary>
        /// 根据模板 ID 获取对应的部队模板
        /// </summary>
        /// <param name="templateId">模板 ID</param>
        /// <returns>找到的模板，未找到则返回 null</returns>
        public FiefPartyTemplate GetTemplate(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return null;

            _templates.TryGetValue(templateId, out var template);
            return template;
        }

        /// <summary>
        /// 清空所有已注册的模板（用于重载或卸载）
        /// </summary>
        public void Clear()
        {
            _templates.Clear();
        }

        /// <summary>
        /// 遍历所有template，初始化全部使能的troop
        /// </summary>
        /// 
        public void InitAllEnableTroops()
        {
            foreach(var kvp in _templates)
            {
                kvp.Value.InitAllEnableTroops();
            }
        }
    }
}
