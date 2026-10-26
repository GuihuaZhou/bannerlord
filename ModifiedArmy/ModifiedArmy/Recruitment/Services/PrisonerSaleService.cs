using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Services
{
    public static class PrisonerSaleService
    {
        public static void HandleSettlementEntry(
            MobileParty party, Settlement settlement)
        {
            if (party == null || party.IsMainParty ||
                settlement?.IsFortification != true ||
                party.MapFaction == null || party.IsDisbanding ||
                party.MapFaction.IsAtWarWith(settlement.MapFaction) ||
                party.PrisonRoster.TotalRegulars <= 0 &&
                !HasEnemyHeroPrisoner(party, settlement)) return;

            TroopRoster transfer = TroopRoster.CreateDummyTroopRoster();
            TroopRoster sale = TroopRoster.CreateDummyTroopRoster();
            int retainCapacity = Math.Max(
                0, party.Party.PrisonerSizeLimit / 2);
            int transferCapacity = settlement.OwnerClan == party.Owner?.Clan
                ? Math.Max(0, settlement.Party.PrisonerSizeLimit -
                    settlement.Party.PrisonRoster.TotalManCount)
                : 0;

            foreach (TroopRosterElement element in
                party.PrisonRoster.GetTroopRoster())
            {
                if (element.Character?.IsHero == true)
                {
                    if (!element.Character.IsPlayerCharacter &&
                        element.Character.HeroObject.MapFaction
                            .IsAtWarWith(settlement.MapFaction))
                        transfer.Add(element);
                    continue;
                }
                CharacterObject troop = element.Character;
                if (troop == null) continue;
                bool useful = troop.Occupation == Occupation.Mercenary ||
                    troop.Tier >= 3;
                int retained = useful
                    ? Math.Min(element.Number, retainCapacity) : 0;
                retainCapacity -= retained;
                int remaining = element.Number - retained;
                int transferred = useful
                    ? Math.Min(remaining, transferCapacity) : 0;
                transferCapacity -= transferred;
                AddPrisoners(transfer, element, transferred);
                AddPrisoners(sale, element, remaining - transferred);
            }

            if (transfer.TotalManCount > 0)
            {
                MoveRegularPrisoners(party.PrisonRoster,
                    settlement.Party.PrisonRoster, transfer);
                if (RecruitmentLogFilter.ShouldLog(party))
                {
                    TextObject message = GameTexts.FindText(
                        "str_modifiedarmy_prisoner_transfer");
                    message.SetTextVariable("PARTY_NAME",
                        PartyLogFormatter.GetDisplayName(party));
                    message.SetTextVariable("COUNT", transfer.TotalRegulars);
                    message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
                    ModLogger.Info(message.ToString());
                }
            }
            if (sale.TotalRegulars > 0)
                SellPrisonersAction.ApplyForSelectedPrisoners(
                    party.Party, null, sale);
        }

        private static bool HasEnemyHeroPrisoner(
            MobileParty party, Settlement settlement) =>
            party.PrisonRoster.TotalHeroes > 0 &&
            party.PrisonRoster.GetTroopRoster().Exists(element =>
                element.Character != CharacterObject.PlayerCharacter &&
                element.Character?.HeroObject?.MapFaction != null &&
                element.Character.HeroObject.MapFaction
                    .IsAtWarWith(settlement.MapFaction));

        private static void AddPrisoners(TroopRoster target,
            TroopRosterElement source, int count)
        {
            if (count <= 0) return;
            int wounded = Math.Min(count, MBRandom.RoundRandomized(
                source.WoundedNumber * (float)count / source.Number));
            target.AddToCounts(source.Character, count, false, wounded,
                0, true, -1);
        }

        private static void MoveRegularPrisoners(TroopRoster source,
            TroopRoster target, TroopRoster transfer)
        {
            foreach (TroopRosterElement element in transfer.GetTroopRoster())
            {
                source.AddToCounts(element.Character, -element.Number, false,
                    -element.WoundedNumber, 0, true, -1);
                target.AddToCounts(element.Character, element.Number, false,
                    element.WoundedNumber, 0, true, -1);
            }
        }
    }
}
