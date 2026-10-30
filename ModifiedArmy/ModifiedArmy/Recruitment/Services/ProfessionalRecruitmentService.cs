using System;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Services
{
    internal sealed class ProfessionalRecruitmentCommit
    {
        public MobileParty Garrison { get; set; }
        public CharacterObject Troop { get; set; }
        public int Count { get; set; }
        public int RosterBefore { get; set; }
        public ProfessionalOfferConsumption Consumption { get; set; }
    }

    /// <summary>Single commit path from a professional offer into a garrison.</summary>
    internal static class ProfessionalRecruitmentService
    {
        public static bool TryCommit(Settlement settlement, MobileParty garrison,
            CharacterObject troop, int count,
            SettlementManpowerProductionBehavior pools,
            out ProfessionalRecruitmentCommit commit)
        {
            commit = null;
            if (settlement == null || garrison == null || troop == null ||
                count <= 0 || pools == null)
            {
                return false;
            }
            int rosterBefore = garrison.MemberRoster.GetElementNumber(troop);
            if (!pools.TryConsumeProfessionalOffer(
                    settlement, troop, count,
                    out ProfessionalOfferConsumption consumption))
                return false;
            try
            {
                garrison.MemberRoster.AddToCounts(
                    troop, count, false, 0, 0, true, -1);
                commit = new ProfessionalRecruitmentCommit
                {
                    Garrison = garrison,
                    Troop = troop,
                    Count = count,
                    RosterBefore = rosterBefore,
                    Consumption = consumption
                };
                return true;
            }
            catch (Exception exception)
            {
                Exception rollbackFailure = null;
                try
                {
                    int added = Math.Max(0,
                        garrison.MemberRoster.GetElementNumber(troop) -
                        rosterBefore);
                    if (added > 0)
                        garrison.MemberRoster.AddToCounts(
                            troop, -added, false, 0, 0, true, -1);
                    pools.RollbackProfessionalOffer(consumption);
                }
                catch (Exception rollbackException)
                {
                    rollbackFailure = rollbackException;
                    TextObject rollbackMessage = GameTexts.FindText(
                        "str_modifiedarmy_professional_recruitment_rollback_failed");
                    rollbackMessage.SetTextVariable(
                        "ERROR", rollbackException.ToString());
                    ModLogger.Error(rollbackMessage.ToString());
                }
                TextObject commitMessage = GameTexts.FindText(
                    "str_modifiedarmy_professional_recruitment_commit_failed");
                commitMessage.SetTextVariable("ERROR", exception.ToString());
                ModLogger.Error(commitMessage.ToString());
                if (rollbackFailure != null)
                    throw new InvalidOperationException(
                        "Professional recruitment source rollback failed.",
                        rollbackFailure);
                return false;
            }
        }

        public static bool TryRollback(
            ProfessionalRecruitmentCommit commit,
            SettlementManpowerProductionBehavior pools,
            out Exception failure)
        {
            failure = null;
            if (commit?.Garrison == null || commit.Troop == null ||
                commit.Consumption == null || pools == null) return false;
            try
            {
                int added = Math.Max(0,
                    commit.Garrison.MemberRoster.GetElementNumber(commit.Troop) -
                    commit.RosterBefore);
                if (added > 0)
                    commit.Garrison.MemberRoster.AddToCounts(
                        commit.Troop, -added, false, 0, 0, true, -1);
                pools.RollbackProfessionalOffer(commit.Consumption);
                return commit.Garrison.MemberRoster.GetElementNumber(
                    commit.Troop) == commit.RosterBefore;
            }
            catch (Exception exception)
            {
                failure = exception;
                return false;
            }
        }
    }
}
