using HarmonyLib;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Models;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Replaces the native all-or-nothing merge of a disbanding lord party
    /// into a fortification. Eligible fief troops return first, remaining
    /// service identities move only to a same-clan garrison, and ordinary
    /// soldiers use the normal composition and capacity approval path.
    /// </summary>
    [HarmonyPatch(
        typeof(DisbandPartyCampaignBehavior),
        "MergeDisbandPartyToFortification")]
    public static class DisbandPartyGarrisonMergePatch
    {
        /// <summary>
        /// Preserves native prisoner and hero handling while delegating all
        /// regular-soldier identity settlement to the D3 service.
        /// </summary>
        private static bool Prefix(
            MobileParty disbandParty,
            Settlement relatedSettlement)
        {
            if (disbandParty?.IsLordParty != true || disbandParty.IsMainParty ||
                disbandParty.ActualClan == null)
            {
                return true;
            }

            HandlePrisoners(disbandParty, relatedSettlement);

            if (disbandParty.MemberRoster.TotalManCount <= 0)
            {
                return false;
            }

            DisbandSettlementResult settlementResult =
                new DisbandSettlementService().SettleDisbandingParty(
                    disbandParty,
                    relatedSettlement);

            if (disbandParty.MapFaction == relatedSettlement.MapFaction)
            {
                if (relatedSettlement.Town.GarrisonParty == null)
                {
                    relatedSettlement.AddGarrisonParty();
                }

                MobileParty garrison = relatedSettlement.Town.GarrisonParty;

                if (garrison != null)
                {
                    TransferHeroes(disbandParty.MemberRoster, garrison);
                    ApplyInfluence(
                        disbandParty,
                        relatedSettlement,
                        settlementResult.AcceptedTroops);
                    GarrisonRecruitFromPrisonersBehavior
                        .NormalizeGarrisonNow(relatedSettlement.Town);
                    LogMergeOverflow(relatedSettlement, garrison);
                }
            }

            // Native disbanding removes every remaining member after the
            // settlement merge. Troops rejected by the plan therefore leave
            // service instead of overflowing the garrison.
            disbandParty.MemberRoster.Clear();
            LogResult(
                disbandParty,
                relatedSettlement,
                settlementResult ?? new DisbandSettlementResult
                {
                    PartyId = disbandParty.StringId,
                    ReasonCode = "NoLegalGarrison"
                });
            return false;
        }

        /// <summary>
        /// Retains native prisoner consequences. Enemy heroes are transferred
        /// to the settlement, friendly heroes escape, and ordinary prisoners
        /// are sold through the native transaction.
        /// </summary>
        private static void HandlePrisoners(
            MobileParty disbandParty,
            Settlement settlement)
        {
            if (disbandParty.PrisonRoster.TotalHeroes > 0)
            {
                TroopRoster donatedHeroes = null;
                List<TroopRosterElement> prisoners =
                    new List<TroopRosterElement>(
                        disbandParty.PrisonRoster.GetTroopRoster());

                foreach (TroopRosterElement element in prisoners)
                {
                    Hero prisoner = element.Character?.HeroObject;

                    if (prisoner == null)
                    {
                        continue;
                    }

                    if (prisoner.MapFaction.IsAtWarWith(
                        settlement.MapFaction))
                    {
                        donatedHeroes ??=
                            TroopRoster.CreateDummyTroopRoster();
                        TransferPrisonerAction.Apply(
                            element.Character,
                            disbandParty.Party,
                            settlement.Party);
                        donatedHeroes.Add(element);
                    }
                    else
                    {
                        EndCaptivityAction.ApplyByEscape(
                            prisoner,
                            null,
                            true);
                    }
                }

                if (donatedHeroes != null)
                {
                    CampaignEventDispatcher.Instance
                        .OnPrisonerDonatedToSettlement(
                            disbandParty,
                            donatedHeroes.ToFlattenedRoster(),
                            settlement);
                }
            }

            if (disbandParty.PrisonRoster.TotalManCount > 0)
            {
                SellPrisonersAction.ApplyForAllPrisoners(
                    disbandParty.Party,
                    settlement.Party);
            }
        }

        /// <summary>
        /// Preserves the native handling of hero members. The recruitment
        /// model evaluates regular soldiers only.
        /// </summary>
        private static void TransferHeroes(
            TroopRoster source,
            MobileParty garrison)
        {
            foreach (TroopRosterElement element in source.GetTroopRoster())
            {
                if (element.Character?.IsHero == true)
                {
                    garrison.MemberRoster.Add(element);
                }
            }
        }

        /// <summary>
        /// Grants influence only for soldiers actually accepted by the
        /// destination garrison; returned or forcibly lost troops never count.
        /// </summary>
        private static void ApplyInfluence(
            MobileParty disbandParty,
            Settlement settlement,
            IReadOnlyDictionary<CharacterObject, int> acceptedTroops)
        {
            float influenceValue = 0f;

            foreach (var pair in acceptedTroops)
            {
                influenceValue += pair.Value * Campaign.Current.Models
                    .PrisonerDonationModel
                    .CalculateInfluenceGainAfterTroopDonation(
                        disbandParty.Party,
                        pair.Key,
                        settlement);
            }

            if (influenceValue > 0f)
            {
                GainKingdomInfluenceAction.ApplyForDonatePrisoners(
                    disbandParty,
                    influenceValue);
            }
        }

        /// <summary>Writes the complete fortification disband ledger.</summary>
        private static void LogResult(
            MobileParty disbandParty,
            Settlement settlement,
            DisbandSettlementResult result)
        {
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_disband_garrison_merge");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(disbandParty));
            message.SetTextVariable(
                "SETTLEMENT_NAME",
                settlement.Name);
            message.SetTextVariable("FIEF_RETURNED", result.FiefReturned);
            message.SetTextVariable(
                "FIEF_TRANSFERRED",
                result.FiefTransferredWithService);
            message.SetTextVariable(
                "ORDINARY_ACCEPTED",
                result.OrdinaryAccepted);
            message.SetTextVariable("FIEF_LOST", result.FiefForcedLost);
            message.SetTextVariable(
                "ORDINARY_LOST",
                result.OrdinaryForcedLost);

            ModLogger.Notice(message.ToString());
        }

        private static void LogMergeOverflow(
            Settlement settlement,
            MobileParty garrison)
        {
            int reserved = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(settlement);
            int effective = GarrisonRecruitmentCapacity
                .GetEffectiveMemberCount(garrison, reserved);
            int limit = garrison.Party.PartySizeLimit;
            if (effective <= limit)
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_disband_merge_overflow");
            message.SetTextVariable("SETTLEMENT_ID", settlement.StringId);
            message.SetTextVariable(
                "REGULARS",
                garrison.Party.NumberOfRegularMembers);
            message.SetTextVariable(
                "HEROES",
                garrison.MemberRoster.TotalHeroes);
            message.SetTextVariable("RESERVED", reserved);
            message.SetTextVariable("LIMIT", limit);
            message.SetTextVariable("EXCESS", effective - limit);
            ModLogger.Debug(message.ToString());
        }
    }

    /// <summary>
    /// Settles fief-service identities for final disband destinations that do
    /// not enter the fortification merge method, then lets native village or
    /// no-target handling continue unchanged.
    /// </summary>
    [HarmonyPatch(typeof(DisbandPartyCampaignBehavior), "OnPartyDisbanded")]
    public static class DisbandPartyFinalizationPatch
    {
        /// <summary>
        /// Handles only final paths that will not invoke the fortification
        /// merge prefix, preventing duplicate settlement of the same party.
        /// </summary>
        private static void Prefix(
            MobileParty disbandParty,
            Settlement relatedSettlement)
        {
            if (disbandParty?.IsLordParty != true || disbandParty.IsMainParty ||
                disbandParty.ActualClan == null ||
                relatedSettlement?.IsFortification == true &&
                !relatedSettlement.IsUnderSiege)
            {
                return;
            }
            DisbandSettlementResult result = new DisbandSettlementService()
                .SettleDisbandingParty(disbandParty, relatedSettlement);
            LogFinalization(disbandParty, relatedSettlement, result);
        }

        /// <summary>Writes the non-fortification disband ledger.</summary>
        private static void LogFinalization(
            MobileParty party,
            Settlement destination,
            DisbandSettlementResult result)
        {
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_disband_finalization");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable(
                "DESTINATION_NAME",
                destination?.Name ?? new TextObject(
                    "{=ModifiedArmy_NoValidDestination}" +
                    "no valid destination"));
            message.SetTextVariable("FIEF_RETURNED", result.FiefReturned);
            message.SetTextVariable("FIEF_LOST", result.FiefForcedLost);
            message.SetTextVariable(
                "ORDINARY_LOST",
                result.OrdinaryForcedLost);
            ModLogger.Notice(message.ToString());
        }
    }
}
