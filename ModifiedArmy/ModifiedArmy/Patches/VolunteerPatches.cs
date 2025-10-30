using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Patches
{
    /// <summary>
    /// 为每个 CultureObject 存储自定义基础兵种（按类型分类）
    /// </summary>
    public static class CultureBasicTroopManager
    {
        public struct TroopEntry
        {
            public CharacterObject Troop;
            public int Weight;

            public TroopEntry(CharacterObject troop, int weight)
            {
                Troop = troop ?? throw new ArgumentNullException(nameof(troop));
                Weight = Math.Max(1, weight); // 权重至少为1，避免无效概率
            }
        }

        /// <summary>
        /// CultureObject → (type → TroopEntry)
        /// 例如：type 可为 "basic", "elite", "infantry", "ranged", "cavalry", "horsearcher", "twohanded" 等
        /// </summary>
        public static readonly Dictionary<CultureObject, Dictionary<string, TroopEntry>> Data =
            new Dictionary<CultureObject, Dictionary<string, TroopEntry>>();
    }

    [HarmonyPatch(typeof(CultureObject), "Deserialize")]
    public static class Patch_CultureObject_Deserialize
    {
        public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
        {
            var entries = new Dictionary<string, CultureBasicTroopManager.TroopEntry>();

            XmlNode basicTroopsNode = node.SelectSingleNode("basic_troop_weight");
            if (basicTroopsNode != null)
            {
                foreach (XmlNode troopNode in basicTroopsNode.SelectNodes("Troop"))
                {
                    string id = GetAttr(troopNode, "id");
                    string type = GetAttr(troopNode, "type")?.ToLowerInvariant();
                    int weight = ParseInt(GetAttr(troopNode, "weight"), 100);

                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
                        continue;

                    CharacterObject troop = objectManager.GetObject<CharacterObject>(id);
                    if (troop != null)
                    {
                        entries[type] = new CultureBasicTroopManager.TroopEntry(troop, weight);
                    }
                }
            }

            // 绑定到当前 CultureObject 实例
            CultureBasicTroopManager.Data[__instance] = entries;
        }

        private static string GetAttr(XmlNode node, string name) =>
            node?.Attributes?[name]?.Value;

        private static int ParseInt(string s, int defaultValue) =>
            string.IsNullOrEmpty(s) || !int.TryParse(s, out int result) ? defaultValue : result;
    }

    //// =============== 扩展 CultureObject 读取 troop + 权重 ===============
    //[HarmonyPatch(typeof(CultureObject), "Deserialize")]
    //public class Patch_CultureObject_Deserialize
    //{
    //    // Troop 字段
    //    public static Dictionary<CultureObject, CharacterObject> InfantryTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> TwoHandedTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> RangedTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> CavalryTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> HorseArcherTroop = new Dictionary<CultureObject, CharacterObject>();

    //    // 权重字段（默认 0）
    //    public static Dictionary<CultureObject, int> InfantryWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> TwoHandedWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> RangedWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> CavalryWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> HorseArcherWeight = new Dictionary<CultureObject, int>();

    //    public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
    //    {
    //        var culture = __instance;

    //        // 清理旧值
    //        InfantryTroop.Remove(culture); TwoHandedTroop.Remove(culture);
    //        RangedTroop.Remove(culture); CavalryTroop.Remove(culture); HorseArcherTroop.Remove(culture);
    //        InfantryWeight.Remove(culture); TwoHandedWeight.Remove(culture);
    //        RangedWeight.Remove(culture); CavalryWeight.Remove(culture); HorseArcherWeight.Remove(culture);

    //        // 读取 troop
    //        var inf = objectManager.ReadObjectReferenceFromXml<CharacterObject>("infantry_basic_troop", node);
    //        var two = objectManager.ReadObjectReferenceFromXml<CharacterObject>("twohanded_basic_troop", node);
    //        var ran = objectManager.ReadObjectReferenceFromXml<CharacterObject>("ranged_basic_troop", node);
    //        var cav = objectManager.ReadObjectReferenceFromXml<CharacterObject>("cavalry_basic_troop", node);
    //        var har = objectManager.ReadObjectReferenceFromXml<CharacterObject>("horseArcher_basic_troop", node);

    //        if (inf != null) InfantryTroop[culture] = inf;
    //        if (two != null) TwoHandedTroop[culture] = two;
    //        if (ran != null) RangedTroop[culture] = ran;
    //        if (cav != null) CavalryTroop[culture] = cav;
    //        if (har != null) HorseArcherTroop[culture] = har;

    //        // 读取权重（默认 0）
    //        InfantryWeight[culture] = ReadIntAttr(node, "infantry_basic_troop_weight", 0);
    //        TwoHandedWeight[culture] = ReadIntAttr(node, "twohanded_basic_troop_weight", 0);
    //        RangedWeight[culture] = ReadIntAttr(node, "ranged_basic_troop_weight", 0);
    //        CavalryWeight[culture] = ReadIntAttr(node, "cavalry_basic_troop_weight", 0);
    //        HorseArcherWeight[culture] = ReadIntAttr(node, "horseArcher_basic_troop_weight", 0);
    //    }

    //    private static int ReadIntAttr(XmlNode node, string attrName, int defaultValue)
    //    {
    //        var attr = node.Attributes?[attrName];
    //        if (attr != null && int.TryParse(attr.Value, out int result))
    //            return Math.Max(0, result);
    //        return defaultValue;
    //    }
    //}

    [HarmonyPatch(typeof(DefaultVolunteerModel), "GetBasicVolunteer")]
    public class Patch_GetBasicVolunteer
    {
        /// <summary>
        /// 尝试从 CultureBasicTroopManager 中获取指定类型的兵种，并加入候选列表
        /// </summary>
        private static void TryAddTroopByType(
            CultureObject culture,
            string type,
            List<CharacterObject> troops,
            List<int> weights)
        {
            if (!CultureBasicTroopManager.Data.TryGetValue(culture, out var typeDict))
                return;

            if (typeDict.TryGetValue(type, out var entry))
            {
                troops.Add(entry.Troop);
                weights.Add(entry.Weight);
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
                // Town / Village→Town: infantry, twohanded, ranged, cavalry, horsearcher + basic_troop (fallback)
                TryAddTroopByType(culture, "infantry", candidates, weights);
                TryAddTroopByType(culture, "twohanded", candidates, weights);
                TryAddTroopByType(culture, "ranged", candidates, weights);
                TryAddTroopByType(culture, "cavalry", candidates, weights);
                TryAddTroopByType(culture, "horsearcher", candidates, weights);

                // Fallback: 如果 XML 中定义了 type="basic"，也会被包含；否则用原版 BasicTroop
                if (candidates.Count == 0 && culture.BasicTroop != null)
                {
                    candidates.Add(culture.BasicTroop);
                    weights.Add(100);
                }
            }
            else if (settlement.IsVillage && settlement.Village.Bound.IsCastle)
            {
                // Village→Castle: 同上，但可额外加入 elite（如果 XML 有 type="elite"）
                TryAddTroopByType(culture, "infantry", candidates, weights);
                TryAddTroopByType(culture, "twohanded", candidates, weights);
                TryAddTroopByType(culture, "ranged", candidates, weights);
                TryAddTroopByType(culture, "cavalry", candidates, weights);
                TryAddTroopByType(culture, "horsearcher", candidates, weights);
                TryAddTroopByType(culture, "elite", candidates, weights); // 新增：支持 elite 类型

                // 如果没有自定义 elite，但原版有 EliteBasicTroop，也可加入（可选）
                if (!CultureBasicTroopManager.Data.TryGetValue(culture, out var dict) ||
                    !dict.ContainsKey("elite"))
                {
                    var elite = culture.EliteBasicTroop;
                    if (elite != null)
                    {
                        candidates.Add(elite);
                        weights.Add(25); // 权重可调
                    }
                }

                // 最终 fallback
                if (candidates.Count == 0 && culture.BasicTroop != null)
                {
                    candidates.Add(culture.BasicTroop);
                    weights.Add(100);
                }
            }
            else
            {
                // 其他情况（如野外营地）：直接用 BasicTroop
                __result = culture.BasicTroop;
                return false;
            }

            // 执行加权随机选择
            __result = WeightedRandomSelect(candidates, weights) ?? culture.BasicTroop;
            return false;
        }
    }

    //// =============== GetBasicVolunteer 按权重选择基础troop ===============
    //[HarmonyPatch(typeof(DefaultVolunteerModel), "GetBasicVolunteer")]
    //public class Patch_GetBasicVolunteer
    //{
    //    private static void TryAddTroop(
    //        Dictionary<CultureObject, CharacterObject> troopDict,
    //        Dictionary<CultureObject, int> weightDict,
    //        CultureObject culture,
    //        List<CharacterObject> troops,
    //        List<int> weights)
    //    {
    //        if (!troopDict.TryGetValue(culture, out CharacterObject troop) || troop == null)
    //            return;
    //        int weight = 0;
    //        weightDict.TryGetValue(culture, out weight);
    //        if (weight > 0)
    //        {
    //            troops.Add(troop);
    //            weights.Add(weight);
    //        }
    //    }

    //    private static CharacterObject WeightedRandomSelect(List<CharacterObject> troops, List<int> weights)
    //    {
    //        if (troops.Count == 0) return null;
    //        int total = 0;
    //        foreach (int w in weights) total += w;
    //        if (total <= 0) return troops[0];
    //        int rand = MBRandom.RandomInt(total);
    //        int sum = 0;
    //        for (int i = 0; i < troops.Count; i++)
    //        {
    //            sum += weights[i];
    //            if (rand < sum)
    //                return troops[i];
    //        }
    //        return troops[troops.Count - 1]; // ✅ 修复：兼容 .NET Framework
    //    }

    //    public static bool Prefix(Hero sellerHero, ref CharacterObject __result)
    //    {
    //        if (!CampaignState.IsReady || sellerHero?.Culture == null || sellerHero.CurrentSettlement == null)
    //            return true;

    //        var culture = sellerHero.Culture;
    //        var settlement = sellerHero.CurrentSettlement;

    //        List<CharacterObject> candidates = new List<CharacterObject>();
    //        List<int> weights = new List<int>();

    //        if (settlement.IsTown || (settlement.IsVillage && settlement.Village.Bound.IsTown))
    //        {
    //            // Town / Village→Town: infantry, twohanded, ranged + basic_troop (weight=100)
    //            TryAddTroop(Patch_CultureObject_Deserialize.InfantryTroop,
    //                        Patch_CultureObject_Deserialize.InfantryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.TwoHandedTroop,
    //                        Patch_CultureObject_Deserialize.TwoHandedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.RangedTroop,
    //                        Patch_CultureObject_Deserialize.RangedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.CavalryTroop,
    //                        Patch_CultureObject_Deserialize.CavalryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.HorseArcherTroop,
    //                        Patch_CultureObject_Deserialize.HorseArcherWeight, culture, candidates, weights);

    //            if (culture.BasicTroop != null)
    //            {
    //                candidates.Add(culture.BasicTroop);
    //                weights.Add(100); // 
    //            }
    //        }
    //        else if (settlement.IsVillage && settlement.Village.Bound.IsCastle)
    //        {
    //            // Village→Castle: infantry, twohanded, ranged, cavalry, horseArcher + elite_basic_troop (weight=25)
    //            TryAddTroop(Patch_CultureObject_Deserialize.InfantryTroop,
    //                        Patch_CultureObject_Deserialize.InfantryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.TwoHandedTroop,
    //                        Patch_CultureObject_Deserialize.TwoHandedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.RangedTroop,
    //                        Patch_CultureObject_Deserialize.RangedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.CavalryTroop,
    //                        Patch_CultureObject_Deserialize.CavalryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.HorseArcherTroop,
    //                        Patch_CultureObject_Deserialize.HorseArcherWeight, culture, candidates, weights);

    //            var elite = culture.EliteBasicTroop;
    //            if (elite != null)
    //            {
    //                candidates.Add(elite);
    //                weights.Add(25); // 
    //            }
    //            else if (culture.BasicTroop != null)
    //            {
    //                // fallback to basic_troop if elite is missing
    //                candidates.Add(culture.BasicTroop);
    //                weights.Add(100);
    //            }
    //        }
    //        else
    //        {
    //            __result = culture.BasicTroop;
    //            return false;
    //        }

    //        if (candidates.Count == 0)
    //        {
    //            __result = culture.BasicTroop;
    //        }
    //        else
    //        {
    //            __result = WeightedRandomSelect(candidates, weights);
    //            if (__result == null) __result = culture.BasicTroop;
    //        }

    //        return false;
    //    }
    //}

    // =============== 限制志愿兵招募 ===============
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
}