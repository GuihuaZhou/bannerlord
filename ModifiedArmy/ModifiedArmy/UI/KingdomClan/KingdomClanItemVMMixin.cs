using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedArmy.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TaleWorlds.Library;

namespace ModifiedArmy.UI.KingdomClan
{
    /// <summary>
    /// Kingdom Management -> Clans 页面
    /// Clan 详情扩展。
    ///
    /// 所有战争潜力相关数据统一由 WarPotentialModel 提供。
    /// UI 层只负责读取和显示，不再重复计算。
    /// </summary>
    [ViewModelMixin("Refresh")]
    public class KingdomClanItemVMMixin
        : BaseViewModelMixin<KingdomClanItemVM>
    {
        private readonly KingdomClanItemVM _vm;


        // =============================================================
        // Backing Fields
        // =============================================================

        private int _warPotential;

        private int _fiefTroops;

        private int _garrisonTroops;

        private int _fieldTroops;

        private int _clanWealth;

        private int _dailyIncome;

        private int _partyDailyWage;


        // =============================================================
        // Constructor
        // =============================================================

        public KingdomClanItemVMMixin(
            KingdomClanItemVM vm)
            : base(vm)
        {
            _vm = vm;

            RefreshWarPotentialData();
        }


        // =============================================================
        // Properties
        // =============================================================

        /// <summary>
        /// 最终战争潜力。
        /// </summary>
        [DataSourceProperty]
        public int WarPotential
        {
            get
            {
                return _warPotential;
            }
            set
            {
                if (value != _warPotential)
                {
                    _warPotential = value;

                    _vm.OnPropertyChangedWithValue(
                        value,
                        "WarPotential");
                }
            }
        }


        /// <summary>
        /// 军役兵力。
        /// </summary>
        [DataSourceProperty]
        public int FiefTroops
        {
            get
            {
                return _fiefTroops;
            }
            set
            {
                if (value != _fiefTroops)
                {
                    _fiefTroops = value;

                    _vm.OnPropertyChangedWithValue(
                        value,
                        "FiefTroops");
                }
            }
        }


        /// <summary>
        /// 驻军兵力。
        /// </summary>
        [DataSourceProperty]
        public int GarrisonTroops
        {
            get
            {
                return _garrisonTroops;
            }
            set
            {
                if (value != _garrisonTroops)
                {
                    _garrisonTroops = value;

                    _vm.OnPropertyChangedWithValue(
                        value,
                        "GarrisonTroops");
                }
            }
        }


        /// <summary>
        /// 当前机动兵力。
        /// </summary>
        [DataSourceProperty]
        public int FieldTroops
        {
            get
            {
                return _fieldTroops;
            }
            set
            {
                if (value != _fieldTroops)
                {
                    _fieldTroops = value;

                    _vm.OnPropertyChangedWithValue(
                        value,
                        "FieldTroops");
                }
            }
        }


        /// <summary>
        /// Clan 总财富。
        /// </summary>
        [DataSourceProperty]
        public int ClanWealth
        {
            get
            {
                return _clanWealth;
            }
            set
            {
                if (value != _clanWealth)
                {
                    _clanWealth = value;

                    _vm.OnPropertyChangedWithValue(
                        value,
                        "ClanWealth");
                }
            }
        }


        /// <summary>
        /// Clan 每日收入。
        /// </summary>
        [DataSourceProperty]
        public int DailyIncome
        {
            get
            {
                return _dailyIncome;
            }
            set
            {
                if (value != _dailyIncome)
                {
                    _dailyIncome = value;

                    _vm.OnPropertyChangedWithValue(
                        value,
                        "DailyIncome");
                }
            }
        }


        /// <summary>
        /// Clan 当前所有 WarParty 的每日工资。
        ///
        /// 注意：
        /// 这里显示的是当前基础工资，
        /// 不是 WarPotentialModel 内部的战时 ×2 工资。
        /// </summary>
        [DataSourceProperty]
        public int PartyDailyWage
        {
            get
            {
                return _partyDailyWage;
            }
            set
            {
                if (value != _partyDailyWage)
                {
                    _partyDailyWage = value;

                    _vm.OnPropertyChangedWithValue(
                        value,
                        "PartyDailyWage");
                }
            }
        }


        // =============================================================
        // UIExtender Refresh Hook
        // =============================================================

        /// <summary>
        /// KingdomClanItemVM.Refresh() 执行后，
        /// UIExtenderEx 会调用此方法。
        ///
        /// 每次 Clan 页面刷新时，
        /// 重新从 WarPotentialModel 获取动态数据。
        /// </summary>
        public override void OnRefresh()
        {
            RefreshWarPotentialData();
        }


        // =============================================================
        // Refresh
        // =============================================================

        /// <summary>
        /// 从 WarPotentialModel 统一获取所有战争潜力相关数据。
        /// </summary>
        private void RefreshWarPotentialData()
        {
            Clan clan =
                GetClan();


            if (clan == null)
            {
                ResetValues();
                return;
            }


            WarPotentialModel model =
                WarPotentialModel.Instance;


            if (model == null)
            {
                ResetValues();
                return;
            }


            WarPotentialResult result =
                model.CalculateWarPotential(clan);


            if (result == null)
            {
                ResetValues();
                return;
            }


            // =========================================================
            // 最终战争潜力
            // =========================================================

            WarPotential =
                result.WarPotential;


            // =========================================================
            // 军事资源
            // =========================================================

            FiefTroops =
                result.FiefTroops;


            GarrisonTroops =
                result.GarrisonTroops;


            FieldTroops =
                result.FieldTroops;


            // =========================================================
            // 财政资源
            // =========================================================

            ClanWealth =
                result.ClanWealth;


            DailyIncome =
                (int)Math.Round(
                    result.DailyIncome,
                    MidpointRounding.AwayFromZero);


            PartyDailyWage =
                result.PartyDailyWage;
        }


        // =============================================================
        // Clan 获取
        // =============================================================

        /// <summary>
        /// 获取 KingdomClanItemVM 当前对应的 Clan。
        ///
        /// KingdomClanItemVM 本体持有 Clan 数据，
        /// 当前 UI 列表项本身就是针对单个 Clan 创建的。
        /// </summary>
        private Clan GetClan()
        {
            if (_vm == null)
            {
                return null;
            }


            return _vm.Clan;
        }


        // =============================================================
        // Reset
        // =============================================================

        /// <summary>
        /// Model 尚未初始化或 Clan 无效时，
        /// 清空 UI 数据，避免保留上一个 Clan 的旧值。
        /// </summary>
        private void ResetValues()
        {
            WarPotential = 0;

            FiefTroops = 0;

            GarrisonTroops = 0;

            FieldTroops = 0;

            ClanWealth = 0;

            DailyIncome = 0;

            PartyDailyWage = 0;
        }
    }
}