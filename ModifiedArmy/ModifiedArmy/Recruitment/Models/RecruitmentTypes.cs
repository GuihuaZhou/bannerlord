namespace ModifiedArmy.Recruitment.Models
{
    public enum RecruitmentSource
    {
        Volunteer,
        Fief,
        Prisoner,
        Transfer,
        Mercenary,
        Special
    }

    public enum RecruitmentPartyType
    {
        MobileParty,
        Garrison
    }

    public enum RecruitmentLimitReason
    {
        None,
        InvalidParty,
        InvalidTroop,
        PartySize,
        CombatRole,
        Quality,
        WageLimit,
        RecruitmentCost,
        MaintenanceFunds
    }

    /// <summary>
    /// A soft minimum and hard maximum expressed as fractions from zero to one.
    /// </summary>
    public sealed class RatioRange
    {
        public RatioRange(float minimumRatio, float maximumRatio)
        {
            MinimumRatio = minimumRatio;
            MaximumRatio = maximumRatio;
        }

        public float MinimumRatio { get; }

        public float MaximumRatio { get; }
    }
}
