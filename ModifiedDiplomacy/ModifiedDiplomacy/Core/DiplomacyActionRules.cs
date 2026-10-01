namespace ModifiedDiplomacy.Core
{
    /// <summary>
    /// Classifies actions before any scoring occurs. This prevents unilateral
    /// actions such as declarations of war from accidentally being evaluated
    /// as contracts that require the target kingdom's consent.
    /// </summary>
    public static class DiplomacyActionRules
    {
        public static bool RequiresTargetConsent(DiplomacyActionType type)
        {
            switch (type)
            {
                case DiplomacyActionType.DeclareWar:
                case DiplomacyActionType.EndAlliance:
                case DiplomacyActionType.EndTradeAgreement:
                case DiplomacyActionType.ReleaseSubject:
                case DiplomacyActionType.DeclareIndependence:
                    return false;

                default:
                    return true;
            }
        }

        public static bool ChangesBilateralRelation(DiplomacyActionType type)
        {
            switch (type)
            {
                case DiplomacyActionType.DeclareWar:
                case DiplomacyActionType.MakePeace:
                case DiplomacyActionType.FormAlliance:
                case DiplomacyActionType.EndAlliance:
                case DiplomacyActionType.FormTradeAgreement:
                case DiplomacyActionType.EndTradeAgreement:
                case DiplomacyActionType.EstablishVassalage:
                case DiplomacyActionType.EstablishPuppetRelation:
                case DiplomacyActionType.ReleaseSubject:
                case DiplomacyActionType.DeclareIndependence:
                    return true;

                default:
                    return false;
            }
        }

        public static bool TransfersAsset(DiplomacyActionType type)
        {
            return type == DiplomacyActionType.TransferGold
                || type == DiplomacyActionType.TransferSettlement
                || type == DiplomacyActionType.ReleasePrisoner;
        }
    }
}
