// Creation-time recruitment projection. Native party-size ownership and the
// existing force/wage models remain authoritative; no party or roster is made.
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.PartyManagement.Finance.Models;
using ModifiedArmy.Recruitment.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Services
{
    /// <summary>Builds the immutable facts used before a lord party exists.</summary>
    public sealed class PlannedLordPartySubjectFactory
    {
        /// <summary>
        /// A newly planned lord party already contains its leader. This shared
        /// assumption keeps creation supply estimates aligned with the preview.
        /// </summary>
        public const int InitialMemberCount = 1;

        public RecruitmentSubject Create(
            Hero leader,
            Clan clan,
            Settlement site)
        {
            if (leader == null || clan == null || site == null ||
                leader.Clan != clan)
                return null;

            int assumedLimit = Math.Max(
                0,
                Campaign.Current.Models.PartySizeLimitModel
                    .GetAssumedPartySizeForLordParty(
                        leader,
                        clan.MapFaction,
                        clan));
            bool atWar = ForceManagementFacade.IsAtFormalWar(clan);
            PartyForceTargetResult target = ForceManagementFacade.Current
                .PartyTargets.Evaluate(new PartyForceEvaluationInput
                {
                    Clan = clan,
                    PartyLimit = assumedLimit,
                    CurrentCount = InitialMemberCount,
                    IsActive = false
                });
            return new RecruitmentSubject
            {
                Kind = RecruitmentSubjectKind.PlannedLordParty,
                Leader = leader,
                Clan = clan,
                Location = site,
                IsLordParty = true,
                IsActiveForAdmission = true,
                IsAtFormalWar = atWar,
                PartySizeLimit = assumedLimit,
                TargetCount = target.TargetCount,
                PaymentLimit = MobilePartyWageLimitCalculator.Calculate(
                    clan,
                    leader,
                    atWar),
                CurrentMemberCount = InitialMemberCount,
                CurrentRegularCount = 0
            };
        }
    }
}
