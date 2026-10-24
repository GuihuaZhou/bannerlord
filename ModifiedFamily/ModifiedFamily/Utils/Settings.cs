using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using ModifiedFamily.Tool;
using TaleWorlds.Localization;

namespace ModifiedFamily.Utils
{
    public sealed class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "ModifiedFamilySettings";
        public override string DisplayName => new TextObject("{=MF_Settings_DisplayName}Modified Family").ToString();
        public override string FolderName => "ModifiedFamily";
        public override string FormatType => "json2";

        [SettingPropertyGroup("{=MF_Logging_Group}Logging", GroupOrder = 0)]
        [SettingPropertyDropdown("{=MF_MinLogLevel}Screen log level", Order = 0, RequireRestart = false,
            HintText = "{=MF_MinLogLevel_Desc}Messages at or above this level are shown in the campaign feed.")]
        public Dropdown<LogLevel> MinLogLevel { get; set; } = new Dropdown<LogLevel>(
            new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Notice, LogLevel.Warn, LogLevel.Error, LogLevel.Disabled }, 2);

        [SettingPropertyGroup("{=MF_Logging_Group}Logging", GroupOrder = 0)]
        [SettingPropertyDropdown("{=MF_FileMinLogLevel}File log level", Order = 1, RequireRestart = false,
            HintText = "{=MF_FileMinLogLevel_Desc}Messages at or above this level are written to ModifiedFamily.log.")]
        public Dropdown<LogLevel> FileMinLogLevel { get; set; } = new Dropdown<LogLevel>(
            new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Notice, LogLevel.Warn, LogLevel.Error, LogLevel.Disabled }, 1);
    }
}
