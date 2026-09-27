using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedPolitics.KingdomDiplomacy.Behaviors
{
    /// <summary>
    /// Repairs invalid trade agreements after loading and whenever a new war
    /// turns a subject's existing trade partner into an overlord enemy.
    /// </summary>
    public sealed class SubjectTradeRestrictionBehavior
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
            RemoveAllInvalidSubjectTradeAgreements();
        }

        private static void OnWarDeclared(
            IFaction firstFaction,
            IFaction secondFaction,
            DeclareWarAction.DeclareWarDetail detail)
        {
            RemoveAllInvalidSubjectTradeAgreements();
        }

        private static void RemoveAllInvalidSubjectTradeAgreements()
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                SubjectTradeRestrictionService
                    .RemoveInvalidTradeAgreements(kingdom);
            }
        }
    }
}
