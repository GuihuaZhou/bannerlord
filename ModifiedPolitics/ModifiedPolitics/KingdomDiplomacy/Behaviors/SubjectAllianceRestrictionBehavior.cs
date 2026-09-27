using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedPolitics.KingdomDiplomacy.Behaviors
{
    /// <summary>
    /// Cleans alliances left in older saves, invalidated by a newly created
    /// subject relation, or invalidated when an overlord enters a new war.
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
            // A vassal alliance may become illegal when its overlord declares
            // war on that ally. Rechecking all subjects also covers the same
            // situation when the other kingdom initiated the war.
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
