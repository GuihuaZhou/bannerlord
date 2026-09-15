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
    /// Kingdom Clan 页面战争潜力数据扩展。
    /// </summary>
    [ViewModelMixin("Refresh")]
    public class KingdomClanItemVMMixin
        : BaseViewModelMixin<KingdomClanItemVM>
    {
        private int _warPotential;

        private int _fiefTroops;
        private int _garrisonTroops;
        private int _fieldTroops;

        private int _clanWealth;
        private int _dailyIncome;
        private int _partyDailyWage;

        public KingdomClanItemVMMixin(KingdomClanItemVM viewModel)
            : base(viewModel)
        {
            RefreshWarInfo();
        }

        // =====================================================================
        // 战争潜力
        // =====================================================================

        [DataSourceProperty]
        public int WarPotential
        {
            get => _warPotential;
            set
            {
                if (_warPotential == value)
                    return;

                _warPotential = value;
                ViewModel?.OnPropertyChanged(nameof(WarPotential));
            }
        }

        // =====================================================================
        // 军事资源
        // =====================================================================

        [DataSourceProperty]
        public int FiefTroops
        {
            get => _fiefTroops;
            set
            {
                if (_fiefTroops == value)
                    return;

                _fiefTroops = value;
                ViewModel?.OnPropertyChanged(nameof(FiefTroops));
            }
        }

        [DataSourceProperty]
        public int GarrisonTroops
        {
            get => _garrisonTroops;
            set
            {
                if (_garrisonTroops == value)
                    return;

                _garrisonTroops = value;
                ViewModel?.OnPropertyChanged(nameof(GarrisonTroops));
            }
        }

        [DataSourceProperty]
        public int FieldTroops
        {
            get => _fieldTroops;
            set
            {
                if (_fieldTroops == value)
                    return;

                _fieldTroops = value;
                ViewModel?.OnPropertyChanged(nameof(FieldTroops));
            }
        }

        // =====================================================================
        // 财政资源
        // =====================================================================

        [DataSourceProperty]
        public int ClanWealth
        {
            get => _clanWealth;
            set
            {
                if (_clanWealth == value)
                    return;

                _clanWealth = value;
                ViewModel?.OnPropertyChanged(nameof(ClanWealth));
            }
        }

        [DataSourceProperty]
        public int DailyIncome
        {
            get => _dailyIncome;
            set
            {
                if (_dailyIncome == value)
                    return;

                _dailyIncome = value;
                ViewModel?.OnPropertyChanged(nameof(DailyIncome));
            }
        }

        [DataSourceProperty]
        public int PartyDailyWage
        {
            get => _partyDailyWage;
            set
            {
                if (_partyDailyWage == value)
                    return;

                _partyDailyWage = value;
                ViewModel?.OnPropertyChanged(nameof(PartyDailyWage));
            }
        }

        // =====================================================================
        // Refresh
        // =====================================================================

        public override void OnRefresh()
        {
            RefreshWarInfo();
        }

        private void RefreshWarInfo()
        {
            if (ViewModel?.Clan == null || Campaign.Current == null)
                return;

            Clan clan = ViewModel.Clan;

            // 军事
            FiefTroops = GetClanFiefTroopCount(clan);
            GarrisonTroops = GetClanGarrisonTroopCount(clan);
            FieldTroops = GetClanFieldTroopCount(clan);

            // 财政
            ClanWealth = GetClanWealth(clan);
            DailyIncome = GetClanDailyIncome(clan);
            PartyDailyWage = GetClanPartyDailyWage(clan);

            // 战争潜力
            WarPotential = CalculateWarPotential(
                FieldTroops,
                GarrisonTroops,
                ClanWealth,
                DailyIncome,
                PartyDailyWage);
        }

        // =====================================================================
        // 战争潜力计算
        // =====================================================================

        /// <summary>
        /// CurrentMilitaryPower =
        ///     FieldTroops
        ///     + GarrisonTroops * 0.25
        ///
        /// WarDailyWage =
        ///     PartyDailyWage * 2
        ///
        /// WarDailyBurn =
        ///     max(0, WarDailyWage - DailyIncome)
        ///
        /// FinancialEndurance =
        ///     ClanWealth / WarDailyBurn
        ///
        /// FinancialFactor =
        ///     min(FinancialEndurance / 60, 1)
        ///
        /// WarPotential =
        ///     CurrentMilitaryPower * FinancialFactor
        /// </summary>
        private static int CalculateWarPotential(
            int fieldTroops,
            int garrisonTroops,
            int clanWealth,
            int dailyIncome,
            int partyDailyWage)
        {
            const float GarrisonFactor = 0.25f;
            const float WarWageMultiplier = 2f;
            const float ReferenceWarDays = 60f;

            float currentMilitaryPower =
                fieldTroops
                + garrisonTroops * GarrisonFactor;

            float warDailyWage =
                partyDailyWage * WarWageMultiplier;

            float warDailyBurn =
                MathF.Max(
                    0f,
                    warDailyWage - dailyIncome);

            float financialFactor = 1f;

            if (warDailyBurn > 0f)
            {
                float financialEndurance =
                    clanWealth / warDailyBurn;

                financialFactor =
                    MathF.Min(
                        financialEndurance / ReferenceWarDays,
                        1f);
            }

            return MathF.Round(
                currentMilitaryPower * financialFactor);
        }

        // =====================================================================
        // 封建部队
        // =====================================================================

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

                if (!settlement.IsTown &&
                    !settlement.IsCastle)
                {
                    continue;
                }

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

        private static int GetClanGarrisonTroopCount(Clan clan)
        {
            if (clan == null)
                return 0;

            int total = 0;

            foreach (var settlement in clan.Settlements)
            {
                if (settlement?.Town == null)
                    continue;

                var garrison =
                    settlement.Town.GarrisonParty;

                if (garrison == null)
                    continue;

                total +=
                    garrison.Party.NumberOfAllMembers;
            }

            return total;
        }

        // =====================================================================
        // 野战部队
        // =====================================================================

        private static int GetClanFieldTroopCount(Clan clan)
        {
            if (clan == null)
                return 0;

            int total = 0;

            foreach (var warParty in clan.WarPartyComponents)
            {
                if (warParty?.Party == null)
                    continue;

                total +=
                    warParty.Party.NumberOfAllMembers;
            }

            return total;
        }

        // =====================================================================
        // Clan 财富
        // =====================================================================

        /// <summary>
        /// Clan Wealth =
        /// 所有 Clan Heroes / Companions 实际持有 Gold 的总和。
        ///
        /// 不使用 clan.Gold。
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

            // 某些 Companion 可能已经存在于 Heroes 中。
            // Contains 用于防止重复计算。
            foreach (Hero companion in clan.Companions)
            {
                if (companion == null)
                    continue;

                if (clan.Heroes.Contains(companion))
                    continue;

                total += companion.Gold;
            }

            return total;
        }

        // =====================================================================
        // Clan 每日收入
        // =====================================================================

        private static int GetClanDailyIncome(Clan clan)
        {
            if (clan == null || Campaign.Current == null)
                return 0;

            var financeModel =
                Campaign.Current.Models.ClanFinanceModel;

            if (financeModel == null)
                return 0;

            ExplainedNumber result =
                financeModel.CalculateClanIncome(
                    clan,
                    includeDescriptions: false,
                    applyWithdrawals: false,
                    includeDetails: false);

            return MathF.Round(result.ResultNumber);
        }

        // =====================================================================
        // Clan Party 每日工资
        // =====================================================================

        private static int GetClanPartyDailyWage(Clan clan)
        {
            if (clan == null)
                return 0;

            int total = 0;

            foreach (var warParty in clan.WarPartyComponents)
            {
                var mobileParty =
                    warParty?.MobileParty;

                if (mobileParty == null)
                    continue;

                total += mobileParty.TotalWage;
            }

            return total;
        }
    }
}