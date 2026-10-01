using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace ModifiedDiplomacy.KingdomDiplomacy.Services
{
    /// <summary>
    /// Prevents AI clans from reopening a recently concluded war through a
    /// kingdom decision. Direct war actions such as subject synchronization
    /// and an approved independence decision are intentionally unaffected.
    /// </summary>
    public static class AiWarProposalCooldownService
    {
        public const int CooldownDays = 21;

        public static bool CanProposeWar(DeclareWarDecision decision)
        {
            if (decision == null
                || decision.ProposerClan == Clan.PlayerClan)
            {
                return true;
            }

            Kingdom declaringKingdom = decision.Kingdom;
            Kingdom targetKingdom =
                decision.FactionToDeclareWarOn as Kingdom;
            if (declaringKingdom == null || targetKingdom == null)
            {
                return true;
            }

            // Bannerlord records the most recent peace on the bilateral
            // stance. Twenty-one full days must pass before an AI clan may
            // submit another declaration-of-war proposal.
            return declaringKingdom
                .GetStanceWith(targetKingdom)
                .PeaceDeclarationDate
                .ElapsedDaysUntilNow >= CooldownDays;
        }
    }
}
