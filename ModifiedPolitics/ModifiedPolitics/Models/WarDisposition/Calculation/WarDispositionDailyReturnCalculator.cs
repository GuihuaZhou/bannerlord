using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ModifiedPolitics.Models.WarDisposition.Calculation
{
    /// <summary>
    /// 计算 Clan 当前战争持续时间和每日固定回归量.
    /// 多场战争并存时使用持续最久的一场, 代表当前政治势力的战争负担.
    /// </summary>
    public static class WarDispositionDailyReturnCalculator
    {
        public static float GetLongestActiveWarDurationDays(
            Clan clan,
            float unknownWarFallbackDays = 0f)
        {
            IFaction faction = clan?.MapFaction;

            if (faction == null)
            {
                return 0f;
            }

            float longestDuration = 0f;

            foreach (IFaction enemy in faction.FactionsAtWarWith)
            {
                if (enemy == null
                    || enemy.IsBanditFaction
                    || enemy.IsMinorFaction
                    || enemy.IsEliminated)
                {
                    continue;
                }

                StanceLink stance = faction.GetStanceWith(enemy);

                if (stance == null || !stance.IsAtWar)
                {
                    continue;
                }

                // 中途安装 Mod 或旧版本存档中的部分战争关系可能没有可靠的
                // WarStartDate. CampaignTime.Zero 会被解释为游戏历法零点,
                // 从而产生十万天级别的伪持续时间, 必须视为未知而不是长期战争.
                if (stance.WarStartDate == CampaignTime.Zero)
                {
                    longestDuration = MathF.Max(
                        longestDuration,
                        unknownWarFallbackDays);
                    continue;
                }

                float duration = stance.WarStartDate.ElapsedDaysUntilNow;

                // 战争不可能早于本局战役开始; 同时排除未来日期和损坏数据.
                float campaignAge = Campaign.Current.Models
                    .CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;

                if (duration < 0f || duration > campaignAge + 1f)
                {
                    longestDuration = MathF.Max(
                        longestDuration,
                        unknownWarFallbackDays);
                    continue;
                }

                if (duration > longestDuration)
                {
                    longestDuration = duration;
                }
            }

            return longestDuration;
        }

        public static float GetReturnAmount(float warDurationDays)
        {
            if (warDurationDays <= 30f)
            {
                return 1f;
            }

            if (warDurationDays <= 90f)
            {
                return 2f;
            }

            if (warDurationDays <= 180f)
            {
                return 4f;
            }

            return 6f;
        }
    }
}
