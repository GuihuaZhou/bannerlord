using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.common;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Models
{
    public sealed class PartyCreationSiteResult
    {
        public Settlement Settlement { get; set; }
        public int EstimatedGarrisonRegulars { get; set; }
        public int EstimatedFiefRegulars { get; set; }
        public int EstimatedCombinedRegulars { get; set; }
        public PartyForceSourceKind PreferredInitialSource { get; set; }
        public string ReasonCode { get; set; }
        public float Distance { get; set; }
    }

    /// <summary>Read-only preflight for a lord party that does not exist yet.</summary>
    public sealed class PartyCreationSettlementModel
    {
        public PartyCreationSiteResult FindEligibleBase(
            Hero leader, Clan clan, int minimumRegulars = 50)
        {
            if (leader == null || clan == null || leader.Clan != clan)
                return null;
            PartySettlementBindingBehavior bindings = Campaign.Current?
                .GetCampaignBehavior<PartySettlementBindingBehavior>();
            FiefPartyManager fiefs = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (bindings == null || fiefs == null) return null;
            int minimumFiefBatch = MilitaryConfigManager.Instance
                .RequireActiveConfig().AiRecruitment.ForceManagement
                .MinimumFiefRecruitBatch;
            Settlement origin = leader.CurrentSettlement ??
                leader.LastKnownClosestSettlement;

            return clan.Settlements
                .Where(settlement => bindings.CanReserveForCreation(
                    leader, clan, settlement))
                .Select(settlement => Evaluate(
                    settlement, fiefs, origin, minimumRegulars, minimumFiefBatch))
                .Where(result => result != null &&
                    result.EstimatedCombinedRegulars >= minimumRegulars)
                .OrderByDescending(result => result.EstimatedCombinedRegulars)
                .ThenBy(result => result.Distance)
                .ThenBy(result => result.Settlement.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static PartyCreationSiteResult Evaluate(Settlement settlement,
            FiefPartyManager fiefs, Settlement origin, int minimum, int minimumFiefBatch)
        {
            int garrison = GarrisonTransferPolicyModel
                .GetTransferableGarrisonTroopCount(settlement);
            int fief = GetEligibleFiefRegulars(fiefs, settlement);
            bool fiefBatch = fief >= Math.Max(1, minimumFiefBatch);
            int usableFief = fiefBatch ? fief : 0;
            int combined = garrison + usableFief;
            if (combined < minimum) return null;
            PartyForceSourceKind first = fiefBatch
                ? PartyForceSourceKind.Fief
                : PartyForceSourceKind.Professional;
            return new PartyCreationSiteResult
            {
                Settlement = settlement,
                EstimatedGarrisonRegulars = garrison,
                EstimatedFiefRegulars = usableFief,
                EstimatedCombinedRegulars = combined,
                PreferredInitialSource = first,
                ReasonCode = "EstimatedSupplyThresholdMet",
                Distance = GetDistance(origin, settlement)
            };
        }

        internal static int GetEligibleFiefRegulars(
            FiefPartyManager fiefs, Settlement settlement)
        {
            var template = fiefs?.GetFiefData(settlement)?
                .GetFiefPartyTemplate();
            if (template == null) return 0;
            return fiefs.GetFiefTroopRoster(settlement).GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0 &&
                    template.IsEnableTroop(element.Character))
                .Sum(element => element.Number);
        }

        private static float GetDistance(Settlement origin, Settlement target)
        {
            if (origin == null || target == null) return float.MaxValue;
            try
            {
                return Campaign.Current.Models.MapDistanceModel.GetDistance(
                    origin, target, false, false,
                    MobileParty.NavigationType.Default);
            }
            catch { return float.MaxValue; }
        }
    }
}
