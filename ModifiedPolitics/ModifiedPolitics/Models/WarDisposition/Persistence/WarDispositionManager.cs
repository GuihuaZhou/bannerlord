using System.Collections.Generic;
using ModifiedPolitics.Models.WarDisposition.Calculation;
using ModifiedPolitics.Models.WarDisposition.Events;
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

        [SaveableField(1)]
        private Dictionary<Clan, WarDispositionData> _clanData =
            new Dictionary<Clan, WarDispositionData>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _clanData);

            if (_clanData == null)
            {
                _clanData = new Dictionary<Clan, WarDispositionData>();
            }
        }

        public WarDispositionData GetOrCreateData(Clan clan)
        {
            if (clan == null)
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

            return clan != null
                && _clanData != null
                && _clanData.TryGetValue(clan, out data)
                && data != null;
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
            foreach (Clan clan in Clan.All)
            {
                GetOrCreateData(clan);
            }
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
