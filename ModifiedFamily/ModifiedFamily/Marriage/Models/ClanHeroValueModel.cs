// Stateless, explainable scoring model used independently by proposal senders and receivers.
using System;
using System.Linq;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Marriage.Models
{
    public sealed class ClanHeroValueModel
    {
        public float ValueToClan(Hero hero, Clan clan)
        {
            if (hero == null || clan == null) return 0f;
            float value = 75f - Math.Abs(hero.Age - 28f) * 1.25f;
            return Clamp(value, 20f, 160f);
        }

        public int CountAdultNobleHeroes(Clan clan) => clan?.Heroes.Count(h => h != null && !h.IsDead && h.IsLord && h.Clan == clan && h.Age >= 18f) ?? 0;

        public MarriageScoreBreakdown Evaluate(FamilyProposal proposal, Clan evaluatingClan)
        {
            var result = new MarriageScoreBreakdown { EvaluatingClan = evaluatingClan };
            if (proposal == null || evaluatingClan == null) return result;
            Hero own = proposal.OriginClanA == evaluatingClan ? proposal.HeroA : proposal.OriginClanB == evaluatingClan ? proposal.HeroB : null;
            Hero other = own == proposal.HeroA ? proposal.HeroB : proposal.HeroA;
            Clan otherClan = proposal.OriginClanA == evaluatingClan ? proposal.OriginClanB : proposal.OriginClanA;
            result.OwnHero = own; result.OtherHero = other; result.OtherClan = otherClan;
            result.Direction = proposal.HeroA != null && proposal.HeroA.IsFemale
                ? (proposal.DestinationClan == proposal.OriginClanA ? MarriageDirection.MaleJoinsFemaleClan : MarriageDirection.FemaleJoinsMaleClan)
                : (proposal.DestinationClan == proposal.OriginClanA ? MarriageDirection.FemaleJoinsMaleClan : MarriageDirection.MaleJoinsFemaleClan);
            if (own == null || other == null || otherClan == null) return result;

            result.AdultHeroCount = CountAdultNobleHeroes(evaluatingClan);
            bool receivesHero = proposal.DestinationClan == evaluatingClan;
            result.BaseHeroValue = ValueToClan(receivesHero ? other : own, evaluatingClan);
            if (receivesHero) result.InflowValue = result.BaseHeroValue;
            else
            {
                float scarcity = 1f + 4f / (result.AdultHeroCount + 1f);
                float direction = own.IsFemale ? MarriageScoreTuning.FemaleOutflowMultiplier : MarriageScoreTuning.MaleOutflowMultiplier;
                result.OutflowCost = result.BaseHeroValue * scarcity * direction;
                if (result.AdultHeroCount - 1 <= 1) result.SuccessionPenalty = MarriageScoreTuning.SuccessionPenalty;
            }

            result.AllianceBenefit = MarriageScoreTuning.AllianceBenefit;
            result.RelationRaw = evaluatingClan.GetRelationWithClan(otherClan);
            result.RelationValue = Clamp(result.RelationRaw * 0.3f, -30f, 30f);
            if (evaluatingClan.Culture == otherClan.Culture) result.CultureValue = MarriageScoreTuning.SameCultureBenefit;
            if (evaluatingClan.Kingdom != null && evaluatingClan.Kingdom == otherClan.Kingdom) result.KingdomValue = MarriageScoreTuning.SameKingdomBenefit;
            result.GiftValue = GiftValue(proposal, evaluatingClan);
            return result;
        }

        public float MarriageScore(Hero ownHero, Hero otherHero, Clan evaluatingClan, Clan destination, int giftDelta)
        {
            if (ownHero == null || otherHero == null) return float.MinValue;
            var preview = new FamilyProposal
            {
                ProposalType = FamilyProposalType.Marriage, HeroA = ownHero, HeroB = otherHero,
                OriginClanA = ownHero.Clan, OriginClanB = otherHero.Clan, DestinationClan = destination,
                GiftAmount = giftDelta == int.MinValue ? int.MaxValue : Math.Abs(giftDelta), GoldReceiverClan = giftDelta > 0 ? evaluatingClan : null,
                GoldPayerClan = giftDelta < 0 ? evaluatingClan : null
            };
            return Evaluate(preview, evaluatingClan).Total;
        }

        private static float GiftValue(FamilyProposal proposal, Clan clan)
        {
            if (proposal.GiftAmount <= 0) return 0f;
            double utility = Math.Min(100d, 35d * Math.Log(1d + proposal.GiftAmount / 10000d));
            if (proposal.GoldReceiverClan == clan) return (float)utility;
            if (proposal.GoldPayerClan != clan) return 0f;
            int gold = Math.Max(1, clan.Leader?.Gold ?? 0);
            float pressure = Clamp(1f + proposal.GiftAmount / (float)gold, 1f, 2.5f);
            return (float)-utility * pressure;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
