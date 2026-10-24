using ModifiedArmy.ForceManagement.Contracts;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Resolves the ownership identity used exclusively by roster transfers.
    /// Bannerlord can leave a real garrison party's ActualClan unset even though
    /// the settlement has a valid owner. This policy treats only the settlement's
    /// authoritative garrison as belonging to that owner and never mutates party
    /// state to repair the missing field.
    /// </summary>
    public static class PartyTransferOwnershipPolicy
    {
        /// <summary>
        /// Returns the effective clan for transfer authorization. Field parties
        /// always use ActualClan; they may not borrow the clan of the settlement
        /// they currently occupy. A garrison receives the settlement owner only
        /// when it is the exact garrison instance owned by that settlement.
        /// </summary>
        public static bool TryGetEffectiveTransferClan(
            MobileParty party,
            Settlement settlement,
            out Clan clan)
        {
            clan = null;
            if (party == null || settlement?.Town == null)
                return false;

            MobileParty realGarrison = settlement.Town.GarrisonParty;
            if (party == realGarrison)
            {
                if (!party.IsGarrison || settlement.OwnerClan == null)
                    return false;
                clan = settlement.OwnerClan;
                return true;
            }

            clan = party.ActualClan;
            return clan != null;
        }

        /// <summary>
        /// Applies the shared ownership boundary used during both planning and
        /// execution. Exactly one endpoint must be the real garrison. Ordinary
        /// transfers require both effective clans to match the settlement owner.
        /// The existing newly-captured exception remains destination-only and
        /// limited to a field party from the owner's kingdom.
        /// </summary>
        public static bool IsAuthorizedRosterPair(
            MobileParty source,
            MobileParty destination,
            Settlement settlement,
            PartyExchangePurpose purpose)
        {
            MobileParty realGarrison = settlement?.Town?.GarrisonParty;
            if (source == null || destination == null || realGarrison == null ||
                !realGarrison.IsGarrison || !source.IsActive ||
                !destination.IsActive || source.MapEvent != null ||
                destination.MapEvent != null || settlement.IsUnderSiege)
                return false;

            // Requiring one and only one real-garrison endpoint prevents a party
            // carrying an IsGarrison flag from impersonating the settlement roster.
            bool sourceIsGarrison = source == realGarrison;
            bool destinationIsGarrison = destination == realGarrison;
            if (sourceIsGarrison == destinationIsGarrison)
                return false;

            if (TryGetEffectiveTransferClan(source, settlement, out Clan sourceClan) &&
                TryGetEffectiveTransferClan(destination, settlement, out Clan destinationClan) &&
                sourceClan == destinationClan && sourceClan == settlement.OwnerClan)
                return true;

            // Preserve the original one-way exception for depositing troops after
            // a capture. It never permits withdrawing troops across clan borders.
            return purpose == PartyExchangePurpose.NewlyCapturedSettlement &&
                destinationIsGarrison && source.ActualClan?.Kingdom != null &&
                source.ActualClan.Kingdom == settlement.OwnerClan?.Kingdom;
        }
    }
}
