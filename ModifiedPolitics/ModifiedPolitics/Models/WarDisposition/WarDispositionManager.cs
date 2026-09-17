using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.Models.WarDisposition
{
    /// <summary>
    /// 管理所有 Clan 的战争倾向持久化数据。
    /// 本阶段只负责初始化、查询和保存，不处理战争事件。
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
