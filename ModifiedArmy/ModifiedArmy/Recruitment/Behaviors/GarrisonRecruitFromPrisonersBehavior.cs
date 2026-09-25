using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// 定居点每天从其俘虏监狱中自动招募士兵来补充守军。
    /// </summary>
    public class GarrisonRecruitFromPrisonersBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(this.OnDailySettlementTick));
        }
        public override void SyncData(IDataStore dataStore)
        {
        }

        private static CharacterObject GetBasicTroopForTown(Town town)
        {
            return town.MapFaction.BasicTroop;
        }


        private ExplainedNumber GetBaseGarrisonChangeExplainedNumber(Town town, bool includeDescriptions = false)
        {
            ExplainedNumber result = Campaign.Current.Models.SettlementGarrisonModel.CalculateBaseGarrisonChange(town.Settlement, includeDescriptions);
            int num = (town.GarrisonParty == null) ? ((int)Campaign.Current.Models.PartySizeLimitModel.CalculateGarrisonPartySizeLimit(town.Settlement, false).ResultNumber) : (town.GarrisonParty.Party.PartySizeLimit - town.GarrisonParty.Party.NumberOfAllMembers);
            if (result.LimitMaxValue > (float)num)
            {
                result.LimitMax((float)num, new TextObject("{=mp68RYnD}Party Size Limit", null));
            }
            int characterWage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(GarrisonRecruitFromPrisonersBehavior.GetBasicTroopForTown(town));
            MobileParty garrisonParty = town.GarrisonParty;
            int num2 = ((garrisonParty != null) ? garrisonParty.GetAvailableWageBudget() : town.Settlement.GarrisonWagePaymentLimit) / characterWage;
            if (result.LimitMaxValue > (float)num2)
            {
                result.LimitMax((float)num2, new TextObject("{=7GJOWuUO}Wage Limit", null));
            }
            return result;
        }

        private void OnDailySettlementTick(Settlement settlement)
        {
            if (settlement.IsFortification)
            {
                Town town = settlement.Town;
                if (town == null || town.GarrisonParty == null)
                    return;

                ExplainedNumber prisonerRecruitmentAmount = GetBaseGarrisonChangeExplainedNumber(town);
                int count = (int)prisonerRecruitmentAmount.ResultNumber;
                if (count > 0)
                {
                    TroopRoster prisonRoster = settlement.Party.PrisonRoster;
                    if (prisonRoster == null)
                    {
                        return;
                    }

                    TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
                    foreach (TroopRosterElement element in prisonRoster.GetTroopRoster())
                    {
                        if (count <= 0)
                            break;

                        CharacterObject troop = element.Character;
                        if (troop == null || troop.IsHero || troop.Occupation == Occupation.Bandit)
                            continue;

                        var type = SoldierTypeClassifier.GetSoldierType(troop);
                        // 低阶俘虏仅10%的概率招募
                        if (type == SoldierType.Militia
                            || type == SoldierType.Other)
                        {
                            if (MBRandom.RandomFloat < 0.1)
                            {
                                int min_count = MathF.Min(count, element.Number);
                                troopRoster.AddToCounts(troop, min_count, false, 0, 0, true, -1);
                                count -= min_count;
                            }
                        }
                        // 有40%的概率招募高阶俘虏
                        else if (MBRandom.RandomFloat < 0.4)
                        {
                            int min_count = MathF.Min(count, element.Number);
                            troopRoster.AddToCounts(troop, min_count, false, 0, 0, true, -1);
                            count -= min_count;
                        }
                    }

                    if (troopRoster.TotalRegulars > 0)
                    {
                        SellPrisonersAction.ApplyForSelectedPrisoners(settlement.Party, null, troopRoster);
                        foreach (TroopRosterElement element in troopRoster.GetTroopRoster())
                        {
                            town.GarrisonParty.MemberRoster.AddToCounts(element.Character, element.Number, false, 0, 0, true, -1);
                            ModLogger.Info($"{settlement.Name}的驻军从俘虏中招募了{element.Number}名{element.Character.Name}");
                        }
                    }
                }
            }
        }
    }
}
