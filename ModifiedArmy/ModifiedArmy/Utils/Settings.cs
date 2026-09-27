using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using ModifiedArmy.Tool;
using TaleWorlds.Localization;

namespace ModifiedArmy.Utils
{
    /// <summary>
    /// Player-facing settings owned exclusively by ModifiedArmy.
    /// Gameplay balance remains in ModConfigs.xml.
    /// </summary>
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "ModifiedArmySettings";
        public override string DisplayName => new TextObject(
            "{=MA_Settings_DisplayName}Modified Army").ToString();
        public override string FolderName => "ModifiedArmy";
        public override string FormatType => "json2";

        /// <summary>
        /// Controls which ModifiedArmy messages are displayed. The setting is
        /// read for every message, so changing it requires no restart.
        /// </summary>
        [SettingPropertyGroup(
            "{=MA_Logging_Group}Logging",
            GroupOrder = 0)]
        [SettingPropertyDropdown(
            "{=MA_MinLogLevel}Minimum log level",
            Order = 0,
            RequireRestart = false,
            HintText =
                "{=MA_MinLogLevel_Desc}Only messages at or above this level are displayed. Disabled hides every ModifiedArmy message.")]
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
        /// Controls which messages are also persisted to ModifiedArmy's file.
        /// File logging is independent from the less verbose in-game display.
        /// </summary>
        [SettingPropertyGroup(
            "{=MA_Logging_Group}Logging",
            GroupOrder = 0)]
        [SettingPropertyDropdown(
            "{=MA_FileMinLogLevel}File log level",
            Order = 1,
            RequireRestart = false,
            HintText =
                "{=MA_FileMinLogLevel_Desc}Messages at or above this level are written to the ModifiedArmy log file. Disabled turns off file logging.")]
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
