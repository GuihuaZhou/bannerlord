using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
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
        public Dictionary<SoldierType, List<BasicTroopEntry>> _allBasicTroops { get; } = new();

        /// <summary>
        /// 不同类型基础士兵所占权重
        /// </summary>
        public Dictionary<SoldierType, int> _soldierTypeWeights { get; } = new();

        /// <summary>
        /// 所有使能的士兵
        /// </summary>
        public Dictionary<CharacterObject, SoldierType> _allEnableTroops { get; } = new();

        public CultureObject culture { get; private set; }


        /// <summary>
        /// 判断指定的 CharacterObject 是否为本模板中启用的兵种（即存在于升级树中）。
        /// </summary>
        /// <param name="troop">要检查的兵种</param>
        /// <returns>如果存在则返回 true，否则返回 false</returns>
        public bool IsEnableTroop(CharacterObject troop)
        {
            return _allEnableTroops.ContainsKey(troop);
        }

        /// <summary>
        /// 获取基础兵种类型权重
        /// </summary>
        public Dictionary<SoldierType, int> GetSoldierTypeWeights()
        {
            return _soldierTypeWeights;
        }

        /// <summary>
        /// 获取所有基础兵种条目
        /// 注意：内部列表和 BasicTroopEntry 对象未深度复制，仅复制字典结构。
        /// </summary>
        public Dictionary<SoldierType, List<BasicTroopEntry>> GetBasicTroops()
        {
            return _allBasicTroops;
        }

        /// <summary>
        /// 递归获取兵种升级树上的全部士兵
        /// </summary>
        private void PropagateTypeThroughUpgradeTree(CharacterObject current, SoldierType type)
        {
            if (current == null) return;
            _allEnableTroops[current] = type;

            if (current.UpgradeTargets?.Length > 0)
            {
                foreach (var upgrade in current.UpgradeTargets)
                {
                    if (upgrade != null)
                    {
                        PropagateTypeThroughUpgradeTree(upgrade, type);
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

            // 仅解析兵种构成部分
            var compNode = node.SelectSingleNode("TroopComposition");
            if (compNode != null)
            {
                ParseTroopGroup(objectManager, compNode, "RetinueTroops", SoldierType.Retinue);
                ParseTroopGroup(objectManager, compNode, "SergeantTroops", SoldierType.Sergeant);
                ParseTroopGroup(objectManager, compNode, "MilitiaTroops", SoldierType.Militia);
                ParseTroopGroup(objectManager, compNode, "MarineTroops", SoldierType.Marine);
                ParseTroopGroup(objectManager, compNode, "SlaveTroops", SoldierType.Slave);
            }

            FiefPartyTemplateManager.Instance.RegisterTemplate(this);
            TroopProductionTemplateRepository.Instance.Register(this);
        }


        /// <summary>
        /// 解析某一类兵种组（如 RetinueTroops）
        /// </summary>
        /// <param name="parent">父节点（TroopComposition）</param>
        /// <param name="groupName">子节点名称（如 "RetinueTroops"）</param>
        /// <param name="troopType">兵种类型</param>
        private void ParseTroopGroup(
            MBObjectManager objectManager,
            XmlNode parent,
            string groupName,
            SoldierType troopType)
        {
            // The legacy fief implementation still consumes a weight per
            // SoldierType. Derive that compatibility value from the sum of
            // concrete troop weights instead of restoring an XML group
            // weight that the new shared pool format no longer supports.
            if (!_soldierTypeWeights.ContainsKey(troopType))
            {
                _soldierTypeWeights[troopType] = 0;
            }

            var groupNode = parent.SelectSingleNode(groupName);
            if (groupNode == null) return;

            if (!_allBasicTroops.TryGetValue(troopType, out var list))
            {
                _allBasicTroops[troopType] = new List<BasicTroopEntry>();
            }

            foreach (XmlNode troopNode in groupNode.SelectNodes("troop"))
            {
                CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("id", troopNode);
                if (troop == null)
                {
                    string rawId = XmlHelper.ReadString(troopNode, "id");
                    ModLogger.Warn($"[FiefPartyTemplate] Unknown troop ID: {rawId} in template '{TemplateId}'");
                    continue;
                }

                // 读取权重（默认为 1）
                int troopWeight = 1;
                if (troopNode.Attributes?["weight"] != null)
                {
                    troopWeight = XmlHelper.ReadInt(troopNode, "weight");
                }

                var basicTroop = BasicTroopGroupManager.FindBasicTroop(culture, troopType, troop);
                if (basicTroop == null)
                {
                    continue;
                }
                BasicTroopEntry templateTroop = new BasicTroopEntry(
                    basicTroop.Troop,
                    troopWeight,
                    troopType,
                    basicTroop.RequiredBarracksLevel);
                _allBasicTroops[troopType].Add(templateTroop);
                _soldierTypeWeights[troopType] += troopWeight;
                _troopEntries.Add(new TroopProductionEntry(
                    templateTroop.Troop,
                    troopWeight,
                    templateTroop.RequiredBarracksLevel));
            }
        }


        /// <summary>
        /// 遍历basic troop的升级树，获取全部使能的troop
        /// </summary>
        /// 
        public void InitAllEnableTroops()
        {
            // 遍历士兵升级树，获取所有使能的士兵
            foreach (var kvp in _allBasicTroops)
            {
                foreach (var basicTroop in kvp.Value)
                {
                    PropagateTypeThroughUpgradeTree(basicTroop.Troop, kvp.Key);
                }
            }
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
            ModLogger.Info(msg.ToString());
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
