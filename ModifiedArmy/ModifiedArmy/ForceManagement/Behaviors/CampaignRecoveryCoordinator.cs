using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.Recruitment.Preferences.Behaviors;
using System;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    public enum CampaignRecoveryPhase
    {
        Constructing,
        LoadingPersistentState,
        RebuildingRuntimeIndexes,
        Ready,
        Failed
    }

    /// <summary>
    /// Final recovery barrier for campaign-scoped services. Specialist
    /// behaviors retain their existing save keys and load handlers; this
    /// behavior runs last, rebuilds their shared dependency indexes, then
    /// permits new resource transactions.
    /// </summary>
    public sealed class CampaignRecoveryCoordinator : CampaignBehaviorBase
    {
        private bool _finalized;

        public CampaignRecoveryPhase Phase { get; private set; } =
            CampaignRecoveryPhase.Constructing;

        public bool IsReady => Phase == CampaignRecoveryPhase.Ready;

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this, OnGameLoadFinished);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this, OnSessionLaunched);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(
                this, OnNewGameCreated);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (!dataStore.IsLoading) return;
            _finalized = false;
            Phase = CampaignRecoveryPhase.LoadingPersistentState;
        }

        public static bool CanMutateCampaignState()
        {
            CampaignRecoveryCoordinator coordinator = Campaign.Current?
                .GetCampaignBehavior<CampaignRecoveryCoordinator>();
            // Compatibility for tests or older bootstrap paths that do not
            // install this behavior. Live campaigns always register it.
            return coordinator == null || coordinator.IsReady;
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            _finalized = false;
            Phase = CampaignRecoveryPhase.Constructing;
        }

        private void OnGameLoadFinished()
        {
            // Persistent objects are now available, but session-launched
            // initialization owned by fief/supply behaviors may still follow.
            FinalizeRecovery(false);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // New games have no loading callback. For loaded games this is
            // the final barrier after every earlier-registered session handler.
            FinalizeRecovery(true);
        }

        private void FinalizeRecovery(bool markReady)
        {
            if (_finalized) return;
            Phase = CampaignRecoveryPhase.RebuildingRuntimeIndexes;
            try
            {
                FiefPartyManager fiefs = Campaign.Current?
                    .GetCampaignBehavior<FiefPartyManager>();
                fiefs?.RebuildServingDetachmentIndex();

                Campaign.Current?
                    .GetCampaignBehavior<PartySettlementBindingBehavior>()?
                    .RevalidateAndRebuildIndexes();

                // Revalidate saved AI tasks only after fief batches and party
                // settlement bindings resolve.
                Campaign.Current?
                    .GetCampaignBehavior<AiForcePlanningBehavior>()?
                    .RevalidateAfterCampaignRecovery();

                Campaign.Current?
                    .GetCampaignBehavior<SettlementProfessionalPreferenceBehavior>()?
                    .RevalidateAfterCampaignRecovery();

                // Reading the journal here proves SyncData has produced its
                // lookup before writes are enabled. No operation is replayed.
                Campaign.Current?
                    .GetCampaignBehavior<TransferOperationJournalBehavior>();

                if (markReady)
                {
                    _finalized = true;
                    Phase = CampaignRecoveryPhase.Ready;
                }
            }
            catch
            {
                Phase = CampaignRecoveryPhase.Failed;
                throw;
            }
        }
    }
}
