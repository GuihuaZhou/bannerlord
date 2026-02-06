using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using ModifiedArmy.common;
using ModifiedArmy.Tool;

namespace ModifiedArmy.Utils
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "ModifiedArmySettings";
        public override string DisplayName => "ModifiedArmy Settings";
        public override string FolderName => "Modified Army";
        public override string FormatType => "json2";


        // ========== Prisoner Escape Settings ==========
        [SettingPropertyGroup("{=MA_Prisoner_Group}Prisoner Escape", GroupOrder = 0)]
        [SettingPropertyFloatingInteger(
            "{=MA_Prisoner_EscapeChance_PlayerSettlement}Escape Chance for Prisoners in Player-Owned Settlements",
            0.0f, 1.0f,
            Order = 0,
            RequireRestart = false,
            HintText = "{=MA_Prisoner_EscapeChance_PlayerSettlement_Desc}Chance per day for prisoners to escape from player-owned settlements (towns/castles). Set to 0 to disable.")]
        public float PlayerSettlementPrisonerEscapeChance { get; set; } = 0.01f;


        // ========== Recruitment Cost Settings ==========
        [SettingPropertyGroup("{=MA_RecruitmentCost_Group}Recruitment Costs", GroupOrder = 1)]
        [SettingPropertyInteger(
            "{=MA_CastleProsperityCostPerTier}Castle: Prosperity Cost per Tier",
            0, 10,
            Order = 0,
            RequireRestart = false,
            HintText = "{=MA_CastleProsperityCostPerTier_Desc}Prosperity consumed per soldier tier when recruiting from a castle.")]
        public int CastleProsperityCostPerTier { get; set; } = 4;

        [SettingPropertyGroup("{=MA_RecruitmentCost_Group}Recruitment Costs", GroupOrder = 1)]
        [SettingPropertyInteger(
            "{=MA_TownProsperityCostPerTier}Town: Prosperity Cost per Tier",
            0, 10,
            Order = 1,
            RequireRestart = false,
            HintText = "{=MA_TownProsperityCostPerTier_Desc}Prosperity consumed per soldier tier when recruiting from a town.")]
        public int TownProsperityCostPerTier { get; set; } = 8;

        [SettingPropertyGroup("{=MA_RecruitmentCost_Group}Recruitment Costs", GroupOrder = 1)]
        [SettingPropertyInteger(
            "{=MA_VillageHearthCostPer}Village: Hearth Cost per Recruit",
            0, 20,
            Order = 2,
            RequireRestart = false,
            HintText = "{=MA_VillageHearthCostPer_Desc}Number of hearths consumed per recruit when recruiting from a village.")]
        public int VillageHearthCostPer { get; set; } = 1;


        // ========== Reinforcement Threshold Settings ==========
        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 2)]
        [SettingPropertyFloatingInteger(
            "{=MA_CastleMaxReinforcementProsperityThreshold}Castle: Prosperity Required for Max Reinforcement Efficiency",
            500f, 2000f,
            Order = 0,
            RequireRestart = false,
            HintText = "{=MA_CastleMaxReinforcementProsperityThreshold_Desc}The prosperity level a castle needs to reach 100% weekly reinforcement efficiency. Lower values make it easier to achieve full recruitment potential.")]
        public float CastleMaxReinforcementProsperityThreshold { get; set; } = 2000f;

        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 2)]
        [SettingPropertyFloatingInteger(
            "{=MA_TownMaxReinforcementProsperityThreshold}Town: Prosperity Required for Max Reinforcement Efficiency",
            2000f, 20000f,
            Order = 1,
            RequireRestart = false,
            HintText = "{=MA_TownMaxReinforcementProsperityThreshold_Desc}The prosperity level a town needs to reach 100% weekly reinforcement efficiency. Lower values make it easier to achieve full recruitment potential.")]
        public float TownMaxReinforcementProsperityThreshold { get; set; } = 12000f;

        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 2)]
        [SettingPropertyFloatingInteger(
            "{=MA_VillageMaxReinforcementHearthThreshold}Village: Hearths Required per Village for Max Reinforcement Efficiency",
            200f, 1500f,
            Order = 2,
            RequireRestart = false,
            HintText = "{=MA_VillageMaxReinforcementHearthThreshold_Desc}The number of hearths each bound village must contribute to reach 100% reinforcement efficiency from villages. Total efficiency is calculated based on the average hearths per village.")]
        public float VillageMaxReinforcementHearthThreshold { get; set; } = 900f;

        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 3)]
        [SettingPropertyFloatingInteger(
            "{=MA_CastleMinProsperityThreshold}Castle: Minimum Prosperity for Reinforcement",
            0f, 1000f,
            Order = 3,
            RequireRestart = false,
            HintText = "{=MA_CastleMinProsperityThreshold_Desc}Minimum prosperity a castle must have to enable any reinforcement. Below this, no troops will be recruited.")]
        public float CastleMinProsperityThreshold { get; set; } = 300f;

        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 3)]
        [SettingPropertyFloatingInteger(
            "{=MA_TownMinProsperityThreshold}Town: Minimum Prosperity for Reinforcement",
            0f, 3000f,
            Order = 4,
            RequireRestart = false,
            HintText = "{=MA_TownMinProsperityThreshold_Desc}Minimum prosperity a town must have to enable any reinforcement. Below this, no troops will be recruited.")]
        public float TownMinProsperityThreshold { get; set; } = 1000f;

        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 3)]
        [SettingPropertyFloatingInteger(
            "{=MA_VillageMinHearthThreshold}Village: Minimum Hearths per Village for Contribution",
            0f, 500f,
            Order = 5,
            RequireRestart = false,
            HintText = "{=MA_VillageMinHearthThreshold_Desc}Minimum hearths each bound village must have to contribute to reinforcement efficiency. Villages below this are ignored in the calculation.")]
        public float VillageMinHearthThreshold { get; set; } = 100f;

        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 3)]
        [SettingPropertyFloatingInteger(
            "{=MA_ProsperityWeight}Prosperity Weight in Reinforcement Formula",
            0f, 1f,
            Order = 6,
            RequireRestart = false,
            HintText = "{=MA_ProsperityWeight_Desc}Weight of settlement prosperity in the total reinforcement multiplier (0.0–1.0). The remaining weight is assigned to village hearths.")]
        public float ProsperityWeight { get; set; } = 0.4f;

        [SettingPropertyGroup("{=MA_ReinforcementThreshold_Group}Reinforcement Efficiency Requirements", GroupOrder = 3)]
        [SettingPropertyFloatingInteger(
            "{=MA_HearthsWeight}Village Hearths Weight in Reinforcement Formula",
            0f, 1f,
            Order = 7,
            RequireRestart = false,
            HintText = "{=MA_HearthsWeight_Desc}Weight of village hearths in the total reinforcement multiplier (0.0–1.0). The remaining weight is assigned to settlement prosperity.")]
        public float HearthsWeight { get; set; } = 0.6f;



        [SettingPropertyGroup("{=MA_Fief_Group_Advanced}Advanced Settings", GroupOrder = 4)]
        [SettingPropertyDropdown("{=MA_Fief_MinLogLevel}Minimum Log Level", Order = 0, RequireRestart = false)]
        public Dropdown<LogLevel> MinLogLevel { get; set; } =
            new Dropdown<LogLevel>(
                new LogLevel[] { LogLevel.Debug, LogLevel.Info, LogLevel.Notice, LogLevel.Warn, LogLevel.Error },
                selectedIndex: 1 // Info
            );

        
    }
}
