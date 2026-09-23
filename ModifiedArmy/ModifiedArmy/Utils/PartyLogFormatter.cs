using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Tool
{
    /// <summary>
    /// Builds a consistent, localized ownership prefix for party log messages.
    /// </summary>
    public static class PartyLogFormatter
    {
        /// <summary>
        /// Returns "kingdom + clan family + party" when a kingdom exists, or
        /// "clan family + party" when it does not. Parties without a clan keep
        /// their original name.
        /// </summary>
        public static TextObject GetDisplayName(MobileParty party)
        {
            if (party == null)
            {
                return new TextObject(string.Empty);
            }

            Clan clan = ResolveClan(party);
            if (clan == null)
            {
                return party.Name;
            }

            Kingdom kingdom = clan.Kingdom;
            TextObject result = kingdom != null
                ? GameTexts.FindText(
                    "str_modifiedarmy_party_log_name_with_kingdom")
                : GameTexts.FindText(
                    "str_modifiedarmy_party_log_name_without_kingdom");

            if (kingdom != null)
            {
                result.SetTextVariable("KINGDOM_NAME", kingdom.Name);
            }

            result.SetTextVariable("CLAN_NAME", clan.Name);
            result.SetTextVariable("PARTY_NAME", party.Name);
            return result;
        }

        /// <summary>
        /// Returns the same ownership prefix without a party name. Settlement
        /// and garrison messages use this form.
        /// </summary>
        public static TextObject GetClanDisplayName(Clan clan)
        {
            if (clan == null)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_party_log_owner_unknown");
            }

            TextObject result = clan.Kingdom != null
                ? GameTexts.FindText(
                    "str_modifiedarmy_party_log_clan_with_kingdom")
                : GameTexts.FindText(
                    "str_modifiedarmy_party_log_clan_without_kingdom");

            if (clan.Kingdom != null)
            {
                result.SetTextVariable(
                    "KINGDOM_NAME",
                    clan.Kingdom.Name);
            }
            result.SetTextVariable("CLAN_NAME", clan.Name);
            return result;
        }

        private static Clan ResolveClan(MobileParty party)
        {
            return party.ActualClan
                ?? party.LeaderHero?.Clan
                ?? party.CurrentSettlement?.OwnerClan;
        }
    }
}
