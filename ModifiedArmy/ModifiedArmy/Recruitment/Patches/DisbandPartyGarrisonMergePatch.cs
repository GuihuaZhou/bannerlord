using HarmonyLib;
using ModifiedArmy.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
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
    /// into a fortification. Regular troops must pass the same composition,
    /// establishment, wage and clan-budget rules as every other AI recruit.
    /// </summary>
    [HarmonyPatch(
        typeof(DisbandPartyCampaignBehavior),
        "MergeDisbandPartyToFortification")]
    public static class DisbandPartyGarrisonMergePatch
    {
        private static bool Prefix(
            MobileParty disbandParty,
            Settlement relatedSettlement)
        {
            HandlePrisoners(disbandParty, relatedSettlement);

            if (disbandParty.MemberRoster.TotalManCount <= 0)
            {
                return false;
            }

            int originalCount = disbandParty.MemberRoster.TotalRegulars;
            int acceptedCount = 0;

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
                    acceptedCount = TransferApprovedRegulars(
                        disbandParty,
                        relatedSettlement,
                        garrison);
                    GarrisonRecruitFromPrisonersBehavior
                        .NormalizeGarrisonNow(relatedSettlement.Town);
                }
            }

            // Native disbanding removes every remaining member after the
            // settlement merge. Troops rejected by the plan therefore leave
            // service instead of overflowing the garrison.
            disbandParty.MemberRoster.Clear();
            LogResult(
                disbandParty,
                relatedSettlement,
                originalCount,
                acceptedCount);
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

        private static int TransferApprovedRegulars(
            MobileParty disbandParty,
            Settlement settlement,
            MobileParty garrison)
        {
            List<RecruitmentCandidate> candidates =
                new List<RecruitmentCandidate>();

            foreach (TroopRosterElement element in
                disbandParty.MemberRoster.GetTroopRoster())
            {
                if (element.Character == null ||
                    element.Character.IsHero ||
                    element.Number <= 0)
                {
                    continue;
                }

                candidates.Add(new RecruitmentCandidate(
                    element.Character,
                    element.Number,
                    RecruitmentSource.Transfer));
            }

            if (candidates.Count == 0)
            {
                return 0;
            }

            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                garrison,
                candidates);
            TroopRoster acceptedRoster =
                TroopRoster.CreateDummyTroopRoster();
            int acceptedCount = 0;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                CharacterObject troop = evaluation.Troop;
                int availableCount = troop == null
                    ? 0
                    : disbandParty.MemberRoster.GetTroopCount(troop);
                int count = Math.Min(
                    evaluation.RecruitableCount,
                    availableCount);

                if (count <= 0)
                {
                    continue;
                }

                int woundedCount = GetWoundedCount(
                    disbandParty.MemberRoster,
                    troop,
                    count);
                garrison.MemberRoster.AddToCounts(
                    troop,
                    count,
                    false,
                    woundedCount,
                    0,
                    true,
                    -1);
                acceptedRoster.AddToCounts(
                    troop,
                    count,
                    false,
                    woundedCount,
                    0,
                    true,
                    -1);
                ClanRecruitmentBudgetManager.CommitRecruitment(
                    garrison,
                    count,
                    0f,
                    evaluation.UnitDailyWage);
                acceptedCount += count;
            }

            ApplyInfluence(
                disbandParty,
                settlement,
                acceptedRoster);
            return acceptedCount;
        }

        private static int GetWoundedCount(
            TroopRoster roster,
            CharacterObject troop,
            int acceptedCount)
        {
            foreach (TroopRosterElement element in roster.GetTroopRoster())
            {
                if (element.Character == troop)
                {
                    return Math.Min(
                        acceptedCount,
                        element.WoundedNumber);
                }
            }

            return 0;
        }

        private static void ApplyInfluence(
            MobileParty disbandParty,
            Settlement settlement,
            TroopRoster acceptedRoster)
        {
            float influenceValue = 0f;

            foreach (TroopRosterElement element in
                acceptedRoster.GetTroopRoster())
            {
                influenceValue += element.Number * Campaign.Current.Models
                    .PrisonerDonationModel
                    .CalculateInfluenceGainAfterTroopDonation(
                        disbandParty.Party,
                        element.Character,
                        settlement);
            }

            if (influenceValue > 0f)
            {
                GainKingdomInfluenceAction.ApplyForDonatePrisoners(
                    disbandParty,
                    influenceValue);
            }
        }

        private static void LogResult(
            MobileParty disbandParty,
            Settlement settlement,
            int originalCount,
            int acceptedCount)
        {
            if (!RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
            {
                return;
            }

            int dismissedCount = Math.Max(
                0,
                originalCount - acceptedCount);
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_disband_garrison_merge");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(disbandParty));
            message.SetTextVariable(
                "SETTLEMENT_NAME",
                settlement.Name);
            message.SetTextVariable("ACCEPTED", acceptedCount);
            message.SetTextVariable("DISMISSED", dismissedCount);

            ModLogger.Notice(message.ToString());
        }
    }
}
