namespace ModifiedPolitics.KingdomDiplomacy.Negotiation
{
    /// <summary>
    /// Identifies a clause in an independent kingdom negotiation draft.
    /// </summary>
    public enum KingdomNegotiationTermType
    {
        Peace,
        TradeAgreement,
        Alliance,
        Gold,
        Settlement,
        PrisonerHero,
        TargetBecomesVassal,
        TargetBecomesPuppet,
        PlayerBecomesVassal,
        PlayerBecomesPuppet
    }

    public static class KingdomNegotiationTermRules
    {
        public static bool IsSubjectTerm(
            KingdomNegotiationTermType type)
        {
            return type == KingdomNegotiationTermType.TargetBecomesVassal
                || type == KingdomNegotiationTermType.TargetBecomesPuppet
                || type == KingdomNegotiationTermType.PlayerBecomesVassal
                || type == KingdomNegotiationTermType.PlayerBecomesPuppet;
        }

        public static bool IsBilateralTreaty(
            KingdomNegotiationTermType type)
        {
            return type == KingdomNegotiationTermType.Peace
                || type == KingdomNegotiationTermType.TradeAgreement
                || type == KingdomNegotiationTermType.Alliance;
        }
    }
}
