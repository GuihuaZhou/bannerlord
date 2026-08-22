using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models
{
    public class NewSettlementLoyaltyModel : DefaultSettlementLoyaltyModel
    {
        public override float SettlementOwnerDifferentCultureLoyaltyEffect
        {
            get
            {
                return -2f;
            }
        }
    }
}
