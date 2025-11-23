using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.Fief
{
    //public class FiefPartySizeLimitModel : DefaultPartySizeLimitModel
    //{
    //    public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false)
    //    {
    //        if (party.IsMobile && party.MobileParty.PartyComponent is FiefPartyComponent fiefComp)
    //        {
    //            var settlement = fiefComp.HomeSettlement;
    //            if (settlement == null)
    //                return new ExplainedNumber(0f, includeDescriptions);

    //            int totalLimit = FiefTroopLimitCalculator.CalculateSizeLimit(settlement);

    //            var result = new ExplainedNumber(totalLimit, includeDescriptions);
    //            result.Add(totalLimit, new TextObject("{=Fief_Party_Size_Limit}Fief Party Size Limit"), null);

    //            return result;
    //        }

    //        return base.GetPartyMemberSizeLimit(party, includeDescriptions);
    //    }
    //}
}
