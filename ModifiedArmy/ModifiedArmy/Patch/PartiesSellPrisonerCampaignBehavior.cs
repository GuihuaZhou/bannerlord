using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Patch
{
    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "OnSettlementEntered")]
    public static class PartiesSellPrisonerCampaignBehaviorPatch
    {
        public static bool Prefix(MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            if (mobileParty != null
                && !mobileParty.IsMainParty
                && settlement.IsFortification
                && mobileParty.MapFaction != null
                && !mobileParty.IsDisbanding
                && !mobileParty.MapFaction.IsAtWarWith(settlement.MapFaction)
                && (mobileParty.PrisonRoster.TotalRegulars > 0 || (mobileParty.PrisonRoster.TotalHeroes > 0 && mobileParty.PrisonRoster.GetTroopRoster().Exists((TroopRosterElement x) => x.Character != CharacterObject.PlayerCharacter && x.Character.HeroObject.MapFaction.IsAtWarWith(settlement.MapFaction)))))
            {
                TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();

                foreach (TroopRosterElement troopRosterElement in mobileParty.PrisonRoster.GetTroopRoster())
                {
                    if (troopRosterElement.Character.IsHero || troopRosterElement.Character.IsPlayerCharacter)
                    {
                        continue;
                    }

                    var troop = troopRosterElement.Character;
                    if (troop == null)
                    {
                        continue;
                    }

                    // 如果是所属Clan的定居点，则可以转移全部俘虏
                    if (settlement.OwnerClan == mobileParty.Owner.Clan)
                    {
                        troopRoster.Add(troopRosterElement);
                    }
                    // 否则只转移部分俘虏
                    else
                    {
                        if (troop.Occupation == Occupation.Mercenary)
                        {
                            continue;
                        }

                        var type = SoldierTypeClassifier.GetSoldierType(troop);
                        // 低价值俘虏全部转移
                        if (type != SoldierType.Retinue
                            && type != SoldierType.Marine
                            && type != SoldierType.Sergeant
                            && type != SoldierType.Slave)
                        {
                            troopRoster.Add(troopRosterElement);
                        }
                        // 只有30%的概率转移高阶俘虏
                        else if (MBRandom.RandomFloat < 0.3)
                        {
                            troopRoster.Add(troopRosterElement);
                        }
                    }
                }

                if (troopRoster.TotalManCount > 0)
                {
                    SellPrisonersAction.ApplyForSelectedPrisoners(mobileParty.Party, settlement.Party, troopRoster);
                    ModLogger.Info($"{mobileParty.Name}转移了{troopRoster.TotalRegulars}名俘虏到{settlement.Name}");
                }

                //// 第二步，从高价值俘虏中随机卖掉一部分
                //float currentSize = mobileParty.Party.NumberOfAllMembers;
                //float maxSize = mobileParty.Party.PartySizeLimit;

                //int deficit = Math.Max(0, (int)(maxSize - currentSize));
                //if (deficit <= 0)
                //{
                //    return false;
                //}

                //// Party所属Clan拥有该定居点，则尽可能多的转移Prisoner到定居点
                //if (settlement.OwnerClan == mobileParty.LeaderHero.Clan)
                //{
                //    troopRoster.Clear();
                //    foreach (TroopRosterElement troopRosterElement in mobileParty.PrisonRoster.GetTroopRoster())
                //    {
                //        if (troopRosterElement.Character.IsHero || troopRosterElement.Character.IsPlayerCharacter)
                //        {
                //            continue;
                //        }

                //        troopRoster.Add(troopRosterElement);
                //    }
                //    if (troopRoster.TotalManCount > 0)
                //    {
                //        SellPrisonersAction.ApplyForSelectedPrisoners(mobileParty.Party, settlement.Party, troopRoster);
                //        ModLogger.Info($"{mobileParty.Name}转移了{troopRoster.TotalRegulars}名俘虏到{settlement.Name}");
                //    }
                //}
                ////// 卖掉俘虏赚钱
                ////else
                ////{
                ////    float fillRatio = currentSize / Math.Max(1f, maxSize);

                ////    // 满编率越低，继续卖高级俘虏的概率也越低
                ////    if (MBRandom.RandomFloat > fillRatio)
                ////        return false;

                ////    troopRoster.Clear();
                ////    foreach (TroopRosterElement troopRosterElement in mobileParty.PrisonRoster.GetTroopRoster())
                ////    {
                ////        if (troopRosterElement.Character.IsHero || troopRosterElement.Character.IsPlayerCharacter)
                ////        {
                ////            continue;
                ////        }

                ////        if (MBRandom.RandomFloat < fillRatio)
                ////        {
                ////            troopRoster.Add(troopRosterElement);
                ////        }
                ////    }
                ////    if (troopRoster.TotalManCount > 0)
                ////    {
                ////        SellPrisonersAction.ApplyForSelectedPrisoners(mobileParty.Party, null, troopRoster);
                ////        ModLogger.Info($"{mobileParty.Name}在{settlement.Name}卖掉了{troopRoster.TotalRegulars}名高阶俘虏");
                ////    }
                ////}
            }
            return false;
        }
    }


    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior), "DailyTickSettlement")]
    public static class DailyTickSettlementPatch
    {
        private static float GetAISellPrisonerRatio(Settlement settlement)
        {
            // 默认：每天卖 10%（与原版一致），可调整
            return 0.01f;
        }

        private static float GetAIRecruitPrisonerRatio(Settlement settlement)
        {
            // 默认：每天有 5% 概率尝试招募每个高价值俘虏
            return 0.1f;
        }

        public static bool Prefix(Settlement settlement)
        {
            if (settlement.IsFortification)
            {
                TroopRoster prisonRoster = settlement.Party.PrisonRoster;

                //// 尝试招募一部分俘虏
                //if (prisonRoster.TotalRegulars > 0
                //    && settlement.Town != null
                //    && settlement.Town.GarrisonParty != null)
                //{
                //    IEnumerable<TroopRosterElement> enumerable = from t in prisonRoster.GetTroopRoster()
                //                                                 orderby t.Character.Tier
                //                                                 select t;

                //    int space = settlement.Town.GarrisonParty.Party.PartySizeLimit - settlement.Town.GarrisonParty.Party.NumberOfAllMembers;

                //    if (space > 0)
                //    {
                //        TroopRoster removedTroopRoster = TroopRoster.CreateDummyTroopRoster();
                //        TroopRoster addedTroopRoster = TroopRoster.CreateDummyTroopRoster();

                //        foreach (TroopRosterElement troopRosterElement in enumerable)
                //        {
                //            var troop = troopRosterElement.Character;
                //            if (troop == null)
                //            {
                //                continue;
                //            }

                //            if (!troopRosterElement.Character.IsHero && MBRandom.RandomFloat < GetAIRecruitPrisonerRatio(settlement))
                //            {
                //                int count = troopRosterElement.Number;
                //                if (count <= 0)
                //                {
                //                    continue;
                //                }

                //                // 随机生成招募数量
                //                int randomCount = MBRandom.RandomInt(0, count);
                //                randomCount = Math.Min(3, randomCount);
                //                if (randomCount <= 0)
                //                    continue;

                //                // 优先加入封建部队
                //                var fiefManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
                //                var fiefObject = fiefManager.GetFiefData(settlement);
                //                if (fiefObject != null)
                //                {
                //                    if (fiefObject.RecruitFromPrisoners(troop, randomCount))
                //                    {
                //                        // 招募成功
                //                        removedTroopRoster.AddToCounts(troop, randomCount, false, 0, 0, true, -1);
                //                        continue;
                //                    }
                //                }

                //                // 加入驻军部队
                //                if (space > randomCount)
                //                {
                //                    space -= randomCount;
                //                    addedTroopRoster.AddToCounts(troop, randomCount, false, 0, 0, true, -1);
                //                }
                //            }
                //        }


                //        if (addedTroopRoster.TotalManCount > 0)
                //        {
                //            foreach (TroopRosterElement troopRosterElement in addedTroopRoster.GetTroopRoster())
                //            {
                //                if (troopRosterElement.Character.IsHero || troopRosterElement.Character.IsPlayerCharacter)
                //                {
                //                    continue;
                //                }

                //                var troop = troopRosterElement.Character;
                //                if (troop == null)
                //                    continue;

                //                settlement.Town.GarrisonParty.MemberRoster.AddToCounts(troop, troopRosterElement.Number, false, 0, 0, true, -1);
                //                ModLogger.Info($"{settlement.Name}的驻军从俘虏中招募了{troopRosterElement.Number}名{troop.Name}");
                //            }
                //            SellPrisonersAction.ApplyForSelectedPrisoners(settlement.Party, null, addedTroopRoster);
                //        }

                //        if (removedTroopRoster.TotalManCount > 0)
                //        {
                //            SellPrisonersAction.ApplyForSelectedPrisoners(settlement.Party, null, removedTroopRoster);
                //        }
                //    }
                //}

                // 卖掉一部分俘虏
                if (prisonRoster.TotalRegulars > 0)
                {
                    int num = 0;

                    if (settlement.Owner == Hero.MainHero)
                    {
                        num = prisonRoster.TotalManCount - settlement.Party.PrisonerSizeLimit;
                    }
                    else
                    {
                        float sellRatio = GetAISellPrisonerRatio(settlement);
                        num = MBRandom.RoundRandomized(prisonRoster.TotalRegulars * sellRatio);
                    }

                    if (num > 0)
                    {
                        TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();

                        // 对俘虏排序，优先处理低阶俘虏
                        IEnumerable<TroopRosterElement> enumerable = from t in prisonRoster.GetTroopRoster()
                                                                     orderby t.Character.Tier
                                                                     select t;
                        foreach (TroopRosterElement troopRosterElement in enumerable)
                        {
                            if (!troopRosterElement.Character.IsHero)
                            {
                                int num2 = Math.Min(num, troopRosterElement.Number);
                                num -= num2;
                                troopRoster.AddToCounts(troopRosterElement.Character, num2, false, 0, 0, true, -1);
                                if (num <= 0)
                                {
                                    break;
                                }
                            }
                        }
                        if (troopRoster.TotalManCount > 0)
                        {
                            SellPrisonersAction.ApplyForSelectedPrisoners(settlement.Party, null, troopRoster);
                            ModLogger.Info($"{settlement.Name}卖掉了{troopRoster.TotalRegulars}名俘虏");
                        }
                    }
                }
            }

            return false;
        }
    }

}
