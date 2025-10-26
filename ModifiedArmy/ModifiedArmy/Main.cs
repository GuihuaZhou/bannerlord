using HarmonyLib;
using Helpers; // HeroHelper 所在命名空间
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
    [HarmonyPatch(typeof(DefaultVolunteerModel), "MaximumIndexHeroCanRecruitFromHero")]
    public class Patch_MaximumIndexHeroCanRecruitFromHero
    {
        public static bool Prefix(
            DefaultVolunteerModel __instance,
            Hero buyerHero,
            Hero sellerHero,
            int useValueAsRelation,
            ref int __result)
        {
            if (!CampaignState.IsReady || buyerHero == null || sellerHero == null)
            {
                __result = -1;
                return false;
            }

            MobileParty buyerParty = buyerHero.PartyBelongedTo;
            Settlement settlement = sellerHero.CurrentSettlement;
            if (settlement == null)
            {
                __result = -1;
                return false;
            }

            Clan buyerClan = buyerParty?.ActualClan;
            Clan ownerClan = settlement.OwnerClan;

            if (buyerClan == null)
            {
                __result = -1;
                return false;
            }

            // ====== 地域限制 ======
            bool isPlayer = (buyerClan == Clan.PlayerClan);
            bool canRecruitFromSettlement = false;

            if (isPlayer)
            {
                bool playerHasSettlement = Clan.PlayerClan.Settlements.Count > 0;
                canRecruitFromSettlement = !playerHasSettlement || (ownerClan == Clan.PlayerClan);
            }
            else
            {
                canRecruitFromSettlement = (ownerClan == buyerClan);
            }

            if (!canRecruitFromSettlement)
            {
                __result = -1;
                return false;
            }

            // ====== 关系限制 ======
            // int relation = (useValueAsRelation < -100) ? buyerHero.GetRelation(sellerHero) : useValueAsRelation;
            int relation = buyerHero.GetRelation(sellerHero);
            if (relation < -10)
            {
                __result = -1;
                return false;
            }

            // ====== 基础招募格子数 ======
            int baseIndex = 2;

            // ====== 原版加成计算 ======
            int relationBonus = (relation >= 100) ? 7 :
                                (relation >= 80) ? 6 :
                                (relation >= 60) ? 5 :
                                (relation >= 40) ? 4 :
                                (relation >= 20) ? 3 :
                                (relation >= 10) ? 2 :
                                (relation >= 5) ? 1 :
                                (relation >= 0) ? 0 : -1;

            int factionBonus = (settlement.MapFaction == buyerHero.MapFaction) ? 1 : 0;
            int npcPenalty = (buyerHero != Hero.MainHero) ? 1 : 0;
            int warPenalty = (buyerHero.MapFaction.IsAtWarWith(settlement.MapFaction)) ? -(1 + npcPenalty) : 0;

            if (buyerHero.IsMinorFactionHero && settlement.IsVillage)
            {
                warPenalty = 0;
            }

            int perkBonus = 0;
            if (sellerHero.IsMerchant && buyerHero.GetPerkValue(DefaultPerks.Trade.ArtisanCommunity))
                perkBonus += (int)DefaultPerks.Trade.ArtisanCommunity.SecondaryBonus;
            if (sellerHero.Culture == buyerHero.Culture && buyerHero.GetPerkValue(DefaultPerks.Leadership.CombatTips))
                perkBonus += (int)DefaultPerks.Leadership.CombatTips.SecondaryBonus;
            if (sellerHero.IsRuralNotable && buyerHero.GetPerkValue(DefaultPerks.Charm.Firebrand))
                perkBonus += (int)DefaultPerks.Charm.Firebrand.SecondaryBonus;
            if (sellerHero.IsUrbanNotable && buyerHero.GetPerkValue(DefaultPerks.Charm.FlexibleEthics))
                perkBonus += (int)DefaultPerks.Charm.FlexibleEthics.SecondaryBonus;
            if (sellerHero.IsArtisan && buyerParty?.EffectiveEngineer?.GetPerkValue(DefaultPerks.Engineering.EngineeringGuilds) == true)
                perkBonus += (int)DefaultPerks.Engineering.EngineeringGuilds.PrimaryBonus;

            int maxIndex = MathF.Min(6, baseIndex + relationBonus + factionBonus + npcPenalty + warPenalty + perkBonus);
            if (maxIndex < 0) maxIndex = -1;

            // ====== 高阶兵限制 ======
            float influence = buyerClan.Influence;
            CharacterObject[] volunteerTypes = sellerHero.VolunteerTypes;

            int finalIndex = -1;
            //string debugLog = $"[RecruitDebug] Buyer: {buyerHero.Name}, Seller: {sellerHero.Name}\n" +
            //                  $"Relation: {relation}, BaseIndex: {baseIndex}, MaxIndex: {maxIndex}, Influence: {influence:F1}\n" +
            //                  $"Volunteer Tiers: ";

            for (int i = 0; i <= maxIndex && i < 6; i++)
            {
                CharacterObject troop = volunteerTypes[i];
                if (troop == null)
                {
                    //debugLog += $"[{i}: null], ";
                    continue;
                }

                int tier = troop.Tier;
                bool allowed = false;
                if (tier <= 3)
                {
                    allowed = true;
                }
                else if (tier == 4 && influence > 200f)
                {
                    allowed = true;
                }
                else if (tier >= 5 && influence > 500f)
                {
                    allowed = true;
                }
                else
                {
                    break;
                }

                if (allowed)
                {
                    finalIndex = i;
                    //debugLog += $"[{i}: T{tier} ✓], ";
                }
                else
                {
                    //debugLog += $"[{i}: T{tier} ✗], ";
                }
            }

            //debugLog += $"\nFinalIndex: {finalIndex}";
            //InformationManager.DisplayMessage(new InformationMessage(debugLog, new Color(1f, 1f, 0f)));

            __result = finalIndex;
            return false;
        }
    }

    // =============== 补丁3：扩展 CultureObject 读取 troop + 权重 ===============
    [HarmonyPatch(typeof(CultureObject), "Deserialize")]
    public class Patch_CultureObject_Deserialize
    {
        // Troop 字段
        public static Dictionary<CultureObject, CharacterObject> InfantryTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> TwoHandedTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> RangedTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> CavalryTroop = new Dictionary<CultureObject, CharacterObject>();
        public static Dictionary<CultureObject, CharacterObject> HorseArcherTroop = new Dictionary<CultureObject, CharacterObject>();

        // 权重字段（默认 0）
        public static Dictionary<CultureObject, int> InfantryWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> TwoHandedWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> RangedWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> CavalryWeight = new Dictionary<CultureObject, int>();
        public static Dictionary<CultureObject, int> HorseArcherWeight = new Dictionary<CultureObject, int>();

        public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
        {
            var culture = __instance;

            // 清理旧值
            InfantryTroop.Remove(culture); TwoHandedTroop.Remove(culture);
            RangedTroop.Remove(culture); CavalryTroop.Remove(culture); HorseArcherTroop.Remove(culture);
            InfantryWeight.Remove(culture); TwoHandedWeight.Remove(culture);
            RangedWeight.Remove(culture); CavalryWeight.Remove(culture); HorseArcherWeight.Remove(culture);

            // 读取 troop
            var inf = objectManager.ReadObjectReferenceFromXml<CharacterObject>("infantry_basic_troop", node);
            var two = objectManager.ReadObjectReferenceFromXml<CharacterObject>("twohanded_basic_troop", node);
            var ran = objectManager.ReadObjectReferenceFromXml<CharacterObject>("ranged_basic_troop", node);
            var cav = objectManager.ReadObjectReferenceFromXml<CharacterObject>("cavalry_basic_troop", node);
            var har = objectManager.ReadObjectReferenceFromXml<CharacterObject>("horseArcher_basic_troop", node);

            if (inf != null) InfantryTroop[culture] = inf;
            if (two != null) TwoHandedTroop[culture] = two;
            if (ran != null) RangedTroop[culture] = ran;
            if (cav != null) CavalryTroop[culture] = cav;
            if (har != null) HorseArcherTroop[culture] = har;

            // 读取权重（默认 0）
            InfantryWeight[culture] = ReadIntAttr(node, "infantry_basic_troop_weight", 0);
            TwoHandedWeight[culture] = ReadIntAttr(node, "twohanded_basic_troop_weight", 0);
            RangedWeight[culture] = ReadIntAttr(node, "ranged_basic_troop_weight", 0);
            CavalryWeight[culture] = ReadIntAttr(node, "cavalry_basic_troop_weight", 0);
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
            List<CharacterObject> troops,
            List<int> weights)
        {
            if (!troopDict.TryGetValue(culture, out CharacterObject troop) || troop == null)
                return;
            int weight = 0;
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
                // Town / Village→Town: infantry, twohanded, ranged + basic_troop (weight=100)
                TryAddTroop(Patch_CultureObject_Deserialize.InfantryTroop,
                            Patch_CultureObject_Deserialize.InfantryWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.TwoHandedTroop,
                            Patch_CultureObject_Deserialize.TwoHandedWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.RangedTroop,
                            Patch_CultureObject_Deserialize.RangedWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.CavalryTroop,
                            Patch_CultureObject_Deserialize.CavalryWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.HorseArcherTroop,
                            Patch_CultureObject_Deserialize.HorseArcherWeight, culture, candidates, weights);

                if (culture.BasicTroop != null)
                {
                    candidates.Add(culture.BasicTroop);
                    weights.Add(100); // 
                }
            }
            else if (settlement.IsVillage && settlement.Village.Bound.IsCastle)
            {
                // Village→Castle: infantry, twohanded, ranged, cavalry, horseArcher + elite_basic_troop (weight=25)
                TryAddTroop(Patch_CultureObject_Deserialize.InfantryTroop,
                            Patch_CultureObject_Deserialize.InfantryWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.TwoHandedTroop,
                            Patch_CultureObject_Deserialize.TwoHandedWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.RangedTroop,
                            Patch_CultureObject_Deserialize.RangedWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.CavalryTroop,
                            Patch_CultureObject_Deserialize.CavalryWeight, culture, candidates, weights);
                TryAddTroop(Patch_CultureObject_Deserialize.HorseArcherTroop,
                            Patch_CultureObject_Deserialize.HorseArcherWeight, culture, candidates, weights);

                var elite = culture.EliteBasicTroop;
                if (elite != null)
                {
                    candidates.Add(elite);
                    weights.Add(25); // 
                }
                else if (culture.BasicTroop != null)
                {
                    // fallback to basic_troop if elite is missing
                    candidates.Add(culture.BasicTroop);
                    weights.Add(100);
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
