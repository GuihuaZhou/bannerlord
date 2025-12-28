using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace ModifiedArmy.Models
{
    public class NewClanTierModel : DefaultClanTierModel
    {
        private void AddPartyLimitPerkEffects(Clan clan, ref ExplainedNumber result)
        {
            if (clan.Leader != null && clan.Leader.GetPerkValue(DefaultPerks.Leadership.TalentMagnet))
            {
                result.Add(DefaultPerks.Leadership.TalentMagnet.SecondaryBonus, DefaultPerks.Leadership.TalentMagnet.Name, null);
            }
        }

        public override int GetPartyLimitForTier(Clan clan, int clanTierToCheck)
        {
            ExplainedNumber explainedNumber = new ExplainedNumber(0f, false, null);

            if (!clan.IsMinorFaction)
            {
                // 根据拥有的定居点数量（城镇+城堡）决定基础分队数
                int settlementCount = clan.Settlements.Count;

                if (settlementCount <= 1)
                    explainedNumber.Add(1f, null, null);
                else if (settlementCount <= 3)
                    explainedNumber.Add(2f, null, null);
                else
                    explainedNumber.Add(3f, null, null);
            }
            else
            {
                // 次要派系保持原逻辑（基于 clanTierToCheck）
                explainedNumber.Add(MathF.Clamp((float)clanTierToCheck, 1f, 4f), null, null);
            }

            // 保留原版特性加成（如 TalentMagnet）
            AddPartyLimitPerkEffects(clan, ref explainedNumber);

            return MathF.Round(explainedNumber.ResultNumber);
        }
    }
}
