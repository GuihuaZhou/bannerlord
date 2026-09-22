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
    }
}
