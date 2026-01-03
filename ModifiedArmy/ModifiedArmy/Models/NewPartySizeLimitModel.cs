using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
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
    }
}
