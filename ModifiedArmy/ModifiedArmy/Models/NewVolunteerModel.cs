using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models
{
    public class NewVolunteerModel : DefaultVolunteerModel
    {
        // 降低volunteer troop生成的概率
        public override float GetDailyVolunteerProductionProbability(Hero hero, int index, Settlement settlement)
        {
            float num = 0.7f; // 原0.7
            int num2 = 0;
            foreach (Town town in hero.CurrentSettlement.MapFaction.Fiefs)
            {
                num2 += (town.IsTown ? (((town.Prosperity < 3000f) ? 1 : ((town.Prosperity < 6000f) ? 2 : 3)) + town.Villages.Count) : town.Villages.Count);
            }
            float num3 = (num2 < 46) ? ((float)num2 / 46f * ((float)num2 / 46f)) : 1f;
            num += ((hero.CurrentSettlement != null && num3 < 1f) ? ((1f - num3) * 0.2f) : 0f);
            float baseNumber = 0.75f * MathF.Clamp(MathF.Pow(num, (float)(index + 1)), 0f, 1f);
            ExplainedNumber explainedNumber = new ExplainedNumber(baseNumber, false, null);
            Clan clan = hero.Clan;
            if (((clan != null) ? clan.Kingdom : null) != null && hero.Clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.Cantons))
            {
                explainedNumber.AddFactor(0.2f, null);
            }
            Town town2;
            if (!settlement.IsTown)
            {
                Settlement tradeBound = settlement.Village.TradeBound;
                town2 = ((tradeBound != null) ? tradeBound.Town : null);
            }
            else
            {
                town2 = settlement.Town;
            }
            Town town3 = town2;
            if (town3 != null && hero.IsAlive && hero.VolunteerTypes[index] != null && hero.VolunteerTypes[index].IsMounted && PerkHelper.GetPerkValueForTown(DefaultPerks.Riding.CavalryTactics, town3))
            {
                explainedNumber.AddFactor(DefaultPerks.Riding.CavalryTactics.PrimaryBonus, null);
            }

            //InformationManager.DisplayMessage(new InformationMessage($"[MOD] Call NewVolunteerModel::GetDailyVolunteerProductionProbability {explainedNumber.ResultNumber}"));
            return explainedNumber.ResultNumber;
        }


        private int MaximumIndexCanPartyRecruitFromHeroInternal(Hero buyerHero, Hero sellerHero)
        {
            Settlement currentSettlement = sellerHero.CurrentSettlement;
            int num = 1;
            int num2 = (buyerHero == Hero.MainHero) ? Campaign.Current.Models.DifficultyModel.GetPlayerRecruitSlotBonus() : 0;
            int num3 = 0;
            if (sellerHero.IsGangLeader && currentSettlement != null && currentSettlement.OwnerClan == buyerHero.Clan)
            {
                if (currentSettlement.IsTown)
                {
                    Hero governor = currentSettlement.Town.Governor;
                    if (governor != null && governor.GetPerkValue(DefaultPerks.Roguery.OneOfTheFamily))
                    {
                        goto IL_9A;
                    }
                }
                if (!currentSettlement.IsVillage)
                {
                    goto IL_A8;
                }
                Hero governor2 = currentSettlement.Village.Bound.Town.Governor;
                if (governor2 == null || !governor2.GetPerkValue(DefaultPerks.Roguery.OneOfTheFamily))
                {
                    goto IL_A8;
                }
            IL_9A:
                num3 += (int)DefaultPerks.Roguery.OneOfTheFamily.SecondaryBonus;
            }
        IL_A8:
            return MathF.Min(6, MathF.Max(0, num + num2 + num3));
        } 
        
        // 限制招募志愿troop
        // NPC只能在clan拥有的settlement招募
        // 玩家没有settlement时，可以在任意settlement招募；拥有时，只能在所属的settlement招募troop

        public override int MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero, int useValueAsRelation = -101)
        {
            Settlement settlement = sellerHero.CurrentSettlement;
            if (settlement == null)
            {
                return -1;
            }

            MobileParty buyerParty = buyerHero.PartyBelongedTo; 
            Clan buyerClan = buyerParty.ActualClan;
            if (buyerClan == null)
            {
                return -1;
            }

            Clan ownerClan = settlement.OwnerClan;
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
                return -1;
            }

            if (buyerHero.MapFaction.IsAtWarWith(settlement.MapFaction))
            {
                return -1;
            }

            int num = this.MaximumIndexCanPartyRecruitFromHeroInternal(buyerHero, sellerHero);
            int num2 = (useValueAsRelation < -100) ? buyerHero.GetRelation(sellerHero) : useValueAsRelation;
            int num3 = (num2 >= 100) ? 7 : ((num2 >= 80) ? 6 : ((num2 >= 60) ? 5 : ((num2 >= 40) ? 4 : ((num2 >= 20) ? 3 : ((num2 >= 10) ? 2 : ((num2 >= 5) ? 1 : ((num2 >= 0) ? 0 : -1)))))));
            int num4 = (sellerHero.CurrentSettlement != null && buyerHero.MapFaction == sellerHero.CurrentSettlement.MapFaction) ? 1 : 0;
            int num5 = (buyerHero != Hero.MainHero) ? 1 : 0;
            int num6 = (sellerHero.CurrentSettlement != null && buyerHero.MapFaction.IsAtWarWith(sellerHero.CurrentSettlement.MapFaction)) ? (-(1 + num5)) : 0;
            if (buyerHero.IsMinorFactionHero && sellerHero.CurrentSettlement != null && sellerHero.CurrentSettlement.IsVillage)
            {
                num6 = 0;
            }
            int num7 = 0;
            if (sellerHero.IsMerchant && buyerHero.GetPerkValue(DefaultPerks.Trade.ArtisanCommunity))
            {
                num7 += (int)DefaultPerks.Trade.ArtisanCommunity.SecondaryBonus;
            }
            if (sellerHero.Culture == buyerHero.Culture && buyerHero.GetPerkValue(DefaultPerks.Leadership.CombatTips))
            {
                num7 += (int)DefaultPerks.Leadership.CombatTips.SecondaryBonus;
            }
            if (sellerHero.IsRuralNotable && buyerHero.GetPerkValue(DefaultPerks.Charm.Firebrand))
            {
                num7 += (int)DefaultPerks.Charm.Firebrand.SecondaryBonus;
            }
            if (sellerHero.IsUrbanNotable && buyerHero.GetPerkValue(DefaultPerks.Charm.FlexibleEthics))
            {
                num7 += (int)DefaultPerks.Charm.FlexibleEthics.SecondaryBonus;
            }
            if (sellerHero.IsArtisan && buyerHero.PartyBelongedTo != null && buyerHero.PartyBelongedTo.EffectiveEngineer != null && buyerHero.PartyBelongedTo.EffectiveEngineer.GetPerkValue(DefaultPerks.Engineering.EngineeringGuilds))
            {
                num7 += (int)DefaultPerks.Engineering.EngineeringGuilds.PrimaryBonus;
            }
            return MathF.Min(6, num + num3 + num4 + num5 + num6 + num7);
        }

        // 按权重选择volunteer troops
        private CharacterObject WeightedRandomSelect(List<CharacterObject> troops, List<int> weights)
        {
            if (troops.Count == 0 || weights.Count == 0 || troops.Count != weights.Count)
            {
                return null;
            }

            int total = weights.Sum();
            if (total <= 0)
            {
                // 如果总权重 <= 0，返回列表中的第一个单位，或者 null
                return troops.Count > 0 ? troops[0] : null;
            }

            int rand = MBRandom.RandomInt(total);
            int sum = 0;
            for (int i = 0; i < troops.Count; i++)
            {
                sum += weights[i];
                if (rand < sum)
                {
                    return troops[i];
                }
            }

            // 理论上不应该到达这里，但如果到达了，返回最后一个单位
            return troops[troops.Count - 1];
        }

        // 按权重返回volunteer troops
        public override CharacterObject GetBasicVolunteer(Hero sellerHero)
        {
            if (!CampaignState.IsReady || sellerHero?.Culture == null || sellerHero.CurrentSettlement == null)
            {
                return sellerHero?.Culture?.BasicTroop;
            }

            var culture = sellerHero.Culture;
            var settlement = sellerHero.CurrentSettlement;

            if (!settlement.IsTown &&
                !(settlement.IsVillage && (settlement.Village.Bound.IsTown || settlement.Village.Bound.IsCastle)))
            {
                return culture.BasicTroop;
            }

            var group = BasicTroopGroupManager.GetGroupForCulture(culture);

            if (group != null)
            {
                var allEntries = new List<BasicTroopEntry>();
                allEntries.AddRange(group.TroopsByType[FiefTroopType.Fief_Retinue]);
                allEntries.AddRange(group.TroopsByType[FiefTroopType.Fief_Sergeant]);
                allEntries.AddRange(group.TroopsByType[FiefTroopType.Fief_Militia]);

                if (allEntries.Count > 0)
                {
                    var troops = allEntries.Select(entry => entry.Troop).ToList();
                    var weights = allEntries.Select(entry => entry.Weight).ToList(); 

                    CharacterObject selectedTroop = WeightedRandomSelect(troops, weights);

                    return selectedTroop ?? culture.BasicTroop;
                }
            }

            // fallback：没有自定义配置时，返回原版基础兵
            return culture.BasicTroop;
        }
    }

    /// <summary>
    /// 禁止原版的志愿兵自动更新机制
    /// </summary>
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior))]
    [HarmonyPatch("UpdateVolunteersOfNotablesInSettlement")]
    public static class RecruitmentCampaignBehavior_UpdateVolunteers_Patch
    {
        // Prefix 返回 false 表示跳过原方法
        public static bool Prefix()
        {
            // 完全禁用原版志愿兵生成逻辑
            return false;
        }
    }
}