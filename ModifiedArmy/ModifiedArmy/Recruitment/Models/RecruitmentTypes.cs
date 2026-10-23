using ModifiedArmy.ForceManagement.Contracts;

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
        MaintenanceFunds
    }

    /// <summary>
    /// Explicit bridge from the legacy strategic preference enum. This keeps
    /// garrison transfer distinct from genuinely adding a newly bought troop.
    /// </summary>
    public static class LegacyRecruitSourceMap
    {
        /// <summary>Maps the legacy preference into the unified planner.</summary>
        public static PartyForceSourceKind ToPlanningSource(
            global::ModifiedArmy.common.RecruitSource source)
        {
            switch (source)
            {
                case global::ModifiedArmy.common.RecruitSource.Fief:
                    return PartyForceSourceKind.Fief;
                case global::ModifiedArmy.common.RecruitSource.GarrisonTransfer:
                    return PartyForceSourceKind.Professional;
                case global::ModifiedArmy.common.RecruitSource.Mercenary:
                    return PartyForceSourceKind.Mercenary;
                default:
                    return PartyForceSourceKind.None;
            }
        }

        /// <summary>
        /// Maps only for compatibility with the existing approval model;
        /// garrison soldiers remain a transfer rather than a purchase.
        /// </summary>
        public static RecruitmentSource ToRecruitmentSource(
            global::ModifiedArmy.common.RecruitSource source)
        {
            switch (source)
            {
                case global::ModifiedArmy.common.RecruitSource.Fief:
                    return RecruitmentSource.Fief;
                case global::ModifiedArmy.common.RecruitSource.GarrisonTransfer:
                    return RecruitmentSource.Transfer;
                case global::ModifiedArmy.common.RecruitSource.Mercenary:
                    return RecruitmentSource.Mercenary;
                default:
                    return RecruitmentSource.Special;
            }
        }
    }

}
