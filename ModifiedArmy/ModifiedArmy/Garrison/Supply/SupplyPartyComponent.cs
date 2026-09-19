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
    /// Ownership is derived from the current owner of the home settlement.
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

        [CachedData]
        private TextObject _cachedName;

        private InitializationArgs _initializationArgs;

        public SupplyPartyMissionState MissionState =>
            (SupplyPartyMissionState)MissionStateValue;

        public override Hero PartyOwner =>
            SupplyHomeSettlement?.OwnerClan?.Leader;

        public override Settlement HomeSettlement =>
            SupplyHomeSettlement;

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
            MissionStateValue =
                (int)SupplyPartyMissionState.TravelingToSource;
            _initializationArgs = initializationArgs;
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

        public void BeginReturnJourney()
        {
            MissionStateValue =
                (int)SupplyPartyMissionState.ReturningHome;
        }

        public void CompleteMission()
        {
            MissionStateValue =
                (int)SupplyPartyMissionState.Completed;
        }

        public void ChangeSource(
            Settlement sourceSettlement,
            int requestedFood)
        {
            SourceSettlement = sourceSettlement;
            RequestedFood = requestedFood;
            MissionStateValue =
                (int)SupplyPartyMissionState.TravelingToSource;
        }

        /// <summary>
        /// Synchronizes cached faction identity after ownership changes or
        /// loading a save.
        /// </summary>
        public void SynchronizeOwnership()
        {
            if (MobileParty == null || SupplyHomeSettlement?.OwnerClan == null)
            {
                return;
            }

            MobileParty.ActualClan = SupplyHomeSettlement.OwnerClan;
            ClearCachedName();
            MobileParty.Party.SetVisualAsDirty();
        }

        public override Banner GetDefaultComponentBanner()
        {
            return SupplyHomeSettlement?.Banner;
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
            MobileParty.ActualClan = SupplyHomeSettlement.OwnerClan;

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
        }
    }
}
