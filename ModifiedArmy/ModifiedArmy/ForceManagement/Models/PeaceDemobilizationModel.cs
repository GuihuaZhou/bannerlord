using ModifiedArmy.common;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Transport;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Models
{
    /// <summary>
    /// Approves gradual peacetime demobilization without changing rosters or
    /// issuing movement orders.
    /// </summary>
    public sealed class PeaceDemobilizationModel
        : IPeaceDemobilizationModel
    {
        /// <summary>
        /// Evaluates the live party target, stable-peace interval, daily
        /// budget and safety gates for one natural settlement visit.
        /// </summary>
        public PeaceDemobilizationDecision Evaluate(
            MobileParty party,
            PeaceDemobilizationContext context)
        {
            PeaceDemobilizationDecision result =
                new PeaceDemobilizationDecision
                {
                    ReasonCode = "InvalidParty"
                };
            DemobilizationConfig config = MilitaryConfigManager.Instance
                .GetActiveConfig()?.AiRecruitment.ForceManagement
                .Demobilization;
            if (config?.EnableAutomaticDemobilization != true)
            {
                result.ReasonCode = "AutomaticDemobilizationDisabled";
                return result;
            }
            Clan clan = party?.ActualClan;
            Settlement settlement = context?.VisitedSettlement;
            if (party == null || !party.IsActive || !party.IsLordParty ||
                party.IsMainParty || party.IsDisbanding ||
                party.LeaderHero == null || clan == null ||
                clan == Clan.PlayerClan)
            {
                return result;
            }
            if (settlement?.IsFortification != true ||
                party.CurrentSettlement != settlement ||
                settlement.OwnerClan != clan || settlement.IsUnderSiege ||
                party.MapEvent != null || party.Army != null ||
                party.CurrentSettlement?.SiegeEvent != null)
            {
                result.ReasonCode = "UnsafeOrNotAtClanFortification";
                return result;
            }
            if (TransportThreatModel.IsNativeAvoidanceActive(party) ||
                TransportThreatModel.HasSuperiorEnemy(
                    party,
                    party.Position,
                    party.MapFaction) ||
                party.TargetSettlement?.IsUnderSiege == true)
            {
                result.ReasonCode = "HigherPriorityMilitaryTask";
                return result;
            }
            if (ForceManagementFacade.IsAtFormalWar(clan))
            {
                result.ReasonCode = "AtFormalWar";
                return result;
            }
            if (context == null || context.PeaceSinceDay < 0 ||
                context.Today - context.PeaceSinceDay <
                    config.PeaceStabilityDays)
            {
                result.ReasonCode = "PeaceNotStable";
                return result;
            }

            PartyForceTargetResult target = ForceManagementFacade.Current
                .GetPartyTarget(party);
            result.TargetCount = target.TargetCount;
            result.CurrentCount = target.CurrentCount;
            result.SurplusCount = Math.Max(
                0,
                target.CurrentCount - target.TargetCount);
            if (target.Scenario != ForceScenario.Peace)
            {
                result.ReasonCode = "NotPeaceScenario";
                return result;
            }
            if (result.SurplusCount <= 0)
            {
                result.ReasonCode = "NoSurplus";
                return result;
            }

            int dailyLimit = Math.Min(
                config.MaxDailyDemobilizationCount,
                (int)Math.Ceiling(
                    target.CurrentCount *
                    config.MaxDailyDemobilizationRatio));
            int remainingBudget = Math.Max(
                0,
                dailyLimit - Math.Max(
                    0,
                    context.AlreadyDemobilizedToday));
            result.ApprovedToday = Math.Min(
                result.SurplusCount,
                remainingBudget);
            result.CanDemobilize = result.ApprovedToday > 0;
            result.ReasonCode = result.CanDemobilize
                ? "Approved"
                : "DailyLimitReached";
            return result;
        }
    }
}
