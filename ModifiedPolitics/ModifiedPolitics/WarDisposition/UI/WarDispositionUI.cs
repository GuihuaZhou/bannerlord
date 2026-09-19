using System.Collections.Generic;
using System.Globalization;
using ModifiedPolitics.Models.WarDisposition.Calculation;
using ModifiedPolitics.Models.WarDisposition;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

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

        private string _warDispositionLabelText;
        private string _leaderTraitsLabelText;
        private string _warDurationLabelText;
        private string _dailyReturnLabelText;
        private string _todayBattleInfluenceLabelText;
        private string _todayTerritoryInfluenceLabelText;
        private string _todayWarGainLabelText;
        private string _weeklyWealthChangeLabelText;

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

        [DataSourceProperty]
        public string WarDispositionLabelText
        {
            get => _warDispositionLabelText;
            set => SetWarDispositionProperty(
                ref _warDispositionLabelText,
                value,
                nameof(WarDispositionLabelText));
        }

        [DataSourceProperty]
        public string LeaderTraitsLabelText
        {
            get => _leaderTraitsLabelText;
            set => SetWarDispositionProperty(
                ref _leaderTraitsLabelText,
                value,
                nameof(LeaderTraitsLabelText));
        }

        [DataSourceProperty]
        public string WarDurationLabelText
        {
            get => _warDurationLabelText;
            set => SetWarDispositionProperty(
                ref _warDurationLabelText,
                value,
                nameof(WarDurationLabelText));
        }

        [DataSourceProperty]
        public string DailyReturnLabelText
        {
            get => _dailyReturnLabelText;
            set => SetWarDispositionProperty(
                ref _dailyReturnLabelText,
                value,
                nameof(DailyReturnLabelText));
        }

        [DataSourceProperty]
        public string TodayBattleInfluenceLabelText
        {
            get => _todayBattleInfluenceLabelText;
            set => SetWarDispositionProperty(
                ref _todayBattleInfluenceLabelText,
                value,
                nameof(TodayBattleInfluenceLabelText));
        }

        [DataSourceProperty]
        public string TodayTerritoryInfluenceLabelText
        {
            get => _todayTerritoryInfluenceLabelText;
            set => SetWarDispositionProperty(
                ref _todayTerritoryInfluenceLabelText,
                value,
                nameof(TodayTerritoryInfluenceLabelText));
        }

        [DataSourceProperty]
        public string TodayWarGainLabelText
        {
            get => _todayWarGainLabelText;
            set => SetWarDispositionProperty(
                ref _todayWarGainLabelText,
                value,
                nameof(TodayWarGainLabelText));
        }

        [DataSourceProperty]
        public string WeeklyWealthChangeLabelText
        {
            get => _weeklyWealthChangeLabelText;
            set => SetWarDispositionProperty(
                ref _weeklyWealthChangeLabelText,
                value,
                nameof(WeeklyWealthChangeLabelText));
        }

        private void RefreshWarDispositionLocalization()
        {
            WarDispositionLabelText =
                new TextObject("{=ModifiedPolitics_WarDisposition}War Disposition").ToString();

            LeaderTraitsLabelText =
                new TextObject("{=ModifiedPolitics_LeaderTraits}Leader Traits").ToString();

            WarDurationLabelText =
                new TextObject("{=ModifiedPolitics_WarDuration}War Duration").ToString();

            DailyReturnLabelText =
                new TextObject("{=ModifiedPolitics_DailyReturn}Daily Return").ToString();

            TodayBattleInfluenceLabelText =
                new TextObject("{=ModifiedPolitics_TodayBattleInfluence}Today's Battle Influence").ToString();

            TodayTerritoryInfluenceLabelText =
                new TextObject("{=ModifiedPolitics_TodayTerritoryInfluence}Today's Territory Influence").ToString();

            TodayWarGainLabelText =
                new TextObject("{=ModifiedPolitics_TodayWarGain}Today's War Gain").ToString();

            WeeklyWealthChangeLabelText =
                new TextObject("{=ModifiedPolitics_WeeklyWealthChange}Weekly Wealth Change").ToString();
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
            if (clan?.IsMinorFaction == true && clan != Clan.PlayerClan)
            {
                WarDispositionText = Localize("{=ModifiedPolitics_NotApplicable}Not Applicable");
                WarDispositionTraitsText = Localize("{=ModifiedPolitics_None}None");
                WarDurationText = FormatDays(0);
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

            WarDurationText = FormatDays((int)warDuration);
            DailyReturnAmountText = dailyReturnAmount.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

            if (data == null)
            {
                WarDispositionText =
                    Localize("{=ModifiedPolitics_Neutral}Neutral") + "(0)";
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
                return Localize("{=ModifiedPolitics_None}None");
            }

            List<string> traits = new List<string>();

            AddTraitText(traits, Localize("{=ModifiedPolitics_TraitValor}Valor"), leader.GetTraitLevel(DefaultTraits.Valor));
            AddTraitText(traits, Localize("{=ModifiedPolitics_TraitMercy}Mercy"), leader.GetTraitLevel(DefaultTraits.Mercy));
            AddTraitText(traits, Localize("{=ModifiedPolitics_TraitHonor}Honor"), leader.GetTraitLevel(DefaultTraits.Honor));
            AddTraitText(traits, Localize("{=ModifiedPolitics_TraitGenerosity}Generosity"), leader.GetTraitLevel(DefaultTraits.Generosity));
            AddTraitText(traits, Localize("{=ModifiedPolitics_TraitCalculating}Calculating"), leader.GetTraitLevel(DefaultTraits.Calculating));

            return traits.Count == 0
                ? Localize("{=ModifiedPolitics_None}None")
                : string.Join(" / ", traits);
        }

        private static string FormatDays(int days)
        {
            TextObject text = new TextObject(
                "{=ModifiedPolitics_Days}{DAYS} days");
            text.SetTextVariable("DAYS", days);
            return text.ToString();
        }

        private static string Localize(string taggedText)
        {
            return new TextObject(taggedText).ToString();
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
