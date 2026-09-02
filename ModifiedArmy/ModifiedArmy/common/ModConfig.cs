using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.common
{
    /// <summary>
    /// AI 招募兵源偏好（按文化）。
    /// </summary>
    public struct AiCulturalPreference
    {
        /// <summary>
        /// 封邑偏好权重。
        /// </summary>
        public float Fief;
        /// <summary>
        /// 志愿兵偏好权重。
        /// </summary>
        public float Volunteer;
        /// <summary>
        /// 雇佣兵偏好权重。
        /// </summary>
        public float Mercenary;
    }

    /// <summary>
    /// AI 情境修正（按情境类型）。
    /// </summary>
    public struct AiSituationalModifier
    {
        /// <summary>
        /// 封邑偏好乘数。
        /// </summary>
        public float Fief;
        /// <summary>
        /// 志愿兵偏好乘数。
        /// </summary>
        public float Volunteer;
        /// <summary>
        /// 雇佣兵偏好乘数。
        /// </summary>
        public float Mercenary;
        /// <summary>
        /// 情境触发阈值（broke: Gold &lt; threshold × Wage; critical: PartySizeRatio &lt; threshold）。仅部分情境使用。
        /// </summary>
        public float Threshold;
    }

    /// <summary>
    /// 全局配置对象，通过 TaleWorlds MBObjectManager 系统从 XML 加载。
    /// 与 FiefPartyTemplate 模式完全一致。
    /// </summary>
    public class ModConfig : MBObjectBase
    {
        /// <summary>
        /// 配置唯一标识符。
        /// </summary>
        public string ConfigId => StringId;

        /// <summary>
        /// 最低日志显示级别。0=Debug, 1=Info, 2=Notice, 3=Warn, 4=Error。默认 2（Notice）。
        /// </summary>
        public int MinLogLevel { get; private set; } = 2;

        /// <summary>
        /// 城镇贫困繁荣度阈值。低于此值视为贫困城镇。默认 1000。
        /// </summary>
        public float TownPoorThreshold { get; private set; } = 1000f;

        /// <summary>
        /// 城镇一般繁荣度阈值。低于此值视为一般城镇。默认 4000。
        /// </summary>
        public float TownAverageThreshold { get; private set; } = 4000f;

        /// <summary>
        /// 城镇富有繁荣度阈值。低于此值视为富有城镇。默认 8000。
        /// </summary>
        public float TownRichThreshold { get; private set; } = 8000f;

        /// <summary>
        /// 城镇非常富有繁荣度阈值。高于此值视为非常富有城镇。默认 12000。
        /// </summary>
        public float TownVeryRichThreshold { get; private set; } = 12000f;

        /// <summary>
        /// 城堡贫困繁荣度阈值。低于此值视为贫困城堡。默认 300。
        /// </summary>
        public float CastlePoorThreshold { get; private set; } = 300f;

        /// <summary>
        /// 城堡一般繁荣度阈值。低于此值视为一般城堡。默认 800。
        /// </summary>
        public float CastleAverageThreshold { get; private set; } = 800f;

        /// <summary>
        /// 城堡富有繁荣度阈值。低于此值视为富有城堡。默认 1500。
        /// </summary>
        public float CastleRichThreshold { get; private set; } = 1500f;

        /// <summary>
        /// 城堡非常富有繁荣度阈值。高于此值视为非常富有城堡。默认 2000。
        /// </summary>
        public float CastleVeryRichThreshold { get; private set; } = 2000f;

        /// <summary>
        /// 补员公式中繁荣度的权重。默认 0.4（与 HearthsWeight 之和为 1）。
        /// </summary>
        public float ProsperityWeight { get; private set; } = 0.4f;

        /// <summary>
        /// 补员公式中村庄户数的权重。默认 0.6（与 ProsperityWeight 之和为 1）。
        /// </summary>
        public float HearthsWeight { get; private set; } = 0.6f;

        /// <summary>
        /// 村庄最低户数阈值。低于此值的村庄不参与补员计算。默认 100。
        /// </summary>
        public float VillageMinHearthThreshold { get; private set; } = 100f;

        /// <summary>
        /// 村庄满补员户数阈值。达到此值时补员效率为 100%。默认 900。
        /// </summary>
        public float VillageMaxReinforcementHearthThreshold { get; private set; } = 900f;

        /// <summary>
        /// 封邑兵工资豁免天数。征召后此天数内免工资。默认 28。
        /// </summary>
        public int FiefWageExemptionDays { get; private set; } = 28;

        /// <summary>
        /// AI 对封邑繁荣度的影响系数。AI 征召/解散对繁荣度的影响乘以该值。默认 0.1。
        /// </summary>
        public float AiFiefProsperityImpactMultiplier { get; private set; } = 0.1f;

        /// <summary>
        /// AI 对封邑户数的影响系数。AI 征召/解散对户数的影响乘以该值。默认 0.1。
        /// </summary>
        public float AiFiefHearthImpactMultiplier { get; private set; } = 0.1f;

        /// <summary>
        /// 瓦兰迪亚志愿兵生成倍率。值越低志愿兵越少。默认 0.2。
        /// </summary>
        public float VlandiaVolunteerMultiplier { get; private set; } = 0.2f;

        /// <summary>
        /// 帝国志愿兵生成倍率。默认 0.6。
        /// </summary>
        public float EmpireVolunteerMultiplier { get; private set; } = 0.6f;

        /// <summary>
        /// 阿塞莱志愿兵生成倍率。默认 0.7。
        /// </summary>
        public float AseraiVolunteerMultiplier { get; private set; } = 0.7f;

        /// <summary>
        /// 玩家定居点囚犯逃逸概率。默认 0.01（1%）。
        /// </summary>
        public float PlayerSettlementPrisonerEscapeChance { get; private set; } = 0.01f;

        // ========== AI 招募决策配置 ==========

        // --- Need Score（招兵紧迫度）---

        /// <summary>
        /// 兵力缺口因子权重。默认 0.4。
        /// </summary>
        public float AiNeedManpowerWeight { get; private set; } = 0.4f;

        /// <summary>
        /// 战争紧迫度因子权重。默认 0.35。
        /// </summary>
        public float AiNeedWarUrgencyWeight { get; private set; } = 0.35f;

        /// <summary>
        /// 经济承受力因子权重。默认 0.25。
        /// </summary>
        public float AiNeedEconomicWeight { get; private set; } = 0.25f;

        /// <summary>
        /// NeedScore 阈值，低于此分数不触发招募决策。默认 0.25。
        /// </summary>
        public float AiNeedThreshold { get; private set; } = 0.25f;

        /// <summary>
        /// 和平期的战争紧迫度值。默认 0.1。
        /// </summary>
        public float AiNeedPeaceUrgency { get; private set; } = 0.1f;

        /// <summary>
        /// 战争中的战争紧迫度值。默认 0.7。
        /// </summary>
        public float AiNeedWarUrgency { get; private set; } = 0.7f;

        /// <summary>
        /// 被围攻时的战争紧迫度值。默认 1.0。
        /// </summary>
        public float AiNeedSiegeUrgency { get; private set; } = 1.0f;

        /// <summary>
        /// 经济承受力计算周数。EconomicCapacity = Gold / (Wage × 此值)。默认 10.0。
        /// </summary>
        public float AiNeedEconomicWeeks { get; private set; } = 10.0f;

        // --- 文化基础偏好（字典，按 CultureObject 索引）---

        /// <summary>
        /// AI 各文化的兵源偏好权重。键为文化对象。
        /// </summary>
        public Dictionary<CultureObject, AiCulturalPreference> AiCulturalPrefs { get; private set; } = new Dictionary<CultureObject, AiCulturalPreference>();

        // --- 文化身份修正 ---

        /// <summary>
        /// 本文化领主封邑偏好乘数。默认 1.3。
        /// </summary>
        public float AiCultMatchFiefBonus { get; private set; } = 1.3f;

        /// <summary>
        /// 异文化领主志愿兵偏好乘数。默认 0.5。
        /// </summary>
        public float AiCultMismatchVolunteerPenalty { get; private set; } = 0.5f;

        /// <summary>
        /// 异文化领主雇佣兵偏好乘数。默认 1.3。
        /// </summary>
        public float AiCultMismatchMercenaryBonus { get; private set; } = 1.3f;

        // --- 情境修正（字典，按情境 id 索引）---

        /// <summary>
        /// AI 各情境下的兵源偏好修正。键为情境 id（siege/broke/critical/peace/war）。
        /// </summary>
        public Dictionary<string, AiSituationalModifier> AiSituationalMods { get; private set; } = new Dictionary<string, AiSituationalModifier>();

        // --- 敌军距离检测 ---

        /// <summary>
        /// 检测附近敌军的距离半径。默认 50.0。
        /// </summary>
        public float AiEnemyDetectRadius { get; private set; } = 50.0f;

        /// <summary>
        /// 敌军强度比例阈值。敌军总强度 &gt; 己方 × 此值视为威胁。默认 0.5。
        /// </summary>
        public float AiEnemyStrengthRatio { get; private set; } = 0.5f;

        // --- 杂项（替代原硬编码）---

        /// <summary>
        /// PartySizeRatio 上限，超过此比例不招募。默认 0.7。
        /// </summary>
        public float AiRecruitPartySizeRatioCap { get; private set; } = 0.7f;

        /// <summary>
        /// 最低所需金币占工资比例。默认 0.3。
        /// </summary>
        public float AiRecruitMinGoldWageRatio { get; private set; } = 0.3f;

        /// <summary>
        /// 随机概率门槛。默认 0.8。
        /// </summary>
        public float AiRecruitRandomChance { get; private set; } = 0.8f;

        /// <summary>
        /// 雇佣兵搜索半径。只搜索本国 town（不限距离）或此半径内的非敌对 town。默认 150.0。
        /// </summary>
        public float AiMercenarySearchRadius { get; private set; } = 150.0f;

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            // ========== Logging ==========
            XmlNode loggingNode = node.SelectSingleNode("Logging");
            if (loggingNode != null)
            {
                MinLogLevel = ReadIntVal(loggingNode, "MinLogLevel", 2);
                if (MinLogLevel < 0 || MinLogLevel > 4) MinLogLevel = 2;
            }

            // ========== SettlementProsperity ==========
            XmlNode settlementProsperityNode = node.SelectSingleNode("SettlementProsperity");
            if (settlementProsperityNode != null)
            {
                XmlNode townNode = settlementProsperityNode.SelectSingleNode("Town");
                if (townNode != null)
                {
                    TownPoorThreshold = ReadFloatVal(townNode, "PoorThreshold", 1000f);
                    TownAverageThreshold = ReadFloatVal(townNode, "AverageThreshold", 4000f);
                    TownRichThreshold = ReadFloatVal(townNode, "RichThreshold", 8000f);
                    TownVeryRichThreshold = ReadFloatVal(townNode, "VeryRichThreshold", 12000f);
                }
                XmlNode castleNode = settlementProsperityNode.SelectSingleNode("Castle");
                if (castleNode != null)
                {
                    CastlePoorThreshold = ReadFloatVal(castleNode, "PoorThreshold", 300f);
                    CastleAverageThreshold = ReadFloatVal(castleNode, "AverageThreshold", 800f);
                    CastleRichThreshold = ReadFloatVal(castleNode, "RichThreshold", 1500f);
                    CastleVeryRichThreshold = ReadFloatVal(castleNode, "VeryRichThreshold", 2000f);
                }
            }

            // ========== FiefRecruitment ==========
            XmlNode fiefRecruitmentNode = node.SelectSingleNode("FiefRecruitment");
            if (fiefRecruitmentNode != null)
            {
                XmlNode weightsNode = fiefRecruitmentNode.SelectSingleNode("Weights");
                if (weightsNode != null)
                {
                    ProsperityWeight = ReadFloatVal(weightsNode, "Prosperity", 0.4f);
                    HearthsWeight = ReadFloatVal(weightsNode, "Hearths", 0.6f);
                }
                XmlNode villageNode = fiefRecruitmentNode.SelectSingleNode("Village");
                if (villageNode != null)
                {
                    VillageMinHearthThreshold = ReadFloatVal(villageNode, "MinHearthThreshold", 100f);
                    VillageMaxReinforcementHearthThreshold = ReadFloatVal(villageNode, "MaxReinforcementHearthThreshold", 900f);
                }
            }

            // ========== FiefEconomy ==========
            XmlNode fiefEconomyNode = node.SelectSingleNode("FiefEconomy");
            if (fiefEconomyNode != null)
            {
                XmlNode wageNode = fiefEconomyNode.SelectSingleNode("Wage");
                if (wageNode != null)
                {
                    FiefWageExemptionDays = ReadIntVal(wageNode, "ExemptionDays", 28);
                }
                XmlNode aiImpactNode = fiefEconomyNode.SelectSingleNode("AiImpact");
                if (aiImpactNode != null)
                {
                    AiFiefProsperityImpactMultiplier = ReadFloatVal(aiImpactNode, "ProsperityMultiplier", 0.1f);
                    AiFiefHearthImpactMultiplier = ReadFloatVal(aiImpactNode, "HearthMultiplier", 0.1f);
                }
            }

            // ========== VolunteerRecruitment ==========
            XmlNode volunteerRecruitmentNode = node.SelectSingleNode("VolunteerRecruitment");
            if (volunteerRecruitmentNode != null)
            {
                XmlNode vlandiaNode = volunteerRecruitmentNode.SelectSingleNode("Vlandia");
                if (vlandiaNode != null)
                {
                    VlandiaVolunteerMultiplier = ReadFloatVal(vlandiaNode, "GenerationMultiplier", 0.2f);
                }
                XmlNode empireNode = volunteerRecruitmentNode.SelectSingleNode("Empire");
                if (empireNode != null)
                {
                    EmpireVolunteerMultiplier = ReadFloatVal(empireNode, "GenerationMultiplier", 0.6f);
                }
                XmlNode aseraiNode = volunteerRecruitmentNode.SelectSingleNode("Aserai");
                if (aseraiNode != null)
                {
                    AseraiVolunteerMultiplier = ReadFloatVal(aseraiNode, "GenerationMultiplier", 0.7f);
                }
            }

            // ========== Prisoners ==========
            XmlNode prisonersNode = node.SelectSingleNode("Prisoners");
            if (prisonersNode != null)
            {
                XmlNode playerSettlementNode = prisonersNode.SelectSingleNode("PlayerSettlement");
                if (playerSettlementNode != null)
                {
                    PlayerSettlementPrisonerEscapeChance = ReadFloatVal(playerSettlementNode, "EscapeChance", 0.01f);
                }
            }

            // ========== AI 招募决策（嵌套 XML）==========
            DeserializeAiRecruitment(node);

            ModConfigManager.Instance.RegisterConfig(this);

            TextObject msg = GameTexts.FindText("str_modifiedarmy_config_loaded");
            msg.SetTextVariable("CONFIG_ID", ConfigId);
            ModLogger.Info(msg.ToString());

            // ===== 配置加载验证（一次性）=====
            ModLogger.Notice($"[ModConfig] AI招募配置已加载: 文化偏好={AiCulturalPrefs.Count}条, 情境修正={AiSituationalMods.Count}条, NeedThreshold={AiNeedThreshold}, RandomChance={AiRecruitRandomChance}");
        }

        /// <summary>
        /// 解析 AiRecruitment 嵌套节点。
        /// </summary>
        private void DeserializeAiRecruitment(XmlNode modConfigNode)
        {
            XmlNode aiNode = modConfigNode.SelectSingleNode("AiRecruitment");
            if (aiNode == null)
            {
                ModLogger.Notice("[ModConfig] 警告：未找到AiRecruitment节点，使用默认值");
                return;
            }

            // NeedScore
            XmlNode needScoreNode = aiNode.SelectSingleNode("NeedScore");
            if (needScoreNode != null)
            {
                AiNeedManpowerWeight = ReadFloatVal(needScoreNode, "ManpowerWeight", 0.4f);
                AiNeedWarUrgencyWeight = ReadFloatVal(needScoreNode, "WarUrgencyWeight", 0.35f);
                AiNeedEconomicWeight = ReadFloatVal(needScoreNode, "EconomicWeight", 0.25f);
                AiNeedThreshold = ReadFloatVal(needScoreNode, "Threshold", 0.25f);
                AiNeedPeaceUrgency = ReadFloatVal(needScoreNode, "PeaceUrgency", 0.1f);
                AiNeedWarUrgency = ReadFloatVal(needScoreNode, "WarUrgency", 0.7f);
                AiNeedSiegeUrgency = ReadFloatVal(needScoreNode, "SiegeUrgency", 1.0f);
                AiNeedEconomicWeeks = ReadFloatVal(needScoreNode, "EconomicWeeks", 10.0f);
            }

            // CulturalPreferences
            XmlNode cultPrefsNode = aiNode.SelectSingleNode("CulturalPreferences");
            if (cultPrefsNode != null)
            {
                foreach (XmlNode cultureNode in cultPrefsNode.SelectNodes("Culture"))
                {
                    CultureObject culture = MBObjectManager.Instance.ReadObjectReferenceFromXml<CultureObject>("id", cultureNode);
                    if (culture != null)
                    {
                        AiCulturalPrefs[culture] = new AiCulturalPreference
                        {
                            Fief = ReadFloatVal(cultureNode, "Fief", 0.4f),
                            Volunteer = ReadFloatVal(cultureNode, "Volunteer", 0.4f),
                            Mercenary = ReadFloatVal(cultureNode, "Mercenary", 0.2f)
                        };
                    }
                    else
                    {
                        string idAttr = cultureNode.Attributes?["id"]?.Value ?? "unknown";
                        ModLogger.Notice($"[ModConfig] 警告：文化id='{idAttr}'无法解析，跳过");
                    }
                }
            }

            // CulturalIdentity
            XmlNode cultIdNode = aiNode.SelectSingleNode("CulturalIdentity");
            if (cultIdNode != null)
            {
                AiCultMatchFiefBonus = ReadFloatVal(cultIdNode, "MatchFiefBonus", 1.3f);
                AiCultMismatchVolunteerPenalty = ReadFloatVal(cultIdNode, "MismatchVolunteerPenalty", 0.5f);
                AiCultMismatchMercenaryBonus = ReadFloatVal(cultIdNode, "MismatchMercenaryBonus", 1.3f);
            }

            // SituationalModifiers
            XmlNode sitModsNode = aiNode.SelectSingleNode("SituationalModifiers");
            if (sitModsNode != null)
            {
                foreach (XmlNode sitNode in sitModsNode.SelectNodes("Situation"))
                {
                    string id = sitNode.Attributes?["id"]?.Value;
                    if (id != null)
                    {
                        AiSituationalMods[id] = new AiSituationalModifier
                        {
                            Fief = ReadFloatVal(sitNode, "Fief", 1.0f),
                            Volunteer = ReadFloatVal(sitNode, "Volunteer", 1.0f),
                            Mercenary = ReadFloatVal(sitNode, "Mercenary", 1.0f),
                            Threshold = ReadFloatVal(sitNode, "Threshold", 0f)
                        };
                    }
                }
            }

            // EnemyDetection
            XmlNode enemyNode = aiNode.SelectSingleNode("EnemyDetection");
            if (enemyNode != null)
            {
                AiEnemyDetectRadius = ReadFloatVal(enemyNode, "Radius", 50.0f);
                AiEnemyStrengthRatio = ReadFloatVal(enemyNode, "StrengthRatio", 0.5f);
            }

            // RecruitmentLimits
            XmlNode limitsNode = aiNode.SelectSingleNode("RecruitmentLimits");
            if (limitsNode != null)
            {
                AiRecruitPartySizeRatioCap = ReadFloatVal(limitsNode, "PartySizeRatioCap", 0.7f);
                AiRecruitMinGoldWageRatio = ReadFloatVal(limitsNode, "MinGoldWageRatio", 0.3f);
                AiRecruitRandomChance = ReadFloatVal(limitsNode, "RandomChance", 0.8f);
                AiMercenarySearchRadius = ReadFloatVal(limitsNode, "MercenarySearchRadius", 150.0f);
            }
        }

        /// <summary>
        /// 根据文化对象获取 AI 招募兵源偏好权重。未找到文化时返回默认均衡偏好。
        /// </summary>
        public (float Fief, float Volunteer, float Mercenary) GetAiCulturalPreference(CultureObject culture)
        {
            if (culture != null && AiCulturalPrefs.TryGetValue(culture, out var pref))
                return (pref.Fief, pref.Volunteer, pref.Mercenary);

            return (0.4f, 0.4f, 0.2f);
        }

        /// <summary>
        /// 根据情境 id 获取 AI 招募兵源偏好修正。未找到情境时返回中性修正（全 1.0）。
        /// </summary>
        public (float Fief, float Volunteer, float Mercenary) GetAiSituationalModifier(string situationId)
        {
            if (situationId != null && AiSituationalMods.TryGetValue(situationId, out var mod))
                return (mod.Fief, mod.Volunteer, mod.Mercenary);

            return (1.0f, 1.0f, 1.0f);
        }

        /// <summary>
        /// 获取指定情境的阈值参数。仅 broke/critical 等情境有阈值。
        /// </summary>
        public float GetAiSituationalThreshold(string situationId)
        {
            if (situationId != null && AiSituationalMods.TryGetValue(situationId, out var mod))
                return mod.Threshold;
            return 0f;
        }

        /// <summary>
        /// 从当前节点的属性中读取 float 值。
        /// </summary>
        private static float ReadAttr(XmlNode node, string attrName, float defaultVal)
        {
            if (node?.Attributes?[attrName] != null && float.TryParse(node.Attributes[attrName].Value, out var v))
                return v;
            return defaultVal;
        }

        /// <summary>
        /// 从子元素中读取 value 属性的 float 值（扁平 XML 格式，兼容旧配置）。
        /// </summary>
        private static float ReadFloatVal(XmlNode node, string elementName, float defaultVal)
        {
            var el = node.SelectSingleNode(elementName);
            if (el?.Attributes?["value"] != null && float.TryParse(el.Attributes["value"].Value, out var v))
            {
                return v;
            }
            return defaultVal;
        }

        /// <summary>
        /// 从子元素中读取 value 属性的 int 值（扁平 XML 格式，兼容旧配置）。
        /// </summary>
        private static int ReadIntVal(XmlNode node, string elementName, int defaultVal)
        {
            var el = node.SelectSingleNode(elementName);
            if (el?.Attributes?["value"] != null && int.TryParse(el.Attributes["value"].Value, out var v))
            {
                return v;
            }
            return defaultVal;
        }
    }

    /// <summary>
    /// ModConfig 管理器，单例模式。
    /// </summary>
    public class ModConfigManager
    {
        public static readonly ModConfigManager Instance = new();
        private ModConfig _activeConfig;

        private ModConfigManager() { }

        /// <summary>
        /// 注册一个配置到管理器。
        /// </summary>
        /// <param name="config">要注册的配置</param>
        public void RegisterConfig(ModConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId)) return;
            _activeConfig = config;
        }

        /// <summary>
        /// 获取当前活跃的配置。
        /// </summary>
        public ModConfig GetActiveConfig() => _activeConfig;
    }
}