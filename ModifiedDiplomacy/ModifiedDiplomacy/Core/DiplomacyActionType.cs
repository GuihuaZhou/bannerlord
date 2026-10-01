namespace ModifiedDiplomacy.Core
{
    /// <summary>
    /// Stable vocabulary shared by single-action diplomacy, compound
    /// proposals, AI evaluation and UI. It deliberately describes intent,
    /// rather than a particular Bannerlord decision implementation.
    /// </summary>
    public enum DiplomacyActionType
    {
        DeclareWar,
        MakePeace,
        FormAlliance,
        EndAlliance,
        FormTradeAgreement,
        EndTradeAgreement,
        EstablishVassalage,
        EstablishPuppetRelation,
        ReleaseSubject,
        DeclareIndependence,
        TransferGold,
        TransferSettlement,
        ReleasePrisoner,
        JoinWar
    }
}
