using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
            //CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, MapEvent.BattleTypes>(this.OnAfterSiegeCompleted));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
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
                //if (settlement.OwnerClan != Clan.PlayerClan) continue;
                if (!settlement.IsCastle && !settlement.IsTown) continue;
                if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
                {
                    ModLogger.Debug($"[InitializeFiefData] Refreshing existing data for {settlement.Name}");
                    data.Reflush(true);
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
            if (_fiefDataMap.TryGetValue(settlement, out var data))
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
            if (_fiefDataMap.TryGetValue(settlement, out var data))
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
        public int GetAvailableRecruitCount(Settlement settlement)
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

        /// <summary>
        /// 获取指定封邑中扈从（Retinue）的当前总数量。
        /// </summary>
        public int GetFiefRetinueCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data))
                return data.GetRetinueCount();
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中扈从（Retinue）的最大允许数量。
        /// </summary>
        public int GetFiefMaxRetinueCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data))
                return data.GetMaxRetinueCount();
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中处于征召状态的扈从（Retinue）数量。
        /// </summary>
        public int GetRecruitedRetinueCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetRecruitedRetinueCount();
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中军士（Sergeant）的当前总数量。
        /// </summary>
        public int GetFiefSergeantCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data))
                return data.GetSergeantCount();
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中军士（Sergeant）的最大允许数量。
        /// </summary>
        public int GetFiefMaxSergeantCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data))
                return data.GetMaxSergeantCount();
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中处于征召状态的军士（Sergeant）数量。
        /// </summary>
        public int GetRecruitedSergeantCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetRecruitedSergeantCount();
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中民兵（Militia）的当前总数量。
        /// </summary>
        public int GetFiefMilitiaCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data))
                return data.GetMilitiaCount();
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中民兵（Militia）的最大允许数量。
        /// </summary>
        public int GetFiefMaxMilitiaCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data))
                return data.GetMaxMilitiaCount();
            return 0;
        }

        /// <summary>
        /// 获取指定封邑中处于征召状态的民兵（Militia）数量。
        /// </summary>
        public int GetRecruitedMilitiaCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetRecruitedMilitiaCount();
            }
            return 0;
        }


        private void OnAfterSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool isWin, MapEvent.BattleTypes battleType)
        {
            if (battleType != MapEvent.BattleTypes.Siege && battleType != MapEvent.BattleTypes.SallyOut)
                return;

            if (!_fiefDataMap.TryGetValue(siegeSettlement, out var fiefData) || fiefData == null)
                return;

            fiefData.OnSiegeCompleted(isWin);
        }


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

                TextObject msgRecruit = GameTexts.FindText("str_modifiedarmy_fief_manual_recruit");
                msgRecruit.SetTextVariable("COUNT", selectedRoster.TotalManCount);
                msgRecruit.SetTextVariable("SETTLEMENT_NAME", settlement.Name.ToString());
                ModLogger.Notice(msgRecruit.ToString());
            }
        }

    }
}