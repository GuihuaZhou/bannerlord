namespace ModifiedPolitics.Models.WarDisposition.Calculation
{
    /// <summary>
    /// 将 Clan 每周财富变化比例转换为事件基础值.
    /// 精确落在 5%, 10%, 20% 和 50% 边界时进入影响更强的档位.
    /// </summary>
    public static class WarDispositionWealthCalculator
    {
        public static float GetEventBaseValue(float changePercent)
        {
            if (changePercent >= 50f)
            {
                return 12f;
            }

            if (changePercent >= 20f)
            {
                return 8f;
            }

            if (changePercent >= 10f)
            {
                return 4f;
            }

            if (changePercent >= 5f)
            {
                return 2f;
            }

            if (changePercent <= -50f)
            {
                return -12f;
            }

            if (changePercent <= -20f)
            {
                return -8f;
            }

            if (changePercent <= -10f)
            {
                return -4f;
            }

            if (changePercent <= -5f)
            {
                return -2f;
            }

            return 0f;
        }

        public static float CalculateChangePercent(
            int previousWealth,
            int currentWealth)
        {
            if (previousWealth <= 0)
            {
                return currentWealth > 0 ? 100f : 0f;
            }

            return (currentWealth - previousWealth)
                * 100f
                / previousWealth;
        }
    }
}
