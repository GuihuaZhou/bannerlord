using HarmonyLib;
using Helpers; // HeroHelper 所在命名空间
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

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
    // TODO: 禁止female NPC创建mobile party

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
    
    // =============== 补丁3：扩展 CultureObject 读取 troop + 权重 ===============
    [HarmonyPatch(typeof(CultureObject), "Deserialize")]
    public class Patch_CultureObject_Deserialize
    {
        // Troop 字典
        public static Dictionary<CultureObject, CharacterObject> InfantryTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> RangedTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> CavalryTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> TwohandedTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> HorseArcherTroop = new Dictionary<CultureObject, CharacterObject>();

        // 权重字典（默认 0）
        public static Dictionary<CultureObject, int> InfantryWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> RangedWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> CavalryWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> TwohandedWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> HorseArcherWeight = new Dictionary<CultureObject, int>();

        public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
        {
            var culture = __instance;

            // 清理
            InfantryTroop.Remove(culture); RangedTroop.Remove(culture);
            CavalryTroop.Remove(culture); TwohandedTroop.Remove(culture);
            HorseArcherTroop.Remove(culture);

            InfantryWeight.Remove(culture); RangedWeight.Remove(culture);
            CavalryWeight.Remove(culture); TwohandedWeight.Remove(culture);
            HorseArcherWeight.Remove(culture);

            // 读取 troop
            var inf = objectManager.ReadObjectReferenceFromXml<CharacterObject>("infantry_basic_troop", node);
            var ran = objectManager.ReadObjectReferenceFromXml<CharacterObject>("ranged_basic_troop", node);
            var cav = objectManager.ReadObjectReferenceFromXml<CharacterObject>("cavalry_basic_troop", node);
            var two = objectManager.ReadObjectReferenceFromXml<CharacterObject>("twohanded_basic_troop", node);
            var har = objectManager.ReadObjectReferenceFromXml<CharacterObject>("horseArcher_basic_troop", node);
            var spr = objectManager.ReadObjectReferenceFromXml<CharacterObject>("spearman_basic_troop", node);

            if (inf != null) InfantryTroop[culture] = inf;
            if (ran != null) RangedTroop[culture] = ran;
            if (cav != null) CavalryTroop[culture] = cav;
            if (two != null) TwohandedTroop[culture] = two;
            if (har != null) HorseArcherTroop[culture] = har;

            // 读取权重（默认 0）
            InfantryWeight[culture] = ReadIntAttr(node, "infantry_basic_troop_weight", 0);
            RangedWeight[culture] = ReadIntAttr(node, "ranged_basic_troop_weight", 0);
            CavalryWeight[culture] = ReadIntAttr(node, "cavalry_basic_troop_weight", 0);
            TwohandedWeight[culture] = ReadIntAttr(node, "twohanded_basic_troop_weight", 0);
            HorseArcherWeight[culture] = ReadIntAttr(node, "horseArcher_basic_troop_weight", 0);
        }

        private static int ReadIntAttr(XmlNode node, string attrName, int defaultValue)
        {
            var attr = node.Attributes?[attrName];
            if (attr != null && int.TryParse(attr.Value, out int result))
                return Math.Max(0, result);
            return defaultValue;
        }
    }

    // =============== 补丁4：GetBasicVolunteer 按权重选择 ===============
    [HarmonyPatch(typeof(DefaultVolunteerModel), "GetBasicVolunteer")]
    public class Patch_GetBasicVolunteer
    {
        private static void TryAddTroop(
            Dictionary<CultureObject, CharacterObject> troopDict,
            Dictionary<CultureObject, int> weightDict,
            CultureObject culture,
            int defaultWeight,
            List<CharacterObject> troops,
            List<int> weights)
        {
            if (!troopDict.TryGetValue(culture, out CharacterObject troop) || troop == null)
                return;
            int weight = defaultWeight;
            weightDict.TryGetValue(culture, out weight);
            if (weight > 0)
            {
                troops.Add(troop);
                weights.Add(weight);
            }
        }

        private static CharacterObject WeightedRandomSelect(List<CharacterObject> troops, List<int> weights)
        {
            if (troops.Count == 0) return null;

            int total = 0;
            foreach (int w in weights) total += w;
            if (total <= 0) return troops[0];

            int rand = MBRandom.RandomInt(total);
            int sum = 0;
            for (int i = 0; i < troops.Count; i++)
            {
                sum += weights[i];
                if (rand < sum)
                    return troops[i];
            }
            return troops[troops.Count - 1];
        }

        public static bool Prefix(Hero sellerHero, ref CharacterObject __result)
        {
            if (!CampaignState.IsReady || sellerHero?.Culture == null || sellerHero.CurrentSettlement == null)
                return true;

            var culture = sellerHero.Culture;
            var settlement = sellerHero.CurrentSettlement;

            List<CharacterObject> candidates = new List<CharacterObject>();
            List<int> weights = new List<int>();

            if (settlement.IsTown || (settlement.IsVillage && settlement.Village.Bound.IsTown))
            {
                // Town / Village→Town: infantry, ranged, axe + basic_troop (weight=100)
                TryAddTroop(Patch_CultureObject_Deserialize.InfantryTroop,
                            Patch_CultureObject_Deserialize.InfantryWeight,
                            culture, 0, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.RangedTroop,
                            Patch_CultureObject_Deserialize.RangedWeight,
                            culture, 0, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.TwohandedTroop,
                            Patch_CultureObject_Deserialize.TwohandedWeight,
                            culture, 0, candidates, weights);

                // basic_troop always included (default weight=100)
                if (culture.BasicTroop != null)
                {
                    candidates.Add(culture.BasicTroop);
                    weights.Add(100);
                }
            }
            else if (settlement.IsVillage && settlement.Village.Bound.IsCastle)
            {
                // Village→Castle: cavalry, horseArcher + elite_basic_troop (fallback to basic, weight=100)
                TryAddTroop(Patch_CultureObject_Deserialize.CavalryTroop,
                            Patch_CultureObject_Deserialize.CavalryWeight,
                            culture, 0, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.HorseArcherTroop,
                            Patch_CultureObject_Deserialize.HorseArcherWeight,
                            culture, 0, candidates, weights);

                var elite = culture.EliteBasicTroop ?? culture.BasicTroop;
                if (elite != null)
                {
                    candidates.Add(elite);
                    weights.Add(25);
                }
            }
            else
            {
                __result = culture.BasicTroop;
                return false;
            }

            if (candidates.Count == 0)
            {
                __result = culture.BasicTroop;
            }
            else
            {
                __result = WeightedRandomSelect(candidates, weights);
                if (__result == null) __result = culture.BasicTroop;
            }

            return false;
        }
    }
}
