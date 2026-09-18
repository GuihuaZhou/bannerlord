using System.Collections.Generic;
using System.Globalization;
using ModifiedPolitics.Models.WarDisposition.Calculation;
using ModifiedPolitics.Models.WarDisposition;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedPolitics.UI.KingdomClan
{
    public partial class KingdomClanItemVMMixin
    {
        private string _warDispositionText;
        private string _warDispositionTraitsText;
        private string _warDurationText;
        private string _dailyReturnAmountText;
        private string _todayBattleInfluenceText;
        private string _todayTerritoryInfluenceText;
        private string _todayWarGainInfluenceText;
        private string _weeklyWealthChangeText;

        [DataSourceProperty]
        public string WarDispositionText
        {
            get => _warDispositionText;
            set => SetWarDispositionProperty(ref _warDispositionText, value, nameof(WarDispositionText));
        }

        [DataSourceProperty]
        public string WarDispositionTraitsText
        {
            get => _warDispositionTraitsText;
            set => SetWarDispositionProperty(ref _warDispositionTraitsText, value, nameof(WarDispositionTraitsText));
        }

        [DataSourceProperty]
        public string WarDurationText
        {
            get => _warDurationText;
            set => SetWarDispositionProperty(ref _warDurationText, value, nameof(WarDurationText));
        }

        [DataSourceProperty]
        public string DailyReturnAmountText
        {
            get => _dailyReturnAmountText;
            set => SetWarDispositionProperty(ref _dailyReturnAmountText, value, nameof(DailyReturnAmountText));
        }

        [DataSourceProperty]
        public string TodayBattleInfluenceText
        {
            get => _todayBattleInfluenceText;
            set => SetWarDispositionProperty(ref _todayBattleInfluenceText, value, nameof(TodayBattleInfluenceText));
        }

        [DataSourceProperty]
        public string TodayTerritoryInfluenceText
        {
            get => _todayTerritoryInfluenceText;
            set => SetWarDispositionProperty(ref _todayTerritoryInfluenceText, value, nameof(TodayTerritoryInfluenceText));
        }

        [DataSourceProperty]
        public string TodayWarGainInfluenceText
        {
            get => _todayWarGainInfluenceText;
            set => SetWarDispositionProperty(ref _todayWarGainInfluenceText, value, nameof(TodayWarGainInfluenceText));
        }

        [DataSourceProperty]
        public string WeeklyWealthChangeText
        {
            get => _weeklyWealthChangeText;
            set => SetWarDispositionProperty(ref _weeklyWealthChangeText, value, nameof(WeeklyWealthChangeText));
        }

        private void SetWarDispositionProperty(
            ref string field,
            string value,
            string propertyName)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _vm.OnPropertyChangedWithValue(value, propertyName);
        }

        private void RefreshWarDispositionUiData()
        {
            Clan clan = GetClan();

            // 小家族不参与王国政治态度计算, 也不为其创建持久化数据.
            if (clan?.IsMinorFaction == true)
            {
                WarDispositionText = "不适用";
                WarDispositionTraitsText = "无";
                WarDurationText = "0天";
                DailyReturnAmountText = "0";
                TodayBattleInfluenceText = "0";
                TodayTerritoryInfluenceText = "0";
                TodayWarGainInfluenceText = "0";
                WeeklyWealthChangeText = "0%(0)";
                return;
            }

            WarDispositionManager manager = Campaign.Current?
                .GetCampaignBehavior<WarDispositionManager>();

            WarDispositionData data = manager?.GetOrCreateData(clan);

            WarDispositionTraitsText = BuildWarDispositionTraitsText(clan?.Leader);
            float warDuration = manager != null
                ? manager.GetLongestActiveWarDurationDays(clan)
                : WarDispositionDailyReturnCalculator
                    .GetLongestActiveWarDurationDays(clan);
            float dailyReturnAmount = WarDispositionDailyReturnCalculator
                .GetReturnAmount(warDuration);

            WarDurationText = ((int)warDuration).ToString(
                CultureInfo.InvariantCulture) + "天";
            DailyReturnAmountText = dailyReturnAmount.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

            if (data == null)
            {
                WarDispositionText = "中立(0)";
                TodayBattleInfluenceText = "0";
                TodayTerritoryInfluenceText = "0";
                TodayWarGainInfluenceText = "0";
                WeeklyWealthChangeText = "0%(0)";
                return;
            }

            WarDispositionText = FormatWarDisposition(data.Value);
            TodayBattleInfluenceText = FormatSignedValue(data.TodayBattleInfluence);
            TodayTerritoryInfluenceText = FormatSignedValue(data.TodayTerritoryInfluence);
            TodayWarGainInfluenceText = FormatSignedValue(data.TodayWarGainInfluence);
            WeeklyWealthChangeText =
                FormatSignedValue(data.WeeklyWealthChangePercent)
                + "%("
                + FormatSignedValue(data.WeeklyWealthInfluence)
                + ")";
        }

        private static string FormatWarDisposition(float value)
        {
            WarDispositionLevel level =
                WarDispositionCalculator.GetLevel(value);

            string levelText =
                WarDispositionCalculator.GetLevelText(level);

            return levelText + "(" + FormatSignedValue(value) + ")";
        }

        private static string FormatSignedValue(float value)
        {
            float rounded = (float)System.Math.Round(
                value,
                1,
                System.MidpointRounding.AwayFromZero);

            if (System.Math.Abs(rounded) < 0.05f)
            {
                return "0";
            }

            string format = System.Math.Abs(rounded % 1f) < 0.05f
                ? "0"
                : "0.0";

            return rounded.ToString(
                (rounded > 0f ? "+" : string.Empty) + format,
                CultureInfo.InvariantCulture);
        }

        private static string BuildWarDispositionTraitsText(Hero leader)
        {
            if (leader == null)
            {
                return "无";
            }

            List<string> traits = new List<string>();

            AddTraitText(traits, "勇武", leader.GetTraitLevel(DefaultTraits.Valor));
            AddTraitText(traits, "仁慈", leader.GetTraitLevel(DefaultTraits.Mercy));
            AddTraitText(traits, "荣誉", leader.GetTraitLevel(DefaultTraits.Honor));
            AddTraitText(traits, "慷慨", leader.GetTraitLevel(DefaultTraits.Generosity));
            AddTraitText(traits, "谋算", leader.GetTraitLevel(DefaultTraits.Calculating));

            return traits.Count == 0
                ? "无"
                : string.Join(" / ", traits);
        }

        private static void AddTraitText(
            ICollection<string> traits,
            string name,
            int level)
        {
            if (level == 0)
            {
                return;
            }

            traits.Add(name + (level > 0 ? "+" : string.Empty) + level);
        }
    }
}
