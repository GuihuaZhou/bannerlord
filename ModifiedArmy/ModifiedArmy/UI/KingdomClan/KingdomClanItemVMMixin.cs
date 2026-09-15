using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedArmy.Models.Fief;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TaleWorlds.Library;

namespace ModifiedArmy.UI.KingdomClan
{
    /// <summary>
    /// 给原版 KingdomClanItemVM 增加战争潜力相关原始数据显示。
    ///
    /// 军力：
    /// 封建部队 / 驻军 / 野战部队
    ///
    /// 财富：
    /// Clan财富总额 / Clan每日收入 / Clan Party每日工资总额
    /// </summary>
    [ViewModelMixin("Refresh")]
    public class KingdomClanItemVMMixin
        : BaseViewModelMixin<KingdomClanItemVM>
    {
        private string _militaryInfo = string.Empty;
        private string _financialInfo = string.Empty;

        public KingdomClanItemVMMixin(KingdomClanItemVM viewModel)
            : base(viewModel)
        {
            RefreshWarInfo();
        }

        [DataSourceProperty]
        public string MilitaryInfo
        {
            get => _militaryInfo;
            set
            {
                if (value == _militaryInfo)
                    return;

                _militaryInfo = value;

                if (ViewModel != null)
                {
                    ViewModel.OnPropertyChanged(nameof(MilitaryInfo));
                }
            }
        }

        [DataSourceProperty]
        public string FinancialInfo
        {
            get => _financialInfo;
            set
            {
                if (value == _financialInfo)
                    return;

                _financialInfo = value;

                if (ViewModel != null)
                {
                    ViewModel.OnPropertyChanged(nameof(FinancialInfo));
                }
            }
        }

        /// <summary>
        /// KingdomClanItemVM.Refresh() 执行后自动刷新。
        /// </summary>
        public override void OnRefresh()
        {
            RefreshWarInfo();
        }

        private void RefreshWarInfo()
        {
            if (ViewModel?.Clan == null || Campaign.Current == null)
                return;

            Clan clan = ViewModel.Clan;

            // =========================================================
            // 军事
            // =========================================================

            int fiefTroops = GetClanFiefTroopCount(clan);
            int garrisonTroops = GetClanGarrisonTroopCount(clan);
            int fieldTroops = GetClanFieldTroopCount(clan);

            // =========================================================
            // 财政
            // =========================================================

            int clanWealth = GetClanWealth(clan);
            int dailyIncome = GetClanDailyIncome(clan);
            int partyDailyWage = GetClanPartyDailyWage(clan);

            // =========================================================
            // UI
            // =========================================================

            MilitaryInfo =
                $"军力（封建 / 驻军 / 野战）：{fiefTroops} / {garrisonTroops} / {fieldTroops}";

            FinancialInfo =
                $"财富（总额 / 收入 / 工资）：{clanWealth} / {dailyIncome} / {partyDailyWage}";
        }

        // =====================================================================
        // 封建部队
        // =====================================================================

        /// <summary>
        /// 获取 Clan 所有城镇/城堡的封建部队总数。
        ///
        /// 使用 FiefPartyManager.GetFiefTroopCounts()。
        /// 该接口返回每种 SoldierType 的实际数量，
        /// 将所有类型数量求和，再对 Clan 所有封邑累加。
        /// </summary>
        private static int GetClanFiefTroopCount(Clan clan)
        {
            if (clan == null || Campaign.Current == null)
                return 0;

            var fiefManager =
                Campaign.Current.GetCampaignBehavior<FiefPartyManager>();

            if (fiefManager == null)
                return 0;

            int total = 0;

            foreach (var settlement in clan.Settlements)
            {
                if (settlement == null)
                    continue;

                if (!settlement.IsTown && !settlement.IsCastle)
                    continue;

                var counts =
                    fiefManager.GetFiefTroopCounts(settlement);

                if (counts == null)
                    continue;

                total += counts.Values.Sum();
            }

            return total;
        }

        // =====================================================================
        // 驻军
        // =====================================================================

        /// <summary>
        /// 获取 Clan 所有城镇/城堡驻军人数。
        ///
        /// 与原版 Clan 管理页面获取 Garrison 的方式一致：
        ///
        /// Clan.Settlements
        ///     -> Settlement.Town
        ///     -> GarrisonParty
        ///
        /// NumberOfAllMembers 包含当前 Party 的全部成员，
        /// 不包含俘虏。
        /// </summary>
        private static int GetClanGarrisonTroopCount(Clan clan)
        {
            if (clan == null)
                return 0;

            int total = 0;

            foreach (var settlement in clan.Settlements)
            {
                if (settlement?.Town == null)
                    continue;

                var garrisonParty =
                    settlement.Town.GarrisonParty;

                if (garrisonParty == null)
                    continue;

                total += garrisonParty.Party.NumberOfAllMembers;
            }

            return total;
        }

        // =====================================================================
        // 野战部队
        // =====================================================================

        /// <summary>
        /// 获取 Clan 所有 War Party 的士兵总人数。
        ///
        /// 使用 Clan.WarPartyComponents。
        ///
        /// 这是原版 Clan 管理页面 Parties 列表本身使用的数据源，
        /// 因此：
        ///
        /// 包含：
        /// - 玩家/领主主力 Party
        /// - Clan 其他成员 Party
        ///
        /// 不包含：
        /// - Caravan
        /// - Garrison
        ///
        /// 正好符合战争潜力设计中的 Clan Party。
        /// </summary>
        private static int GetClanFieldTroopCount(Clan clan)
        {
            if (clan == null)
                return 0;

            int total = 0;

            foreach (var warPartyComponent in clan.WarPartyComponents)
            {
                if (warPartyComponent?.Party == null)
                    continue;

                total += warPartyComponent.Party.NumberOfAllMembers;
            }

            return total;
        }

        // =====================================================================
        // Clan Wealth
        // =====================================================================

        /// <summary>
        /// Clan 当前财政储备。
        ///
        /// </summary>
        private static int GetClanWealth(Clan clan)
        {
            if (clan == null)
                return 0;

            int total = 0;

            foreach (Hero hero in clan.Heroes)
            {
                if (hero == null)
                    continue;

                total += hero.Gold;
            }

            foreach (Hero companion in clan.Companions)
            {
                if (companion == null)
                    continue;

                // 避免理论上的重复统计
                if (clan.Heroes.Contains(companion))
                    continue;

                total += companion.Gold;
            }

            return total;
        }

        // =====================================================================
        // Clan Daily Income
        // =====================================================================

        /// <summary>
        /// 获取 Clan 原版财政模型计算出的每日收入。
        ///
        /// CalculateClanIncome 会计算收入项目，例如：
        /// - Settlement income
        /// - Party / Caravan income
        /// - Workshop income
        /// - Mercenary income
        /// - Tribute income
        /// - Kingdom budget income
        /// - Ruling clan policy income
        /// 等。
        ///
        /// applyWithdrawals = false：
        /// 仅计算，不实际修改任何钱包或累计值。
        ///
        /// 注意：
        /// 这里是 Income，不是 GoldChange。
        /// 所以不会把工资等 Expense 再扣一次。
        /// 这正好符合：
        ///
        /// WarDailyBalance =
        /// DailyIncome - PartyDailyWage × 2
        ///
        /// 的设计。
        /// </summary>
        private static int GetClanDailyIncome(Clan clan)
        {
            if (clan == null || Campaign.Current == null)
                return 0;

            var financeModel =
                Campaign.Current.Models.ClanFinanceModel;

            if (financeModel == null)
                return 0;

            ExplainedNumber income =
                financeModel.CalculateClanIncome(
                    clan,
                    includeDescriptions: false,
                    applyWithdrawals: false,
                    includeDetails: false);

            return MathF.Round(income.ResultNumber);
        }

        // =====================================================================
        // Clan Party Daily Wage
        // =====================================================================

        /// <summary>
        /// 获取 Clan 所有 War Party 当前每日工资总额。
        ///
        /// 只统计 Clan.WarPartyComponents：
        ///
        /// 包含野战 Party；
        /// 不包含驻军；
        /// 不包含商队。
        ///
        /// MobileParty.TotalWage 会通过当前 Campaign 的
        /// PartyWageModel 进行计算，因此会自动使用当前实际生效的工资模型。
        /// </summary>
        private static int GetClanPartyDailyWage(Clan clan)
        {
            if (clan == null)
                return 0;

            int total = 0;

            foreach (var warPartyComponent in clan.WarPartyComponents)
            {
                var mobileParty =
                    warPartyComponent?.MobileParty;

                if (mobileParty == null)
                    continue;

                total += mobileParty.TotalWage;
            }

            return total;
        }
    }
}