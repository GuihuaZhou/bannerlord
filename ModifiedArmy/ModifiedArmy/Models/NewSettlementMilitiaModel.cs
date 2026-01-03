using Helpers;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models
{
    public class NewSettlementMilitiaModel : DefaultSettlementMilitiaModel
    {
        

        // Token: 0x04000884 RID: 2180
        private static readonly TextObject BaseText = new TextObject("{=militarybase}Base", null);

        // Token: 0x04000885 RID: 2181
        private static readonly TextObject FromHearthsText = new TextObject("{=ecdZglky}From Hearths", null);

        // Token: 0x04000886 RID: 2182
        private static readonly TextObject FromProsperityText = new TextObject("{=cTmiNAlI}From Prosperity", null);

        // Token: 0x04000887 RID: 2183
        private static readonly TextObject RetiredText = new TextObject("{=gHnfFi1s}Retired", null);

        // Token: 0x04000888 RID: 2184
        private static readonly TextObject MilitiaFromMarketText = new TextObject("{=7ve3bQxg}Weapons From Market", null);

        // Token: 0x04000889 RID: 2185
        private static readonly TextObject LowLoyaltyText = new TextObject("{=SJ2qsRdF}Low Loyalty", null);

        // Token: 0x0400088A RID: 2186
        private static readonly TextObject CultureText = GameTexts.FindText("str_culture", null);


        private static void GetSettlementMilitiaChangeDueToPolicies(Settlement settlement, ref ExplainedNumber result)
        {
            Kingdom kingdom = settlement.OwnerClan.Kingdom;
            if (kingdom != null && kingdom.ActivePolicies.Contains(DefaultPolicies.Citizenship))
            {
                result.Add(1f, DefaultPolicies.Citizenship.Name, null);
            }
        }

        private static void GetSettlementMilitiaChangeDueToPerks(Settlement settlement, ref ExplainedNumber result)
        {
            if (settlement.Town != null && settlement.Town.Governor != null)
            {
                PerkHelper.AddPerkBonusForTown(DefaultPerks.OneHanded.SwiftStrike, settlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Polearm.KeepAtBay, settlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Bow.MerryMen, settlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Crossbow.LongShots, settlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Throwing.SlingingCompetitions, settlement.Town, ref result);
                if (settlement.IsUnderSiege)
                {
                    PerkHelper.AddPerkBonusForTown(DefaultPerks.Roguery.ArmsDealer, settlement.Town, ref result);
                }
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.SevenVeterans, settlement.Town, ref result);
            }
        }

        private static void GetSettlementMilitiaChangeDueToIssues(Settlement settlement, ref ExplainedNumber result)
        {
            Campaign.Current.Models.IssueModel.GetIssueEffectsOfSettlement(DefaultIssueEffects.SettlementMilitia, settlement, ref result);
        }

        public override ExplainedNumber CalculateMilitiaChange(Settlement settlement, bool includeDescriptions = false)
        {
            return CalculateMilitiaChangeInternal(settlement, includeDescriptions);
        }

        ///
        /// 计算民兵数量变化时，减去封邑部队人数
        /// 
        private ExplainedNumber CalculateMilitiaChangeInternal(Settlement settlement, bool includeDescriptions = false)
        {
            ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
            float militia = settlement.Militia;

            var fiefManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            var fiefTroopCount = fiefManager.GetAvailableTroopCount(settlement);
            militia -= fiefTroopCount;

            if (settlement.IsFortification)
            {
                result.Add(2f, NewSettlementMilitiaModel.BaseText, null);
            }
            float value = -militia * 0.025f;
            result.Add(value, NewSettlementMilitiaModel.RetiredText, null);
            if (settlement.IsVillage)
            {
                float value2 = settlement.Village.Hearth / 400f;
                result.Add(value2, NewSettlementMilitiaModel.FromHearthsText, null);
            }
            else if (settlement.IsFortification)
            {
                float num = settlement.Town.Prosperity / 1000f;
                result.Add(num, NewSettlementMilitiaModel.FromProsperityText, null);
                if (settlement.Town.InRebelliousState)
                {
                    float num2 = MBMath.Map(settlement.Town.Loyalty, 0f, (float)Campaign.Current.Models.SettlementLoyaltyModel.RebelliousStateStartLoyaltyThreshold, (float)Campaign.Current.Models.SettlementLoyaltyModel.MilitiaBoostPercentage, 0f);
                    float value3 = MathF.Abs(num * (num2 * 0.01f));
                    result.Add(value3, NewSettlementMilitiaModel.LowLoyaltyText, null);
                }
            }
            if (settlement.IsTown)
            {
                int num3 = settlement.Town.SoldItems.Sum(delegate (Town.SellLog x)
                {
                    if (x.Category.Properties != ItemCategory.Property.BonusToMilitia)
                    {
                        return 0;
                    }
                    return x.Number;
                });
                if (num3 > 0)
                {
                    result.Add(0.2f * (float)num3, NewSettlementMilitiaModel.MilitiaFromMarketText, null);
                }
                if (settlement.OwnerClan.Kingdom != null)
                {
                    if (settlement.OwnerClan.Kingdom.ActivePolicies.Contains(DefaultPolicies.Serfdom) && settlement.IsTown)
                    {
                        result.Add(-1f, DefaultPolicies.Serfdom.Name, null);
                    }
                    if (settlement.OwnerClan.Kingdom.ActivePolicies.Contains(DefaultPolicies.Cantons))
                    {
                        result.Add(1f, DefaultPolicies.Cantons.Name, null);
                    }
                }
                if (settlement.OwnerClan.Culture.HasFeat(DefaultCulturalFeats.BattanianMilitiaFeat))
                {
                    result.Add(DefaultCulturalFeats.BattanianMilitiaFeat.EffectBonus, NewSettlementMilitiaModel.CultureText, null);
                }
            }
            if (settlement.IsCastle || settlement.IsTown)
            {
                settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.Militia, ref result);
                if (settlement.IsCastle && settlement.Town.InRebelliousState)
                {
                    settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.MilitiaReduction, ref result);
                }
                NewSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPolicies(settlement, ref result);
                NewSettlementMilitiaModel.GetSettlementMilitiaChangeDueToPerks(settlement, ref result);
                NewSettlementMilitiaModel.GetSettlementMilitiaChangeDueToIssues(settlement, ref result);
            }
            return result;
        }
    }
}
