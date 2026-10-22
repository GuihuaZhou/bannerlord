using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x02000419 RID: 1049
	public class MarriageOfferCampaignBehavior : CampaignBehaviorBase, IMarriageOfferCampaignBehavior, ICampaignBehavior
	{
		// Token: 0x17000E41 RID: 3649
		// (get) Token: 0x06004324 RID: 17188 RVA: 0x00144BD9 File Offset: 0x00142DD9
		internal bool IsThereActiveMarriageOffer
		{
			get
			{
				return this._currentOfferedPlayerClanHero != null && this._currentOfferedOtherClanHero != null;
			}
		}

		// Token: 0x06004325 RID: 17189 RVA: 0x00144BF0 File Offset: 0x00142DF0
		public override void RegisterEvents()
		{
			CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, new Action<Clan>(this.DailyTickClan));
			CampaignEvents.OnMarriageOfferedToPlayerEvent.AddNonSerializedListener(this, new Action<Hero, Hero>(this.OnMarriageOfferedToPlayer));
			CampaignEvents.OnMarriageOfferCanceledEvent.AddNonSerializedListener(this, new Action<Hero, Hero>(this.OnMarriageOfferCanceled));
			CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(this.HourlyTick));
			CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, new Action<PartyBase, Hero>(this.OnHeroPrisonerTaken));
			CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, new Action<Hero, Hero, bool>(this.OnHeroesMarried));
			CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, new Action<Hero, Hero, KillCharacterAction.KillCharacterActionDetail, bool>(this.OnHeroKilled));
			CampaignEvents.ArmyCreated.AddNonSerializedListener(this, new Action<Army>(this.OnArmyCreated));
			CampaignEvents.MapEventStarted.AddNonSerializedListener(this, new Action<MapEvent, PartyBase, PartyBase>(this.OnMapEventStarted));
			CampaignEvents.CharacterBecameFugitiveEvent.AddNonSerializedListener(this, new Action<Hero, bool>(this.CharacterBecameFugitive));
			CampaignEvents.WarDeclared.AddNonSerializedListener(this, new Action<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>(this.OnWarDeclared));
			CampaignEvents.HeroRelationChanged.AddNonSerializedListener(this, new Action<Hero, Hero, int, bool, ChangeRelationAction.ChangeRelationDetail, Hero, Hero>(this.OnHeroRelationChanged));
			CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, new Action<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>(this.OnClanChangedKingdom));
		}

		// Token: 0x06004326 RID: 17190 RVA: 0x00144D2C File Offset: 0x00142F2C
		public override void SyncData(IDataStore dataStore)
		{
			dataStore.SyncData<Hero>("_currentOfferedPlayerClanHero", ref this._currentOfferedPlayerClanHero);
			dataStore.SyncData<Hero>("_currentOfferedOtherClanHero", ref this._currentOfferedOtherClanHero);
			dataStore.SyncData<CampaignTime>("_lastMarriageOfferTime", ref this._lastMarriageOfferTime);
			dataStore.SyncData<Dictionary<Hero, Hero>>("_acceptedMarriageOffersThatWaitingForAvailability", ref this._acceptedMarriageOffersThatWaitingForAvailability);
		}

		// Token: 0x06004327 RID: 17191 RVA: 0x00144D84 File Offset: 0x00142F84
		public void CreateMarriageOffer(Hero currentOfferedPlayerClanHero, Hero currentOfferedOtherClanHero)
		{
			this._currentOfferedPlayerClanHero = currentOfferedPlayerClanHero;
			this._currentOfferedOtherClanHero = currentOfferedOtherClanHero;
			this._lastMarriageOfferTime = CampaignTime.Now;
			this.MarriageOfferPanelExplanationText.SetCharacterProperties("CLAN_MEMBER", this._currentOfferedPlayerClanHero.CharacterObject, false);
			this.MarriageOfferPanelExplanationText.SetTextVariable("OFFERING_CLAN_NAME", this._currentOfferedOtherClanHero.Clan.Name);
			Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new MarriageOfferMapNotification(this._currentOfferedPlayerClanHero, this._currentOfferedOtherClanHero, this.MarriageOfferPanelExplanationText));
		}

		// Token: 0x06004328 RID: 17192 RVA: 0x00144E10 File Offset: 0x00143010
		public MBBindingList<TextObject> GetMarriageAcceptedConsequences()
		{
			MBBindingList<TextObject> mbbindingList = new MBBindingList<TextObject>();
			TextObject textObject = GameTexts.FindText("str_marriage_consequence_hero_join_clan", null);
			if (Campaign.Current.Models.MarriageModel.GetClanAfterMarriage(this._currentOfferedPlayerClanHero, this._currentOfferedOtherClanHero) == this._currentOfferedPlayerClanHero.Clan)
			{
				textObject.SetCharacterProperties("HERO", this._currentOfferedOtherClanHero.CharacterObject, false);
				textObject.SetTextVariable("CLAN_NAME", this._currentOfferedPlayerClanHero.Clan.Name);
			}
			else
			{
				textObject.SetCharacterProperties("HERO", this._currentOfferedPlayerClanHero.CharacterObject, false);
				textObject.SetTextVariable("CLAN_NAME", this._currentOfferedOtherClanHero.Clan.Name);
			}
			mbbindingList.Add(textObject);
			TextObject textObject2 = GameTexts.FindText("str_marriage_consequence_clan_relation", null);
			textObject2.SetTextVariable("CLAN_NAME", this._currentOfferedOtherClanHero.Clan.Name);
			textObject2.SetTextVariable("AMOUNT", 10.ToString("+0;-#"));
			mbbindingList.Add(textObject2);
			return mbbindingList;
		}

		// Token: 0x06004329 RID: 17193 RVA: 0x00144F14 File Offset: 0x00143114
		private void DailyTickClan(Clan consideringClan)
		{
			if (this.CanOfferMarriageForClan(consideringClan))
			{
				MobileParty.NavigationType navigationType = consideringClan.HasNavalNavigationCapability ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;
				float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(Clan.PlayerClan.FactionMidSettlement, consideringClan.FactionMidSettlement, false, false, navigationType);
				if (MBRandom.RandomFloat >= distance / Campaign.Current.Models.MapDistanceModel.GetMaximumDistanceBetweenTwoConnectedSettlements(navigationType) - 0.5f)
				{
					foreach (Hero hero in Clan.PlayerClan.Heroes)
					{
						if (hero != Hero.MainHero && hero.CanMarry() && !this._acceptedMarriageOffersThatWaitingForAvailability.ContainsKey(hero) && this.ConsiderMarriageForPlayerClanMember(hero, consideringClan))
						{
							break;
						}
					}
				}
			}
		}

		// Token: 0x0600432A RID: 17194 RVA: 0x00144FF4 File Offset: 0x001431F4
		private void MarryHeroesViaOffer(Hero playerClanHero, Hero otherClanHero, bool showSceneNotification)
		{
			if (playerClanHero != Hero.MainHero && showSceneNotification)
			{
				Hero groomHero = playerClanHero.IsFemale ? otherClanHero : playerClanHero;
				Hero brideHero = playerClanHero.IsFemale ? playerClanHero : otherClanHero;
				MBInformationManager.ShowSceneNotification(new MarriageSceneNotificationItem(groomHero, brideHero, CampaignTime.Now, SceneNotificationData.RelevantContextType.Any));
			}
			ChangeRelationAction.ApplyPlayerRelation(otherClanHero.Clan.Leader, 10, true, true);
			MarriageAction.Apply(playerClanHero, otherClanHero, true);
		}

		// Token: 0x0600432B RID: 17195 RVA: 0x00145058 File Offset: 0x00143258
		public void OnMarriageOfferAcceptedOnPopUp()
		{
			if (Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(this._currentOfferedPlayerClanHero, this._currentOfferedOtherClanHero))
			{
				this.MarryHeroesViaOffer(this._currentOfferedPlayerClanHero, this._currentOfferedOtherClanHero, true);
			}
			else
			{
				this._acceptedMarriageOffersThatWaitingForAvailability.Add(this._currentOfferedPlayerClanHero, this._currentOfferedOtherClanHero);
				InformationManager.ShowInquiry(new InquiryData(string.Empty, this.MarriagePreparationStartedText.ToString(), true, false, GameTexts.FindText("str_ok", null).ToString(), string.Empty, null, null, "", 0f, null, null, null), false, false);
			}
			this.FinalizeMarriageOffer();
		}

		// Token: 0x0600432C RID: 17196 RVA: 0x001450FB File Offset: 0x001432FB
		public void OnMarriageOfferedToPlayer(Hero suitor, Hero maiden)
		{
		}

		// Token: 0x0600432D RID: 17197 RVA: 0x00145100 File Offset: 0x00143300
		public void OnMarriageOfferDeclinedOnPopUp()
		{
			CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
		}

		// Token: 0x0600432E RID: 17198 RVA: 0x0014514D File Offset: 0x0014334D
		public void OnMarriageOfferCanceled(Hero suitor, Hero maiden)
		{
			this.FinalizeMarriageOffer();
		}

		// Token: 0x0600432F RID: 17199 RVA: 0x00145158 File Offset: 0x00143358
		public bool IsHeroEngaged(Hero hero)
		{
			foreach (KeyValuePair<Hero, Hero> keyValuePair in this._acceptedMarriageOffersThatWaitingForAvailability)
			{
				if (keyValuePair.Key == hero || keyValuePair.Value == hero)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x06004330 RID: 17200 RVA: 0x001451C0 File Offset: 0x001433C0
		private void HourlyTick()
		{
			if (this.IsThereActiveMarriageOffer && this._lastMarriageOfferTime.ElapsedDaysUntilNow >= 2f)
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
			MarriageModel marriageModel = Campaign.Current.Models.MarriageModel;
			List<Hero> list = new List<Hero>();
			foreach (KeyValuePair<Hero, Hero> keyValuePair in this._acceptedMarriageOffersThatWaitingForAvailability)
			{
				Hero key = keyValuePair.Key;
				Hero value = keyValuePair.Value;
				if (marriageModel.IsCoupleSuitableForMarriage(key, value))
				{
					this.MarryHeroesViaOffer(key, value, false);
					list.Add(key);
				}
				else if (!marriageModel.ShouldNpcMarriageBetweenClansBeAllowed(Clan.PlayerClan, value.Clan))
				{
					list.Add(key);
				}
			}
			foreach (Hero key2 in list)
			{
				this._acceptedMarriageOffersThatWaitingForAvailability.Remove(key2);
			}
		}

		// Token: 0x06004331 RID: 17201 RVA: 0x00145314 File Offset: 0x00143514
		private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
		{
			if (this.IsThereActiveMarriageOffer && (prisoner == Hero.MainHero || prisoner == this._currentOfferedPlayerClanHero || prisoner == this._currentOfferedOtherClanHero))
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x06004332 RID: 17202 RVA: 0x00145384 File Offset: 0x00143584
		private void OnHeroesMarried(Hero hero1, Hero hero2, bool showNotification = true)
		{
			if (this.IsThereActiveMarriageOffer && ((hero1 == this._currentOfferedPlayerClanHero && hero2 == this._currentOfferedOtherClanHero) || (hero1 == this._currentOfferedOtherClanHero && hero2 == this._currentOfferedPlayerClanHero)))
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x06004333 RID: 17203 RVA: 0x00145400 File Offset: 0x00143600
		private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
		{
			if (this.IsThereActiveMarriageOffer && (victim == Hero.MainHero || victim == this._currentOfferedPlayerClanHero || victim == this._currentOfferedOtherClanHero))
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
			Hero hero = null;
			foreach (KeyValuePair<Hero, Hero> keyValuePair in this._acceptedMarriageOffersThatWaitingForAvailability)
			{
				if (keyValuePair.Key == victim || keyValuePair.Value == victim)
				{
					hero = keyValuePair.Key;
				}
			}
			if (hero != null)
			{
				Hero hero2 = null;
				Hero hero3 = null;
				if (hero.IsDead)
				{
					hero2 = hero;
					hero3 = this._acceptedMarriageOffersThatWaitingForAvailability[hero];
				}
				else if (this._acceptedMarriageOffersThatWaitingForAvailability[hero].IsDead)
				{
					hero2 = this._acceptedMarriageOffersThatWaitingForAvailability[hero];
					hero3 = hero;
				}
				this.MarriagePreparationCanceledText.SetCharacterProperties("DEAD_HERO", hero2.CharacterObject, false);
				this.MarriagePreparationCanceledText.SetTextVariable("OTHER_HERO", hero3.Name);
				InformationManager.ShowInquiry(new InquiryData(string.Empty, this.MarriagePreparationCanceledText.ToString(), true, false, GameTexts.FindText("str_ok", null).ToString(), string.Empty, null, null, "", 0f, null, null, null), false, false);
				this._acceptedMarriageOffersThatWaitingForAvailability.Remove(hero);
			}
		}

		// Token: 0x06004334 RID: 17204 RVA: 0x00145594 File Offset: 0x00143794
		private void OnArmyCreated(Army army)
		{
			if (this.IsThereActiveMarriageOffer)
			{
				MobileParty partyBelongedTo = this._currentOfferedPlayerClanHero.PartyBelongedTo;
				if (((partyBelongedTo != null) ? partyBelongedTo.Army : null) == null)
				{
					MobileParty partyBelongedTo2 = this._currentOfferedOtherClanHero.PartyBelongedTo;
					if (((partyBelongedTo2 != null) ? partyBelongedTo2.Army : null) == null)
					{
						return;
					}
				}
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x06004335 RID: 17205 RVA: 0x0014561C File Offset: 0x0014381C
		private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
		{
			if (this.IsThereActiveMarriageOffer)
			{
				MobileParty partyBelongedTo = this._currentOfferedPlayerClanHero.PartyBelongedTo;
				if (((partyBelongedTo != null) ? partyBelongedTo.MapEvent : null) == null)
				{
					MobileParty partyBelongedTo2 = this._currentOfferedOtherClanHero.PartyBelongedTo;
					if (((partyBelongedTo2 != null) ? partyBelongedTo2.MapEvent : null) == null)
					{
						return;
					}
				}
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x06004336 RID: 17206 RVA: 0x001456A4 File Offset: 0x001438A4
		private void CharacterBecameFugitive(Hero hero, bool showNotification)
		{
			if (this.IsThereActiveMarriageOffer && (!this._currentOfferedPlayerClanHero.IsActive || !this._currentOfferedOtherClanHero.IsActive))
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x06004337 RID: 17207 RVA: 0x00145714 File Offset: 0x00143914
		private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
		{
			if (this.IsThereActiveMarriageOffer && (!Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(this._currentOfferedPlayerClanHero, this._currentOfferedOtherClanHero) || !Campaign.Current.Models.MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(Clan.PlayerClan, this._currentOfferedOtherClanHero.Clan)))
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x06004338 RID: 17208 RVA: 0x001457B4 File Offset: 0x001439B4
		private void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange, bool showNotification, ChangeRelationAction.ChangeRelationDetail detail, Hero originalHero, Hero originalGainedRelationWith)
		{
			if (this.IsThereActiveMarriageOffer && (effectiveHero.Clan == this._currentOfferedPlayerClanHero.Clan || effectiveHero.Clan == this._currentOfferedOtherClanHero.Clan) && (effectiveHeroGainedRelationWith.Clan == this._currentOfferedPlayerClanHero.Clan || effectiveHeroGainedRelationWith.Clan == this._currentOfferedOtherClanHero.Clan) && !Campaign.Current.Models.MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(this._currentOfferedPlayerClanHero.Clan, this._currentOfferedOtherClanHero.Clan))
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x06004339 RID: 17209 RVA: 0x00145888 File Offset: 0x00143A88
		private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
		{
			if (this.IsThereActiveMarriageOffer && (this._currentOfferedPlayerClanHero.Clan == clan || this._currentOfferedOtherClanHero.Clan == clan) && !Campaign.Current.Models.MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(this._currentOfferedPlayerClanHero.Clan, this._currentOfferedOtherClanHero.Clan))
			{
				CampaignEventDispatcher.Instance.OnMarriageOfferCanceled(this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedOtherClanHero : this._currentOfferedPlayerClanHero, this._currentOfferedPlayerClanHero.IsFemale ? this._currentOfferedPlayerClanHero : this._currentOfferedOtherClanHero);
			}
		}

		// Token: 0x0600433A RID: 17210 RVA: 0x00145928 File Offset: 0x00143B28
		private bool CanOfferMarriageForClan(Clan consideringClan)
		{
			return !this.IsThereActiveMarriageOffer && this._lastMarriageOfferTime.ElapsedDaysUntilNow >= 7f && !Hero.MainHero.IsPrisoner && !MobileParty.MainParty.IsInRaftState && consideringClan != Clan.PlayerClan && Campaign.Current.Models.MarriageModel.IsClanSuitableForMarriage(consideringClan) && Campaign.Current.Models.MarriageModel.ShouldNpcMarriageBetweenClansBeAllowed(Clan.PlayerClan, consideringClan);
		}

		// Token: 0x0600433B RID: 17211 RVA: 0x001459A4 File Offset: 0x00143BA4
		private bool ConsiderMarriageForPlayerClanMember(Hero playerClanHero, Clan consideringClan)
		{
			MarriageModel marriageModel = Campaign.Current.Models.MarriageModel;
			foreach (Hero hero in consideringClan.Heroes)
			{
				float num = marriageModel.NpcCoupleMarriageChance(playerClanHero, hero);
				if (num > 0f && MBRandom.RandomFloat < num)
				{
					foreach (Romance.RomanticState romanticState in Romance.RomanticStateList)
					{
						if (romanticState.Level >= Romance.RomanceLevelEnum.MatchMadeByFamily && (romanticState.Person1 == playerClanHero || romanticState.Person2 == playerClanHero || romanticState.Person1 == hero || romanticState.Person2 == hero))
						{
							return false;
						}
					}
					this.CreateMarriageOffer(playerClanHero, hero);
					return true;
				}
			}
			return false;
		}

		// Token: 0x0600433C RID: 17212 RVA: 0x00145AA4 File Offset: 0x00143CA4
		private void FinalizeMarriageOffer()
		{
			this._currentOfferedPlayerClanHero = null;
			this._currentOfferedOtherClanHero = null;
		}

		// Token: 0x04001336 RID: 4918
		private const int MarriageOfferCooldownDurationAsDays = 7;

		// Token: 0x04001337 RID: 4919
		private const int OfferRelationGainAmountWithTheMarriageClan = 10;

		// Token: 0x04001338 RID: 4920
		private const float MapNotificationAutoDeclineDurationInDays = 2f;

		// Token: 0x04001339 RID: 4921
		private readonly TextObject MarriageOfferPanelExplanationText = new TextObject("{=CZwrlJMJ}A courier with a marriage offer for {CLAN_MEMBER.NAME} from {OFFERING_CLAN_NAME} has arrived.", null);

		// Token: 0x0400133A RID: 4922
		private readonly TextObject MarriagePreparationStartedText = new TextObject("{=yz78jqbx}Ceremony is being prepared and will be conducted as soon as possible.", null);

		// Token: 0x0400133B RID: 4923
		private readonly TextObject MarriagePreparationCanceledText = new TextObject("{=dp044PNk}Due to the untimely death of {DEAD_HERO}, {?DEAD_HERO.GENDER}her{?}his{\\\\?} marriage with {OTHER_HERO} has been cancelled.", null);

		// Token: 0x0400133C RID: 4924
		private Hero _currentOfferedPlayerClanHero;

		// Token: 0x0400133D RID: 4925
		private Hero _currentOfferedOtherClanHero;

		// Token: 0x0400133E RID: 4926
		private CampaignTime _lastMarriageOfferTime;

		// Token: 0x0400133F RID: 4927
		private Dictionary<Hero, Hero> _acceptedMarriageOffersThatWaitingForAvailability = new Dictionary<Hero, Hero>();
	}
}
