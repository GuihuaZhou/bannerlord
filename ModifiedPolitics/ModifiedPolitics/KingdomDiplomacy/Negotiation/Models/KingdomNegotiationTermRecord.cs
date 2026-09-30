using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Models
{
    /// <summary>
    /// Save-compatible representation of one formal negotiation term. The UI
    /// draft uses an object subject for convenience, but a pending kingdom
    /// decision must persist concrete campaign-object references.
    /// </summary>
    [SaveableRootClass(12)]
    public sealed class KingdomNegotiationTermRecord
    {
        [SaveableProperty(1)]
        public KingdomNegotiationTermType Type { get; private set; }

        [SaveableProperty(2)]
        public Kingdom ProviderKingdom { get; private set; }

        [SaveableProperty(3)]
        public Settlement Settlement { get; private set; }

        [SaveableProperty(4)]
        public Hero Hero { get; private set; }

        [SaveableProperty(5)]
        public int Amount { get; private set; }

        public KingdomNegotiationTermRecord(
            KingdomNegotiationTermType type,
            Kingdom providerKingdom,
            Settlement settlement,
            Hero hero,
            int amount)
        {
            Type = type;
            ProviderKingdom = providerKingdom;
            Settlement = settlement;
            Hero = hero;
            Amount = amount;
        }

        public static KingdomNegotiationTermRecord FromDraftTerm(
            KingdomNegotiationDraftTerm term)
        {
            return new KingdomNegotiationTermRecord(
                term.Type,
                term.ProviderKingdom,
                term.Subject as Settlement,
                term.Subject as Hero,
                term.Amount);
        }

        public KingdomNegotiationDraftTerm ToDraftTerm()
        {
            object subject;
            switch (Type)
            {
                case KingdomNegotiationTermType.Settlement:
                    subject = Settlement;
                    break;
                case KingdomNegotiationTermType.PrisonerHero:
                case KingdomNegotiationTermType.Gold:
                    subject = Hero;
                    break;
                default:
                    subject = ProviderKingdom;
                    break;
            }

            return new KingdomNegotiationDraftTerm(
                Type,
                ProviderKingdom,
                subject,
                Amount);
        }
    }
}
