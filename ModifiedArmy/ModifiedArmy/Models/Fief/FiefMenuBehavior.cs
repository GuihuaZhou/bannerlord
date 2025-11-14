using SandBox.View.Menu;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// 负责在村庄、城镇、城堡菜单中添加“采邑”按钮及其子菜单的 CampaignBehavior。
    /// 仅当访问定居点的 Hero 所属的 Clan 拥有该定居点时，才显示“采邑”按钮。
    /// </summary>
    public class FiefMenuBehavior : CampaignBehaviorBase
    {
        // 定义菜单和选项的 ID，方便管理
        private const string VILLAGE_MENU_ID = "village";
        private const string TOWN_MENU_ID = "town";
        private const string CASTLE_MENU_ID = "castle";
        private const string FIEF_MENU_ID = "modified_army_fief_menu";
        private const string FIEF_OPTION_ID = "modified_army_settlement_fief"; // 通用ID
        private const string RECRUIT_FIEF_OPTION_ID = "fief_recruit";
        private const string DISBAND_FIEF_OPTION_ID = "fief_disband";
        private const string MANAGE_FIEF_OPTION_ID = "fief_manage";
        private const string LEAVE_FIEF_OPTION_ID = "fief_return";

        public override void RegisterEvents()
        {
            // 在会话启动时注册菜单
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 此行为不涉及存档同步
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddGameMenus(starter);
        }

        private void AddGameMenus(CampaignGameStarter starter)
        {
            // 1. 定义一个通用的条件委托，用于检查 Hero 的 Clan 是否拥有当前 Settlement
            // 这个委托可以复用于 village, town, castle
            GameMenuOption.OnConditionDelegate fiefCondition = (args) =>
            {
                // 获取当前访问的 Settlement (村庄、城镇或城堡)
                Settlement currentSettlement = Settlement.CurrentSettlement; 
                // 获取当前访问 Settlement 的 Hero (玩家控制的 Party 的 Leader)
                Hero visitingHero = Hero.MainHero; // 通常 MainHero 就是访问 Settlement 的 Hero
                if (currentSettlement != null && visitingHero != null)
                {
                    // 检查 Hero 的 Clan 是否是 Settlement 的 OwnerClan
                    // 注意: 在某些情况下 (如围攻), OwnerClan 可能是入侵者，需要更复杂的判断
                    // 这里简单使用 OwnerClan 进行判断
                    args.IsEnabled = visitingHero.Clan == currentSettlement.OwnerClan;
                    if (args.IsEnabled)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu; // 如果启用，则设置行为类型
                        return true; // 按钮可见且可用
                    }
                }
                args.IsEnabled = false;
                return false; // 按钮不可见/不可用
            };

            // 2. 定义一个通用的后果委托，用于切换到 Fief 子菜单
            GameMenuOption.OnConsequenceDelegate fiefConsequence = (args) =>
            {
                GameMenu.SwitchToMenu(FIEF_MENU_ID); // 切换到“Your Fief”子菜单
            };

            //// 3. 向 "village" 菜单添加 "Fief" 选项 (入口按钮)
            //starter.AddGameMenuOption(
            //    VILLAGE_MENU_ID,
            //    FIEF_OPTION_ID,
            //    "{=ModifiedArmy_FiefMenu_Entry}Fief", // 按钮显示的文本 - 使用本地化键
            //    fiefCondition, // 使用通用条件委托
            //    fiefConsequence, // 使用通用后果委托
            //    isLeave: false,          // 不是离开选项 (因为后果是切换到子菜单)
            //    index: -1,               // 添加到菜单末尾
            //    isRepeatable: false      // 不可重复
            //);

            // 4. 向 "town" 菜单添加 "Fief" 选项 (入口按钮)
            starter.AddGameMenuOption(
                TOWN_MENU_ID,
                FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Entry}Fief", // 按钮显示的文本 - 使用本地化键
                fiefCondition, // 使用通用条件委托
                fiefConsequence, // 使用通用后果委托
                isLeave: false,          // 不是离开选项 (因为后果是切换到子菜单)
                index: -1,               // 添加到菜单末尾
                isRepeatable: false      // 不可重复
            );

            // 5. 向 "castle" 菜单添加 "Fief" 选项 (入口按钮)
            starter.AddGameMenuOption(
                CASTLE_MENU_ID,
                FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Entry}Fief", // 按钮显示的文本 - 使用本地化键
                fiefCondition, // 使用通用条件委托
                fiefConsequence, // 使用通用后果委托
                isLeave: false,          // 不是离开选项 (因为后果是切换到子菜单)
                index: -1,               // 添加到菜单末尾
                isRepeatable: false      // 不可重复
            );

            // 6. 创建 "Your Fief" 子菜单
            starter.AddGameMenu(
                FIEF_MENU_ID, // 新菜单ID
                "{=ModifiedArmy_FiefMenu_Title}Your Fief", // 菜单标题 - 使用本地化键 (string 类型)
                (args) => {
                    // 初始化菜单时的逻辑，例如设置背景或变量
                    // MBTextManager.SetTextVariable("FIEF_NAME", args.MenuContext.ReadFromCache<Village>("Village").Name, false);
                },
                GameMenu.MenuOverlayType.SettlementWithBoth // 使用正确的枚举路径
            );

            // 7. 向 "Your Fief" 子菜单添加 "Recruit Fief Troops" 选项
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                RECRUIT_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Recruit}Recruit Fief Troops", // 直接使用本地化键
                (args) => {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu; // 设置行为类型
                    // 可以在这里添加条件，例如是否满足征召条件
                    return true;
                },
                (args) => {
                    // 实现“征召采邑军队”的逻辑
                    InformationManager.DisplayMessage(new InformationMessage("{=ModifiedArmy_FiefMenu_Recruit_Msg}Recruit Fief Troops feature not yet implemented.")); // 也可以本地化消息
                    // 例如: YourModLogic.RecruitFiefTroops();
                },
                isLeave: false, -1, false // isLeave 为 false
            );

            // 8. 向 "Your Fief" 子菜单添加 "Disband Fief Troops" 选项
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                DISBAND_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Disband}Disband Fief Troops", // 直接使用本地化键
                (args) => {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu; // 设置行为类型
                    // 可以在这里添加条件，例如是否有可解散的单位
                    return true;
                },
                (args) => {
                    // 实现“解散采邑军队”的逻辑
                    InformationManager.DisplayMessage(new InformationMessage("{=ModifiedArmy_FiefMenu_Disband_Msg}Disband Fief Troops feature not yet implemented.")); // 也可以本地化消息
                    // 例如: YourModLogic.DisbandFiefTroops();
                },
                isLeave: false, -1, false // isLeave 为 false
            );

            // 9. 向 "Your Fief" 子菜单添加 "Manage Fief Troops" 选项 (修改了本地化键和文本)
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                MANAGE_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Manage}Manage Fief Troops", // 修改为 "Manage Fief Troops"
                (args) => {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu; // 设置行为类型
                    // 可以在这里添加条件，例如是否有可解散的单位
                    return true;
                },
                (args) =>
                {
                    // 实现“管理采邑军队”的逻辑
                    //InformationManager.DisplayMessage(new InformationMessage("{=ModifiedArmy_FiefMenu_Manage_Msg}Manage Fief Troops feature not yet implemented.")); // 也可以本地化消息
                    // 例如: YourModLogic.DisbandFiefTroops();

                    //args.MenuContext.OpenRecruitVolunteers();
                    //args.MenuContext.SetPanelSound("event:/ui/panels/panel_settlement_enter_recruit");


                    args.MenuContext.OpenRecruitVolunteers();
                    args.MenuContext.SetPanelSound("event:/ui/panels/panel_settlement_enter_recruit");
                },
                isLeave: false, -1, false // isLeave 为 false
            );

            // 10. 向 "Your Fief" 子菜单添加 "Return" 选项 (这是一个 isLeave 选项)
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                LEAVE_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Return}Return", // 直接使用本地化键
                (args) => {
                    // args.optionLeaveType = GameMenuOption.LeaveType.Leave; // 移除这行，使用 isLeave 参数已足够
                    return true;
                },
                (args) => {
                    GameMenu.ExitToLast(); // 修正：返回上一级菜单 (village, town, 或 castle)
                },
                isLeave: true, // 关键：这是离开选项，允许按 Tab 返回
                index: -1, false
            );
        }
    }

}
