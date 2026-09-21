using HarmonyLib;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Routes AI garrison volunteer recruitment through the unified model.
    /// Player-owned settlements keep the native behavior so this AI policy
    /// does not override a player's auto-recruitment choice.
    /// </summary>
    [HarmonyPatch(
        typeof(GarrisonRecruitmentCampaignBehavior),
        "TickAutoRecruitmentGarrisonChange")]
    public static class GarrisonRecruitmentPatch
    {
        private static bool Prefix(Town town)
        {
            if (town?.Settlement == null ||
                town.Settlement.OwnerClan == null ||
                town.Settlement.OwnerClan == Clan.PlayerClan)
            {
                return true;
            }

            List<RecruitmentCandidate> candidates =
                CollectVolunteerCandidates(town);

            if (candidates.Count == 0)
            {
                return false;
            }

            // Native code creates the garrison only when recruitment can
            // actually proceed. Candidate discovery remains non-mutating.
            if (town.GarrisonParty == null)
            {
                town.Owner.Settlement.AddGarrisonParty();
            }

            MobileParty garrison = town.GarrisonParty;

            if (garrison == null)
            {
                return false;
            }

            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                garrison,
                candidates);
            int dailyLimit = Math.Max(
                0,
                Campaign.Current.Models.SettlementGarrisonModel
                    .GetMaximumDailyAutoRecruitmentCount(town));
            int approvedCount = ExecutePlan(
                town,
                garrison,
                plan,
                dailyLimit);

            LogPlan(
                town,
                plan,
                candidates.Count,
                approvedCount,
                dailyLimit);
            return false;
        }

        /// <summary>
        /// Collects every volunteer slot that the native garrison rules make
        /// available in the town and its normal bound villages.
        /// </summary>
        private static List<RecruitmentCandidate>
            CollectVolunteerCandidates(Town town)
        {
            List<RecruitmentCandidate> result =
                new List<RecruitmentCandidate>();

            AddSettlementVolunteers(
                town.Settlement,
                town.Settlement,
                result);

            foreach (Village village in town.Settlement.BoundVillages)
            {
                if (village?.Settlement == null ||
                    village.VillageState != Village.VillageStates.Normal)
                {
                    continue;
                }

                AddSettlementVolunteers(
                    town.Settlement,
                    village.Settlement,
                    result);
            }

            return result;
        }

        private static void AddSettlementVolunteers(
            Settlement recruitingSettlement,
            Settlement notableSettlement,
            ICollection<RecruitmentCandidate> candidates)
        {
            foreach (Hero notable in notableSettlement.Notables)
            {
                if (notable == null || !notable.IsAlive)
                {
                    continue;
                }

                int accessibleSlotCount = Campaign.Current.Models
                    .VolunteerModel
                    .MaximumIndexGarrisonCanRecruitFromHero(
                        recruitingSettlement,
                        notable);
                int slotCount = Math.Min(
                    notable.VolunteerTypes.Length,
                    Math.Max(0, accessibleSlotCount));

                for (int slotIndex = 0;
                    slotIndex < slotCount;
                    slotIndex++)
                {
                    CharacterObject troop =
                        notable.VolunteerTypes[slotIndex];

                    if (troop == null)
                    {
                        continue;
                    }

                    candidates.Add(
                        new RecruitmentCandidate(
                            troop,
                            1,
                            RecruitmentSource.Volunteer,
                            new GarrisonVolunteerOffer(
                                notable,
                                slotIndex)));
                }
            }
        }

        /// <summary>
        /// Executes approved candidates in model priority order while retaining
        /// the native maximum number of automatic recruits per settlement day.
        /// </summary>
        private static int ExecutePlan(
            Town town,
            MobileParty garrison,
            RecruitmentPlan plan,
            int dailyLimit)
        {
            int recruited = 0;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (recruited >= dailyLimit)
                {
                    break;
                }

                if (evaluation.RecruitableCount <= 0 ||
                    !(evaluation.Candidate?.SourceContext
                        is GarrisonVolunteerOffer offer))
                {
                    continue;
                }

                CharacterObject troop = offer.Notable
                    .VolunteerTypes[offer.SlotIndex];

                // Another behavior may have consumed the slot between plan
                // creation and execution. Never recruit a stale replacement.
                if (troop == null || troop != evaluation.Troop)
                {
                    continue;
                }

                int recruitmentCost = Campaign.Current.Models.PartyWageModel
                    .GetTroopRecruitmentCost(
                        troop,
                        town.Settlement.OwnerClan.Leader,
                        false)
                    .RoundedResultNumber;
                garrison.MemberRoster.AddToCounts(
                    troop,
                    1,
                    false,
                    0,
                    0,
                    true,
                    -1);
                town.Settlement.OwnerClan.AutoRecruitmentExpenses +=
                    recruitmentCost;
                offer.Notable.VolunteerTypes[offer.SlotIndex] = null;
                recruited++;
            }

            return recruited;
        }

        private static void LogPlan(
            Town town,
            RecruitmentPlan plan,
            int offeredCount,
            int approvedCount,
            int dailyLimit)
        {
            RecruitmentLimitReason mainLimit = RecruitmentLimitReason.None;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.RecruitableCount <= 0)
                {
                    mainLimit = evaluation.PrimaryLimit;
                    break;
                }
            }

            TextObject message = new TextObject(
                "{=ModifiedArmy_AIRecruitmentGarrisonPlan}" +
                "[AIRecruitment] Garrison='{PARTY_NAME}' | " +
                "Settlement='{SETTLEMENT_NAME}' | Culture={CULTURE_ID} | " +
                "VolunteerOffers={OFFERED} | Approved={APPROVED} | " +
                "DailyLimit={DAILY_LIMIT} | FirstLimit={LIMIT}");
            message.SetTextVariable("PARTY_NAME", town.GarrisonParty.Name);
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("CULTURE_ID", plan.CultureId);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", approvedCount);
            message.SetTextVariable("DAILY_LIMIT", dailyLimit);
            message.SetTextVariable(
                "LIMIT",
                new TextObject(
                    "{=ModifiedArmy_RecruitLimit_" + mainLimit + "}" +
                    mainLimit));
            ModLogger.Notice(message.ToString());
        }

        private sealed class GarrisonVolunteerOffer
        {
            public GarrisonVolunteerOffer(Hero notable, int slotIndex)
            {
                Notable = notable;
                SlotIndex = slotIndex;
            }

            public Hero Notable { get; }

            public int SlotIndex { get; }
        }
    }
}
