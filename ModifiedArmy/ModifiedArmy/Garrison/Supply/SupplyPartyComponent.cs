using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Garrison.Supply
{
    /// <summary>
    /// Identifies a temporary supply party and stores its persistent route.
    /// Local mission ownership is captured at dispatch and never follows a
    /// later change of settlement owner.
    /// </summary>
    [SaveableRootClass(1)]
    public class SupplyPartyComponent : PartyComponent
    {
        [SaveableProperty(1)]
        public Settlement SupplyHomeSettlement { get; private set; }

        [SaveableProperty(2)]
        public Settlement SourceSettlement { get; private set; }

        [SaveableProperty(3)]
        public int MissionStateValue { get; private set; }

        [SaveableProperty(4)]
        public int RequestedFood { get; private set; }

        [SaveableProperty(5)]
        public List<Settlement> VisitedSettlements { get; private set; }
            = new List<Settlement>();

        [SaveableProperty(6)]
        public int MissionModeValue { get; private set; }

        [SaveableProperty(7)]
        public Settlement AidDestination { get; private set; }

        [SaveableProperty(8)]
        public Clan DispatchClan { get; private set; }

        [SaveableProperty(9)]
        public Settlement RecoverySettlement { get; private set; }

        [SaveableProperty(10)]
        public CampaignTime Deadline { get; private set; }

        [SaveableProperty(11)]
        public Clan OriginalDispatchClan { get; private set; }

        [SaveableProperty(12)]
        public bool RecoveryRequired { get; private set; }

        [SaveableProperty(13)]
        public string RecoveryReason { get; private set; }

        [SaveableProperty(14)]
        public bool PurchaseCommitted { get; private set; }

        [SaveableProperty(15)]
        public CampaignTime LastProgressTime { get; private set; }

        [CachedData]
        private TextObject _cachedName;

        private InitializationArgs _initializationArgs;

        public SupplyPartyMissionState MissionState =>
            (SupplyPartyMissionState)MissionStateValue;

        public SupplyPartyMissionMode MissionMode =>
            (SupplyPartyMissionMode)MissionModeValue;

        public bool IsAidMission =>
            MissionMode == SupplyPartyMissionMode.CrossSettlementAid;

        public override Hero PartyOwner =>
            (IsAidMission ? DispatchClan :
                OriginalDispatchClan)?.Leader;

        public override Settlement HomeSettlement =>
            SupplyHomeSettlement;

        // Marks the local logistics party as non-aggressive without disabling
        // MobilePartyAi decisions. Native short-term avoidance still requires
        // matching-version runtime verification for this custom component.
        public override bool AvoidHostileActions => !IsAidMission;

        public override TextObject Name
        {
            get
            {
                if (_cachedName == null)
                {
                    _cachedName = new TextObject(
                        "{=ModifiedArmy_SupplyPartyName}" +
                        "Supply Party of {SETTLEMENT}");
                    _cachedName.SetTextVariable(
                        "SETTLEMENT",
                        SupplyHomeSettlement?.Name
                            ?? TextObject.GetEmpty());
                }

                return _cachedName;
            }
        }

        private SupplyPartyComponent(
            Settlement homeSettlement,
            Settlement sourceSettlement,
            int requestedFood,
            InitializationArgs initializationArgs)
        {
            SupplyHomeSettlement = homeSettlement;
            SourceSettlement = sourceSettlement;
            RequestedFood = requestedFood;
            VisitedSettlements = new List<Settlement>();
            MissionStateValue =
                (int)SupplyPartyMissionState.TravelingToSource;
            OriginalDispatchClan = homeSettlement?.OwnerClan;
            LastProgressTime = CampaignTime.Now;
            _initializationArgs = initializationArgs;
        }

        private SupplyPartyComponent(
            Settlement sourceSettlement,
            Settlement aidDestination,
            Clan dispatchClan,
            int requestedFood,
            InitializationArgs initializationArgs)
            : this(sourceSettlement, sourceSettlement, requestedFood,
                initializationArgs)
        {
            MissionModeValue =
                (int)SupplyPartyMissionMode.CrossSettlementAid;
            AidDestination = aidDestination;
            DispatchClan = dispatchClan;
            MissionStateValue =
                (int)SupplyPartyMissionState.DeliveringAid;
            Deadline = CampaignTime.Now + CampaignTime.Days(Math.Max(1,
                MilitaryConfigManager.Instance.GetActiveConfig()?
                    .AiRecruitment.ForceManagement
                    .TransportMissionDeadlineDays ?? 20));
        }

        /// <summary>
        /// Creates a supply party with troops already removed from the home
        /// garrison. The party starts at the home settlement gate.
        /// </summary>
        public static MobileParty CreateSupplyParty(
            Settlement homeSettlement,
            Settlement sourceSettlement,
            int requestedFood,
            TroopRoster escortRoster)
        {
            string stringId =
                $"modifiedarmy_supply_{homeSettlement.StringId}";

            return MobileParty.CreateParty(
                stringId,
                new SupplyPartyComponent(
                    homeSettlement,
                    sourceSettlement,
                    requestedFood,
                    new InitializationArgs(escortRoster)));
        }

        public static MobileParty CreateAidParty(
            Settlement sourceSettlement,
            Settlement aidDestination,
            Clan dispatchClan,
            int requestedFood,
            TroopRoster escortRoster)
        {
            string stringId = "modifiedarmy_supply_aid_" +
                Guid.NewGuid().ToString("N");
            return MobileParty.CreateParty(stringId,
                new SupplyPartyComponent(sourceSettlement,
                    aidDestination, dispatchClan, requestedFood,
                    new InitializationArgs(escortRoster)));
        }

        public void BeginReturnJourney()
        {
            MissionStateValue =
                (int)SupplyPartyMissionState.ReturningHome;
            LastProgressTime = CampaignTime.Now;
        }

        public void CompleteMission()
        {
            MissionStateValue =
                (int)SupplyPartyMissionState.Completed;
            LastProgressTime = CampaignTime.Now;
        }

        public void BeginAidReturn()
        {
            MissionStateValue =
                (int)SupplyPartyMissionState.ReturningFromAid;
        }

        public void BeginRecovery(Settlement settlement)
        {
            RecoverySettlement = settlement;
            MissionStateValue =
                (int)SupplyPartyMissionState.Recovering;
        }

        public void BeginFriendlyRecovery(Settlement settlement)
        {
            RecoverySettlement = settlement;
            MissionStateValue =
                (int)SupplyPartyMissionState.RestingAtFriendlySettlement;
        }

        public void ChangeSource(Settlement sourceSettlement)
        {
            SourceSettlement = sourceSettlement;
            MissionStateValue =
                (int)SupplyPartyMissionState.TravelingToSource;
            LastProgressTime = CampaignTime.Now;
        }

        public void RecordSourceVisit(Settlement settlement, int acquiredFood)
        {
            VisitedSettlements ??= new List<Settlement>();

            if (settlement != null && !VisitedSettlements.Contains(settlement))
            {
                VisitedSettlements.Add(settlement);
            }

            RequestedFood = Math.Max(0, RequestedFood - acquiredFood);
            PurchaseCommitted = true;
            LastProgressTime = CampaignTime.Now;
        }

        public void RequireRecovery(string reason, Settlement settlement = null)
        {
            RecoveryRequired = true;
            RecoveryReason = reason ?? "RecoveryRequired";
            RecoverySettlement = settlement;
            LastProgressTime = CampaignTime.Now;
        }

        public void ClearRecovery()
        {
            RecoveryRequired = false;
            RecoveryReason = null;
            LastProgressTime = CampaignTime.Now;
        }

        public void RestoreLegacyDispatchClan(Clan clan)
        {
            if (!IsAidMission && OriginalDispatchClan == null && clan != null)
                OriginalDispatchClan = clan;
        }

        public bool HasVisited(Settlement settlement)
        {
            VisitedSettlements ??= new List<Settlement>();
            return settlement != null && VisitedSettlements.Contains(settlement);
        }

        /// <summary>
        /// Synchronizes cached faction identity after ownership changes or
        /// loading a save.
        /// </summary>
        public void SynchronizeOwnership()
        {
            Clan ownerClan = IsAidMission
                ? DispatchClan
                : OriginalDispatchClan;

            if (MobileParty == null
                || ownerClan == null
                || MobileParty.ActualClan == ownerClan)
            {
                return;
            }

            MobileParty.ActualClan = ownerClan;
            ClearCachedName();
            MobileParty.Party.SetVisualAsDirty();
        }

        public override Banner GetDefaultComponentBanner()
        {
            return (IsAidMission ? DispatchClan : OriginalDispatchClan)
                ?.Banner ?? SupplyHomeSettlement?.Banner;
        }

        /// <summary>
        /// Uses the native caravan pack-animal icon without identifying this
        /// party as a caravan to campaign trade and finance behaviors.
        /// </summary>
        public override void GetMountAndHarnessVisualIdsForPartyIcon(
            PartyBase party,
            out string mountStringId,
            out string harnessStringId)
        {
            string cultureId =
                party.MapFaction?.Culture?.StringId ?? string.Empty;

            if (cultureId == "aserai" || cultureId == "khuzait")
            {
                mountStringId = "camel";
                harnessStringId = party.Index % 2 == 0
                    ? "camel_saddle_a"
                    : "camel_saddle_b";
                return;
            }

            mountStringId = "mule";

            switch (party.Index % 3)
            {
                case 0:
                    harnessStringId = "mule_load_a";
                    break;
                case 1:
                    harnessStringId = "mule_load_b";
                    break;
                default:
                    harnessStringId = "mule_load_c";
                    break;
            }
        }

        public override void ClearCachedName()
        {
            _cachedName = null;
        }

        protected override void OnMobilePartySetOnCreation()
        {
            MobileParty.Aggressiveness = 0f;
            MobileParty.ActualClan = IsAidMission
                ? DispatchClan
                : OriginalDispatchClan ?? SupplyHomeSettlement.OwnerClan;
            MobileParty.Ai.SetDoNotMakeNewDecisions(false);

            if (_initializationArgs != null)
            {
                MobileParty.InitializeMobilePartyAroundPosition(
                    _initializationArgs.EscortRoster,
                    TroopRoster.CreateDummyTroopRoster(),
                    SupplyHomeSettlement.GatePosition,
                    1f,
                    0f,
                    false);
                _initializationArgs = null;
            }
        }

        private sealed class InitializationArgs
        {
            public InitializationArgs(TroopRoster escortRoster)
            {
                EscortRoster = escortRoster;
            }

            public TroopRoster EscortRoster { get; }
        }
    }

    /// <summary>
    /// Registers the custom party component with Bannerlord's save system.
    /// </summary>
    public class SupplyPartySaveDefiner : SaveableTypeDefiner
    {
        public SupplyPartySaveDefiner()
            : base(20260919)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(SupplyPartyComponent), 1);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(
                typeof(Dictionary<Settlement, CampaignTime>));
            ConstructContainerDefinition(
                typeof(Dictionary<Settlement, MobileParty>));
            ConstructContainerDefinition(typeof(List<Settlement>));
        }
    }
}
