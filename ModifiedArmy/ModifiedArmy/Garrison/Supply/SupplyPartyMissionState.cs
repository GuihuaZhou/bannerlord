namespace ModifiedArmy.Garrison.Supply
{
    /// <summary>
    /// Persistent mission stages for a temporary garrison supply party.
    /// </summary>
    public enum SupplyPartyMissionState
    {
        TravelingToSource = 0,
        ReturningHome = 1,
        Completed = 2,
        DeliveringAid = 3,
        ReturningFromAid = 4,
        Recovering = 5
    }

    public enum SupplyPartyMissionMode
    {
        LocalForaging = 0,
        CrossSettlementAid = 1
    }
}
