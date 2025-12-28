using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// 封邑部队模板，定义某类封邑可招募的兵种池。
    /// 模板通过 ID 标识，不包含文化/类型/港口等匹配信息——
    /// 这些由外部调用方根据命名约定或上下文决定。
    /// </summary>
    public class FiefPartyTemplate : MBObjectBase
    {
        /// <summary>
        /// 模板唯一标识符（来自 XML 的 id 属性）
        /// </summary>
        public string TemplateId => StringId;

        /// <summary>
        /// 所有兵种条目（包含类型信息），便于统一遍历
        /// </summary>
        public List<BasicTroopEntry> AllTroops { get; } = new();

        /// <summary>
        /// 扈从兵种列表（Retinue）
        /// </summary>
        public List<BasicTroopEntry> RetinueTroops { get; } = new();

        /// <summary>
        /// 军士兵种列表（Sergeant）
        /// </summary>
        public List<BasicTroopEntry> SergeantTroops { get; } = new();

        /// <summary>
        /// 民兵兵种列表（Militia）
        /// </summary>
        public List<BasicTroopEntry> MilitiaTroops { get; } = new();

        /// <summary>
        /// 从 XML 节点反序列化模板数据
        /// </summary>
        /// <param name="objectManager">对象管理器</param>
        /// <param name="node">XML 节点</param>
        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            // 仅解析兵种构成部分
            var compNode = node.SelectSingleNode("TroopComposition");
            if (compNode != null)
            {
                ParseTroopGroup(objectManager, compNode, "RetinueTroops", FiefTroopType.Fief_Retinue, RetinueTroops);
                ParseTroopGroup(objectManager, compNode, "SergeantTroops", FiefTroopType.Fief_Sergeant, SergeantTroops);
                ParseTroopGroup(objectManager, compNode, "MilitiaTroops", FiefTroopType.Fief_Militia, MilitiaTroops);
            }

            // 合并到 AllTroops（可选，方便统一处理）
            AllTroops.AddRange(RetinueTroops);
            AllTroops.AddRange(SergeantTroops);
            AllTroops.AddRange(MilitiaTroops);

            // 注册到全局管理器
            FiefPartyTemplateManager.Instance.RegisterTemplate(this);
        }


        /// <summary>
        /// 解析某一类兵种组（如 RetinueTroops）
        /// </summary>
        /// <param name="parent">父节点（TroopComposition）</param>
        /// <param name="groupName">子节点名称（如 "RetinueTroops"）</param>
        /// <param name="troopType">兵种类型</param>
        /// <param name="targetList">目标列表</param>
        private void ParseTroopGroup(
            MBObjectManager objectManager,
            XmlNode parent,
            string groupName,
            FiefTroopType troopType,
            List<BasicTroopEntry> targetList)
        {
            var groupNode = parent.SelectSingleNode(groupName);
            if (groupNode == null) return;

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
                int weight = 1;
                if (troopNode.Attributes?["weight"] != null)
                {
                    weight = XmlHelper.ReadInt(troopNode, "weight");
                }

                // 创建条目并加入列表
                var entry = new BasicTroopEntry(troop, weight, troopType);
                targetList.Add(entry);
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
    }
}
