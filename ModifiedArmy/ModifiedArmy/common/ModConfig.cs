using ModifiedArmy.Tool;
using System;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.common
{
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

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            MinLogLevel = XmlHelper.ReadInt(node, "MinLogLevel");
            if (MinLogLevel < 0 || MinLogLevel > 4) MinLogLevel = 2;

            TownPoorThreshold = ReadFloatVal(node, "TownPoorThreshold", 1000f);
            TownAverageThreshold = ReadFloatVal(node, "TownAverageThreshold", 4000f);
            TownRichThreshold = ReadFloatVal(node, "TownRichThreshold", 8000f);
            TownVeryRichThreshold = ReadFloatVal(node, "TownVeryRichThreshold", 12000f);

            CastlePoorThreshold = ReadFloatVal(node, "CastlePoorThreshold", 300f);
            CastleAverageThreshold = ReadFloatVal(node, "CastleAverageThreshold", 800f);
            CastleRichThreshold = ReadFloatVal(node, "CastleRichThreshold", 1500f);
            CastleVeryRichThreshold = ReadFloatVal(node, "CastleVeryRichThreshold", 2000f);

            ProsperityWeight = ReadFloatVal(node, "ProsperityWeight", 0.4f);
            HearthsWeight = ReadFloatVal(node, "HearthsWeight", 0.6f);

            VillageMinHearthThreshold = ReadFloatVal(node, "VillageMinHearthThreshold", 100f);
            VillageMaxReinforcementHearthThreshold = ReadFloatVal(node, "VillageMaxReinforcementHearthThreshold", 900f);

            FiefWageExemptionDays = XmlHelper.ReadInt(node, "FiefWageExemptionDays");
            if (FiefWageExemptionDays == 0) FiefWageExemptionDays = 28;

            AiFiefProsperityImpactMultiplier = ReadFloatVal(node, "AiFiefProsperityImpactMultiplier", 0.1f);
            AiFiefHearthImpactMultiplier = ReadFloatVal(node, "AiFiefHearthImpactMultiplier", 0.1f);

            VlandiaVolunteerMultiplier = ReadFloatVal(node, "VlandiaVolunteerMultiplier", 0.2f);
            EmpireVolunteerMultiplier = ReadFloatVal(node, "EmpireVolunteerMultiplier", 0.6f);
            AseraiVolunteerMultiplier = ReadFloatVal(node, "AseraiVolunteerMultiplier", 0.7f);

            PlayerSettlementPrisonerEscapeChance = ReadFloatVal(node, "PlayerSettlementPrisonerEscapeChance", 0.01f);

            ModConfigManager.Instance.RegisterConfig(this);

            TextObject msg = GameTexts.FindText("str_modifiedarmy_config_loaded");
            msg.SetTextVariable("CONFIG_ID", ConfigId);
            ModLogger.Info(msg.ToString());
        }

        private static float ReadFloatVal(XmlNode node, string elementName, float defaultVal)
        {
            var el = node.SelectSingleNode(elementName);
            if (el?.Attributes?["value"] != null && float.TryParse(el.Attributes["value"].Value, out var v))
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
