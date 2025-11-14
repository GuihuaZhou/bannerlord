using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x02000441 RID: 1089
	public class SiegeEventCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x17000E25 RID: 3621
		// (get) Token: 0x06004502 RID: 17666 RVA: 0x001564D8 File Offset: 0x001546D8
		private TextObject _currentSiegeDescription
		{
			get
			{
				if (PlayerSiege.PlayerSiegeEvent == null)
				{
					return TextObject.GetEmpty();
				}
				TextObject textObject = (PlayerSiege.PlayerSide == BattleSideEnum.Attacker) ? this._attackerSummaryText : this._defenderSummaryText;
				Settlement settlement = PlayerEncounter.EncounterSettlement ?? PlayerSiege.PlayerSiegeEvent.BesiegedSettlement;
				textObject.SetTextVariable("SETTLEMENT", settlement.Name);
				Hero leaderOfSiegeEvent = Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(PlayerSiege.PlayerSiegeEvent, PlayerSiege.PlayerSide);
				if (leaderOfSiegeEvent == Hero.MainHero)
				{
					TextObject variable = (PlayerSiege.PlayerSide == BattleSideEnum.Attacker) ? new TextObject("{=0DpoSNky}You are commanding the besiegers.", null) : new TextObject("{=W0FR7yy0}You are commanding the defenders.", null);
					textObject.SetTextVariable("FURTHER_EXPLANATION", variable);
				}
				else if (leaderOfSiegeEvent != null)
				{
					TextObject textObject2 = (PlayerSiege.PlayerSide == BattleSideEnum.Attacker) ? new TextObject("{=d2spYiHG}{LEADER.NAME} is commanding the besiegers.", null) : new TextObject("{=Ja8dMYHi}{LEADER.NAME} is commanding the defenders.", null);
					StringHelpers.SetCharacterProperties("LEADER", leaderOfSiegeEvent.CharacterObject, textObject2, false);
					textObject.SetTextVariable("FURTHER_EXPLANATION", textObject2);
				}
				else
				{
					textObject.SetTextVariable("FURTHER_EXPLANATION", TextObject.GetEmpty());
				}
				return textObject;
			}
		}

		// Token: 0x06004503 RID: 17667 RVA: 0x001565E4 File Offset: 0x001547E4
		public override void RegisterEvents()
		{
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
			CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, new Action<SiegeEvent>(this.OnSiegeEventStarted));
			CampaignEvents.SiegeEngineBuiltEvent.AddNonSerializedListener(this, new Action<SiegeEvent, BattleSideEnum, SiegeEngineType>(this.OnSiegeEngineBuilt));
			CampaignEvents.OnSiegeEngineDestroyedEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement, BattleSideEnum, SiegeEngineType>(this.OnSiegeEngineDestroyed));
			CampaignEvents.OnSiegeBombardmentHitEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, SiegeBombardTargets>(this.OnSiegeEngineHit));
			CampaignEvents.OnSiegeBombardmentWallHitEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, bool>(this.OnSiegeBombardmentWallHit));
			CampaignEvents.MakePeace.AddNonSerializedListener(this, new Action<IFaction, IFaction, MakePeaceAction.MakePeaceDetail>(this.OnPeaceDeclared));
			CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement>(this.OnSettlementLeft));
		}

		// Token: 0x06004504 RID: 17668 RVA: 0x001566AC File Offset: 0x001548AC
		private void OnPeaceDeclared(IFaction faction1, IFaction faction2, MakePeaceAction.MakePeaceDetail detail)
		{
			if (Campaign.Current.CurrentMenuContext != null && Game.Current.GameStateManager.ActiveState != null && Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu() == "menu_siege_strategies")
			{
				Campaign.Current.CurrentMenuContext.Refresh();
			}
		}

		// Token: 0x06004505 RID: 17669 RVA: 0x00156705 File Offset: 0x00154905
		private void OnSettlementLeft(MobileParty party, Settlement settlement)
		{
			if (settlement.SiegeEvent != null && party == MobileParty.MainParty)
			{
				this.SetDefaultTactics(settlement.SiegeEvent, BattleSideEnum.Defender);
			}
		}

		// Token: 0x06004506 RID: 17670 RVA: 0x00156724 File Offset: 0x00154924
		private void OnSiegeBombardmentWallHit(MobileParty party, Settlement settlement, BattleSideEnum battleSide, SiegeEngineType siegeEngine, bool isWallCracked)
		{
			if (isWallCracked && party != null)
			{
				SkillLevelingManager.OnWallBreached(party);
			}
		}

		// Token: 0x06004507 RID: 17671 RVA: 0x00156733 File Offset: 0x00154933
		private void OnSiegeEngineHit(MobileParty party, Settlement settlement, BattleSideEnum side, SiegeEngineType engine, SiegeBombardTargets target)
		{
			if (target == SiegeBombardTargets.RangedEngines)
			{
				this.BombardHitEngineCasualties(settlement.SiegeEvent.GetSiegeEventSide(side), engine);
			}
		}

		// Token: 0x06004508 RID: 17672 RVA: 0x00156750 File Offset: 0x00154950
		private void OnSiegeEngineDestroyed(MobileParty besiegerParty, Settlement besiegedSettlement, BattleSideEnum lostSide, SiegeEngineType siegeEngine)
		{
			SiegeEventModel siegeEventModel = Campaign.Current.Models.SiegeEventModel;
			SiegeEvent siegeEvent = besiegedSettlement.SiegeEvent;
			MobileParty effectiveSiegePartyForSide = siegeEventModel.GetEffectiveSiegePartyForSide(siegeEvent, lostSide);
			MobileParty effectiveSiegePartyForSide2 = siegeEventModel.GetEffectiveSiegePartyForSide(siegeEvent, lostSide.GetOppositeSide());
			if (effectiveSiegePartyForSide2 != null)
			{
				SkillLevelingManager.OnSiegeEngineDestroyed(effectiveSiegePartyForSide2, siegeEngine);
			}
			float casualtyChance = Campaign.Current.Models.SiegeEventModel.GetCasualtyChance(effectiveSiegePartyForSide, siegeEvent, lostSide);
			if (MBRandom.RandomFloat <= casualtyChance)
			{
				ISiegeEventSide siegeEventSide = siegeEvent.GetSiegeEventSide(lostSide);
				int num = siegeEventModel.GetSiegeEngineDestructionCasualties(siegeEvent, siegeEventSide.BattleSide, siegeEngine);
				BattleSideEnum oppositeSide = siegeEventSide.BattleSide.GetOppositeSide();
				if (effectiveSiegePartyForSide2 != null && oppositeSide == BattleSideEnum.Attacker && effectiveSiegePartyForSide2.HasPerk(DefaultPerks.Tactics.PickThemOfTheWalls, false) && MBRandom.RandomFloat < DefaultPerks.Tactics.PickThemOfTheWalls.PrimaryBonus)
				{
					num *= 2;
				}
				if (oppositeSide == BattleSideEnum.Defender)
				{
					Hero governor = siegeEvent.BesiegedSettlement.Town.Governor;
					if (governor != null && governor.GetPerkValue(DefaultPerks.Tactics.PickThemOfTheWalls) && MBRandom.RandomFloat < DefaultPerks.Tactics.PickThemOfTheWalls.SecondaryBonus)
					{
						num *= 2;
					}
				}
				this.KillRandomTroopsOfEnemy(siegeEventSide, num);
			}
		}

		// Token: 0x06004509 RID: 17673 RVA: 0x0015685C File Offset: 0x00154A5C
		private void OnSiegeEngineBuilt(SiegeEvent siegeEvent, BattleSideEnum side, SiegeEngineType siegeEngineType)
		{
			MobileParty effectiveSiegePartyForSide = Campaign.Current.Models.SiegeEventModel.GetEffectiveSiegePartyForSide(siegeEvent, side);
			if (effectiveSiegePartyForSide != null)
			{
				SkillLevelingManager.OnSiegeEngineBuilt(effectiveSiegePartyForSide, siegeEngineType);
				if (effectiveSiegePartyForSide.HasPerk(DefaultPerks.Engineering.Apprenticeship, false))
				{
					for (int i = 0; i < effectiveSiegePartyForSide.MemberRoster.Count; i++)
					{
						CharacterObject characterAtIndex = effectiveSiegePartyForSide.MemberRoster.GetCharacterAtIndex(i);
						if (!characterAtIndex.IsHero)
						{
							int elementNumber = effectiveSiegePartyForSide.MemberRoster.GetElementNumber(i);
							effectiveSiegePartyForSide.MemberRoster.AddXpToTroop(characterAtIndex, elementNumber * (int)DefaultPerks.Engineering.Apprenticeship.PrimaryBonus);
						}
					}
				}
			}
		}

		// Token: 0x0600450A RID: 17674 RVA: 0x001568EC File Offset: 0x00154AEC
		private int KillRandomTroopsOfEnemy(ISiegeEventSide siegeEventSide, int count)
		{
			IEnumerable<PartyBase> involvedPartiesForEventType = siegeEventSide.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege);
			int num = involvedPartiesForEventType.Sum((PartyBase p) => p.NumberOfRegularMembers);
			if (num == 0 || count == 0)
			{
				return 0;
			}
			int num2 = 0;
			int num3 = MBRandom.RandomInt(involvedPartiesForEventType.Count<PartyBase>() - 1);
			for (int i = 0; i < involvedPartiesForEventType.Count<PartyBase>(); i++)
			{
				PartyBase partyBase = involvedPartiesForEventType.ElementAt((i + num3) % involvedPartiesForEventType.Count<PartyBase>());
				float siegeBombardmentHitSurgeryChance = Campaign.Current.Models.PartyHealingModel.GetSiegeBombardmentHitSurgeryChance(partyBase);
				float num4 = (float)partyBase.NumberOfRegularMembers / (float)num;
				float randomFloat = MBRandom.RandomFloat;
				int num5 = MathF.Min(MBRandom.RoundRandomized((float)(count - num2) * (num4 + randomFloat)), count);
				int num6 = partyBase.MemberRoster.TotalRegulars - partyBase.MemberRoster.TotalWoundedRegulars;
				if (num5 > num6)
				{
					num5 = num6;
				}
				if (num5 > 0)
				{
					int num7 = MathF.Round((float)num5 * siegeBombardmentHitSurgeryChance);
					num2 += num5;
					num5 -= num7;
					siegeEventSide.OnTroopsKilledOnSide(num5);
					partyBase.MemberRoster.RemoveNumberOfNonHeroTroopsRandomly(num5);
					if (num7 > 0)
					{
						partyBase.MemberRoster.WoundNumberOfNonHeroTroopsRandomly(num7);
					}
				}
				if (num2 >= count)
				{
					break;
				}
			}
			return num2;
		}

		// Token: 0x0600450B RID: 17675 RVA: 0x00156A24 File Offset: 0x00154C24
		private void BombardHitEngineCasualties(ISiegeEventSide siegeEventSide, SiegeEngineType attackerEngineType)
		{
			SiegeEvent siegeEvent = siegeEventSide.SiegeEvent;
			Settlement besiegedSettlement = siegeEvent.BesiegedSettlement;
			BesiegerCamp besiegerCamp = siegeEvent.BesiegerCamp;
			MobileParty effectiveSiegePartyForSide = Campaign.Current.Models.SiegeEventModel.GetEffectiveSiegePartyForSide(siegeEvent, siegeEventSide.BattleSide);
			ISiegeEventSide siegeEventSide2 = siegeEvent.GetSiegeEventSide(siegeEventSide.BattleSide.GetOppositeSide());
			float siegeEngineHitChance = Campaign.Current.Models.SiegeEventModel.GetSiegeEngineHitChance(attackerEngineType, siegeEventSide.BattleSide, SiegeBombardTargets.People, besiegedSettlement.Town);
			if (MBRandom.RandomFloat < siegeEngineHitChance)
			{
				int colleteralDamageCasualties = Campaign.Current.Models.SiegeEventModel.GetColleteralDamageCasualties(attackerEngineType, effectiveSiegePartyForSide);
				if (this.KillRandomTroopsOfEnemy(siegeEventSide2, colleteralDamageCasualties) > 0)
				{
					CampaignEventDispatcher.Instance.OnSiegeBombardmentHit(besiegerCamp.LeaderParty, besiegedSettlement, siegeEventSide.BattleSide, attackerEngineType, SiegeBombardTargets.People);
				}
			}
		}

		// Token: 0x0600450C RID: 17676 RVA: 0x00156AE0 File Offset: 0x00154CE0
		public override void SyncData(IDataStore dataStore)
		{
		}

		// Token: 0x0600450D RID: 17677 RVA: 0x00156AE2 File Offset: 0x00154CE2
		public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
		{
			this.AddGameMenus(campaignGameStarter);
		}

		// Token: 0x0600450E RID: 17678 RVA: 0x00156AEB File Offset: 0x00154CEB
		private void OnSiegeEventStarted(SiegeEvent siegeEvent)
		{
			this.SetDefaultTactics(siegeEvent, BattleSideEnum.Attacker);
			this.SetDefaultTactics(siegeEvent, BattleSideEnum.Defender);
		}

		// Token: 0x0600450F RID: 17679 RVA: 0x00156B00 File Offset: 0x00154D00
		protected void AddGameMenus(CampaignGameStarter campaignGameSystemStarter)
		{
			campaignGameSystemStarter.AddWaitGameMenu("menu_siege_strategies", "{=!}{CURRENT_STRATEGY}", new OnInitDelegate(this.game_menu_siege_strategies_on_init), null, null, new OnTickDelegate(this.game_menu_siege_strategies_on_tick), GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption, GameMenu.MenuOverlayType.Encounter, 0f, GameMenu.MenuFlags.None, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_strategies", "menu_siege_strategies_lead_assault", "{=mjOcwUSA}Lead an assault", new GameMenuOption.OnConditionDelegate(SiegeEventCampaignBehavior.game_menu_siege_strategies_lead_assault_on_condition), new GameMenuOption.OnConsequenceDelegate(SiegeEventCampaignBehavior.menu_siege_strategies_lead_assault_on_consequence), false, -1, false, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_strategies", "menu_siege_strategies_order_troops", "{=TtGJqRI5}Send troops", new GameMenuOption.OnConditionDelegate(SiegeEventCampaignBehavior.game_menu_siege_strategies_order_assault_on_condition), new GameMenuOption.OnConsequenceDelegate(SiegeEventCampaignBehavior.menu_order_an_assault_on_consequence), false, -1, false, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_strategies", "menu_siege_strategies_request_parley", "{=2xVbLS5r}Request a parley", new GameMenuOption.OnConditionDelegate(SiegeEventCampaignBehavior.menu_defender_side_request_audience_on_condition), new GameMenuOption.OnConsequenceDelegate(SiegeEventCampaignBehavior.menu_defender_side_request_audience_on_consequence), false, -1, false, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_strategies", "menu_siege_strategies_leave", "{=3sRdGQou}Leave", new GameMenuOption.OnConditionDelegate(this.menu_siege_leave_on_condition), new GameMenuOption.OnConsequenceDelegate(SiegeEventCampaignBehavior.menu_siege_leave_on_consequence), true, 10, false, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_strategies", "menu_siege_strategies_leave_army", "{=hSdJ0UUv}Leave Army", new GameMenuOption.OnConditionDelegate(this.menu_siege_strategies_passive_wait_leave_on_condition), new GameMenuOption.OnConsequenceDelegate(this.menu_siege_strategies_passive_wait_leave_on_consequence), true, -1, false, null);
			campaignGameSystemStarter.AddGameMenu("menu_siege_strategies_break_siege", "{=!}{SIEGE_LEAVE_TEXT}", new OnInitDelegate(this.menu_break_siege_on_init), GameMenu.MenuOverlayType.Encounter, GameMenu.MenuFlags.None, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_strategies_break_siege", "menu_siege_strategies_break_siege_return", "{=25ifdWOy}Return to siege", new GameMenuOption.OnConditionDelegate(this.return_siege_on_condition), delegate(MenuCallbackArgs args)
			{
				GameMenu.SwitchToMenu("menu_siege_strategies");
			}, false, -1, false, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_strategies_break_siege", "menu_siege_strategies_break_siege_go_on", "{=TGYJUUn0}Go on.", new GameMenuOption.OnConditionDelegate(this.leave_siege_on_condition), new GameMenuOption.OnConsequenceDelegate(SiegeEventCampaignBehavior.menu_end_besieging_on_consequence), true, -1, false, null);
			campaignGameSystemStarter.AddGameMenu("menu_siege_safe_passage_accepted", "Besiegers have agreed to allow safe passage for your party.", null, GameMenu.MenuOverlayType.None, GameMenu.MenuFlags.None, null);
			campaignGameSystemStarter.AddGameMenuOption("menu_siege_safe_passage_accepted", "menu_siege_safe_passage_accepted_leave", "Leave", new GameMenuOption.OnConditionDelegate(this.leave_siege_on_condition), new GameMenuOption.OnConsequenceDelegate(SiegeEventCampaignBehavior.menu_siege_leave_on_consequence), true, -1, false, null);
		}

		// Token: 0x06004510 RID: 17680 RVA: 0x00156D10 File Offset: 0x00154F10
		private void game_menu_siege_strategies_on_tick(MenuCallbackArgs args, CampaignTime dt)
		{
			string genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
			if (!(genericStateMenu != "menu_siege_strategies"))
			{
				args.MenuContext.GameMenu.GetText().SetTextVariable("CURRENT_STRATEGY", this._currentSiegeDescription);
				Campaign.Current.GameMenuManager.RefreshMenuOptionConditions(args.MenuContext);
				return;
			}
			if (!string.IsNullOrEmpty(genericStateMenu))
			{
				GameMenu.SwitchToMenu(genericStateMenu);
				return;
			}
			GameMenu.ExitToLast();
		}

		// Token: 0x06004511 RID: 17681 RVA: 0x00156D8A File Offset: 0x00154F8A
		private void game_menu_siege_strategies_on_init(MenuCallbackArgs args)
		{
			MBTextManager.SetTextVariable("CURRENT_STRATEGY", this._currentSiegeDescription, false);
		}

		// Token: 0x06004512 RID: 17682 RVA: 0x00156D9D File Offset: 0x00154F9D
		private static void menu_siege_strategies_lead_assault_on_consequence(MenuCallbackArgs args)
		{
			if (PlayerEncounter.IsActive)
			{
				PlayerEncounter.LeaveEncounter = false;
			}
			else
			{
				EncounterManager.StartSettlementEncounter(MobileParty.MainParty, PlayerSiege.PlayerSiegeEvent.BesiegedSettlement);
			}
			GameMenu.SwitchToMenu("assault_town");
		}

		// Token: 0x06004513 RID: 17683 RVA: 0x00156DCC File Offset: 0x00154FCC
		private static void menu_order_an_assault_on_consequence(MenuCallbackArgs args)
		{
			if (PlayerEncounter.IsActive)
			{
				PlayerEncounter.LeaveEncounter = false;
			}
			else
			{
				PlayerEncounter.Start();
				PlayerEncounter.Current.SetupFields(PartyBase.MainParty, PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.Party);
			}
			GameMenu.SwitchToMenu("assault_town_order_attack");
		}

		// Token: 0x06004514 RID: 17684 RVA: 0x00156E0C File Offset: 0x0015500C
		private bool menu_siege_leave_on_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.Leave;
			return (MobileParty.MainParty.Army == null || MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty) && ((PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSide == BattleSideEnum.Defender && !MobileParty.MainParty.MapFaction.IsAtWarWith(PlayerSiege.PlayerSiegeEvent.BesiegerCamp.MapFaction)) || (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSide == BattleSideEnum.Attacker));
		}

		// Token: 0x06004515 RID: 17685 RVA: 0x00156E88 File Offset: 0x00155088
		private bool menu_siege_strategies_passive_wait_leave_on_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.Leave;
			return PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSide == BattleSideEnum.Attacker && MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty;
		}

		// Token: 0x06004516 RID: 17686 RVA: 0x00156ED8 File Offset: 0x001550D8
		private void menu_break_siege_on_init(MenuCallbackArgs args)
		{
			if (MobileParty.MainParty.SiegeEvent.BesiegerCamp.LeaderParty == MobileParty.MainParty)
			{
				MBTextManager.SetTextVariable("SIEGE_LEAVE_TEXT", this._removeSiegeCompletelyText, false);
			}
			else
			{
				MBTextManager.SetTextVariable("SIEGE_LEAVE_TEXT", this._leaveSiegeText, false);
			}
			Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
		}

		// Token: 0x06004517 RID: 17687 RVA: 0x00156F2F File Offset: 0x0015512F
		private bool return_siege_on_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.Continue;
			return true;
		}

		// Token: 0x06004518 RID: 17688 RVA: 0x00156F3A File Offset: 0x0015513A
		private bool leave_siege_on_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.Leave;
			return true;
		}

		// Token: 0x06004519 RID: 17689 RVA: 0x00156F45 File Offset: 0x00155145
		private void menu_siege_strategies_passive_wait_leave_on_consequence(MenuCallbackArgs args)
		{
			GameMenu.ExitToLast();
			if (PlayerSiege.PlayerSiegeEvent != null)
			{
				PlayerSiege.FinalizePlayerSiege();
			}
			MobileParty.MainParty.BesiegerCamp = null;
			MobileParty.MainParty.Army = null;
		}

		// Token: 0x0600451A RID: 17690 RVA: 0x00156F70 File Offset: 0x00155170
		private static bool game_menu_siege_strategies_order_assault_on_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.OrderTroopsToAttack;
			if (MobileParty.MainParty.BesiegedSettlement == null)
			{
				return false;
			}
			if (Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(MobileParty.MainParty.BesiegedSettlement.SiegeEvent, PlayerSiege.PlayerSide) == Hero.MainHero)
			{
				Settlement settlement = (PlayerEncounter.EncounteredParty != null) ? PlayerEncounter.EncounteredParty.Settlement : PlayerSiege.PlayerSiegeEvent.BesiegedSettlement;
				if (PlayerSiege.PlayerSide == BattleSideEnum.Attacker && !settlement.SiegeEvent.BesiegerCamp.IsPreparationComplete)
				{
					args.IsEnabled = false;
					args.Tooltip = SiegeEventCampaignBehavior._waitSiegeEquipmentsText;
				}
				else
				{
					bool flag = MobileParty.MainParty.MemberRoster.GetTroopRoster().Any(delegate(TroopRosterElement x)
					{
						if (x.Character.IsHero)
						{
							return x.Character != CharacterObject.PlayerCharacter && !x.Character.HeroObject.IsWounded;
						}
						return x.Number > x.WoundedNumber;
					});
					if (!flag && MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty)
					{
						foreach (MobileParty mobileParty in MobileParty.MainParty.Army.LeaderParty.AttachedParties)
						{
							flag = mobileParty.MemberRoster.GetTroopRoster().Any(delegate(TroopRosterElement x)
							{
								if (x.Character.IsHero)
								{
									return x.Character != CharacterObject.PlayerCharacter && !x.Character.HeroObject.IsWounded;
								}
								return x.Number > x.WoundedNumber;
							});
							if (flag)
							{
								break;
							}
						}
					}
					args.Tooltip = TooltipHelper.GetSendTroopsPowerContextTooltipForSiege();
					if (!flag)
					{
						args.IsEnabled = false;
						args.Tooltip = new TextObject("{=ao9bhAhf}You are not leading any troops", null);
					}
					else if (!MobilePartyHelper.CanPartyAttackWithCurrentMorale(MobileParty.MainParty))
					{
						args.Tooltip = new TextObject("{=xnRtINwH}Your men lack the courage to continue the battle without you. (Low Morale)", null);
						args.IsEnabled = false;
					}
				}
			}
			else
			{
				args.IsEnabled = false;
				args.Tooltip = new TextObject("{=1Hd19nq5}You are not in command of this siege.", null);
			}
			return true;
		}

		// Token: 0x0600451B RID: 17691 RVA: 0x0015715C File Offset: 0x0015535C
		private static bool game_menu_siege_strategies_lead_assault_on_condition(MenuCallbackArgs args)
		{
			args.optionLeaveType = GameMenuOption.LeaveType.LeadAssault;
			if (MobileParty.MainParty.BesiegedSettlement == null)
			{
				return false;
			}
			if (Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(MobileParty.MainParty.BesiegedSettlement.SiegeEvent, PlayerSiege.PlayerSide) == Hero.MainHero)
			{
				Settlement settlement = (PlayerEncounter.EncounteredParty != null) ? PlayerEncounter.EncounteredParty.Settlement : PlayerSiege.PlayerSiegeEvent.BesiegedSettlement;
				if (PlayerSiege.PlayerSide == BattleSideEnum.Attacker && !settlement.SiegeEvent.BesiegerCamp.IsPreparationComplete)
				{
					args.IsEnabled = false;
					args.Tooltip = SiegeEventCampaignBehavior._waitSiegeEquipmentsText;
				}
				else if (Hero.MainHero.IsWounded)
				{
					args.IsEnabled = false;
					args.Tooltip = SiegeEventCampaignBehavior._woundedAssaultText;
				}
			}
			else
			{
				args.IsEnabled = false;
				args.Tooltip = SiegeEventCampaignBehavior._noCommandText;
			}
			return true;
		}

		// Token: 0x0600451C RID: 17692 RVA: 0x0015722D File Offset: 0x0015542D
		private static void LeaveSiege()
		{
			MobileParty.MainParty.BesiegerCamp = null;
			if (PlayerEncounter.Current != null)
			{
				PlayerEncounter.Finish(true);
				return;
			}
			GameMenu.ExitToLast();
		}

		// Token: 0x0600451D RID: 17693 RVA: 0x00157250 File Offset: 0x00155450
		private static void menu_siege_leave_on_consequence(MenuCallbackArgs args)
		{
			if (MobileParty.MainParty.BesiegerCamp == null)
			{
				if (PlayerEncounter.Current != null && MobileParty.MainParty.CurrentSettlement != null)
				{
					if (MobileParty.MainParty.Army != null)
					{
						MobileParty.MainParty.Army = null;
					}
					PlayerSiege.FinalizePlayerSiege();
					PlayerEncounter.LeaveSettlement();
					PlayerEncounter.Finish(true);
					return;
				}
				GameMenu.ExitToLast();
				return;
			}
			else
			{
				if (MobileParty.MainParty.BesiegerCamp.LeaderParty == MobileParty.MainParty)
				{
					GameMenu.SwitchToMenu("menu_siege_strategies_break_siege");
					return;
				}
				SiegeEventCampaignBehavior.LeaveSiege();
				return;
			}
		}

		// Token: 0x0600451E RID: 17694 RVA: 0x001572D0 File Offset: 0x001554D0
		private static void menu_end_besieging_on_consequence(MenuCallbackArgs args)
		{
			SiegeEventCampaignBehavior.LeaveSiege();
		}

		// Token: 0x0600451F RID: 17695 RVA: 0x001572D8 File Offset: 0x001554D8
		private static bool menu_defender_side_request_audience_on_condition(MenuCallbackArgs args)
		{
			if (PlayerSiege.PlayerSiegeEvent == null || PlayerSiege.PlayerSide != BattleSideEnum.Defender)
			{
				return false;
			}
			if (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSide == BattleSideEnum.Defender && !MobileParty.MainParty.MapFaction.IsAtWarWith(PlayerSiege.PlayerSiegeEvent.BesiegerCamp.MapFaction))
			{
				return false;
			}
			Settlement settlement = Settlement.CurrentSettlement ?? PlayerSiege.PlayerSiegeEvent.BesiegedSettlement;
			if (settlement.SiegeEvent != null)
			{
				if (!settlement.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Any((PartyBase party) => party.LeaderHero != null && party.MobileParty.IsLordParty))
				{
					args.IsEnabled = false;
					args.Tooltip = new TextObject("{=rO704KOG}There is no one with the authority to talk to you.", null);
				}
			}
			if (PlayerSiege.PlayerSiegeEvent.BesiegerCamp.LeaderParty.MapEvent != null)
			{
				args.IsEnabled = false;
				args.Tooltip = new TextObject("{=1UO0yMBr}You can not parley during an ongoing battle.", null);
			}
			args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
			return true;
		}

		// Token: 0x06004520 RID: 17696 RVA: 0x001573C7 File Offset: 0x001555C7
		private static void menu_defender_side_request_audience_on_consequence(MenuCallbackArgs args)
		{
			GameMenu.SwitchToMenu("request_meeting_with_besiegers");
		}

		// Token: 0x06004521 RID: 17697 RVA: 0x001573D3 File Offset: 0x001555D3
		private void SetTactic(SiegeEvent siegeEvent, BattleSideEnum side, SiegeStrategy strategy)
		{
			siegeEvent.GetSiegeEventSide(side).SetSiegeStrategy(strategy);
		}

		// Token: 0x06004522 RID: 17698 RVA: 0x001573E4 File Offset: 0x001555E4
		private void SetDefaultTactics(SiegeEvent siegeEvent, BattleSideEnum side)
		{
			Hero leaderOfSiegeEvent = Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(siegeEvent, side);
			SiegeStrategy strategy = null;
			if (leaderOfSiegeEvent == Hero.MainHero)
			{
				strategy = DefaultSiegeStrategies.Custom;
			}
			else
			{
				IEnumerable<SiegeStrategy> enumerable = (side == BattleSideEnum.Attacker) ? DefaultSiegeStrategies.AllAttackerStrategies : DefaultSiegeStrategies.AllDefenderStrategies;
				float num = float.MinValue;
				foreach (SiegeStrategy siegeStrategy in enumerable)
				{
					float num2 = Campaign.Current.Models.SiegeEventModel.GetSiegeStrategyScore(siegeEvent, side, siegeStrategy) * (0.5f + 0.5f * MBRandom.RandomFloat);
					if (num2 > num)
					{
						num = num2;
						strategy = siegeStrategy;
					}
				}
			}
			this.SetTactic(siegeEvent, side, strategy);
		}

		// Token: 0x06004523 RID: 17699 RVA: 0x001574A0 File Offset: 0x001556A0
		[GameMenuInitializationHandler("menu_siege_strategies")]
		private static void game_menu_siege_strategies_background_on_init(MenuCallbackArgs args)
		{
			args.MenuContext.SetBackgroundMeshName("wait_besieging");
		}

		// Token: 0x04001345 RID: 4933
		private readonly TextObject _attackerSummaryText = new TextObject("{=sbmWGPYG}You are besieging {SETTLEMENT}. {FURTHER_EXPLANATION}", null);

		// Token: 0x04001346 RID: 4934
		private readonly TextObject _defenderSummaryText = new TextObject("{=l5YipTe3}{SETTLEMENT} is under siege. {FURTHER_EXPLANATION}", null);

		// Token: 0x04001347 RID: 4935
		private readonly TextObject _removeSiegeCompletelyText = new TextObject("{=5ZDCnrDQ}This will end the siege. You cannot take your siege engines with you, and they will be destroyed.", null);

		// Token: 0x04001348 RID: 4936
		private readonly TextObject _leaveSiegeText = new TextObject("{=176K8dcb}You will end the siege if you leave. Are you sure?", null);

		// Token: 0x04001349 RID: 4937
		private static readonly TextObject _waitSiegeEquipmentsText = new TextObject("{=bCuxzp1N}You need to wait for the siege equipment to be prepared.", null);

		// Token: 0x0400134A RID: 4938
		private static readonly TextObject _woundedAssaultText = new TextObject("{=gzYuWR28}You are wounded, and in no condition to lead an assault.", null);

		// Token: 0x0400134B RID: 4939
		private static readonly TextObject _noCommandText = new TextObject("{=1Hd19nq5}You are not in command of this siege.", null);
	}
}
