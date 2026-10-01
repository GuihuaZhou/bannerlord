using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedDiplomacy.Subjects.Behaviors
{
    /// <summary>
    /// Repairs alliances left in older saves or invalidated by a change in
    /// subject status. The relation store remains in ModifiedPolitics during
    /// the migration, so this behavior temporarily calls its rule service.
    /// </summary>
    public sealed class SubjectAllianceRestrictionBehavior
        : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
            CampaignEvents.WarDeclared.AddNonSerializedListener(
                this,
                OnWarDeclared);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnSessionLaunched(CampaignGameStarter starter)
        {
            RemoveAllInvalidSubjectAlliances();
        }

        private static void OnWarDeclared(
            IFaction firstFaction,
            IFaction secondFaction,
            DeclareWarAction.DeclareWarDetail detail)
        {
            RemoveAllInvalidSubjectAlliances();
        }

        private static void RemoveAllInvalidSubjectAlliances()
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                SubjectAllianceRestrictionService
                    .RemoveExistingAlliances(kingdom);
            }
        }
    }
}
