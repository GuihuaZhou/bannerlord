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
        private static readonly DecisionPoint[] DefaultCurve =
        {
            new DecisionPoint(-100, 0f, 0f), new DecisionPoint(-50, 0f, .01f),
            new DecisionPoint(0, 0f, .05f), new DecisionPoint(30, .22f, .20f),
            new DecisionPoint(60, .35f, .35f), new DecisionPoint(100, .45f, .45f),
            new DecisionPoint(150, .65f, .30f), new DecisionPoint(200, .80f, .17f),
            new DecisionPoint(250, .90f, .08f), new DecisionPoint(300, .95f, .04f)
        };

        public readonly struct DecisionPoint
        {
            public readonly float Score, Accept, Counter;
            public DecisionPoint(float score, float accept, float counter)
            { Score = score; Accept = accept; Counter = counter; }
        }

        public static MarriageAiConfig Current { get; private set; } =
            CreateDefault();

        public int ProposalCheckIntervalDays { get; private set; }
        public float BaseProposalChance { get; private set; }
        public int ClanProposalCooldownDays { get; private set; }
        public IReadOnlyList<DecisionPoint> DecisionCurve { get; private set; }
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
                config.AiMaxGiftGoldRatio = Probability(root,
                    "AiMaxGiftGoldRatio", config.AiMaxGiftGoldRatio);
                config.GiftSteps = GiftValues(root, config.GiftSteps);
                XElement curve = root.Element("DecisionCurve");
                if (curve == null)
                    FamilyDiagnostics.Warn("Marriage AI decision curve is missing; built-in defaults are active.");
                else
                {
                    DecisionPoint[] points = ParseCurve(curve);
                    if (points == null)
                        FamilyDiagnostics.Warn("Marriage AI decision curve is invalid; built-in defaults are active.");
                    else config.DecisionCurve = points;
                }
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
                DecisionCurve = DefaultCurve,
                GiftSteps = DefaultGiftSteps,
                AiMaxGiftGoldRatio = 0.30f
            };

        private static DecisionPoint[] ParseCurve(XElement curve)
        {
            var points = new List<DecisionPoint>();
            foreach (XElement element in curve.Elements())
            {
                if (element.Name != "Point" ||
                    !float.TryParse((string)element.Attribute("score"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float score) ||
                    !float.TryParse((string)element.Attribute("accept"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float accept) ||
                    !float.TryParse((string)element.Attribute("counter"), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out float counter) ||
                    float.IsNaN(score) || float.IsInfinity(score) ||
                    !ValidProbability(accept) || !ValidProbability(counter) ||
                    accept + counter > 1f ||
                    points.Count > 0 && score <= points[points.Count - 1].Score)
                    return null;
                points.Add(new DecisionPoint(score, accept, counter));
            }
            return points.Count >= 2 && points[0].Score <= -100f &&
                points[points.Count - 1].Score >= 300f ? points.ToArray() : null;
        }

        private static bool ValidProbability(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

        private static int PositiveInt(XElement root, string name,
            int fallback) => int.TryParse(root.Element(name)?.Value,
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int value) && value > 0 ? value : fallback;

        private static float Probability(XElement root, string name,
            float fallback) => float.TryParse(root.Element(name)?.Value,
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out float value) && value >= 0f && value <= 1f
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
            string[] gifts = (root.Element("GiftSteps")?.Value ?? "")
                .Split(',');
            bool validGifts = gifts.Length > 0 && gifts.All(part =>
                int.TryParse(part.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value) && value >= 0);
            return !PositiveInteger("ProposalCheckIntervalDays") ||
                !ProbabilityValue("BaseProposalChance") ||
                !PositiveInteger("ClanProposalCooldownDays") ||
                !ProbabilityValue("AiMaxGiftGoldRatio") || !validGifts;
        }
    }
}
