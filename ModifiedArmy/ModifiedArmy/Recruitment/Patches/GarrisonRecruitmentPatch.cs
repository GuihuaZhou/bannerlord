using HarmonyLib;
using ModifiedArmy.Recruitment.Diagnostics;
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
            if (!RecruitmentLogFilter.ShouldLog(town.GarrisonParty))
            {
                return;
            }

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

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_ai_recruitment_garrison_plan");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(town.GarrisonParty));
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("CULTURE_ID", plan.CultureId);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", approvedCount);
            message.SetTextVariable("DAILY_LIMIT", dailyLimit);
            message.SetTextVariable("LIMIT", GetLimitText(mainLimit));
            if (approvedCount > 0)
            {
                // Garrison recruitment runs once per eligible settlement per
                // day, so even successful records remain below Notice level.
                ModLogger.Info(message.ToString());
            }
            else
            {
                ModLogger.Debug(message.ToString());
            }
        }

        private static TextObject GetLimitText(
            RecruitmentLimitReason reason)
        {
            switch (reason)
            {
                case RecruitmentLimitReason.InvalidParty:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_invalid_party");
                case RecruitmentLimitReason.InvalidTroop:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_invalid_troop");
                case RecruitmentLimitReason.PartySize:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_party_size");
                case RecruitmentLimitReason.CombatRole:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_combat_role");
                case RecruitmentLimitReason.Quality:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_quality");
                case RecruitmentLimitReason.WageLimit:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_wage");
                case RecruitmentLimitReason.RecruitmentCost:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_cost");
                case RecruitmentLimitReason.MaintenanceFunds:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_maintenance");
                default:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_none");
            }
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
