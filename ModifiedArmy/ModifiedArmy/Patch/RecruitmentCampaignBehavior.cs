using HarmonyLib;
using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Classification;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Patch
{
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "UpdateCurrentMercenaryTroopAndCount")]
    public static class RecruitmentPatches
    {
        /// <summary>
        /// 替换雇佣兵生成逻辑。
        /// 根据文化模板（MercenaryTemplate）配置刷新人数和概率。
        /// CaravanGuard 的生成由原版 RegularMercenariesSpawnChance 控制，不受影响。
        /// </summary>
        public static bool Prefix(
            RecruitmentCampaignBehavior __instance,
            Town town,
            bool forceUpdate = false)  
        {
            var mercenaryData = __instance.GetMercenaryData(town);

            if (!forceUpdate && mercenaryData.HasAvailableMercenary(Occupation.NotAssigned))
            {
                return false;
            }

            // ============================================================
            // 1. 尝试生成雇佣兵（使用模板配置）
            // ============================================================
            var template = MercenaryTemplateManager.Instance.GetTemplateByCulture(town.Culture);
            float spawnChance = template?.SpawnChance ?? 0.3f;
            int minCount = template?.MinCount ?? 5;
            int maxCount = template?.MaxCount ?? 10;

            if (MBRandom.RandomFloat < spawnChance)
            {
                List<CharacterObject> basicMercenaries = town.Culture.BasicMercenaryTroops;
                if (basicMercenaries != null && basicMercenaries.Count > 0)
                {
                    CharacterObject selectedTroop = basicMercenaries[MBRandom.RandomInt(basicMercenaries.Count)];
                    int finalCount = MBRandom.RandomInt(minCount, maxCount);
                    mercenaryData.ChangeMercenaryType(selectedTroop, finalCount);

                    // 仅在实际生成雇佣兵时打印，验证 XML 配置是否生效
                    ModLogger.Info(
                        $"[RecruitmentPatches] {town.Name} 更新了 {finalCount} 名 {selectedTroop.Name}"
                    );

                    return false;
                }
            }

            // ============================================================
            // 2. 原版逻辑：CaravanGuard 的生成由 RegularMercenariesSpawnChance 控制
            // ============================================================
            if (MBRandom.RandomFloat < Campaign.Current.Models.TavernMercenaryTroopsModel.RegularMercenariesSpawnChance)
            {
                CharacterObject caravanGuard = town.Culture.CaravanGuard;
                if (caravanGuard != null)
                {
                    return false;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 替换原版 RecruitmentCampaignBehavior.CheckRecruiting
    /// 将单一志愿兵招募升级为按 AI 决策权重降序的多兵源动态招募
    /// </summary>
    /// 

    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "CheckRecruiting")]
    public static class AiRecruitmentPatch
    {
        /// <summary>
        /// 完全接管 CheckRecruiting，按 AI 决策权重降序尝试各兵源
        /// </summary>
        private static bool Prefix(
            RecruitmentCampaignBehavior __instance, 
            MobileParty mobileParty, 
            Settlement settlement)
        {
            // ==========================================
            // 1. 商队雇佣兵逻辑（原版 if 分支保留）
            // ==========================================
            if (settlement.IsTown && mobileParty.IsCaravan)
            {
                TryRecruitCaravanMercenary(__instance, mobileParty, settlement);
                return false; // 商队分支处理完毕，跳过原版
            }

            // ==========================================
            // 2. 领主队伍前置条件检查（原版 else if 条件保留）
            // ==========================================
            bool isLordEligible =
                mobileParty.IsLordParty &&
                !mobileParty.IsDisbanding &&
                mobileParty.LeaderHero != null &&
                !mobileParty.Party.IsStarving &&
                mobileParty.Party.LeaderHero.IsAlive &&
                (float)mobileParty.PartyTradeGold > HeroHelper.StartRecruitingMoneyLimit(mobileParty.LeaderHero) &&
                (mobileParty.LeaderHero == mobileParty.LeaderHero.Clan.Leader ||
                    (float)mobileParty.LeaderHero.Clan.Gold > HeroHelper.StartRecruitingMoneyLimitForClanLeader(mobileParty.LeaderHero)) &&
                ((float)mobileParty.Party.NumberOfAllMembers + 0.5f) / (float)mobileParty.Party.PartySizeLimit <= 1f;

            if (!isLordEligible)
                return false; // 不满足领主条件，跳过原版

            // ==========================================
            // 3. 获取 AI 招募决策
            // ==========================================
            var aiBehavior = Campaign.Current.GetCampaignBehavior<AiRecruitmentBehavior>();
            if (aiBehavior == null)
                return false;

            RecruitmentDecision decision = aiBehavior.GetRecruitmentDecision(mobileParty);
            if (!decision.ShouldRecruit)
                return false;

            // ==========================================
            // 4. 按权重降序遍历候选兵源并路由执行
            // ==========================================
            var candidates = new List<(RecruitSource source, float weight)>
            {
                (RecruitSource.Fief, decision.FiefWeight),
                (RecruitSource.Volunteer, decision.VolunteerWeight),
                (RecruitSource.Mercenary, decision.MercenaryWeight)
            };
            // 按权重从高到低排序
            candidates.Sort((a, b) => b.weight.CompareTo(a.weight));
            
            // 衰减因子：每次成功招募后，后续兵权的概率乘以此系数
            const float DecayFactor = 0.4f; 
            // 当前概率乘数（初始为 1.0，成功招募后衰减）
            float currentProbabilityMultiplier = 1.0f;

            foreach (var (source, weight) in candidates)
            {
                if (weight <= 0.01f) continue;

                // 将原始权重转换为 [0, 1] 之间的浮点数
                float recruitProbability = MathF.Clamp(weight, 0f, 1f);
                // 应用衰减后的最终概率
                float finalProbability = recruitProbability * currentProbabilityMultiplier;
                ModLogger.Info($"[AI招募] {mobileParty.MapFaction?.Name} | {mobileParty.ActualClan?.Name} | {mobileParty.Name} 在 {settlement.Name} 尝试招募 {source} 概率: {finalProbability:P2}");

                // 生成随机数，小于权重（概率）则招募
                if (MBRandom.RandomFloat < finalProbability)
                {
                    bool recruited = false;
                    switch (source)
                    {
                        case RecruitSource.Fief:
                            recruited = TryRecruitFief(mobileParty, settlement);
                            break;
                        case RecruitSource.Volunteer:
                            recruited = TryRecruitVolunteers(__instance, mobileParty, settlement);
                            break;
                        case RecruitSource.Mercenary:
                            recruited = TryRecruitLordMercenary(__instance, mobileParty, settlement, finalProbability);
                            break;
                    }

                    // 一旦前面执行了招募，后面的招募概率继续降低
                    if (recruited)
                    {
                        currentProbabilityMultiplier *= DecayFactor;
                    }
                }
            }

            return false; // 拦截原版逻辑
        }

        private static void ApplyInternal(
            RecruitmentCampaignBehavior instance, 
            MobileParty side1Party, 
            Settlement settlement, 
            Hero individual, 
            CharacterObject troop, 
            int number, 
            int bitCode, 
            RecruitmentCampaignBehavior.RecruitingDetail detail)
        {
            int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, side1Party.LeaderHero, false).RoundedResultNumber;
            if (detail == RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern)
            {
                if (side1Party.IsCaravan)
                {
                    side1Party.PartyTradeGold -= number * roundedResultNumber;
                    instance.GetMercenaryData(settlement.Town).ChangeMercenaryCount(-number);
                }
                else
                {
                    GiveGoldAction.ApplyBetweenCharacters(side1Party.LeaderHero, null, number * roundedResultNumber, true);
                    instance.GetMercenaryData(settlement.Town).ChangeMercenaryCount(-number);
                }
                side1Party.AddElementToMemberRoster(troop, number, false);
            }
            else if (detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual)
            {
                GiveGoldAction.ApplyBetweenCharacters(side1Party.LeaderHero, null, roundedResultNumber, true);
                individual.VolunteerTypes[bitCode] = null;
                side1Party.AddElementToMemberRoster(troop, 1, false);
            }
            else if (detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromMap)
            {
                GiveGoldAction.ApplyBetweenCharacters(side1Party.LeaderHero, null, number * roundedResultNumber, true);
                side1Party.AddElementToMemberRoster(troop, number, false);
            }
            else if (detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividualToGarrison)
            {
                individual.VolunteerTypes[bitCode] = null;
                side1Party.AddElementToMemberRoster(troop, 1, false);
            }
            CampaignEventDispatcher.Instance.OnTroopRecruited(side1Party.LeaderHero, settlement, individual, troop, number);
        }
        
        private static void GetRecruitVolunteerFromIndividual(
            RecruitmentCampaignBehavior instance, 
            MobileParty side1Party, 
            CharacterObject subject, 
            Hero individual, 
            int bitCode)
        {
            ApplyInternal(instance, side1Party, individual.CurrentSettlement, individual, subject, 1, bitCode, RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual);
        }

        private static void ApplyRecruitMercenary(
            RecruitmentCampaignBehavior instance, 
            MobileParty side1Party, 
            Settlement side2Party, 
            CharacterObject subject, 
            int number)
		{
			ApplyInternal(instance, side1Party, side2Party, null, subject, number, -1, RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern);
		}

        // ==========================================
        // 商队雇佣兵招募（原版 if 分支逻辑提取）
        // ==========================================
        private static void TryRecruitCaravanMercenary(
            RecruitmentCampaignBehavior instance, MobileParty party, Settlement settlement)
        {
            var mercenaryData = instance.GetMercenaryData(settlement.Town);
            if (!mercenaryData.HasAvailableMercenary(Occupation.CaravanGuard) &&
                !mercenaryData.HasAvailableMercenary(Occupation.Mercenary))
                return;

            int partySizeLimit = party.Party.PartySizeLimit;
            if (party.Party.NumberOfAllMembers >= partySizeLimit)
                return;

            CharacterObject troopType = mercenaryData.TroopType;
            int cost = Campaign.Current.Models.PartyWageModel
                .GetTroopRecruitmentCost(troopType, party.LeaderHero, false).RoundedResultNumber;
            int extraCost = party.IsCaravan ? 2000 : 0;

            if (party.PartyTradeGold <= cost + extraCost)
                return;

            bool flag = true;
            double probability = 0.0;
            for (int i = 0; i < mercenaryData.Number; i++)
            {
                if (flag)
                {
                    int remainingGold = party.PartyTradeGold - (cost + extraCost);
                    double goldFactor = Math.Sqrt(MathF.Min(1f, (float)remainingGold / (100f * cost)));
                    float fillRatio = (float)party.Party.NumberOfAllMembers / partySizeLimit;
                    float sizeFactor = (MathF.Min(10f, 1f / fillRatio) * MathF.Min(10f, 1f / fillRatio) - 1f)
                                     * ((party.IsCaravan && party.Party.Owner == Hero.MainHero) ? 0.4f : 0.1f);
                    probability = goldFactor * sizeFactor;
                }

                if (MBRandom.RandomFloat < probability)
                {
                    ApplyRecruitMercenary(instance, party, settlement, troopType, 1);
                    flag = true;
                }
                else
                {
                    flag = false;
                }
            }
        }

        // ==========================================
        // 封邑兵招募
        // ==========================================
        private static bool TryRecruitFief(MobileParty party, Settlement settlement)
        {
            var fiefManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (fiefManager == null) return false;

            int available = fiefManager.GetAvailableTroopCount(settlement);
            if (available <= 0) return false;

            return fiefManager.RecruitFiefTroopsFromSettlement(
                settlement,
                party) > 0;
        }

        // ==========================================
        // AI lord volunteer recruitment
        // ==========================================
        private static bool TryRecruitVolunteers(
            RecruitmentCampaignBehavior instance, MobileParty party, Settlement settlement)
        {
            if (party.Party.NumberOfAllMembers >= party.Party.PartySizeLimit)
            {
                return false;
            }

            List<RecruitmentCandidate> candidates =
                CollectVolunteerCandidates(party, settlement);

            if (candidates.Count == 0)
            {
                return false;
            }

            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                party,
                candidates);
            int approvedCount = 0;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.RecruitableCount <= 0
                    || !(evaluation.Candidate?.SourceContext
                        is VolunteerOffer offer))
                {
                    continue;
                }

                // Every candidate represents one concrete notable slot, so an
                // approved evaluation can execute the native transaction once.
                GetRecruitVolunteerFromIndividual(
                    instance,
                    party,
                    evaluation.Troop,
                    offer.Notable,
                    offer.SlotIndex);
                approvedCount++;
            }

            LogVolunteerPlan(
                party,
                settlement,
                plan,
                candidates.Count,
                approvedCount);
            return approvedCount > 0;
        }

        /// <summary>
        /// Collects every non-empty volunteer slot currently unlocked by the
        /// party leader's relation. No troop or gold is changed during this
        /// discovery pass.
        /// </summary>
        private static List<RecruitmentCandidate> CollectVolunteerCandidates(
            MobileParty party,
            Settlement settlement)
        {
            List<RecruitmentCandidate> result =
                new List<RecruitmentCandidate>();

            foreach (Hero notable in settlement.Notables)
            {
                if (notable == null || !notable.IsAlive)
                {
                    continue;
                }

                int accessibleSlotCount = Campaign.Current.Models
                    .VolunteerModel
                    .MaximumIndexHeroCanRecruitFromHero(
                        party.LeaderHero,
                        notable,
                        -101);
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

                    result.Add(
                        new RecruitmentCandidate(
                            troop,
                            1,
                            RecruitmentSource.Volunteer,
                            new VolunteerOffer(notable, slotIndex)));
                }
            }

            return result;
        }

        /// <summary>
        /// Logs one summary per settlement visit instead of one message per
        /// volunteer slot, keeping Notice output readable during AI activity.
        /// </summary>
        private static void LogVolunteerPlan(
            MobileParty party,
            Settlement settlement,
            RecruitmentPlan plan,
            int offeredCount,
            int approvedCount)
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
                "{=ModifiedArmy_AIRecruitmentVolunteerPlan}" +
                "[AIRecruitment] Party='{PARTY_NAME}' | " +
                "Settlement='{SETTLEMENT_NAME}' | Culture={CULTURE_ID} | " +
                "VolunteerOffers={OFFERED} | Approved={APPROVED} | " +
                "FirstLimit={LIMIT}");
            message.SetTextVariable("PARTY_NAME", party.Name);
            message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
            message.SetTextVariable("CULTURE_ID", plan.CultureId);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", approvedCount);
            message.SetTextVariable("LIMIT", GetLimitReasonText(mainLimit));
            ModLogger.Notice(message.ToString());
        }

        /// <summary>
        /// Execution metadata linking a model candidate back to the native
        /// notable volunteer slot that must be cleared after recruitment.
        /// </summary>
        private sealed class VolunteerOffer
        {
            public VolunteerOffer(Hero notable, int slotIndex)
            {
                Notable = notable;
                SlotIndex = slotIndex;
            }

            public Hero Notable { get; }

            public int SlotIndex { get; }
        }

        // ==========================================
        // 领主雇佣兵招募（原版 else if 内 mercenary 逻辑提取）
        // ==========================================

        private static bool TryRecruitLordMercenary(
            RecruitmentCampaignBehavior instance, 
            MobileParty party, 
            Settlement settlement,
            float mercenaryWeight)
        {
            if (!settlement.IsTown) return false;

            var mercenaryData = instance.GetMercenaryData(settlement.Town);
            if (!mercenaryData.HasAvailableMercenary(Occupation.Mercenary)) return false;

            CharacterObject troopType = mercenaryData.TroopType;
            if (troopType == null) return false;

            float recruitProbability = MathF.Clamp(mercenaryWeight, 0f, 1f);

            int recruited = 0;
            int maxAvailable = mercenaryData.Number;

            // 对每个可用雇佣兵掷骰子
            for (int j = 0; j < maxAvailable; j++)
            {
                if (MBRandom.RandomFloat < recruitProbability)
                    recruited++;
            }

            if (recruited <= 0)
            {
                return false;
            }

            // The probability roll decides demand. The unified model then
            // applies composition, capacity, wage and thirty-day affordability
            // constraints to the requested quantity.
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                party,
                new List<RecruitmentCandidate>
                {
                    new RecruitmentCandidate(
                        troopType,
                        recruited,
                        RecruitmentSource.Mercenary)
                });
            RecruitmentEvaluationResult evaluation =
                plan.Evaluations.Count > 0
                    ? plan.Evaluations[0]
                    : null;

            if (evaluation == null)
            {
                return false;
            }

            LogMercenaryEvaluation(
                party,
                settlement,
                plan,
                evaluation);

            if (evaluation.RecruitableCount <= 0)
            {
                return false;
            }

            ApplyRecruitMercenary(
                instance,
                party,
                settlement,
                troopType,
                evaluation.RecruitableCount);

            return true;
        }

        /// <summary>
        /// Emits one localized diagnostic record for each mercenary request
        /// evaluated by the unified recruitment model.
        /// </summary>
        private static void LogMercenaryEvaluation(
            MobileParty party,
            Settlement settlement,
            RecruitmentPlan plan,
            RecruitmentEvaluationResult evaluation)
        {
            TextObject message = new TextObject(
                "{=ModifiedArmy_AIRecruitmentMercenaryEvaluation}" +
                "[AIRecruitment] Party='{PARTY_NAME}' | " +
                "Settlement='{SETTLEMENT_NAME}' | Culture={CULTURE_ID} | " +
                "Troop='{TROOP_NAME}' | Tier={TIER} | " +
                "Role={ROLE} | Quality={QUALITY} | " +
                "Requested={REQUESTED} | Approved={APPROVED} | " +
                "Limit={LIMIT} | UnitCost={UNIT_COST} | " +
                "UnitWage={UNIT_WAGE} | SustainableDays={DAYS}");

            message.SetTextVariable("PARTY_NAME", party.Name);
            message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
            message.SetTextVariable("CULTURE_ID", plan.CultureId);
            message.SetTextVariable("TROOP_NAME", evaluation.Troop.Name);
            message.SetTextVariable("TIER", evaluation.Troop.Tier);
            message.SetTextVariable(
                "ROLE",
                GetCombatRoleText(evaluation.CombatRole));
            message.SetTextVariable(
                "QUALITY",
                GetQualityText(evaluation.Quality));
            message.SetTextVariable("REQUESTED", evaluation.RequestedCount);
            message.SetTextVariable("APPROVED", evaluation.RecruitableCount);
            message.SetTextVariable(
                "LIMIT",
                GetLimitReasonText(evaluation.PrimaryLimit));
            message.SetTextVariable(
                "UNIT_COST",
                evaluation.UnitRecruitmentCost);
            message.SetTextVariable("UNIT_WAGE", evaluation.UnitDailyWage);
            message.SetTextVariable("DAYS", evaluation.SustainableDays);
            ModLogger.Notice(message.ToString());
        }

        /// <summary>
        /// Converts internal diagnostic enums to localized player-facing text.
        /// </summary>
        private static TextObject GetCombatRoleText(
            CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Ranged:
                    return new TextObject("{=ModifiedArmy_RecruitRoleRanged}Ranged");
                case CombatRole.Cavalry:
                    return new TextObject("{=ModifiedArmy_RecruitRoleCavalry}Cavalry");
                case CombatRole.HorseArcher:
                    return new TextObject("{=ModifiedArmy_RecruitRoleHorseArcher}Horse Archer");
                default:
                    return new TextObject("{=ModifiedArmy_RecruitRoleInfantry}Infantry");
            }
        }

        /// <summary>
        /// Converts a Tier-based quality group to localized diagnostic text.
        /// </summary>
        private static TextObject GetQualityText(
            TroopQuality quality)
        {
            switch (quality)
            {
                case TroopQuality.MiddleTier:
                    return new TextObject("{=ModifiedArmy_RecruitQualityMiddle}Middle Tier");
                case TroopQuality.TopTier:
                    return new TextObject("{=ModifiedArmy_RecruitQualityTop}Top Tier");
                default:
                    return new TextObject("{=ModifiedArmy_RecruitQualityLow}Low Tier");
            }
        }

        /// <summary>
        /// Uses a stable localization key for every recruitment limit reason.
        /// </summary>
        private static TextObject GetLimitReasonText(
            RecruitmentLimitReason reason)
        {
            return new TextObject(
                "{=ModifiedArmy_RecruitLimit_" + reason + "}" + reason);
        }
    }
}
