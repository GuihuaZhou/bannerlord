using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace ModifiedArmy.Models
{
    public class NewPrisonerRecruitmentCalculationModel : DefaultPrisonerRecruitmentCalculationModel
    {
        // ========== 配置常量 ==========
        /// <summary>
        /// 俘虏招募费用的折扣系数。0.8 表示为原价的 80%。
        /// </summary>
        private const float PrisonerRecruitmentCostFactor = 0.8f;

        // ========== 重写方法：计算所需驯服度 ==========
        public override int GetConformityNeededToRecruitPrisoner(CharacterObject character)
        {
            if (character == null || character.IsHero)
                return base.GetConformityNeededToRecruitPrisoner(character);

            // 1. 雇佣兵：更容易
            if (character.Occupation == Occupation.Mercenary)
            {
                return Math.Max(0, (character.Level + 3) * (character.Level + 3) - 10);
            }

            // 2. 正规军直接按游戏实际 Tier 决定偏移。
            // Tier 1-2 为普通兵，Tier 3-4 为受训兵，Tier 5+ 为精锐兵。
            if (character.Occupation == Occupation.Soldier)
            {
                int levelOffset = character.Tier >= 5
                    ? 25
                    : character.Tier >= 3 ? 15 : 6;

                return Math.Max(0, (character.Level + levelOffset) * (character.Level + levelOffset) - 10);
            }

            return base.GetConformityNeededToRecruitPrisoner(character);
        }

        // ========== 重写方法：计算士气影响 ==========
        public override int GetPrisonerRecruitmentMoraleEffect(PartyBase party, CharacterObject character, int num)
        {
            // 保留原版的士气计算逻辑
            return base.GetPrisonerRecruitmentMoraleEffect(party, character, num);
        }

        // ========== 新增方法：计算招募金币成本 ==========
        /// <summary>
        /// 计算招募指定数量的俘虏所需的金币总数。
        /// 复用 PartyWageModel 的招募费用逻辑，并应用折扣。
        /// </summary>
        public int CalculateGoldCostForRecruitment(CharacterObject character, int count, Hero buyerHero = null)
        {
            if (character == null || count <= 0)
                return 0;

            // 1. 使用当前生效的 PartyWageModel 获取标准招募费用
            ExplainedNumber recruitmentCost = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(character, buyerHero, false);

            // 2. 应用俘虏招募折扣
            float discountedCost = recruitmentCost.ResultNumber * PrisonerRecruitmentCostFactor;

            // 3. 计算总费用并返回整数
            return (int)MathF.Round(discountedCost * count);
        }
    }
}
