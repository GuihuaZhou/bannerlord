using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// 雇佣兵模板，定义某文化在酒馆中每天刷新的雇佣兵人数范围。
    /// 通过 TaleWorlds MBObjectManager 系统从 XML 加载。
    /// </summary>
    public class MercenaryTemplate : MBObjectBase
    {
        /// <summary>
        /// 模板唯一标识符。
        /// </summary>
        public string TemplateId => StringId;

        /// <summary>
        /// 所属文化。
        /// </summary>
        public CultureObject Culture { get; private set; }

        /// <summary>
        /// 每次刷新的最少人数。默认 5。
        /// </summary>
        public int MinCount { get; private set; } = 5;

        /// <summary>
        /// 每次刷新的最多人数。默认 10。
        /// </summary>
        public int MaxCount { get; private set; } = 10;

        /// <summary>
        /// 刷新概率（0-1）。默认 0.3。
        /// </summary>
        public float SpawnChance { get; private set; } = 0.3f;

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            Culture = MBObjectManager.Instance.ReadObjectReferenceFromXml<CultureObject>("culture", node);

            MinCount = XmlHelper.ReadInt(node, "minCount");
            if (MinCount <= 0) MinCount = 5;

            MaxCount = XmlHelper.ReadInt(node, "maxCount");
            if (MaxCount <= 0) MaxCount = 10;

            var spawnChanceStr = node.Attributes?["spawnChance"]?.Value;
            if (spawnChanceStr != null && float.TryParse(spawnChanceStr, out var v))
            {
                SpawnChance = v;
            }

            MercenaryTemplateManager.Instance.RegisterTemplate(this);
        }
    }

    /// <summary>
    /// MercenaryTemplate 管理器，单例模式。
    /// </summary>
    public class MercenaryTemplateManager
    {
        public static readonly MercenaryTemplateManager Instance = new();

        private readonly Dictionary<string, MercenaryTemplate> _templates = new();

        private MercenaryTemplateManager() { }

        /// <summary>
        /// 注册一个模板到管理器。
        /// </summary>
        public void RegisterTemplate(MercenaryTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.TemplateId)) return;
            _templates[template.TemplateId] = template;
        }

        /// <summary>
        /// 根据文化获取对应的雇佣兵模板。
        /// </summary>
        public MercenaryTemplate GetTemplateByCulture(CultureObject culture)
        {
            if (culture == null) return null;
            foreach (var kvp in _templates)
            {
                if (kvp.Value.Culture == culture)
                    return kvp.Value;
            }
            return null;
        }
    }
}