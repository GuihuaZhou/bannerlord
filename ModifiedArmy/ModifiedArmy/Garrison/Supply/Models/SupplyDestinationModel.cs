using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Supply.Models
{
    /// <summary>
    /// Pure single-destination ranking for local foraging. Candidate
    /// validation and native distance calculation remain at the campaign
    /// adapter boundary; this model performs no game-state writes.
    /// </summary>
    public sealed class SupplyDestinationModel
    {
        public SupplyDestinationDecision Select(
            IEnumerable<SupplyDestinationCandidate> candidates)
        {
            SupplyDestinationCandidate best = candidates?
                .Where(x => x.Settlement != null && x.Request > 0 &&
                    x.Distance > 0f)
                .OrderByDescending(x => x.Request / x.Distance)
                .ThenBy(x => x.Distance)
                .FirstOrDefault();
            return best == null
                ? SupplyDestinationDecision.Rejected("NoValidFoodSource")
                : SupplyDestinationDecision.Approved(best.Settlement,
                    best.Request, best.Request / best.Distance);
        }
    }

    public sealed class SupplyDestinationCandidate
    {
        public SupplyDestinationCandidate(Settlement settlement, int request,
            float distance)
        {
            Settlement = settlement;
            Request = request;
            Distance = distance;
        }

        public Settlement Settlement { get; }
        public int Request { get; }
        public float Distance { get; }
    }

    public sealed class SupplyDestinationDecision
    {
        private SupplyDestinationDecision(bool approved, Settlement settlement,
            int request, float score, string reasonCode)
        {
            IsApproved = approved;
            Settlement = settlement;
            Request = request;
            Score = score;
            ReasonCode = reasonCode;
        }

        public bool IsApproved { get; }
        public Settlement Settlement { get; }
        public int Request { get; }
        public float Score { get; }
        public string ReasonCode { get; }

        public static SupplyDestinationDecision Approved(Settlement settlement,
            int request, float score) => new SupplyDestinationDecision(true,
                settlement, request, score, "Approved");

        public static SupplyDestinationDecision Rejected(string reasonCode) =>
            new SupplyDestinationDecision(false, null, 0, 0f, reasonCode);
    }
}
