namespace ModifiedPolitics.HeroOffices.Domain
{
    /// <summary>
    /// Stable identifiers for every office. Never reorder existing values because they are saved.
    /// </summary>
    public enum OfficeType
    {
        Marshal = 0,
        ChiefMinister = 1,
        CourtSteward = 2,
        TaxOfficer = 3,
        AgricultureOfficer = 4,
        MilitaryOfficer = 5,
        SecurityOfficer = 6
    }

    public enum OfficeCompensation
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }
}
