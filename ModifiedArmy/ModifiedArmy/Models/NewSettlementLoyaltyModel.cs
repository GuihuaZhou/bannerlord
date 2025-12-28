using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Models
{
    public class NewSettlementLoyaltyModel : DefaultSettlementLoyaltyModel
    {
        public override float SettlementOwnerDifferentCultureLoyaltyEffect
        {
            get
            {
                return -3f;
            }
        }
    }
}
