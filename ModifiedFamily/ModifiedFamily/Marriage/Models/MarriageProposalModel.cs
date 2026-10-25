// Validates proposal creation, response, and execution as separate lifecycle stages.
using System.Collections.Generic;
using ModifiedFamily.Core;
using ModifiedFamily.Persistence;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using ModifiedFamily.Marriage.Models;

namespace ModifiedFamily.Marriage.Models
{
    /// <summary>
    /// Applies the marriage rules appropriate to each proposal lifecycle stage.
    /// It contains no persistence or state mutation.
    /// </summary>
    public sealed class MarriageProposalModel
    {
        private readonly IEnumerable<AdoptiveKinshipRecord> _kinship;
        private readonly MarriagePoliticalValidationModel _political = new MarriagePoliticalValidationModel();
        public MarriageProposalModel(IEnumerable<AdoptiveKinshipRecord> kinship) { _kinship = kinship; }

        public FamilyResult CanProposeMarriage(FamilyProposal p)
        {
            if (p == null || p.HeroA == null || p.HeroB == null || p.HeroA == p.HeroB) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            var political = _political.Validate(p); if (!political.IsAllowed) return political;
            var a = MarriageEligibilityModel.CanNegotiateHero(p.HeroA); if (!a.IsAllowed) return a;
            var b = MarriageEligibilityModel.CanNegotiateHero(p.HeroB); if (!b.IsAllowed) return b;
            if (p.HeroA.IsFemale == p.HeroB.IsFemale) return FamilyResult.Deny(FamilyReasonCode.SameSex);
            if (MarriageEligibilityModel.AreBloodRelated(p.HeroA, p.HeroB)) return FamilyResult.Deny(FamilyReasonCode.Related);
            if (MarriageEligibilityModel.AreAdoptiveRelated(p.HeroA, p.HeroB, _kinship)) return FamilyResult.Deny(FamilyReasonCode.AdoptiveRelated);
            if (p.DestinationClan != p.OriginClanA && p.DestinationClan != p.OriginClanB) return FamilyResult.Deny(FamilyReasonCode.InvalidClan);
            if ((p.OriginClanA.Leader == p.HeroA && p.DestinationClan != p.OriginClanA) || (p.OriginClanB.Leader == p.HeroB && p.DestinationClan != p.OriginClanB)) return FamilyResult.Deny(FamilyReasonCode.LeaderCannotLeave);
            if (!ValidGift(p)) return FamilyResult.Deny(FamilyReasonCode.InvalidGift);
            var blockers = new List<FamilyReasonCode>(); blockers.AddRange(a.TemporaryBlockers); blockers.AddRange(b.TemporaryBlockers);
            return FamilyResult.Allow(null, blockers.ToArray());
        }

        public FamilyResult CanRespond(FamilyProposal p)
        {
            var result = CanProposeMarriage(p);
            if (!result.IsAllowed) return result;
            return result;
        }

        public FamilyResult CanExecuteMarriage(FamilyProposal p)
        {
            if (p.Status != FamilyProposalStatus.AcceptedPendingExecution) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            var result = CanRespond(p); if (!result.IsAllowed || result.TemporaryBlockers.Count > 0) return result;
            if (p.GiftAmount > 0 && (p.GoldPayerClan?.Leader == null || p.GoldPayerClan.Leader.Gold < p.GiftAmount)) return FamilyResult.Deny(FamilyReasonCode.InsufficientFunds);
            return result;
        }

        private static bool ValidGift(FamilyProposal p) => p.GiftAmount >= 0 && (p.GiftAmount == 0 || (p.GoldPayerClan != null && p.GoldReceiverClan != null && p.GoldPayerClan != p.GoldReceiverClan && (p.GoldPayerClan == p.OriginClanA || p.GoldPayerClan == p.OriginClanB) && (p.GoldReceiverClan == p.OriginClanA || p.GoldReceiverClan == p.OriginClanB)));
        internal static bool CultureAndKingdomAllowed(Hero a, Hero b)
        {
            if (a?.Clan == null || b?.Clan == null) return false;
            bool sameKingdom = a.Clan.Kingdom != null && a.Clan.Kingdom == b.Clan.Kingdom;
            bool sameCulture = a.Clan.Culture == b.Clan.Culture;
            return sameCulture || (sameKingdom && MarriagePolicyModel.AllowSameKingdomDifferentCulture);
        }
    }
}
