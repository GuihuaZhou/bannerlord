using System.Collections.Generic;
using System.Globalization;
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
        private string _dailyReturnRateText;
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
        public string DailyReturnRateText
        {
            get => _dailyReturnRateText;
            set => SetWarDispositionProperty(ref _dailyReturnRateText, value, nameof(DailyReturnRateText));
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

            WarDispositionManager manager = Campaign.Current?
                .GetCampaignBehavior<WarDispositionManager>();

            WarDispositionData data = manager?.GetOrCreateData(clan);

            WarDispositionTraitsText = BuildWarDispositionTraitsText(clan?.Leader);
            WarDurationText = "0天";
            DailyReturnRateText = "1%";

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
                + "%（"
                + FormatSignedValue(data.WeeklyWealthInfluence)
                + "）";
        }

        private static string FormatWarDisposition(float value)
        {
            string level = value > 0f
                ? "倾向战争"
                : value < 0f
                    ? "倾向停战"
                    : "中立";

            return level + "(" + FormatSignedValue(value) + ")";
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
