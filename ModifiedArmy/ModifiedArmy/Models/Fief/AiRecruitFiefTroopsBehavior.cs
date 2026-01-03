using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.LinQuick;

namespace ModifiedArmy.Models.Fief
{
    public class AiRecruitFiefTroopsBehavior : CampaignBehaviorBase
    {
        private FiefPartyManager _fiefPartyManager;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, AiHourlyTick);

            var msg = GameTexts.FindText("str_modifiedarmy_ai_recruit_behavior_loaded");
            ModLogger.Notice(msg.ToString());
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        // ========== 阶段1：每日决策是否需要前往封邑 ==========
        private void OnDailyTick()
        {
            if (Campaign.Current == null) return;

            _fiefPartyManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (_fiefPartyManager == null) return;

            Dictionary<Clan, List<Settlement>> clanAvailableSettlements = new Dictionary<Clan, List<Settlement>>();

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;

                foreach (Clan clan in kingdom.Clans)
                {
                    if (clan.IsEliminated || clan == Clan.PlayerClan) continue;

                    List<Settlement> availableSettlements = new List<Settlement>();
                    foreach (Settlement settlement in clan.Settlements)
                    {
                        if (!settlement.IsTown && !settlement.IsCastle) continue;
                        if (_fiefPartyManager.GetAvailableTroopCount(settlement) > 0)
                        {
                            availableSettlements.Add(settlement);
                        }
                    }

                    if (availableSettlements.Count == 0) continue;

                    clanAvailableSettlements[clan] = availableSettlements;
                    int settlementIndex = 0;
                    int totalSettlements = availableSettlements.Count;

                    foreach (WarPartyComponent warParty in clan.WarPartyComponents)
                    {
                        MobileParty party = warParty.MobileParty;
                        if (party == null || !party.IsLordParty || party.Army != null) continue;
                        if (IsPartyCurrentlyOnValidFiefRecruitmentTask(party, clan))
                        {
                            continue;
                        }
                        if (!ShouldRecruitForParty(party)) continue;

                        Settlement targetSettlement = availableSettlements[settlementIndex % totalSettlements];
                        settlementIndex++;

                        party.SetMoveGoToSettlement(
                            targetSettlement,
                            MobileParty.NavigationType.Default,
                            isTargetingThePort: false
                        );
                    }
                }
            }
        }

        // ========== 阶段2：AI 每小时检查是否在封邑内并招募 ==========
        private void AiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
        {
            // 跳过非领主或已在 army 中的部队
            if (!party.IsLordParty || party.Army != null)
                return;

            // 必须当前位于某个 settlement 内部
            if (party.CurrentSettlement is not Settlement settlement)
                return;

            // 必须是自有 Town 或 Castle
            if (settlement.OwnerClan != party.LeaderHero?.Clan ||
                (!settlement.IsTown && !settlement.IsCastle))
                return;

            // 获取 manager（复用字段）
            _fiefPartyManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (_fiefPartyManager == null)
                return;

            // 检查是否有可招募兵源
            int recruitableCount = _fiefPartyManager.GetAvailableTroopCount(settlement);
            if (recruitableCount <= 0)
                return;

            // 再次检查招募条件
            if (!ShouldRecruitForParty(party))
                return;

            // 执行招募
            int recruited = _fiefPartyManager.RecruitFiefTroopsFromSettlement(settlement, party);
            if (recruited > 0)
            {
                string partyName = party.Name?.ToString() ?? "UnknownParty";
                string settlementName = settlement.Name?.ToString() ?? "UnknownSettlement";

                TextObject message = GameTexts.FindText("str_modifiedarmy_ai_recruited_fief_troops");
                message.SetTextVariable("PARTY_NAME", partyName);
                message.SetTextVariable("SETTLEMENT_NAME", settlementName);
                message.SetTextVariable("COUNT", recruited);

                ModLogger.Info(message.ToString());
            }
        }

        /// <summary>
        /// 判断该部队是否正在前往本家族封邑执行招募任务（且尚未到达）。
        /// </summary>
        private bool IsPartyCurrentlyOnValidFiefRecruitmentTask(MobileParty party, Clan clan)
        {
            if (party == null || party.Ai == null)
                return false;

            var target = party.TargetSettlement;

            // 条件2: 有明确的目标定居点
            if (target == null)
                return false;

            // 条件3: 目标属于本家族，且是 Town 或 Castle
            if (target.OwnerClan != clan || (!target.IsTown && !target.IsCastle))
                return false;

            // 条件4: 尚未到达（仍在路上）
            float joinRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
            if (party.Position.DistanceSquared(target.GatePosition) <= joinRadius * joinRadius)
                return false; // 已到达，不算“执行中”

            return true;
        }

        /// <summary>
        /// 判断该领主部队是否应发起招募（金钱、兵力缺口等）。
        /// </summary>
        private bool ShouldRecruitForParty(MobileParty party)
        {
            if (party.Party.IsStarving) return false;
            if (party.PartySizeRatio >= 0.9f) return false;

            float minGoldNeeded = party.TotalWage * 0.3f;
            bool hasEnoughGold = party.PartyTradeGold >= minGoldNeeded ||
                                (party.LeaderHero?.Clan.Gold ?? 0) >= minGoldNeeded;

            return hasEnoughGold;
        }
    }

    ///
    /// 招募志愿兵消耗繁荣度或户数
    /// 
    [HarmonyPatch(typeof(RecruitmentVM), "OnDone")]
    public static class RecruitmentVM_OnDone_ReplacePatch
    {
        public static void Prefix(RecruitmentVM __instance)
        {
            // __instance.RefreshPartyProperties();
            int num = __instance.TroopsInCart.Sum((RecruitVolunteerTroopVM t) => t.Cost);
            if (num > Hero.MainHero.Gold)
            {
                Debug.FailedAssert("Execution shouldn't come here. The checks should happen before", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\GameMenu\\Recruitment\\RecruitmentVM.cs", "OnDone", 229);
                return;
            }

            Settlement settlement = Settlement.CurrentSettlement;

            int prosperityCost = 0;
            int hearthCost = 0;
            int count = 0;

            foreach (RecruitVolunteerTroopVM recruitVolunteerTroopVM in __instance.TroopsInCart)
            {
                recruitVolunteerTroopVM.Owner.OwnerHero.VolunteerTypes[recruitVolunteerTroopVM.Index] = null;
                MobileParty.MainParty.MemberRoster.AddToCounts(recruitVolunteerTroopVM.Character, 1, false, 0, 0, true, -1);
                CampaignEventDispatcher.Instance.OnUnitRecruited(recruitVolunteerTroopVM.Character, 1);

                if (settlement.IsTown)
                {
                    prosperityCost += RecruitmentCosts.TownProsperityCostPerTier * recruitVolunteerTroopVM.Character.Tier;
                }
                else if (settlement.IsVillage)
                {
                    hearthCost += RecruitmentCosts.VillageHearthCostPerTier * recruitVolunteerTroopVM.Character.Tier;
                }
                count += 1;
            }

            if (settlement.IsTown)
            {
                // 扣除繁荣度
                settlement.Town.Prosperity = Math.Max(0f, settlement.Town.Prosperity - prosperityCost);

                TextObject msg = GameTexts.FindText("str_recruitment_prosperity_cost");
                msg.SetTextVariable("PARTY_NAME", MobileParty.MainParty.Name.ToString());
                msg.SetTextVariable("SETTLEMENT_NAME", settlement.Name.ToString());
                msg.SetTextVariable("TROOP_COUNT", count);
                msg.SetTextVariable("PROSPERITY_COST", prosperityCost);

                ModLogger.Notice(msg.ToString());
            }
            else if (settlement.IsVillage)
            {
                // 扣除户数
                settlement.Village.Hearth = Math.Max(0f, settlement.Village.Hearth - hearthCost);

                TextObject msg = GameTexts.FindText("str_recruitment_hearth_cost");
                msg.SetTextVariable("PARTY_NAME", MobileParty.MainParty.Name.ToString());
                msg.SetTextVariable("SETTLEMENT_NAME", settlement.Name.ToString());
                msg.SetTextVariable("TROOP_COUNT", count);
                msg.SetTextVariable("HEARTH_COST", hearthCost);

                ModLogger.Notice(msg.ToString());
            }

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, num, true);
            if (num > 0)
            {
                MBTextManager.SetTextVariable("GOLD_AMOUNT", MathF.Abs(num));
                InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_gold_removed_with_icon", null).ToString(), "event:/ui/notification/coins_negative"));
            }
            __instance.Deactivate();
        }
    }

   // [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "RecruitVolunteersFromNotable")]
   // public static class RecruitmentCampaignBehavior_RecruitVolunteersFromNotable_ReplacePatch
   // {

   //     public static void Prefix(
   //         RecruitmentCampaignBehavior __instance,
   //         MobileParty mobileParty,
   //         Settlement settlement)
   //     {
   //         if (((float)mobileParty.Party.NumberOfAllMembers + 0.5f) / (float)mobileParty.Party.PartySizeLimit <= 1f)
			//{
			//	foreach (Hero hero in settlement.Notables)
			//	{
			//		if (hero.IsAlive)
			//		{
			//			int num = hero.VolunteerTypes.FindIndexQ((CharacterObject x) => x != null);
			//			if (num >= 0)
			//			{
			//				int num2 = MBRandom.RandomInt(6);
			//				int num3 = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(mobileParty.IsGarrison ? mobileParty.Party.Owner : mobileParty.LeaderHero, hero, -101);
			//				if (num <= num3)
			//				{
			//					for (int i = num2; i < num2 + 6; i++)
			//					{
			//						int num4 = i % 6;
			//						if (num4 >= num3)
			//						{
			//							break;
			//						}
			//						int num5 = (mobileParty.LeaderHero != null) ? ((int)MathF.Sqrt((float)mobileParty.PartyTradeGold / 10000f)) : 0;
			//						float num6 = MBRandom.RandomFloat;
			//						for (int j = 0; j < num5; j++)
			//						{
			//							float randomFloat = MBRandom.RandomFloat;
			//							if (randomFloat > num6)
			//							{
			//								num6 = randomFloat;
			//							}
			//						}
			//						if (mobileParty.Army != null)
			//						{
			//							float y = (mobileParty.Army.LeaderParty == mobileParty) ? 0.5f : 0.67f;
			//							num6 = MathF.Pow(num6, y);
			//						}
			//						float num7 = (float)mobileParty.Party.NumberOfAllMembers / (float)mobileParty.Party.PartySizeLimit;
			//						if (num6 > num7 - 0.1f)
			//						{
			//							CharacterObject characterObject = hero.VolunteerTypes[num4];
			//							if (characterObject != null && mobileParty.PartyTradeGold > Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(characterObject, mobileParty.LeaderHero, false).RoundedResultNumber && mobileParty.GetAvailableWageBudget() >= Campaign.Current.Models.PartyWageModel.GetCharacterWage(characterObject))
			//							{
   //                                         __instance.GetRecruitVolunteerFromIndividual(mobileParty, characterObject, hero, num4);
			//								break;
			//							}
			//						}
			//					}
			//				}
			//			}
			//		}
			//	}
			//}
   //     }
   // }

}
