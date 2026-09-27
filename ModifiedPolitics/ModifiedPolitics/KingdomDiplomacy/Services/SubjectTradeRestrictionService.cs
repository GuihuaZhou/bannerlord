using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Keeps trade diplomacy independent for subjects while preventing both
    /// vassals and puppets from trading with an enemy of their overlord.
    /// </summary>
    public static class SubjectTradeRestrictionService
    {
        public static bool CanFormTradeAgreement(
            Kingdom first,
            Kingdom second,
            out TextObject reason)
        {
            reason = TextObject.GetEmpty();
            if (first == null || second == null || first == second)
            {
                return false;
            }

            if (CanSubjectTradeWith(first, second)
                && CanSubjectTradeWith(second, first))
            {
                return true;
            }

            reason = new TextObject(
                "{=ModifiedPolitics_SubjectEnemyTradeForbidden}A subject kingdom cannot sign a trade agreement with an enemy of its overlord.");
            return false;
        }

        /// <summary>
        /// Removes only agreements forbidden by the subject's current
        /// overlord. All other pre-existing trade agreements are preserved.
        /// </summary>
        public static void RemoveInvalidTradeAgreements(Kingdom subject)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (subject == null
                || manager?.GetSubjectType(subject) == SubjectType.None)
            {
                return;
            }

            ITradeAgreementsCampaignBehavior behavior = Campaign.Current
                ?.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
            if (behavior == null)
            {
                return;
            }

            foreach (Kingdom other in Kingdom.All)
            {
                if (other == null
                    || other == subject
                    || other.IsEliminated
                    || CanFormTradeAgreement(subject, other, out _))
                {
                    continue;
                }

                TradeAgreementsCampaignBehavior.TradeAgreement agreement;
                if (behavior.HasTradeAgreement(
                    subject,
                    other,
                    out agreement))
                {
                    behavior.EndTradeAgreement(subject, other);
                }
            }
        }

        private static bool CanSubjectTradeWith(
            Kingdom possibleSubject,
            Kingdom tradePartner)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager == null
                || manager.GetSubjectType(possibleSubject) == SubjectType.None)
            {
                return true;
            }

            Kingdom overlord = manager.GetOverlord(possibleSubject);
            return overlord == null
                || !FactionManager.IsAtWarAgainstFaction(
                    overlord,
                    tradePartner);
        }
    }
}
