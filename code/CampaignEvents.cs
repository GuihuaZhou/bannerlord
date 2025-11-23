using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem
{
	// Token: 0x0200003D RID: 61
	public class CampaignEvents : CampaignEventReceiver
	{
		// Token: 0x170000B4 RID: 180
		// (get) Token: 0x0600060F RID: 1551 RVA: 0x00021827 File Offset: 0x0001FA27
		private static CampaignEvents Instance
		{
			get
			{
				return Campaign.Current.CampaignEvents;
			}
		}

		// Token: 0x06000610 RID: 1552 RVA: 0x00021834 File Offset: 0x0001FA34
		public override void RemoveListeners(object obj)
		{
			this._heroLevelledUp.ClearListeners(obj);
			this._onHomeHideoutChangedEvent.ClearListeners(obj);
			this._heroGainedSkill.ClearListeners(obj);
			this._heroRelationChanged.ClearListeners(obj);
			this._questLogAddedEvent.ClearListeners(obj);
			this._issueLogAddedEvent.ClearListeners(obj);
			this._onCharacterCreationIsOverEvent.ClearListeners(obj);
			this._clanChangedKingdom.ClearListeners(obj);
			this._onClanDefected.ClearListeners(obj);
			this._onClanCreatedEvent.ClearListeners(obj);
			this._onHeroJoinedPartyEvent.ClearListeners(obj);
			this._partyAttachedParty.ClearListeners(obj);
			this._nearbyPartyAddedToPlayerMapEvent.ClearListeners(obj);
			this._armyCreated.ClearListeners(obj);
			this._armyGathered.ClearListeners(obj);
			this._armyDispersed.ClearListeners(obj);
			this._villageStateChanged.ClearListeners(obj);
			this._settlementEntered.ClearListeners(obj);
			this._afterSettlementEntered.ClearListeners(obj);
			this._beforeSettlementEntered.ClearListeners(obj);
			this._mercenaryTroopChangedInTown.ClearListeners(obj);
			this._mercenaryNumberChangedInTown.ClearListeners(obj);
			this._alleyOwnerChanged.ClearListeners(obj);
			this._alleyOccupiedByPlayer.ClearListeners(obj);
			this._alleyClearedByPlayer.ClearListeners(obj);
			this._romanticStateChanged.ClearListeners(obj);
			this._warDeclared.ClearListeners(obj);
			this._battleStarted.ClearListeners(obj);
			this._rebellionFinished.ClearListeners(obj);
			this._townRebelliousStateChanged.ClearListeners(obj);
			this._rebelliousClanDisbandedAtSettlement.ClearListeners(obj);
			this._mobilePartyDestroyed.ClearListeners(obj);
			this._mobilePartyCreated.ClearListeners(obj);
			this._mapInteractableCreated.ClearListeners(obj);
			this._mapInteractableDestroyed.ClearListeners(obj);
			this._mobilePartyQuestStatusChanged.ClearListeners(obj);
			this._heroKilled.ClearListeners(obj);
			this._characterDefeated.ClearListeners(obj);
			this._heroPrisonerTaken.ClearListeners(obj);
			this._onPartySizeChangedEvent.ClearListeners(obj);
			this._characterBecameFugitiveEvent.ClearListeners(obj);
			this._playerMetHero.ClearListeners(obj);
			this._playerLearnsAboutHero.ClearListeners(obj);
			this._renownGained.ClearListeners(obj);
			this._barterablesRequested.ClearListeners(obj);
			this._crimeRatingChanged.ClearListeners(obj);
			this._newCompanionAdded.ClearListeners(obj);
			this._afterMissionStarted.ClearListeners(obj);
			this._gameMenuOpened.ClearListeners(obj);
			this._makePeace.ClearListeners(obj);
			this._kingdomCreated.ClearListeners(obj);
			this._kingdomDestroyed.ClearListeners(obj);
			this._canKingdomBeDiscontinued.ClearListeners(obj);
			this._villageBeingRaided.ClearListeners(obj);
			this._villageLooted.ClearListeners(obj);
			this._mapEventEnded.ClearListeners(obj);
			this._mapEventStarted.ClearListeners(obj);
			this._prisonersChangeInSettlement.ClearListeners(obj);
			this._onMissionStartedEvent.ClearListeners(obj);
			this._beforeMissionOpenedEvent.ClearListeners(obj);
			this._onPartyRemovedEvent.ClearListeners(obj);
			this._onPartyLeaderChangedEvent.ClearListeners(obj);
			this._banditPartyRecruited.ClearListeners(obj);
			this._onSettlementOwnerChangedEvent.ClearListeners(obj);
			this._onGovernorChangedEvent.ClearListeners(obj);
			this._onSettlementLeftEvent.ClearListeners(obj);
			this._weeklyTickEvent.ClearListeners(obj);
			this._dailyTickEvent.ClearListeners(obj);
			this._dailyTickPartyEvent.ClearListeners(obj);
			this._hourlyTickEvent.ClearListeners(obj);
			this._tickEvent.ClearListeners(obj);
			this._onSessionLaunchedEvent.ClearListeners(obj);
			this._onAfterSessionLaunchedEvent.ClearListeners(obj);
			this._onNewGameCreatedPartialFollowUpEvent.ClearListeners(obj);
			this._onNewGameCreatedPartialFollowUpEndEvent.ClearListeners(obj);
			this._onNewGameCreatedEvent.ClearListeners(obj);
			this._onGameLoadedEvent.ClearListeners(obj);
			this._onBarterAcceptedEvent.ClearListeners(obj);
			this._onBarterCanceledEvent.ClearListeners(obj);
			this._onGameEarlyLoadedEvent.ClearListeners(obj);
			this._onGameLoadFinishedEvent.ClearListeners(obj);
			this._aiHourlyTickEvent.ClearListeners(obj);
			this._tickPartialHourlyAiEvent.ClearListeners(obj);
			this._onPartyJoinedArmyEvent.ClearListeners(obj);
			this._onPartyRemovedFromArmyEvent.ClearListeners(obj);
			this._onMissionEndedEvent.ClearListeners(obj);
			this._onPlayerBattleEndEvent.ClearListeners(obj);
			this._onPlayerBoardGameOver.ClearListeners(obj);
			this._onRansomOfferedToPlayer.ClearListeners(obj);
			this._onRansomOfferCancelled.ClearListeners(obj);
			this._onPeaceOfferedToPlayer.ClearListeners(obj);
			this._onTradeAgreementSignedEvent.ClearListeners(obj);
			this._onPeaceOfferResolved.ClearListeners(obj);
			this._onMarriageOfferedToPlayerEvent.ClearListeners(obj);
			this._onMarriageOfferCanceledEvent.ClearListeners(obj);
			this._onVassalOrMercenaryServiceOfferedToPlayerEvent.ClearListeners(obj);
			this._onVassalOrMercenaryServiceOfferCanceledEvent.ClearListeners(obj);
			this._afterGameMenuInitializedEvent.ClearListeners(obj);
			this._beforeGameMenuOpenedEvent.ClearListeners(obj);
			this._onChildConceived.ClearListeners(obj);
			this._onGivenBirthEvent.ClearListeners(obj);
			this._missionTickEvent.ClearListeners(obj);
			this._armyOverlaySetDirty.ClearListeners(obj);
			this._onPlayerArmyLeaderChangedBehaviorEvent.ClearListeners(obj);
			this._partyVisibilityChanged.ClearListeners(obj);
			this._onHeroCreated.ClearListeners(obj);
			this._heroOccupationChangedEvent.ClearListeners(obj);
			this._onHeroWounded.ClearListeners(obj);
			this._playerDesertedBattle.ClearListeners(obj);
			this._companionRemoved.ClearListeners(obj);
			this._trackLostEvent.ClearListeners(obj);
			this._trackDetectedEvent.ClearListeners(obj);
			this._locationCharactersAreReadyToSpawn.ClearListeners(obj);
			this._locationCharactersSimulatedSpawned.ClearListeners(obj);
			this._playerUpgradedTroopsEvent.ClearListeners(obj);
			this._onHeroCombatHitEvent.ClearListeners(obj);
			this._characterPortraitPopUpOpenedEvent.ClearListeners(obj);
			this._characterPortraitPopUpClosedEvent.ClearListeners(obj);
			this._playerStartTalkFromMenu.ClearListeners(obj);
			this._gameMenuOptionSelectedEvent.ClearListeners(obj);
			this._playerStartRecruitmentEvent.ClearListeners(obj);
			this._onAgentJoinedConversationEvent.ClearListeners(obj);
			this._onConversationEnded.ClearListeners(obj);
			this._beforeHeroesMarried.ClearListeners(obj);
			this._onTroopsDesertedEvent.ClearListeners(obj);
			this._onBeforePlayerCharacterChangedEvent.ClearListeners(obj);
			this._onPlayerCharacterChangedEvent.ClearListeners(obj);
			this._onClanLeaderChangedEvent.ClearListeners(obj);
			this._onSiegeEventStartedEvent.ClearListeners(obj);
			this._onPlayerSiegeStartedEvent.ClearListeners(obj);
			this._onSiegeEventEndedEvent.ClearListeners(obj);
			this._siegeAftermathAppliedEvent.ClearListeners(obj);
			this._onSiegeBombardmentHitEvent.ClearListeners(obj);
			this._onSiegeBombardmentWallHitEvent.ClearListeners(obj);
			this._onSiegeEngineDestroyedEvent.ClearListeners(obj);
			this._kingdomDecisionAdded.ClearListeners(obj);
			this._kingdomDecisionCancelled.ClearListeners(obj);
			this._kingdomDecisionConcluded.ClearListeners(obj);
			this._childEducationCompleted.ClearListeners(obj);
			this._heroComesOfAge.ClearListeners(obj);
			this._heroGrowsOutOfInfancyEvent.ClearListeners(obj);
			this._heroReachesTeenAgeEvent.ClearListeners(obj);
			this._onCheckForIssueEvent.ClearListeners(obj);
			this._onIssueUpdatedEvent.ClearListeners(obj);
			this._onTroopRecruitedEvent.ClearListeners(obj);
			this._onTroopGivenToSettlementEvent.ClearListeners(obj);
			this._onItemSoldEvent.ClearListeners(obj);
			this._onCaravanTransactionCompletedEvent.ClearListeners(obj);
			this._onPrisonerSoldEvent.ClearListeners(obj);
			this._heroPrisonerReleased.ClearListeners(obj);
			this._heroOrPartyTradedGold.ClearListeners(obj);
			this._heroOrPartyGaveItem.ClearListeners(obj);
			this._perkOpenedEvent.ClearListeners(obj);
			this._playerTraitChangedEvent.ClearListeners(obj);
			this._onPartyDisbandedEvent.ClearListeners(obj);
			this._onPartyDisbandStartedEvent.ClearListeners(obj);
			this._onPartyDisbandCanceledEvent.ClearListeners(obj);
			this._itemsLooted.ClearListeners(obj);
			this._hideoutSpottedEvent.ClearListeners(obj);
			this._hideoutBattleCompletedEvent.ClearListeners(obj);
			this._hideoutDeactivatedEvent.ClearListeners(obj);
			this._heroSharedFoodWithAnotherHeroEvent.ClearListeners(obj);
			this._onQuestCompletedEvent.ClearListeners(obj);
			this._itemProducedEvent.ClearListeners(obj);
			this._itemConsumedEvent.ClearListeners(obj);
			this._onQuestStartedEvent.ClearListeners(obj);
			this._onPartyConsumedFoodEvent.ClearListeners(obj);
			this._siegeCompletedEvent.ClearListeners(obj);
			this._afterSiegeCompletedEvent.ClearListeners(obj);
			this._raidCompletedEvent.ClearListeners(obj);
			this._forceVolunteersCompletedEvent.ClearListeners(obj);
			this._forceSuppliesCompletedEvent.ClearListeners(obj);
			this._onBeforeMainCharacterDiedEvent.ClearListeners(obj);
			this._onGameOverEvent.ClearListeners(obj);
			this._onClanDestroyedEvent.ClearListeners(obj);
			this._onNewIssueCreatedEvent.ClearListeners(obj);
			this._onIssueOwnerChangedEvent.ClearListeners(obj);
			this._onTutorialCompletedEvent.ClearListeners(obj);
			this._collectAvailableTutorialsEvent.ClearListeners(obj);
			this._playerEliminatedFromTournament.ClearListeners(obj);
			this._playerStartedTournamentMatch.ClearListeners(obj);
			this._tournamentStarted.ClearListeners(obj);
			this._tournamentFinished.ClearListeners(obj);
			this._tournamentCancelled.ClearListeners(obj);
			this._playerInventoryExchangeEvent.ClearListeners(obj);
			this._onItemsDiscardedByPlayerEvent.ClearListeners(obj);
			this._onNewItemCraftedEvent.ClearListeners(obj);
			this._craftingPartUnlockedEvent.ClearListeners(obj);
			this._onWorkshopInitializedEvent.ClearListeners(obj);
			this._onWorkshopOwnerChangedEvent.ClearListeners(obj);
			this._onWorkshopTypeChangedEvent.ClearListeners(obj);
			this._persuasionProgressCommittedEvent.ClearListeners(obj);
			this._onBeforeSaveEvent.ClearListeners(obj);
			this._onPrisonerTakenEvent.ClearListeners(obj);
			this._onPrisonerReleasedEvent.ClearListeners(obj);
			this._onMainPartyPrisonerRecruitedEvent.ClearListeners(obj);
			this._onPrisonerDonatedToSettlementEvent.ClearListeners(obj);
			this._onEquipmentSmeltedByHero.ClearListeners(obj);
			this._onPlayerTradeProfit.ClearListeners(obj);
			this._onBeforeHeroKilled.ClearListeners(obj);
			this._onBuildingLevelChangedEvent.ClearListeners(obj);
			this._hourlyTickSettlementEvent.ClearListeners(obj);
			this._hourlyTickClanEvent.ClearListeners(obj);
			this._onUnitRecruitedEvent.ClearListeners(obj);
			this._trackDetectedEvent.ClearListeners(obj);
			this._trackLostEvent.ClearListeners(obj);
			this._onTradeRumorIsTakenEvent.ClearListeners(obj);
			this._siegeEngineBuiltEvent.ClearListeners(obj);
			this._dailyTickHeroEvent.ClearListeners(obj);
			this._dailyTickSettlementEvent.ClearListeners(obj);
			this._hourlyTickPartyEvent.ClearListeners(obj);
			this._dailyTickClanEvent.ClearListeners(obj);
			this._villageBecomeNormal.ClearListeners(obj);
			this._clanTierIncrease.ClearListeners(obj);
			this._dailyTickTownEvent.ClearListeners(obj);
			this._onHeroChangedClan.ClearListeners(obj);
			this._onHeroGetsBusy.ClearListeners(obj);
			this._onSaveStartedEvent.ClearListeners(obj);
			this._onSaveOverEvent.ClearListeners(obj);
			this._onPlayerBodyPropertiesChangedEvent.ClearListeners(obj);
			this._rulingClanChanged.ClearListeners(obj);
			this._onCollectLootItems.ClearListeners(obj);
			this._onLootDistributedToPartyEvent.ClearListeners(obj);
			this._onHeroTeleportationRequestedEvent.ClearListeners(obj);
			this._onPartyLeaderChangeOfferCanceledEvent.ClearListeners(obj);
			this._canBeGovernorOrHavePartyRoleEvent.ClearListeners(obj);
			this._canHeroLeadPartyEvent.ClearListeners(obj);
			this._canMarryEvent.ClearListeners(obj);
			this._canHeroDieEvent.ClearListeners(obj);
			this._canHeroBecomePrisonerEvent.ClearListeners(obj);
			this._canHeroEquipmentBeChangedEvent.ClearListeners(obj);
			this._canHaveCampaignIssues.ClearListeners(obj);
			this._isSettlementBusy.ClearListeners(obj);
			this._canMoveToSettlementEvent.ClearListeners(obj);
			this._onQuarterDailyPartyTick.ClearListeners(obj);
			this._onMainPartyStarving.ClearListeners(obj);
			this._onClanInfluenceChangedEvent.ClearListeners(obj);
			this._onPlayerPartyKnockedOrKilledTroopEvent.ClearListeners(obj);
			this._onPlayerEarnedGoldFromAssetEvent.ClearListeners(obj);
			this._onClanEarnedGoldFromTributeEvent.ClearListeners(obj);
			this._onPlayerJoinedTournamentEvent.ClearListeners(obj);
			this._onHeroUnregisteredEvent.ClearListeners(obj);
			this._onConfigChanged.ClearListeners(obj);
			this._onCraftingOrderCompleted.ClearListeners(obj);
			this._onItemsRefined.ClearListeners(obj);
			this._onMapEventContinuityNeedsUpdate.ClearListeners(obj);
			this._onPlayerAgentSpawned.ClearListeners(obj);
			this._onBeforePlayerAgentSpawn.ClearListeners(obj);
			this._perkResetEvent.ClearListeners(obj);
			this._onHeirSelectionOver.ClearListeners(obj);
			this._onMobilePartyRaftStateChanged.ClearListeners(obj);
			this._onCharacterCreationInitialized.ClearListeners(obj);
			this._onShipDestroyedEvent.ClearListeners(obj);
			this._onShipRepairedEvent.ClearListeners(obj);
			this._onShipCreatedEvent.ClearListeners(obj);
			this._onPartyLeftArmyEvent.ClearListeners(obj);
			this._onPartyAddedToMapEventEvent.ClearListeners(obj);
			this._onIncidentResolvedEvent.ClearListeners(obj);
			this._onFigureheadUnlockedEvent.ClearListeners(obj);
			this._onShipOwnerChangedEvent.ClearListeners(obj);
			this._onMobilePartyNavigationStateChangedEvent.ClearListeners(obj);
			this._onMobilePartyJoinedToSiegeEventEvent.ClearListeners(obj);
			this._onMobilePartyLeftSiegeEventEvent.ClearListeners(obj);
			this._onBlockadeActivatedEvent.ClearListeners(obj);
			this._onBlockadeDeactivatedEvent.ClearListeners(obj);
			this._onMercenaryServiceStartedEvent.ClearListeners(obj);
			this._onMercenaryServiceEndedEvent.ClearListeners(obj);
			this._canPlayerMeetWithHeroAfterConversationEvent.ClearListeners(obj);
			this._onMapMarkerCreatedEvent.ClearListeners(obj);
			this._onMapMarkerRemovedEvent.ClearListeners(obj);
			this._onAllianceStartedEvent.ClearListeners(obj);
			this._onAllianceEndedEvent.ClearListeners(obj);
			this._onCallToWarAgreementStartedEvent.ClearListeners(obj);
			this._onCallToWarAgreementEndedEvent.ClearListeners(obj);
		}

		// Token: 0x170000B5 RID: 181
		// (get) Token: 0x06000611 RID: 1553 RVA: 0x00022519 File Offset: 0x00020719
		public static IMbEvent OnPlayerBodyPropertiesChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerBodyPropertiesChangedEvent;
			}
		}

		// Token: 0x06000612 RID: 1554 RVA: 0x00022525 File Offset: 0x00020725
		public override void OnPlayerBodyPropertiesChanged()
		{
			CampaignEvents.Instance._onPlayerBodyPropertiesChangedEvent.Invoke();
		}

		// Token: 0x170000B6 RID: 182
		// (get) Token: 0x06000613 RID: 1555 RVA: 0x00022536 File Offset: 0x00020736
		public static IMbEvent<BarterData> BarterablesRequested
		{
			get
			{
				return CampaignEvents.Instance._barterablesRequested;
			}
		}

		// Token: 0x06000614 RID: 1556 RVA: 0x00022542 File Offset: 0x00020742
		public override void OnBarterablesRequested(BarterData args)
		{
			CampaignEvents.Instance._barterablesRequested.Invoke(args);
		}

		// Token: 0x170000B7 RID: 183
		// (get) Token: 0x06000615 RID: 1557 RVA: 0x00022554 File Offset: 0x00020754
		public static IMbEvent<Hero, bool> HeroLevelledUp
		{
			get
			{
				return CampaignEvents.Instance._heroLevelledUp;
			}
		}

		// Token: 0x06000616 RID: 1558 RVA: 0x00022560 File Offset: 0x00020760
		public override void OnHeroLevelledUp(Hero hero, bool shouldNotify = true)
		{
			this._heroLevelledUp.Invoke(hero, shouldNotify);
		}

		// Token: 0x170000B8 RID: 184
		// (get) Token: 0x06000617 RID: 1559 RVA: 0x0002256F File Offset: 0x0002076F
		public static IMbEvent<BanditPartyComponent, Hideout> OnHomeHideoutChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onHomeHideoutChangedEvent;
			}
		}

		// Token: 0x06000618 RID: 1560 RVA: 0x0002257B File Offset: 0x0002077B
		public override void OnHomeHideoutChanged(BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
		{
			CampaignEvents.Instance._onHomeHideoutChangedEvent.Invoke(banditPartyComponent, oldHomeHideout);
		}

		// Token: 0x170000B9 RID: 185
		// (get) Token: 0x06000619 RID: 1561 RVA: 0x0002258E File Offset: 0x0002078E
		public static IMbEvent<Hero, SkillObject, int, bool> HeroGainedSkill
		{
			get
			{
				return CampaignEvents.Instance._heroGainedSkill;
			}
		}

		// Token: 0x0600061A RID: 1562 RVA: 0x0002259A File Offset: 0x0002079A
		public override void OnHeroGainedSkill(Hero hero, SkillObject skill, int change = 1, bool shouldNotify = true)
		{
			this._heroGainedSkill.Invoke(hero, skill, change, shouldNotify);
		}

		// Token: 0x170000BA RID: 186
		// (get) Token: 0x0600061B RID: 1563 RVA: 0x000225AC File Offset: 0x000207AC
		public static IMbEvent OnCharacterCreationIsOverEvent
		{
			get
			{
				return CampaignEvents.Instance._onCharacterCreationIsOverEvent;
			}
		}

		// Token: 0x0600061C RID: 1564 RVA: 0x000225B8 File Offset: 0x000207B8
		public override void OnCharacterCreationIsOver()
		{
			CampaignEvents.Instance._onCharacterCreationIsOverEvent.Invoke();
		}

		// Token: 0x170000BB RID: 187
		// (get) Token: 0x0600061D RID: 1565 RVA: 0x000225C9 File Offset: 0x000207C9
		public static IMbEvent<Hero, bool> HeroCreated
		{
			get
			{
				return CampaignEvents.Instance._onHeroCreated;
			}
		}

		// Token: 0x0600061E RID: 1566 RVA: 0x000225D5 File Offset: 0x000207D5
		public override void OnHeroCreated(Hero hero, bool isBornNaturally = false)
		{
			this._onHeroCreated.Invoke(hero, isBornNaturally);
		}

		// Token: 0x170000BC RID: 188
		// (get) Token: 0x0600061F RID: 1567 RVA: 0x000225E4 File Offset: 0x000207E4
		public static IMbEvent<Hero, Occupation> HeroOccupationChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._heroOccupationChangedEvent;
			}
		}

		// Token: 0x06000620 RID: 1568 RVA: 0x000225F0 File Offset: 0x000207F0
		public override void OnHeroOccupationChanged(Hero hero, Occupation oldOccupation)
		{
			this._heroOccupationChangedEvent.Invoke(hero, oldOccupation);
		}

		// Token: 0x170000BD RID: 189
		// (get) Token: 0x06000621 RID: 1569 RVA: 0x000225FF File Offset: 0x000207FF
		public static IMbEvent<Hero> HeroWounded
		{
			get
			{
				return CampaignEvents.Instance._onHeroWounded;
			}
		}

		// Token: 0x06000622 RID: 1570 RVA: 0x0002260B File Offset: 0x0002080B
		public override void OnHeroWounded(Hero woundedHero)
		{
			this._onHeroWounded.Invoke(woundedHero);
		}

		// Token: 0x170000BE RID: 190
		// (get) Token: 0x06000623 RID: 1571 RVA: 0x00022619 File Offset: 0x00020819
		public static IMbEvent<Hero, Hero, List<Barterable>> OnBarterAcceptedEvent
		{
			get
			{
				return CampaignEvents.Instance._onBarterAcceptedEvent;
			}
		}

		// Token: 0x06000624 RID: 1572 RVA: 0x00022625 File Offset: 0x00020825
		public override void OnBarterAccepted(Hero offererHero, Hero otherHero, List<Barterable> barters)
		{
			CampaignEvents.Instance._onBarterAcceptedEvent.Invoke(offererHero, otherHero, barters);
		}

		// Token: 0x170000BF RID: 191
		// (get) Token: 0x06000625 RID: 1573 RVA: 0x00022639 File Offset: 0x00020839
		public static IMbEvent<Hero, Hero, List<Barterable>> OnBarterCanceledEvent
		{
			get
			{
				return CampaignEvents.Instance._onBarterCanceledEvent;
			}
		}

		// Token: 0x06000626 RID: 1574 RVA: 0x00022645 File Offset: 0x00020845
		public override void OnBarterCanceled(Hero offererHero, Hero otherHero, List<Barterable> barters)
		{
			CampaignEvents.Instance._onBarterCanceledEvent.Invoke(offererHero, otherHero, barters);
		}

		// Token: 0x170000C0 RID: 192
		// (get) Token: 0x06000627 RID: 1575 RVA: 0x00022659 File Offset: 0x00020859
		public static IMbEvent<Hero, Hero, int, bool, ChangeRelationAction.ChangeRelationDetail, Hero, Hero> HeroRelationChanged
		{
			get
			{
				return CampaignEvents.Instance._heroRelationChanged;
			}
		}

		// Token: 0x06000628 RID: 1576 RVA: 0x00022665 File Offset: 0x00020865
		public override void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange, bool showNotification, ChangeRelationAction.ChangeRelationDetail detail, Hero originalHero, Hero originalGainedRelationWith)
		{
			CampaignEvents.Instance._heroRelationChanged.Invoke(effectiveHero, effectiveHeroGainedRelationWith, relationChange, showNotification, detail, originalHero, originalGainedRelationWith);
		}

		// Token: 0x170000C1 RID: 193
		// (get) Token: 0x06000629 RID: 1577 RVA: 0x00022681 File Offset: 0x00020881
		public static IMbEvent<QuestBase, bool> QuestLogAddedEvent
		{
			get
			{
				return CampaignEvents.Instance._questLogAddedEvent;
			}
		}

		// Token: 0x0600062A RID: 1578 RVA: 0x0002268D File Offset: 0x0002088D
		public override void OnQuestLogAdded(QuestBase quest, bool hideInformation)
		{
			CampaignEvents.Instance._questLogAddedEvent.Invoke(quest, hideInformation);
		}

		// Token: 0x170000C2 RID: 194
		// (get) Token: 0x0600062B RID: 1579 RVA: 0x000226A0 File Offset: 0x000208A0
		public static IMbEvent<IssueBase, bool> IssueLogAddedEvent
		{
			get
			{
				return CampaignEvents.Instance._issueLogAddedEvent;
			}
		}

		// Token: 0x0600062C RID: 1580 RVA: 0x000226AC File Offset: 0x000208AC
		public override void OnIssueLogAdded(IssueBase issue, bool hideInformation)
		{
			CampaignEvents.Instance._issueLogAddedEvent.Invoke(issue, hideInformation);
		}

		// Token: 0x170000C3 RID: 195
		// (get) Token: 0x0600062D RID: 1581 RVA: 0x000226BF File Offset: 0x000208BF
		public static IMbEvent<Clan, bool> ClanTierIncrease
		{
			get
			{
				return CampaignEvents.Instance._clanTierIncrease;
			}
		}

		// Token: 0x0600062E RID: 1582 RVA: 0x000226CB File Offset: 0x000208CB
		public override void OnClanTierChanged(Clan clan, bool shouldNotify = true)
		{
			CampaignEvents.Instance._clanTierIncrease.Invoke(clan, shouldNotify);
		}

		// Token: 0x170000C4 RID: 196
		// (get) Token: 0x0600062F RID: 1583 RVA: 0x000226DE File Offset: 0x000208DE
		public static IMbEvent<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool> OnClanChangedKingdomEvent
		{
			get
			{
				return CampaignEvents.Instance._clanChangedKingdom;
			}
		}

		// Token: 0x06000630 RID: 1584 RVA: 0x000226EA File Offset: 0x000208EA
		public override void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
		{
			CampaignEvents.Instance._clanChangedKingdom.Invoke(clan, oldKingdom, newKingdom, detail, showNotification);
		}

		// Token: 0x170000C5 RID: 197
		// (get) Token: 0x06000631 RID: 1585 RVA: 0x00022702 File Offset: 0x00020902
		public static IMbEvent<Clan, Kingdom, Kingdom> OnClanDefectedEvent
		{
			get
			{
				return CampaignEvents.Instance._onClanDefected;
			}
		}

		// Token: 0x06000632 RID: 1586 RVA: 0x0002270E File Offset: 0x0002090E
		public override void OnClanDefected(Clan clan, Kingdom oldKingdom, Kingdom newKingdom)
		{
			CampaignEvents.Instance._onClanDefected.Invoke(clan, oldKingdom, newKingdom);
		}

		// Token: 0x170000C6 RID: 198
		// (get) Token: 0x06000633 RID: 1587 RVA: 0x00022722 File Offset: 0x00020922
		public static IMbEvent<Clan, bool> OnClanCreatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onClanCreatedEvent;
			}
		}

		// Token: 0x06000634 RID: 1588 RVA: 0x0002272E File Offset: 0x0002092E
		public override void OnClanCreated(Clan clan, bool isCompanion)
		{
			CampaignEvents.Instance._onClanCreatedEvent.Invoke(clan, isCompanion);
		}

		// Token: 0x170000C7 RID: 199
		// (get) Token: 0x06000635 RID: 1589 RVA: 0x00022741 File Offset: 0x00020941
		public static IMbEvent<Hero, MobileParty> OnHeroJoinedPartyEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeroJoinedPartyEvent;
			}
		}

		// Token: 0x06000636 RID: 1590 RVA: 0x0002274D File Offset: 0x0002094D
		public override void OnHeroJoinedParty(Hero hero, MobileParty mobileParty)
		{
			CampaignEvents.Instance._onHeroJoinedPartyEvent.Invoke(hero, mobileParty);
		}

		// Token: 0x170000C8 RID: 200
		// (get) Token: 0x06000637 RID: 1591 RVA: 0x00022760 File Offset: 0x00020960
		public static IMbEvent<ValueTuple<Hero, PartyBase>, ValueTuple<Hero, PartyBase>, ValueTuple<int, string>, bool> HeroOrPartyTradedGold
		{
			get
			{
				return CampaignEvents.Instance._heroOrPartyTradedGold;
			}
		}

		// Token: 0x06000638 RID: 1592 RVA: 0x0002276C File Offset: 0x0002096C
		public override void OnHeroOrPartyTradedGold(ValueTuple<Hero, PartyBase> giver, ValueTuple<Hero, PartyBase> recipient, ValueTuple<int, string> goldAmount, bool showNotification)
		{
			CampaignEvents.Instance._heroOrPartyTradedGold.Invoke(giver, recipient, goldAmount, showNotification);
		}

		// Token: 0x170000C9 RID: 201
		// (get) Token: 0x06000639 RID: 1593 RVA: 0x00022782 File Offset: 0x00020982
		public static IMbEvent<ValueTuple<Hero, PartyBase>, ValueTuple<Hero, PartyBase>, ItemRosterElement, bool> HeroOrPartyGaveItem
		{
			get
			{
				return CampaignEvents.Instance._heroOrPartyGaveItem;
			}
		}

		// Token: 0x0600063A RID: 1594 RVA: 0x0002278E File Offset: 0x0002098E
		public override void OnHeroOrPartyGaveItem(ValueTuple<Hero, PartyBase> giver, ValueTuple<Hero, PartyBase> receiver, ItemRosterElement itemRosterElement, bool showNotification)
		{
			CampaignEvents.Instance._heroOrPartyGaveItem.Invoke(giver, receiver, itemRosterElement, showNotification);
		}

		// Token: 0x170000CA RID: 202
		// (get) Token: 0x0600063B RID: 1595 RVA: 0x000227A4 File Offset: 0x000209A4
		public static IMbEvent<MobileParty> BanditPartyRecruited
		{
			get
			{
				return CampaignEvents.Instance._banditPartyRecruited;
			}
		}

		// Token: 0x0600063C RID: 1596 RVA: 0x000227B0 File Offset: 0x000209B0
		public override void OnBanditPartyRecruited(MobileParty banditParty)
		{
			CampaignEvents.Instance._banditPartyRecruited.Invoke(banditParty);
		}

		// Token: 0x170000CB RID: 203
		// (get) Token: 0x0600063D RID: 1597 RVA: 0x000227C2 File Offset: 0x000209C2
		public static IMbEvent<KingdomDecision, bool> KingdomDecisionAdded
		{
			get
			{
				return CampaignEvents.Instance._kingdomDecisionAdded;
			}
		}

		// Token: 0x0600063E RID: 1598 RVA: 0x000227CE File Offset: 0x000209CE
		public override void OnKingdomDecisionAdded(KingdomDecision decision, bool isPlayerInvolved)
		{
			CampaignEvents.Instance._kingdomDecisionAdded.Invoke(decision, isPlayerInvolved);
		}

		// Token: 0x170000CC RID: 204
		// (get) Token: 0x0600063F RID: 1599 RVA: 0x000227E1 File Offset: 0x000209E1
		public static IMbEvent<KingdomDecision, bool> KingdomDecisionCancelled
		{
			get
			{
				return CampaignEvents.Instance._kingdomDecisionCancelled;
			}
		}

		// Token: 0x06000640 RID: 1600 RVA: 0x000227ED File Offset: 0x000209ED
		public override void OnKingdomDecisionCancelled(KingdomDecision decision, bool isPlayerInvolved)
		{
			CampaignEvents.Instance._kingdomDecisionCancelled.Invoke(decision, isPlayerInvolved);
		}

		// Token: 0x170000CD RID: 205
		// (get) Token: 0x06000641 RID: 1601 RVA: 0x00022800 File Offset: 0x00020A00
		public static IMbEvent<KingdomDecision, DecisionOutcome, bool> KingdomDecisionConcluded
		{
			get
			{
				return CampaignEvents.Instance._kingdomDecisionConcluded;
			}
		}

		// Token: 0x06000642 RID: 1602 RVA: 0x0002280C File Offset: 0x00020A0C
		public override void OnKingdomDecisionConcluded(KingdomDecision decision, DecisionOutcome chosenOutcome, bool isPlayerInvolved)
		{
			CampaignEvents.Instance._kingdomDecisionConcluded.Invoke(decision, chosenOutcome, isPlayerInvolved);
		}

		// Token: 0x170000CE RID: 206
		// (get) Token: 0x06000643 RID: 1603 RVA: 0x00022820 File Offset: 0x00020A20
		public static IMbEvent<MobileParty> PartyAttachedAnotherParty
		{
			get
			{
				return CampaignEvents.Instance._partyAttachedParty;
			}
		}

		// Token: 0x06000644 RID: 1604 RVA: 0x0002282C File Offset: 0x00020A2C
		public override void OnPartyAttachedAnotherParty(MobileParty mobileParty)
		{
			CampaignEvents.Instance._partyAttachedParty.Invoke(mobileParty);
		}

		// Token: 0x170000CF RID: 207
		// (get) Token: 0x06000645 RID: 1605 RVA: 0x0002283E File Offset: 0x00020A3E
		public static IMbEvent<MobileParty> NearbyPartyAddedToPlayerMapEvent
		{
			get
			{
				return CampaignEvents.Instance._nearbyPartyAddedToPlayerMapEvent;
			}
		}

		// Token: 0x06000646 RID: 1606 RVA: 0x0002284A File Offset: 0x00020A4A
		public override void OnNearbyPartyAddedToPlayerMapEvent(MobileParty mobileParty)
		{
			CampaignEvents.Instance._nearbyPartyAddedToPlayerMapEvent.Invoke(mobileParty);
		}

		// Token: 0x170000D0 RID: 208
		// (get) Token: 0x06000647 RID: 1607 RVA: 0x0002285C File Offset: 0x00020A5C
		public static IMbEvent<Army> ArmyCreated
		{
			get
			{
				return CampaignEvents.Instance._armyCreated;
			}
		}

		// Token: 0x06000648 RID: 1608 RVA: 0x00022868 File Offset: 0x00020A68
		public override void OnArmyCreated(Army army)
		{
			CampaignEvents.Instance._armyCreated.Invoke(army);
		}

		// Token: 0x170000D1 RID: 209
		// (get) Token: 0x06000649 RID: 1609 RVA: 0x0002287A File Offset: 0x00020A7A
		public static IMbEvent<Army, Army.ArmyDispersionReason, bool> ArmyDispersed
		{
			get
			{
				return CampaignEvents.Instance._armyDispersed;
			}
		}

		// Token: 0x0600064A RID: 1610 RVA: 0x00022886 File Offset: 0x00020A86
		public override void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
		{
			CampaignEvents.Instance._armyDispersed.Invoke(army, reason, isPlayersArmy);
		}

		// Token: 0x170000D2 RID: 210
		// (get) Token: 0x0600064B RID: 1611 RVA: 0x0002289A File Offset: 0x00020A9A
		public static IMbEvent<Army, IMapPoint> ArmyGathered
		{
			get
			{
				return CampaignEvents.Instance._armyGathered;
			}
		}

		// Token: 0x0600064C RID: 1612 RVA: 0x000228A6 File Offset: 0x00020AA6
		public override void OnArmyGathered(Army army, IMapPoint gatheringPoint)
		{
			CampaignEvents.Instance._armyGathered.Invoke(army, gatheringPoint);
		}

		// Token: 0x170000D3 RID: 211
		// (get) Token: 0x0600064D RID: 1613 RVA: 0x000228B9 File Offset: 0x00020AB9
		public static IMbEvent<Hero, PerkObject> PerkOpenedEvent
		{
			get
			{
				return CampaignEvents.Instance._perkOpenedEvent;
			}
		}

		// Token: 0x0600064E RID: 1614 RVA: 0x000228C5 File Offset: 0x00020AC5
		public override void OnPerkOpened(Hero hero, PerkObject perk)
		{
			CampaignEvents.Instance._perkOpenedEvent.Invoke(hero, perk);
		}

		// Token: 0x170000D4 RID: 212
		// (get) Token: 0x0600064F RID: 1615 RVA: 0x000228D8 File Offset: 0x00020AD8
		public static IMbEvent<Hero, PerkObject> PerkResetEvent
		{
			get
			{
				return CampaignEvents.Instance._perkResetEvent;
			}
		}

		// Token: 0x06000650 RID: 1616 RVA: 0x000228E4 File Offset: 0x00020AE4
		public override void OnPerkReset(Hero hero, PerkObject perk)
		{
			CampaignEvents.Instance._perkResetEvent.Invoke(hero, perk);
		}

		// Token: 0x170000D5 RID: 213
		// (get) Token: 0x06000651 RID: 1617 RVA: 0x000228F7 File Offset: 0x00020AF7
		public static IMbEvent<TraitObject, int> PlayerTraitChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._playerTraitChangedEvent;
			}
		}

		// Token: 0x06000652 RID: 1618 RVA: 0x00022903 File Offset: 0x00020B03
		public override void OnPlayerTraitChanged(TraitObject trait, int previousLevel)
		{
			CampaignEvents.Instance._playerTraitChangedEvent.Invoke(trait, previousLevel);
		}

		// Token: 0x170000D6 RID: 214
		// (get) Token: 0x06000653 RID: 1619 RVA: 0x00022916 File Offset: 0x00020B16
		public static IMbEvent<Village, Village.VillageStates, Village.VillageStates, MobileParty> VillageStateChanged
		{
			get
			{
				return CampaignEvents.Instance._villageStateChanged;
			}
		}

		// Token: 0x06000654 RID: 1620 RVA: 0x00022922 File Offset: 0x00020B22
		public override void OnVillageStateChanged(Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raiderParty)
		{
			CampaignEvents.Instance._villageStateChanged.Invoke(village, oldState, newState, raiderParty);
		}

		// Token: 0x170000D7 RID: 215
		// (get) Token: 0x06000655 RID: 1621 RVA: 0x00022938 File Offset: 0x00020B38
		public static IMbEvent<MobileParty, Settlement, Hero> SettlementEntered
		{
			get
			{
				return CampaignEvents.Instance._settlementEntered;
			}
		}

		// Token: 0x06000656 RID: 1622 RVA: 0x00022944 File Offset: 0x00020B44
		public override void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
		{
			CampaignEvents.Instance._settlementEntered.Invoke(party, settlement, hero);
		}

		// Token: 0x170000D8 RID: 216
		// (get) Token: 0x06000657 RID: 1623 RVA: 0x00022958 File Offset: 0x00020B58
		public static IMbEvent<MobileParty, Settlement, Hero> AfterSettlementEntered
		{
			get
			{
				return CampaignEvents.Instance._afterSettlementEntered;
			}
		}

		// Token: 0x06000658 RID: 1624 RVA: 0x00022964 File Offset: 0x00020B64
		public override void OnAfterSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
		{
			CampaignEvents.Instance._afterSettlementEntered.Invoke(party, settlement, hero);
		}

		// Token: 0x170000D9 RID: 217
		// (get) Token: 0x06000659 RID: 1625 RVA: 0x00022978 File Offset: 0x00020B78
		public static IMbEvent<MobileParty, Settlement, Hero> BeforeSettlementEnteredEvent
		{
			get
			{
				return CampaignEvents.Instance._beforeSettlementEntered;
			}
		}

		// Token: 0x0600065A RID: 1626 RVA: 0x00022984 File Offset: 0x00020B84
		public override void OnBeforeSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
		{
			CampaignEvents.Instance._beforeSettlementEntered.Invoke(party, settlement, hero);
		}

		// Token: 0x170000DA RID: 218
		// (get) Token: 0x0600065B RID: 1627 RVA: 0x00022998 File Offset: 0x00020B98
		public static IMbEvent<Town, CharacterObject, CharacterObject> MercenaryTroopChangedInTown
		{
			get
			{
				return CampaignEvents.Instance._mercenaryTroopChangedInTown;
			}
		}

		// Token: 0x0600065C RID: 1628 RVA: 0x000229A4 File Offset: 0x00020BA4
		public override void OnMercenaryTroopChangedInTown(Town town, CharacterObject oldTroopType, CharacterObject newTroopType)
		{
			CampaignEvents.Instance._mercenaryTroopChangedInTown.Invoke(town, oldTroopType, newTroopType);
		}

		// Token: 0x170000DB RID: 219
		// (get) Token: 0x0600065D RID: 1629 RVA: 0x000229B8 File Offset: 0x00020BB8
		public static IMbEvent<Town, int, int> MercenaryNumberChangedInTown
		{
			get
			{
				return CampaignEvents.Instance._mercenaryNumberChangedInTown;
			}
		}

		// Token: 0x0600065E RID: 1630 RVA: 0x000229C4 File Offset: 0x00020BC4
		public override void OnMercenaryNumberChangedInTown(Town town, int oldNumber, int newNumber)
		{
			CampaignEvents.Instance._mercenaryNumberChangedInTown.Invoke(town, oldNumber, newNumber);
		}

		// Token: 0x170000DC RID: 220
		// (get) Token: 0x0600065F RID: 1631 RVA: 0x000229D8 File Offset: 0x00020BD8
		public static IMbEvent<Alley, Hero, Hero> AlleyOwnerChanged
		{
			get
			{
				return CampaignEvents.Instance._alleyOwnerChanged;
			}
		}

		// Token: 0x170000DD RID: 221
		// (get) Token: 0x06000660 RID: 1632 RVA: 0x000229E4 File Offset: 0x00020BE4
		public static IMbEvent<Alley, TroopRoster> AlleyOccupiedByPlayer
		{
			get
			{
				return CampaignEvents.Instance._alleyOccupiedByPlayer;
			}
		}

		// Token: 0x06000661 RID: 1633 RVA: 0x000229F0 File Offset: 0x00020BF0
		public override void OnAlleyOccupiedByPlayer(Alley alley, TroopRoster troops)
		{
			CampaignEvents.Instance._alleyOccupiedByPlayer.Invoke(alley, troops);
		}

		// Token: 0x06000662 RID: 1634 RVA: 0x00022A03 File Offset: 0x00020C03
		public override void OnAlleyOwnerChanged(Alley alley, Hero newOwner, Hero oldOwner)
		{
			CampaignEvents.Instance._alleyOwnerChanged.Invoke(alley, newOwner, oldOwner);
		}

		// Token: 0x170000DE RID: 222
		// (get) Token: 0x06000663 RID: 1635 RVA: 0x00022A17 File Offset: 0x00020C17
		public static IMbEvent<Alley> AlleyClearedByPlayer
		{
			get
			{
				return CampaignEvents.Instance._alleyClearedByPlayer;
			}
		}

		// Token: 0x06000664 RID: 1636 RVA: 0x00022A23 File Offset: 0x00020C23
		public override void OnAlleyClearedByPlayer(Alley alley)
		{
			CampaignEvents.Instance._alleyClearedByPlayer.Invoke(alley);
		}

		// Token: 0x170000DF RID: 223
		// (get) Token: 0x06000665 RID: 1637 RVA: 0x00022A35 File Offset: 0x00020C35
		public static IMbEvent<Hero, Hero, Romance.RomanceLevelEnum> RomanticStateChanged
		{
			get
			{
				return CampaignEvents.Instance._romanticStateChanged;
			}
		}

		// Token: 0x06000666 RID: 1638 RVA: 0x00022A41 File Offset: 0x00020C41
		public override void OnRomanticStateChanged(Hero hero1, Hero hero2, Romance.RomanceLevelEnum romanceLevel)
		{
			CampaignEvents.Instance._romanticStateChanged.Invoke(hero1, hero2, romanceLevel);
		}

		// Token: 0x170000E0 RID: 224
		// (get) Token: 0x06000667 RID: 1639 RVA: 0x00022A55 File Offset: 0x00020C55
		public static IMbEvent<Hero, Hero, bool> BeforeHeroesMarried
		{
			get
			{
				return CampaignEvents.Instance._beforeHeroesMarried;
			}
		}

		// Token: 0x06000668 RID: 1640 RVA: 0x00022A61 File Offset: 0x00020C61
		public override void OnBeforeHeroesMarried(Hero hero1, Hero hero2, bool showNotification = true)
		{
			CampaignEvents.Instance._beforeHeroesMarried.Invoke(hero1, hero2, showNotification);
		}

		// Token: 0x170000E1 RID: 225
		// (get) Token: 0x06000669 RID: 1641 RVA: 0x00022A75 File Offset: 0x00020C75
		public static IMbEvent<int, Town> PlayerEliminatedFromTournament
		{
			get
			{
				return CampaignEvents.Instance._playerEliminatedFromTournament;
			}
		}

		// Token: 0x0600066A RID: 1642 RVA: 0x00022A81 File Offset: 0x00020C81
		public override void OnPlayerEliminatedFromTournament(int round, Town town)
		{
			CampaignEvents.Instance._playerEliminatedFromTournament.Invoke(round, town);
		}

		// Token: 0x170000E2 RID: 226
		// (get) Token: 0x0600066B RID: 1643 RVA: 0x00022A94 File Offset: 0x00020C94
		public static IMbEvent<Town> PlayerStartedTournamentMatch
		{
			get
			{
				return CampaignEvents.Instance._playerStartedTournamentMatch;
			}
		}

		// Token: 0x0600066C RID: 1644 RVA: 0x00022AA0 File Offset: 0x00020CA0
		public override void OnPlayerStartedTournamentMatch(Town town)
		{
			CampaignEvents.Instance._playerStartedTournamentMatch.Invoke(town);
		}

		// Token: 0x170000E3 RID: 227
		// (get) Token: 0x0600066D RID: 1645 RVA: 0x00022AB2 File Offset: 0x00020CB2
		public static IMbEvent<Town> TournamentStarted
		{
			get
			{
				return CampaignEvents.Instance._tournamentStarted;
			}
		}

		// Token: 0x0600066E RID: 1646 RVA: 0x00022ABE File Offset: 0x00020CBE
		public override void OnTournamentStarted(Town town)
		{
			CampaignEvents.Instance._tournamentStarted.Invoke(town);
		}

		// Token: 0x170000E4 RID: 228
		// (get) Token: 0x0600066F RID: 1647 RVA: 0x00022AD0 File Offset: 0x00020CD0
		public static IMbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail> WarDeclared
		{
			get
			{
				return CampaignEvents.Instance._warDeclared;
			}
		}

		// Token: 0x06000670 RID: 1648 RVA: 0x00022ADC File Offset: 0x00020CDC
		public override void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
		{
			CampaignEvents.Instance._warDeclared.Invoke(faction1, faction2, declareWarDetail);
		}

		// Token: 0x170000E5 RID: 229
		// (get) Token: 0x06000671 RID: 1649 RVA: 0x00022AF0 File Offset: 0x00020CF0
		public static IMbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> TournamentFinished
		{
			get
			{
				return CampaignEvents.Instance._tournamentFinished;
			}
		}

		// Token: 0x06000672 RID: 1650 RVA: 0x00022AFC File Offset: 0x00020CFC
		public override void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
		{
			CampaignEvents.Instance._tournamentFinished.Invoke(winner, participants, town, prize);
		}

		// Token: 0x170000E6 RID: 230
		// (get) Token: 0x06000673 RID: 1651 RVA: 0x00022B12 File Offset: 0x00020D12
		public static IMbEvent<Town> TournamentCancelled
		{
			get
			{
				return CampaignEvents.Instance._tournamentCancelled;
			}
		}

		// Token: 0x06000674 RID: 1652 RVA: 0x00022B1E File Offset: 0x00020D1E
		public override void OnTournamentCancelled(Town town)
		{
			CampaignEvents.Instance._tournamentCancelled.Invoke(town);
		}

		// Token: 0x170000E7 RID: 231
		// (get) Token: 0x06000675 RID: 1653 RVA: 0x00022B30 File Offset: 0x00020D30
		public static IMbEvent<PartyBase, PartyBase, object, bool> BattleStarted
		{
			get
			{
				return CampaignEvents.Instance._battleStarted;
			}
		}

		// Token: 0x06000676 RID: 1654 RVA: 0x00022B3C File Offset: 0x00020D3C
		public override void OnStartBattle(PartyBase attackerParty, PartyBase defenderParty, object subject, bool showNotification)
		{
			CampaignEvents.Instance._battleStarted.Invoke(attackerParty, defenderParty, subject, showNotification);
		}

		// Token: 0x170000E8 RID: 232
		// (get) Token: 0x06000677 RID: 1655 RVA: 0x00022B52 File Offset: 0x00020D52
		public static IMbEvent<Settlement, Clan> RebellionFinished
		{
			get
			{
				return CampaignEvents.Instance._rebellionFinished;
			}
		}

		// Token: 0x06000678 RID: 1656 RVA: 0x00022B5E File Offset: 0x00020D5E
		public override void OnRebellionFinished(Settlement settlement, Clan oldOwnerClan)
		{
			CampaignEvents.Instance._rebellionFinished.Invoke(settlement, oldOwnerClan);
		}

		// Token: 0x170000E9 RID: 233
		// (get) Token: 0x06000679 RID: 1657 RVA: 0x00022B71 File Offset: 0x00020D71
		public static IMbEvent<Town, bool> TownRebelliosStateChanged
		{
			get
			{
				return CampaignEvents.Instance._townRebelliousStateChanged;
			}
		}

		// Token: 0x0600067A RID: 1658 RVA: 0x00022B7D File Offset: 0x00020D7D
		public override void TownRebelliousStateChanged(Town town, bool rebelliousState)
		{
			CampaignEvents.Instance._townRebelliousStateChanged.Invoke(town, rebelliousState);
		}

		// Token: 0x170000EA RID: 234
		// (get) Token: 0x0600067B RID: 1659 RVA: 0x00022B90 File Offset: 0x00020D90
		public static IMbEvent<Settlement, Clan> RebelliousClanDisbandedAtSettlement
		{
			get
			{
				return CampaignEvents.Instance._rebelliousClanDisbandedAtSettlement;
			}
		}

		// Token: 0x0600067C RID: 1660 RVA: 0x00022B9C File Offset: 0x00020D9C
		public override void OnRebelliousClanDisbandedAtSettlement(Settlement settlement, Clan clan)
		{
			CampaignEvents.Instance._rebelliousClanDisbandedAtSettlement.Invoke(settlement, clan);
		}

		// Token: 0x170000EB RID: 235
		// (get) Token: 0x0600067D RID: 1661 RVA: 0x00022BAF File Offset: 0x00020DAF
		public static IMbEvent<MobileParty, ItemRoster> ItemsLooted
		{
			get
			{
				return CampaignEvents.Instance._itemsLooted;
			}
		}

		// Token: 0x0600067E RID: 1662 RVA: 0x00022BBB File Offset: 0x00020DBB
		public override void OnItemsLooted(MobileParty mobileParty, ItemRoster items)
		{
			CampaignEvents.Instance._itemsLooted.Invoke(mobileParty, items);
		}

		// Token: 0x170000EC RID: 236
		// (get) Token: 0x0600067F RID: 1663 RVA: 0x00022BCE File Offset: 0x00020DCE
		public static IMbEvent<MobileParty, PartyBase> MobilePartyDestroyed
		{
			get
			{
				return CampaignEvents.Instance._mobilePartyDestroyed;
			}
		}

		// Token: 0x06000680 RID: 1664 RVA: 0x00022BDA File Offset: 0x00020DDA
		public override void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
		{
			CampaignEvents.Instance._mobilePartyDestroyed.Invoke(mobileParty, destroyerParty);
		}

		// Token: 0x170000ED RID: 237
		// (get) Token: 0x06000681 RID: 1665 RVA: 0x00022BED File Offset: 0x00020DED
		public static IMbEvent<MobileParty> MobilePartyCreated
		{
			get
			{
				return CampaignEvents.Instance._mobilePartyCreated;
			}
		}

		// Token: 0x06000682 RID: 1666 RVA: 0x00022BF9 File Offset: 0x00020DF9
		public override void OnMobilePartyCreated(MobileParty party)
		{
			CampaignEvents.Instance._mobilePartyCreated.Invoke(party);
		}

		// Token: 0x170000EE RID: 238
		// (get) Token: 0x06000683 RID: 1667 RVA: 0x00022C0B File Offset: 0x00020E0B
		public static IMbEvent<IInteractablePoint> MapInteractableCreated
		{
			get
			{
				return CampaignEvents.Instance._mapInteractableCreated;
			}
		}

		// Token: 0x06000684 RID: 1668 RVA: 0x00022C17 File Offset: 0x00020E17
		public override void OnMapInteractableCreated(IInteractablePoint interactable)
		{
			CampaignEvents.Instance._mapInteractableCreated.Invoke(interactable);
		}

		// Token: 0x170000EF RID: 239
		// (get) Token: 0x06000685 RID: 1669 RVA: 0x00022C29 File Offset: 0x00020E29
		public static IMbEvent<IInteractablePoint> MapInteractableDestroyed
		{
			get
			{
				return CampaignEvents.Instance._mapInteractableDestroyed;
			}
		}

		// Token: 0x06000686 RID: 1670 RVA: 0x00022C35 File Offset: 0x00020E35
		public override void OnMapInteractableDestroyed(IInteractablePoint interactable)
		{
			CampaignEvents.Instance._mapInteractableDestroyed.Invoke(interactable);
		}

		// Token: 0x170000F0 RID: 240
		// (get) Token: 0x06000687 RID: 1671 RVA: 0x00022C47 File Offset: 0x00020E47
		public static IMbEvent<MobileParty, bool> MobilePartyQuestStatusChanged
		{
			get
			{
				return CampaignEvents.Instance._mobilePartyQuestStatusChanged;
			}
		}

		// Token: 0x06000688 RID: 1672 RVA: 0x00022C53 File Offset: 0x00020E53
		public override void OnMobilePartyQuestStatusChanged(MobileParty party, bool isUsedByQuest)
		{
			CampaignEvents.Instance._mobilePartyQuestStatusChanged.Invoke(party, isUsedByQuest);
		}

		// Token: 0x170000F1 RID: 241
		// (get) Token: 0x06000689 RID: 1673 RVA: 0x00022C66 File Offset: 0x00020E66
		public static IMbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool> HeroKilledEvent
		{
			get
			{
				return CampaignEvents.Instance._heroKilled;
			}
		}

		// Token: 0x0600068A RID: 1674 RVA: 0x00022C72 File Offset: 0x00020E72
		public override void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
		{
			CampaignEvents.Instance._heroKilled.Invoke(victim, killer, detail, showNotification);
		}

		// Token: 0x170000F2 RID: 242
		// (get) Token: 0x0600068B RID: 1675 RVA: 0x00022C88 File Offset: 0x00020E88
		public static IMbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool> BeforeHeroKilledEvent
		{
			get
			{
				return CampaignEvents.Instance._onBeforeHeroKilled;
			}
		}

		// Token: 0x0600068C RID: 1676 RVA: 0x00022C94 File Offset: 0x00020E94
		public override void OnBeforeHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
		{
			CampaignEvents.Instance._onBeforeHeroKilled.Invoke(victim, killer, detail, showNotification);
		}

		// Token: 0x170000F3 RID: 243
		// (get) Token: 0x0600068D RID: 1677 RVA: 0x00022CAA File Offset: 0x00020EAA
		public static IMbEvent<Hero, int> ChildEducationCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._childEducationCompleted;
			}
		}

		// Token: 0x0600068E RID: 1678 RVA: 0x00022CB6 File Offset: 0x00020EB6
		public override void OnChildEducationCompleted(Hero hero, int age)
		{
			CampaignEvents.Instance._childEducationCompleted.Invoke(hero, age);
		}

		// Token: 0x170000F4 RID: 244
		// (get) Token: 0x0600068F RID: 1679 RVA: 0x00022CC9 File Offset: 0x00020EC9
		public static IMbEvent<Hero> HeroComesOfAgeEvent
		{
			get
			{
				return CampaignEvents.Instance._heroComesOfAge;
			}
		}

		// Token: 0x06000690 RID: 1680 RVA: 0x00022CD5 File Offset: 0x00020ED5
		public override void OnHeroComesOfAge(Hero hero)
		{
			CampaignEvents.Instance._heroComesOfAge.Invoke(hero);
		}

		// Token: 0x170000F5 RID: 245
		// (get) Token: 0x06000691 RID: 1681 RVA: 0x00022CE7 File Offset: 0x00020EE7
		public static IMbEvent<Hero> HeroGrowsOutOfInfancyEvent
		{
			get
			{
				return CampaignEvents.Instance._heroGrowsOutOfInfancyEvent;
			}
		}

		// Token: 0x06000692 RID: 1682 RVA: 0x00022CF3 File Offset: 0x00020EF3
		public override void OnHeroGrowsOutOfInfancy(Hero hero)
		{
			CampaignEvents.Instance._heroGrowsOutOfInfancyEvent.Invoke(hero);
		}

		// Token: 0x170000F6 RID: 246
		// (get) Token: 0x06000693 RID: 1683 RVA: 0x00022D05 File Offset: 0x00020F05
		public static IMbEvent<Hero> HeroReachesTeenAgeEvent
		{
			get
			{
				return CampaignEvents.Instance._heroReachesTeenAgeEvent;
			}
		}

		// Token: 0x06000694 RID: 1684 RVA: 0x00022D11 File Offset: 0x00020F11
		public override void OnHeroReachesTeenAge(Hero hero)
		{
			CampaignEvents.Instance._heroReachesTeenAgeEvent.Invoke(hero);
		}

		// Token: 0x170000F7 RID: 247
		// (get) Token: 0x06000695 RID: 1685 RVA: 0x00022D23 File Offset: 0x00020F23
		public static IMbEvent<Hero, Hero> CharacterDefeated
		{
			get
			{
				return CampaignEvents.Instance._characterDefeated;
			}
		}

		// Token: 0x06000696 RID: 1686 RVA: 0x00022D2F File Offset: 0x00020F2F
		public override void OnCharacterDefeated(Hero winner, Hero loser)
		{
			CampaignEvents.Instance._characterDefeated.Invoke(winner, loser);
		}

		// Token: 0x170000F8 RID: 248
		// (get) Token: 0x06000697 RID: 1687 RVA: 0x00022D42 File Offset: 0x00020F42
		public static IMbEvent<Kingdom, Clan> RulingClanChanged
		{
			get
			{
				return CampaignEvents.Instance._rulingClanChanged;
			}
		}

		// Token: 0x06000698 RID: 1688 RVA: 0x00022D4E File Offset: 0x00020F4E
		public override void OnRulingClanChanged(Kingdom kingdom, Clan newRulingClan)
		{
			CampaignEvents.Instance._rulingClanChanged.Invoke(kingdom, newRulingClan);
		}

		// Token: 0x170000F9 RID: 249
		// (get) Token: 0x06000699 RID: 1689 RVA: 0x00022D61 File Offset: 0x00020F61
		public static IMbEvent<PartyBase, Hero> HeroPrisonerTaken
		{
			get
			{
				return CampaignEvents.Instance._heroPrisonerTaken;
			}
		}

		// Token: 0x0600069A RID: 1690 RVA: 0x00022D6D File Offset: 0x00020F6D
		public override void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
		{
			CampaignEvents.Instance._heroPrisonerTaken.Invoke(capturer, prisoner);
		}

		// Token: 0x170000FA RID: 250
		// (get) Token: 0x0600069B RID: 1691 RVA: 0x00022D80 File Offset: 0x00020F80
		public static IMbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool> HeroPrisonerReleased
		{
			get
			{
				return CampaignEvents.Instance._heroPrisonerReleased;
			}
		}

		// Token: 0x0600069C RID: 1692 RVA: 0x00022D8C File Offset: 0x00020F8C
		public override void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification = true)
		{
			CampaignEvents.Instance._heroPrisonerReleased.Invoke(prisoner, party, capturerFaction, detail, showNotification);
		}

		// Token: 0x170000FB RID: 251
		// (get) Token: 0x0600069D RID: 1693 RVA: 0x00022DA4 File Offset: 0x00020FA4
		public static IMbEvent<Hero, bool> CharacterBecameFugitiveEvent
		{
			get
			{
				return CampaignEvents.Instance._characterBecameFugitiveEvent;
			}
		}

		// Token: 0x0600069E RID: 1694 RVA: 0x00022DB0 File Offset: 0x00020FB0
		public override void OnCharacterBecameFugitive(Hero hero, bool showNotification)
		{
			CampaignEvents.Instance._characterBecameFugitiveEvent.Invoke(hero, showNotification);
		}

		// Token: 0x170000FC RID: 252
		// (get) Token: 0x0600069F RID: 1695 RVA: 0x00022DC3 File Offset: 0x00020FC3
		public static IMbEvent<Hero> OnPlayerMetHeroEvent
		{
			get
			{
				return CampaignEvents.Instance._playerMetHero;
			}
		}

		// Token: 0x060006A0 RID: 1696 RVA: 0x00022DCF File Offset: 0x00020FCF
		public override void OnPlayerMetHero(Hero hero)
		{
			CampaignEvents.Instance._playerMetHero.Invoke(hero);
		}

		// Token: 0x170000FD RID: 253
		// (get) Token: 0x060006A1 RID: 1697 RVA: 0x00022DE1 File Offset: 0x00020FE1
		public static IMbEvent<Hero> OnPlayerLearnsAboutHeroEvent
		{
			get
			{
				return CampaignEvents.Instance._playerLearnsAboutHero;
			}
		}

		// Token: 0x060006A2 RID: 1698 RVA: 0x00022DED File Offset: 0x00020FED
		public override void OnPlayerLearnsAboutHero(Hero hero)
		{
			CampaignEvents.Instance._playerLearnsAboutHero.Invoke(hero);
		}

		// Token: 0x170000FE RID: 254
		// (get) Token: 0x060006A3 RID: 1699 RVA: 0x00022DFF File Offset: 0x00020FFF
		public static IMbEvent<Hero, int, bool> RenownGained
		{
			get
			{
				return CampaignEvents.Instance._renownGained;
			}
		}

		// Token: 0x060006A4 RID: 1700 RVA: 0x00022E0B File Offset: 0x0002100B
		public override void OnRenownGained(Hero hero, int gainedRenown, bool doNotNotify)
		{
			CampaignEvents.Instance._renownGained.Invoke(hero, gainedRenown, doNotNotify);
		}

		// Token: 0x170000FF RID: 255
		// (get) Token: 0x060006A5 RID: 1701 RVA: 0x00022E1F File Offset: 0x0002101F
		public static IMbEvent<IFaction, float> CrimeRatingChanged
		{
			get
			{
				return CampaignEvents.Instance._crimeRatingChanged;
			}
		}

		// Token: 0x060006A6 RID: 1702 RVA: 0x00022E2B File Offset: 0x0002102B
		public override void OnCrimeRatingChanged(IFaction kingdom, float deltaCrimeAmount)
		{
			CampaignEvents.Instance._crimeRatingChanged.Invoke(kingdom, deltaCrimeAmount);
		}

		// Token: 0x17000100 RID: 256
		// (get) Token: 0x060006A7 RID: 1703 RVA: 0x00022E3E File Offset: 0x0002103E
		public static IMbEvent<Hero> NewCompanionAdded
		{
			get
			{
				return CampaignEvents.Instance._newCompanionAdded;
			}
		}

		// Token: 0x060006A8 RID: 1704 RVA: 0x00022E4A File Offset: 0x0002104A
		public override void OnNewCompanionAdded(Hero newCompanion)
		{
			CampaignEvents.Instance._newCompanionAdded.Invoke(newCompanion);
		}

		// Token: 0x17000101 RID: 257
		// (get) Token: 0x060006A9 RID: 1705 RVA: 0x00022E5C File Offset: 0x0002105C
		public static IMbEvent<IMission> AfterMissionStarted
		{
			get
			{
				return CampaignEvents.Instance._afterMissionStarted;
			}
		}

		// Token: 0x060006AA RID: 1706 RVA: 0x00022E68 File Offset: 0x00021068
		public override void OnAfterMissionStarted(IMission iMission)
		{
			CampaignEvents.Instance._afterMissionStarted.Invoke(iMission);
		}

		// Token: 0x17000102 RID: 258
		// (get) Token: 0x060006AB RID: 1707 RVA: 0x00022E7A File Offset: 0x0002107A
		public static IMbEvent<MenuCallbackArgs> GameMenuOpened
		{
			get
			{
				return CampaignEvents.Instance._gameMenuOpened;
			}
		}

		// Token: 0x060006AC RID: 1708 RVA: 0x00022E86 File Offset: 0x00021086
		public override void OnGameMenuOpened(MenuCallbackArgs args)
		{
			CampaignEvents.Instance._gameMenuOpened.Invoke(args);
		}

		// Token: 0x17000103 RID: 259
		// (get) Token: 0x060006AD RID: 1709 RVA: 0x00022E98 File Offset: 0x00021098
		public static IMbEvent<MenuCallbackArgs> AfterGameMenuInitializedEvent
		{
			get
			{
				return CampaignEvents.Instance._afterGameMenuInitializedEvent;
			}
		}

		// Token: 0x060006AE RID: 1710 RVA: 0x00022EA4 File Offset: 0x000210A4
		public override void AfterGameMenuInitialized(MenuCallbackArgs args)
		{
			CampaignEvents.Instance._afterGameMenuInitializedEvent.Invoke(args);
		}

		// Token: 0x17000104 RID: 260
		// (get) Token: 0x060006AF RID: 1711 RVA: 0x00022EB6 File Offset: 0x000210B6
		public static IMbEvent<MenuCallbackArgs> BeforeGameMenuOpenedEvent
		{
			get
			{
				return CampaignEvents.Instance._beforeGameMenuOpenedEvent;
			}
		}

		// Token: 0x060006B0 RID: 1712 RVA: 0x00022EC2 File Offset: 0x000210C2
		public override void BeforeGameMenuOpened(MenuCallbackArgs args)
		{
			CampaignEvents.Instance._beforeGameMenuOpenedEvent.Invoke(args);
		}

		// Token: 0x17000105 RID: 261
		// (get) Token: 0x060006B1 RID: 1713 RVA: 0x00022ED4 File Offset: 0x000210D4
		public static IMbEvent<IFaction, IFaction, MakePeaceAction.MakePeaceDetail> MakePeace
		{
			get
			{
				return CampaignEvents.Instance._makePeace;
			}
		}

		// Token: 0x060006B2 RID: 1714 RVA: 0x00022EE0 File Offset: 0x000210E0
		public override void OnMakePeace(IFaction side1Faction, IFaction side2Faction, MakePeaceAction.MakePeaceDetail detail)
		{
			CampaignEvents.Instance._makePeace.Invoke(side1Faction, side2Faction, detail);
		}

		// Token: 0x17000106 RID: 262
		// (get) Token: 0x060006B3 RID: 1715 RVA: 0x00022EF4 File Offset: 0x000210F4
		public static IMbEvent<Kingdom> KingdomDestroyedEvent
		{
			get
			{
				return CampaignEvents.Instance._kingdomDestroyed;
			}
		}

		// Token: 0x060006B4 RID: 1716 RVA: 0x00022F00 File Offset: 0x00021100
		public override void OnKingdomDestroyed(Kingdom destroyedKingdom)
		{
			CampaignEvents.Instance._kingdomDestroyed.Invoke(destroyedKingdom);
		}

		// Token: 0x17000107 RID: 263
		// (get) Token: 0x060006B5 RID: 1717 RVA: 0x00022F12 File Offset: 0x00021112
		public static ReferenceIMBEvent<Kingdom, bool> CanKingdomBeDiscontinuedEvent
		{
			get
			{
				return CampaignEvents.Instance._canKingdomBeDiscontinued;
			}
		}

		// Token: 0x060006B6 RID: 1718 RVA: 0x00022F1E File Offset: 0x0002111E
		public override void CanKingdomBeDiscontinued(Kingdom kingdom, ref bool result)
		{
			CampaignEvents.Instance._canKingdomBeDiscontinued.Invoke(kingdom, ref result);
		}

		// Token: 0x17000108 RID: 264
		// (get) Token: 0x060006B7 RID: 1719 RVA: 0x00022F31 File Offset: 0x00021131
		public static IMbEvent<Kingdom> KingdomCreatedEvent
		{
			get
			{
				return CampaignEvents.Instance._kingdomCreated;
			}
		}

		// Token: 0x060006B8 RID: 1720 RVA: 0x00022F3D File Offset: 0x0002113D
		public override void OnKingdomCreated(Kingdom createdKingdom)
		{
			CampaignEvents.Instance._kingdomCreated.Invoke(createdKingdom);
		}

		// Token: 0x17000109 RID: 265
		// (get) Token: 0x060006B9 RID: 1721 RVA: 0x00022F4F File Offset: 0x0002114F
		public static IMbEvent<Village> VillageBecomeNormal
		{
			get
			{
				return CampaignEvents.Instance._villageBecomeNormal;
			}
		}

		// Token: 0x060006BA RID: 1722 RVA: 0x00022F5B File Offset: 0x0002115B
		public override void OnVillageBecomeNormal(Village village)
		{
			CampaignEvents.Instance._villageBecomeNormal.Invoke(village);
		}

		// Token: 0x1700010A RID: 266
		// (get) Token: 0x060006BB RID: 1723 RVA: 0x00022F6D File Offset: 0x0002116D
		public static IMbEvent<Village> VillageBeingRaided
		{
			get
			{
				return CampaignEvents.Instance._villageBeingRaided;
			}
		}

		// Token: 0x060006BC RID: 1724 RVA: 0x00022F79 File Offset: 0x00021179
		public override void OnVillageBeingRaided(Village village)
		{
			CampaignEvents.Instance._villageBeingRaided.Invoke(village);
		}

		// Token: 0x1700010B RID: 267
		// (get) Token: 0x060006BD RID: 1725 RVA: 0x00022F8B File Offset: 0x0002118B
		public static IMbEvent<Village> VillageLooted
		{
			get
			{
				return CampaignEvents.Instance._villageLooted;
			}
		}

		// Token: 0x060006BE RID: 1726 RVA: 0x00022F97 File Offset: 0x00021197
		public override void OnVillageLooted(Village village)
		{
			CampaignEvents.Instance._villageLooted.Invoke(village);
		}

		// Token: 0x1700010C RID: 268
		// (get) Token: 0x060006BF RID: 1727 RVA: 0x00022FA9 File Offset: 0x000211A9
		public static IMbEvent<Hero, RemoveCompanionAction.RemoveCompanionDetail> CompanionRemoved
		{
			get
			{
				return CampaignEvents.Instance._companionRemoved;
			}
		}

		// Token: 0x060006C0 RID: 1728 RVA: 0x00022FB5 File Offset: 0x000211B5
		public override void OnCompanionRemoved(Hero companion, RemoveCompanionAction.RemoveCompanionDetail detail)
		{
			CampaignEvents.Instance._companionRemoved.Invoke(companion, detail);
		}

		// Token: 0x1700010D RID: 269
		// (get) Token: 0x060006C1 RID: 1729 RVA: 0x00022FC8 File Offset: 0x000211C8
		public static IMbEvent<IAgent> OnAgentJoinedConversationEvent
		{
			get
			{
				return CampaignEvents.Instance._onAgentJoinedConversationEvent;
			}
		}

		// Token: 0x060006C2 RID: 1730 RVA: 0x00022FD4 File Offset: 0x000211D4
		public override void OnAgentJoinedConversation(IAgent agent)
		{
			CampaignEvents.Instance._onAgentJoinedConversationEvent.Invoke(agent);
		}

		// Token: 0x1700010E RID: 270
		// (get) Token: 0x060006C3 RID: 1731 RVA: 0x00022FE6 File Offset: 0x000211E6
		public static IMbEvent<IEnumerable<CharacterObject>> ConversationEnded
		{
			get
			{
				return CampaignEvents.Instance._onConversationEnded;
			}
		}

		// Token: 0x060006C4 RID: 1732 RVA: 0x00022FF2 File Offset: 0x000211F2
		public override void OnConversationEnded(IEnumerable<CharacterObject> characters)
		{
			CampaignEvents.Instance._onConversationEnded.Invoke(characters);
		}

		// Token: 0x1700010F RID: 271
		// (get) Token: 0x060006C5 RID: 1733 RVA: 0x00023004 File Offset: 0x00021204
		public static IMbEvent<MapEvent> MapEventEnded
		{
			get
			{
				return CampaignEvents.Instance._mapEventEnded;
			}
		}

		// Token: 0x060006C6 RID: 1734 RVA: 0x00023010 File Offset: 0x00021210
		public override void OnMapEventEnded(MapEvent mapEvent)
		{
			CampaignEvents.Instance._mapEventEnded.Invoke(mapEvent);
		}

		// Token: 0x17000110 RID: 272
		// (get) Token: 0x060006C7 RID: 1735 RVA: 0x00023022 File Offset: 0x00021222
		public static IMbEvent<MapEvent, PartyBase, PartyBase> MapEventStarted
		{
			get
			{
				return CampaignEvents.Instance._mapEventStarted;
			}
		}

		// Token: 0x060006C8 RID: 1736 RVA: 0x0002302E File Offset: 0x0002122E
		public override void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
		{
			CampaignEvents.Instance._mapEventStarted.Invoke(mapEvent, attackerParty, defenderParty);
		}

		// Token: 0x17000111 RID: 273
		// (get) Token: 0x060006C9 RID: 1737 RVA: 0x00023042 File Offset: 0x00021242
		public static IMbEvent<Settlement, FlattenedTroopRoster, Hero, bool> PrisonersChangeInSettlement
		{
			get
			{
				return CampaignEvents.Instance._prisonersChangeInSettlement;
			}
		}

		// Token: 0x060006CA RID: 1738 RVA: 0x0002304E File Offset: 0x0002124E
		public override void OnPrisonersChangeInSettlement(Settlement settlement, FlattenedTroopRoster prisonerRoster, Hero prisonerHero, bool takenFromDungeon)
		{
			CampaignEvents.Instance._prisonersChangeInSettlement.Invoke(settlement, prisonerRoster, prisonerHero, takenFromDungeon);
		}

		// Token: 0x17000112 RID: 274
		// (get) Token: 0x060006CB RID: 1739 RVA: 0x00023064 File Offset: 0x00021264
		public static IMbEvent<Hero, BoardGameHelper.BoardGameState> OnPlayerBoardGameOverEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerBoardGameOver;
			}
		}

		// Token: 0x060006CC RID: 1740 RVA: 0x00023070 File Offset: 0x00021270
		public override void OnPlayerBoardGameOver(Hero opposingHero, BoardGameHelper.BoardGameState state)
		{
			CampaignEvents.Instance._onPlayerBoardGameOver.Invoke(opposingHero, state);
		}

		// Token: 0x17000113 RID: 275
		// (get) Token: 0x060006CD RID: 1741 RVA: 0x00023083 File Offset: 0x00021283
		public static IMbEvent<Hero> OnRansomOfferedToPlayerEvent
		{
			get
			{
				return CampaignEvents.Instance._onRansomOfferedToPlayer;
			}
		}

		// Token: 0x060006CE RID: 1742 RVA: 0x0002308F File Offset: 0x0002128F
		public override void OnRansomOfferedToPlayer(Hero captiveHero)
		{
			CampaignEvents.Instance._onRansomOfferedToPlayer.Invoke(captiveHero);
		}

		// Token: 0x17000114 RID: 276
		// (get) Token: 0x060006CF RID: 1743 RVA: 0x000230A1 File Offset: 0x000212A1
		public static IMbEvent<Hero> OnRansomOfferCancelledEvent
		{
			get
			{
				return CampaignEvents.Instance._onRansomOfferCancelled;
			}
		}

		// Token: 0x060006D0 RID: 1744 RVA: 0x000230AD File Offset: 0x000212AD
		public override void OnRansomOfferCancelled(Hero captiveHero)
		{
			CampaignEvents.Instance._onRansomOfferCancelled.Invoke(captiveHero);
		}

		// Token: 0x17000115 RID: 277
		// (get) Token: 0x060006D1 RID: 1745 RVA: 0x000230BF File Offset: 0x000212BF
		public static IMbEvent<IFaction, int, int> OnPeaceOfferedToPlayerEvent
		{
			get
			{
				return CampaignEvents.Instance._onPeaceOfferedToPlayer;
			}
		}

		// Token: 0x060006D2 RID: 1746 RVA: 0x000230CB File Offset: 0x000212CB
		public override void OnPeaceOfferedToPlayer(IFaction opponentFaction, int tributeAmount, int tributeDurationInDays)
		{
			CampaignEvents.Instance._onPeaceOfferedToPlayer.Invoke(opponentFaction, tributeAmount, tributeDurationInDays);
		}

		// Token: 0x17000116 RID: 278
		// (get) Token: 0x060006D3 RID: 1747 RVA: 0x000230DF File Offset: 0x000212DF
		public static IMbEvent<Kingdom, Kingdom> OnTradeAgreementSignedEvent
		{
			get
			{
				return CampaignEvents.Instance._onTradeAgreementSignedEvent;
			}
		}

		// Token: 0x060006D4 RID: 1748 RVA: 0x000230EB File Offset: 0x000212EB
		public override void OnTradeAgreementSigned(Kingdom kingdom, Kingdom other)
		{
			CampaignEvents.Instance._onTradeAgreementSignedEvent.Invoke(kingdom, other);
		}

		// Token: 0x17000117 RID: 279
		// (get) Token: 0x060006D5 RID: 1749 RVA: 0x000230FE File Offset: 0x000212FE
		public static IMbEvent<IFaction> OnPeaceOfferResolvedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPeaceOfferResolved;
			}
		}

		// Token: 0x060006D6 RID: 1750 RVA: 0x0002310A File Offset: 0x0002130A
		public override void OnPeaceOfferResolved(IFaction opponentFaction)
		{
			CampaignEvents.Instance._onPeaceOfferResolved.Invoke(opponentFaction);
		}

		// Token: 0x17000118 RID: 280
		// (get) Token: 0x060006D7 RID: 1751 RVA: 0x0002311C File Offset: 0x0002131C
		public static IMbEvent<Hero, Hero> OnMarriageOfferedToPlayerEvent
		{
			get
			{
				return CampaignEvents.Instance._onMarriageOfferedToPlayerEvent;
			}
		}

		// Token: 0x060006D8 RID: 1752 RVA: 0x00023128 File Offset: 0x00021328
		public override void OnMarriageOfferedToPlayer(Hero suitor, Hero maiden)
		{
			CampaignEvents.Instance._onMarriageOfferedToPlayerEvent.Invoke(suitor, maiden);
		}

		// Token: 0x17000119 RID: 281
		// (get) Token: 0x060006D9 RID: 1753 RVA: 0x0002313B File Offset: 0x0002133B
		public static IMbEvent<Hero, Hero> OnMarriageOfferCanceledEvent
		{
			get
			{
				return CampaignEvents.Instance._onMarriageOfferCanceledEvent;
			}
		}

		// Token: 0x060006DA RID: 1754 RVA: 0x00023147 File Offset: 0x00021347
		public override void OnMarriageOfferCanceled(Hero suitor, Hero maiden)
		{
			CampaignEvents.Instance._onMarriageOfferCanceledEvent.Invoke(suitor, maiden);
		}

		// Token: 0x1700011A RID: 282
		// (get) Token: 0x060006DB RID: 1755 RVA: 0x0002315A File Offset: 0x0002135A
		public static IMbEvent<Kingdom> OnVassalOrMercenaryServiceOfferedToPlayerEvent
		{
			get
			{
				return CampaignEvents.Instance._onVassalOrMercenaryServiceOfferedToPlayerEvent;
			}
		}

		// Token: 0x060006DC RID: 1756 RVA: 0x00023166 File Offset: 0x00021366
		public override void OnVassalOrMercenaryServiceOfferedToPlayer(Kingdom offeredKingdom)
		{
			CampaignEvents.Instance._onVassalOrMercenaryServiceOfferedToPlayerEvent.Invoke(offeredKingdom);
		}

		// Token: 0x1700011B RID: 283
		// (get) Token: 0x060006DD RID: 1757 RVA: 0x00023178 File Offset: 0x00021378
		public static IMbEvent<Kingdom> OnVassalOrMercenaryServiceOfferCanceledEvent
		{
			get
			{
				return CampaignEvents.Instance._onVassalOrMercenaryServiceOfferCanceledEvent;
			}
		}

		// Token: 0x060006DE RID: 1758 RVA: 0x00023184 File Offset: 0x00021384
		public override void OnVassalOrMercenaryServiceOfferCanceled(Kingdom offeredKingdom)
		{
			CampaignEvents.Instance._onVassalOrMercenaryServiceOfferCanceledEvent.Invoke(offeredKingdom);
		}

		// Token: 0x1700011C RID: 284
		// (get) Token: 0x060006DF RID: 1759 RVA: 0x00023196 File Offset: 0x00021396
		public static IMbEvent<Clan, StartMercenaryServiceAction.StartMercenaryServiceActionDetails> OnMercenaryServiceStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMercenaryServiceStartedEvent;
			}
		}

		// Token: 0x060006E0 RID: 1760 RVA: 0x000231A2 File Offset: 0x000213A2
		public override void OnMercenaryServiceStarted(Clan mercenaryClan, StartMercenaryServiceAction.StartMercenaryServiceActionDetails details)
		{
			CampaignEvents.Instance._onMercenaryServiceStartedEvent.Invoke(mercenaryClan, details);
		}

		// Token: 0x1700011D RID: 285
		// (get) Token: 0x060006E1 RID: 1761 RVA: 0x000231B5 File Offset: 0x000213B5
		public static IMbEvent<Clan, EndMercenaryServiceAction.EndMercenaryServiceActionDetails> OnMercenaryServiceEndedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMercenaryServiceEndedEvent;
			}
		}

		// Token: 0x060006E2 RID: 1762 RVA: 0x000231C1 File Offset: 0x000213C1
		public override void OnMercenaryServiceEnded(Clan mercenaryClan, EndMercenaryServiceAction.EndMercenaryServiceActionDetails details)
		{
			CampaignEvents.Instance._onMercenaryServiceEndedEvent.Invoke(mercenaryClan, details);
		}

		// Token: 0x1700011E RID: 286
		// (get) Token: 0x060006E3 RID: 1763 RVA: 0x000231D4 File Offset: 0x000213D4
		public static IMbEvent<IMission> OnMissionStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMissionStartedEvent;
			}
		}

		// Token: 0x060006E4 RID: 1764 RVA: 0x000231E0 File Offset: 0x000213E0
		public override void OnMissionStarted(IMission mission)
		{
			CampaignEvents.Instance._onMissionStartedEvent.Invoke(mission);
		}

		// Token: 0x1700011F RID: 287
		// (get) Token: 0x060006E5 RID: 1765 RVA: 0x000231F2 File Offset: 0x000213F2
		public static IMbEvent BeforeMissionOpenedEvent
		{
			get
			{
				return CampaignEvents.Instance._beforeMissionOpenedEvent;
			}
		}

		// Token: 0x060006E6 RID: 1766 RVA: 0x000231FE File Offset: 0x000213FE
		public override void BeforeMissionOpened()
		{
			CampaignEvents.Instance._beforeMissionOpenedEvent.Invoke();
		}

		// Token: 0x17000120 RID: 288
		// (get) Token: 0x060006E7 RID: 1767 RVA: 0x0002320F File Offset: 0x0002140F
		public static IMbEvent<PartyBase> OnPartyRemovedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyRemovedEvent;
			}
		}

		// Token: 0x060006E8 RID: 1768 RVA: 0x0002321B File Offset: 0x0002141B
		public override void OnPartyRemoved(PartyBase party)
		{
			CampaignEvents.Instance._onPartyRemovedEvent.Invoke(party);
		}

		// Token: 0x17000121 RID: 289
		// (get) Token: 0x060006E9 RID: 1769 RVA: 0x0002322D File Offset: 0x0002142D
		public static IMbEvent<PartyBase> OnPartySizeChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartySizeChangedEvent;
			}
		}

		// Token: 0x060006EA RID: 1770 RVA: 0x00023239 File Offset: 0x00021439
		public override void OnPartySizeChanged(PartyBase party)
		{
			CampaignEvents.Instance._onPartySizeChangedEvent.Invoke(party);
		}

		// Token: 0x17000122 RID: 290
		// (get) Token: 0x060006EB RID: 1771 RVA: 0x0002324B File Offset: 0x0002144B
		public static IMbEvent<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail> OnSettlementOwnerChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onSettlementOwnerChangedEvent;
			}
		}

		// Token: 0x060006EC RID: 1772 RVA: 0x00023257 File Offset: 0x00021457
		public override void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
		{
			CampaignEvents.Instance._onSettlementOwnerChangedEvent.Invoke(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail);
		}

		// Token: 0x17000123 RID: 291
		// (get) Token: 0x060006ED RID: 1773 RVA: 0x00023271 File Offset: 0x00021471
		public static IMbEvent<Town, Hero, Hero> OnGovernorChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onGovernorChangedEvent;
			}
		}

		// Token: 0x060006EE RID: 1774 RVA: 0x0002327D File Offset: 0x0002147D
		public override void OnGovernorChanged(Town fortification, Hero oldGovernor, Hero newGovernor)
		{
			CampaignEvents.Instance._onGovernorChangedEvent.Invoke(fortification, oldGovernor, newGovernor);
		}

		// Token: 0x17000124 RID: 292
		// (get) Token: 0x060006EF RID: 1775 RVA: 0x00023291 File Offset: 0x00021491
		public static IMbEvent<MobileParty, Settlement> OnSettlementLeftEvent
		{
			get
			{
				return CampaignEvents.Instance._onSettlementLeftEvent;
			}
		}

		// Token: 0x060006F0 RID: 1776 RVA: 0x0002329D File Offset: 0x0002149D
		public override void OnSettlementLeft(MobileParty party, Settlement settlement)
		{
			CampaignEvents.Instance._onSettlementLeftEvent.Invoke(party, settlement);
		}

		// Token: 0x17000125 RID: 293
		// (get) Token: 0x060006F1 RID: 1777 RVA: 0x000232B0 File Offset: 0x000214B0
		public static IMbEvent WeeklyTickEvent
		{
			get
			{
				return CampaignEvents.Instance._weeklyTickEvent;
			}
		}

		// Token: 0x060006F2 RID: 1778 RVA: 0x000232BC File Offset: 0x000214BC
		public override void WeeklyTick()
		{
			CampaignEvents.Instance._weeklyTickEvent.Invoke();
		}

		// Token: 0x17000126 RID: 294
		// (get) Token: 0x060006F3 RID: 1779 RVA: 0x000232CD File Offset: 0x000214CD
		public static IMbEvent DailyTickEvent
		{
			get
			{
				return CampaignEvents.Instance._dailyTickEvent;
			}
		}

		// Token: 0x060006F4 RID: 1780 RVA: 0x000232D9 File Offset: 0x000214D9
		public override void DailyTick()
		{
			CampaignEvents.Instance._dailyTickEvent.Invoke();
		}

		// Token: 0x17000127 RID: 295
		// (get) Token: 0x060006F5 RID: 1781 RVA: 0x000232EA File Offset: 0x000214EA
		public static IMbEvent<MobileParty> DailyTickPartyEvent
		{
			get
			{
				return CampaignEvents.Instance._dailyTickPartyEvent;
			}
		}

		// Token: 0x060006F6 RID: 1782 RVA: 0x000232F6 File Offset: 0x000214F6
		public override void DailyTickParty(MobileParty mobileParty)
		{
			CampaignEvents.Instance._dailyTickPartyEvent.Invoke(mobileParty);
		}

		// Token: 0x17000128 RID: 296
		// (get) Token: 0x060006F7 RID: 1783 RVA: 0x00023308 File Offset: 0x00021508
		public static IMbEvent<Town> DailyTickTownEvent
		{
			get
			{
				return CampaignEvents.Instance._dailyTickTownEvent;
			}
		}

		// Token: 0x060006F8 RID: 1784 RVA: 0x00023314 File Offset: 0x00021514
		public override void DailyTickTown(Town town)
		{
			CampaignEvents.Instance._dailyTickTownEvent.Invoke(town);
		}

		// Token: 0x17000129 RID: 297
		// (get) Token: 0x060006F9 RID: 1785 RVA: 0x00023326 File Offset: 0x00021526
		public static IMbEvent<Settlement> DailyTickSettlementEvent
		{
			get
			{
				return CampaignEvents.Instance._dailyTickSettlementEvent;
			}
		}

		// Token: 0x060006FA RID: 1786 RVA: 0x00023332 File Offset: 0x00021532
		public override void DailyTickSettlement(Settlement settlement)
		{
			CampaignEvents.Instance._dailyTickSettlementEvent.Invoke(settlement);
		}

		// Token: 0x1700012A RID: 298
		// (get) Token: 0x060006FB RID: 1787 RVA: 0x00023344 File Offset: 0x00021544
		public static IMbEvent<Hero> DailyTickHeroEvent
		{
			get
			{
				return CampaignEvents.Instance._dailyTickHeroEvent;
			}
		}

		// Token: 0x060006FC RID: 1788 RVA: 0x00023350 File Offset: 0x00021550
		public override void DailyTickHero(Hero hero)
		{
			CampaignEvents.Instance._dailyTickHeroEvent.Invoke(hero);
		}

		// Token: 0x1700012B RID: 299
		// (get) Token: 0x060006FD RID: 1789 RVA: 0x00023362 File Offset: 0x00021562
		public static IMbEvent<Clan> DailyTickClanEvent
		{
			get
			{
				return CampaignEvents.Instance._dailyTickClanEvent;
			}
		}

		// Token: 0x060006FE RID: 1790 RVA: 0x0002336E File Offset: 0x0002156E
		public override void DailyTickClan(Clan clan)
		{
			CampaignEvents.Instance._dailyTickClanEvent.Invoke(clan);
		}

		// Token: 0x1700012C RID: 300
		// (get) Token: 0x060006FF RID: 1791 RVA: 0x00023380 File Offset: 0x00021580
		public static IMbEvent<List<CampaignTutorial>> CollectAvailableTutorialsEvent
		{
			get
			{
				return CampaignEvents.Instance._collectAvailableTutorialsEvent;
			}
		}

		// Token: 0x06000700 RID: 1792 RVA: 0x0002338C File Offset: 0x0002158C
		public override void CollectAvailableTutorials(ref List<CampaignTutorial> tutorials)
		{
			CampaignEvents.Instance._collectAvailableTutorialsEvent.Invoke(tutorials);
		}

		// Token: 0x1700012D RID: 301
		// (get) Token: 0x06000701 RID: 1793 RVA: 0x0002339F File Offset: 0x0002159F
		public static IMbEvent<string> OnTutorialCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._onTutorialCompletedEvent;
			}
		}

		// Token: 0x06000702 RID: 1794 RVA: 0x000233AB File Offset: 0x000215AB
		public override void OnTutorialCompleted(string tutorial)
		{
			CampaignEvents.Instance._onTutorialCompletedEvent.Invoke(tutorial);
		}

		// Token: 0x1700012E RID: 302
		// (get) Token: 0x06000703 RID: 1795 RVA: 0x000233BD File Offset: 0x000215BD
		public static IMbEvent<Town, Building, int> OnBuildingLevelChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onBuildingLevelChangedEvent;
			}
		}

		// Token: 0x06000704 RID: 1796 RVA: 0x000233C9 File Offset: 0x000215C9
		public override void OnBuildingLevelChanged(Town town, Building building, int levelChange)
		{
			CampaignEvents.Instance._onBuildingLevelChangedEvent.Invoke(town, building, levelChange);
		}

		// Token: 0x1700012F RID: 303
		// (get) Token: 0x06000705 RID: 1797 RVA: 0x000233DD File Offset: 0x000215DD
		public static IMbEvent HourlyTickEvent
		{
			get
			{
				return CampaignEvents.Instance._hourlyTickEvent;
			}
		}

		// Token: 0x06000706 RID: 1798 RVA: 0x000233E9 File Offset: 0x000215E9
		public override void HourlyTick()
		{
			CampaignEvents.Instance._hourlyTickEvent.Invoke();
		}

		// Token: 0x17000130 RID: 304
		// (get) Token: 0x06000707 RID: 1799 RVA: 0x000233FA File Offset: 0x000215FA
		public static IMbEvent<MobileParty> HourlyTickPartyEvent
		{
			get
			{
				return CampaignEvents.Instance._hourlyTickPartyEvent;
			}
		}

		// Token: 0x06000708 RID: 1800 RVA: 0x00023406 File Offset: 0x00021606
		public override void HourlyTickParty(MobileParty mobileParty)
		{
			CampaignEvents.Instance._hourlyTickPartyEvent.Invoke(mobileParty);
		}

		// Token: 0x17000131 RID: 305
		// (get) Token: 0x06000709 RID: 1801 RVA: 0x00023418 File Offset: 0x00021618
		public static IMbEvent<Settlement> HourlyTickSettlementEvent
		{
			get
			{
				return CampaignEvents.Instance._hourlyTickSettlementEvent;
			}
		}

		// Token: 0x0600070A RID: 1802 RVA: 0x00023424 File Offset: 0x00021624
		public override void HourlyTickSettlement(Settlement settlement)
		{
			CampaignEvents.Instance._hourlyTickSettlementEvent.Invoke(settlement);
		}

		// Token: 0x17000132 RID: 306
		// (get) Token: 0x0600070B RID: 1803 RVA: 0x00023436 File Offset: 0x00021636
		public static IMbEvent<Clan> HourlyTickClanEvent
		{
			get
			{
				return CampaignEvents.Instance._hourlyTickClanEvent;
			}
		}

		// Token: 0x0600070C RID: 1804 RVA: 0x00023442 File Offset: 0x00021642
		public override void HourlyTickClan(Clan clan)
		{
			CampaignEvents.Instance._hourlyTickClanEvent.Invoke(clan);
		}

		// Token: 0x17000133 RID: 307
		// (get) Token: 0x0600070D RID: 1805 RVA: 0x00023454 File Offset: 0x00021654
		public static IMbEvent<float> TickEvent
		{
			get
			{
				return CampaignEvents.Instance._tickEvent;
			}
		}

		// Token: 0x0600070E RID: 1806 RVA: 0x00023460 File Offset: 0x00021660
		public override void Tick(float dt)
		{
			CampaignEvents.Instance._tickEvent.Invoke(dt);
		}

		// Token: 0x17000134 RID: 308
		// (get) Token: 0x0600070F RID: 1807 RVA: 0x00023472 File Offset: 0x00021672
		public static IMbEvent<CampaignGameStarter> OnSessionLaunchedEvent
		{
			get
			{
				return CampaignEvents.Instance._onSessionLaunchedEvent;
			}
		}

		// Token: 0x06000710 RID: 1808 RVA: 0x0002347E File Offset: 0x0002167E
		public override void OnSessionStart(CampaignGameStarter campaignGameStarter)
		{
			CampaignEvents.Instance._onSessionLaunchedEvent.Invoke(campaignGameStarter);
		}

		// Token: 0x17000135 RID: 309
		// (get) Token: 0x06000711 RID: 1809 RVA: 0x00023490 File Offset: 0x00021690
		public static IMbEvent<CampaignGameStarter> OnAfterSessionLaunchedEvent
		{
			get
			{
				return CampaignEvents.Instance._onAfterSessionLaunchedEvent;
			}
		}

		// Token: 0x06000712 RID: 1810 RVA: 0x0002349C File Offset: 0x0002169C
		public override void OnAfterSessionStart(CampaignGameStarter campaignGameStarter)
		{
			CampaignEvents.Instance._onAfterSessionLaunchedEvent.Invoke(campaignGameStarter);
		}

		// Token: 0x17000136 RID: 310
		// (get) Token: 0x06000713 RID: 1811 RVA: 0x000234AE File Offset: 0x000216AE
		public static IMbEvent<CampaignGameStarter> OnNewGameCreatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onNewGameCreatedEvent;
			}
		}

		// Token: 0x17000137 RID: 311
		// (get) Token: 0x06000714 RID: 1812 RVA: 0x000234BA File Offset: 0x000216BA
		public static IMbEvent<CampaignGameStarter, int> OnNewGameCreatedPartialFollowUpEvent
		{
			get
			{
				return CampaignEvents.Instance._onNewGameCreatedPartialFollowUpEvent;
			}
		}

		// Token: 0x06000715 RID: 1813 RVA: 0x000234C8 File Offset: 0x000216C8
		public override void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
		{
			CampaignEvents.Instance._onNewGameCreatedEvent.Invoke(campaignGameStarter);
			for (int i = 0; i < 100; i++)
			{
				CampaignEvents.Instance._onNewGameCreatedPartialFollowUpEvent.Invoke(campaignGameStarter, i);
			}
			CampaignEvents.Instance._onNewGameCreatedPartialFollowUpEndEvent.Invoke(campaignGameStarter);
		}

		// Token: 0x17000138 RID: 312
		// (get) Token: 0x06000716 RID: 1814 RVA: 0x00023513 File Offset: 0x00021713
		public static IMbEvent<CampaignGameStarter> OnNewGameCreatedPartialFollowUpEndEvent
		{
			get
			{
				return CampaignEvents.Instance._onNewGameCreatedPartialFollowUpEndEvent;
			}
		}

		// Token: 0x17000139 RID: 313
		// (get) Token: 0x06000717 RID: 1815 RVA: 0x0002351F File Offset: 0x0002171F
		public static IMbEvent<CampaignGameStarter> OnGameEarlyLoadedEvent
		{
			get
			{
				return CampaignEvents.Instance._onGameEarlyLoadedEvent;
			}
		}

		// Token: 0x06000718 RID: 1816 RVA: 0x0002352B File Offset: 0x0002172B
		public override void OnGameEarlyLoaded(CampaignGameStarter campaignGameStarter)
		{
			CampaignEvents.Instance._onGameEarlyLoadedEvent.Invoke(campaignGameStarter);
		}

		// Token: 0x1700013A RID: 314
		// (get) Token: 0x06000719 RID: 1817 RVA: 0x0002353D File Offset: 0x0002173D
		public static IMbEvent<CampaignGameStarter> OnGameLoadedEvent
		{
			get
			{
				return CampaignEvents.Instance._onGameLoadedEvent;
			}
		}

		// Token: 0x0600071A RID: 1818 RVA: 0x00023549 File Offset: 0x00021749
		public override void OnGameLoaded(CampaignGameStarter campaignGameStarter)
		{
			CampaignEvents.Instance._onGameLoadedEvent.Invoke(campaignGameStarter);
		}

		// Token: 0x1700013B RID: 315
		// (get) Token: 0x0600071B RID: 1819 RVA: 0x0002355B File Offset: 0x0002175B
		public static IMbEvent OnGameLoadFinishedEvent
		{
			get
			{
				return CampaignEvents.Instance._onGameLoadFinishedEvent;
			}
		}

		// Token: 0x0600071C RID: 1820 RVA: 0x00023567 File Offset: 0x00021767
		public override void OnGameLoadFinished()
		{
			CampaignEvents.Instance._onGameLoadFinishedEvent.Invoke();
		}

		// Token: 0x1700013C RID: 316
		// (get) Token: 0x0600071D RID: 1821 RVA: 0x00023578 File Offset: 0x00021778
		public static IMbEvent<MobileParty, PartyThinkParams> AiHourlyTickEvent
		{
			get
			{
				return CampaignEvents.Instance._aiHourlyTickEvent;
			}
		}

		// Token: 0x0600071E RID: 1822 RVA: 0x00023584 File Offset: 0x00021784
		public override void AiHourlyTick(MobileParty party, PartyThinkParams partyThinkParams)
		{
			CampaignEvents.Instance._aiHourlyTickEvent.Invoke(party, partyThinkParams);
		}

		// Token: 0x1700013D RID: 317
		// (get) Token: 0x0600071F RID: 1823 RVA: 0x00023597 File Offset: 0x00021797
		public static IMbEvent<MobileParty> TickPartialHourlyAiEvent
		{
			get
			{
				return CampaignEvents.Instance._tickPartialHourlyAiEvent;
			}
		}

		// Token: 0x06000720 RID: 1824 RVA: 0x000235A3 File Offset: 0x000217A3
		public override void TickPartialHourlyAi(MobileParty party)
		{
			CampaignEvents.Instance._tickPartialHourlyAiEvent.Invoke(party);
		}

		// Token: 0x1700013E RID: 318
		// (get) Token: 0x06000721 RID: 1825 RVA: 0x000235B5 File Offset: 0x000217B5
		public static IMbEvent<MobileParty> OnPartyJoinedArmyEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyJoinedArmyEvent;
			}
		}

		// Token: 0x06000722 RID: 1826 RVA: 0x000235C1 File Offset: 0x000217C1
		public override void OnPartyJoinedArmy(MobileParty mobileParty)
		{
			CampaignEvents.Instance._onPartyJoinedArmyEvent.Invoke(mobileParty);
		}

		// Token: 0x1700013F RID: 319
		// (get) Token: 0x06000723 RID: 1827 RVA: 0x000235D3 File Offset: 0x000217D3
		public static IMbEvent<MobileParty> PartyRemovedFromArmyEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyRemovedFromArmyEvent;
			}
		}

		// Token: 0x06000724 RID: 1828 RVA: 0x000235DF File Offset: 0x000217DF
		public override void OnPartyRemovedFromArmy(MobileParty mobileParty)
		{
			CampaignEvents.Instance._onPartyRemovedFromArmyEvent.Invoke(mobileParty);
		}

		// Token: 0x17000140 RID: 320
		// (get) Token: 0x06000725 RID: 1829 RVA: 0x000235F1 File Offset: 0x000217F1
		public static IMbEvent OnPlayerArmyLeaderChangedBehaviorEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerArmyLeaderChangedBehaviorEvent;
			}
		}

		// Token: 0x06000726 RID: 1830 RVA: 0x000235FD File Offset: 0x000217FD
		public override void OnPlayerArmyLeaderChangedBehavior()
		{
			CampaignEvents.Instance._onPlayerArmyLeaderChangedBehaviorEvent.Invoke();
		}

		// Token: 0x17000141 RID: 321
		// (get) Token: 0x06000727 RID: 1831 RVA: 0x0002360E File Offset: 0x0002180E
		public static IMbEvent<IMission> OnMissionEndedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMissionEndedEvent;
			}
		}

		// Token: 0x06000728 RID: 1832 RVA: 0x0002361A File Offset: 0x0002181A
		public override void OnMissionEnded(IMission mission)
		{
			CampaignEvents.Instance._onMissionEndedEvent.Invoke(mission);
		}

		// Token: 0x17000142 RID: 322
		// (get) Token: 0x06000729 RID: 1833 RVA: 0x0002362C File Offset: 0x0002182C
		public static IMbEvent<MobileParty> OnQuarterDailyPartyTick
		{
			get
			{
				return CampaignEvents.Instance._onQuarterDailyPartyTick;
			}
		}

		// Token: 0x0600072A RID: 1834 RVA: 0x00023638 File Offset: 0x00021838
		public override void QuarterDailyPartyTick(MobileParty mobileParty)
		{
			CampaignEvents.Instance._onQuarterDailyPartyTick.Invoke(mobileParty);
		}

		// Token: 0x17000143 RID: 323
		// (get) Token: 0x0600072B RID: 1835 RVA: 0x0002364A File Offset: 0x0002184A
		public static IMbEvent<MapEvent> OnPlayerBattleEndEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerBattleEndEvent;
			}
		}

		// Token: 0x0600072C RID: 1836 RVA: 0x00023656 File Offset: 0x00021856
		public override void OnPlayerBattleEnd(MapEvent mapEvent)
		{
			CampaignEvents.Instance._onPlayerBattleEndEvent.Invoke(mapEvent);
		}

		// Token: 0x17000144 RID: 324
		// (get) Token: 0x0600072D RID: 1837 RVA: 0x00023668 File Offset: 0x00021868
		public static IMbEvent<CharacterObject, int> OnUnitRecruitedEvent
		{
			get
			{
				return CampaignEvents.Instance._onUnitRecruitedEvent;
			}
		}

		// Token: 0x0600072E RID: 1838 RVA: 0x00023674 File Offset: 0x00021874
		public override void OnUnitRecruited(CharacterObject character, int amount)
		{
			CampaignEvents.Instance._onUnitRecruitedEvent.Invoke(character, amount);
		}

		// Token: 0x17000145 RID: 325
		// (get) Token: 0x0600072F RID: 1839 RVA: 0x00023687 File Offset: 0x00021887
		public static IMbEvent<Hero> OnChildConceivedEvent
		{
			get
			{
				return CampaignEvents.Instance._onChildConceived;
			}
		}

		// Token: 0x06000730 RID: 1840 RVA: 0x00023693 File Offset: 0x00021893
		public override void OnChildConceived(Hero mother)
		{
			CampaignEvents.Instance._onChildConceived.Invoke(mother);
		}

		// Token: 0x17000146 RID: 326
		// (get) Token: 0x06000731 RID: 1841 RVA: 0x000236A5 File Offset: 0x000218A5
		public static IMbEvent<Hero, List<Hero>, int> OnGivenBirthEvent
		{
			get
			{
				return CampaignEvents.Instance._onGivenBirthEvent;
			}
		}

		// Token: 0x06000732 RID: 1842 RVA: 0x000236B1 File Offset: 0x000218B1
		public override void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
		{
			CampaignEvents.Instance._onGivenBirthEvent.Invoke(mother, aliveChildren, stillbornCount);
		}

		// Token: 0x17000147 RID: 327
		// (get) Token: 0x06000733 RID: 1843 RVA: 0x000236C5 File Offset: 0x000218C5
		public static IMbEvent<float> MissionTickEvent
		{
			get
			{
				return CampaignEvents.Instance._missionTickEvent;
			}
		}

		// Token: 0x06000734 RID: 1844 RVA: 0x000236D1 File Offset: 0x000218D1
		public override void MissionTick(float dt)
		{
			CampaignEvents.Instance._missionTickEvent.Invoke(dt);
		}

		// Token: 0x17000148 RID: 328
		// (get) Token: 0x06000735 RID: 1845 RVA: 0x000236E4 File Offset: 0x000218E4
		public static IMbEvent ArmyOverlaySetDirtyEvent
		{
			get
			{
				MbEvent result;
				if ((result = CampaignEvents.Instance._armyOverlaySetDirty) == null)
				{
					result = (CampaignEvents.Instance._armyOverlaySetDirty = new MbEvent());
				}
				return result;
			}
		}

		// Token: 0x06000736 RID: 1846 RVA: 0x00023711 File Offset: 0x00021911
		public override void OnArmyOverlaySetDirty()
		{
			if (CampaignEvents.Instance._armyOverlaySetDirty == null)
			{
				CampaignEvents.Instance._armyOverlaySetDirty = new MbEvent();
			}
			CampaignEvents.Instance._armyOverlaySetDirty.Invoke();
		}

		// Token: 0x17000149 RID: 329
		// (get) Token: 0x06000737 RID: 1847 RVA: 0x0002373D File Offset: 0x0002193D
		public static IMbEvent<int> PlayerDesertedBattleEvent
		{
			get
			{
				return CampaignEvents.Instance._playerDesertedBattle;
			}
		}

		// Token: 0x06000738 RID: 1848 RVA: 0x00023749 File Offset: 0x00021949
		public override void OnPlayerDesertedBattle(int sacrificedMenCount)
		{
			CampaignEvents.Instance._playerDesertedBattle.Invoke(sacrificedMenCount);
		}

		// Token: 0x1700014A RID: 330
		// (get) Token: 0x06000739 RID: 1849 RVA: 0x0002375C File Offset: 0x0002195C
		public static IMbEvent<PartyBase> PartyVisibilityChangedEvent
		{
			get
			{
				MbEvent<PartyBase> result;
				if ((result = CampaignEvents.Instance._partyVisibilityChanged) == null)
				{
					result = (CampaignEvents.Instance._partyVisibilityChanged = new MbEvent<PartyBase>());
				}
				return result;
			}
		}

		// Token: 0x0600073A RID: 1850 RVA: 0x00023789 File Offset: 0x00021989
		public override void OnPartyVisibilityChanged(PartyBase party)
		{
			if (CampaignEvents.Instance._partyVisibilityChanged == null)
			{
				CampaignEvents.Instance._partyVisibilityChanged = new MbEvent<PartyBase>();
			}
			CampaignEvents.Instance._partyVisibilityChanged.Invoke(party);
		}

		// Token: 0x1700014B RID: 331
		// (get) Token: 0x0600073B RID: 1851 RVA: 0x000237B6 File Offset: 0x000219B6
		public static IMbEvent<Track> TrackDetectedEvent
		{
			get
			{
				return CampaignEvents.Instance._trackDetectedEvent;
			}
		}

		// Token: 0x0600073C RID: 1852 RVA: 0x000237C2 File Offset: 0x000219C2
		public override void TrackDetected(Track track)
		{
			CampaignEvents.Instance._trackDetectedEvent.Invoke(track);
		}

		// Token: 0x1700014C RID: 332
		// (get) Token: 0x0600073D RID: 1853 RVA: 0x000237D4 File Offset: 0x000219D4
		public static IMbEvent<Track> TrackLostEvent
		{
			get
			{
				return CampaignEvents.Instance._trackLostEvent;
			}
		}

		// Token: 0x0600073E RID: 1854 RVA: 0x000237E0 File Offset: 0x000219E0
		public override void TrackLost(Track track)
		{
			CampaignEvents.Instance._trackLostEvent.Invoke(track);
		}

		// Token: 0x1700014D RID: 333
		// (get) Token: 0x0600073F RID: 1855 RVA: 0x000237F2 File Offset: 0x000219F2
		public static IMbEvent<Dictionary<string, int>> LocationCharactersAreReadyToSpawnEvent
		{
			get
			{
				return CampaignEvents.Instance._locationCharactersAreReadyToSpawn;
			}
		}

		// Token: 0x06000740 RID: 1856 RVA: 0x00023800 File Offset: 0x00021A00
		public override void LocationCharactersAreReadyToSpawn(Dictionary<string, int> unusedUsablePointCount)
		{
			foreach (KeyValuePair<string, int> keyValuePair in unusedUsablePointCount)
			{
			}
			CampaignEvents.Instance._locationCharactersAreReadyToSpawn.Invoke(unusedUsablePointCount);
		}

		// Token: 0x1700014E RID: 334
		// (get) Token: 0x06000741 RID: 1857 RVA: 0x00023858 File Offset: 0x00021A58
		public static ReferenceIMBEvent<MatrixFrame> BeforePlayerAgentSpawnEvent
		{
			get
			{
				return CampaignEvents.Instance._onBeforePlayerAgentSpawn;
			}
		}

		// Token: 0x06000742 RID: 1858 RVA: 0x00023864 File Offset: 0x00021A64
		public override void OnBeforePlayerAgentSpawn(ref MatrixFrame spawnFrame)
		{
			CampaignEvents.Instance._onBeforePlayerAgentSpawn.Invoke(ref spawnFrame);
		}

		// Token: 0x1700014F RID: 335
		// (get) Token: 0x06000743 RID: 1859 RVA: 0x00023876 File Offset: 0x00021A76
		public static IMbEvent PlayerAgentSpawned
		{
			get
			{
				return CampaignEvents.Instance._onPlayerAgentSpawned;
			}
		}

		// Token: 0x06000744 RID: 1860 RVA: 0x00023882 File Offset: 0x00021A82
		public override void OnPlayerAgentSpawned()
		{
			CampaignEvents.Instance._onPlayerAgentSpawned.Invoke();
		}

		// Token: 0x17000150 RID: 336
		// (get) Token: 0x06000745 RID: 1861 RVA: 0x00023893 File Offset: 0x00021A93
		public static IMbEvent LocationCharactersSimulatedEvent
		{
			get
			{
				return CampaignEvents.Instance._locationCharactersSimulatedSpawned;
			}
		}

		// Token: 0x06000746 RID: 1862 RVA: 0x0002389F File Offset: 0x00021A9F
		public override void LocationCharactersSimulated()
		{
			CampaignEvents.Instance._locationCharactersSimulatedSpawned.Invoke();
		}

		// Token: 0x17000151 RID: 337
		// (get) Token: 0x06000747 RID: 1863 RVA: 0x000238B0 File Offset: 0x00021AB0
		public static IMbEvent<CharacterObject, CharacterObject, int> PlayerUpgradedTroopsEvent
		{
			get
			{
				return CampaignEvents.Instance._playerUpgradedTroopsEvent;
			}
		}

		// Token: 0x06000748 RID: 1864 RVA: 0x000238BC File Offset: 0x00021ABC
		public override void OnPlayerUpgradedTroops(CharacterObject upgradeFromTroop, CharacterObject upgradeToTroop, int number)
		{
			CampaignEvents.Instance._playerUpgradedTroopsEvent.Invoke(upgradeFromTroop, upgradeToTroop, number);
		}

		// Token: 0x17000152 RID: 338
		// (get) Token: 0x06000749 RID: 1865 RVA: 0x000238D0 File Offset: 0x00021AD0
		public static IMbEvent<CharacterObject, CharacterObject, PartyBase, WeaponComponentData, bool, int> OnHeroCombatHitEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeroCombatHitEvent;
			}
		}

		// Token: 0x0600074A RID: 1866 RVA: 0x000238DC File Offset: 0x00021ADC
		public override void OnHeroCombatHit(CharacterObject attackerTroop, CharacterObject attackedTroop, PartyBase party, WeaponComponentData usedWeapon, bool isFatal, int xp)
		{
			CampaignEvents.Instance._onHeroCombatHitEvent.Invoke(attackerTroop, attackedTroop, party, usedWeapon, isFatal, xp);
		}

		// Token: 0x17000153 RID: 339
		// (get) Token: 0x0600074B RID: 1867 RVA: 0x000238F6 File Offset: 0x00021AF6
		public static IMbEvent<CharacterObject> CharacterPortraitPopUpOpenedEvent
		{
			get
			{
				return CampaignEvents.Instance._characterPortraitPopUpOpenedEvent;
			}
		}

		// Token: 0x0600074C RID: 1868 RVA: 0x00023902 File Offset: 0x00021B02
		public override void OnCharacterPortraitPopUpOpened(CharacterObject character)
		{
			this._timeControlModeBeforePopUpOpened = Campaign.Current.TimeControlMode;
			Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
			Campaign.Current.SetTimeControlModeLock(true);
			CampaignEvents.Instance._characterPortraitPopUpOpenedEvent.Invoke(character);
		}

		// Token: 0x17000154 RID: 340
		// (get) Token: 0x0600074D RID: 1869 RVA: 0x0002393A File Offset: 0x00021B3A
		public static IMbEvent CharacterPortraitPopUpClosedEvent
		{
			get
			{
				return CampaignEvents.Instance._characterPortraitPopUpClosedEvent;
			}
		}

		// Token: 0x0600074E RID: 1870 RVA: 0x00023946 File Offset: 0x00021B46
		public override void OnCharacterPortraitPopUpClosed()
		{
			Campaign.Current.SetTimeControlModeLock(false);
			Campaign.Current.TimeControlMode = this._timeControlModeBeforePopUpOpened;
			this._timeControlModeBeforePopUpOpened = CampaignTimeControlMode.Stop;
			CampaignEvents.Instance._characterPortraitPopUpClosedEvent.Invoke();
		}

		// Token: 0x17000155 RID: 341
		// (get) Token: 0x0600074F RID: 1871 RVA: 0x00023979 File Offset: 0x00021B79
		public static IMbEvent<Hero> PlayerStartTalkFromMenu
		{
			get
			{
				return CampaignEvents.Instance._playerStartTalkFromMenu;
			}
		}

		// Token: 0x06000750 RID: 1872 RVA: 0x00023985 File Offset: 0x00021B85
		public override void OnPlayerStartTalkFromMenu(Hero hero)
		{
			CampaignEvents.Instance._playerStartTalkFromMenu.Invoke(hero);
		}

		// Token: 0x17000156 RID: 342
		// (get) Token: 0x06000751 RID: 1873 RVA: 0x00023997 File Offset: 0x00021B97
		public static IMbEvent<GameMenu, GameMenuOption> GameMenuOptionSelectedEvent
		{
			get
			{
				return CampaignEvents.Instance._gameMenuOptionSelectedEvent;
			}
		}

		// Token: 0x06000752 RID: 1874 RVA: 0x000239A3 File Offset: 0x00021BA3
		public override void OnGameMenuOptionSelected(GameMenu gameMenu, GameMenuOption gameMenuOption)
		{
			CampaignEvents.Instance._gameMenuOptionSelectedEvent.Invoke(gameMenu, gameMenuOption);
		}

		// Token: 0x17000157 RID: 343
		// (get) Token: 0x06000753 RID: 1875 RVA: 0x000239B6 File Offset: 0x00021BB6
		public static IMbEvent<CharacterObject> PlayerStartRecruitmentEvent
		{
			get
			{
				return CampaignEvents.Instance._playerStartRecruitmentEvent;
			}
		}

		// Token: 0x06000754 RID: 1876 RVA: 0x000239C2 File Offset: 0x00021BC2
		public override void OnPlayerStartRecruitment(CharacterObject recruitTroopCharacter)
		{
			CampaignEvents.Instance._playerStartRecruitmentEvent.Invoke(recruitTroopCharacter);
		}

		// Token: 0x17000158 RID: 344
		// (get) Token: 0x06000755 RID: 1877 RVA: 0x000239D4 File Offset: 0x00021BD4
		public static IMbEvent<Hero, Hero> OnBeforePlayerCharacterChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onBeforePlayerCharacterChangedEvent;
			}
		}

		// Token: 0x06000756 RID: 1878 RVA: 0x000239E0 File Offset: 0x00021BE0
		public override void OnBeforePlayerCharacterChanged(Hero oldPlayer, Hero newPlayer)
		{
			CampaignEvents.Instance._onBeforePlayerCharacterChangedEvent.Invoke(oldPlayer, newPlayer);
		}

		// Token: 0x17000159 RID: 345
		// (get) Token: 0x06000757 RID: 1879 RVA: 0x000239F3 File Offset: 0x00021BF3
		public static IMbEvent<Hero, Hero, MobileParty, bool> OnPlayerCharacterChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerCharacterChangedEvent;
			}
		}

		// Token: 0x06000758 RID: 1880 RVA: 0x000239FF File Offset: 0x00021BFF
		public override void OnPlayerCharacterChanged(Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
		{
			CampaignEvents.Instance._onPlayerCharacterChangedEvent.Invoke(oldPlayer, newPlayer, newMainParty, isMainPartyChanged);
		}

		// Token: 0x1700015A RID: 346
		// (get) Token: 0x06000759 RID: 1881 RVA: 0x00023A15 File Offset: 0x00021C15
		public static IMbEvent<Hero, Hero> OnClanLeaderChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onClanLeaderChangedEvent;
			}
		}

		// Token: 0x0600075A RID: 1882 RVA: 0x00023A21 File Offset: 0x00021C21
		public override void OnClanLeaderChanged(Hero oldLeader, Hero newLeader)
		{
			CampaignEvents.Instance._onClanLeaderChangedEvent.Invoke(oldLeader, newLeader);
		}

		// Token: 0x1700015B RID: 347
		// (get) Token: 0x0600075B RID: 1883 RVA: 0x00023A34 File Offset: 0x00021C34
		public static IMbEvent<SiegeEvent> OnSiegeEventStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onSiegeEventStartedEvent;
			}
		}

		// Token: 0x0600075C RID: 1884 RVA: 0x00023A40 File Offset: 0x00021C40
		public override void OnSiegeEventStarted(SiegeEvent siegeEvent)
		{
			CampaignEvents.Instance._onSiegeEventStartedEvent.Invoke(siegeEvent);
		}

		// Token: 0x1700015C RID: 348
		// (get) Token: 0x0600075D RID: 1885 RVA: 0x00023A52 File Offset: 0x00021C52
		public static IMbEvent OnPlayerSiegeStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerSiegeStartedEvent;
			}
		}

		// Token: 0x0600075E RID: 1886 RVA: 0x00023A5E File Offset: 0x00021C5E
		public override void OnPlayerSiegeStarted()
		{
			CampaignEvents.Instance._onPlayerSiegeStartedEvent.Invoke();
		}

		// Token: 0x1700015D RID: 349
		// (get) Token: 0x0600075F RID: 1887 RVA: 0x00023A6F File Offset: 0x00021C6F
		public static IMbEvent<SiegeEvent> OnSiegeEventEndedEvent
		{
			get
			{
				return CampaignEvents.Instance._onSiegeEventEndedEvent;
			}
		}

		// Token: 0x06000760 RID: 1888 RVA: 0x00023A7B File Offset: 0x00021C7B
		public override void OnSiegeEventEnded(SiegeEvent siegeEvent)
		{
			CampaignEvents.Instance._onSiegeEventEndedEvent.Invoke(siegeEvent);
		}

		// Token: 0x1700015E RID: 350
		// (get) Token: 0x06000761 RID: 1889 RVA: 0x00023A8D File Offset: 0x00021C8D
		public static IMbEvent<MobileParty, Settlement, SiegeAftermathAction.SiegeAftermath, Clan, Dictionary<MobileParty, float>> OnSiegeAftermathAppliedEvent
		{
			get
			{
				return CampaignEvents.Instance._siegeAftermathAppliedEvent;
			}
		}

		// Token: 0x06000762 RID: 1890 RVA: 0x00023A99 File Offset: 0x00021C99
		public override void OnSiegeAftermathApplied(MobileParty attackerParty, Settlement settlement, SiegeAftermathAction.SiegeAftermath aftermathType, Clan previousSettlementOwner, Dictionary<MobileParty, float> partyContributions)
		{
			CampaignEvents.Instance._siegeAftermathAppliedEvent.Invoke(attackerParty, settlement, aftermathType, previousSettlementOwner, partyContributions);
		}

		// Token: 0x1700015F RID: 351
		// (get) Token: 0x06000763 RID: 1891 RVA: 0x00023AB1 File Offset: 0x00021CB1
		public static IMbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, SiegeBombardTargets> OnSiegeBombardmentHitEvent
		{
			get
			{
				return CampaignEvents.Instance._onSiegeBombardmentHitEvent;
			}
		}

		// Token: 0x06000764 RID: 1892 RVA: 0x00023ABD File Offset: 0x00021CBD
		public override void OnSiegeBombardmentHit(MobileParty besiegerParty, Settlement besiegedSettlement, BattleSideEnum side, SiegeEngineType weapon, SiegeBombardTargets target)
		{
			CampaignEvents.Instance._onSiegeBombardmentHitEvent.Invoke(besiegerParty, besiegedSettlement, side, weapon, target);
		}

		// Token: 0x17000160 RID: 352
		// (get) Token: 0x06000765 RID: 1893 RVA: 0x00023AD5 File Offset: 0x00021CD5
		public static IMbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, bool> OnSiegeBombardmentWallHitEvent
		{
			get
			{
				return CampaignEvents.Instance._onSiegeBombardmentWallHitEvent;
			}
		}

		// Token: 0x06000766 RID: 1894 RVA: 0x00023AE1 File Offset: 0x00021CE1
		public override void OnSiegeBombardmentWallHit(MobileParty besiegerParty, Settlement besiegedSettlement, BattleSideEnum side, SiegeEngineType weapon, bool isWallCracked)
		{
			CampaignEvents.Instance._onSiegeBombardmentWallHitEvent.Invoke(besiegerParty, besiegedSettlement, side, weapon, isWallCracked);
		}

		// Token: 0x17000161 RID: 353
		// (get) Token: 0x06000767 RID: 1895 RVA: 0x00023AF9 File Offset: 0x00021CF9
		public static IMbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType> OnSiegeEngineDestroyedEvent
		{
			get
			{
				return CampaignEvents.Instance._onSiegeEngineDestroyedEvent;
			}
		}

		// Token: 0x06000768 RID: 1896 RVA: 0x00023B05 File Offset: 0x00021D05
		public override void OnSiegeEngineDestroyed(MobileParty besiegerParty, Settlement besiegedSettlement, BattleSideEnum side, SiegeEngineType destroyedEngine)
		{
			CampaignEvents.Instance._onSiegeEngineDestroyedEvent.Invoke(besiegerParty, besiegedSettlement, side, destroyedEngine);
		}

		// Token: 0x17000162 RID: 354
		// (get) Token: 0x06000769 RID: 1897 RVA: 0x00023B1B File Offset: 0x00021D1B
		public static IMbEvent<List<TradeRumor>, Settlement> OnTradeRumorIsTakenEvent
		{
			get
			{
				return CampaignEvents.Instance._onTradeRumorIsTakenEvent;
			}
		}

		// Token: 0x0600076A RID: 1898 RVA: 0x00023B27 File Offset: 0x00021D27
		public override void OnTradeRumorIsTaken(List<TradeRumor> newRumors, Settlement sourceSettlement = null)
		{
			CampaignEvents.Instance._onTradeRumorIsTakenEvent.Invoke(newRumors, sourceSettlement);
		}

		// Token: 0x17000163 RID: 355
		// (get) Token: 0x0600076B RID: 1899 RVA: 0x00023B3A File Offset: 0x00021D3A
		public static IMbEvent<Hero> OnCheckForIssueEvent
		{
			get
			{
				return CampaignEvents.Instance._onCheckForIssueEvent;
			}
		}

		// Token: 0x0600076C RID: 1900 RVA: 0x00023B46 File Offset: 0x00021D46
		public override void OnCheckForIssue(Hero hero)
		{
			CampaignEvents.Instance._onCheckForIssueEvent.Invoke(hero);
		}

		// Token: 0x17000164 RID: 356
		// (get) Token: 0x0600076D RID: 1901 RVA: 0x00023B58 File Offset: 0x00021D58
		public static IMbEvent<IssueBase, IssueBase.IssueUpdateDetails, Hero> OnIssueUpdatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onIssueUpdatedEvent;
			}
		}

		// Token: 0x0600076E RID: 1902 RVA: 0x00023B64 File Offset: 0x00021D64
		public override void OnIssueUpdated(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero issueSolver = null)
		{
			CampaignEvents.Instance._onIssueUpdatedEvent.Invoke(issue, details, issueSolver);
		}

		// Token: 0x17000165 RID: 357
		// (get) Token: 0x0600076F RID: 1903 RVA: 0x00023B78 File Offset: 0x00021D78
		public static IMbEvent<MobileParty, TroopRoster> OnTroopsDesertedEvent
		{
			get
			{
				return CampaignEvents.Instance._onTroopsDesertedEvent;
			}
		}

		// Token: 0x06000770 RID: 1904 RVA: 0x00023B84 File Offset: 0x00021D84
		public override void OnTroopsDeserted(MobileParty mobileParty, TroopRoster desertedTroops)
		{
			CampaignEvents.Instance._onTroopsDesertedEvent.Invoke(mobileParty, desertedTroops);
		}

		// Token: 0x17000166 RID: 358
		// (get) Token: 0x06000771 RID: 1905 RVA: 0x00023B97 File Offset: 0x00021D97
		public static IMbEvent<Hero, Settlement, Hero, CharacterObject, int> OnTroopRecruitedEvent
		{
			get
			{
				return CampaignEvents.Instance._onTroopRecruitedEvent;
			}
		}

		// Token: 0x06000772 RID: 1906 RVA: 0x00023BA3 File Offset: 0x00021DA3
		public override void OnTroopRecruited(Hero recruiterHero, Settlement recruitmentSettlement, Hero recruitmentSource, CharacterObject troop, int amount)
		{
			CampaignEvents.Instance._onTroopRecruitedEvent.Invoke(recruiterHero, recruitmentSettlement, recruitmentSource, troop, amount);
		}

		// Token: 0x17000167 RID: 359
		// (get) Token: 0x06000773 RID: 1907 RVA: 0x00023BBB File Offset: 0x00021DBB
		public static IMbEvent<Hero, Settlement, TroopRoster> OnTroopGivenToSettlementEvent
		{
			get
			{
				return CampaignEvents.Instance._onTroopGivenToSettlementEvent;
			}
		}

		// Token: 0x06000774 RID: 1908 RVA: 0x00023BC7 File Offset: 0x00021DC7
		public override void OnTroopGivenToSettlement(Hero giverHero, Settlement recipientSettlement, TroopRoster roster)
		{
			CampaignEvents.Instance._onTroopGivenToSettlementEvent.Invoke(giverHero, recipientSettlement, roster);
		}

		// Token: 0x17000168 RID: 360
		// (get) Token: 0x06000775 RID: 1909 RVA: 0x00023BDB File Offset: 0x00021DDB
		public static IMbEvent<PartyBase, PartyBase, ItemRosterElement, int, Settlement> OnItemSoldEvent
		{
			get
			{
				return CampaignEvents.Instance._onItemSoldEvent;
			}
		}

		// Token: 0x06000776 RID: 1910 RVA: 0x00023BE7 File Offset: 0x00021DE7
		public override void OnItemSold(PartyBase receiverParty, PartyBase payerParty, ItemRosterElement itemRosterElement, int number, Settlement currentSettlement)
		{
			CampaignEvents.Instance._onItemSoldEvent.Invoke(receiverParty, payerParty, itemRosterElement, number, currentSettlement);
		}

		// Token: 0x17000169 RID: 361
		// (get) Token: 0x06000777 RID: 1911 RVA: 0x00023BFF File Offset: 0x00021DFF
		public static IMbEvent<MobileParty, Town, List<ValueTuple<EquipmentElement, int>>> OnCaravanTransactionCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._onCaravanTransactionCompletedEvent;
			}
		}

		// Token: 0x06000778 RID: 1912 RVA: 0x00023C0B File Offset: 0x00021E0B
		public override void OnCaravanTransactionCompleted(MobileParty caravanParty, Town town, List<ValueTuple<EquipmentElement, int>> itemRosterElements)
		{
			CampaignEvents.Instance._onCaravanTransactionCompletedEvent.Invoke(caravanParty, town, itemRosterElements);
		}

		// Token: 0x1700016A RID: 362
		// (get) Token: 0x06000779 RID: 1913 RVA: 0x00023C1F File Offset: 0x00021E1F
		public static IMbEvent<PartyBase, PartyBase, TroopRoster> OnPrisonerSoldEvent
		{
			get
			{
				return CampaignEvents.Instance._onPrisonerSoldEvent;
			}
		}

		// Token: 0x0600077A RID: 1914 RVA: 0x00023C2B File Offset: 0x00021E2B
		public override void OnPrisonerSold(PartyBase sellerParty, PartyBase buyerParty, TroopRoster prisoners)
		{
			CampaignEvents.Instance._onPrisonerSoldEvent.Invoke(sellerParty, buyerParty, prisoners);
		}

		// Token: 0x1700016B RID: 363
		// (get) Token: 0x0600077B RID: 1915 RVA: 0x00023C3F File Offset: 0x00021E3F
		public static IMbEvent<MobileParty> OnPartyDisbandStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyDisbandStartedEvent;
			}
		}

		// Token: 0x0600077C RID: 1916 RVA: 0x00023C4B File Offset: 0x00021E4B
		public override void OnPartyDisbandStarted(MobileParty disbandParty)
		{
			CampaignEvents.Instance._onPartyDisbandStartedEvent.Invoke(disbandParty);
		}

		// Token: 0x1700016C RID: 364
		// (get) Token: 0x0600077D RID: 1917 RVA: 0x00023C5D File Offset: 0x00021E5D
		public static IMbEvent<MobileParty, Settlement> OnPartyDisbandedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyDisbandedEvent;
			}
		}

		// Token: 0x0600077E RID: 1918 RVA: 0x00023C69 File Offset: 0x00021E69
		public override void OnPartyDisbanded(MobileParty disbandParty, Settlement relatedSettlement)
		{
			CampaignEvents.Instance._onPartyDisbandedEvent.Invoke(disbandParty, relatedSettlement);
		}

		// Token: 0x1700016D RID: 365
		// (get) Token: 0x0600077F RID: 1919 RVA: 0x00023C7C File Offset: 0x00021E7C
		public static IMbEvent<MobileParty> OnPartyDisbandCanceledEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyDisbandCanceledEvent;
			}
		}

		// Token: 0x06000780 RID: 1920 RVA: 0x00023C88 File Offset: 0x00021E88
		public override void OnPartyDisbandCanceled(MobileParty disbandParty)
		{
			CampaignEvents.Instance._onPartyDisbandCanceledEvent.Invoke(disbandParty);
		}

		// Token: 0x1700016E RID: 366
		// (get) Token: 0x06000781 RID: 1921 RVA: 0x00023C9A File Offset: 0x00021E9A
		public static IMbEvent<PartyBase, PartyBase> OnHideoutSpottedEvent
		{
			get
			{
				return CampaignEvents.Instance._hideoutSpottedEvent;
			}
		}

		// Token: 0x06000782 RID: 1922 RVA: 0x00023CA6 File Offset: 0x00021EA6
		public override void OnHideoutSpotted(PartyBase party, PartyBase hideoutParty)
		{
			CampaignEvents.Instance._hideoutSpottedEvent.Invoke(party, hideoutParty);
		}

		// Token: 0x1700016F RID: 367
		// (get) Token: 0x06000783 RID: 1923 RVA: 0x00023CB9 File Offset: 0x00021EB9
		public static IMbEvent<Settlement> OnHideoutDeactivatedEvent
		{
			get
			{
				return CampaignEvents.Instance._hideoutDeactivatedEvent;
			}
		}

		// Token: 0x06000784 RID: 1924 RVA: 0x00023CC5 File Offset: 0x00021EC5
		public override void OnHideoutDeactivated(Settlement hideout)
		{
			CampaignEvents.Instance._hideoutDeactivatedEvent.Invoke(hideout);
		}

		// Token: 0x17000170 RID: 368
		// (get) Token: 0x06000785 RID: 1925 RVA: 0x00023CD7 File Offset: 0x00021ED7
		public static IMbEvent<Hero, Hero, float> OnHeroSharedFoodWithAnotherHeroEvent
		{
			get
			{
				return CampaignEvents.Instance._heroSharedFoodWithAnotherHeroEvent;
			}
		}

		// Token: 0x06000786 RID: 1926 RVA: 0x00023CE3 File Offset: 0x00021EE3
		public override void OnHeroSharedFoodWithAnother(Hero supporterHero, Hero supportedHero, float influence)
		{
			CampaignEvents.Instance._heroSharedFoodWithAnotherHeroEvent.Invoke(supporterHero, supportedHero, influence);
		}

		// Token: 0x17000171 RID: 369
		// (get) Token: 0x06000787 RID: 1927 RVA: 0x00023CF7 File Offset: 0x00021EF7
		public static IMbEvent<List<ValueTuple<ItemRosterElement, int>>, List<ValueTuple<ItemRosterElement, int>>, bool> PlayerInventoryExchangeEvent
		{
			get
			{
				return CampaignEvents.Instance._playerInventoryExchangeEvent;
			}
		}

		// Token: 0x06000788 RID: 1928 RVA: 0x00023D03 File Offset: 0x00021F03
		public override void OnPlayerInventoryExchange(List<ValueTuple<ItemRosterElement, int>> purchasedItems, List<ValueTuple<ItemRosterElement, int>> soldItems, bool isTrading)
		{
			CampaignEvents.Instance._playerInventoryExchangeEvent.Invoke(purchasedItems, soldItems, isTrading);
		}

		// Token: 0x17000172 RID: 370
		// (get) Token: 0x06000789 RID: 1929 RVA: 0x00023D17 File Offset: 0x00021F17
		public static IMbEvent<ItemRoster> OnItemsDiscardedByPlayerEvent
		{
			get
			{
				return CampaignEvents.Instance._onItemsDiscardedByPlayerEvent;
			}
		}

		// Token: 0x0600078A RID: 1930 RVA: 0x00023D23 File Offset: 0x00021F23
		public override void OnItemsDiscardedByPlayer(ItemRoster discardedItems)
		{
			CampaignEvents.Instance._onItemsDiscardedByPlayerEvent.Invoke(discardedItems);
		}

		// Token: 0x17000173 RID: 371
		// (get) Token: 0x0600078B RID: 1931 RVA: 0x00023D35 File Offset: 0x00021F35
		public static IMbEvent<Tuple<PersuasionOptionArgs, PersuasionOptionResult>> PersuasionProgressCommittedEvent
		{
			get
			{
				return CampaignEvents.Instance._persuasionProgressCommittedEvent;
			}
		}

		// Token: 0x0600078C RID: 1932 RVA: 0x00023D41 File Offset: 0x00021F41
		public override void OnPersuasionProgressCommitted(Tuple<PersuasionOptionArgs, PersuasionOptionResult> progress)
		{
			CampaignEvents.Instance._persuasionProgressCommittedEvent.Invoke(progress);
		}

		// Token: 0x17000174 RID: 372
		// (get) Token: 0x0600078D RID: 1933 RVA: 0x00023D53 File Offset: 0x00021F53
		public static IMbEvent<QuestBase, QuestBase.QuestCompleteDetails> OnQuestCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._onQuestCompletedEvent;
			}
		}

		// Token: 0x0600078E RID: 1934 RVA: 0x00023D5F File Offset: 0x00021F5F
		public override void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
		{
			CampaignEvents.Instance._onQuestCompletedEvent.Invoke(quest, detail);
		}

		// Token: 0x17000175 RID: 373
		// (get) Token: 0x0600078F RID: 1935 RVA: 0x00023D72 File Offset: 0x00021F72
		public static IMbEvent<QuestBase> OnQuestStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onQuestStartedEvent;
			}
		}

		// Token: 0x06000790 RID: 1936 RVA: 0x00023D7E File Offset: 0x00021F7E
		public override void OnQuestStarted(QuestBase quest)
		{
			CampaignEvents.Instance._onQuestStartedEvent.Invoke(quest);
		}

		// Token: 0x17000176 RID: 374
		// (get) Token: 0x06000791 RID: 1937 RVA: 0x00023D90 File Offset: 0x00021F90
		public static IMbEvent<ItemObject, Settlement, int> OnItemProducedEvent
		{
			get
			{
				return CampaignEvents.Instance._itemProducedEvent;
			}
		}

		// Token: 0x06000792 RID: 1938 RVA: 0x00023D9C File Offset: 0x00021F9C
		public override void OnItemProduced(ItemObject itemObject, Settlement settlement, int count)
		{
			CampaignEvents.Instance._itemProducedEvent.Invoke(itemObject, settlement, count);
		}

		// Token: 0x17000177 RID: 375
		// (get) Token: 0x06000793 RID: 1939 RVA: 0x00023DB0 File Offset: 0x00021FB0
		public static IMbEvent<ItemObject, Settlement, int> OnItemConsumedEvent
		{
			get
			{
				return CampaignEvents.Instance._itemConsumedEvent;
			}
		}

		// Token: 0x06000794 RID: 1940 RVA: 0x00023DBC File Offset: 0x00021FBC
		public override void OnItemConsumed(ItemObject itemObject, Settlement settlement, int count)
		{
			CampaignEvents.Instance._itemConsumedEvent.Invoke(itemObject, settlement, count);
		}

		// Token: 0x17000178 RID: 376
		// (get) Token: 0x06000795 RID: 1941 RVA: 0x00023DD0 File Offset: 0x00021FD0
		public static IMbEvent<MobileParty> OnPartyConsumedFoodEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyConsumedFoodEvent;
			}
		}

		// Token: 0x06000796 RID: 1942 RVA: 0x00023DDC File Offset: 0x00021FDC
		public override void OnPartyConsumedFood(MobileParty party)
		{
			CampaignEvents.Instance._onPartyConsumedFoodEvent.Invoke(party);
		}

		// Token: 0x17000179 RID: 377
		// (get) Token: 0x06000797 RID: 1943 RVA: 0x00023DEE File Offset: 0x00021FEE
		public static IMbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool> OnBeforeMainCharacterDiedEvent
		{
			get
			{
				return CampaignEvents.Instance._onBeforeMainCharacterDiedEvent;
			}
		}

		// Token: 0x06000798 RID: 1944 RVA: 0x00023DFA File Offset: 0x00021FFA
		public override void OnBeforeMainCharacterDied(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
		{
			CampaignEvents.Instance._onBeforeMainCharacterDiedEvent.Invoke(victim, killer, detail, showNotification);
		}

		// Token: 0x1700017A RID: 378
		// (get) Token: 0x06000799 RID: 1945 RVA: 0x00023E10 File Offset: 0x00022010
		public static IMbEvent<IssueBase> OnNewIssueCreatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onNewIssueCreatedEvent;
			}
		}

		// Token: 0x0600079A RID: 1946 RVA: 0x00023E1C File Offset: 0x0002201C
		public override void OnNewIssueCreated(IssueBase issue)
		{
			CampaignEvents.Instance._onNewIssueCreatedEvent.Invoke(issue);
		}

		// Token: 0x1700017B RID: 379
		// (get) Token: 0x0600079B RID: 1947 RVA: 0x00023E2E File Offset: 0x0002202E
		public static IMbEvent<IssueBase, Hero> OnIssueOwnerChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onIssueOwnerChangedEvent;
			}
		}

		// Token: 0x0600079C RID: 1948 RVA: 0x00023E3A File Offset: 0x0002203A
		public override void OnIssueOwnerChanged(IssueBase issue, Hero oldOwner)
		{
			CampaignEvents.Instance._onIssueOwnerChangedEvent.Invoke(issue, oldOwner);
		}

		// Token: 0x0600079D RID: 1949 RVA: 0x00023E4D File Offset: 0x0002204D
		public override void OnGameOver()
		{
			CampaignEvents.Instance._onGameOverEvent.Invoke();
		}

		// Token: 0x1700017C RID: 380
		// (get) Token: 0x0600079E RID: 1950 RVA: 0x00023E5E File Offset: 0x0002205E
		public static IMbEvent OnGameOverEvent
		{
			get
			{
				return CampaignEvents.Instance._onGameOverEvent;
			}
		}

		// Token: 0x1700017D RID: 381
		// (get) Token: 0x0600079F RID: 1951 RVA: 0x00023E6A File Offset: 0x0002206A
		public static IMbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes> SiegeCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._siegeCompletedEvent;
			}
		}

		// Token: 0x060007A0 RID: 1952 RVA: 0x00023E76 File Offset: 0x00022076
		public override void SiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool isWin, MapEvent.BattleTypes battleType)
		{
			CampaignEvents.Instance._siegeCompletedEvent.Invoke(siegeSettlement, attackerParty, isWin, battleType);
		}

		// Token: 0x1700017E RID: 382
		// (get) Token: 0x060007A1 RID: 1953 RVA: 0x00023E8C File Offset: 0x0002208C
		public static IMbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes> AfterSiegeCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._afterSiegeCompletedEvent;
			}
		}

		// Token: 0x060007A2 RID: 1954 RVA: 0x00023E98 File Offset: 0x00022098
		public override void AfterSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool isWin, MapEvent.BattleTypes battleType)
		{
			CampaignEvents.Instance._afterSiegeCompletedEvent.Invoke(siegeSettlement, attackerParty, isWin, battleType);
		}

		// Token: 0x1700017F RID: 383
		// (get) Token: 0x060007A3 RID: 1955 RVA: 0x00023EAE File Offset: 0x000220AE
		public static IMbEvent<SiegeEvent, BattleSideEnum, SiegeEngineType> SiegeEngineBuiltEvent
		{
			get
			{
				return CampaignEvents.Instance._siegeEngineBuiltEvent;
			}
		}

		// Token: 0x060007A4 RID: 1956 RVA: 0x00023EBA File Offset: 0x000220BA
		public override void SiegeEngineBuilt(SiegeEvent siegeEvent, BattleSideEnum side, SiegeEngineType siegeEngineType)
		{
			CampaignEvents.Instance._siegeEngineBuiltEvent.Invoke(siegeEvent, side, siegeEngineType);
		}

		// Token: 0x17000180 RID: 384
		// (get) Token: 0x060007A5 RID: 1957 RVA: 0x00023ECE File Offset: 0x000220CE
		public static IMbEvent<BattleSideEnum, RaidEventComponent> RaidCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._raidCompletedEvent;
			}
		}

		// Token: 0x060007A6 RID: 1958 RVA: 0x00023EDA File Offset: 0x000220DA
		public override void RaidCompleted(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
		{
			CampaignEvents.Instance._raidCompletedEvent.Invoke(winnerSide, raidEvent);
		}

		// Token: 0x17000181 RID: 385
		// (get) Token: 0x060007A7 RID: 1959 RVA: 0x00023EED File Offset: 0x000220ED
		public static IMbEvent<BattleSideEnum, ForceVolunteersEventComponent> ForceVolunteersCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._forceVolunteersCompletedEvent;
			}
		}

		// Token: 0x060007A8 RID: 1960 RVA: 0x00023EF9 File Offset: 0x000220F9
		public override void ForceVolunteersCompleted(BattleSideEnum winnerSide, ForceVolunteersEventComponent forceVolunteersEvent)
		{
			CampaignEvents.Instance._forceVolunteersCompletedEvent.Invoke(winnerSide, forceVolunteersEvent);
		}

		// Token: 0x17000182 RID: 386
		// (get) Token: 0x060007A9 RID: 1961 RVA: 0x00023F0C File Offset: 0x0002210C
		public static IMbEvent<BattleSideEnum, ForceSuppliesEventComponent> ForceSuppliesCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._forceSuppliesCompletedEvent;
			}
		}

		// Token: 0x060007AA RID: 1962 RVA: 0x00023F18 File Offset: 0x00022118
		public override void ForceSuppliesCompleted(BattleSideEnum winnerSide, ForceSuppliesEventComponent forceSuppliesEvent)
		{
			CampaignEvents.Instance._forceSuppliesCompletedEvent.Invoke(winnerSide, forceSuppliesEvent);
		}

		// Token: 0x17000183 RID: 387
		// (get) Token: 0x060007AB RID: 1963 RVA: 0x00023F2B File Offset: 0x0002212B
		public static MbEvent<BattleSideEnum, HideoutEventComponent> OnHideoutBattleCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._hideoutBattleCompletedEvent;
			}
		}

		// Token: 0x060007AC RID: 1964 RVA: 0x00023F37 File Offset: 0x00022137
		public override void OnHideoutBattleCompleted(BattleSideEnum winnerSide, HideoutEventComponent hideoutEventComponent)
		{
			CampaignEvents.Instance._hideoutBattleCompletedEvent.Invoke(winnerSide, hideoutEventComponent);
		}

		// Token: 0x17000184 RID: 388
		// (get) Token: 0x060007AD RID: 1965 RVA: 0x00023F4A File Offset: 0x0002214A
		public static IMbEvent<Clan> OnClanDestroyedEvent
		{
			get
			{
				return CampaignEvents.Instance._onClanDestroyedEvent;
			}
		}

		// Token: 0x060007AE RID: 1966 RVA: 0x00023F56 File Offset: 0x00022156
		public override void OnClanDestroyed(Clan destroyedClan)
		{
			CampaignEvents.Instance._onClanDestroyedEvent.Invoke(destroyedClan);
		}

		// Token: 0x17000185 RID: 389
		// (get) Token: 0x060007AF RID: 1967 RVA: 0x00023F68 File Offset: 0x00022168
		public static IMbEvent<ItemObject, ItemModifier, bool> OnNewItemCraftedEvent
		{
			get
			{
				return CampaignEvents.Instance._onNewItemCraftedEvent;
			}
		}

		// Token: 0x060007B0 RID: 1968 RVA: 0x00023F74 File Offset: 0x00022174
		public override void OnNewItemCrafted(ItemObject itemObject, ItemModifier overriddenItemModifier, bool isCraftingOrderItem)
		{
			CampaignEvents.Instance._onNewItemCraftedEvent.Invoke(itemObject, overriddenItemModifier, isCraftingOrderItem);
		}

		// Token: 0x17000186 RID: 390
		// (get) Token: 0x060007B1 RID: 1969 RVA: 0x00023F88 File Offset: 0x00022188
		public static IMbEvent<CraftingPiece> CraftingPartUnlockedEvent
		{
			get
			{
				return CampaignEvents.Instance._craftingPartUnlockedEvent;
			}
		}

		// Token: 0x060007B2 RID: 1970 RVA: 0x00023F94 File Offset: 0x00022194
		public override void CraftingPartUnlocked(CraftingPiece craftingPiece)
		{
			CampaignEvents.Instance._craftingPartUnlockedEvent.Invoke(craftingPiece);
		}

		// Token: 0x17000187 RID: 391
		// (get) Token: 0x060007B3 RID: 1971 RVA: 0x00023FA6 File Offset: 0x000221A6
		public static IMbEvent<Workshop> WorkshopInitializedEvent
		{
			get
			{
				return CampaignEvents.Instance._onWorkshopInitializedEvent;
			}
		}

		// Token: 0x060007B4 RID: 1972 RVA: 0x00023FB2 File Offset: 0x000221B2
		public override void OnWorkshopInitialized(Workshop workshop)
		{
			CampaignEvents.Instance._onWorkshopInitializedEvent.Invoke(workshop);
		}

		// Token: 0x060007B5 RID: 1973 RVA: 0x00023FC4 File Offset: 0x000221C4
		public override void OnWorkshopOwnerChanged(Workshop workshop, Hero oldOwner)
		{
			CampaignEvents.Instance._onWorkshopOwnerChangedEvent.Invoke(workshop, oldOwner);
		}

		// Token: 0x17000188 RID: 392
		// (get) Token: 0x060007B6 RID: 1974 RVA: 0x00023FD7 File Offset: 0x000221D7
		public static IMbEvent<Workshop, Hero> WorkshopOwnerChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onWorkshopOwnerChangedEvent;
			}
		}

		// Token: 0x17000189 RID: 393
		// (get) Token: 0x060007B7 RID: 1975 RVA: 0x00023FE3 File Offset: 0x000221E3
		public static IMbEvent<Workshop> WorkshopTypeChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onWorkshopTypeChangedEvent;
			}
		}

		// Token: 0x060007B8 RID: 1976 RVA: 0x00023FEF File Offset: 0x000221EF
		public override void OnWorkshopTypeChanged(Workshop workshop)
		{
			CampaignEvents.Instance._onWorkshopTypeChangedEvent.Invoke(workshop);
		}

		// Token: 0x1700018A RID: 394
		// (get) Token: 0x060007B9 RID: 1977 RVA: 0x00024001 File Offset: 0x00022201
		public static IMbEvent OnBeforeSaveEvent
		{
			get
			{
				return CampaignEvents.Instance._onBeforeSaveEvent;
			}
		}

		// Token: 0x060007BA RID: 1978 RVA: 0x0002400D File Offset: 0x0002220D
		public override void OnBeforeSave()
		{
			CampaignEvents.Instance._onBeforeSaveEvent.Invoke();
		}

		// Token: 0x1700018B RID: 395
		// (get) Token: 0x060007BB RID: 1979 RVA: 0x0002401E File Offset: 0x0002221E
		public static IMbEvent OnSaveStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onSaveStartedEvent;
			}
		}

		// Token: 0x060007BC RID: 1980 RVA: 0x0002402A File Offset: 0x0002222A
		public override void OnSaveStarted()
		{
			CampaignEvents.Instance._onSaveStartedEvent.Invoke();
		}

		// Token: 0x1700018C RID: 396
		// (get) Token: 0x060007BD RID: 1981 RVA: 0x0002403B File Offset: 0x0002223B
		public static IMbEvent<bool, string> OnSaveOverEvent
		{
			get
			{
				return CampaignEvents.Instance._onSaveOverEvent;
			}
		}

		// Token: 0x060007BE RID: 1982 RVA: 0x00024047 File Offset: 0x00022247
		public override void OnSaveOver(bool isSuccessful, string saveName)
		{
			CampaignEvents.Instance._onSaveOverEvent.Invoke(isSuccessful, saveName);
		}

		// Token: 0x1700018D RID: 397
		// (get) Token: 0x060007BF RID: 1983 RVA: 0x0002405A File Offset: 0x0002225A
		public static IMbEvent<FlattenedTroopRoster> OnPrisonerTakenEvent
		{
			get
			{
				return CampaignEvents.Instance._onPrisonerTakenEvent;
			}
		}

		// Token: 0x060007C0 RID: 1984 RVA: 0x00024066 File Offset: 0x00022266
		public override void OnPrisonerTaken(FlattenedTroopRoster roster)
		{
			CampaignEvents.Instance._onPrisonerTakenEvent.Invoke(roster);
		}

		// Token: 0x1700018E RID: 398
		// (get) Token: 0x060007C1 RID: 1985 RVA: 0x00024078 File Offset: 0x00022278
		public static IMbEvent<FlattenedTroopRoster> OnPrisonerReleasedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPrisonerReleasedEvent;
			}
		}

		// Token: 0x060007C2 RID: 1986 RVA: 0x00024084 File Offset: 0x00022284
		public override void OnPrisonerReleased(FlattenedTroopRoster roster)
		{
			CampaignEvents.Instance._onPrisonerReleasedEvent.Invoke(roster);
		}

		// Token: 0x1700018F RID: 399
		// (get) Token: 0x060007C3 RID: 1987 RVA: 0x00024096 File Offset: 0x00022296
		public static IMbEvent<FlattenedTroopRoster> OnMainPartyPrisonerRecruitedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMainPartyPrisonerRecruitedEvent;
			}
		}

		// Token: 0x060007C4 RID: 1988 RVA: 0x000240A2 File Offset: 0x000222A2
		public override void OnMainPartyPrisonerRecruited(FlattenedTroopRoster roster)
		{
			CampaignEvents.Instance._onMainPartyPrisonerRecruitedEvent.Invoke(roster);
		}

		// Token: 0x17000190 RID: 400
		// (get) Token: 0x060007C5 RID: 1989 RVA: 0x000240B4 File Offset: 0x000222B4
		public static IMbEvent<MobileParty, FlattenedTroopRoster, Settlement> OnPrisonerDonatedToSettlementEvent
		{
			get
			{
				return CampaignEvents.Instance._onPrisonerDonatedToSettlementEvent;
			}
		}

		// Token: 0x060007C6 RID: 1990 RVA: 0x000240C0 File Offset: 0x000222C0
		public override void OnPrisonerDonatedToSettlement(MobileParty donatingParty, FlattenedTroopRoster donatedPrisoners, Settlement donatedSettlement)
		{
			CampaignEvents.Instance._onPrisonerDonatedToSettlementEvent.Invoke(donatingParty, donatedPrisoners, donatedSettlement);
		}

		// Token: 0x17000191 RID: 401
		// (get) Token: 0x060007C7 RID: 1991 RVA: 0x000240D4 File Offset: 0x000222D4
		public static IMbEvent<Hero, EquipmentElement> OnEquipmentSmeltedByHeroEvent
		{
			get
			{
				return CampaignEvents.Instance._onEquipmentSmeltedByHero;
			}
		}

		// Token: 0x060007C8 RID: 1992 RVA: 0x000240E0 File Offset: 0x000222E0
		public override void OnEquipmentSmeltedByHero(Hero hero, EquipmentElement smeltedEquipmentElement)
		{
			CampaignEvents.Instance._onEquipmentSmeltedByHero.Invoke(hero, smeltedEquipmentElement);
		}

		// Token: 0x17000192 RID: 402
		// (get) Token: 0x060007C9 RID: 1993 RVA: 0x000240F3 File Offset: 0x000222F3
		public static IMbEvent<int> OnPlayerTradeProfitEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerTradeProfit;
			}
		}

		// Token: 0x060007CA RID: 1994 RVA: 0x000240FF File Offset: 0x000222FF
		public override void OnPlayerTradeProfit(int profit)
		{
			CampaignEvents.Instance._onPlayerTradeProfit.Invoke(profit);
		}

		// Token: 0x17000193 RID: 403
		// (get) Token: 0x060007CB RID: 1995 RVA: 0x00024111 File Offset: 0x00022311
		public static IMbEvent<Hero, Clan> OnHeroChangedClanEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeroChangedClan;
			}
		}

		// Token: 0x060007CC RID: 1996 RVA: 0x0002411D File Offset: 0x0002231D
		public override void OnHeroChangedClan(Hero hero, Clan oldClan)
		{
			CampaignEvents.Instance._onHeroChangedClan.Invoke(hero, oldClan);
		}

		// Token: 0x17000194 RID: 404
		// (get) Token: 0x060007CD RID: 1997 RVA: 0x00024130 File Offset: 0x00022330
		public static IMbEvent<Hero, HeroGetsBusyReasons> OnHeroGetsBusyEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeroGetsBusy;
			}
		}

		// Token: 0x060007CE RID: 1998 RVA: 0x0002413C File Offset: 0x0002233C
		public override void OnHeroGetsBusy(Hero hero, HeroGetsBusyReasons heroGetsBusyReason)
		{
			CampaignEvents.Instance._onHeroGetsBusy.Invoke(hero, heroGetsBusyReason);
		}

		// Token: 0x17000195 RID: 405
		// (get) Token: 0x060007CF RID: 1999 RVA: 0x0002414F File Offset: 0x0002234F
		public static IMbEvent<PartyBase, ItemRoster> OnCollectLootsItemsEvent
		{
			get
			{
				return CampaignEvents.Instance._onCollectLootItems;
			}
		}

		// Token: 0x060007D0 RID: 2000 RVA: 0x0002415B File Offset: 0x0002235B
		public override void OnCollectLootItems(PartyBase winnerParty, ItemRoster gainedLoots)
		{
			CampaignEvents.Instance._onCollectLootItems.Invoke(winnerParty, gainedLoots);
		}

		// Token: 0x17000196 RID: 406
		// (get) Token: 0x060007D1 RID: 2001 RVA: 0x0002416E File Offset: 0x0002236E
		public static IMbEvent<PartyBase, PartyBase, ItemRoster> OnLootDistributedToPartyEvent
		{
			get
			{
				return CampaignEvents.Instance._onLootDistributedToPartyEvent;
			}
		}

		// Token: 0x060007D2 RID: 2002 RVA: 0x0002417A File Offset: 0x0002237A
		public override void OnLootDistributedToParty(PartyBase winnerParty, PartyBase defeatedParty, ItemRoster lootedItems)
		{
			CampaignEvents.Instance._onLootDistributedToPartyEvent.Invoke(winnerParty, defeatedParty, lootedItems);
		}

		// Token: 0x17000197 RID: 407
		// (get) Token: 0x060007D3 RID: 2003 RVA: 0x0002418E File Offset: 0x0002238E
		public static IMbEvent<Hero, Settlement, MobileParty, TeleportHeroAction.TeleportationDetail> OnHeroTeleportationRequestedEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeroTeleportationRequestedEvent;
			}
		}

		// Token: 0x060007D4 RID: 2004 RVA: 0x0002419A File Offset: 0x0002239A
		public override void OnHeroTeleportationRequested(Hero hero, Settlement targetSettlement, MobileParty targetParty, TeleportHeroAction.TeleportationDetail detail)
		{
			CampaignEvents.Instance._onHeroTeleportationRequestedEvent.Invoke(hero, targetSettlement, targetParty, detail);
		}

		// Token: 0x17000198 RID: 408
		// (get) Token: 0x060007D5 RID: 2005 RVA: 0x000241B0 File Offset: 0x000223B0
		public static IMbEvent<MobileParty> OnPartyLeaderChangeOfferCanceledEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyLeaderChangeOfferCanceledEvent;
			}
		}

		// Token: 0x060007D6 RID: 2006 RVA: 0x000241BC File Offset: 0x000223BC
		public override void OnPartyLeaderChangeOfferCanceled(MobileParty party)
		{
			CampaignEvents.Instance._onPartyLeaderChangeOfferCanceledEvent.Invoke(party);
		}

		// Token: 0x17000199 RID: 409
		// (get) Token: 0x060007D7 RID: 2007 RVA: 0x000241CE File Offset: 0x000223CE
		public static IMbEvent<MobileParty, Hero> OnPartyLeaderChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyLeaderChangedEvent;
			}
		}

		// Token: 0x060007D8 RID: 2008 RVA: 0x000241DA File Offset: 0x000223DA
		public override void OnPartyLeaderChanged(MobileParty mobileParty, Hero oldLeader)
		{
			CampaignEvents.Instance._onPartyLeaderChangedEvent.Invoke(mobileParty, oldLeader);
		}

		// Token: 0x1700019A RID: 410
		// (get) Token: 0x060007D9 RID: 2009 RVA: 0x000241ED File Offset: 0x000223ED
		public static IMbEvent<Clan, float> OnClanInfluenceChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onClanInfluenceChangedEvent;
			}
		}

		// Token: 0x060007DA RID: 2010 RVA: 0x000241F9 File Offset: 0x000223F9
		public override void OnClanInfluenceChanged(Clan clan, float change)
		{
			CampaignEvents.Instance._onClanInfluenceChangedEvent.Invoke(clan, change);
		}

		// Token: 0x1700019B RID: 411
		// (get) Token: 0x060007DB RID: 2011 RVA: 0x0002420C File Offset: 0x0002240C
		public static IMbEvent<CharacterObject> OnPlayerPartyKnockedOrKilledTroopEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerPartyKnockedOrKilledTroopEvent;
			}
		}

		// Token: 0x060007DC RID: 2012 RVA: 0x00024218 File Offset: 0x00022418
		public override void OnPlayerPartyKnockedOrKilledTroop(CharacterObject strikedTroop)
		{
			CampaignEvents.Instance._onPlayerPartyKnockedOrKilledTroopEvent.Invoke(strikedTroop);
		}

		// Token: 0x1700019C RID: 412
		// (get) Token: 0x060007DD RID: 2013 RVA: 0x0002422A File Offset: 0x0002242A
		public static IMbEvent<DefaultClanFinanceModel.AssetIncomeType, int> OnPlayerEarnedGoldFromAssetEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerEarnedGoldFromAssetEvent;
			}
		}

		// Token: 0x060007DE RID: 2014 RVA: 0x00024236 File Offset: 0x00022436
		public override void OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType incomeType, int incomeAmount)
		{
			CampaignEvents.Instance._onPlayerEarnedGoldFromAssetEvent.Invoke(incomeType, incomeAmount);
		}

		// Token: 0x1700019D RID: 413
		// (get) Token: 0x060007DF RID: 2015 RVA: 0x00024249 File Offset: 0x00022449
		public static IMbEvent<Clan, IFaction> OnClanEarnedGoldFromTributeEvent
		{
			get
			{
				return CampaignEvents.Instance._onClanEarnedGoldFromTributeEvent;
			}
		}

		// Token: 0x060007E0 RID: 2016 RVA: 0x00024255 File Offset: 0x00022455
		public override void OnClanEarnedGoldFromTribute(Clan receiverClan, IFaction payingFaction)
		{
			CampaignEvents.Instance._onClanEarnedGoldFromTributeEvent.Invoke(receiverClan, payingFaction);
		}

		// Token: 0x1700019E RID: 414
		// (get) Token: 0x060007E1 RID: 2017 RVA: 0x00024268 File Offset: 0x00022468
		public static IMbEvent OnMainPartyStarvingEvent
		{
			get
			{
				return CampaignEvents.Instance._onMainPartyStarving;
			}
		}

		// Token: 0x060007E2 RID: 2018 RVA: 0x00024274 File Offset: 0x00022474
		public override void OnMainPartyStarving()
		{
			CampaignEvents.Instance._onMainPartyStarving.Invoke();
		}

		// Token: 0x1700019F RID: 415
		// (get) Token: 0x060007E3 RID: 2019 RVA: 0x00024285 File Offset: 0x00022485
		public static IMbEvent<Town, bool> OnPlayerJoinedTournamentEvent
		{
			get
			{
				return CampaignEvents.Instance._onPlayerJoinedTournamentEvent;
			}
		}

		// Token: 0x060007E4 RID: 2020 RVA: 0x00024291 File Offset: 0x00022491
		public override void OnPlayerJoinedTournament(Town town, bool isParticipant)
		{
			CampaignEvents.Instance._onPlayerJoinedTournamentEvent.Invoke(town, isParticipant);
		}

		// Token: 0x170001A0 RID: 416
		// (get) Token: 0x060007E5 RID: 2021 RVA: 0x000242A4 File Offset: 0x000224A4
		public static IMbEvent<Hero> OnHeroUnregisteredEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeroUnregisteredEvent;
			}
		}

		// Token: 0x060007E6 RID: 2022 RVA: 0x000242B0 File Offset: 0x000224B0
		public override void OnHeroUnregistered(Hero hero)
		{
			CampaignEvents.Instance._onHeroUnregisteredEvent.Invoke(hero);
		}

		// Token: 0x170001A1 RID: 417
		// (get) Token: 0x060007E7 RID: 2023 RVA: 0x000242C2 File Offset: 0x000224C2
		public static IMbEvent OnConfigChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onConfigChanged;
			}
		}

		// Token: 0x060007E8 RID: 2024 RVA: 0x000242CE File Offset: 0x000224CE
		public override void OnConfigChanged()
		{
			CampaignEvents.Instance._onConfigChanged.Invoke();
		}

		// Token: 0x170001A2 RID: 418
		// (get) Token: 0x060007E9 RID: 2025 RVA: 0x000242DF File Offset: 0x000224DF
		public static IMbEvent<Town, CraftingOrder, ItemObject, Hero> OnCraftingOrderCompletedEvent
		{
			get
			{
				return CampaignEvents.Instance._onCraftingOrderCompleted;
			}
		}

		// Token: 0x060007EA RID: 2026 RVA: 0x000242EB File Offset: 0x000224EB
		public override void OnCraftingOrderCompleted(Town town, CraftingOrder craftingOrder, ItemObject craftedItem, Hero completerHero)
		{
			CampaignEvents.Instance._onCraftingOrderCompleted.Invoke(town, craftingOrder, craftedItem, completerHero);
		}

		// Token: 0x170001A3 RID: 419
		// (get) Token: 0x060007EB RID: 2027 RVA: 0x00024301 File Offset: 0x00022501
		public static IMbEvent<Hero, Crafting.RefiningFormula> OnItemsRefinedEvent
		{
			get
			{
				return CampaignEvents.Instance._onItemsRefined;
			}
		}

		// Token: 0x060007EC RID: 2028 RVA: 0x0002430D File Offset: 0x0002250D
		public override void OnItemsRefined(Hero hero, Crafting.RefiningFormula refineFormula)
		{
			CampaignEvents.Instance._onItemsRefined.Invoke(hero, refineFormula);
		}

		// Token: 0x170001A4 RID: 420
		// (get) Token: 0x060007ED RID: 2029 RVA: 0x00024320 File Offset: 0x00022520
		public static IMbEvent<Dictionary<Hero, int>> OnHeirSelectionRequestedEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeirSelectionRequested;
			}
		}

		// Token: 0x060007EE RID: 2030 RVA: 0x0002432C File Offset: 0x0002252C
		public override void OnHeirSelectionRequested(Dictionary<Hero, int> heirApparents)
		{
			CampaignEvents.Instance._onHeirSelectionRequested.Invoke(heirApparents);
		}

		// Token: 0x170001A5 RID: 421
		// (get) Token: 0x060007EF RID: 2031 RVA: 0x0002433E File Offset: 0x0002253E
		public static IMbEvent<Hero> OnHeirSelectionOverEvent
		{
			get
			{
				return CampaignEvents.Instance._onHeirSelectionOver;
			}
		}

		// Token: 0x060007F0 RID: 2032 RVA: 0x0002434A File Offset: 0x0002254A
		public override void OnHeirSelectionOver(Hero selectedHero)
		{
			CampaignEvents.Instance._onHeirSelectionOver.Invoke(selectedHero);
		}

		// Token: 0x170001A6 RID: 422
		// (get) Token: 0x060007F1 RID: 2033 RVA: 0x0002435C File Offset: 0x0002255C
		public static IMbEvent<CharacterCreationManager> OnCharacterCreationInitializedEvent
		{
			get
			{
				return CampaignEvents.Instance._onCharacterCreationInitialized;
			}
		}

		// Token: 0x060007F2 RID: 2034 RVA: 0x00024368 File Offset: 0x00022568
		public override void OnMobilePartyRaftStateChanged(MobileParty mobileParty)
		{
			CampaignEvents.Instance._onMobilePartyRaftStateChanged.Invoke(mobileParty);
		}

		// Token: 0x060007F3 RID: 2035 RVA: 0x0002437A File Offset: 0x0002257A
		public override void OnCharacterCreationInitialized(CharacterCreationManager characterCreationManager)
		{
			CampaignEvents.Instance._onCharacterCreationInitialized.Invoke(characterCreationManager);
		}

		// Token: 0x170001A7 RID: 423
		// (get) Token: 0x060007F4 RID: 2036 RVA: 0x0002438C File Offset: 0x0002258C
		public static IMbEvent<MobileParty> OnMobilePartyRaftStateChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMobilePartyRaftStateChanged;
			}
		}

		// Token: 0x170001A8 RID: 424
		// (get) Token: 0x060007F5 RID: 2037 RVA: 0x00024398 File Offset: 0x00022598
		public static IMbEvent<PartyBase, Ship, DestroyShipAction.ShipDestroyDetail> OnShipDestroyedEvent
		{
			get
			{
				return CampaignEvents.Instance._onShipDestroyedEvent;
			}
		}

		// Token: 0x060007F6 RID: 2038 RVA: 0x000243A4 File Offset: 0x000225A4
		public override void OnShipDestroyed(PartyBase owner, Ship ship, DestroyShipAction.ShipDestroyDetail detail)
		{
			CampaignEvents.Instance._onShipDestroyedEvent.Invoke(owner, ship, detail);
		}

		// Token: 0x170001A9 RID: 425
		// (get) Token: 0x060007F7 RID: 2039 RVA: 0x000243B8 File Offset: 0x000225B8
		public static IMbEvent<Ship, PartyBase, ChangeShipOwnerAction.ShipOwnerChangeDetail> OnShipOwnerChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onShipOwnerChangedEvent;
			}
		}

		// Token: 0x060007F8 RID: 2040 RVA: 0x000243C4 File Offset: 0x000225C4
		public override void OnShipOwnerChanged(Ship ship, PartyBase oldOwner, ChangeShipOwnerAction.ShipOwnerChangeDetail changeDetail)
		{
			CampaignEvents.Instance._onShipOwnerChangedEvent.Invoke(ship, oldOwner, changeDetail);
		}

		// Token: 0x170001AA RID: 426
		// (get) Token: 0x060007F9 RID: 2041 RVA: 0x000243D8 File Offset: 0x000225D8
		public static IMbEvent<Ship, Settlement> OnShipRepairedEvent
		{
			get
			{
				return CampaignEvents.Instance._onShipRepairedEvent;
			}
		}

		// Token: 0x060007FA RID: 2042 RVA: 0x000243E4 File Offset: 0x000225E4
		public override void OnShipRepaired(Ship ship, Settlement repairPort)
		{
			CampaignEvents.Instance._onShipRepairedEvent.Invoke(ship, repairPort);
		}

		// Token: 0x170001AB RID: 427
		// (get) Token: 0x060007FB RID: 2043 RVA: 0x000243F7 File Offset: 0x000225F7
		public static IMbEvent<Ship, Settlement> OnShipCreatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onShipCreatedEvent;
			}
		}

		// Token: 0x060007FC RID: 2044 RVA: 0x00024403 File Offset: 0x00022603
		public override void OnShipCreated(Ship ship, Settlement createdSettlement)
		{
			CampaignEvents.Instance._onShipCreatedEvent.Invoke(ship, createdSettlement);
		}

		// Token: 0x170001AC RID: 428
		// (get) Token: 0x060007FD RID: 2045 RVA: 0x00024416 File Offset: 0x00022616
		public static IMbEvent<Figurehead> OnFigureheadUnlockedEvent
		{
			get
			{
				return CampaignEvents.Instance._onFigureheadUnlockedEvent;
			}
		}

		// Token: 0x060007FE RID: 2046 RVA: 0x00024422 File Offset: 0x00022622
		public override void OnFigureheadUnlocked(Figurehead figurehead)
		{
			CampaignEvents.Instance._onFigureheadUnlockedEvent.Invoke(figurehead);
		}

		// Token: 0x170001AD RID: 429
		// (get) Token: 0x060007FF RID: 2047 RVA: 0x00024434 File Offset: 0x00022634
		public static IMbEvent<MobileParty, Army> OnPartyLeftArmyEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyLeftArmyEvent;
			}
		}

		// Token: 0x06000800 RID: 2048 RVA: 0x00024440 File Offset: 0x00022640
		public override void OnPartyLeftArmy(MobileParty party, Army army)
		{
			CampaignEvents.Instance._onPartyLeftArmyEvent.Invoke(party, army);
		}

		// Token: 0x170001AE RID: 430
		// (get) Token: 0x06000801 RID: 2049 RVA: 0x00024453 File Offset: 0x00022653
		public static IMbEvent<PartyBase> OnPartyAddedToMapEventEvent
		{
			get
			{
				return CampaignEvents.Instance._onPartyAddedToMapEventEvent;
			}
		}

		// Token: 0x06000802 RID: 2050 RVA: 0x0002445F File Offset: 0x0002265F
		public override void OnPartyAddedToMapEvent(PartyBase partyBase)
		{
			CampaignEvents.Instance._onPartyAddedToMapEventEvent.Invoke(partyBase);
		}

		// Token: 0x170001AF RID: 431
		// (get) Token: 0x06000803 RID: 2051 RVA: 0x00024471 File Offset: 0x00022671
		public static IMbEvent<Incident> OnIncidentResolvedEvent
		{
			get
			{
				return CampaignEvents.Instance._onIncidentResolvedEvent;
			}
		}

		// Token: 0x06000804 RID: 2052 RVA: 0x0002447D File Offset: 0x0002267D
		public override void OnIncidentResolved(Incident incident)
		{
			CampaignEvents.Instance._onIncidentResolvedEvent.Invoke(incident);
		}

		// Token: 0x170001B0 RID: 432
		// (get) Token: 0x06000805 RID: 2053 RVA: 0x0002448F File Offset: 0x0002268F
		public static IMbEvent<MobileParty> OnMobilePartyNavigationStateChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMobilePartyNavigationStateChangedEvent;
			}
		}

		// Token: 0x06000806 RID: 2054 RVA: 0x0002449B File Offset: 0x0002269B
		public override void OnMobilePartyNavigationStateChanged(MobileParty mobileParty)
		{
			CampaignEvents.Instance._onMobilePartyNavigationStateChangedEvent.Invoke(mobileParty);
		}

		// Token: 0x170001B1 RID: 433
		// (get) Token: 0x06000807 RID: 2055 RVA: 0x000244AD File Offset: 0x000226AD
		public static IMbEvent<MobileParty> OnMobilePartyJoinedToSiegeEventEvent
		{
			get
			{
				return CampaignEvents.Instance._onMobilePartyJoinedToSiegeEventEvent;
			}
		}

		// Token: 0x06000808 RID: 2056 RVA: 0x000244B9 File Offset: 0x000226B9
		public override void OnMobilePartyJoinedToSiegeEvent(MobileParty mobileParty)
		{
			CampaignEvents.Instance._onMobilePartyJoinedToSiegeEventEvent.Invoke(mobileParty);
		}

		// Token: 0x170001B2 RID: 434
		// (get) Token: 0x06000809 RID: 2057 RVA: 0x000244CB File Offset: 0x000226CB
		public static IMbEvent<MobileParty> OnMobilePartyLeftSiegeEventEvent
		{
			get
			{
				return CampaignEvents.Instance._onMobilePartyLeftSiegeEventEvent;
			}
		}

		// Token: 0x0600080A RID: 2058 RVA: 0x000244D7 File Offset: 0x000226D7
		public override void OnMobilePartyLeftSiegeEvent(MobileParty mobileParty)
		{
			CampaignEvents.Instance._onMobilePartyLeftSiegeEventEvent.Invoke(mobileParty);
		}

		// Token: 0x170001B3 RID: 435
		// (get) Token: 0x0600080B RID: 2059 RVA: 0x000244E9 File Offset: 0x000226E9
		public static IMbEvent<SiegeEvent> OnBlockadeActivatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onBlockadeActivatedEvent;
			}
		}

		// Token: 0x0600080C RID: 2060 RVA: 0x000244F5 File Offset: 0x000226F5
		public override void OnBlockadeActivated(SiegeEvent siegeEvent)
		{
			CampaignEvents.Instance._onBlockadeActivatedEvent.Invoke(siegeEvent);
		}

		// Token: 0x170001B4 RID: 436
		// (get) Token: 0x0600080D RID: 2061 RVA: 0x00024507 File Offset: 0x00022707
		public static IMbEvent<SiegeEvent> OnBlockadeDeactivatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onBlockadeDeactivatedEvent;
			}
		}

		// Token: 0x0600080E RID: 2062 RVA: 0x00024513 File Offset: 0x00022713
		public override void OnBlockadeDeactivated(SiegeEvent siegeEvent)
		{
			CampaignEvents.Instance._onBlockadeDeactivatedEvent.Invoke(siegeEvent);
		}

		// Token: 0x170001B5 RID: 437
		// (get) Token: 0x0600080F RID: 2063 RVA: 0x00024525 File Offset: 0x00022725
		public static IMbEvent<MapMarker> OnMapMarkerCreatedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMapMarkerCreatedEvent;
			}
		}

		// Token: 0x06000810 RID: 2064 RVA: 0x00024531 File Offset: 0x00022731
		public override void OnMapMarkerCreated(MapMarker mapMarker)
		{
			CampaignEvents.Instance._onMapMarkerCreatedEvent.Invoke(mapMarker);
		}

		// Token: 0x170001B6 RID: 438
		// (get) Token: 0x06000811 RID: 2065 RVA: 0x00024543 File Offset: 0x00022743
		public static IMbEvent<MapMarker> OnMapMarkerRemovedEvent
		{
			get
			{
				return CampaignEvents.Instance._onMapMarkerRemovedEvent;
			}
		}

		// Token: 0x06000812 RID: 2066 RVA: 0x0002454F File Offset: 0x0002274F
		public override void OnMapMarkerRemoved(MapMarker mapMarker)
		{
			CampaignEvents.Instance._onMapMarkerRemovedEvent.Invoke(mapMarker);
		}

		// Token: 0x170001B7 RID: 439
		// (get) Token: 0x06000813 RID: 2067 RVA: 0x00024561 File Offset: 0x00022761
		public static IMbEvent<Kingdom, Kingdom> OnAllianceStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onAllianceStartedEvent;
			}
		}

		// Token: 0x06000814 RID: 2068 RVA: 0x0002456D File Offset: 0x0002276D
		public override void OnAllianceStarted(Kingdom kingdom1, Kingdom kingdom2)
		{
			CampaignEvents.Instance._onAllianceStartedEvent.Invoke(kingdom1, kingdom2);
		}

		// Token: 0x170001B8 RID: 440
		// (get) Token: 0x06000815 RID: 2069 RVA: 0x00024580 File Offset: 0x00022780
		public static IMbEvent<Kingdom, Kingdom> OnAllianceEndedEvent
		{
			get
			{
				return CampaignEvents.Instance._onAllianceEndedEvent;
			}
		}

		// Token: 0x06000816 RID: 2070 RVA: 0x0002458C File Offset: 0x0002278C
		public override void OnAllianceEnded(Kingdom kingdom1, Kingdom kingdom2)
		{
			CampaignEvents.Instance._onAllianceEndedEvent.Invoke(kingdom1, kingdom2);
		}

		// Token: 0x170001B9 RID: 441
		// (get) Token: 0x06000817 RID: 2071 RVA: 0x0002459F File Offset: 0x0002279F
		public static IMbEvent<Kingdom, Kingdom, Kingdom> OnCallToWarAgreementStartedEvent
		{
			get
			{
				return CampaignEvents.Instance._onCallToWarAgreementStartedEvent;
			}
		}

		// Token: 0x06000818 RID: 2072 RVA: 0x000245AB File Offset: 0x000227AB
		public override void OnCallToWarAgreementStarted(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
		{
			CampaignEvents.Instance._onCallToWarAgreementStartedEvent.Invoke(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
		}

		// Token: 0x170001BA RID: 442
		// (get) Token: 0x06000819 RID: 2073 RVA: 0x000245BF File Offset: 0x000227BF
		public static IMbEvent<Kingdom, Kingdom, Kingdom> OnCallToWarAgreementEndedEvent
		{
			get
			{
				return CampaignEvents.Instance._onCallToWarAgreementEndedEvent;
			}
		}

		// Token: 0x0600081A RID: 2074 RVA: 0x000245CB File Offset: 0x000227CB
		public override void OnCallToWarAgreementEnded(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
		{
			CampaignEvents.Instance._onCallToWarAgreementEndedEvent.Invoke(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
		}

		// Token: 0x170001BB RID: 443
		// (get) Token: 0x0600081B RID: 2075 RVA: 0x000245DF File Offset: 0x000227DF
		public static ReferenceIMBEvent<Hero, bool> CanHeroLeadPartyEvent
		{
			get
			{
				return CampaignEvents.Instance._canHeroLeadPartyEvent;
			}
		}

		// Token: 0x0600081C RID: 2076 RVA: 0x000245EB File Offset: 0x000227EB
		public override void CanHeroLeadParty(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canHeroLeadPartyEvent.Invoke(hero, ref result);
		}

		// Token: 0x170001BC RID: 444
		// (get) Token: 0x0600081D RID: 2077 RVA: 0x000245FE File Offset: 0x000227FE
		public static ReferenceIMBEvent<Hero, bool> CanHeroMarryEvent
		{
			get
			{
				return CampaignEvents.Instance._canMarryEvent;
			}
		}

		// Token: 0x0600081E RID: 2078 RVA: 0x0002460A File Offset: 0x0002280A
		public override void CanHeroMarry(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canMarryEvent.Invoke(hero, ref result);
		}

		// Token: 0x170001BD RID: 445
		// (get) Token: 0x0600081F RID: 2079 RVA: 0x0002461D File Offset: 0x0002281D
		public static ReferenceIMBEvent<Hero, bool> CanHeroEquipmentBeChangedEvent
		{
			get
			{
				return CampaignEvents.Instance._canHeroEquipmentBeChangedEvent;
			}
		}

		// Token: 0x06000820 RID: 2080 RVA: 0x00024629 File Offset: 0x00022829
		public override void CanHeroEquipmentBeChanged(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canHeroEquipmentBeChangedEvent.Invoke(hero, ref result);
		}

		// Token: 0x170001BE RID: 446
		// (get) Token: 0x06000821 RID: 2081 RVA: 0x0002463C File Offset: 0x0002283C
		public static ReferenceIMBEvent<Hero, bool> CanBeGovernorOrHavePartyRoleEvent
		{
			get
			{
				return CampaignEvents.Instance._canBeGovernorOrHavePartyRoleEvent;
			}
		}

		// Token: 0x06000822 RID: 2082 RVA: 0x00024648 File Offset: 0x00022848
		public override void CanBeGovernorOrHavePartyRole(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canBeGovernorOrHavePartyRoleEvent.Invoke(hero, ref result);
		}

		// Token: 0x170001BF RID: 447
		// (get) Token: 0x06000823 RID: 2083 RVA: 0x0002465B File Offset: 0x0002285B
		public static ReferenceIMBEvent<Hero, KillCharacterAction.KillCharacterActionDetail, bool> CanHeroDieEvent
		{
			get
			{
				return CampaignEvents.Instance._canHeroDieEvent;
			}
		}

		// Token: 0x06000824 RID: 2084 RVA: 0x00024667 File Offset: 0x00022867
		public override void CanHeroDie(Hero hero, KillCharacterAction.KillCharacterActionDetail causeOfDeath, ref bool result)
		{
			CampaignEvents.Instance._canHeroDieEvent.Invoke(hero, causeOfDeath, ref result);
		}

		// Token: 0x170001C0 RID: 448
		// (get) Token: 0x06000825 RID: 2085 RVA: 0x0002467B File Offset: 0x0002287B
		public static ReferenceIMBEvent<Hero, bool> CanPlayerMeetWithHeroAfterConversationEvent
		{
			get
			{
				return CampaignEvents.Instance._canPlayerMeetWithHeroAfterConversationEvent;
			}
		}

		// Token: 0x06000826 RID: 2086 RVA: 0x00024687 File Offset: 0x00022887
		public override void CanPlayerMeetWithHeroAfterConversation(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canPlayerMeetWithHeroAfterConversationEvent.Invoke(hero, ref result);
		}

		// Token: 0x170001C1 RID: 449
		// (get) Token: 0x06000827 RID: 2087 RVA: 0x0002469A File Offset: 0x0002289A
		public static ReferenceIMBEvent<Hero, bool> CanHeroBecomePrisonerEvent
		{
			get
			{
				return CampaignEvents.Instance._canHeroBecomePrisonerEvent;
			}
		}

		// Token: 0x06000828 RID: 2088 RVA: 0x000246A6 File Offset: 0x000228A6
		public override void CanHeroBecomePrisoner(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canHeroBecomePrisonerEvent.Invoke(hero, ref result);
		}

		// Token: 0x170001C2 RID: 450
		// (get) Token: 0x06000829 RID: 2089 RVA: 0x000246B9 File Offset: 0x000228B9
		public static ReferenceIMBEvent<Hero, bool> CanMoveToSettlementEvent
		{
			get
			{
				return CampaignEvents.Instance._canMoveToSettlementEvent;
			}
		}

		// Token: 0x0600082A RID: 2090 RVA: 0x000246C5 File Offset: 0x000228C5
		public override void CanMoveToSettlement(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canMoveToSettlementEvent.Invoke(hero, ref result);
		}

		// Token: 0x170001C3 RID: 451
		// (get) Token: 0x0600082B RID: 2091 RVA: 0x000246D8 File Offset: 0x000228D8
		public static ReferenceIMBEvent<Hero, bool> CanHaveCampaignIssuesEvent
		{
			get
			{
				return CampaignEvents.Instance._canHaveCampaignIssues;
			}
		}

		// Token: 0x0600082C RID: 2092 RVA: 0x000246E4 File Offset: 0x000228E4
		public override void CanHaveCampaignIssues(Hero hero, ref bool result)
		{
			CampaignEvents.Instance._canHaveCampaignIssues.Invoke(hero, ref result);
		}

		// Token: 0x170001C4 RID: 452
		// (get) Token: 0x0600082D RID: 2093 RVA: 0x000246F7 File Offset: 0x000228F7
		public static ReferenceIMBEvent<Settlement, object, int> IsSettlementBusyEvent
		{
			get
			{
				return CampaignEvents.Instance._isSettlementBusy;
			}
		}

		// Token: 0x0600082E RID: 2094 RVA: 0x00024703 File Offset: 0x00022903
		public override void IsSettlementBusy(Settlement settlement, object asker, ref int priority)
		{
			CampaignEvents.Instance._isSettlementBusy.Invoke(settlement, asker, ref priority);
		}

		// Token: 0x170001C5 RID: 453
		// (get) Token: 0x0600082F RID: 2095 RVA: 0x00024717 File Offset: 0x00022917
		public static IMbEvent<IFaction> OnMapEventContinuityNeedsUpdateEvent
		{
			get
			{
				return CampaignEvents.Instance._onMapEventContinuityNeedsUpdate;
			}
		}

		// Token: 0x06000830 RID: 2096 RVA: 0x00024723 File Offset: 0x00022923
		public override void OnMapEventContinuityNeedsUpdate(IFaction faction)
		{
			CampaignEvents.Instance._onMapEventContinuityNeedsUpdate.Invoke(faction);
		}

		// Token: 0x04000186 RID: 390
		private readonly MbEvent _onPlayerBodyPropertiesChangedEvent = new MbEvent();

		// Token: 0x04000187 RID: 391
		private readonly MbEvent<BarterData> _barterablesRequested = new MbEvent<BarterData>();

		// Token: 0x04000188 RID: 392
		private readonly MbEvent<Hero, bool> _heroLevelledUp = new MbEvent<Hero, bool>();

		// Token: 0x04000189 RID: 393
		private readonly MbEvent<BanditPartyComponent, Hideout> _onHomeHideoutChangedEvent = new MbEvent<BanditPartyComponent, Hideout>();

		// Token: 0x0400018A RID: 394
		private readonly MbEvent<Hero, SkillObject, int, bool> _heroGainedSkill = new MbEvent<Hero, SkillObject, int, bool>();

		// Token: 0x0400018B RID: 395
		private readonly MbEvent _onCharacterCreationIsOverEvent = new MbEvent();

		// Token: 0x0400018C RID: 396
		private readonly MbEvent<Hero, bool> _onHeroCreated = new MbEvent<Hero, bool>();

		// Token: 0x0400018D RID: 397
		private readonly MbEvent<Hero, Occupation> _heroOccupationChangedEvent = new MbEvent<Hero, Occupation>();

		// Token: 0x0400018E RID: 398
		private readonly MbEvent<Hero> _onHeroWounded = new MbEvent<Hero>();

		// Token: 0x0400018F RID: 399
		private readonly MbEvent<Hero, Hero, List<Barterable>> _onBarterAcceptedEvent = new MbEvent<Hero, Hero, List<Barterable>>();

		// Token: 0x04000190 RID: 400
		private readonly MbEvent<Hero, Hero, List<Barterable>> _onBarterCanceledEvent = new MbEvent<Hero, Hero, List<Barterable>>();

		// Token: 0x04000191 RID: 401
		private readonly MbEvent<Hero, Hero, int, bool, ChangeRelationAction.ChangeRelationDetail, Hero, Hero> _heroRelationChanged = new MbEvent<Hero, Hero, int, bool, ChangeRelationAction.ChangeRelationDetail, Hero, Hero>();

		// Token: 0x04000192 RID: 402
		private readonly MbEvent<QuestBase, bool> _questLogAddedEvent = new MbEvent<QuestBase, bool>();

		// Token: 0x04000193 RID: 403
		private readonly MbEvent<IssueBase, bool> _issueLogAddedEvent = new MbEvent<IssueBase, bool>();

		// Token: 0x04000194 RID: 404
		private readonly MbEvent<Clan, bool> _clanTierIncrease = new MbEvent<Clan, bool>();

		// Token: 0x04000195 RID: 405
		private readonly MbEvent<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool> _clanChangedKingdom = new MbEvent<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>();

		// Token: 0x04000196 RID: 406
		private readonly MbEvent<Clan, Kingdom, Kingdom> _onClanDefected = new MbEvent<Clan, Kingdom, Kingdom>();

		// Token: 0x04000197 RID: 407
		private readonly MbEvent<Clan, bool> _onClanCreatedEvent = new MbEvent<Clan, bool>();

		// Token: 0x04000198 RID: 408
		private readonly MbEvent<Hero, MobileParty> _onHeroJoinedPartyEvent = new MbEvent<Hero, MobileParty>();

		// Token: 0x04000199 RID: 409
		private readonly MbEvent<ValueTuple<Hero, PartyBase>, ValueTuple<Hero, PartyBase>, ValueTuple<int, string>, bool> _heroOrPartyTradedGold = new MbEvent<ValueTuple<Hero, PartyBase>, ValueTuple<Hero, PartyBase>, ValueTuple<int, string>, bool>();

		// Token: 0x0400019A RID: 410
		private readonly MbEvent<ValueTuple<Hero, PartyBase>, ValueTuple<Hero, PartyBase>, ItemRosterElement, bool> _heroOrPartyGaveItem = new MbEvent<ValueTuple<Hero, PartyBase>, ValueTuple<Hero, PartyBase>, ItemRosterElement, bool>();

		// Token: 0x0400019B RID: 411
		private readonly MbEvent<MobileParty> _banditPartyRecruited = new MbEvent<MobileParty>();

		// Token: 0x0400019C RID: 412
		private readonly MbEvent<KingdomDecision, bool> _kingdomDecisionAdded = new MbEvent<KingdomDecision, bool>();

		// Token: 0x0400019D RID: 413
		private readonly MbEvent<KingdomDecision, bool> _kingdomDecisionCancelled = new MbEvent<KingdomDecision, bool>();

		// Token: 0x0400019E RID: 414
		private readonly MbEvent<KingdomDecision, DecisionOutcome, bool> _kingdomDecisionConcluded = new MbEvent<KingdomDecision, DecisionOutcome, bool>();

		// Token: 0x0400019F RID: 415
		private readonly MbEvent<MobileParty> _partyAttachedParty = new MbEvent<MobileParty>();

		// Token: 0x040001A0 RID: 416
		private readonly MbEvent<MobileParty> _nearbyPartyAddedToPlayerMapEvent = new MbEvent<MobileParty>();

		// Token: 0x040001A1 RID: 417
		private readonly MbEvent<Army> _armyCreated = new MbEvent<Army>();

		// Token: 0x040001A2 RID: 418
		private readonly MbEvent<Army, Army.ArmyDispersionReason, bool> _armyDispersed = new MbEvent<Army, Army.ArmyDispersionReason, bool>();

		// Token: 0x040001A3 RID: 419
		private readonly MbEvent<Army, IMapPoint> _armyGathered = new MbEvent<Army, IMapPoint>();

		// Token: 0x040001A4 RID: 420
		private readonly MbEvent<Hero, PerkObject> _perkOpenedEvent = new MbEvent<Hero, PerkObject>();

		// Token: 0x040001A5 RID: 421
		private readonly MbEvent<Hero, PerkObject> _perkResetEvent = new MbEvent<Hero, PerkObject>();

		// Token: 0x040001A6 RID: 422
		private readonly MbEvent<TraitObject, int> _playerTraitChangedEvent = new MbEvent<TraitObject, int>();

		// Token: 0x040001A7 RID: 423
		private readonly MbEvent<Village, Village.VillageStates, Village.VillageStates, MobileParty> _villageStateChanged = new MbEvent<Village, Village.VillageStates, Village.VillageStates, MobileParty>();

		// Token: 0x040001A8 RID: 424
		private readonly MbEvent<MobileParty, Settlement, Hero> _settlementEntered = new MbEvent<MobileParty, Settlement, Hero>();

		// Token: 0x040001A9 RID: 425
		private readonly MbEvent<MobileParty, Settlement, Hero> _afterSettlementEntered = new MbEvent<MobileParty, Settlement, Hero>();

		// Token: 0x040001AA RID: 426
		private readonly MbEvent<MobileParty, Settlement, Hero> _beforeSettlementEntered = new MbEvent<MobileParty, Settlement, Hero>();

		// Token: 0x040001AB RID: 427
		private readonly MbEvent<Town, CharacterObject, CharacterObject> _mercenaryTroopChangedInTown = new MbEvent<Town, CharacterObject, CharacterObject>();

		// Token: 0x040001AC RID: 428
		private readonly MbEvent<Town, int, int> _mercenaryNumberChangedInTown = new MbEvent<Town, int, int>();

		// Token: 0x040001AD RID: 429
		private readonly MbEvent<Alley, Hero, Hero> _alleyOwnerChanged = new MbEvent<Alley, Hero, Hero>();

		// Token: 0x040001AE RID: 430
		private readonly MbEvent<Alley, TroopRoster> _alleyOccupiedByPlayer = new MbEvent<Alley, TroopRoster>();

		// Token: 0x040001AF RID: 431
		private readonly MbEvent<Alley> _alleyClearedByPlayer = new MbEvent<Alley>();

		// Token: 0x040001B0 RID: 432
		private readonly MbEvent<Hero, Hero, Romance.RomanceLevelEnum> _romanticStateChanged = new MbEvent<Hero, Hero, Romance.RomanceLevelEnum>();

		// Token: 0x040001B1 RID: 433
		private readonly MbEvent<Hero, Hero, bool> _beforeHeroesMarried = new MbEvent<Hero, Hero, bool>();

		// Token: 0x040001B2 RID: 434
		private readonly MbEvent<int, Town> _playerEliminatedFromTournament = new MbEvent<int, Town>();

		// Token: 0x040001B3 RID: 435
		private readonly MbEvent<Town> _playerStartedTournamentMatch = new MbEvent<Town>();

		// Token: 0x040001B4 RID: 436
		private readonly MbEvent<Town> _tournamentStarted = new MbEvent<Town>();

		// Token: 0x040001B5 RID: 437
		private readonly MbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail> _warDeclared = new MbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>();

		// Token: 0x040001B6 RID: 438
		private readonly MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> _tournamentFinished = new MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject>();

		// Token: 0x040001B7 RID: 439
		private readonly MbEvent<Town> _tournamentCancelled = new MbEvent<Town>();

		// Token: 0x040001B8 RID: 440
		private readonly MbEvent<PartyBase, PartyBase, object, bool> _battleStarted = new MbEvent<PartyBase, PartyBase, object, bool>();

		// Token: 0x040001B9 RID: 441
		private readonly MbEvent<Settlement, Clan> _rebellionFinished = new MbEvent<Settlement, Clan>();

		// Token: 0x040001BA RID: 442
		private readonly MbEvent<Town, bool> _townRebelliousStateChanged = new MbEvent<Town, bool>();

		// Token: 0x040001BB RID: 443
		private readonly MbEvent<Settlement, Clan> _rebelliousClanDisbandedAtSettlement = new MbEvent<Settlement, Clan>();

		// Token: 0x040001BC RID: 444
		private readonly MbEvent<MobileParty, ItemRoster> _itemsLooted = new MbEvent<MobileParty, ItemRoster>();

		// Token: 0x040001BD RID: 445
		private readonly MbEvent<MobileParty, PartyBase> _mobilePartyDestroyed = new MbEvent<MobileParty, PartyBase>();

		// Token: 0x040001BE RID: 446
		private readonly MbEvent<MobileParty> _mobilePartyCreated = new MbEvent<MobileParty>();

		// Token: 0x040001BF RID: 447
		private readonly MbEvent<IInteractablePoint> _mapInteractableCreated = new MbEvent<IInteractablePoint>();

		// Token: 0x040001C0 RID: 448
		private readonly MbEvent<IInteractablePoint> _mapInteractableDestroyed = new MbEvent<IInteractablePoint>();

		// Token: 0x040001C1 RID: 449
		private readonly MbEvent<MobileParty, bool> _mobilePartyQuestStatusChanged = new MbEvent<MobileParty, bool>();

		// Token: 0x040001C2 RID: 450
		private readonly MbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool> _heroKilled = new MbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>();

		// Token: 0x040001C3 RID: 451
		private readonly MbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool> _onBeforeHeroKilled = new MbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>();

		// Token: 0x040001C4 RID: 452
		private readonly MbEvent<Hero, int> _childEducationCompleted = new MbEvent<Hero, int>();

		// Token: 0x040001C5 RID: 453
		private readonly MbEvent<Hero> _heroComesOfAge = new MbEvent<Hero>();

		// Token: 0x040001C6 RID: 454
		private readonly MbEvent<Hero> _heroGrowsOutOfInfancyEvent = new MbEvent<Hero>();

		// Token: 0x040001C7 RID: 455
		private readonly MbEvent<Hero> _heroReachesTeenAgeEvent = new MbEvent<Hero>();

		// Token: 0x040001C8 RID: 456
		private readonly MbEvent<Hero, Hero> _characterDefeated = new MbEvent<Hero, Hero>();

		// Token: 0x040001C9 RID: 457
		private readonly MbEvent<Kingdom, Clan> _rulingClanChanged = new MbEvent<Kingdom, Clan>();

		// Token: 0x040001CA RID: 458
		private readonly MbEvent<PartyBase, Hero> _heroPrisonerTaken = new MbEvent<PartyBase, Hero>();

		// Token: 0x040001CB RID: 459
		private readonly MbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool> _heroPrisonerReleased = new MbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool>();

		// Token: 0x040001CC RID: 460
		private readonly MbEvent<Hero, bool> _characterBecameFugitiveEvent = new MbEvent<Hero, bool>();

		// Token: 0x040001CD RID: 461
		private readonly MbEvent<Hero> _playerMetHero = new MbEvent<Hero>();

		// Token: 0x040001CE RID: 462
		private readonly MbEvent<Hero> _playerLearnsAboutHero = new MbEvent<Hero>();

		// Token: 0x040001CF RID: 463
		private readonly MbEvent<Hero, int, bool> _renownGained = new MbEvent<Hero, int, bool>();

		// Token: 0x040001D0 RID: 464
		private readonly MbEvent<IFaction, float> _crimeRatingChanged = new MbEvent<IFaction, float>();

		// Token: 0x040001D1 RID: 465
		private readonly MbEvent<Hero> _newCompanionAdded = new MbEvent<Hero>();

		// Token: 0x040001D2 RID: 466
		private readonly MbEvent<IMission> _afterMissionStarted = new MbEvent<IMission>();

		// Token: 0x040001D3 RID: 467
		private readonly MbEvent<MenuCallbackArgs> _gameMenuOpened = new MbEvent<MenuCallbackArgs>();

		// Token: 0x040001D4 RID: 468
		private readonly MbEvent<MenuCallbackArgs> _afterGameMenuInitializedEvent = new MbEvent<MenuCallbackArgs>();

		// Token: 0x040001D5 RID: 469
		private readonly MbEvent<MenuCallbackArgs> _beforeGameMenuOpenedEvent = new MbEvent<MenuCallbackArgs>();

		// Token: 0x040001D6 RID: 470
		private readonly MbEvent<IFaction, IFaction, MakePeaceAction.MakePeaceDetail> _makePeace = new MbEvent<IFaction, IFaction, MakePeaceAction.MakePeaceDetail>();

		// Token: 0x040001D7 RID: 471
		private readonly MbEvent<Kingdom> _kingdomDestroyed = new MbEvent<Kingdom>();

		// Token: 0x040001D8 RID: 472
		private readonly ReferenceMBEvent<Kingdom, bool> _canKingdomBeDiscontinued = new ReferenceMBEvent<Kingdom, bool>();

		// Token: 0x040001D9 RID: 473
		private readonly MbEvent<Kingdom> _kingdomCreated = new MbEvent<Kingdom>();

		// Token: 0x040001DA RID: 474
		private readonly MbEvent<Village> _villageBecomeNormal = new MbEvent<Village>();

		// Token: 0x040001DB RID: 475
		private readonly MbEvent<Village> _villageBeingRaided = new MbEvent<Village>();

		// Token: 0x040001DC RID: 476
		private readonly MbEvent<Village> _villageLooted = new MbEvent<Village>();

		// Token: 0x040001DD RID: 477
		private readonly MbEvent<Hero, RemoveCompanionAction.RemoveCompanionDetail> _companionRemoved = new MbEvent<Hero, RemoveCompanionAction.RemoveCompanionDetail>();

		// Token: 0x040001DE RID: 478
		private readonly MbEvent<IAgent> _onAgentJoinedConversationEvent = new MbEvent<IAgent>();

		// Token: 0x040001DF RID: 479
		private readonly MbEvent<IEnumerable<CharacterObject>> _onConversationEnded = new MbEvent<IEnumerable<CharacterObject>>();

		// Token: 0x040001E0 RID: 480
		private readonly MbEvent<MapEvent> _mapEventEnded = new MbEvent<MapEvent>();

		// Token: 0x040001E1 RID: 481
		private readonly MbEvent<MapEvent, PartyBase, PartyBase> _mapEventStarted = new MbEvent<MapEvent, PartyBase, PartyBase>();

		// Token: 0x040001E2 RID: 482
		private readonly MbEvent<Settlement, FlattenedTroopRoster, Hero, bool> _prisonersChangeInSettlement = new MbEvent<Settlement, FlattenedTroopRoster, Hero, bool>();

		// Token: 0x040001E3 RID: 483
		private readonly MbEvent<Hero, BoardGameHelper.BoardGameState> _onPlayerBoardGameOver = new MbEvent<Hero, BoardGameHelper.BoardGameState>();

		// Token: 0x040001E4 RID: 484
		private readonly MbEvent<Hero> _onRansomOfferedToPlayer = new MbEvent<Hero>();

		// Token: 0x040001E5 RID: 485
		private readonly MbEvent<Hero> _onRansomOfferCancelled = new MbEvent<Hero>();

		// Token: 0x040001E6 RID: 486
		private readonly MbEvent<IFaction, int, int> _onPeaceOfferedToPlayer = new MbEvent<IFaction, int, int>();

		// Token: 0x040001E7 RID: 487
		private readonly MbEvent<Kingdom, Kingdom> _onTradeAgreementSignedEvent = new MbEvent<Kingdom, Kingdom>();

		// Token: 0x040001E8 RID: 488
		private readonly MbEvent<IFaction> _onPeaceOfferResolved = new MbEvent<IFaction>();

		// Token: 0x040001E9 RID: 489
		private readonly MbEvent<Hero, Hero> _onMarriageOfferedToPlayerEvent = new MbEvent<Hero, Hero>();

		// Token: 0x040001EA RID: 490
		private readonly MbEvent<Hero, Hero> _onMarriageOfferCanceledEvent = new MbEvent<Hero, Hero>();

		// Token: 0x040001EB RID: 491
		private readonly MbEvent<Kingdom> _onVassalOrMercenaryServiceOfferedToPlayerEvent = new MbEvent<Kingdom>();

		// Token: 0x040001EC RID: 492
		private readonly MbEvent<Kingdom> _onVassalOrMercenaryServiceOfferCanceledEvent = new MbEvent<Kingdom>();

		// Token: 0x040001ED RID: 493
		private readonly MbEvent<Clan, StartMercenaryServiceAction.StartMercenaryServiceActionDetails> _onMercenaryServiceStartedEvent = new MbEvent<Clan, StartMercenaryServiceAction.StartMercenaryServiceActionDetails>();

		// Token: 0x040001EE RID: 494
		private readonly MbEvent<Clan, EndMercenaryServiceAction.EndMercenaryServiceActionDetails> _onMercenaryServiceEndedEvent = new MbEvent<Clan, EndMercenaryServiceAction.EndMercenaryServiceActionDetails>();

		// Token: 0x040001EF RID: 495
		private readonly MbEvent<IMission> _onMissionStartedEvent = new MbEvent<IMission>();

		// Token: 0x040001F0 RID: 496
		private readonly MbEvent _beforeMissionOpenedEvent = new MbEvent();

		// Token: 0x040001F1 RID: 497
		private readonly MbEvent<PartyBase> _onPartyRemovedEvent = new MbEvent<PartyBase>();

		// Token: 0x040001F2 RID: 498
		private readonly MbEvent<PartyBase> _onPartySizeChangedEvent = new MbEvent<PartyBase>();

		// Token: 0x040001F3 RID: 499
		private readonly MbEvent<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail> _onSettlementOwnerChangedEvent = new MbEvent<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>();

		// Token: 0x040001F4 RID: 500
		private readonly MbEvent<Town, Hero, Hero> _onGovernorChangedEvent = new MbEvent<Town, Hero, Hero>();

		// Token: 0x040001F5 RID: 501
		private readonly MbEvent<MobileParty, Settlement> _onSettlementLeftEvent = new MbEvent<MobileParty, Settlement>();

		// Token: 0x040001F6 RID: 502
		private readonly MbEvent _weeklyTickEvent = new MbEvent();

		// Token: 0x040001F7 RID: 503
		private readonly MbEvent _dailyTickEvent = new MbEvent();

		// Token: 0x040001F8 RID: 504
		private readonly MbEvent<MobileParty> _dailyTickPartyEvent = new MbEvent<MobileParty>();

		// Token: 0x040001F9 RID: 505
		private readonly MbEvent<Town> _dailyTickTownEvent = new MbEvent<Town>();

		// Token: 0x040001FA RID: 506
		private readonly MbEvent<Settlement> _dailyTickSettlementEvent = new MbEvent<Settlement>();

		// Token: 0x040001FB RID: 507
		private readonly MbEvent<Hero> _dailyTickHeroEvent = new MbEvent<Hero>();

		// Token: 0x040001FC RID: 508
		private readonly MbEvent<Clan> _dailyTickClanEvent = new MbEvent<Clan>();

		// Token: 0x040001FD RID: 509
		private readonly MbEvent<List<CampaignTutorial>> _collectAvailableTutorialsEvent = new MbEvent<List<CampaignTutorial>>();

		// Token: 0x040001FE RID: 510
		private readonly MbEvent<string> _onTutorialCompletedEvent = new MbEvent<string>();

		// Token: 0x040001FF RID: 511
		private readonly MbEvent<Town, Building, int> _onBuildingLevelChangedEvent = new MbEvent<Town, Building, int>();

		// Token: 0x04000200 RID: 512
		private readonly MbEvent _hourlyTickEvent = new MbEvent();

		// Token: 0x04000201 RID: 513
		private readonly MbEvent<MobileParty> _hourlyTickPartyEvent = new MbEvent<MobileParty>();

		// Token: 0x04000202 RID: 514
		private readonly MbEvent<Settlement> _hourlyTickSettlementEvent = new MbEvent<Settlement>();

		// Token: 0x04000203 RID: 515
		private readonly MbEvent<Clan> _hourlyTickClanEvent = new MbEvent<Clan>();

		// Token: 0x04000204 RID: 516
		private readonly MbEvent<float> _tickEvent = new MbEvent<float>();

		// Token: 0x04000205 RID: 517
		private readonly MbEvent<CampaignGameStarter> _onSessionLaunchedEvent = new MbEvent<CampaignGameStarter>();

		// Token: 0x04000206 RID: 518
		private readonly MbEvent<CampaignGameStarter> _onAfterSessionLaunchedEvent = new MbEvent<CampaignGameStarter>();

		// Token: 0x04000207 RID: 519
		public const int OnNewGameCreatedPartialFollowUpEventMaxIndex = 100;

		// Token: 0x04000208 RID: 520
		private readonly MbEvent<CampaignGameStarter> _onNewGameCreatedEvent = new MbEvent<CampaignGameStarter>();

		// Token: 0x04000209 RID: 521
		private readonly MbEvent<CampaignGameStarter, int> _onNewGameCreatedPartialFollowUpEvent = new MbEvent<CampaignGameStarter, int>();

		// Token: 0x0400020A RID: 522
		private readonly MbEvent<CampaignGameStarter> _onNewGameCreatedPartialFollowUpEndEvent = new MbEvent<CampaignGameStarter>();

		// Token: 0x0400020B RID: 523
		private readonly MbEvent<CampaignGameStarter> _onGameEarlyLoadedEvent = new MbEvent<CampaignGameStarter>();

		// Token: 0x0400020C RID: 524
		private readonly MbEvent<CampaignGameStarter> _onGameLoadedEvent = new MbEvent<CampaignGameStarter>();

		// Token: 0x0400020D RID: 525
		private readonly MbEvent _onGameLoadFinishedEvent = new MbEvent();

		// Token: 0x0400020E RID: 526
		private readonly MbEvent<MobileParty, PartyThinkParams> _aiHourlyTickEvent = new MbEvent<MobileParty, PartyThinkParams>();

		// Token: 0x0400020F RID: 527
		private readonly MbEvent<MobileParty> _tickPartialHourlyAiEvent = new MbEvent<MobileParty>();

		// Token: 0x04000210 RID: 528
		private readonly MbEvent<MobileParty> _onPartyJoinedArmyEvent = new MbEvent<MobileParty>();

		// Token: 0x04000211 RID: 529
		private readonly MbEvent<MobileParty> _onPartyRemovedFromArmyEvent = new MbEvent<MobileParty>();

		// Token: 0x04000212 RID: 530
		private readonly MbEvent _onPlayerArmyLeaderChangedBehaviorEvent = new MbEvent();

		// Token: 0x04000213 RID: 531
		private readonly MbEvent<IMission> _onMissionEndedEvent = new MbEvent<IMission>();

		// Token: 0x04000214 RID: 532
		private readonly MbEvent<MobileParty> _onQuarterDailyPartyTick = new MbEvent<MobileParty>();

		// Token: 0x04000215 RID: 533
		private readonly MbEvent<MapEvent> _onPlayerBattleEndEvent = new MbEvent<MapEvent>();

		// Token: 0x04000216 RID: 534
		private readonly MbEvent<CharacterObject, int> _onUnitRecruitedEvent = new MbEvent<CharacterObject, int>();

		// Token: 0x04000217 RID: 535
		private readonly MbEvent<Hero> _onChildConceived = new MbEvent<Hero>();

		// Token: 0x04000218 RID: 536
		private readonly MbEvent<Hero, List<Hero>, int> _onGivenBirthEvent = new MbEvent<Hero, List<Hero>, int>();

		// Token: 0x04000219 RID: 537
		private readonly MbEvent<float> _missionTickEvent = new MbEvent<float>();

		// Token: 0x0400021A RID: 538
		private MbEvent _armyOverlaySetDirty = new MbEvent();

		// Token: 0x0400021B RID: 539
		private readonly MbEvent<int> _playerDesertedBattle = new MbEvent<int>();

		// Token: 0x0400021C RID: 540
		private MbEvent<PartyBase> _partyVisibilityChanged = new MbEvent<PartyBase>();

		// Token: 0x0400021D RID: 541
		private readonly MbEvent<Track> _trackDetectedEvent = new MbEvent<Track>();

		// Token: 0x0400021E RID: 542
		private readonly MbEvent<Track> _trackLostEvent = new MbEvent<Track>();

		// Token: 0x0400021F RID: 543
		private readonly MbEvent<Dictionary<string, int>> _locationCharactersAreReadyToSpawn = new MbEvent<Dictionary<string, int>>();

		// Token: 0x04000220 RID: 544
		private readonly ReferenceMBEvent<MatrixFrame> _onBeforePlayerAgentSpawn = new ReferenceMBEvent<MatrixFrame>();

		// Token: 0x04000221 RID: 545
		private readonly MbEvent _onPlayerAgentSpawned = new MbEvent();

		// Token: 0x04000222 RID: 546
		private readonly MbEvent _locationCharactersSimulatedSpawned = new MbEvent();

		// Token: 0x04000223 RID: 547
		private readonly MbEvent<CharacterObject, CharacterObject, int> _playerUpgradedTroopsEvent = new MbEvent<CharacterObject, CharacterObject, int>();

		// Token: 0x04000224 RID: 548
		private readonly MbEvent<CharacterObject, CharacterObject, PartyBase, WeaponComponentData, bool, int> _onHeroCombatHitEvent = new MbEvent<CharacterObject, CharacterObject, PartyBase, WeaponComponentData, bool, int>();

		// Token: 0x04000225 RID: 549
		private readonly MbEvent<CharacterObject> _characterPortraitPopUpOpenedEvent = new MbEvent<CharacterObject>();

		// Token: 0x04000226 RID: 550
		private CampaignTimeControlMode _timeControlModeBeforePopUpOpened;

		// Token: 0x04000227 RID: 551
		private readonly MbEvent _characterPortraitPopUpClosedEvent = new MbEvent();

		// Token: 0x04000228 RID: 552
		private readonly MbEvent<Hero> _playerStartTalkFromMenu = new MbEvent<Hero>();

		// Token: 0x04000229 RID: 553
		private readonly MbEvent<GameMenu, GameMenuOption> _gameMenuOptionSelectedEvent = new MbEvent<GameMenu, GameMenuOption>();

		// Token: 0x0400022A RID: 554
		private readonly MbEvent<CharacterObject> _playerStartRecruitmentEvent = new MbEvent<CharacterObject>();

		// Token: 0x0400022B RID: 555
		private readonly MbEvent<Hero, Hero> _onBeforePlayerCharacterChangedEvent = new MbEvent<Hero, Hero>();

		// Token: 0x0400022C RID: 556
		private readonly MbEvent<Hero, Hero, MobileParty, bool> _onPlayerCharacterChangedEvent = new MbEvent<Hero, Hero, MobileParty, bool>();

		// Token: 0x0400022D RID: 557
		private readonly MbEvent<Hero, Hero> _onClanLeaderChangedEvent = new MbEvent<Hero, Hero>();

		// Token: 0x0400022E RID: 558
		private readonly MbEvent<SiegeEvent> _onSiegeEventStartedEvent = new MbEvent<SiegeEvent>();

		// Token: 0x0400022F RID: 559
		private readonly MbEvent _onPlayerSiegeStartedEvent = new MbEvent();

		// Token: 0x04000230 RID: 560
		private readonly MbEvent<SiegeEvent> _onSiegeEventEndedEvent = new MbEvent<SiegeEvent>();

		// Token: 0x04000231 RID: 561
		private readonly MbEvent<MobileParty, Settlement, SiegeAftermathAction.SiegeAftermath, Clan, Dictionary<MobileParty, float>> _siegeAftermathAppliedEvent = new MbEvent<MobileParty, Settlement, SiegeAftermathAction.SiegeAftermath, Clan, Dictionary<MobileParty, float>>();

		// Token: 0x04000232 RID: 562
		private readonly MbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, SiegeBombardTargets> _onSiegeBombardmentHitEvent = new MbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, SiegeBombardTargets>();

		// Token: 0x04000233 RID: 563
		private readonly MbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, bool> _onSiegeBombardmentWallHitEvent = new MbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType, bool>();

		// Token: 0x04000234 RID: 564
		private readonly MbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType> _onSiegeEngineDestroyedEvent = new MbEvent<MobileParty, Settlement, BattleSideEnum, SiegeEngineType>();

		// Token: 0x04000235 RID: 565
		private readonly MbEvent<List<TradeRumor>, Settlement> _onTradeRumorIsTakenEvent = new MbEvent<List<TradeRumor>, Settlement>();

		// Token: 0x04000236 RID: 566
		private readonly MbEvent<Hero> _onCheckForIssueEvent = new MbEvent<Hero>();

		// Token: 0x04000237 RID: 567
		private readonly MbEvent<IssueBase, IssueBase.IssueUpdateDetails, Hero> _onIssueUpdatedEvent = new MbEvent<IssueBase, IssueBase.IssueUpdateDetails, Hero>();

		// Token: 0x04000238 RID: 568
		private readonly MbEvent<MobileParty, TroopRoster> _onTroopsDesertedEvent = new MbEvent<MobileParty, TroopRoster>();

		// Token: 0x04000239 RID: 569
		private readonly MbEvent<Hero, Settlement, Hero, CharacterObject, int> _onTroopRecruitedEvent = new MbEvent<Hero, Settlement, Hero, CharacterObject, int>();

		// Token: 0x0400023A RID: 570
		private readonly MbEvent<Hero, Settlement, TroopRoster> _onTroopGivenToSettlementEvent = new MbEvent<Hero, Settlement, TroopRoster>();

		// Token: 0x0400023B RID: 571
		private readonly MbEvent<PartyBase, PartyBase, ItemRosterElement, int, Settlement> _onItemSoldEvent = new MbEvent<PartyBase, PartyBase, ItemRosterElement, int, Settlement>();

		// Token: 0x0400023C RID: 572
		private readonly MbEvent<MobileParty, Town, List<ValueTuple<EquipmentElement, int>>> _onCaravanTransactionCompletedEvent = new MbEvent<MobileParty, Town, List<ValueTuple<EquipmentElement, int>>>();

		// Token: 0x0400023D RID: 573
		private readonly MbEvent<PartyBase, PartyBase, TroopRoster> _onPrisonerSoldEvent = new MbEvent<PartyBase, PartyBase, TroopRoster>();

		// Token: 0x0400023E RID: 574
		private readonly MbEvent<MobileParty> _onPartyDisbandStartedEvent = new MbEvent<MobileParty>();

		// Token: 0x0400023F RID: 575
		private readonly MbEvent<MobileParty, Settlement> _onPartyDisbandedEvent = new MbEvent<MobileParty, Settlement>();

		// Token: 0x04000240 RID: 576
		private readonly MbEvent<MobileParty> _onPartyDisbandCanceledEvent = new MbEvent<MobileParty>();

		// Token: 0x04000241 RID: 577
		private readonly MbEvent<PartyBase, PartyBase> _hideoutSpottedEvent = new MbEvent<PartyBase, PartyBase>();

		// Token: 0x04000242 RID: 578
		private readonly MbEvent<Settlement> _hideoutDeactivatedEvent = new MbEvent<Settlement>();

		// Token: 0x04000243 RID: 579
		private readonly MbEvent<Hero, Hero, float> _heroSharedFoodWithAnotherHeroEvent = new MbEvent<Hero, Hero, float>();

		// Token: 0x04000244 RID: 580
		private readonly MbEvent<List<ValueTuple<ItemRosterElement, int>>, List<ValueTuple<ItemRosterElement, int>>, bool> _playerInventoryExchangeEvent = new MbEvent<List<ValueTuple<ItemRosterElement, int>>, List<ValueTuple<ItemRosterElement, int>>, bool>();

		// Token: 0x04000245 RID: 581
		private readonly MbEvent<ItemRoster> _onItemsDiscardedByPlayerEvent = new MbEvent<ItemRoster>();

		// Token: 0x04000246 RID: 582
		private readonly MbEvent<Tuple<PersuasionOptionArgs, PersuasionOptionResult>> _persuasionProgressCommittedEvent = new MbEvent<Tuple<PersuasionOptionArgs, PersuasionOptionResult>>();

		// Token: 0x04000247 RID: 583
		private readonly MbEvent<QuestBase, QuestBase.QuestCompleteDetails> _onQuestCompletedEvent = new MbEvent<QuestBase, QuestBase.QuestCompleteDetails>();

		// Token: 0x04000248 RID: 584
		private readonly MbEvent<QuestBase> _onQuestStartedEvent = new MbEvent<QuestBase>();

		// Token: 0x04000249 RID: 585
		private readonly MbEvent<ItemObject, Settlement, int> _itemProducedEvent = new MbEvent<ItemObject, Settlement, int>();

		// Token: 0x0400024A RID: 586
		private readonly MbEvent<ItemObject, Settlement, int> _itemConsumedEvent = new MbEvent<ItemObject, Settlement, int>();

		// Token: 0x0400024B RID: 587
		private readonly MbEvent<MobileParty> _onPartyConsumedFoodEvent = new MbEvent<MobileParty>();

		// Token: 0x0400024C RID: 588
		private readonly MbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool> _onBeforeMainCharacterDiedEvent = new MbEvent<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>();

		// Token: 0x0400024D RID: 589
		private readonly MbEvent<IssueBase> _onNewIssueCreatedEvent = new MbEvent<IssueBase>();

		// Token: 0x0400024E RID: 590
		private readonly MbEvent<IssueBase, Hero> _onIssueOwnerChangedEvent = new MbEvent<IssueBase, Hero>();

		// Token: 0x0400024F RID: 591
		private readonly MbEvent _onGameOverEvent = new MbEvent();

		// Token: 0x04000250 RID: 592
		private readonly MbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes> _siegeCompletedEvent = new MbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes>();

		// Token: 0x04000251 RID: 593
		private readonly MbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes> _afterSiegeCompletedEvent = new MbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes>();

		// Token: 0x04000252 RID: 594
		private readonly MbEvent<SiegeEvent, BattleSideEnum, SiegeEngineType> _siegeEngineBuiltEvent = new MbEvent<SiegeEvent, BattleSideEnum, SiegeEngineType>();

		// Token: 0x04000253 RID: 595
		private readonly MbEvent<BattleSideEnum, RaidEventComponent> _raidCompletedEvent = new MbEvent<BattleSideEnum, RaidEventComponent>();

		// Token: 0x04000254 RID: 596
		private readonly MbEvent<BattleSideEnum, ForceVolunteersEventComponent> _forceVolunteersCompletedEvent = new MbEvent<BattleSideEnum, ForceVolunteersEventComponent>();

		// Token: 0x04000255 RID: 597
		private readonly MbEvent<BattleSideEnum, ForceSuppliesEventComponent> _forceSuppliesCompletedEvent = new MbEvent<BattleSideEnum, ForceSuppliesEventComponent>();

		// Token: 0x04000256 RID: 598
		private readonly MbEvent<BattleSideEnum, HideoutEventComponent> _hideoutBattleCompletedEvent = new MbEvent<BattleSideEnum, HideoutEventComponent>();

		// Token: 0x04000257 RID: 599
		private readonly MbEvent<Clan> _onClanDestroyedEvent = new MbEvent<Clan>();

		// Token: 0x04000258 RID: 600
		private readonly MbEvent<ItemObject, ItemModifier, bool> _onNewItemCraftedEvent = new MbEvent<ItemObject, ItemModifier, bool>();

		// Token: 0x04000259 RID: 601
		private readonly MbEvent<CraftingPiece> _craftingPartUnlockedEvent = new MbEvent<CraftingPiece>();

		// Token: 0x0400025A RID: 602
		private readonly MbEvent<Workshop> _onWorkshopInitializedEvent = new MbEvent<Workshop>();

		// Token: 0x0400025B RID: 603
		private readonly MbEvent<Workshop, Hero> _onWorkshopOwnerChangedEvent = new MbEvent<Workshop, Hero>();

		// Token: 0x0400025C RID: 604
		private readonly MbEvent<Workshop> _onWorkshopTypeChangedEvent = new MbEvent<Workshop>();

		// Token: 0x0400025D RID: 605
		private readonly MbEvent _onBeforeSaveEvent = new MbEvent();

		// Token: 0x0400025E RID: 606
		private readonly MbEvent _onSaveStartedEvent = new MbEvent();

		// Token: 0x0400025F RID: 607
		private readonly MbEvent<bool, string> _onSaveOverEvent = new MbEvent<bool, string>();

		// Token: 0x04000260 RID: 608
		private readonly MbEvent<FlattenedTroopRoster> _onPrisonerTakenEvent = new MbEvent<FlattenedTroopRoster>();

		// Token: 0x04000261 RID: 609
		private readonly MbEvent<FlattenedTroopRoster> _onPrisonerReleasedEvent = new MbEvent<FlattenedTroopRoster>();

		// Token: 0x04000262 RID: 610
		private readonly MbEvent<FlattenedTroopRoster> _onMainPartyPrisonerRecruitedEvent = new MbEvent<FlattenedTroopRoster>();

		// Token: 0x04000263 RID: 611
		private readonly MbEvent<MobileParty, FlattenedTroopRoster, Settlement> _onPrisonerDonatedToSettlementEvent = new MbEvent<MobileParty, FlattenedTroopRoster, Settlement>();

		// Token: 0x04000264 RID: 612
		private readonly MbEvent<Hero, EquipmentElement> _onEquipmentSmeltedByHero = new MbEvent<Hero, EquipmentElement>();

		// Token: 0x04000265 RID: 613
		private readonly MbEvent<int> _onPlayerTradeProfit = new MbEvent<int>();

		// Token: 0x04000266 RID: 614
		private readonly MbEvent<Hero, Clan> _onHeroChangedClan = new MbEvent<Hero, Clan>();

		// Token: 0x04000267 RID: 615
		private readonly MbEvent<Hero, HeroGetsBusyReasons> _onHeroGetsBusy = new MbEvent<Hero, HeroGetsBusyReasons>();

		// Token: 0x04000268 RID: 616
		private readonly MbEvent<PartyBase, ItemRoster> _onCollectLootItems = new MbEvent<PartyBase, ItemRoster>();

		// Token: 0x04000269 RID: 617
		private readonly MbEvent<PartyBase, PartyBase, ItemRoster> _onLootDistributedToPartyEvent = new MbEvent<PartyBase, PartyBase, ItemRoster>();

		// Token: 0x0400026A RID: 618
		private readonly MbEvent<Hero, Settlement, MobileParty, TeleportHeroAction.TeleportationDetail> _onHeroTeleportationRequestedEvent = new MbEvent<Hero, Settlement, MobileParty, TeleportHeroAction.TeleportationDetail>();

		// Token: 0x0400026B RID: 619
		private readonly MbEvent<MobileParty> _onPartyLeaderChangeOfferCanceledEvent = new MbEvent<MobileParty>();

		// Token: 0x0400026C RID: 620
		private readonly MbEvent<MobileParty, Hero> _onPartyLeaderChangedEvent = new MbEvent<MobileParty, Hero>();

		// Token: 0x0400026D RID: 621
		private readonly MbEvent<Clan, float> _onClanInfluenceChangedEvent = new MbEvent<Clan, float>();

		// Token: 0x0400026E RID: 622
		private readonly MbEvent<CharacterObject> _onPlayerPartyKnockedOrKilledTroopEvent = new MbEvent<CharacterObject>();

		// Token: 0x0400026F RID: 623
		private readonly MbEvent<DefaultClanFinanceModel.AssetIncomeType, int> _onPlayerEarnedGoldFromAssetEvent = new MbEvent<DefaultClanFinanceModel.AssetIncomeType, int>();

		// Token: 0x04000270 RID: 624
		private readonly MbEvent<Clan, IFaction> _onClanEarnedGoldFromTributeEvent = new MbEvent<Clan, IFaction>();

		// Token: 0x04000271 RID: 625
		private readonly MbEvent _onMainPartyStarving = new MbEvent();

		// Token: 0x04000272 RID: 626
		private readonly MbEvent<Town, bool> _onPlayerJoinedTournamentEvent = new MbEvent<Town, bool>();

		// Token: 0x04000273 RID: 627
		private readonly MbEvent<Hero> _onHeroUnregisteredEvent = new MbEvent<Hero>();

		// Token: 0x04000274 RID: 628
		private readonly MbEvent _onConfigChanged = new MbEvent();

		// Token: 0x04000275 RID: 629
		private readonly MbEvent<Town, CraftingOrder, ItemObject, Hero> _onCraftingOrderCompleted = new MbEvent<Town, CraftingOrder, ItemObject, Hero>();

		// Token: 0x04000276 RID: 630
		private readonly MbEvent<Hero, Crafting.RefiningFormula> _onItemsRefined = new MbEvent<Hero, Crafting.RefiningFormula>();

		// Token: 0x04000277 RID: 631
		private readonly MbEvent<Dictionary<Hero, int>> _onHeirSelectionRequested = new MbEvent<Dictionary<Hero, int>>();

		// Token: 0x04000278 RID: 632
		private readonly MbEvent<Hero> _onHeirSelectionOver = new MbEvent<Hero>();

		// Token: 0x04000279 RID: 633
		private readonly MbEvent<MobileParty> _onMobilePartyRaftStateChanged = new MbEvent<MobileParty>();

		// Token: 0x0400027A RID: 634
		private readonly MbEvent<CharacterCreationManager> _onCharacterCreationInitialized = new MbEvent<CharacterCreationManager>();

		// Token: 0x0400027B RID: 635
		private readonly MbEvent<PartyBase, Ship, DestroyShipAction.ShipDestroyDetail> _onShipDestroyedEvent = new MbEvent<PartyBase, Ship, DestroyShipAction.ShipDestroyDetail>();

		// Token: 0x0400027C RID: 636
		private readonly MbEvent<Ship, PartyBase, ChangeShipOwnerAction.ShipOwnerChangeDetail> _onShipOwnerChangedEvent = new MbEvent<Ship, PartyBase, ChangeShipOwnerAction.ShipOwnerChangeDetail>();

		// Token: 0x0400027D RID: 637
		private readonly MbEvent<Ship, Settlement> _onShipRepairedEvent = new MbEvent<Ship, Settlement>();

		// Token: 0x0400027E RID: 638
		private readonly MbEvent<Ship, Settlement> _onShipCreatedEvent = new MbEvent<Ship, Settlement>();

		// Token: 0x0400027F RID: 639
		private readonly MbEvent<Figurehead> _onFigureheadUnlockedEvent = new MbEvent<Figurehead>();

		// Token: 0x04000280 RID: 640
		private readonly MbEvent<MobileParty, Army> _onPartyLeftArmyEvent = new MbEvent<MobileParty, Army>();

		// Token: 0x04000281 RID: 641
		private readonly MbEvent<PartyBase> _onPartyAddedToMapEventEvent = new MbEvent<PartyBase>();

		// Token: 0x04000282 RID: 642
		private readonly MbEvent<Incident> _onIncidentResolvedEvent = new MbEvent<Incident>();

		// Token: 0x04000283 RID: 643
		private readonly MbEvent<MobileParty> _onMobilePartyNavigationStateChangedEvent = new MbEvent<MobileParty>();

		// Token: 0x04000284 RID: 644
		private readonly MbEvent<MobileParty> _onMobilePartyJoinedToSiegeEventEvent = new MbEvent<MobileParty>();

		// Token: 0x04000285 RID: 645
		private readonly MbEvent<MobileParty> _onMobilePartyLeftSiegeEventEvent = new MbEvent<MobileParty>();

		// Token: 0x04000286 RID: 646
		private readonly MbEvent<SiegeEvent> _onBlockadeActivatedEvent = new MbEvent<SiegeEvent>();

		// Token: 0x04000287 RID: 647
		private readonly MbEvent<SiegeEvent> _onBlockadeDeactivatedEvent = new MbEvent<SiegeEvent>();

		// Token: 0x04000288 RID: 648
		private readonly MbEvent<MapMarker> _onMapMarkerCreatedEvent = new MbEvent<MapMarker>();

		// Token: 0x04000289 RID: 649
		private readonly MbEvent<MapMarker> _onMapMarkerRemovedEvent = new MbEvent<MapMarker>();

		// Token: 0x0400028A RID: 650
		private readonly MbEvent<Kingdom, Kingdom> _onAllianceStartedEvent = new MbEvent<Kingdom, Kingdom>();

		// Token: 0x0400028B RID: 651
		private readonly MbEvent<Kingdom, Kingdom> _onAllianceEndedEvent = new MbEvent<Kingdom, Kingdom>();

		// Token: 0x0400028C RID: 652
		private readonly MbEvent<Kingdom, Kingdom, Kingdom> _onCallToWarAgreementStartedEvent = new MbEvent<Kingdom, Kingdom, Kingdom>();

		// Token: 0x0400028D RID: 653
		private readonly MbEvent<Kingdom, Kingdom, Kingdom> _onCallToWarAgreementEndedEvent = new MbEvent<Kingdom, Kingdom, Kingdom>();

		// Token: 0x0400028E RID: 654
		private readonly ReferenceMBEvent<Hero, bool> _canHeroLeadPartyEvent = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x0400028F RID: 655
		private readonly ReferenceMBEvent<Hero, bool> _canMarryEvent = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x04000290 RID: 656
		private readonly ReferenceMBEvent<Hero, bool> _canHeroEquipmentBeChangedEvent = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x04000291 RID: 657
		private readonly ReferenceMBEvent<Hero, bool> _canBeGovernorOrHavePartyRoleEvent = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x04000292 RID: 658
		private readonly ReferenceMBEvent<Hero, KillCharacterAction.KillCharacterActionDetail, bool> _canHeroDieEvent = new ReferenceMBEvent<Hero, KillCharacterAction.KillCharacterActionDetail, bool>();

		// Token: 0x04000293 RID: 659
		private readonly ReferenceMBEvent<Hero, bool> _canPlayerMeetWithHeroAfterConversationEvent = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x04000294 RID: 660
		private readonly ReferenceMBEvent<Hero, bool> _canHeroBecomePrisonerEvent = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x04000295 RID: 661
		private readonly ReferenceMBEvent<Hero, bool> _canMoveToSettlementEvent = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x04000296 RID: 662
		private readonly ReferenceMBEvent<Hero, bool> _canHaveCampaignIssues = new ReferenceMBEvent<Hero, bool>();

		// Token: 0x04000297 RID: 663
		private readonly ReferenceMBEvent<Settlement, object, int> _isSettlementBusy = new ReferenceMBEvent<Settlement, object, int>();

		// Token: 0x04000298 RID: 664
		private readonly MbEvent<IFaction> _onMapEventContinuityNeedsUpdate = new MbEvent<IFaction>();
	}
}
