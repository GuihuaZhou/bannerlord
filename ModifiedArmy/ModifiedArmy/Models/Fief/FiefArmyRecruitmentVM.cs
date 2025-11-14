using HarmonyLib;
using Helpers;
using SandBox.GauntletUI.Menu;
using SandBox.View.Map;
using SandBox.View.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Tutorial;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;

namespace ModifiedArmy.Models.Fief
{
    [HarmonyPatch(typeof(RecruitmentVM), "RefreshScreen")]
    public static class RecruitmentVM_RefreshScreen_Patch
    {
        public static bool Prefix(RecruitmentVM __instance)
        {
            // 只处理玩家拥有的定居点（采邑）
            if (Settlement.CurrentSettlement?.OwnerClan != Clan.PlayerClan)
                return true; // 走原版逻辑

            // 清空
            __instance.VolunteerList.Clear();
            __instance.TroopsInCart.Clear();

            // 获取采邑军队
            var fiefManager = Campaign.Current.GetCampaignBehavior<FiefSquadManager>();
            List<CharacterObject> fiefTroops = fiefManager?.GetAllFiefTroopsForSettlement(Settlement.CurrentSettlement) ?? new();

            if (fiefTroops.Count > 0)
            {
                // 使用 MainHero 作为“招募者”（UI 显示需要）
                var owner = Hero.MainHero;

                // 创建 RecruitVolunteerVM，但禁用招募操作
                var vm = new RecruitVolunteerVM(
                    owner,
                    fiefTroops,
                    onRecruit: (v, t) => { /* 禁止招募 */ },
                    onRemoveFromCart: (v, t) => { /* 禁止移除 */ }
                );

                // ⭐ 关键：设为不可招募（只读）
                foreach (var troop in vm.Troops)
                {
                    troop.CanBeRecruited = false;
                }

                __instance.VolunteerList.Add(vm);
            }

            // 更新标题（可选）
            __instance.TitleText = $"采邑军队";
            __instance.OnPropertyChanged(nameof(__instance.TitleText));

            // 更新财富和属性（保持 UI 一致）
            __instance.TotalWealth = Hero.MainHero.Gold;
            __instance.InitialPartySize = PartyBase.MainParty.NumberOfAllMembers;
            //__instance.RefreshPartyProperties();
            //__instance.UpdateRecruitAllProperties(); // 虽然不能招募，但保持按钮状态一致

            return false; // 跳过原版 RefreshScreen
        }
    }

    // 关键：继承原版 View，使字段类型兼容
    //public class FiefArmyRecruitView : MenuRecruitVolunteersView
    //{
    //    // 可以为空，数据由 VM 控制
    //}

    //[HarmonyPatch]
    //public static class MenuViewContext_OnOpenRecruitVolunteers_Patch
    //{
    //    public static MethodBase TargetMethod() =>
    //        AccessTools.Method(typeof(MenuViewContext), "SandBox.View.Menu.IMenuContextHandler.OnOpenRecruitVolunteers");

    //    public static bool Prefix(MenuViewContext __instance)
    //    {
    //        var field = AccessTools.Field(typeof(MenuViewContext), "_menuRecruitVolunteers");
    //        if (field.GetValue(__instance) != null)
    //            return false; // 已存在，跳过

    //        // 创建你的 View（不再是 internal 类型！）
    //        var view = __instance.AddMenuView<GauntletFiefArmyRecruitView>(Array.Empty<object>());
    //        field.SetValue(__instance, view);

    //        return false; // 跳过原版逻辑
    //    }
    //}

    //public class GauntletFiefArmyRecruitView : GauntletMenuRecruitVolunteersView
    //{
    //    // Token: 0x17000038 RID: 56
    //    // (get) Token: 0x060001E9 RID: 489 RVA: 0x0000C135 File Offset: 0x0000A335
    //    public override bool ShouldUpdateMenuAfterRemoved
    //    {
    //        get
    //        {
    //            return true;
    //        }
    //    }

    //    // Token: 0x060001EA RID: 490 RVA: 0x0000C138 File Offset: 0x0000A338
    //    protected override void OnInitialize()
    //    {
    //        base.OnInitialize();
    //        this._dataSource = new RecruitmentVM();
    //        this._dataSource.SetCancelInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Exit"));
    //        this._dataSource.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm"));
    //        this._dataSource.SetResetInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Reset"));
    //        this._dataSource.SetRecruitAllInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("TakeAll"));
    //        this._dataSource.SetGetKeyTextFromKeyIDFunc(new Func<string, TextObject>(Game.Current.GameTextManager.GetHotKeyGameTextFromKeyID));
    //        base.Layer = new GauntletLayer(206, "GauntletLayer", false)
    //        {
    //            Name = "RecuritLayer"
    //        };
    //        this._layerAsGauntletLayer = (base.Layer as GauntletLayer);
    //        base.Layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
    //        base.MenuViewContext.AddLayer(base.Layer);
    //        base.Layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
    //        base.Layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
    //        this._movie = this._layerAsGauntletLayer.LoadMovie("RecruitmentPopup", this._dataSource);
    //        base.Layer.IsFocusLayer = true;
    //        ScreenManager.TrySetFocus(base.Layer);
    //        this._dataSource.RefreshScreen();
    //        this._dataSource.Enabled = true;
    //        Game.Current.EventManager.TriggerEvent<TutorialContextChangedEvent>(new TutorialContextChangedEvent(TutorialContexts.RecruitmentWindow));
    //        MapScreen mapScreen;
    //        if ((mapScreen = (ScreenManager.TopScreen as MapScreen)) != null)
    //        {
    //            mapScreen.SetIsInRecruitment(true);
    //        }
    //    }

