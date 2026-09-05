using HarmonyLib;
using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

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
                    ModLogger.Notice(
                        $"[RecruitmentPatches] '{town.Name}' 生成雇佣兵: " +
                        $"兵种='{selectedTroop.Name}', 数量={finalCount}, " +
                        $"模板='{template?.TemplateId}', 文化='{town.Culture.StringId}', " +
                        $"概率={spawnChance:F3}, 数量范围=[{minCount}-{maxCount}]"
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
            candidates.Sort((a, b) => b.weight.CompareTo(a.weight));

            foreach (var (source, weight) in candidates)
            {
                if (weight <= 0.01f) continue;

                bool success = RouteRecruitment(__instance, mobileParty, settlement, source);
                if (success)
                {
                    ModLogger.Notice(
                        $"[AI招兵] {mobileParty.Name}@{settlement.Name} | " +
                        $"兵源={source} | Weight={weight:F3} | Need={decision.NeedScore:F2}");
                    return false; // 成功招募一种即停止
                }
            }

            return false; // 所有候选均失败，跳过原版
        }

        // ==========================================
        // 招募路由器：根据兵源类型分发到具体执行函数
        // ==========================================
        private static bool RouteRecruitment(
            RecruitmentCampaignBehavior instance,
            MobileParty party, Settlement settlement, RecruitSource source)
        {
            switch (source)
            {
                case RecruitSource.Fief:
                    return TryRecruitFief(party, settlement);

                case RecruitSource.Volunteer:
                    return TryRecruitVolunteers(instance, party, settlement);

                case RecruitSource.Mercenary:
                    return TryRecruitLordMercenary(instance, party, settlement);

                default:
                    return false;
            }
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

            fiefManager.RecruitFiefTroopsFromSettlement(settlement, party);
            return true;
        }

        // ==========================================
        // 志愿兵招募（复用原版方法）
        // ==========================================

        private static void RecruitVolunteersFromNotable(
            RecruitmentCampaignBehavior instance, 
            MobileParty mobileParty, 
            Settlement settlement)
		{
			if (((float)mobileParty.Party.NumberOfAllMembers + 0.5f) / (float)mobileParty.Party.PartySizeLimit <= 1f)
			{
				foreach (Hero hero in settlement.Notables)
				{
					if (hero.IsAlive)
					{
						int num = hero.VolunteerTypes.FindIndexQ((CharacterObject x) => x != null);
						if (num >= 0)
						{
							int num2 = MBRandom.RandomInt(6);
							int num3 = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(mobileParty.IsGarrison ? mobileParty.Party.Owner : mobileParty.LeaderHero, hero, -101);
							if (num <= num3)
							{
								for (int i = num2; i < num2 + 6; i++)
								{
									int num4 = i % 6;
									if (num4 >= num3)
									{
										break;
									}
									int num5 = (mobileParty.LeaderHero != null) ? ((int)MathF.Sqrt((float)mobileParty.PartyTradeGold / 10000f)) : 0;
									float num6 = MBRandom.RandomFloat;
									for (int j = 0; j < num5; j++)
									{
										float randomFloat = MBRandom.RandomFloat;
										if (randomFloat > num6)
										{
											num6 = randomFloat;
										}
									}
									if (mobileParty.Army != null)
									{
										float y = (mobileParty.Army.LeaderParty == mobileParty) ? 0.5f : 0.67f;
										num6 = MathF.Pow(num6, y);
									}
									float num7 = (float)mobileParty.Party.NumberOfAllMembers / (float)mobileParty.Party.PartySizeLimit;
									if (num6 > num7 - 0.1f)
									{
										CharacterObject characterObject = hero.VolunteerTypes[num4];
										if (characterObject != null && mobileParty.PartyTradeGold > Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(characterObject, mobileParty.LeaderHero, false).RoundedResultNumber && mobileParty.GetAvailableWageBudget() >= Campaign.Current.Models.PartyWageModel.GetCharacterWage(characterObject))
										{
											GetRecruitVolunteerFromIndividual(instance, mobileParty, characterObject, hero, num4);
											break;
										}
									}
								}
							}
						}
					}
				}
			}
		}

        private static bool TryRecruitVolunteers(
            RecruitmentCampaignBehavior instance, MobileParty party, Settlement settlement)
        {
            if (party.Party.NumberOfAllMembers >= party.Party.PartySizeLimit) return false;
            if (party.IsWageLimitExceeded()) return false;

            // 直接调用原版 RecruitmentCampaignBehavior 的方法
            // 该方法内部已包含 Notable 好感度、志愿兵池等完整检查
            RecruitVolunteersFromNotable(instance, party, settlement);
            return true;
        }

        // ==========================================
        // 领主雇佣兵招募（原版 else if 内 mercenary 逻辑提取）
        // ==========================================
        private static bool TryRecruitLordMercenary(
            RecruitmentCampaignBehavior instance, MobileParty party, Settlement settlement)
        {
            if (!settlement.IsTown) return false;

            var mercenaryData = instance.GetMercenaryData(settlement.Town);
            if (!mercenaryData.HasAvailableMercenary(Occupation.Mercenary)) return false;

            CharacterObject troopType = mercenaryData.TroopType;
            if (troopType == null) return false;

            int cost = Campaign.Current.Models.PartyWageModel
                .GetTroopRecruitmentCost(troopType, party.LeaderHero, false).RoundedResultNumber;
            if (cost >= 5000) return false;

            float fillRatio = (float)party.Party.NumberOfAllMembers / party.Party.PartySizeLimit;

            // 原版概率计算
            float goldDenominator = cost <= 100 ? 100000f
                : cost <= 200 ? 125000f
                : cost <= 400 ? 150000f
                : cost <= 700 ? 175000f
                : cost <= 1100 ? 200000f
                : cost <= 1600 ? 250000f
                : cost <= 2200 ? 300000f
                : 400000f;

            float goldProb = MathF.Min(1f, party.PartyTradeGold / goldDenominator);
            float sizeFactor = MathF.Max(1f, MathF.Min(10f, 1f / fillRatio)) - 1f;
            float recruitProb = goldProb * goldProb * sizeFactor * 0.25f;

            // 按概率决定招募数量
            int recruited = 0;
            int maxAvailable = mercenaryData.Number;
            int wagePerTroop = Campaign.Current.Models.PartyWageModel.GetCharacterWage(troopType);

            for (int j = 0; j < maxAvailable; j++)
            {
                if (MBRandom.RandomFloat < recruitProb)
                    recruited++;
            }

            // 多重上限约束
            recruited = MathF.Min(recruited, party.Party.PartySizeLimit - party.Party.NumberOfAllMembers);
            recruited = cost <= 0 ? recruited : MathF.Min(party.PartyTradeGold / cost, recruited);
            recruited = MathF.Min(recruited, party.GetAvailableWageBudget() / wagePerTroop);

            if (recruited <= 0) return false;

            ApplyRecruitMercenary(instance, party, settlement, troopType, recruited);
            return true;
        }
    }
}