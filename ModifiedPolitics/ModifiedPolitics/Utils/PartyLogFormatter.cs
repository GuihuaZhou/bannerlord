using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Tool
{
    /// <summary>
    /// Builds the standard ownership prefix for party names written by
    /// ModifiedPolitics. Keeping this formatter local avoids requiring a
    /// newly compiled ModifiedArmy DLL merely to compile this project.
    /// </summary>
    public static class PartyLogFormatter
    {
        public static TextObject GetDisplayName(MobileParty party)
        {
            if (party == null)
            {
                return new TextObject(string.Empty);
            }

            Clan clan = party.ActualClan
                ?? party.LeaderHero?.Clan
                ?? party.CurrentSettlement?.OwnerClan;

            if (clan == null)
            {
                return party.Name;
            }

            TextObject result = clan.Kingdom != null
                ? new TextObject(
                    "{=ModifiedPolitics_PartyLogNameWithKingdom}" +
                    "{KINGDOM_NAME}{CLAN_NAME} clan's {PARTY_NAME}")
                : new TextObject(
                    "{=ModifiedPolitics_PartyLogNameWithoutKingdom}" +
                    "{CLAN_NAME} clan's {PARTY_NAME}");

            if (clan.Kingdom != null)
            {
                result.SetTextVariable(
                    "KINGDOM_NAME",
                    clan.Kingdom.Name);
            }

            result.SetTextVariable("CLAN_NAME", clan.Name);
            result.SetTextVariable("PARTY_NAME", party.Name);
            return result;
        }
    }
}
