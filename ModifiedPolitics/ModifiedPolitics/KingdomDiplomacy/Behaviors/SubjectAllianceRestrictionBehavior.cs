using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Behaviors
{
    /// <summary>
    /// Cleans alliances left in older saves or created before a kingdom became
    /// a subject. New alliances are blocked separately at the native mutation
    /// entry point.
    /// </summary>
    public sealed class SubjectAllianceRestrictionBehavior
        : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnSessionLaunched(CampaignGameStarter starter)
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                SubjectAllianceRestrictionService
                    .RemoveExistingAlliances(kingdom);
            }
        }
    }
}
