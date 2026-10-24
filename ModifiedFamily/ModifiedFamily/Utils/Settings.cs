// Player-facing MCM settings for independent screen and file log thresholds.
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
        public Dropdown<string> MinLogLevel { get; set; } = CreateLogLevelDropdown(2);

        [SettingPropertyGroup("{=MF_Logging_Group}Logging", GroupOrder = 0)]
        [SettingPropertyDropdown("{=MF_FileMinLogLevel}File log level", Order = 1, RequireRestart = false,
            HintText = "{=MF_FileMinLogLevel_Desc}Messages at or above this level are written to ModifiedFamily.log.")]
        public Dropdown<string> FileMinLogLevel { get; set; } = CreateLogLevelDropdown(1);

        private static Dropdown<string> CreateLogLevelDropdown(int selectedIndex)
        {
            // MCM renders dropdown values via ToString, so localized strings are stored as the values.
            return new Dropdown<string>(new[]
            {
                new TextObject("{=MF_LogLevel_Debug}Debug").ToString(),
                new TextObject("{=MF_LogLevel_Info}Information").ToString(),
                new TextObject("{=MF_LogLevel_Notice}Notice").ToString(),
                new TextObject("{=MF_LogLevel_Warn}Warning").ToString(),
                new TextObject("{=MF_LogLevel_Error}Error").ToString(),
                new TextObject("{=MF_LogLevel_Disabled}Disabled").ToString()
            }, selectedIndex);
        }
    }
}
