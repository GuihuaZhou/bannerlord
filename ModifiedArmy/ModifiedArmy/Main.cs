using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Helpers; // HeroHelper 所在命名空间

namespace ModifiedArmy
{
    public static class CampaignState
    {
        public static bool IsReady { get; set; } = false;
    }

    public class Main : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            new Harmony("com.mod.ModifiedArmy").PatchAll(Assembly.GetExecutingAssembly());
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (game.GameType is Campaign)
            {
                var campaignStarter = (CampaignGameStarter)gameStarterObject;
                campaignStarter.AddBehavior(new CampaignReadyBehavior());
            }
        }
    }

    public class CampaignReadyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUpEnd);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnNewGameCreatedPartialFollowUpEnd(CampaignGameStarter starter)
        {
            CampaignState.IsReady = true;
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            CampaignState.IsReady = true;
        }
    }

    // =============== 补丁 1: 禁止女性 NPC 创建 Army ===============
    [HarmonyPatch(typeof(Kingdom), "CreateArmy")]
    public class Patch_CreateArmy
    {
        public static bool Prefix(Hero armyLeader)
        {
            if (!CampaignState.IsReady) return true;

            if (armyLeader != null && armyLeader != Hero.MainHero && armyLeader.IsFemale)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[ModifiedArmy] Blocked female lord {armyLeader.Name} from creating an army!",
                    Color.FromUint(0xFFFF5555)
                ));
                return false;
            }
            return true;
        }
    }

    // =============== 补丁 2: 限制志愿兵招募（使用 Clan.Settlements 优化） ===============
    [HarmonyPatch(typeof(HeroHelper), "HeroCanRecruitFromHero")]
    public class Patch_HeroHelper_HeroCanRecruitFromHero
    {
        public static void Postfix(Hero buyerHero, Hero sellerHero, int index, ref bool __result)
        {
            if (!CampaignState.IsReady) return;
            if (!__result) return;

            if (buyerHero == null || sellerHero == null)
                return;

            MobileParty buyerParty = buyerHero.PartyBelongedTo;
            if (buyerParty == null) return;

            Settlement settlement = sellerHero.CurrentSettlement;
            if (settlement == null) return;

            Clan buyerClan = buyerParty.ActualClan;
            if (buyerClan == null) return;

            Clan ownerClan = settlement.OwnerClan;

            bool isPlayerClan = (buyerClan == Clan.PlayerClan);

            if (isPlayerClan)
            {
                // 高效判断：使用 Clan.Settlements
                bool playerClanHasAnySettlement = Clan.PlayerClan.Settlements.Count > 0;

                if (playerClanHasAnySettlement)
                {
                    // 有领地：必须是本 Clan 领地
                    if (ownerClan != Clan.PlayerClan)
                    {
                        __result = false;
                    }
                }
                // 无领地：允许招募（__result 保持 true）
            }
            else
            {
                // 非玩家 Clan：必须是自己 Clan 领地
                if (ownerClan != buyerClan)
                {
                    __result = false;
                }
            }
        }
    }
}
