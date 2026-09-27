using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using ModifiedPolitics.Tool;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Utils
{
    /// <summary>
    /// Player-facing settings owned exclusively by ModifiedPolitics.
    /// </summary>
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "ModifiedPoliticsSettings";
        public override string DisplayName => new TextObject(
            "{=MP_Settings_DisplayName}Modified Politics").ToString();
        public override string FolderName => "ModifiedPolitics";
        public override string FormatType => "json2";

        /// <summary>
        /// Controls only messages emitted by ModifiedPolitics. The setting is
        /// read for every message and therefore does not require a restart.
        /// </summary>
        [SettingPropertyGroup(
            "{=MP_Logging_Group}Logging",
            GroupOrder = 0)]
        [SettingPropertyDropdown(
            "{=MP_MinLogLevel}Minimum log level",
            Order = 0,
            RequireRestart = false,
            HintText =
                "{=MP_MinLogLevel_Desc}Only messages at or above this level are displayed. Disabled hides every ModifiedPolitics message.")]
        public Dropdown<LogLevel> MinLogLevel { get; set; } =
            new Dropdown<LogLevel>(
                new[]
                {
                    LogLevel.Debug,
                    LogLevel.Info,
                    LogLevel.Notice,
                    LogLevel.Warn,
                    LogLevel.Error,
                    LogLevel.Disabled
                },
                selectedIndex: 2);

        /// <summary>
        /// Controls the independent persistent ModifiedPolitics log. Keeping
        /// this below the display threshold permits detailed diagnostics
        /// without flooding the in-game message feed.
        /// </summary>
        [SettingPropertyGroup(
            "{=MP_Logging_Group}Logging",
            GroupOrder = 0)]
        [SettingPropertyDropdown(
            "{=MP_FileMinLogLevel}File log level",
            Order = 1,
            RequireRestart = false,
            HintText =
                "{=MP_FileMinLogLevel_Desc}Messages at or above this level are written to the ModifiedPolitics log file. Disabled turns off file logging.")]
        public Dropdown<LogLevel> FileMinLogLevel { get; set; } =
            new Dropdown<LogLevel>(
                new[]
                {
                    LogLevel.Debug,
                    LogLevel.Info,
                    LogLevel.Notice,
                    LogLevel.Warn,
                    LogLevel.Error,
                    LogLevel.Disabled
                },
                selectedIndex: 1);
    }
}
