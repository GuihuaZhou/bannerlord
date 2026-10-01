using ModifiedDiplomacy.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedDiplomacy.Subjects.Behaviors
{
    /// <summary>
    /// Repairs trade agreements that became illegal because an overlord went
    /// to war. This behavior owns no save data and is therefore safe to move
    /// before the subject-relation persistence layer.
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
