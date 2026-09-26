using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Finance
{
    /// <summary>
    /// Calculates each clan's share of subject tribute. Subject expenses mirror
    /// Bannerlord's native tribute weights: towns count as three, castles as one,
    /// every clan receives one base share, and the ruling clan receives one
    /// additional share. All income is paid to the overlord's ruling clan.
    /// </summary>
    public static class SubjectTributeCalculator
    {
        public static int GetIncomeForClan(Clan clan)
        {
            Kingdom kingdom = clan?.Kingdom;
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (kingdom == null
                || !IsEligibleClan(clan)
                || manager == null)
            {
                return 0;
            }

            if (kingdom.RulingClan != clan)
            {
                return 0;
            }

            int totalIncome = 0;
            foreach (SubjectRelationData relation in manager.GetSubjects(kingdom))
            {
                if (IsActive(relation))
                {
                    totalIncome += relation.DailyTribute;
                }
            }

            return totalIncome;
        }

        public static int GetExpenseForClan(Clan clan)
        {
            Kingdom kingdom = clan?.Kingdom;
            SubjectRelationData relation = KingdomDiplomacyManager.Current
                ?.GetSubjectRelation(kingdom);
            if (kingdom == null
                || !IsEligibleClan(clan)
                || !IsActive(relation))
            {
                return 0;
            }

            return GetAllocatedShare(kingdom, clan, relation.DailyTribute);
        }

        private static int GetAllocatedShare(
            Kingdom kingdom,
            Clan targetClan,
            int totalAmount)
        {
            if (kingdom == null || targetClan == null || totalAmount <= 0)
            {
                return 0;
            }

            List<Clan> clans = kingdom.Clans
                .Where(IsEligibleClan)
                .ToList();
            int targetIndex = clans.IndexOf(targetClan);
            if (targetIndex < 0)
            {
                return 0;
            }

            int totalWeight = clans.Sum(clan => GetClanWeight(kingdom, clan));
            if (totalWeight <= 0)
            {
                return 0;
            }

            int floorShare = GetFloorShare(
                totalAmount,
                GetClanWeight(kingdom, targetClan),
                totalWeight);
            int allFloorShares = clans.Sum(clan => GetFloorShare(
                totalAmount,
                GetClanWeight(kingdom, clan),
                totalWeight));
            int remainder = totalAmount - allFloorShares;

            // Give one remainder denar to clans in stable kingdom-list order.
            // This makes all independently calculated clan shares total exactly
            // the configured tribute amount without storing transient wallets.
            return floorShare + (targetIndex < remainder ? 1 : 0);
        }

        private static int GetFloorShare(
            int totalAmount,
            int clanWeight,
            int totalWeight)
        {
            return (int)((long)totalAmount * clanWeight / totalWeight);
        }

        private static int GetClanWeight(Kingdom kingdom, Clan clan)
        {
            int fiefWeight = clan.Fiefs.Sum(fief => fief.IsCastle ? 1 : 3);
            return fiefWeight + 1 + (clan == kingdom.RulingClan ? 1 : 0);
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
                && relation.DailyTribute > 0
                && relation.SubjectKingdom != null
                && relation.OverlordKingdom != null
                && !relation.SubjectKingdom.IsEliminated
                && !relation.OverlordKingdom.IsEliminated;
        }
    }
}
