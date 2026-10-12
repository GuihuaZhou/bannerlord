using System;
using System.Collections.Generic;
using ModifiedPolitics.Tool;
using ModifiedPolitics.Models.WarDisposition.Calculation;
using ModifiedPolitics.Models.WarDisposition.Events;
using ModifiedPolitics.Models.WarDisposition.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.Models.WarDisposition
{
    /// <summary>
    /// 管理所有 Clan 的战争倾向持久化数据。
    /// 事件监听层通过统一入口提交事件，管理器负责计算、累计和保存。
    /// </summary>
    public sealed class WarDispositionManager : CampaignBehaviorBase
    {
        private const string SaveKey =
            "_modifiedPoliticsWarDispositionData";
        private const string WarTrackingStartTimeSaveKey =
            "_modifiedPoliticsWarDispositionTrackingStartTime";

        [SaveableField(1)]
        private Dictionary<Clan, WarDispositionData> _clanData =
            new Dictionary<Clan, WarDispositionData>();

        private CampaignTime _warTrackingStartTime = CampaignTime.Zero;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _clanData);
            dataStore.SyncData(
                WarTrackingStartTimeSaveKey,
                ref _warTrackingStartTime);

            if (_clanData == null)
            {
                _clanData = new Dictionary<Clan, WarDispositionData>();
            }

            EnsureWarTrackingStartTime();
        }

        public WarDispositionData GetOrCreateData(Clan clan)
        {
            if (!WarDispositionClanEligibility.IsEligible(clan))
            {
                return null;
            }

            if (_clanData == null)
            {
                _clanData = new Dictionary<Clan, WarDispositionData>();
            }

            if (!_clanData.TryGetValue(clan, out WarDispositionData data)
                || data == null)
            {
                data = new WarDispositionData(GetInitialWealth(clan));
                _clanData[clan] = data;
            }

            return data;
        }

        public bool TryGetData(
            Clan clan,
            out WarDispositionData data)
        {
            data = null;

            return WarDispositionClanEligibility.IsEligible(clan)
                && _clanData != null
                && _clanData.TryGetValue(clan, out data)
                && data != null;
        }

        /// <summary>
        /// 获取当前政治势力持续最久的战争天数.
        /// 旧存档缺少本体开战日期时, 从本 Mod 首次跟踪时间开始估算.
        /// </summary>
        public float GetLongestActiveWarDurationDays(Clan clan)
        {
            EnsureWarTrackingStartTime();

            float fallbackDays = _warTrackingStartTime == CampaignTime.Zero
                ? 0f
                : Math.Max(0f, _warTrackingStartTime.ElapsedDaysUntilNow);

            return WarDispositionDailyReturnCalculator
                .GetLongestActiveWarDurationDays(clan, fallbackDays);
        }

        public bool IsInFormalWar(Clan clan)
        {
            return WarDispositionDailyReturnCalculator.HasActiveFormalWar(clan);
        }

        /// <summary>
        /// Returns completed peace days. Old saves begin tracking when peace is first observed.
        /// </summary>
        public int GetPeaceDurationDays(Clan clan)
        {
            WarDispositionData data = GetOrCreateData(clan);
            if (data == null || IsInFormalWar(clan))
                return 0;

            EnsurePeaceStartTime(data);
            return data.PeaceStartTime == CampaignTime.Zero
                ? 0
                : Math.Max(0, (int)Math.Floor(data.PeaceStartTime.ElapsedDaysUntilNow));
        }

        /// <summary>
        /// 将一次战争事件应用到指定 Clan。
        /// 本方法是事件监听器唯一应调用的数值入口。
        /// </summary>
        /// <param name="targetClan">正在形成战争态度的 Clan。</param>
        /// <param name="eventType">实际发生的事件类型。</param>
        /// <param name="isOwnClanEvent">
        /// true 表示事件发生在 targetClan；false 表示发生在同王国其他 Clan。
        /// </param>
        /// <param name="baseValueOverride">
        /// 财富变化等动态事件使用的基础值；普通事件保持为 null。
        /// </param>
        public WarDispositionCalculationResult ApplyEvent(
            Clan targetClan,
            WarDispositionEventType eventType,
            bool isOwnClanEvent,
            float? baseValueOverride = null)
        {
            WarDispositionData data = GetOrCreateData(targetClan);

            if (data == null)
            {
                return null;
            }

            WarDispositionCalculationResult result =
                WarDispositionCalculator.Calculate(
                    targetClan,
                    eventType,
                    isOwnClanEvent,
                    baseValueOverride);

            float previousValue = data.Value;
            data.Value = WarDispositionCalculator.ClampDisposition(
                previousValue + result.CalculatedDelta);

            // 靠近边界时，实际写入值可能小于公式结果。
            // UI 分类汇总必须记录实际变化，不能记录被截断的部分。
            result.AppliedDelta = data.Value - previousValue;
            AddCategoryInfluence(data, result);

            return result;
        }

        private static void AddCategoryInfluence(
            WarDispositionData data,
            WarDispositionCalculationResult result)
        {
            switch (result.Category)
            {
                case WarDispositionInfluenceCategory.Battle:
                    data.TodayBattleInfluence += result.AppliedDelta;
                    break;

                case WarDispositionInfluenceCategory.Territory:
                    data.TodayTerritoryInfluence += result.AppliedDelta;
                    break;

                case WarDispositionInfluenceCategory.WarGain:
                    data.TodayWarGainInfluence += result.AppliedDelta;
                    break;

                case WarDispositionInfluenceCategory.Economy:
                    data.WeeklyWealthInfluence += result.AppliedDelta;
                    break;
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            EnsureWarTrackingStartTime();

            foreach (Clan clan in Clan.All)
            {
                WarDispositionData data = GetOrCreateData(clan);
                if (data == null)
                    continue;

                if (IsInFormalWar(clan))
                    data.PeaceStartTime = CampaignTime.Zero;
                else
                    EnsurePeaceStartTime(data);
            }
        }

        private void OnDailyTick()
        {
            int processedClanCount = 0;
            float totalAbsoluteTrend = 0f;

            foreach (Clan clan in Clan.All)
            {
                if (!WarDispositionClanEligibility.IsEligible(clan))
                {
                    continue;
                }

                WarDispositionData data = GetOrCreateData(clan);

                if (data == null)
                {
                    continue;
                }

                // DailyTick 表示新一天开始; 先清空上一日的 UI 分类汇总.
                ResetDailyInfluences(data);

                float previousValue = data.Value;
                bool atWar = IsInFormalWar(clan);
                float baseline = WarDispositionCalculator.CalculateBaseline(clan);
                float warDuration = 0f;
                int peaceDays = 0;
                float recoveryRate = 0f;
                float requestedChange;

                if (atWar)
                {
                    data.PeaceStartTime = CampaignTime.Zero;
                    warDuration = GetLongestActiveWarDurationDays(clan);
                    requestedChange = WarDispositionDailyReturnCalculator
                        .GetWarDailyChange(previousValue);
                }
                else
                {
                    EnsurePeaceStartTime(data);
                    peaceDays = GetPeaceDurationDays(clan);
                    recoveryRate = WarDispositionDailyReturnCalculator
                        .GetPeaceRecoveryRate(peaceDays);
                    requestedChange = WarDispositionDailyReturnCalculator
                        .GetPeaceDailyChange(previousValue, baseline, peaceDays);
                }

                data.Value = WarDispositionCalculator.ClampDisposition(
                    previousValue + requestedChange);

                if (!atWar && Math.Abs(baseline - data.Value) <= 1f)
                    data.Value = baseline;

                float actualChange = data.Value - previousValue;
                processedClanCount++;
                totalAbsoluteTrend += Math.Abs(actualChange);

                if (clan == Clan.PlayerClan)
                {
                    ModLogger.Info(
                        $"[战争倾向] 每日趋势 | 家族={clan.Name} | " +
                        $"状态={(atWar ? "战争" : "和平")} | " +
                        $"战争持续={warDuration:0.0}天 | 和平持续={peaceDays}天 | " +
                        $"人格基准={baseline:0.00} | 恢复比例={recoveryRate:P1} | " +
                        $"变化前={previousValue:0.00} | " +
                        $"实际变化={actualChange:+0.00;-0.00;0.00} | " +
                        $"变化后={data.Value:0.00}");
                }
            }

            ModLogger.Info(
                $"[战争倾向] 每日趋势完成 | 有效家族={processedClanCount} | " +
                $"绝对变化总量={totalAbsoluteTrend:0.00}");
        }

        private static void ResetDailyInfluences(WarDispositionData data)
        {
            data.TodayBattleInfluence = 0f;
            data.TodayTerritoryInfluence = 0f;
            data.TodayWarGainInfluence = 0f;
        }

        private void EnsureWarTrackingStartTime()
        {
            if (_warTrackingStartTime == CampaignTime.Zero
                && Campaign.Current != null)
            {
                _warTrackingStartTime = CampaignTime.Now;
            }
        }

        private static void EnsurePeaceStartTime(WarDispositionData data)
        {
            if (data != null
                && data.PeaceStartTime == CampaignTime.Zero
                && Campaign.Current != null)
                data.PeaceStartTime = CampaignTime.Now;
        }

        private static int GetInitialWealth(Clan clan)
        {
            long wealth = clan?.Gold ?? 0;

            if (wealth > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (wealth < 0)
            {
                return 0;
            }

            return (int)wealth;
        }
    }
}
