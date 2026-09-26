namespace ModifiedPolitics.KingdomDiplomacy.Models
{
    /// <summary>
    /// Runtime reason used to explain why a subject relation ended in war.
    /// This value is not persistent because the resulting diplomatic state is.
    /// </summary>
    public enum SubjectIndependenceReason
    {
        VoluntaryDeclaration = 0,
        PuppetDefiance = 1,
        WarWithOverlord = 2,
        OverlordAggression = 3
    }
}
