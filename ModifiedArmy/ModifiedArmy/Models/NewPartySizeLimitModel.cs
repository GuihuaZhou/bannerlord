using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models
{
    public class NewPartySizeLimitModel : DefaultPartySizeLimitModel
    {
        public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false)
        {
            var result = base.GetPartyMemberSizeLimit(party, includeDescriptions);

            if (party.MobileParty.IsMilitia)
            {
                var settlement = party.MobileParty.HomeSettlement;
                if (settlement != null 
                    && (settlement.IsTown || settlement.IsCastle))
                {
                    var manager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
                    int totalLimit = manager.GetFiefTroopLimit(settlement);
                    result.Add(totalLimit, new TextObject("{=Fief_Party_Size_Limit}Fief Party Size Limit"), null);
                }
            }

            return result;
        }


        private void AddGarrisonOwnerPerkEffects(Settlement currentSettlement, ref ExplainedNumber result)
        {
            if (currentSettlement != null && currentSettlement.IsFortification)
            {
                PerkHelper.AddPerkBonusForTown(DefaultPerks.OneHanded.CorpsACorps, currentSettlement.Town, ref result);
                PerkHelper.AddPerkBonusForTown(DefaultPerks.Leadership.VeteransRespect, currentSettlement.Town, ref result);
            }
        }

        private void AddSettlementProjectBonuses(Settlement settlement, ref ExplainedNumber result)
        {
            if (settlement != null && settlement.IsFortification)
            {
                settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.GarrisonCapacity, ref result);
            }
        }

        /// <summary>
        /// 限制驻军人数
        /// </summary>
        /// <param name="settlement"></param>
        /// <param name="includeDescriptions"></param>
        /// <returns></returns>
        public override ExplainedNumber CalculateGarrisonPartySizeLimit(Settlement settlement, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(GarrisonConstants.BaseGarrisonSize, includeDescriptions, this._baseSizeText);
			SkillHelper.AddSkillBonusForCharacter(DefaultSkillEffects.LeadershipGarrisonSizeBonus, settlement.OwnerClan.Leader.CharacterObject, ref result);
			if (settlement.IsTown)
			{
				result.Add(GarrisonConstants.TownGarrisonBonus, this._townBonusText, null);
			}
			this.AddGarrisonOwnerPerkEffects(settlement, ref result);
			this.AddSettlementProjectBonuses(settlement, ref result);
			return result;
		}



        private readonly TextObject _baseSizeText = GameTexts.FindText("str_base_size", null);
        private readonly TextObject _townBonusText = GameTexts.FindText("str_town_bonus", null);

    }
}
