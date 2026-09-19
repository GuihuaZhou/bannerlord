using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x02000451 RID: 1105
	public class VillagerCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x0600475E RID: 18270 RVA: 0x00165A74 File Offset: 0x00163C74
		public override void RegisterEvents()
		{
			CampaignEvents.HourlyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(this.HourlyTickSettlement));
			CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.HourlyTickParty));
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
			CampaignEvents.SettlementEntered.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(this.OnSettlementEntered));
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));
			CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, new Action<MobileParty, PartyBase>(this.OnMobilePartyDestroyed));
			CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, new Action<PartyBase, PartyBase, ItemRoster>(this.OnLootDistributedToParty));
			CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, new Action<SiegeEvent>(this.OnSiegeEventStarted));
		}

		// Token: 0x0600475F RID: 18271 RVA: 0x00165B3C File Offset: 0x00163D3C
		private void OnSiegeEventStarted(SiegeEvent siegeEvent)
		{
			for (int i = 0; i < siegeEvent.BesiegedSettlement.Parties.Count; i++)
			{
				if (siegeEvent.BesiegedSettlement.Parties[i].IsVillager)
				{
					siegeEvent.BesiegedSettlement.Parties[i].SetMoveModeHold();
				}
			}
		}

		// Token: 0x06004760 RID: 18272 RVA: 0x00165B92 File Offset: 0x00163D92
		private void OnLootDistributedToParty(PartyBase winnerParty, PartyBase defeatedParty, ItemRoster lootedItems)
		{
			if (winnerParty.IsMobile && defeatedParty.IsMobile && defeatedParty.MobileParty.IsVillager)
			{
				SkillLevelingManager.OnLoot(winnerParty.MobileParty, defeatedParty.MobileParty, lootedItems, true);
			}
		}

		// Token: 0x06004761 RID: 18273 RVA: 0x00165BC4 File Offset: 0x00163DC4
		public override void SyncData(IDataStore dataStore)
		{
			dataStore.SyncData<float>("_collectFoodWaitHoursProgress", ref this._collectFoodWaitHoursProgress);
			dataStore.SyncData<float>("_collectVolunteerWaitHoursProgress", ref this._collectVolunteerWaitHoursProgress);
			dataStore.SyncData<Dictionary<MobileParty, CampaignTime>>("_lootedVillagers", ref this._lootedVillagers);
			dataStore.SyncData<Dictionary<MobileParty, VillagerCampaignBehavior.PlayerInteraction>>("_interactedVillagers", ref this._interactedVillagers);
			dataStore.SyncData<Dictionary<Village, CampaignTime>>("_villageLastVillagerSendTime", ref this._villageLastVillagerSendTime);
		}

		// Token: 0x06004762 RID: 18274 RVA: 0x00165C2C File Offset: 0x00163E2C
		private void DeleteExpiredLootedVillagers()
		{
			List<MobileParty> list = new List<MobileParty>();
			foreach (KeyValuePair<MobileParty, CampaignTime> keyValuePair in this._lootedVillagers)
			{
				if (CampaignTime.Now - keyValuePair.Value >= CampaignTime.Days(10f))
				{
					list.Add(keyValuePair.Key);
				}
			}
			foreach (MobileParty key in list)
			{
				this._lootedVillagers.Remove(key);
			}
		}

		// Token: 0x06004763 RID: 18275 RVA: 0x00165CF4 File Offset: 0x00163EF4
		public void DailyTick()
		{
			this.DeleteExpiredLootedVillagers();
		}

		// Token: 0x06004764 RID: 18276 RVA: 0x00165CFC File Offset: 0x00163EFC
		private void TickVillageThink(Settlement settlement)
		{
			Village village = settlement.Village;
			if (village != null && village.VillageState == Village.VillageStates.Normal && settlement.Party.MapEvent == null)
			{
				this.ThinkAboutSendingItemToTown(village);
			}
		}

		// Token: 0x06004765 RID: 18277 RVA: 0x00165D30 File Offset: 0x00163F30
		private void ThinkAboutSendingItemToTown(Village village)
		{
			if (MBRandom.RandomFloat < 0.15f)
			{
				VillagerPartyComponent villagerPartyComponent = village.VillagerPartyComponent;
				MobileParty mobileParty = (villagerPartyComponent != null) ? villagerPartyComponent.MobileParty : null;
				if ((mobileParty != null && (mobileParty.CurrentSettlement != village.Owner.Settlement || mobileParty.MapEvent != null || mobileParty.IsInRaftState)) || village.Owner.MapEvent != null)
				{
					return;
				}
				int num = 0;
				for (int i = 0; i < village.Owner.ItemRoster.Count; i++)
				{
					num += village.Owner.ItemRoster[i].Amount;
				}
				int warehouseCapacity = village.GetWarehouseCapacity();
				if (num >= warehouseCapacity && village.Owner.MapEvent == null)
				{
					if (mobileParty == null || (this._villageLastVillagerSendTime.ContainsKey(village) && this._villageLastVillagerSendTime[village].ElapsedDaysUntilNow > 7f && mobileParty.CurrentSettlement != village.Settlement))
					{
						if (village.Hearth > (float)Campaign.Current.Models.PartySizeLimitModel.MinimumNumberOfVillagersAtVillagerParty)
						{
							this.CreateVillagerParty(village);
						}
					}
					else
					{
						int idealVillagerPartySize = Campaign.Current.Models.PartySizeLimitModel.GetIdealVillagerPartySize(village);
						if (mobileParty.MemberRoster.TotalManCount < idealVillagerPartySize && mobileParty.HomeSettlement.Village.Hearth > 0f)
						{
							this.AddVillagersToParty(mobileParty, idealVillagerPartySize - mobileParty.MemberRoster.TotalManCount);
						}
					}
					if (mobileParty != null)
					{
						this.LoadAndSendVillagerParty(village, mobileParty);
					}
				}
			}
		}

		// Token: 0x06004766 RID: 18278 RVA: 0x00165EAC File Offset: 0x001640AC
		private void AddVillagersToParty(MobileParty villagerParty, int numberOfVillagersToAdd)
		{
			if (numberOfVillagersToAdd > (int)villagerParty.HomeSettlement.Village.Hearth)
			{
				numberOfVillagersToAdd = (int)villagerParty.HomeSettlement.Village.Hearth;
			}
			villagerParty.HomeSettlement.Village.Hearth -= (float)((numberOfVillagersToAdd + 1) / 2);
			CharacterObject character = villagerParty.HomeSettlement.Culture.VillagerPartyTemplate.Stacks.GetRandomElement<PartyTemplateStack>().Character;
			villagerParty.MemberRoster.AddToCounts(character, numberOfVillagersToAdd, false, 0, 0, true, -1);
		}

		// Token: 0x06004767 RID: 18279 RVA: 0x00165F30 File Offset: 0x00164130
		private void CreateVillagerParty(Village village)
		{
			MobileParty mobileParty = VillagerPartyComponent.CreateVillagerParty(village.Settlement.Culture.VillagerPartyTemplate.StringId + "_1", village);
			village.Hearth = MathF.Max(0f, village.Hearth - (float)((mobileParty.MemberRoster.TotalManCount + 1) / 2));
			EnterSettlementAction.ApplyForParty(mobileParty, village.Settlement);
		}

		// Token: 0x06004768 RID: 18280 RVA: 0x00165F98 File Offset: 0x00164198
		private void LoadAndSendVillagerParty(Village village, MobileParty villagerParty)
		{
			if (!this._villageLastVillagerSendTime.ContainsKey(village))
			{
				this._villageLastVillagerSendTime.Add(village, CampaignTime.Now);
			}
			else
			{
				this._villageLastVillagerSendTime[village] = CampaignTime.Now;
			}
			VillagerCampaignBehavior.MoveItemsToVillagerParty(village, villagerParty);
			this.SendVillagerPartyToTradeBoundTown(villagerParty);
		}

		// Token: 0x06004769 RID: 18281 RVA: 0x00165FE8 File Offset: 0x001641E8
		private static void MoveItemsToVillagerParty(Village village, MobileParty villagerParty)
		{
			ItemRoster itemRoster = village.Settlement.ItemRoster;
			float num = (float)villagerParty.InventoryCapacity - villagerParty.TotalWeightCarried;
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < itemRoster.Count; j++)
				{
					ItemRosterElement itemRosterElement = itemRoster[j];
					ItemObject item = itemRosterElement.EquipmentElement.Item;
					int num2 = MBRandom.RoundRandomized((float)itemRosterElement.Amount * 0.2f);
					if (num2 > 0)
					{
						if (!item.HasHorseComponent && item.Weight * (float)num2 > num)
						{
							num2 = MathF.Ceiling(num / item.Weight);
							if (num2 <= 0)
							{
								goto IL_CC;
							}
						}
						if (!item.HasHorseComponent)
						{
							num -= (float)num2 * item.Weight;
						}
						villagerParty.Party.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, num2);
						itemRoster.AddToCounts(itemRosterElement.EquipmentElement, -num2);
					}
					IL_CC:;
				}
			}
		}

		// Token: 0x0600476A RID: 18282 RVA: 0x001660DC File Offset: 0x001642DC
		private void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
		{
			if (this._interactedVillagers.ContainsKey(mobileParty))
			{
				this._interactedVillagers.Remove(mobileParty);
			}
		}

		// Token: 0x0600476B RID: 18283 RVA: 0x001660F9 File Offset: 0x001642F9
		private void HourlyTickSettlement(Settlement settlement)
		{
			this.DestroyVillagerPartyIfMemberCountIsZero(settlement);
			this.ThinkAboutSendingInsideVillagersToTheirHomeVillage(settlement);
			this.TickVillageThink(settlement);
		}

		// Token: 0x0600476C RID: 18284 RVA: 0x00166110 File Offset: 0x00164310
		private void DestroyVillagerPartyIfMemberCountIsZero(Settlement settlement)
		{
			Village village = settlement.Village;
			if (village != null && village.VillagerPartyComponent != null && village.VillagerPartyComponent.MobileParty.MapEvent == null && village.VillagerPartyComponent.MobileParty.MemberRoster.TotalHealthyCount == 0)
			{
				DestroyPartyAction.Apply(null, village.VillagerPartyComponent.MobileParty);
			}
		}

		// Token: 0x0600476D RID: 18285 RVA: 0x0016616C File Offset: 0x0016436C
		private void HourlyTickParty(MobileParty villagerParty)
		{
			if (!villagerParty.IsVillager || villagerParty.MapEvent != null || !villagerParty.HasLandNavigationCapability)
			{
				return;
			}
			bool flag = false;
			if (villagerParty.HomeSettlement.Village.VillagerPartyComponent == null || villagerParty.HomeSettlement.Village.VillagerPartyComponent.MobileParty != villagerParty)
			{
				DestroyPartyAction.Apply(null, villagerParty);
			}
			else if (villagerParty.DefaultBehavior == AiBehavior.GoToSettlement)
			{
				if (villagerParty.TargetSettlement.IsTown && (villagerParty.TargetSettlement == null || villagerParty.TargetSettlement.IsUnderSiege || FactionManager.IsAtWarAgainstFaction(villagerParty.MapFaction, villagerParty.TargetSettlement.MapFaction)))
				{
					flag = true;
				}
			}
			else
			{
				flag = true;
			}
			float num;
			if (villagerParty.DefaultBehavior == AiBehavior.GoToSettlement && villagerParty.TargetSettlement == villagerParty.HomeSettlement && villagerParty.HomeSettlement.IsUnderRaid && ((villagerParty.CurrentSettlement != null) ? Campaign.Current.Models.MapDistanceModel.GetDistance(villagerParty.CurrentSettlement, villagerParty.HomeSettlement, false, false, MobileParty.NavigationType.Default) : Campaign.Current.Models.MapDistanceModel.GetDistance(villagerParty, villagerParty.HomeSettlement, false, MobileParty.NavigationType.Default, out num)) < Campaign.Current.EstimatedAverageVillagerPartySpeed * 2.5f)
			{
				villagerParty.SetMoveModeHold();
				flag = false;
			}
			if (flag && (villagerParty.CurrentSettlement == null || !villagerParty.CurrentSettlement.IsUnderSiege))
			{
				if (villagerParty.ItemRoster.Count > 1)
				{
					this.SendVillagerPartyToTradeBoundTown(villagerParty);
					return;
				}
				this.TrySendingVillagerPartyToVillage(villagerParty);
			}
		}

		// Token: 0x0600476E RID: 18286 RVA: 0x001662D4 File Offset: 0x001644D4
		private void TrySendingVillagerPartyToVillage(MobileParty villagerParty)
		{
			bool flag = true;
			float num;
			if (villagerParty.HomeSettlement.IsUnderRaid && ((villagerParty.CurrentSettlement != null) ? Campaign.Current.Models.MapDistanceModel.GetDistance(villagerParty.CurrentSettlement, villagerParty.HomeSettlement, false, false, MobileParty.NavigationType.Default) : Campaign.Current.Models.MapDistanceModel.GetDistance(villagerParty, villagerParty.HomeSettlement, false, MobileParty.NavigationType.Default, out num)) < Campaign.Current.EstimatedAverageVillagerPartySpeed * 2.5f)
			{
				flag = false;
			}
			if (flag)
			{
				this.MoveVillagersToSettlementWithBestNavigationType(villagerParty, villagerParty.HomeSettlement);
			}
		}

		// Token: 0x0600476F RID: 18287 RVA: 0x00166360 File Offset: 0x00164560
		private void SendVillagerPartyToTradeBoundTown(MobileParty villagerParty)
		{
			Settlement tradeBound = villagerParty.HomeSettlement.Village.TradeBound;
			if (tradeBound != null && !tradeBound.IsUnderSiege)
			{
				this.MoveVillagersToSettlementWithBestNavigationType(villagerParty, tradeBound);
			}
		}

		// Token: 0x06004770 RID: 18288 RVA: 0x00166394 File Offset: 0x00164594
		private void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
		{
			if (mobileParty != null && mobileParty.IsActive && mobileParty.IsVillager)
			{
				if (settlement.IsTown)
				{
					SellGoodsForTradeAction.ApplyByVillagerTrade(settlement, mobileParty);
				}
				if (settlement.IsVillage && mobileParty.PartyTradeGold != 0)
				{
					int num = Campaign.Current.Models.SettlementTaxModel.CalculateVillageTaxFromIncome(mobileParty.HomeSettlement.Village, mobileParty.PartyTradeGold);
					mobileParty.PartyTradeGold = 0;
					mobileParty.HomeSettlement.Village.TradeTaxAccumulated += num;
				}
				if (settlement.IsTown && settlement.Town.Governor != null && settlement.Town.Governor.GetPerkValue(DefaultPerks.Trade.TravelingRumors))
				{
					settlement.Town.TradeTaxAccumulated += MathF.Round(DefaultPerks.Trade.TravelingRumors.SecondaryBonus);
				}
			}
		}

		// Token: 0x06004771 RID: 18289 RVA: 0x0016646E File Offset: 0x0016466E
		private void SetPlayerInteraction(MobileParty mobileParty, VillagerCampaignBehavior.PlayerInteraction interaction)
		{
			if (this._interactedVillagers.ContainsKey(mobileParty))
			{
				this._interactedVillagers[mobileParty] = interaction;
				return;
			}
			this._interactedVillagers.Add(mobileParty, interaction);
		}

		// Token: 0x06004772 RID: 18290 RVA: 0x0016649C File Offset: 0x0016469C
		private VillagerCampaignBehavior.PlayerInteraction GetPlayerInteraction(MobileParty mobileParty)
		{
			VillagerCampaignBehavior.PlayerInteraction result;
			if (this._interactedVillagers.TryGetValue(mobileParty, out result))
			{
				return result;
			}
			return VillagerCampaignBehavior.PlayerInteraction.None;
		}

		// Token: 0x06004773 RID: 18291 RVA: 0x001664BC File Offset: 0x001646BC
		private void ThinkAboutSendingInsideVillagersToTheirHomeVillage(Settlement settlement)
		{
			if ((settlement.IsVillage || settlement.IsTown) && !settlement.IsUnderSiege && settlement.Party.MapEvent == null)
			{
				for (int i = 0; i < settlement.Parties.Count; i++)
				{
					MobileParty mobileParty = settlement.Parties[i];
					if (mobileParty.IsActive && mobileParty.IsVillager && MBRandom.RandomFloat < 0.2f)
					{
						if (settlement.IsTown)
						{
							this.MoveVillagersToSettlementWithBestNavigationType(mobileParty, mobileParty.HomeSettlement);
						}
						else if (mobileParty.ItemRoster.Count > 1 && settlement != mobileParty.HomeSettlement)
						{
							this.SendVillagerPartyToTradeBoundTown(mobileParty);
						}
					}
				}
			}
		}

		// Token: 0x06004774 RID: 18292 RVA: 0x00166568 File Offset: 0x00164768
		private void MoveVillagersToSettlementWithBestNavigationType(MobileParty villagerParty, Settlement settlement)
		{
			MobileParty.NavigationType navigationType;
			float num;
			bool isFromPort;
			AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(villagerParty, settlement, false, out navigationType, out num, out isFromPort);
			SetPartyAiAction.GetActionForVisitingSettlement(villagerParty, settlement, navigationType, isFromPort, false);
		}

		// Token: 0x06004775 RID: 18293 RVA: 0x0016658D File Offset: 0x0016478D
		public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
		{
			this.AddDialogs(campaignGameStarter);
			this.AddMenus(campaignGameStarter);
		}

		// Token: 0x06004776 RID: 18294 RVA: 0x0016659D File Offset: 0x0016479D
		protected void AddDialogs(CampaignGameStarter campaignGameSystemStarter)
		{
			this.AddVillageFarmerTradeAndLootDialogs(campaignGameSystemStarter);
		}

		// Token: 0x06004777 RID: 18295 RVA: 0x001665A6 File Offset: 0x001647A6
		private void AddMenus(CampaignGameStarter campaignGameSystemStarter)
		{
		}

		// Token: 0x06004778 RID: 18296 RVA: 0x001665A8 File Offset: 0x001647A8
		private void take_food_confirm_forget_it_on_consequence(MenuCallbackArgs args)
		{
			GameMenu.SwitchToMenu("village_hostile_action");
		}

		// Token: 0x06004779 RID: 18297 RVA: 0x001665B4 File Offset: 0x001647B4
		public bool taking_food_from_villagers_wait_on_condition(MenuCallbackArgs args)
		{
			int skillValue = MobilePartyHelper.GetHeroWithHighestSkill(MobileParty.MainParty, DefaultSkills.Roguery).GetSkillValue(DefaultSkills.Roguery);
			this._collectFoodTotalWaitHours = (float)(12 - (int)((float)skillValue / 30f));
			args.MenuContext.GameMenu.SetTargetedWaitingTimeAndInitialProgress(this._collectFoodTotalWaitHours, this._collectFoodWaitHoursProgress / this._collectFoodTotalWaitHours);
			return true;
		}

		// Token: 0x0600477A RID: 18298 RVA: 0x00166614 File Offset: 0x00164814
		public bool press_into_service_confirm_on_condition(MenuCallbackArgs args)
		{
			int skillValue = MobilePartyHelper.GetHeroWithHighestSkill(MobileParty.MainParty, DefaultSkills.Roguery).GetSkillValue(DefaultSkills.Roguery);
			this._collectVolunteersTotalWaitHours = (float)(24 - (int)((float)skillValue / 15f));
			args.MenuContext.GameMenu.SetTargetedWaitingTimeAndInitialProgress(this._collectVolunteersTotalWaitHours, this._collectFoodWaitHoursProgress / this._collectFoodTotalWaitHours);
			return true;
		}

		// Token: 0x0600477B RID: 18299 RVA: 0x00166672 File Offset: 0x00164872
		public void taking_food_from_villagers_wait_on_consequence(MenuCallbackArgs args)
		{
			Village village = Settlement.CurrentSettlement.Village;
			GameMenu.ActivateGameMenu("menu_village_take_food_success");
			ChangeVillageStateAction.ApplyBySettingToNormal(MobileParty.MainParty.CurrentSettlement);
		}

		// Token: 0x0600477C RID: 18300 RVA: 0x00166698 File Offset: 0x00164898
		private void press_into_service_confirm_on_consequence(MenuCallbackArgs args)
		{
			Village village = Settlement.CurrentSettlement.Village;
			GameMenu.ActivateGameMenu("menu_press_into_service_success");
			ChangeVillageStateAction.ApplyBySettingToNormal(MobileParty.MainParty.CurrentSettlement);
		}

		// Token: 0x0600477D RID: 18301 RVA: 0x001666C0 File Offset: 0x001648C0
		private void AddVillageFarmerTradeAndLootDialogs(CampaignGameStarter starter)
		{
			starter.AddDialogLine("village_farmer_talk_start", "start", "village_farmer_talk", "{=ddymPMWg}{VILLAGER_GREETING}", new ConversationSentence.OnConditionDelegate(this.village_farmer_talk_start_on_condition), null, 100, null);
			starter.AddDialogLine("village_farmer_pretalk_start", "village_farmer_pretalk", "village_farmer_talk", "{=cZjaGL9R}Is there anything else I can do it for you?", null, null, 100, null);
			starter.AddPlayerLine("village_farmer_buy_products", "village_farmer_talk", "village_farmer_player_trade", "{=r46NWboa}I'm going to market too. What kind of products do you have?", new ConversationSentence.OnConditionDelegate(this.village_farmer_buy_products_on_condition), null, 100, null, null);
			starter.AddDialogLine("village_farmer_specify_products", "village_farmer_player_trade", "player_trade_decision", "{=BxazyNwY}We have {PRODUCTS}. We can let you have them for {TOTAL_PRICE}{GOLD_ICON}.", null, null, 100, null);
			starter.AddPlayerLine("player_decided_to_buy", "player_trade_decision", "close_window", "{=HQ6hyVNH}All right. Here is {TOTAL_PRICE}{GOLD_ICON}.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_player_decided_to_buy_on_consequence), 100, null, null);
			starter.AddPlayerLine("player_decided_not_to_buy", "player_trade_decision", "village_farmer_pretalk", "{=D33fIGQe}Never mind.", null, null, 100, null, null);
			starter.AddPlayerLine("village_farmer_loot", "village_farmer_talk", "village_farmer_loot_talk", "{=XaPMUJV0}Whatever you have, I'm taking it. Surrender or die!", new ConversationSentence.OnConditionDelegate(this.village_farmer_loot_on_condition), null, 100, new ConversationSentence.OnClickableConditionDelegate(this.village_farmer_loot_on_clickable_condition), null);
			starter.AddDialogLine("village_farmer_fight", "village_farmer_loot_talk", "village_farmer_do_not_bribe", "{=ctEEfvsk}What? We're not warriors, but I bet we can take you. If you want our goods, you'll have to fight us![rf:idle_angry][ib:aggressive]", new ConversationSentence.OnConditionDelegate(this.conversation_village_farmer_not_bribe_on_condition), null, 100, null);
			starter.AddPlayerLine("village_farmer_leave", "village_farmer_talk", "close_window", "{=1IJouNaM}Carry on, then. Farewell.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_leave_on_consequence), 100, null, null);
			starter.AddPlayerLine("player_decided_to_fight_villagers", "village_farmer_do_not_bribe", "close_window", "{=1r0tDsrR}Attack!", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_fight_on_consequence), 100, null, null);
			starter.AddPlayerLine("player_decided_to_not_fight_villagers_1", "village_farmer_do_not_bribe", "close_window", "{=D33fIGQe}Never mind.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_leave_on_consequence), 100, null, null);
			starter.AddDialogLine("village_farmer_accepted_to_give_some_goods", "village_farmer_loot_talk", "village_farmer_give_some_goods", "{=dMc3SjOK}We can pay you. {TAKE_MONEY_AND_PRODUCT_STRING}[rf:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(this.conversation_village_farmer_give_goods_on_condition), null, 100, null);
			starter.AddPlayerLine("player_decided_to_take_some_goods_villagers", "village_farmer_give_some_goods", "village_farmer_end_talk", "{=VT1hSCaw}All right.", null, null, 100, null, null);
			starter.AddPlayerLine("player_decided_to_take_everything_villagers", "village_farmer_give_some_goods", "player_wants_everything_villagers", "{=VpGjkNrF}I want everything.", null, null, 100, null, null);
			starter.AddPlayerLine("player_decided_to_not_fight_villagers_2", "village_farmer_give_some_goods", "close_window", "{=D33fIGQe}Never mind.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_leave_on_consequence), 100, null, null);
			starter.AddDialogLine("village_farmer_fight_no_surrender", "player_wants_everything_villagers", "close_window", "{=wAhXFoNH}You'll have to fight us first![rf:idle_angry][ib:aggressive]", new ConversationSentence.OnConditionDelegate(this.conversation_village_farmer_not_surrender_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_fight_on_consequence), 100, null);
			starter.AddDialogLine("village_farmer_accepted_to_give_everything", "player_wants_everything_villagers", "player_decision_to_take_prisoner_villagers", "{=33mKghKQ}Please don't kill us. We surrender.[rf:idle_angry][ib:nervous]", new ConversationSentence.OnConditionDelegate(this.conversation_village_farmer_give_goods_on_condition), null, 100, null);
			starter.AddPlayerLine("player_do_not_take_prisoner_villagers", "player_decision_to_take_prisoner_villagers", "village_farmer_end_talk_surrender", "{=6kaia5qP}Give me all your wares!", null, null, 100, delegate(out TextObject explanation)
			{
				if (!Hero.MainHero.MapFaction.IsAtWarWith(MobileParty.ConversationParty.MapFaction))
				{
					explanation = new TextObject("{=1LlH1Jof}This action will start a war.", null);
				}
				else
				{
					explanation = null;
				}
				return true;
			}, null);
			starter.AddPlayerLine("player_decided_to_take_prisoner_2", "player_decision_to_take_prisoner_villagers", "villager_taken_prisoner_warning", "{=g5G8AJ5n}You are my prisoner now.", null, null, 100, delegate(out TextObject explanation)
			{
				if (!Hero.MainHero.MapFaction.IsAtWarWith(MobileParty.ConversationParty.MapFaction))
				{
					explanation = new TextObject("{=1LlH1Jof}This action will start a war.", null);
				}
				else
				{
					explanation = null;
				}
				return true;
			}, null);
			starter.AddPlayerLine("player_decided_to_take_prisoner_2", "player_decision_to_take_prisoner_villagers", "villager_start_encounter", "{=ha53qb7v}Don't bother pleading for your lives. At them, lads!", null, null, 100, delegate(out TextObject explanation)
			{
				if (!Hero.MainHero.MapFaction.IsAtWarWith(MobileParty.ConversationParty.MapFaction))
				{
					explanation = new TextObject("{=1LlH1Jof}This action will start a war.", null);
				}
				else
				{
					explanation = null;
				}
				return true;
			}, null);
			starter.AddDialogLine("villager_warn_player_to_take_prisoner", "villager_taken_prisoner_warning", "villager_taken_prisoner_warning_answer", "{=dPOOmYGQ}You think the lords and warriors of the {KINGDOM} won't just stand by idly when their people are kidnapped? You'd best let us go!", new ConversationSentence.OnConditionDelegate(this.conversation_warn_player_on_condition), null, 100, null);
			starter.AddDialogLine("villager_warn_player_to_take_prisoner_2", "villager_taken_prisoner_warning", "close_window", "{=BvytaDUJ}Heaven protect us from the likes of you.", null, delegate()
			{
				Campaign.Current.ConversationManager.ConversationEndOneShot += this.conversation_village_farmer_took_prisoner_on_consequence;
			}, 100, null);
			starter.AddPlayerLine("player_decided_to_take_prisoner_continue_2", "villager_taken_prisoner_warning_answer", "close_window", "{=Dfl5WJfN}Enough talking. Now march.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_took_prisoner_on_consequence), 100, null, null);
			starter.AddPlayerLine("player_decided_to_take_prisoner_leave_2", "villager_taken_prisoner_warning_answer", "village_farmer_loot_talk", "{=BNb88lyN}Never mind. Go on your way.", null, null, 100, null, null);
			starter.AddDialogLine("village_farmer_bribery_leave", "village_farmer_end_talk", "close_window", "{=Pa1ZtapI}Okay. Okay then. We're going.", new ConversationSentence.OnConditionDelegate(this.conversation_village_farmer_looted_leave_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_looted_leave_on_consequence), 100, null);
			starter.AddDialogLine("village_farmer_surrender_leave", "village_farmer_end_talk_surrender", "close_window", "{=Pa1ZtapI}Okay. Okay then. We're going.", new ConversationSentence.OnConditionDelegate(this.conversation_village_farmer_looted_leave_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_surrender_leave_on_consequence), 100, null);
			starter.AddDialogLine("village_farmer_surrender_leave", "villager_start_encounter", "close_window", "{=yoWl6w1I}Heaven will avenge us, you butcher!", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_village_farmer_fight_forced_on_consequence), 100, null);
		}

		// Token: 0x0600477E RID: 18302 RVA: 0x00166B88 File Offset: 0x00164D88
		private bool village_farmer_loot_on_clickable_condition(out TextObject explanation)
		{
			if (this._lootedVillagers.ContainsKey(MobileParty.ConversationParty))
			{
				explanation = new TextObject("{=PVPBqy1e}You just looted these people.", null);
				return false;
			}
			int num;
			ItemRoster source;
			this.CalculateConversationPartyBribeAmount(out num, out source);
			bool flag = num > 0;
			bool flag2 = !source.IsEmpty<ItemRosterElement>();
			if (!flag && !flag2)
			{
				explanation = new TextObject("{=pbRwAjUN}They seem to have no valuable goods.", null);
				return false;
			}
			explanation = null;
			return true;
		}

		// Token: 0x0600477F RID: 18303 RVA: 0x00166BE8 File Offset: 0x00164DE8
		private bool village_farmer_talk_start_on_condition()
		{
			PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
			if (PlayerEncounter.Current != null && Campaign.Current.CurrentConversationContext == ConversationContext.PartyEncounter && encounteredParty.IsMobile && encounteredParty.MobileParty.IsVillager)
			{
				VillagerCampaignBehavior.PlayerInteraction playerInteraction = this.GetPlayerInteraction(encounteredParty.MobileParty);
				if (playerInteraction == VillagerCampaignBehavior.PlayerInteraction.None)
				{
					MBTextManager.SetTextVariable("VILLAGE", encounteredParty.MobileParty.HomeSettlement.EncyclopediaLinkWithName, false);
					Settlement settlement;
					if (encounteredParty.MobileParty.HomeSettlement.Village.TradeBound != null)
					{
						settlement = encounteredParty.MobileParty.HomeSettlement.Village.TradeBound;
					}
					else if (encounteredParty.MobileParty.LastVisitedSettlement != null && encounteredParty.MobileParty.LastVisitedSettlement.IsTown)
					{
						settlement = encounteredParty.MobileParty.LastVisitedSettlement;
					}
					else
					{
						settlement = SettlementHelper.FindNearestTownToSettlement(encounteredParty.MobileParty.HomeSettlement, encounteredParty.MobileParty.NavigationCapability, null).Settlement;
					}
					MBTextManager.SetTextVariable("TOWN", settlement.EncyclopediaLinkWithName, false);
					if (encounteredParty.MobileParty.DefaultBehavior == AiBehavior.GoToSettlement && encounteredParty.MobileParty.TargetSettlement.IsTown)
					{
						MBTextManager.SetTextVariable("VILLAGER_STATE", GameTexts.FindText("str_villager_goes_to_town", null), false);
					}
					else
					{
						MBTextManager.SetTextVariable("VILLAGER_STATE", (encounteredParty.MobileParty.PartyTradeGold > 0) ? GameTexts.FindText("str_villager_returns_to_village", null) : GameTexts.FindText("str_looted_villager_returns_to_village", null), false);
					}
					MBTextManager.SetTextVariable("VILLAGER_GREETING", "{=a7NrxcAD}Greetings, my {?PLAYER.GENDER}lady{?}lord{\\?}. {VILLAGER_PARTY_EXPLANATION}. {VILLAGER_STATE}".ToString(), false);
					TextObject text = new TextObject("{=Epm86qnY}We're farmers from the village of {VILLAGE}", null);
					if (encounteredParty.MobileParty.HasNavalNavigationCapability)
					{
						text = new TextObject("{=b4fpZGsv}We're fisherman from the village of {VILLAGE}", null);
					}
					MBTextManager.SetTextVariable("VILLAGER_PARTY_EXPLANATION", text, false);
				}
				else if (playerInteraction == VillagerCampaignBehavior.PlayerInteraction.Hostile)
				{
					MBTextManager.SetTextVariable("VILLAGER_GREETING", "{=L7AN6ybY}What do you want with us now?", false);
				}
				else if (playerInteraction == VillagerCampaignBehavior.PlayerInteraction.Friendly)
				{
					MBTextManager.SetTextVariable("VILLAGER_GREETING", "{=5Mu1cdbc}Greetings, once again. How may we help you?", false);
				}
				if (playerInteraction == VillagerCampaignBehavior.PlayerInteraction.None)
				{
					this.SetPlayerInteraction(encounteredParty.MobileParty, VillagerCampaignBehavior.PlayerInteraction.Friendly);
				}
				return true;
			}
			return false;
		}

		// Token: 0x06004780 RID: 18304 RVA: 0x00166DDB File Offset: 0x00164FDB
		private bool village_farmer_loot_on_condition()
		{
			return MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsVillager && MobileParty.ConversationParty.Party.MapFaction != Hero.MainHero.MapFaction;
		}

		// Token: 0x06004781 RID: 18305 RVA: 0x00166E10 File Offset: 0x00165010
		private void conversation_village_farmer_leave_on_consequence()
		{
			PlayerEncounter.LeaveEncounter = true;
		}

		// Token: 0x06004782 RID: 18306 RVA: 0x00166E18 File Offset: 0x00165018
		private bool village_farmer_buy_products_on_condition()
		{
			bool flag = true;
			PartyBase encounteredParty = PlayerEncounter.EncounteredParty;
			if (!encounteredParty.MobileParty.IsVillager || encounteredParty.ItemRoster.IsEmpty<ItemRosterElement>())
			{
				return false;
			}
			int num = 0;
			for (int i = 0; i < encounteredParty.ItemRoster.Count; i++)
			{
				ItemRosterElement elementCopyAtIndex = encounteredParty.ItemRoster.GetElementCopyAtIndex(i);
				if (elementCopyAtIndex.EquipmentElement.Item.ItemCategory != DefaultItemCategories.PackAnimal)
				{
					int num2 = encounteredParty.MobileParty.HomeSettlement.Village.GetItemPrice(elementCopyAtIndex.EquipmentElement, MobileParty.MainParty, true);
					int num3 = encounteredParty.MobileParty.HomeSettlement.Village.GetItemPrice(elementCopyAtIndex.EquipmentElement, MobileParty.MainParty, true);
					if (MobileParty.MainParty.HasPerk(DefaultPerks.Trade.SilverTongue, true))
					{
						num2 = MathF.Ceiling((float)num2 * (1f - DefaultPerks.Trade.SilverTongue.SecondaryBonus));
						num3 = MathF.Ceiling((float)num3 * (1f - DefaultPerks.Trade.SilverTongue.SecondaryBonus));
					}
					int elementNumber = encounteredParty.ItemRoster.GetElementNumber(i);
					num += num3 * elementNumber;
					MBTextManager.SetTextVariable("NUMBER_OF", elementNumber);
					MBTextManager.SetTextVariable("ITEM", elementCopyAtIndex.EquipmentElement.Item.Name, false);
					MBTextManager.SetTextVariable("AMOUNT", num2);
					if (flag)
					{
						MBTextManager.SetTextVariable("LEFT", GameTexts.FindText("str_number_of_item_and_price", null).ToString(), false);
						flag = false;
					}
					else
					{
						MBTextManager.SetTextVariable("RIGHT", GameTexts.FindText("str_number_of_item_and_price", null).ToString(), false);
						MBTextManager.SetTextVariable("LEFT", GameTexts.FindText("str_LEFT_comma_RIGHT", null).ToString(), false);
					}
				}
			}
			if (Hero.MainHero.Gold >= num && num > 0)
			{
				MBTextManager.SetTextVariable("PRODUCTS", GameTexts.FindText("str_LEFT_ONLY", null).ToString(), false);
				MBTextManager.SetTextVariable("TOTAL_PRICE", num);
				return true;
			}
			return false;
		}

		// Token: 0x06004783 RID: 18307 RVA: 0x0016700C File Offset: 0x0016520C
		private void conversation_player_decided_to_buy_on_consequence()
		{
			if (MobileParty.ConversationParty.IsVillager && MobileParty.ConversationParty.ItemRoster.Count > 0)
			{
				for (int i = MobileParty.ConversationParty.ItemRoster.Count - 1; i >= 0; i--)
				{
					ItemRosterElement elementCopyAtIndex = MobileParty.ConversationParty.ItemRoster.GetElementCopyAtIndex(i);
					if (elementCopyAtIndex.EquipmentElement.Item.ItemCategory != DefaultItemCategories.PackAnimal)
					{
						int itemPrice = MobileParty.ConversationParty.HomeSettlement.Village.GetItemPrice(elementCopyAtIndex.EquipmentElement, MobileParty.MainParty, true);
						int elementNumber = MobileParty.ConversationParty.ItemRoster.GetElementNumber(i);
						int num = itemPrice * elementNumber;
						if (elementNumber > 0)
						{
							GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, num, false);
							MobileParty.ConversationParty.PartyTradeGold += num;
							PartyBase.MainParty.ItemRoster.AddToCounts(MobileParty.ConversationParty.ItemRoster.GetElementCopyAtIndex(i).EquipmentElement, elementNumber);
							MobileParty.ConversationParty.ItemRoster.AddToCounts(MobileParty.ConversationParty.ItemRoster.GetElementCopyAtIndex(i).EquipmentElement, -1 * elementNumber);
						}
					}
				}
			}
			PlayerEncounter.LeaveEncounter = true;
		}

		// Token: 0x06004784 RID: 18308 RVA: 0x00167145 File Offset: 0x00165345
		private bool conversation_village_farmer_not_bribe_on_condition()
		{
			return MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsVillager && !this.IsBribeFeasible();
		}

		// Token: 0x06004785 RID: 18309 RVA: 0x00167165 File Offset: 0x00165365
		private bool conversation_village_farmer_not_surrender_on_condition()
		{
			return MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsVillager && !this.IsSurrenderFeasible();
		}

		// Token: 0x06004786 RID: 18310 RVA: 0x00167185 File Offset: 0x00165385
		private bool conversation_village_farmer_looted_leave_on_condition()
		{
			return MobileParty.ConversationParty != null && MobileParty.ConversationParty.IsVillager;
		}

		// Token: 0x06004787 RID: 18311 RVA: 0x0016719C File Offset: 0x0016539C
		private bool conversation_warn_player_on_condition()
		{
			IFaction mapFaction = MobileParty.ConversationParty.MapFaction;
			MBTextManager.SetTextVariable("KINGDOM", mapFaction.InformalName, false);
			return !MobileParty.MainParty.MapFaction.IsAtWarWith(MobileParty.ConversationParty.MapFaction);
		}

		// Token: 0x06004788 RID: 18312 RVA: 0x001671E4 File Offset: 0x001653E4
		private void conversation_village_farmer_took_prisoner_on_consequence()
		{
			ItemRoster itemRoster = new ItemRoster(PlayerEncounter.EncounteredParty.ItemRoster);
			if (itemRoster.Count > 0)
			{
				InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster>
				{
					{
						PartyBase.MainParty,
						itemRoster
					}
				});
				PlayerEncounter.EncounteredParty.ItemRoster.Clear();
			}
			int partyTradeGold = PlayerEncounter.EncounteredParty.MobileParty.PartyTradeGold;
			if (partyTradeGold > 0)
			{
				GiveGoldAction.ApplyForPartyToCharacter(PlayerEncounter.EncounteredParty, Hero.MainHero, partyTradeGold, false);
			}
			BeHostileAction.ApplyEncounterHostileAction(PartyBase.MainParty, PlayerEncounter.EncounteredParty);
			this.SetPlayerInteraction(PlayerEncounter.EncounteredParty.MobileParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);
			TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
			foreach (TroopRosterElement troopRosterElement in PlayerEncounter.EncounteredParty.MemberRoster.GetTroopRoster())
			{
				troopRoster.AddToCounts(troopRosterElement.Character, troopRosterElement.Number, false, 0, 0, true, -1);
			}
			PartyScreenHelper.OpenScreenAsLoot(TroopRoster.CreateDummyTroopRoster(), troopRoster, PlayerEncounter.EncounteredParty.Name, troopRoster.TotalManCount, null);
			SkillLevelingManager.OnLoot(MobileParty.MainParty, PlayerEncounter.EncounteredParty.MobileParty, itemRoster, false);
			DestroyPartyAction.Apply(MobileParty.MainParty.Party, PlayerEncounter.EncounteredParty.MobileParty);
			PlayerEncounter.LeaveEncounter = true;
		}

		// Token: 0x06004789 RID: 18313 RVA: 0x00167330 File Offset: 0x00165530
		private void conversation_village_farmer_fight_on_consequence()
		{
			this.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);
		}

		// Token: 0x0600478A RID: 18314 RVA: 0x0016733E File Offset: 0x0016553E
		private void conversation_village_farmer_fight_forced_on_consequence()
		{
			this.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);
			BeHostileAction.ApplyEncounterHostileAction(MobileParty.MainParty.Party, MobileParty.ConversationParty.Party);
		}

		// Token: 0x0600478B RID: 18315 RVA: 0x00167368 File Offset: 0x00165568
		private bool conversation_village_farmer_give_goods_on_condition()
		{
			int num;
			ItemRoster itemRoster;
			this.CalculateConversationPartyBribeAmount(out num, out itemRoster);
			bool flag = num > 0;
			bool flag2 = !itemRoster.IsEmpty<ItemRosterElement>();
			if (flag)
			{
				if (flag2)
				{
					TextObject textObject = (itemRoster.Count == 1) ? GameTexts.FindText("str_LEFT_RIGHT", null) : GameTexts.FindText("str_LEFT_comma_RIGHT", null);
					TextObject textObject2 = GameTexts.FindText("str_looted_party_have_money", null);
					textObject2.SetTextVariable("MONEY", num);
					textObject2.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
					textObject2.SetTextVariable("ITEM_LIST", textObject);
					for (int i = 0; i < itemRoster.Count; i++)
					{
						ItemRosterElement elementCopyAtIndex = itemRoster.GetElementCopyAtIndex(i);
						TextObject textObject3 = GameTexts.FindText("str_offered_item_list", null);
						textObject3.SetTextVariable("COUNT", elementCopyAtIndex.Amount);
						textObject3.SetTextVariable("ITEM", elementCopyAtIndex.EquipmentElement.Item.Name);
						textObject.SetTextVariable("LEFT", textObject3);
						if (itemRoster.Count == 1)
						{
							textObject.SetTextVariable("RIGHT", TextObject.GetEmpty());
						}
						else if (itemRoster.Count - 2 > i)
						{
							TextObject textObject4 = GameTexts.FindText("str_LEFT_comma_RIGHT", null);
							textObject.SetTextVariable("RIGHT", textObject4);
							textObject = textObject4;
						}
						else
						{
							TextObject textObject5 = GameTexts.FindText("str_LEFT_ONLY", null);
							textObject.SetTextVariable("RIGHT", textObject5);
							textObject = textObject5;
						}
					}
					MBTextManager.SetTextVariable("TAKE_MONEY_AND_PRODUCT_STRING", textObject2, false);
				}
				else
				{
					TextObject textObject6 = GameTexts.FindText("str_looted_party_have_money_but_no_item", null);
					textObject6.SetTextVariable("MONEY", num);
					textObject6.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
					MBTextManager.SetTextVariable("TAKE_MONEY_AND_PRODUCT_STRING", textObject6, false);
				}
			}
			else if (flag2)
			{
				TextObject textObject7 = (itemRoster.Count == 1) ? GameTexts.FindText("str_LEFT_RIGHT", null) : GameTexts.FindText("str_LEFT_comma_RIGHT", null);
				TextObject textObject8 = GameTexts.FindText("str_looted_party_have_no_money", null);
				textObject8.SetTextVariable("ITEM_LIST", textObject7);
				for (int j = 0; j < itemRoster.Count; j++)
				{
					ItemRosterElement elementCopyAtIndex2 = itemRoster.GetElementCopyAtIndex(j);
					TextObject textObject9 = GameTexts.FindText("str_offered_item_list", null);
					textObject9.SetTextVariable("COUNT", elementCopyAtIndex2.Amount);
					textObject9.SetTextVariable("ITEM", elementCopyAtIndex2.EquipmentElement.Item.Name);
					textObject7.SetTextVariable("LEFT", textObject9);
					if (itemRoster.Count == 1)
					{
						textObject7.SetTextVariable("RIGHT", TextObject.GetEmpty());
					}
					else if (itemRoster.Count - 2 > j)
					{
						TextObject textObject10 = GameTexts.FindText("str_LEFT_comma_RIGHT", null);
						textObject7.SetTextVariable("RIGHT", textObject10);
						textObject7 = textObject10;
					}
					else
					{
						TextObject textObject11 = GameTexts.FindText("str_LEFT_ONLY", null);
						textObject7.SetTextVariable("RIGHT", textObject11);
						textObject7 = textObject11;
					}
				}
				MBTextManager.SetTextVariable("TAKE_MONEY_AND_PRODUCT_STRING", textObject8, false);
			}
			return true;
		}

		// Token: 0x0600478C RID: 18316 RVA: 0x00167658 File Offset: 0x00165858
		private void conversation_village_farmer_looted_leave_on_consequence()
		{
			int amount;
			ItemRoster itemRoster;
			this.CalculateConversationPartyBribeAmount(out amount, out itemRoster);
			GiveGoldAction.ApplyForPartyToCharacter(MobileParty.ConversationParty.Party, Hero.MainHero, amount, false);
			if (!itemRoster.IsEmpty<ItemRosterElement>())
			{
				for (int i = itemRoster.Count - 1; i >= 0; i--)
				{
					PartyBase party = MobileParty.ConversationParty.Party;
					PartyBase party2 = Hero.MainHero.PartyBelongedTo.Party;
					ItemRosterElement itemRosterElement = itemRoster[i];
					GiveItemAction.ApplyForParties(party, party2, itemRosterElement);
				}
			}
			BeHostileAction.ApplyMinorCoercionHostileAction(PartyBase.MainParty, MobileParty.ConversationParty.Party);
			this.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);
			this._lootedVillagers.Add(MobileParty.ConversationParty, CampaignTime.Now);
			SkillLevelingManager.OnLoot(MobileParty.MainParty, MobileParty.ConversationParty, itemRoster, false);
			PlayerEncounter.LeaveEncounter = true;
		}

		// Token: 0x0600478D RID: 18317 RVA: 0x00167714 File Offset: 0x00165914
		private void conversation_village_farmer_surrender_leave_on_consequence()
		{
			ItemRoster itemRoster = new ItemRoster(MobileParty.ConversationParty.ItemRoster);
			if (itemRoster.Count > 0)
			{
				InventoryScreenHelper.OpenScreenAsLoot(new Dictionary<PartyBase, ItemRoster>
				{
					{
						PartyBase.MainParty,
						itemRoster
					}
				});
				MobileParty.ConversationParty.ItemRoster.Clear();
			}
			int partyTradeGold = MobileParty.ConversationParty.PartyTradeGold;
			if (partyTradeGold > 0)
			{
				GiveGoldAction.ApplyForPartyToCharacter(MobileParty.ConversationParty.Party, Hero.MainHero, partyTradeGold, false);
			}
			this.SetPlayerInteraction(MobileParty.ConversationParty, VillagerCampaignBehavior.PlayerInteraction.Hostile);
			BeHostileAction.ApplyMajorCoercionHostileAction(PartyBase.MainParty, MobileParty.ConversationParty.Party);
			this._lootedVillagers.Add(MobileParty.ConversationParty, CampaignTime.Now);
			SkillLevelingManager.OnLoot(MobileParty.MainParty, MobileParty.ConversationParty, itemRoster, false);
			PlayerEncounter.LeaveEncounter = true;
		}

		// Token: 0x0600478E RID: 18318 RVA: 0x001677D4 File Offset: 0x001659D4
		private bool IsBribeFeasible()
		{
			float resultNumber = Campaign.Current.Models.EncounterModel.GetBribeChance(MobileParty.ConversationParty, MobileParty.MainParty).ResultNumber;
			return MobileParty.ConversationParty.Party.RandomFloatWithSeed(3U, 1f) <= resultNumber;
		}

		// Token: 0x0600478F RID: 18319 RVA: 0x00167824 File Offset: 0x00165A24
		private bool IsSurrenderFeasible()
		{
			float surrenderChance = Campaign.Current.Models.EncounterModel.GetSurrenderChance(MobileParty.ConversationParty, MobileParty.MainParty);
			return MobileParty.ConversationParty.Party.RandomFloatWithSeed(4U, 1f) <= surrenderChance;
		}

		// Token: 0x06004790 RID: 18320 RVA: 0x0016786C File Offset: 0x00165A6C
		private void CalculateConversationPartyBribeAmount(out int gold, out ItemRoster items)
		{
			int num = 0;
			ItemRoster itemRoster = new ItemRoster();
			bool flag = false;
			for (int i = 0; i < MobileParty.ConversationParty.ItemRoster.Count; i++)
			{
				num += MobileParty.ConversationParty.ItemRoster.GetElementUnitCost(i) * MobileParty.ConversationParty.ItemRoster.GetElementNumber(i);
				if (!flag && MobileParty.ConversationParty.ItemRoster.GetElementNumber(i) > 0)
				{
					flag = true;
				}
			}
			num += MobileParty.ConversationParty.PartyTradeGold;
			int num2 = MathF.Min((int)((float)num * 0.2f), 2000);
			if (MobileParty.MainParty.HasPerk(DefaultPerks.Roguery.SaltTheEarth, false))
			{
				num2 = MathF.Round((float)num2 * (1f + DefaultPerks.Roguery.SaltTheEarth.PrimaryBonus));
			}
			int num3 = MathF.Min(MobileParty.ConversationParty.PartyTradeGold, num2);
			if (num3 < num2 && flag)
			{
				ItemRoster itemRoster2 = new ItemRoster(MobileParty.ConversationParty.ItemRoster);
				int num4 = 0;
				while (num3 + num4 < num2)
				{
					ItemRosterElement randomElement = itemRoster2.GetRandomElement<ItemRosterElement>();
					num4 += randomElement.EquipmentElement.ItemValue;
					EquipmentElement rosterElement = new EquipmentElement(randomElement.EquipmentElement.Item, randomElement.EquipmentElement.ItemModifier, null, false);
					itemRoster.AddToCounts(rosterElement, 1);
					itemRoster2.AddToCounts(rosterElement, -1);
					if (itemRoster2.IsEmpty<ItemRosterElement>())
					{
						break;
					}
				}
			}
			gold = num3;
			items = itemRoster;
		}

		// Token: 0x040013F1 RID: 5105
		private float _collectFoodTotalWaitHours;

		// Token: 0x040013F2 RID: 5106
		private float _collectVolunteersTotalWaitHours;

		// Token: 0x040013F3 RID: 5107
		private float _collectFoodWaitHoursProgress;

		// Token: 0x040013F4 RID: 5108
		private float _collectVolunteerWaitHoursProgress;

		// Token: 0x040013F5 RID: 5109
		private Dictionary<MobileParty, CampaignTime> _lootedVillagers = new Dictionary<MobileParty, CampaignTime>();

		// Token: 0x040013F6 RID: 5110
		private Dictionary<MobileParty, VillagerCampaignBehavior.PlayerInteraction> _interactedVillagers = new Dictionary<MobileParty, VillagerCampaignBehavior.PlayerInteraction>();

		// Token: 0x040013F7 RID: 5111
		private Dictionary<Village, CampaignTime> _villageLastVillagerSendTime = new Dictionary<Village, CampaignTime>();

		// Token: 0x0200086E RID: 2158
		public class VillagerCampaignBehaviorTypeDefiner : SaveableTypeDefiner
		{
			// Token: 0x06006869 RID: 26729 RVA: 0x001CA7C8 File Offset: 0x001C89C8
			public VillagerCampaignBehaviorTypeDefiner() : base(140000)
			{
			}

			// Token: 0x0600686A RID: 26730 RVA: 0x001CA7D5 File Offset: 0x001C89D5
			protected override void DefineEnumTypes()
			{
				base.AddEnumDefinition(typeof(VillagerCampaignBehavior.PlayerInteraction), 1, null);
			}

			// Token: 0x0600686B RID: 26731 RVA: 0x001CA7E9 File Offset: 0x001C89E9
			protected override void DefineContainerDefinitions()
			{
				base.ConstructContainerDefinition(typeof(Dictionary<MobileParty, VillagerCampaignBehavior.PlayerInteraction>));
			}
		}

		// Token: 0x0200086F RID: 2159
		private enum PlayerInteraction
		{
			// Token: 0x04002436 RID: 9270
			None,
			// Token: 0x04002437 RID: 9271
			Friendly,
			// Token: 0x04002438 RID: 9272
			TradedWith,
			// Token: 0x04002439 RID: 9273
			Hostile
		}
	}
}
