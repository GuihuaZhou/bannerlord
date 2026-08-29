using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
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
            base.AddClassDefinition(typeof(FiefWageExemption), 3);
            base.AddClassDefinition(typeof(FiefWageExemptionManager), 4);
        }

        protected override void DefineContainerDefinitions()
        {
            base.ConstructContainerDefinition(typeof(Dictionary<CharacterObject, int>));
            base.ConstructContainerDefinition(typeof(List<FiefTroopDetachment>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, FiefPartyData>));

            base.ConstructContainerDefinition(typeof(List<FiefWageExemption>));
            base.ConstructContainerDefinition(typeof(Dictionary<String, List<FiefWageExemption>>));
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
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, MapEvent.BattleTypes>(this.OnAfterSiegeCompleted));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, new Action<Hero, Settlement, Hero, CharacterObject, int>(this.OnTroopRecruited));
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<Dictionary<Settlement, FiefPartyData>>("_fiefDataMap", ref this._fiefDataMap);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            SoldierTypeClassifier.InitializeAll();
            FiefPartyTemplateManager.Instance.InitAllEnableTroops();
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
                //if (settlement.OwnerClan != Clan.PlayerClan) continue;
                if (!settlement.IsCastle && !settlement.IsTown) continue;
                if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
                {
                    ModLogger.Debug($"[InitializeFiefData] Refreshing existing data for {settlement.Name}");
                    data.InitialFeifPartyData();
                }
                else
                {
                    ModLogger.Debug($"[InitializeFiefData] Creating new fief data for {settlement.Name}");
                    _fiefDataMap[settlement] = new FiefPartyData(settlement);
                }
            }
            TextObject msgComplete = GameTexts.FindText("str_modifiedarmy_fief_init_complete");
            msgComplete.SetTextVariable("COUNT", _fiefDataMap.Count);
            ModLogger.Notice(msgComplete.ToString());
        }

        private void OnWeeklyTick()
        {
            int updatedCount = 0;
            foreach (var data in _fiefDataMap.Values)
            {
                data.WeeklyUpdate();
                updatedCount++;
            }
            ModLogger.Debug($"[Fief Squad Mod] Weekly reinforcement update completed for {updatedCount} settlements.");
        }

        private void OnDailyTick()
        {
            foreach (var data in _fiefDataMap.Values)
            {
                data.DailyUpdate();
            }
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
                return data.GetFiefTroopRoster();
            }
            return TroopRoster.CreateDummyTroopRoster();
        }

        /// <summary>
        /// Recruits all available fief squads from a settlement into the target mobile party.
        /// </summary>
        public int RecruitFiefTroopsFromSettlement(Settlement settlement, MobileParty targetParty)
        {
            if (settlement == null || targetParty == null)
            {
                ModLogger.Error("[Fief] Recruitment failed: invalid parameters.");
                return 0;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.RecruitTroopsToParty(targetParty);
            }
            return 0;
        }

        /// <summary>
        /// Disbands all fief squads in a settlement.
        /// Optionally recovers troops from a mobile party to refill squads.
        /// </summary>
        public void ReturnTroopsToSettlement(Settlement settlement, MobileParty mobileParty = null)
        {
            if (settlement == null)
            {
                ModLogger.Error("[Fief] Disband failed: settlement is null.");
                return;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                data.ReturnTroopsToSettlement(mobileParty);
            }
        }

        /// <summary>
        /// 获取指定封邑的部队最大人数。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>
        public int GetFiefTroopLimit(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetFiefTroopLimit();
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑当前可招募的军队人数（即就绪状态的健康士兵总数）。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>可招募人数，若封邑无效或非玩家所有则返回 0</returns>
        public int GetAvailableTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetReadyFiefTroopCount();
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑当前已被征召的士兵人数。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>已被征召人数，若无征召或封邑无效则返回 0</returns>
        public int GetRecruitedTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetRecruitedFiefTroopCount();
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
        /// 获取指定封邑士兵的实际数量
        /// </summary>
        public Dictionary<SoldierType, int> GetFiefTroopCounts(Settlement settlement)
        {
            if (settlement == null) return null;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
                return data.GetTroopCounts();
            return null;
        }

        /// <summary>
        /// 获取指定封邑士兵的最大数量
        /// </summary>
        public Dictionary<SoldierType, int> GetFiefMaxTroopCounts(Settlement settlement)
        {
            if (settlement == null) return null;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
                return data.GetMaxTroopCounts();
            return null;
        }


        /// <summary>
        /// 获取指定封邑每周更新的实际人数
        /// </summary>
        public int GetWeeklyUpdateCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
                return data.GetWeeklyUpdateCount();
            return 0;
        }

        /// <summary>
        /// 监听围城结束事件
        /// </summary>
        private void OnAfterSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool isWin, MapEvent.BattleTypes battleType)
        {
            if (battleType != MapEvent.BattleTypes.Siege && battleType != MapEvent.BattleTypes.SallyOut)
                return;

            if (!_fiefDataMap.TryGetValue(siegeSettlement, out var fiefData) || fiefData == null)
                return;

            fiefData.OnSiegeCompleted(isWin);
        }

        /// <summary>
        /// 监听定居点所有者家族变化事件
        /// </summary>
        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero previousOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement == null) return;

            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData) || fiefData == null)
                return;

            fiefData.OnSettlementOwnerChanged();
        }

        public void ManualRecruitFromFief(Settlement settlement, TroopRoster selectedRoster, MobileParty targetParty)
        {
            if (settlement == null || selectedRoster == null)
                return;

            if (_fiefDataMap.TryGetValue(settlement, out var fiefData) && fiefData != null)
            {
                fiefData.RecruitManualSelection(selectedRoster, targetParty);
            }
        }

        /// <summary>
        /// 监听招募事件，扣除定居点繁荣度/户数。
        ///
        /// 优先使用对应文化的 FiefPartyTemplate 中的
        /// ProsperityCostPerTroop / HearthCostPerTroop。
        ///
        /// 若无模板则使用默认 fallback 值。
        /// </summary>
        ///
        private void OnTroopRecruited(
            Hero recruiter,
            Settlement settlement,
            Hero recruitmentSource,
            CharacterObject troop,
            int count)
        {
            // 安全检查
            if (settlement == null || count <= 0)
                return;

            if (troop.Occupation != Occupation.Soldier)
                return;

            // ============================================================
            // 从已有的 FiefPartyData 获取 template，
            // 以读取 ProsperityCostPerTroop / HearthCostPerTroop。
            //
            // 若无模板，则不扣除繁荣度/户数。
            // ============================================================
            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData) || fiefData == null)
                return;

            var template = fiefData.GetFiefPartyTemplate();
            if (template == null)
                return;

            int prosperityCostPerTroop = template.ProsperityCostPerTroop;
            int hearthCostPerTroop = template.HearthCostPerTroop;

            if (settlement.IsTown)
            {
                // 扣除繁荣度
                int prosperityCost = count * prosperityCostPerTroop * troop.Tier;
                settlement.Town.Prosperity = Math.Max(0f, settlement.Town.Prosperity - prosperityCost);
                ModLogger.Debug($"[ProsperityCost] {recruiter?.Name} recruited {count} {troop.Name} from {settlement.Name}, cost: {prosperityCost:F1}");
            }
            else if (settlement.IsVillage)
            {
                // 扣除户数
                int hearthCost = count * hearthCostPerTroop;
                settlement.Village.Hearth = Math.Max(0f, settlement.Village.Hearth - hearthCost);
                ModLogger.Debug($"[HearthCost] {recruiter?.Name} recruited {count} {troop.Name} from {settlement.Name}, cost: {hearthCost:F1}");
            }
        }
    }
}