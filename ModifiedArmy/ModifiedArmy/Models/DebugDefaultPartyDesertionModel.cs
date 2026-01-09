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
            // === 开始日志：记录 party 基本状态 ===
            InformationManager.DisplayMessage(new InformationMessage(
                $"[Desertion Debug] Party: '{mobileParty.Name}' | " +
                $"IsGarrison: {mobileParty.IsGarrison} | " +
                $"Morale: {mobileParty.Morale:F1} | " +
                $"TotalMembers: {mobileParty.Party.NumberOfAllMembers} | " +
                $"PartySizeLimit: {mobileParty.Party.PartySizeLimit} | " +
                $"HasUnpaidWages: {mobileParty.HasUnpaidWages:F2} | " +
                $"PaymentLimit: {mobileParty.PaymentLimit} | " +
                $"TotalWage: {mobileParty.TotalWage}"
            ));

            TroopRoster troopsToDesert = TroopRoster.CreateDummyTroopRoster();

            // ========== 复制原版 GetTroopsToDesertDueToMorale 逻辑 + 日志 ==========
            {
                int maxDeserters = (int)((float)mobileParty.Party.NumberOfRegularMembers * CalculateDesertionChanceFromTroopLevel(mobileParty.Morale, 20));
                InformationManager.DisplayMessage(new InformationMessage($"  [Morale Path] Max deserters from morale: {maxDeserters}"));

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

                InformationManager.DisplayMessage(new InformationMessage(
                    $"  [Wage/Size Path] ExcessMembers: {excessMembers}, UnpaidWage: {unpaidWage:F2}"
                ));

                if (mobileParty.HasLimitedWage() && mobileParty.PaymentLimit < unpaidWage)
                {
                    int deficit = mobileParty.TotalWage - mobileParty.PaymentLimit;
                    wageDesertion = MathF.Min(20, MathF.Max(1, (int)((float)deficit / Campaign.Current.AverageWage * 0.25f)));
                    InformationManager.DisplayMessage(new InformationMessage($"    → Wage desertion count: {wageDesertion} (deficit={deficit})"));
                }

                if (excessMembers > 0)
                {
                    sizeDesertion = MathF.Max(1, (int)(excessMembers * 0.25f));
                    InformationManager.DisplayMessage(new InformationMessage($"    → Size desertion count: {sizeDesertion} (excess={excessMembers})"));
                }

                int baseDesertion = MathF.Max(wageDesertion, sizeDesertion);

                // ⚠️ 原版驻军欠薪逻辑（关键！）
                if (mobileParty.IsGarrison && mobileParty.HasUnpaidWages > 0f)
                {
                    int garrisonBonus = MathF.Min(mobileParty.Party.NumberOfHealthyMembers, 5);
                    baseDesertion += garrisonBonus;
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"    → Garrison unpaid wages! Adding {garrisonBonus} deserters (HasUnpaidWages={mobileParty.HasUnpaidWages:F2})"
                    ));
                }

                baseDesertion = MathF.Min(baseDesertion, mobileParty.MemberRoster.TotalRegulars);
                InformationManager.DisplayMessage(new InformationMessage($"    → Final desertion count (wage/size): {baseDesertion}"));

                if (baseDesertion > 0)
                {
                    SelectTroopsForDesertion(mobileParty, troopsToDesert, baseDesertion, false);
                }
            }

            // === 结果汇总 ===
            if (troopsToDesert.TotalManCount > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Desertion Result] {troopsToDesert.TotalManCount} troops will desert from '{mobileParty.Name}'"
                ));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Desertion Result] No desertion for '{mobileParty.Name}'"
                ));
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
