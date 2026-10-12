using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ModifiedPolitics.Models.WarDisposition.Calculation
{
    /// <summary>
    /// Calculates formal-war duration and daily war/peace disposition trends.
    /// </summary>
    public static class WarDispositionDailyReturnCalculator
    {
        public const float WarWearinessRate = 0.01f;
        public const float InitialPeaceRecoveryRate = 0.005f;
        public const float PeaceRecoveryRatePerDay = 0.002f;
        public const float MaximumPeaceRecoveryRate = 0.06f;

        public static bool HasActiveFormalWar(Clan clan)
        {
            IFaction faction = clan?.MapFaction;
            if (faction == null)
                return false;

            foreach (IFaction enemy in faction.FactionsAtWarWith)
            {
                if (IsFormalKingdomWar(faction, enemy))
                    return true;
            }

            return false;
        }

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
                if (!IsFormalKingdomWar(faction, enemy))
                    continue;

                StanceLink stance = faction.GetStanceWith(enemy);

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

        public static float GetWarDailyChange(float currentDisposition)
        {
            return (WarDispositionCalculator.MinimumDisposition - currentDisposition)
                   * WarWearinessRate;
        }

        public static float GetPeaceRecoveryRate(int peaceDays)
        {
            return MathF.Min(
                InitialPeaceRecoveryRate
                + PeaceRecoveryRatePerDay * MathF.Max(0, peaceDays),
                MaximumPeaceRecoveryRate);
        }

        public static float GetPeaceDailyChange(
            float currentDisposition,
            float baseline,
            int peaceDays)
        {
            if (MathF.Abs(baseline - currentDisposition) <= 1f)
                return baseline - currentDisposition;

            return (baseline - currentDisposition)
                   * GetPeaceRecoveryRate(peaceDays);
        }

        private static bool IsFormalKingdomWar(IFaction faction, IFaction enemy)
        {
            if (faction == null
                || enemy == null
                || enemy.IsBanditFaction
                || enemy.IsMinorFaction
                || !enemy.IsKingdomFaction
                || enemy.IsEliminated)
                return false;

            StanceLink stance = faction.GetStanceWith(enemy);
            return stance != null && stance.IsAtWar;
        }
    }
}
