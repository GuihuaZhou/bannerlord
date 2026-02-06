using HarmonyLib;
using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Patch
{
    /// <summary>
    /// 每次创建party，消耗 2% 的gold
    /// </summary>
    [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "ConsiderSpawningLordParties")]
    public static class ConsiderSpawningLordPartiesPatch
    {
        [HarmonyPrefix]
        public static void Prefix(
            Clan clan,
            bool isNewGame)
        {
            int partyLimitForTier = Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(clan, clan.Tier);
            int count = clan.WarPartyComponents.Count;
            if (count >= partyLimitForTier)
            {
                return;
            }
            int num = partyLimitForTier - count;
            for (int i = 0; i < num; i++)
            {
                Hero bestAvailableCommander = GetBestAvailableCommander(clan);
                if (bestAvailableCommander == null)
                {
                    break;
                }
                float num2 = CalculateScoreToCreateParty(clan);
                if (GetHeroPartyCommandScore(bestAvailableCommander) + num2 > 100f)
                {

                    if (clan.Gold < ClanPartySpawnConstants.CreationGoldCost * 1.5f && !isNewGame)
                        continue;

                    MobileParty mobileParty = SpawnLordParty(bestAvailableCommander, isNewGame);
                    if (mobileParty != null)
                    {
                        GiveInitialItemsToParty(mobileParty);

                        if (!isNewGame)
                        {
                            GiveGoldAction.ApplyBetweenCharacters(clan.Leader, null, ClanPartySpawnConstants.CreationGoldCost, false);
                            ModLogger.Info($"{clan.Name} has raised a new party led by {bestAvailableCommander.Name}. Gold cost: {ClanPartySpawnConstants.CreationGoldCost}. Remaining clan treasury: {clan.Gold}.");
                        }
                    }
                }
            }

            return;
        }

        private static void GiveInitialItemsToParty(MobileParty heroParty)
        {
            float num = 2f * Campaign.Current.EstimatedAverageLordPartySpeed * CampaignTime.HoursInDay;
            foreach (Settlement settlement in Campaign.Current.Settlements)
            {
                if (settlement.IsVillage)
                {
                    float num2;
                    float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(heroParty, settlement, false, heroParty.NavigationCapability, out num2);
                    if (distance < num)
                    {
                        foreach (ValueTuple<ItemObject, float> valueTuple in settlement.Village.VillageType.Productions)
                        {
                            ItemObject item = valueTuple.Item1;
                            float item2 = valueTuple.Item2;
                            float num3 = item.ItemType == ItemObject.ItemTypeEnum.Horse && item.HorseComponent.IsRideable && !item.HorseComponent.IsPackAnimal ? 7f : item.IsFood ? 0.1f : 0f;
                            float num4 = (heroParty.MemberRoster.TotalManCount + 2f) / 200f;
                            float num5 = 1f - distance / num;
                            int num6 = MBRandom.RoundRandomized(num3 * item2 * num5 * num4);
                            if (num6 > 0)
                            {
                                heroParty.ItemRoster.AddToCounts(item, num6);
                            }
                        }
                    }
                }
            }
        }

        private static MobileParty SpawnLordParty(Hero hero, bool isNewGame)
        {
            if (hero.GovernorOf != null)
            {
                ChangeGovernorAction.RemoveGovernorOf(hero);
            }
            Settlement settlement = SettlementHelper.GetBestSettlementToSpawnAround(hero);
            if (settlement == null || settlement.MapFaction != hero.MapFaction)
            {
                settlement = hero.MapFaction.InitialHomeSettlement;
            }
            if (settlement == null)
            {
                settlement = Settlement.All.First((x) => x.Culture == hero.Culture);
            }
            MobileParty mobileParty = MobilePartyHelper.SpawnLordParty(hero, settlement.GatePosition, Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default) / 2f);
            if (isNewGame)
            {
                int num = (int)((mobileParty.Party.PartySizeLimit - mobileParty.MemberRoster.TotalManCount) * MBRandom.RandomFloatRanged(0.75f, 0.9f));
                PartyTemplateObject defaultPartyTemplate = mobileParty.LordPartyComponent.Owner.Clan.DefaultPartyTemplate;
                List<ValueTuple<CharacterObject, float>> list = new List<ValueTuple<CharacterObject, float>>();
                foreach (PartyTemplateStack partyTemplateStack in defaultPartyTemplate.Stacks)
                {
                    list.Add(new ValueTuple<CharacterObject, float>(partyTemplateStack.Character, (partyTemplateStack.MinValue + partyTemplateStack.MaxValue) / 2f));
                }
                for (int i = 0; i < num; i++)
                {
                    CharacterObject element = MBRandom.ChooseWeighted(list);
                    mobileParty.AddElementToMemberRoster(element, 1, false);
                }
            }
            return mobileParty;
        }

        private static float GetHeroPartyCommandScore(Hero hero)
        {
            return 3f * hero.GetSkillValue(DefaultSkills.Tactics) + 2f * hero.GetSkillValue(DefaultSkills.Leadership) + hero.GetSkillValue(DefaultSkills.Scouting) + hero.GetSkillValue(DefaultSkills.Steward) + hero.GetSkillValue(DefaultSkills.OneHanded) + hero.GetSkillValue(DefaultSkills.TwoHanded) + hero.GetSkillValue(DefaultSkills.Polearm) + hero.GetSkillValue(DefaultSkills.Riding) + (hero.Clan.Leader == hero ? 1000f : 0f) + (hero.GovernorOf == null ? 500f : 0f) + (hero.IsNoncombatant ? -5000 : 0);
        }

        private static float CalculateScoreToCreateParty(Clan clan)
        {
            return clan.Fiefs.Count * 100 - clan.WarPartyComponents.Count * 100 + clan.Gold * 0.01f + (clan.IsMinorFaction ? 200f : 0f) + (clan.WarPartyComponents.Count > 0 ? 0f : 200f);
        }

        private static Hero GetBestAvailableCommander(Clan clan)
        {
            Hero hero = null;
            float num = 0f;
            foreach (Hero hero2 in clan.Heroes)
            {
                if (hero2.IsActive && hero2.IsAlive && hero2.PartyBelongedTo == null && hero2.PartyBelongedToAsPrisoner == null && hero2.CanLeadParty() && hero2.Age > Campaign.Current.Models.AgeModel.HeroComesOfAge && hero2.CharacterObject.Occupation == Occupation.Lord)
                {
                    float heroPartyCommandScore = GetHeroPartyCommandScore(hero2);
                    if (heroPartyCommandScore > num)
                    {
                        num = heroPartyCommandScore;
                        hero = hero2;
                    }
                }
            }
            if (hero != null)
            {
                return hero;
            }
            if (clan != Clan.PlayerClan)
            {
                foreach (Hero hero3 in clan.Heroes)
                {
                    if (hero3.IsActive && hero3.IsAlive && hero3.PartyBelongedTo == null && hero3.PartyBelongedToAsPrisoner == null && hero3.Age > Campaign.Current.Models.AgeModel.HeroComesOfAge && hero3.CharacterObject.Occupation == Occupation.Lord)
                    {
                        float heroPartyCommandScore2 = GetHeroPartyCommandScore(hero3);
                        if (heroPartyCommandScore2 > num)
                        {
                            num = heroPartyCommandScore2;
                            hero = hero3;
                        }
                    }
                }
            }
            return hero;
        }
    }
}
