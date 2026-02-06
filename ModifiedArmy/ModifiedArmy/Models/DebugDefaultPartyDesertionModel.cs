using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;

namespace ModifiedArmy.Models
{
    public class DebugDefaultPartyDesertionModel : DefaultPartyDesertionModel
    {
        public override TroopRoster GetTroopsToDesert(MobileParty mobileParty)
        {
            //// === 开始日志：记录 party 基本状态 ===
            StringBuilder logBuilder = new StringBuilder();

            TroopRoster troopsToDesert = TroopRoster.CreateDummyTroopRoster();

            // ========== 复制原版 GetTroopsToDesertDueToMorale 逻辑 + 日志 ==========
            {
                int maxDeserters = (int)((float)mobileParty.Party.NumberOfRegularMembers * CalculateDesertionChanceFromTroopLevel(mobileParty.Morale, 20));
                logBuilder.AppendLine($"因士气导致的最大逃兵数: {maxDeserters}");

                if (maxDeserters > 0)
                {
                    SelectTroopsForDesertion(mobileParty, troopsToDesert, maxDeserters, true);
                }
            }

            // ========== 复制原版 GetTroopsToDesertDueToWageAndPartySize 逻辑 + 日志 ==========
            {
                int wageDesertion = 0;
                int sizeDesertion = 0;

                int excessMembers = mobileParty.Party.NumberOfAllMembers - troopsToDesert.TotalManCount - mobileParty.Party.PartySizeLimit;
                float currentWage = Campaign.Current.Models.PartyWageModel.GetTotalWage(mobileParty, troopsToDesert, false).ResultNumber;
                float unpaidWage = mobileParty.TotalWage - currentWage;

                logBuilder.AppendLine($"超编人数: {excessMembers}, 未支付工资: {unpaidWage:F2}");

                if (mobileParty.HasLimitedWage() && mobileParty.PaymentLimit < unpaidWage)
                {
                    int deficit = mobileParty.TotalWage - mobileParty.PaymentLimit;
                    wageDesertion = MathF.Min(20, MathF.Max(1, (int)((float)deficit / Campaign.Current.AverageWage * 0.25f)));
                    logBuilder.AppendLine($" → 因工资不足的逃兵数: {wageDesertion} (赤字={deficit})");
                }

                if (excessMembers > 0)
                {
                    sizeDesertion = MathF.Max(1, (int)(excessMembers * 0.25f));
                    logBuilder.AppendLine($" → 因超编的逃兵数: {sizeDesertion} (超编={excessMembers})");
                }

                int baseDesertion = MathF.Max(wageDesertion, sizeDesertion);

                // 驻军欠薪逻辑
                if (mobileParty.IsGarrison && mobileParty.HasUnpaidWages > 0f)
                {
                    int garrisonBonus = MathF.Min(mobileParty.Party.NumberOfHealthyMembers, 5);
                    baseDesertion += garrisonBonus;
                    logBuilder.AppendLine($" → 驻军且有欠薪！额外增加 {garrisonBonus} 名逃兵 (欠薪额={mobileParty.HasUnpaidWages:F2})");
                }

                baseDesertion = MathF.Min(baseDesertion, mobileParty.MemberRoster.TotalRegulars);
                logBuilder.AppendLine($" → 工资/规模导致的最终逃兵数: {baseDesertion}");

                if (baseDesertion > 0)
                {
                    SelectTroopsForDesertion(mobileParty, troopsToDesert, baseDesertion, false);
                }
            }

            // === 结果汇总 ===
            if (mobileParty.IsGarrison && troopsToDesert.TotalManCount > 0)
            {
                string basicInfo = $"[逃兵调试] 部队: '{mobileParty.Name}' | " +
                                   $"士气: {mobileParty.Morale:F1} | " +
                                   $"总成员数: {mobileParty.Party.NumberOfAllMembers} | " +
                                   $"部队规模上限: {mobileParty.Party.PartySizeLimit} | " +
                                   $"欠薪: {mobileParty.HasUnpaidWages:F2} | " +
                                   $"支付上限: {mobileParty.PaymentLimit} | " +
                                   $"总工资: {mobileParty.TotalWage}";
                ModLogger.Info(basicInfo);

                ModLogger.Info(logBuilder.ToString());
            }

            return troopsToDesert;
        }

        // 注意：以下辅助方法在 DefaultPartyDesertionModel 中是 protected 或可访问的，
        // 所以我们可以直接调用，无需重写。
        // 如果编译报错找不到，说明它们是 private —— 那我们就必须内联或复制。

        // 实际上，在 Bannerlord 的 DefaultPartyDesertionModel 中：
        // - CalculateDesertionChanceFromTroopLevel 是 private
        // - SelectTroopsForDesertion 是 private
        //
        // 因此，为了 100% 兼容，我们必须复制这两个方法！

        // ========== 复制原版 private 方法 ==========

        private float CalculateDesertionChanceFromTroopLevel(float partyMorale, int level)
        {
            int threshold = GetMoraleThresholdForTroopDesertion();
            float effectiveMorale = MathF.Min(partyMorale, (float)threshold);
            float ratio = ((float)threshold - effectiveMorale) / (float)threshold;
            return 1f - MathF.Pow(level * 0.01f, 0.1f * ratio);
        }

        private void SelectTroopsForDesertion(MobileParty mobileParty, TroopRoster troopsToDesert, int maxDesertionCount, bool useProbability)
        {
            int selected = 0;
            for (int i = mobileParty.MemberRoster.Count - 1; i >= 0 && selected < maxDesertionCount; i--)
            {
                TroopRosterElement troopRosterElement = mobileParty.MemberRoster.GetElementCopyAtIndex(i);
                if (troopRosterElement.Character.HeroObject != null)
                {
                    continue;
                }
                int num = 0;
                int num2 = 0;
                float desertionChanceForTroop = useProbability ? GetDesertionChanceForTroop(mobileParty, troopRosterElement) : 1f;
                int troopCount = troopsToDesert.GetTroopCount(troopRosterElement.Character);
                for (int j = 0; j < troopRosterElement.WoundedNumber - troopCount && selected + num2 < maxDesertionCount; j++)
                {
                    if (!useProbability || desertionChanceForTroop > mobileParty.RandomFloatWithSeed((uint)(CampaignTime.Now.ToHours + (long)(i * 100 + j))))
                    {
                        num2++;
                    }
                }
                int num3 = troopRosterElement.Number - troopRosterElement.WoundedNumber - troopCount;
                for (int k = 0; k < num3 && selected + num2 + num < maxDesertionCount; k++)
                {
                    if (!useProbability || desertionChanceForTroop > mobileParty.RandomFloatWithSeed((uint)(CampaignTime.Now.ToHours + (long)(i * 100 + k))))
                    {
                        num++;
                    }
                }
                if (num + num2 > 0)
                {
                    troopsToDesert.AddToCounts(troopRosterElement.Character, num + num2, false, num2, 0, true, -1);
                    selected += num + num2;
                }
            }
        }
    }
}
