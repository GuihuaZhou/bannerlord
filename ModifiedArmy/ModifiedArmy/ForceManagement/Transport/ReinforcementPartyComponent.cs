using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.ForceManagement.Transport
{
    public enum ReinforcementTransportState
    {
        TravelingToDestination,
        Unloading,
        Recovering,
        Completed
    }

    [SaveableRootClass(9)]
    public sealed class ReinforcementPartyComponent : PartyComponent
    {
        [SaveableProperty(1)]
        public Settlement SourceSettlement { get; private set; }
        [SaveableProperty(2)]
        public Settlement Destination { get; private set; }
        [SaveableProperty(3)]
        public Clan DispatchClan { get; private set; }
        [SaveableProperty(4)]
        public int StateValue { get; private set; }
        [SaveableProperty(5)]
        public int RequestedTroopCount { get; private set; }
        [SaveableProperty(6)]
        public int RequestedFoodCount { get; private set; }
        [SaveableProperty(7)]
        public Settlement RecoverySettlement { get; private set; }
        [SaveableProperty(8)]
        public CampaignTime Deadline { get; private set; }

        [CachedData]
        private TextObject _cachedName;
        private InitializationArgs _initializationArgs;

        public ReinforcementTransportState State =>
            (ReinforcementTransportState)StateValue;
        public override Hero PartyOwner => DispatchClan?.Leader;
        public override Settlement HomeSettlement => SourceSettlement;
        public override TextObject Name
        {
            get
            {
                if (_cachedName == null)
                {
                    _cachedName = new TextObject(
                        "{=ModifiedArmy_ReinforcementPartyName}" +
                        "Reinforcements for {SETTLEMENT}");
                    _cachedName.SetTextVariable("SETTLEMENT",
                        Destination?.Name ?? TextObject.GetEmpty());
                }
                return _cachedName;
            }
        }

        private ReinforcementPartyComponent(
            Settlement source,
            Settlement destination,
            Clan dispatchClan,
            int requestedTroops,
            int requestedFood,
            CampaignTime deadline,
            TroopRoster roster)
        {
            SourceSettlement = source;
            Destination = destination;
            DispatchClan = dispatchClan;
            RequestedTroopCount = Math.Max(0, requestedTroops);
            RequestedFoodCount = Math.Max(0, requestedFood);
            Deadline = deadline;
            StateValue = (int)ReinforcementTransportState
                .TravelingToDestination;
            _initializationArgs = new InitializationArgs(roster);
        }

        public static MobileParty Create(
            Settlement source,
            Settlement destination,
            Clan dispatchClan,
            int requestedTroops,
            int requestedFood,
            TroopRoster roster)
        {
            string id = "modifiedarmy_reinforcement_" +
                Guid.NewGuid().ToString("N");
            return MobileParty.CreateParty(id,
                new ReinforcementPartyComponent(
                    source, destination, dispatchClan,
                    requestedTroops, requestedFood,
                    CampaignTime.Now + CampaignTime.Days(20f), roster));
        }

        public void BeginUnloading() => StateValue =
            (int)ReinforcementTransportState.Unloading;

        public void BeginRecovery(Settlement settlement)
        {
            RecoverySettlement = settlement;
            StateValue = (int)ReinforcementTransportState.Recovering;
        }

        public void Complete() => StateValue =
            (int)ReinforcementTransportState.Completed;

        public override Banner GetDefaultComponentBanner() =>
            DispatchClan?.Banner ?? SourceSettlement?.Banner;

        public override void ClearCachedName() => _cachedName = null;

        protected override void OnMobilePartySetOnCreation()
        {
            MobileParty.ActualClan = DispatchClan;
            MobileParty.Aggressiveness = 0f;
            MobileParty.Ai.SetDoNotMakeNewDecisions(true);
            if (_initializationArgs != null)
            {
                MobileParty.InitializeMobilePartyAroundPosition(
                    _initializationArgs.Roster,
                    TroopRoster.CreateDummyTroopRoster(),
                    SourceSettlement.GatePosition,
                    1f, 0f, false);
                _initializationArgs = null;
            }
        }

        private sealed class InitializationArgs
        {
            public InitializationArgs(TroopRoster roster) => Roster = roster;
            public TroopRoster Roster { get; }
        }
    }
}
