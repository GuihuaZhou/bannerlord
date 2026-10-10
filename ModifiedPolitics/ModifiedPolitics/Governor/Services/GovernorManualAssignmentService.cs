using System.Collections.Generic;
using ModifiedPolitics.Governor.Models;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Services
{
    /// <summary>
    /// Routes ruler-issued governor changes through the same execution and settlement services.
    /// </summary>
    public sealed class GovernorManualAssignmentService
    {
        private readonly GovernorAssignmentExecutor _executor = new GovernorAssignmentExecutor();
        private readonly GovernorRelationService _relations = new GovernorRelationService();
        private readonly GovernorNotificationService _notifications = new GovernorNotificationService();

        public bool TryApply(Kingdom kingdom, Town destination, Hero selectedGovernor)
        {
            if (kingdom == null || destination?.OwnerClan?.Kingdom != kingdom)
                return false;

            Hero current = destination.Governor;
            if (WouldInvalidateLocalOffice(current, destination, selectedGovernor))
                return false;
            if (selectedGovernor != null && WouldMoveLocalOfficerToWrongType(selectedGovernor, destination))
                return false;

            var targets = new Dictionary<Town, Hero>();
            var contexts = new Dictionary<Town, GovernorSettlementContext>();
            Town origin = selectedGovernor?.GovernorOf;
            if (origin != null && origin != destination)
            {
                targets[origin] = null;
                contexts[origin] = GovernorAssignmentModel.BuildContext(kingdom, origin);
            }

            targets[destination] = selectedGovernor;
            contexts[destination] = GovernorAssignmentModel.BuildContext(kingdom, destination);
            var plan = new GovernorAssignmentPlan(kingdom, contexts, targets, new HashSet<Town>());
            GovernorAssignmentResult result = _executor.Execute(plan);
            if (result.FailedTowns.Count > 0)
                return false;

            _relations.Apply(plan, result);
            _notifications.Publish(plan, result);
            return result.After[destination] == selectedGovernor;
        }

        private static bool WouldInvalidateLocalOffice(Hero current, Town destination, Hero replacement)
        {
            if (current == null || current == replacement)
                return false;

            var office = HeroOfficeBehavior.Current?.GetAssignment(current);
            return office != null && OfficeRules.IsLocal(office.OfficeType);
        }

        private static bool WouldMoveLocalOfficerToWrongType(Hero hero, Town destination)
        {
            var office = HeroOfficeBehavior.Current?.GetAssignment(hero);
            if (office == null || !OfficeRules.IsLocal(office.OfficeType))
                return false;

            return office.OfficeType == OfficeType.MilitaryOfficer
                ? !destination.IsCastle
                : !destination.IsTown;
        }
    }
}
