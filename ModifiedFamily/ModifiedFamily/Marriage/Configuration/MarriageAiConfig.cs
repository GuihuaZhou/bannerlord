using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ModifiedFamily.Diagnostics;
using TaleWorlds.ModuleManager;

namespace ModifiedFamily.Marriage.Configuration
{
    public sealed class MarriageAiConfig
    {
        private static readonly int[] DefaultGiftSteps =
            { 30000, 50000, 80000, 100000 };

        public static MarriageAiConfig Current { get; private set; } =
            CreateDefault();

        public int ProposalCheckIntervalDays { get; private set; }
        public float BaseProposalChance { get; private set; }
        public int ClanProposalCooldownDays { get; private set; }
        public float AcceptanceMaxChance { get; private set; }
        public float AcceptanceExponent { get; private set; }
        public IReadOnlyList<int> GiftSteps { get; private set; }
        public float AiMaxGiftGoldRatio { get; private set; }

        public static void Load()
        {
            MarriageAiConfig config = CreateDefault();
            try
            {
                string path = Path.Combine(ModuleHelper.GetModuleFullPath(
                    "ModifiedFamily"), "ModuleData", "MarriageAIConfig.xml");
                XDocument document = XDocument.Load(path);
                XElement root = document.Root ??
                    throw new InvalidDataException("MarriageAI root is missing.");
                bool invalidValues = HasInvalidValues(root);
                config.ProposalCheckIntervalDays = PositiveInt(root,
                    "ProposalCheckIntervalDays",
                    config.ProposalCheckIntervalDays);
                config.BaseProposalChance = Probability(root,
                    "BaseProposalChance", config.BaseProposalChance);
                config.ClanProposalCooldownDays = PositiveInt(root,
                    "ClanProposalCooldownDays",
                    config.ClanProposalCooldownDays);
                config.AcceptanceMaxChance = Probability(root,
                    "AcceptanceMaxChance", config.AcceptanceMaxChance);
                config.AcceptanceExponent = PositiveFloat(root,
                    "AcceptanceExponent", config.AcceptanceExponent);
                config.AiMaxGiftGoldRatio = Probability(root,
                    "AiMaxGiftGoldRatio", config.AiMaxGiftGoldRatio);
                config.GiftSteps = GiftValues(root, config.GiftSteps);
                if (invalidValues)
                    FamilyDiagnostics.Warn(
                        "Marriage AI configuration contains invalid values; " +
                        "defaults were applied to invalid fields.");
            }
            catch (Exception exception)
            {
                FamilyDiagnostics.Warn(
                    "Marriage AI configuration could not be loaded; " +
                    "built-in defaults are active. reason=" +
                    exception.GetType().Name);
            }
            Current = config;
        }

        private static MarriageAiConfig CreateDefault() =>
            new MarriageAiConfig
            {
                ProposalCheckIntervalDays = 7,
                BaseProposalChance = 0.50f,
                ClanProposalCooldownDays = 14,
                AcceptanceMaxChance = 0.50f,
                AcceptanceExponent = 2.0f,
                GiftSteps = DefaultGiftSteps,
                AiMaxGiftGoldRatio = 0.30f
            };

        private static int PositiveInt(XElement root, string name,
            int fallback) => int.TryParse(root.Element(name)?.Value,
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int value) && value > 0 ? value : fallback;

        private static float Probability(XElement root, string name,
            float fallback) => float.TryParse(root.Element(name)?.Value,
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out float value) && value >= 0f && value <= 1f
                    ? value : fallback;

        private static float PositiveFloat(XElement root, string name,
            float fallback) => float.TryParse(root.Element(name)?.Value,
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out float value) && value > 0f &&
                !float.IsInfinity(value) && !float.IsNaN(value)
                    ? value : fallback;

        private static IReadOnlyList<int> GiftValues(XElement root,
            IReadOnlyList<int> fallback)
        {
            var values = new List<int>();
            foreach (string part in (root.Element("GiftSteps")?.Value ?? "")
                .Split(','))
                if (int.TryParse(part.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value) && value >= 0)
                    values.Add(value);
            int[] result = values.Distinct().OrderBy(value => value).ToArray();
            return result.Length == 0 ? fallback : result;
        }

        private static bool HasInvalidValues(XElement root)
        {
            bool PositiveInteger(string name) => int.TryParse(
                root.Element(name)?.Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int value) && value > 0;
            bool ProbabilityValue(string name) => float.TryParse(
                root.Element(name)?.Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float value) &&
                value >= 0f && value <= 1f;
            bool PositiveNumber(string name) => float.TryParse(
                root.Element(name)?.Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float value) && value > 0f &&
                !float.IsNaN(value) && !float.IsInfinity(value);
            string[] gifts = (root.Element("GiftSteps")?.Value ?? "")
                .Split(',');
            bool validGifts = gifts.Length > 0 && gifts.All(part =>
                int.TryParse(part.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value) && value >= 0);
            return !PositiveInteger("ProposalCheckIntervalDays") ||
                !ProbabilityValue("BaseProposalChance") ||
                !PositiveInteger("ClanProposalCooldownDays") ||
                !ProbabilityValue("AcceptanceMaxChance") ||
                !PositiveNumber("AcceptanceExponent") ||
                !ProbabilityValue("AiMaxGiftGoldRatio") || !validGifts;
        }
    }
}
