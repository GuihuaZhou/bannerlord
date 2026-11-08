using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using ModifiedPolitics.HeroOffices.Diagnostics;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.Models
{
    public sealed class DiagnosticArmyManagementCalculationModel : DefaultArmyManagementCalculationModel
    {
        public override float AIMobilePartySizeRatioToCallToArmy => BaseModel.AIMobilePartySizeRatioToCallToArmy;
        public override float PlayerMobilePartySizeRatioToCallToArmy => BaseModel.PlayerMobilePartySizeRatioToCallToArmy;
        public override float MinimumNeededFoodInDaysToCallToArmy => BaseModel.MinimumNeededFoodInDaysToCallToArmy;
        public override float MaximumDistanceToCallToArmy => BaseModel.MaximumDistanceToCallToArmy;
        public override int InfluenceValuePerGold => BaseModel.InfluenceValuePerGold;
        public override int AverageCallToArmyCost => BaseModel.AverageCallToArmyCost;
        public override int CohesionThresholdForDispersion => BaseModel.CohesionThresholdForDispersion;
        public override float MaximumWaitTime => BaseModel.MaximumWaitTime;

        public override float DailyBeingAtArmyInfluenceAward(MobileParty party) =>
            BaseModel.DailyBeingAtArmyInfluenceAward(party);
        public override int CalculatePartyInfluenceCost(MobileParty leader, MobileParty party)
        {
            int result = BaseModel.CalculatePartyInfluenceCost(leader, party);
            return result > 0 && IsArmyAuthority(leader?.LeaderHero,
                    leader?.MapFaction as Kingdom)
                ? MathF.Ceiling(result * 0.75f)
                : result;
        }
        public override int CalculateTotalInfluenceCost(Army army, float percentage) =>
            BaseModel.CalculateTotalInfluenceCost(army, percentage);
        public override float GetPartySizeScore(MobileParty party) => BaseModel.GetPartySizeScore(party);
        public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false) =>
            BaseModel.CalculateDailyCohesionChange(army, includeDescriptions);
        public override int CalculateNewCohesion(Army army, PartyBase party, int cohesion, int sign) =>
            BaseModel.CalculateNewCohesion(army, party, cohesion, sign);
        public override int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100)
        {
            int result = BaseModel.GetCohesionBoostInfluenceCost(army, percentageToBoost);
            return result > 0 && IsArmyAuthority(army?.LeaderParty?.LeaderHero,
                    army?.LeaderParty?.MapFaction as Kingdom)
                ? MathF.Ceiling(result * 0.80f)
                : result;
        }
        public override int GetPartyRelation(Hero hero) => BaseModel.GetPartyRelation(hero);
        public override bool CanPlayerCreateArmy(out TextObject reason)
        {
            bool result = BaseModel.CanPlayerCreateArmy(out reason);
            Kingdom kingdom = Clan.PlayerClan?.Kingdom;
            if (result && kingdom != null && Hero.MainHero != kingdom.Leader &&
                HasAnyMarshal(kingdom) && !IsArmyAuthority(Hero.MainHero, kingdom))
            {
                reason = new TextObject(
                    "{=MP_MarshalRequired}Only the ruler or the kingdom marshal may create an army.");
                return false;
            }
            return result;
        }
        public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation) =>
            BaseModel.CheckPartyEligibility(party, out explanation);

        public override bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)
        {
            possibleArmyMembers = new MBList<MobileParty>();
            MarshalArmyEligibilityTrace.ModelActive(mobileParty);
            Kingdom kingdom = mobileParty.MapFaction as Kingdom;
            MarshalArmyEligibilityTrace trace = MarshalArmyEligibilityTrace.Begin(mobileParty);
            trace.Entry(GetType().FullName);

            if (mobileParty.IsCurrentlyAtSea)
            {
                trace.Result(false, "AT_SEA", 0, 0);
                return false;
            }
            float influence = mobileParty.LeaderHero.Clan.Influence;
            if (!(influence > 100f))
            {
                trace.Gate("LEADER", "INFLUENCE_LE_100", "influence=" + MarshalArmyEligibilityTrace.F(influence));
                trace.Result(false, "INFLUENCE_LE_100", 0, 0);
                return false;
            }
            if (mobileParty.LeaderHero.Clan.IsUnderMercenaryService)
            {
                trace.Result(false, "MERCENARY_CLAN", 0, 0);
                return false;
            }
            int leaderFoodDays = mobileParty.GetNumDaysForFoodToLast();
            float siegeFoodThreshold = Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege;
            if (!((float)leaderFoodDays > siegeFoodThreshold))
            {
                trace.Gate("LEADER", "FOOD_LE_SIEGE_THRESHOLD", "foodDays=" + leaderFoodDays +
                    " threshold=" + MarshalArmyEligibilityTrace.F(siegeFoodThreshold));
                trace.Result(false, "FOOD_LE_SIEGE_THRESHOLD", 0, 0);
                return false;
            }
            if (!kingdom.FactionsAtWarWith.AnyQ((IFaction x) => x.Fiefs.Any<Town>()))
            {
                trace.Result(false, "NO_WAR_ENEMY_TOWN", 0, 0);
                return false;
            }
            float leaderRatioThreshold = Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy;
            if (!(mobileParty.PartySizeRatio > leaderRatioThreshold))
            {
                trace.Gate("LEADER", "LEADER_RATIO_LE_AI_THRESHOLD", "partyRatio=" +
                    MarshalArmyEligibilityTrace.F(mobileParty.PartySizeRatio) + " threshold=" +
                    MarshalArmyEligibilityTrace.F(leaderRatioThreshold));
                trace.Result(false, "LEADER_RATIO_LE_AI_THRESHOLD", 0, 0);
                return false;
            }
            bool appointedMarshal = HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(item => item?.Hero == mobileParty.LeaderHero) == true;
            if (!(appointedMarshal || mobileParty.LeaderHero.Clan.Leader == mobileParty.LeaderHero ||
                  (mobileParty.LeaderHero.Clan.Leader.PartyBelongedTo == null &&
                   mobileParty.LeaderHero.Clan.WarPartyComponents != null &&
                   mobileParty.LeaderHero.Clan.WarPartyComponents.FirstOrDefault<WarPartyComponent>() == mobileParty.WarPartyComponent)))
            {
                trace.Result(false, "NOT_ELIGIBLE_CLAN_LEADER", 0, 0);
                return false;
            }

            float unusedInfluenceBudget = mobileParty.LeaderHero.Clan.Influence * 0.7f;
            trace.Gate("LEADER_GATE", "PASS", "unusedInfluenceBudget=" +
                MarshalArmyEligibilityTrace.F(unusedInfluenceBudget) +
                " appointedMarshal=" + appointedMarshal + " identityAdaptation=MODEL_DIRECT");
            List<ValueTuple<MobileParty, float, int>> list = new List<ValueTuple<MobileParty, float, int>>();
            foreach (WarPartyComponent component in mobileParty.MapFaction.WarPartyComponents)
            {
                MobileParty candidate = component.MobileParty;
                Hero leader = candidate.LeaderHero;
                if (!candidate.IsLordParty) { trace.Candidate(candidate, false, "NOT_LORD_PARTY", ""); continue; }
                if (candidate.Army != null) { trace.Candidate(candidate, false, "ALREADY_IN_ARMY", ""); continue; }
                if (candidate == mobileParty) { trace.Candidate(candidate, false, "SELF_PARTY", ""); continue; }
                if (leader == null) { trace.Candidate(candidate, false, "NO_LEADER", ""); continue; }
                if (candidate.IsMainParty) { trace.Candidate(candidate, false, "PLAYER_MAIN_PARTY", ""); continue; }
                if (leader == leader.MapFaction.Leader) { trace.Candidate(candidate, false, "RULER_PARTY", ""); continue; }
                if (candidate.Ai.DoNotMakeNewDecisions) { trace.Candidate(candidate, false, "DO_NOT_DECIDE", ""); continue; }
                Settlement current = candidate.CurrentSettlement;
                if (current?.SiegeEvent != null) { trace.Candidate(candidate, false, "SETTLEMENT_UNDER_SIEGE", ""); continue; }
                if (candidate.IsDisbanding) { trace.Candidate(candidate, false, "IS_DISBANDING", ""); continue; }
                int foodDays = candidate.GetNumDaysForFoodToLast();
                float foodThreshold = Campaign.Current.Models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy;
                if (!((float)foodDays > foodThreshold)) { trace.Candidate(candidate, false, "FOOD_LE_THRESHOLD", "foodDays=" + foodDays + " threshold=" + MarshalArmyEligibilityTrace.F(foodThreshold)); continue; }
                float ratioThreshold = Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy;
                if (!(candidate.PartySizeRatio > ratioThreshold)) { trace.Candidate(candidate, false, "PARTY_RATIO_LE_THRESHOLD", "partyRatio=" + MarshalArmyEligibilityTrace.F(candidate.PartySizeRatio) + " threshold=" + MarshalArmyEligibilityTrace.F(ratioThreshold)); continue; }
                if (!leader.CanLeadParty()) { trace.Candidate(candidate, false, "HERO_CANNOT_LEAD", ""); continue; }
                if (candidate.IsInRaftState) { trace.Candidate(candidate, false, "RAFT_STATE", ""); continue; }
                if (candidate.MapEvent != null) { trace.Candidate(candidate, false, "MAP_EVENT_ACTIVE", ""); continue; }
                if (candidate.BesiegedSettlement != null) { trace.Candidate(candidate, false, "BESIEGED_SETTLEMENT", ""); continue; }
                IDisbandPartyCampaignBehavior disband = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
                if (disband != null && disband.IsPartyWaitingForDisband(candidate)) { trace.Candidate(candidate, false, "WAITING_FOR_DISBAND", ""); continue; }
                float maximumDistance = Campaign.Current.Models.ArmyManagementCalculationModel.MaximumDistanceToCallToArmy;
                float distance;
                if (!(DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(candidate, mobileParty, candidate.NavigationCapability, out distance) < maximumDistance))
                { trace.Candidate(candidate, false, "DISTANCE_TOO_FAR", "distance=" + MarshalArmyEligibilityTrace.F(distance) + " maximum=" + MarshalArmyEligibilityTrace.F(maximumDistance)); continue; }
                bool duplicate = false;
                using (List<ValueTuple<MobileParty, float, int>>.Enumerator enumerator = list.GetEnumerator())
                {
                    while (enumerator.MoveNext()) if (enumerator.Current.Item1 == candidate) { duplicate = true; break; }
                }
                if (duplicate) { trace.Candidate(candidate, false, "DUPLICATE_PARTY", ""); continue; }
                int cost = Campaign.Current.Models.ArmyManagementCalculationModel.CalculatePartyInfluenceCost(mobileParty, candidate);
                float strength = candidate.Party.EstimatedStrength;
                float healthyRatio = 1f - (float)candidate.Party.MemberRoster.TotalWounded /
                    (float)candidate.Party.MemberRoster.TotalManCount;
                float score = strength / ((float)cost + 0.1f) * healthyRatio;
                list.Add(new ValueTuple<MobileParty, float, int>(candidate, score, cost));
                trace.Candidate(candidate, true, "ACCEPTED", "foodDays=" + foodDays +
                    " distance=" + MarshalArmyEligibilityTrace.F(distance) + " influenceCost=" + cost +
                    " estimatedStrength=" + MarshalArmyEligibilityTrace.F(strength) +
                    " score=" + MarshalArmyEligibilityTrace.F(score));
            }

            list = list.OrderByQ((ValueTuple<MobileParty, float, int> x) => x.Item2)
                .ToListQ<ValueTuple<MobileParty, float, int>>();
            int eligibleBeforeSort = list.Count;
            trace.Gate("CANDIDATES_SORTED", "SORTED_ASCENDING",
                "eligibleBeforeSort=" + eligibleBeforeSort + " sortedCandidates=\"" +
                string.Join("|", list.Take(25).Select(item =>
                    (item.Item1?.StringId ?? "none") + ":" +
                    MarshalArmyEligibilityTrace.F(item.Item2))) + "\" omitted=" +
                Math.Max(0, list.Count - 25));
            int partyCount = kingdom.WarPartyComponents.Count;
            int armiesPartyCount = kingdom.Armies.SumQ((Army x) => x.Parties.Count);
            int maxNewMembers = MathF.Ceiling((float)partyCount * 0.7f - (float)armiesPartyCount);
            trace.Gate("CAPACITY_CHECK", maxNewMembers > 0 ? "CAPACITY_AVAILABLE" : "KINGDOM_ARMY_CAPACITY_EXHAUSTED",
                "eligibleBeforeSort=" + eligibleBeforeSort + " kingdomWarPartyCount=" + partyCount +
                " existingArmyPartyCount=" + armiesPartyCount + " maxNewMembers=" + maxNewMembers);
            if (maxNewMembers > 0)
            {
                string removedCandidates = "";
                if (maxNewMembers < list.Count)
                {
                    removedCandidates = string.Join("|", list.Skip(maxNewMembers).Take(25)
                        .Select(item => item.Item1?.StringId ?? "none"));
                    list.RemoveRange(maxNewMembers, list.Count - maxNewMembers);
                }
                trace.Gate("CAPACITY_RESULT", "TRUNCATION_COMPLETE",
                    "afterTruncationCount=" + list.Count + " removedCandidateIds=\"" +
                    removedCandidates + "\"");
                possibleArmyMembers = list.SelectQ((ValueTuple<MobileParty, float, int> x) => x.Item1).ToMBList<MobileParty>();
                int beforeStrength = possibleArmyMembers.Count;
                if (possibleArmyMembers.AnyQ<MobileParty>())
                {
                    if (kingdom.Settlements.Count == 0)
                    {
                        trace.Result(true, "PASSED_NO_SETTLEMENT_STRENGTH_GATE", beforeStrength, beforeStrength);
                        return true;
                    }
                    float leaderStrength = mobileParty.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                    float candidateStrength = 0f;
                    foreach (MobileParty candidate in possibleArmyMembers)
                        candidateStrength += candidate.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
                    float totalStrength = leaderStrength + candidateStrength;
                    trace.Gate("STRENGTH_CHECK", totalStrength < 1000f ? "TOTAL_SIEGE_STRENGTH_LT_1000" : "PASS",
                        "candidateCountBeforeStrengthCheck=" + beforeStrength + " leaderSiegeStrength=" +
                        MarshalArmyEligibilityTrace.F(leaderStrength) + " candidatesSiegeStrength=" +
                        MarshalArmyEligibilityTrace.F(candidateStrength) + " totalSiegeStrength=" +
                        MarshalArmyEligibilityTrace.F(totalStrength) + " strengthThreshold=1000");
                    if (totalStrength < 1000f)
                    {
                        possibleArmyMembers.Clear();
                        trace.Result(false, "TOTAL_SIEGE_STRENGTH_LT_1000", beforeStrength, 0);
                        return false;
                    }
                    trace.Result(true, "PASS", beforeStrength, possibleArmyMembers.Count);
                    return true;
                }
                trace.Result(false, "NO_CANDIDATES_AFTER_TRUNCATION", beforeStrength, 0);
                return false;
            }
            trace.Result(false, "KINGDOM_ARMY_CAPACITY_EXHAUSTED", list.Count, possibleArmyMembers.Count);
            return false;
        }

        private static bool HasAnyMarshal(Kingdom kingdom) =>
            kingdom != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(item => item?.Hero != null) == true;

        private static bool IsArmyAuthority(Hero hero, Kingdom kingdom) =>
            hero != null && kingdom != null &&
            (hero == kingdom.Leader || HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(item => item?.Hero == hero) == true);
    }
}
