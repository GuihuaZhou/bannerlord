using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models;
using ModifiedArmy.Tool;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Services
{
    public static class DisbandSettlementCoordinator
    {
        public static bool HandleFortificationMerge(
            MobileParty party, Settlement settlement)
        {
            if (party?.IsLordParty != true || party.IsMainParty ||
                party.ActualClan == null) return true;

            HandlePrisoners(party, settlement);
            if (party.MemberRoster.TotalManCount <= 0) return false;

            DisbandSettlementResult result = new DisbandSettlementService()
                .SettleDisbandingParty(party, settlement);
            if (party.MapFaction == settlement.MapFaction)
            {
                if (settlement.Town.GarrisonParty == null)
                    settlement.AddGarrisonParty();
                MobileParty garrison = settlement.Town.GarrisonParty;
                if (garrison != null)
                {
                    TransferHeroes(party.MemberRoster, garrison);
                    ApplyInfluence(party, settlement, result.AcceptedTroops);
                    GarrisonRecruitFromPrisonersBehavior
                        .NormalizeGarrisonNow(settlement.Town);
                    LogMergeOverflow(settlement, garrison);
                }
            }
            party.MemberRoster.Clear();
            LogResult(party, settlement, result ??
                new DisbandSettlementResult
                {
                    PartyId = party.StringId,
                    ReasonCode = "NoLegalGarrison"
                });
            return false;
        }

        public static void HandleFinalization(
            MobileParty party, Settlement settlement)
        {
            if (party?.IsLordParty != true || party.IsMainParty ||
                party.ActualClan == null ||
                settlement?.IsFortification == true &&
                !settlement.IsUnderSiege) return;
            DisbandSettlementResult result = new DisbandSettlementService()
                .SettleDisbandingParty(party, settlement);
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_disband_finalization");
            message.SetTextVariable("PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("DESTINATION_NAME", settlement?.Name ??
                new TextObject("{=ModifiedArmy_NoValidDestination}" +
                    "no valid destination"));
            message.SetTextVariable("FIEF_RETURNED", result.FiefReturned);
            message.SetTextVariable("FIEF_LOST", result.FiefForcedLost);
            message.SetTextVariable("ORDINARY_LOST",
                result.OrdinaryForcedLost);
            ModLogger.Notice(message.ToString());
        }

        private static void HandlePrisoners(
            MobileParty party, Settlement settlement)
        {
            if (party.PrisonRoster.TotalHeroes > 0)
            {
                TroopRoster donated = null;
                var prisoners = new List<TroopRosterElement>(
                    party.PrisonRoster.GetTroopRoster());
                foreach (TroopRosterElement element in prisoners)
                {
                    Hero prisoner = element.Character?.HeroObject;
                    if (prisoner == null) continue;
                    if (prisoner.MapFaction.IsAtWarWith(settlement.MapFaction))
                    {
                        donated ??= TroopRoster.CreateDummyTroopRoster();
                        TransferPrisonerAction.Apply(element.Character,
                            party.Party, settlement.Party);
                        donated.Add(element);
                    }
                    else EndCaptivityAction.ApplyByEscape(prisoner, null, true);
                }
                if (donated != null)
                    CampaignEventDispatcher.Instance
                        .OnPrisonerDonatedToSettlement(party,
                            donated.ToFlattenedRoster(), settlement);
            }
            if (party.PrisonRoster.TotalManCount > 0)
                SellPrisonersAction.ApplyForAllPrisoners(
                    party.Party, settlement.Party);
        }

        private static void TransferHeroes(
            TroopRoster source, MobileParty garrison)
        {
            foreach (TroopRosterElement element in source.GetTroopRoster())
                if (element.Character?.IsHero == true)
                    garrison.MemberRoster.Add(element);
        }

        private static void ApplyInfluence(MobileParty party,
            Settlement settlement,
            IReadOnlyDictionary<CharacterObject, int> accepted)
        {
            float influence = 0f;
            foreach (var pair in accepted)
                influence += pair.Value * Campaign.Current.Models
                    .PrisonerDonationModel
                    .CalculateInfluenceGainAfterTroopDonation(
                        party.Party, pair.Key, settlement);
            if (influence > 0f)
                GainKingdomInfluenceAction.ApplyForDonatePrisoners(
                    party, influence);
        }

        private static void LogResult(MobileParty party,
            Settlement settlement, DisbandSettlementResult result)
        {
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_disband_garrison_merge");
            message.SetTextVariable("PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
            message.SetTextVariable("FIEF_RETURNED", result.FiefReturned);
            message.SetTextVariable("FIEF_TRANSFERRED",
                result.FiefTransferredWithService);
            message.SetTextVariable("ORDINARY_ACCEPTED",
                result.OrdinaryAccepted);
            message.SetTextVariable("FIEF_LOST", result.FiefForcedLost);
            message.SetTextVariable("ORDINARY_LOST",
                result.OrdinaryForcedLost);
            ModLogger.Notice(message.ToString());
        }

        private static void LogMergeOverflow(
            Settlement settlement, MobileParty garrison)
        {
            int reserved = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(settlement);
            int effective = GarrisonRecruitmentCapacity
                .GetEffectiveMemberCount(garrison, reserved);
            int limit = garrison.Party.PartySizeLimit;
            if (effective <= limit) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_disband_merge_overflow");
            message.SetTextVariable("SETTLEMENT_ID", settlement.StringId);
            message.SetTextVariable("REGULARS",
                garrison.Party.NumberOfRegularMembers);
            message.SetTextVariable("HEROES",
                garrison.MemberRoster.TotalHeroes);
            message.SetTextVariable("RESERVED", reserved);
            message.SetTextVariable("LIMIT", limit);
            message.SetTextVariable("EXCESS", effective - limit);
            ModLogger.Debug(message.ToString());
        }
    }
}
