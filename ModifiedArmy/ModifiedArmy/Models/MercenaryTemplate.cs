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

            // 日志：确认Deserialize被调用及当前节点信息
            ModLogger.Info($"[MercenaryTemplate] 反序列化已触发，节点名称: {node?.Name}, StringId={StringId}");

            Culture = MBObjectManager.Instance.ReadObjectReferenceFromXml<CultureObject>("culture", node);
            // 日志：检查Culture是否成功解析
            ModLogger.Info($"[MercenaryTemplate] 文化对象解析结果: {(Culture != null ? Culture.StringId : "空引用")} in template '{TemplateId}'");

            MinCount = XmlHelper.ReadInt(node, "minCount");
            if (MinCount <= 0) MinCount = 5;
            // 日志：打印minCount读取结果
            ModLogger.Info($"[MercenaryTemplate] 最小人数读取值: {MinCount} in template '{TemplateId}'");

            MaxCount = XmlHelper.ReadInt(node, "maxCount");
            if (MaxCount <= 0) MaxCount = 10;
            // 日志：打印maxCount读取结果
            ModLogger.Info($"[MercenaryTemplate] 最大人数读取值: {MaxCount} in template '{TemplateId}'");

            var spawnChanceStr = node.Attributes?["spawnChance"]?.Value;
            if (spawnChanceStr != null && float.TryParse(spawnChanceStr, out var v))
            {
                SpawnChance = v;
            }
            // 日志：打印spawnChance读取结果
            ModLogger.Info($"[MercenaryTemplate] 刷新概率读取值: {SpawnChance} (原始字符串='{spawnChanceStr}') in template '{TemplateId}'");

            MercenaryTemplateManager.Instance.RegisterTemplate(this);
            // 日志：确认注册动作已执行
            ModLogger.Info($"[MercenaryTemplate] 已调用注册方法，模板ID={TemplateId}");
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
            if (template == null || string.IsNullOrEmpty(template.TemplateId))
            {
                // 日志：注册失败原因
                ModLogger.Info($"[MercenaryTemplateManager] 注册失败: 模板为空或模板ID为空字符串");
                return;
            }

            _templates[template.TemplateId] = template;
            // 日志：注册成功及当前已注册数量
            ModLogger.Info($"[MercenaryTemplateManager] 注册成功: {template.TemplateId}, 当前已注册总数={_templates.Count}");
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
            // 日志：按文化查找未命中
            ModLogger.Info($"[MercenaryTemplateManager] 按文化查找未命中，目标文化={culture.StringId}, 当前已注册模板数={_templates.Count}");
            return null;
        }
    }
}