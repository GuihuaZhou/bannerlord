using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition.Rules;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.Models.WarDisposition.Decisions
{
    /// <summary>
    /// 将 Clan 的战争倾向转换为战争类决议支持度修正.
    /// 根据本体支持度的量级计算百分比修正, 支持战争时使用正值, 反对战争时使用相反值.
    /// </summary>
    public static class WarDispositionDecisionSupport
    {
        // 本体停战决议主要使用 0 和 200 两档分数. 最低基数保证低分与零分结果也会受到倾向影响.
        private const float MinimumSupportScale = 200f;

        public static void Apply(
            Clan clan,
            bool outcomeSupportsWar,
            string decisionType,
            string outcomeType,
            ref float support)
        {
            if (!WarDispositionClanEligibility.IsEligible(clan)
                || Campaign.Current == null)
            {
                ModLogger.Info(
                    $"[战争倾向] 跳过决议支持度修正 | 决议={decisionType} | " +
                    $"家族={clan?.Name} | 原因=Clan无资格或Campaign不可用");
                return;
            }

            WarDispositionManager manager = Campaign.Current
                .GetCampaignBehavior<WarDispositionManager>();

            if (manager == null
                || !manager.TryGetData(clan, out WarDispositionData data))
            {
                ModLogger.Info(
                    $"[战争倾向] 跳过决议支持度修正 | 决议={decisionType} | " +
                    $"家族={clan.Name} | 原因=战争倾向数据不可用");
                return;
            }

            float originalSupport = support;
            float supportScale = System.Math.Max(
                System.Math.Abs(originalSupport),
                MinimumSupportScale);
            float dispositionRate = data.Value / 100f;
            float adjustment = supportScale * dispositionRate;
            if (!outcomeSupportsWar)
            {
                adjustment = -adjustment;
            }

            support += adjustment;

            ModLogger.Notice(
                $"[战争倾向] 决议支持度 | 决议={decisionType} | " +
                $"家族={clan.Name} | 结果={outcomeType} | " +
                $"本体分数={originalSupport:0.00} | " +
                $"倾向={data.Value:+0.00;-0.00;0.00} | " +
                $"修正基数={supportScale:0.00} | " +
                $"修正={adjustment:+0.00;-0.00;0.00} | " +
                $"最终分数={support:0.00}");
        }
    }
}