    //    // Token: 0x060001EB RID: 491 RVA: 0x0000C2F0 File Offset: 0x0000A4F0
    //    protected override void OnFinalize()
    //    {
    //        base.Layer.IsFocusLayer = false;
    //        ScreenManager.TryLoseFocus(base.Layer);
    //        this._dataSource.OnFinalize();
    //        this._dataSource = null;
    //        this._layerAsGauntletLayer.ReleaseMovie(this._movie);
    //        base.MenuViewContext.RemoveLayer(base.Layer);
    //        this._movie = null;
    //        base.Layer = null;
    //        this._layerAsGauntletLayer = null;
    //        Game.Current.EventManager.TriggerEvent<TutorialContextChangedEvent>(new TutorialContextChangedEvent(TutorialContexts.MapWindow));
    //        MapScreen mapScreen;
    //        if ((mapScreen = (ScreenManager.TopScreen as MapScreen)) != null)
    //        {
    //            mapScreen.SetIsInRecruitment(false);
    //        }
    //        base.OnFinalize();
    //    }

    //    // Token: 0x060001EC RID: 492 RVA: 0x0000C390 File Offset: 0x0000A590
    //    protected override void OnFrameTick(float dt)
    //    {
    //        base.OnFrameTick(dt);
    //        if (base.Layer.Input.IsHotKeyReleased("Exit"))
    //        {
    //            UISoundsHelper.PlayUISound("event:/ui/default");
    //            this._dataSource.ExecuteForceQuit();
    //        }
    //        else if (base.Layer.Input.IsHotKeyReleased("Confirm"))
    //        {
    //            UISoundsHelper.PlayUISound("event:/ui/default");
    //            this._dataSource.ExecuteDone();
    //        }
    //        else if (base.Layer.Input.IsHotKeyReleased("Reset"))
    //        {
    //            UISoundsHelper.PlayUISound("event:/ui/default");
    //            this._dataSource.ExecuteReset();
    //        }
    //        else if (base.Layer.Input.IsHotKeyReleased("TakeAll"))
    //        {
    //            UISoundsHelper.PlayUISound("event:/ui/default");
    //            this._dataSource.ExecuteRecruitAll();
    //        }
    //        else if (base.Layer.Input.IsGameKeyReleased(39))
    //        {
    //            if (this._dataSource.FocusedVolunteerOwner != null)
    //            {
    //                this._dataSource.FocusedVolunteerOwner.ExecuteOpenEncyclopedia();
    //            }
    //            else if (this._dataSource.FocusedVolunteerTroop != null)
    //            {
    //                this._dataSource.FocusedVolunteerTroop.ExecuteOpenEncyclopedia();
    //            }
    //        }
    //        if (!this._dataSource.Enabled)
    //        {
    //            base.MenuViewContext.CloseRecruitVolunteers();
    //        }
    //    }

    //    // Token: 0x060001ED RID: 493 RVA: 0x0000C4CA File Offset: 0x0000A6CA
    //    protected override TutorialContexts GetTutorialContext()
    //    {
    //        return TutorialContexts.RecruitmentWindow;
    //    }

    //    // Token: 0x0400009E RID: 158
    //    private GauntletLayer _layerAsGauntletLayer;

    //    // Token: 0x0400009F RID: 159
    //    private RecruitmentVM _dataSource;

    //    // Token: 0x040000A0 RID: 160
    //    private GauntletMovieIdentifier _movie;
    //}

    //public class FiefArmyRecruitmentVM : RecruitmentVM
    //{
    //    public void RefreshScreen()
    //    {
    //        this.VolunteerList.Clear();
    //        this.TroopsInCart.Clear();
    //        int num = 0;
    //        this.InitialPartySize = PartyBase.MainParty.NumberOfAllMembers;
    //        //this.RefreshPartyProperties();
    //        var fiefManager = Campaign.Current.GetCampaignBehavior<FiefSquadManager>();
    //        foreach (Hero hero in Settlement.CurrentSettlement.Notables)
    //        {
    //            if (hero.CanHaveRecruits)
    //            {
    //                MBTextManager.SetTextVariable("INDIVIDUAL_NAME", hero.Name, false);
    //                List<CharacterObject> volunteerTroopsOfHeroForRecruitment = fiefManager?.GetAllFiefTroopsForSettlement(Settlement.CurrentSettlement) ?? new();
    //                RecruitVolunteerVM item = new RecruitVolunteerVM(hero, volunteerTroopsOfHeroForRecruitment, new Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>(this.OnRecruit), new Action<RecruitVolunteerVM, RecruitVolunteerTroopVM>(this.OnRemoveFromCart));
    //                this.VolunteerList.Add(item);
    //                num++;
    //            }
    //        }
    //        this.TotalWealth = Hero.MainHero.Gold;
    //        //this.UpdateRecruitAllProperties();
    //    }
    //}
}
