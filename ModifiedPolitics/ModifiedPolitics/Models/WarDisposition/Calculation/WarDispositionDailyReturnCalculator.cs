using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.Models.WarDisposition.Calculation
{
    /// <summary>
    /// 计算 Clan 当前战争持续时间和每日回归率。
    /// 多场战争并存时使用持续最久的一场，代表当前政治势力的战争负担。
    /// </summary>
    public static class WarDispositionDailyReturnCalculator
    {
        public static float GetLongestActiveWarDurationDays(Clan clan)
        {
            IFaction faction = clan?.MapFaction;

            if (faction == null)
            {
                return 0f;
            }

            float longestDuration = 0f;

            foreach (IFaction enemy in faction.FactionsAtWarWith)
            {
                if (enemy == null || enemy.IsBanditFaction || enemy.IsEliminated)
                {
                    continue;
                }

                StanceLink stance = faction.GetStanceWith(enemy);

                if (stance == null || !stance.IsAtWar)
                {
                    continue;
                }

                float duration = stance.WarStartDate.ElapsedDaysUntilNow;

                if (duration > longestDuration)
                {
                    longestDuration = duration;
                }
            }

            return longestDuration;
        }

        public static float GetReturnRate(float warDurationDays)
        {
            if (warDurationDays <= 30f)
            {
                return 0.01f;
            }

            if (warDurationDays <= 90f)
            {
                return 0.02f;
            }

            if (warDurationDays <= 180f)
            {
                return 0.04f;
            }

            return 0.06f;
        }
    }
}
