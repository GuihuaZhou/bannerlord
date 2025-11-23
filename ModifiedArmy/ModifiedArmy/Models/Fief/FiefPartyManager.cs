using ModifiedArmy.Patches;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    
    /// <summary>
    /// Saveable type definer for Fief Squad system.
    /// Ensures proper serialization of settlement and roster lists.
    /// </summary>
    public class FiefSaveDefiner : SaveableTypeDefiner
    {
        public FiefSaveDefiner() : base(20251116) { }

        protected override void DefineClassTypes() 
        {
            base.AddClassDefinition(typeof(FiefTroopDetachment), 1);
            base.AddClassDefinition(typeof(FiefPartyData), 2);
        }

        protected override void DefineContainerDefinitions()
        {
            base.ConstructContainerDefinition(typeof(Dictionary<CharacterObject, int>));
            base.ConstructContainerDefinition(typeof(List<FiefTroopDetachment>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, FiefPartyData>));
        }
    }

    /// <summary>
    /// Campaign behavior that manages fief squads across all player-owned settlements.
    /// Handles initialization, daily updates, saving/loading, and public APIs for recruitment/disbanding.
    /// </summary>
    public class FiefPartyManager : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private Dictionary<Settlement, FiefPartyData> _fiefDataMap = new Dictionary<Settlement, FiefPartyData>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<Dictionary<Settlement, FiefPartyData>>("_fiefDataMap", ref this._fiefDataMap);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SoldierTypeClassifier.InitializeAll();
            InitializeFiefData();
        }

        /// <summary>
        /// Initializes fief data for all player-owned villages, castles, and towns.
        /// Also restores saved squad compositions from loaded data.
        /// </summary>
        public void InitializeFiefData()
        {
            foreach (var settlement in Settlement.All)
            {
                if (settlement.OwnerClan != Clan.PlayerClan) continue;
                if (!settlement.IsCastle && !settlement.IsTown) continue;
                if (_fiefDataMap.TryGetValue(settlement, out var existingData))
                {
                    ModLogger.Debug($"[InitializeFiefData] Refreshing existing data for {settlement.Name}");
                    existingData.Reflush();
                }
                else
                {
                    ModLogger.Info($"[InitializeFiefData] Creating new fief data for {settlement.Name}");
                    _fiefDataMap[settlement] = new FiefPartyData(settlement);
                }
            }
            ModLogger.Info($"[Fief Squad Mod] Initialized {_fiefDataMap.Count} fiefs.");
        }

        private void OnDailyTick()
        {
            int updatedCount = 0;
            foreach (var data in _fiefDataMap.Values)
            {
                data?.WeeklyUpdate();
                updatedCount++;
            }
            ModLogger.Debug($"[Fief Squad Mod] Daily reinforcement update completed for {updatedCount} settlements.");
        }

        /// <summary>
        /// Retrieves fief data for a specific settlement.
        /// </summary>
        public FiefPartyData GetFiefData(Settlement settlement)
        {
            return _fiefDataMap.TryGetValue(settlement, out var data) ? data : null;
        }

        /// <summary>
        /// Gets all fief data mappings.
        /// </summary>
        public Dictionary<Settlement, FiefPartyData> GetAllFiefData()
        {
            return _fiefDataMap;
        }

        /// <summary>
        /// Returns the combined troop roster of all fief squads in a settlement.
        /// </summary>
        public TroopRoster GetFiefTroopRoster(Settlement settlement)
        {
            if (settlement == null) return TroopRoster.CreateDummyTroopRoster();
            if (_fiefDataMap.TryGetValue(settlement, out FiefPartyData data) && data != null)
            {
                return data.FiefTroops;
            }
            return TroopRoster.CreateDummyTroopRoster();
        }

        /// <summary>
        /// Recruits all available fief squads from a settlement into the target mobile party.
        /// </summary>
        public void RecruitFiefTroopsFromSettlement(Settlement settlement, MobileParty targetParty)
        {
            if (settlement == null || targetParty == null)
            {
                ModLogger.Error("[Fief] Recruitment failed: invalid parameters.");
                return;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data))
            {
                data.RecruitTroopsToParty(targetParty);
            }
        }

        /// <summary>
        /// Disbands all fief squads in a settlement.
        /// Optionally recovers troops from a mobile party to refill squads.
        /// </summary>
        public void DisbandAndReturnTroops(Settlement settlement, MobileParty mobileParty = null)
        {
            if (settlement == null)
            {
                ModLogger.Error("[Fief] Disband failed: settlement is null.");
                return;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data))
            {
                data.DisbandAndReturnTroops(mobileParty);
            }
        }

        /// <summary>
        /// 获取指定封邑当前可招募的军队人数（即就绪状态的健康士兵总数）。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>可招募人数，若封邑无效或非玩家所有则返回 0</returns>
        public int GetAvailableRecruitCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.FiefTroops?.TotalManCount ?? 0;
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑当前已被征召的士兵人数（即处于 RecruitedTroops 中的士兵总数）。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>已被征召人数，若无征召或封邑无效则返回 0</returns>
        public int GetRecruitedTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.RecruitedTroops?.GetTotalCount() ?? 0;
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑当前处于解散期的士兵总人数（即 ReturnedTroopDetachmentList 中所有分遣队的士兵之和）。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>解散期士兵总数，若无或封邑无效则返回 0</returns>
        public int GetWaitCycleTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                int total = 0;
                if (data.ReturnedTroopDetachmentList != null)
                {
                    foreach (var detachment in data.ReturnedTroopDetachmentList)
                    {
                        total += detachment.GetTotalCount();
                    }
                }
                return total;
            }
            return 0;
        }

        /// <summary>
        /// 对外接口：根据 Town 获取税收裁剪系数
        /// </summary>
        public float GetFiefTaxationMultiplier(Town town)
        {
            if (town == null) return 1.0f;

            if (_fiefDataMap.TryGetValue(town.Settlement, out var data) && data != null)
            {
                return data.GetTaxationMultiplier();
            }
            return 1.0f;
        }

    }
}