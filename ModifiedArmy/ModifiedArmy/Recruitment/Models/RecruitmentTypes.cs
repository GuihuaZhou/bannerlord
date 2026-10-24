namespace ModifiedArmy.Recruitment.Models
{
    public enum RecruitmentSource
    {
        ProfessionalProduction,
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
        WageLimit,
        RecruitmentCost,
        MaintenanceFunds,
        BanditTroopExcluded
    }

}
