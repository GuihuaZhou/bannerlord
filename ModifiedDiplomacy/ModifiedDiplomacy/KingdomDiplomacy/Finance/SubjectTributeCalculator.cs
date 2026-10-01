using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;

namespace ModifiedDiplomacy.KingdomDiplomacy.Finance
{
    /// <summary>
    /// Builds one balanced tribute assessment per campaign day. Every payment
    /// is charged to the clan that owns the assessed settlement, while all
    /// collected money is credited to the overlord's ruling clan.
    /// </summary>
    public static class SubjectTributeCalculator
    {
        public const int TributePerVillage = 100;
        public const int TributePerCastle = 200;
        public const int TributePerTown = 400;

        private static readonly Dictionary<Clan, int> ClanExpenses =
            new Dictionary<Clan, int>();
        private static readonly Dictionary<Clan, int> RulingClanIncome =
            new Dictionary<Clan, int>();

        private static int _assessmentDay = int.MinValue;
        private static bool _isBuildingAssessment;

        /// <summary>
        /// True while native clan finances are queried to determine how much a
        /// payer can afford. Finance postfixes must return zero during this
        /// query so tribute is not recursively included in itself.
        /// </summary>
        public static bool IsBuildingAssessment => _isBuildingAssessment;

        public static int GetIncomeForClan(Clan clan)
        {
            EnsureDailyAssessment();
            return clan != null
                && RulingClanIncome.TryGetValue(clan, out int income)
                    ? income
                    : 0;
        }

        public static int GetExpenseForClan(Clan clan)
        {
            EnsureDailyAssessment();
            return clan != null
                && ClanExpenses.TryGetValue(clan, out int expense)
                    ? expense
                    : 0;
        }

        /// <summary>
        /// Returns the nominal daily burden. Proposal evaluation must consider
        /// the lasting obligation rather than only today's affordable payment.
        /// </summary>
        public static int GetAssessedTributeForClan(Kingdom subject, Clan clan)
        {
            return subject == null || clan == null
                ? 0
                : CalculateClanAssessment(subject, clan);
        }

        /// <summary>
        /// Returns the amount that can actually be collected today. Unpaid
        /// tribute is waived instead of being created from nothing.
        /// </summary>
        public static int GetDailyTribute(SubjectRelationData relation)
        {
            EnsureDailyAssessment();
            if (!IsActive(relation))
            {
                return 0;
            }

            return relation.SubjectKingdom.Clans
                .Where(IsEligibleClan)
                .Sum(clan => ClanExpenses.TryGetValue(clan, out int expense)
                    ? expense
                    : 0);
        }

        public static void InvalidateAssessment()
        {
            _assessmentDay = int.MinValue;
            ClanExpenses.Clear();
            RulingClanIncome.Clear();
        }

        private static void EnsureDailyAssessment()
        {
            if (_isBuildingAssessment || Campaign.Current == null)
            {
                return;
            }

            int currentDay = (int)CampaignTime.Now.ToDays;
            if (_assessmentDay == currentDay)
            {
                return;
            }

            ClanExpenses.Clear();
            RulingClanIncome.Clear();
            _isBuildingAssessment = true;
            try
            {
                BuildAssessment();
                _assessmentDay = currentDay;
            }
            finally
            {
                _isBuildingAssessment = false;
            }
        }

        private static void BuildAssessment()
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager == null)
            {
                return;
            }

            foreach (Kingdom overlord in Kingdom.All)
            {
                Clan receiver = overlord?.RulingClan;
                if (!IsEligibleClan(receiver))
                {
                    continue;
                }

                int collectedForOverlord = 0;
                foreach (SubjectRelationData relation in
                    manager.GetSubjects(overlord))
                {
                    if (!IsActive(relation))
                    {
                        continue;
                    }

                    foreach (Clan clan in relation.SubjectKingdom.Clans)
                    {
                        if (!IsEligibleClan(clan))
                        {
                            continue;
                        }

                        int assessed = CalculateClanAssessment(
                            relation.SubjectKingdom,
                            clan);
                        if (assessed <= 0)
                        {
                            continue;
                        }

                        int available = CalculateAvailableGold(clan);
                        int payable = Math.Min(assessed, available);
                        if (payable <= 0)
                        {
                            continue;
                        }

                        ClanExpenses[clan] = payable;
                        collectedForOverlord += payable;
                    }
                }

                if (collectedForOverlord > 0)
                {
                    RulingClanIncome[receiver] = collectedForOverlord;
                }
            }
        }

        private static int CalculateClanAssessment(
            Kingdom subject,
            Clan clan)
        {
            int amount = 0;
            foreach (var settlement in subject.Settlements)
            {
                if (settlement?.OwnerClan != clan)
                {
                    continue;
                }

                if (settlement.IsTown)
                {
                    amount += TributePerTown;
                }
                else if (settlement.IsCastle)
                {
                    amount += TributePerCastle;
                }
            }

            amount += subject.Villages.Count(village =>
                village?.Settlement?.OwnerClan == clan) * TributePerVillage;
            return amount;
        }

        private static int CalculateAvailableGold(Clan clan)
        {
            // Query the complete native daily balance without tribute. The
            // recursion guard makes our finance postfixes contribute zero.
            float nativeBalance = Campaign.Current.Models.ClanFinanceModel
                .CalculateClanGoldChange(clan, false, false, false)
                .ResultNumber;
            return Math.Max(0, (int)(clan.Gold + nativeBalance));
        }

        private static bool IsEligibleClan(Clan clan)
        {
            return clan != null
                && !clan.IsEliminated
                && !clan.IsUnderMercenaryService
                && clan.Leader != null;
        }

        private static bool IsActive(SubjectRelationData relation)
        {
            return relation != null
                && relation.SubjectKingdom != null
                && relation.OverlordKingdom != null
                && !relation.SubjectKingdom.IsEliminated
                && !relation.OverlordKingdom.IsEliminated;
        }
    }
}
