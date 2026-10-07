using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.Domain
{
    public static class OfficeText
    {
        public static TextObject GetName(OfficeType officeType)
        {
            switch (officeType)
            {
                case OfficeType.Marshal:
                    return new TextObject("{=MP_OfficeMarshal}Marshal");
                case OfficeType.ChiefMinister:
                    return new TextObject("{=MP_OfficeChiefMinister}Chief Minister");
                case OfficeType.CourtSteward:
                    return new TextObject("{=MP_OfficeCourtSteward}Court Steward");
                case OfficeType.TaxOfficer:
                    return new TextObject("{=MP_OfficeTaxOfficer}Tax Officer");
                case OfficeType.AgricultureOfficer:
                    return new TextObject("{=MP_OfficeAgricultureOfficer}Agriculture Officer");
                case OfficeType.MilitaryOfficer:
                    return new TextObject("{=MP_OfficeMilitaryOfficer}Military Officer");
                case OfficeType.SecurityOfficer:
                    return new TextObject("{=MP_OfficeSecurityOfficer}Security Officer");
                default:
                    return new TextObject("{=MP_OfficeUnknown}Unknown Office");
            }
        }
    }
}
