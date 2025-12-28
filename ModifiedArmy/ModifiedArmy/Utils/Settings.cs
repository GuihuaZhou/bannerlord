using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using ModifiedArmy.Tool;

namespace ModifiedArmy.Utils
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "ModifiedArmySettings";
        public override string DisplayName => "ModifiedArmy Settings";
        public override string FolderName => "Modified Army";
        public override string FormatType => "json2";

        // ========== Base Replenishment ==========
        [SettingPropertyGroup("{=FiefArmy_Group_Basic}Basic Settings", GroupOrder = 0)]
        [SettingPropertyInteger("{=FiefArmy_BaseReinforcements}Base Weekly Reinforcements", 0, 20,
            Order = 0, RequireRestart = false,
            HintText = "{=FiefArmy_BaseReinforcements_Desc}Base number of troops automatically added to garrison each week.")]
        public int BaseReinforcements { get; set; } = 20;

        [SettingPropertyGroup("{=FiefArmy_Group_Basic}Basic Settings", GroupOrder = 0)]
        [SettingPropertyInteger("{=FiefArmy_MaxReinforcements}Maximum Weekly Reinforcements", 20, 50,
            Order = 1, RequireRestart = false,
            HintText = "{=FiefArmy_MaxReinforcements_Desc}Hard cap on weekly troop replenishment per settlement.")]
        public int MaxReinforcements { get; set; } = 50;

        // ========== Prosperity Scaling ==========
        [SettingPropertyGroup("{=FiefArmy_Group_Prosperity}Prosperity Scaling", GroupOrder = 1)]
        [SettingPropertyInteger("{=FiefArmy_TownProsperityPer10}Town: Prosperity per +10 Reinforcements", 3000, 10000,
            Order = 0, RequireRestart = false,
            HintText = "{=FiefArmy_TownProsperityPer10_Desc}Prosperity required in towns to gain an additional 10 weekly reinforcements.")]
        public int TownProsperityPer { get; set; } = 4000;

        [SettingPropertyGroup("{=FiefArmy_Group_Prosperity}Prosperity Scaling", GroupOrder = 1)]
        [SettingPropertyInteger("{=FiefArmy_CastleProsperityPer10}Castle: Prosperity per +10 Reinforcements", 500, 3000,
            Order = 1, RequireRestart = false,
            HintText = "{=FiefArmy_CastleProsperityPer10_Desc}Prosperity required in castles to gain an additional 10 weekly reinforcements.")]
        public int CastleProsperityPer { get; set; } = 800;

        [SettingPropertyGroup("{=MA_Fief_Group_Advanced}Advanced Settings", GroupOrder = 2)]
        [SettingPropertyDropdown("{=MA_Fief_MinLogLevel}Minimum Log Level", Order = 0, RequireRestart = false)]
        public Dropdown<LogLevel> MinLogLevel { get; set; } =
            new Dropdown<LogLevel>(
                new LogLevel[] { LogLevel.Debug, LogLevel.Info, LogLevel.Notice, LogLevel.Warn, LogLevel.Error },
                selectedIndex: 1 // Info
            );
    }
}
