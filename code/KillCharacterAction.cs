using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.Actions
{
	// Token: 0x020004B9 RID: 1209
	public static class KillCharacterAction
	{
		// Token: 0x06004AB6 RID: 19126 RVA: 0x00179D8C File Offset: 0x00177F8C
		private static void ApplyInternal(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail actionDetail, bool showNotification, bool isForced = false)
		{
			if (!victim.CanDie(actionDetail) && !isForced)
			{
				return;
			}
			if (!victim.IsAlive)
			{
				Debug.FailedAssert("Victim: " + victim.Name + " is already dead!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Actions\\KillCharacterAction.cs", "ApplyInternal", 41);
				return;
			}
			if (victim.IsNotable)
			{
				IssueBase issue = victim.Issue;
				if (((issue != null) ? issue.IssueQuest : null) != null)
				{
					Debug.FailedAssert("Trying to kill a notable that has quest!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Actions\\KillCharacterAction.cs", "ApplyInternal", 48);
				}
			}
			MobileParty partyBelongedTo = victim.PartyBelongedTo;
			if (((partyBelongedTo != null) ? partyBelongedTo.MapEvent : null) == null)
			{
				MobileParty partyBelongedTo2 = victim.PartyBelongedTo;
				if (((partyBelongedTo2 != null) ? partyBelongedTo2.SiegeEvent : null) == null && actionDetail != KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
				{
					goto IL_E6;
				}
			}
			if (victim.DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
			{
				victim.AddDeathMark(killer, actionDetail);
				return;
			}
			IL_E6:
			if (victim.IsHumanPlayerCharacter && !isForced)
			{
				CampaignEventDispatcher.Instance.OnBeforeMainCharacterDied(victim, killer, actionDetail, showNotification);
				return;
			}
			CampaignEventDispatcher.Instance.OnBeforeHeroKilled(victim, killer, actionDetail, showNotification);
			victim.AddDeathMark(killer, actionDetail);
			victim.EncyclopediaText = KillCharacterAction.CreateObituary(victim, actionDetail);
			if (victim.Clan != null)
			{
				if (victim.Clan.Leader == victim || victim == Hero.MainHero)
				{
					if (!victim.Clan.IsEliminated && victim != Hero.MainHero && victim.Clan.Heroes.Any((Hero x) => !x.IsChild && x != victim && x.IsAlive && x.IsLord))
					{
						ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader(victim.Clan);
					}
					if (victim.Clan.Kingdom != null && victim.Clan.Kingdom.RulingClan == victim.Clan)
					{
						List<Clan> list = (from t in victim.Clan.Kingdom.Clans
						where !t.IsEliminated && t.Leader != victim && !t.IsUnderMercenaryService
						select t).ToList<Clan>();
						if (list.IsEmpty<Clan>())
						{
							if (!victim.Clan.Kingdom.IsEliminated)
							{
								DestroyKingdomAction.ApplyByKingdomLeaderDeath(victim.Clan.Kingdom);
							}
						}
						else if (!victim.Clan.Kingdom.IsEliminated)
						{
							if (list.Count > 1)
							{
								Clan clanToExclude = (victim.Clan.Leader == victim || victim.Clan.Leader == null) ? victim.Clan : null;
								victim.Clan.Kingdom.AddDecision(new KingSelectionKingdomDecision(victim.Clan, clanToExclude), true);
								if (clanToExclude != null)
								{
									Clan randomElementWithPredicate = victim.Clan.Kingdom.Clans.GetRandomElementWithPredicate((Clan t) => t != clanToExclude && Campaign.Current.Models.DiplomacyModel.IsClanEligibleToBecomeRuler(t));
									ChangeRulingClanAction.Apply(victim.Clan.Kingdom, randomElementWithPredicate);
								}
							}
							else
							{
								ChangeRulingClanAction.Apply(victim.Clan.Kingdom, list[0]);
							}
						}
					}
				}
				else
				{
					GiveGoldAction.ApplyBetweenCharacters(victim, victim.Clan.Leader, victim.Gold, false);
				}
			}
			if (victim.PartyBelongedTo != null && (victim.PartyBelongedTo.LeaderHero == victim || victim == Hero.MainHero))
			{
				MobileParty partyBelongedTo3 = victim.PartyBelongedTo;
				if (victim.PartyBelongedTo.Army != null)
				{
					if (victim.PartyBelongedTo.Army.LeaderParty == victim.PartyBelongedTo)
					{
						DisbandArmyAction.ApplyByArmyLeaderIsDead(victim.PartyBelongedTo.Army);
					}
					else
					{
						victim.PartyBelongedTo.Army = null;
					}
				}
				if (partyBelongedTo3 != MobileParty.MainParty)
				{
					partyBelongedTo3.SetMoveModeHold();
					if (victim.Clan != null && victim.Clan.IsRebelClan)
					{
						DestroyPartyAction.Apply(null, partyBelongedTo3);
					}
				}
			}
			KillCharacterAction.MakeDead(victim, true);
			if (victim.GovernorOf != null)
			{
				ChangeGovernorAction.RemoveGovernorOf(victim);
			}
			if ((actionDetail == KillCharacterAction.KillCharacterActionDetail.Executed || actionDetail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent) && killer == Hero.MainHero && victim.Clan != null && victim.GetTraitLevel(DefaultTraits.Honor) >= 0)
			{
				TraitLevelingHelper.OnLordExecuted();
			}
			if (victim.Clan != null && !victim.Clan.IsEliminated && !victim.Clan.IsBanditFaction && victim.Clan != Clan.PlayerClan)
			{
				if (victim.Clan.Leader == victim)
				{
					DestroyClanAction.ApplyByClanLeaderDeath(victim.Clan);
				}
				else if (victim.Clan.Leader == null)
				{
					DestroyClanAction.Apply(victim.Clan);
				}
			}
			CampaignEventDispatcher.Instance.OnHeroKilled(victim, killer, actionDetail, showNotification);
			if (victim.Spouse != null)
			{
				victim.Spouse = null;
			}
			if (victim.CompanionOf != null)
			{
				RemoveCompanionAction.ApplyByDeath(victim.CompanionOf, victim);
			}
			if (victim.CurrentSettlement != null)
			{
				if (victim.CurrentSettlement == Settlement.CurrentSettlement)
				{
					LocationComplex locationComplex = LocationComplex.Current;
					if (locationComplex != null)
					{
						locationComplex.RemoveCharacterIfExists(victim);
					}
				}
				if (victim.StayingInSettlement != null)
				{
					victim.StayingInSettlement = null;
				}
			}
			if (!victim.IsHumanPlayerCharacter)
			{
				victim.OnDeath();
			}
		}

		// Token: 0x06004AB7 RID: 19127 RVA: 0x0017A3A4 File Offset: 0x001785A4
		public static void ApplyByOldAge(Hero victim, bool showNotification = true)
		{
			KillCharacterAction.ApplyInternal(victim, null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge, showNotification, false);
		}

		// Token: 0x06004AB8 RID: 19128 RVA: 0x0017A3B0 File Offset: 0x001785B0
		public static void ApplyByWounds(Hero victim, bool showNotification = true)
		{
			KillCharacterAction.ApplyInternal(victim, null, KillCharacterAction.KillCharacterActionDetail.WoundedInBattle, showNotification, false);
		}

		// Token: 0x06004AB9 RID: 19129 RVA: 0x0017A3BC File Offset: 0x001785BC
		public static void ApplyByBattle(Hero victim, Hero killer, bool showNotification = true)
		{
			KillCharacterAction.ApplyInternal(victim, killer, KillCharacterAction.KillCharacterActionDetail.DiedInBattle, showNotification, false);
		}

		// Token: 0x06004ABA RID: 19130 RVA: 0x0017A3C8 File Offset: 0x001785C8
		public static void ApplyByMurder(Hero victim, Hero killer = null, bool showNotification = true)
		{
			KillCharacterAction.ApplyInternal(victim, killer, KillCharacterAction.KillCharacterActionDetail.Murdered, showNotification, false);
		}

		// Token: 0x06004ABB RID: 19131 RVA: 0x0017A3D4 File Offset: 0x001785D4
		public static void ApplyInLabor(Hero lostMother, bool showNotification = true)
		{
			KillCharacterAction.ApplyInternal(lostMother, null, KillCharacterAction.KillCharacterActionDetail.DiedInLabor, showNotification, false);
		}

		// Token: 0x06004ABC RID: 19132 RVA: 0x0017A3E0 File Offset: 0x001785E0
		public static void ApplyByExecution(Hero victim, Hero executer, bool showNotification = true, bool isForced = false)
		{
			KillCharacterAction.ApplyInternal(victim, executer, KillCharacterAction.KillCharacterActionDetail.Executed, showNotification, isForced);
		}

		// Token: 0x06004ABD RID: 19133 RVA: 0x0017A3EC File Offset: 0x001785EC
		public static void ApplyByExecutionAfterMapEvent(Hero victim, Hero executer, bool showNotification = true, bool isForced = false)
		{
			KillCharacterAction.ApplyInternal(victim, executer, KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent, showNotification, isForced);
		}

		// Token: 0x06004ABE RID: 19134 RVA: 0x0017A3F8 File Offset: 0x001785F8
		public static void ApplyByRemove(Hero victim, bool showNotification = false, bool isForced = true)
		{
			KillCharacterAction.ApplyInternal(victim, null, KillCharacterAction.KillCharacterActionDetail.Lost, showNotification, isForced);
		}

		// Token: 0x06004ABF RID: 19135 RVA: 0x0017A404 File Offset: 0x00178604
		public static void ApplyByDeathMark(Hero victim, bool showNotification = false)
		{
			KillCharacterAction.ApplyInternal(victim, victim.DeathMarkKillerHero, victim.DeathMark, showNotification, false);
		}

		// Token: 0x06004AC0 RID: 19136 RVA: 0x0017A41A File Offset: 0x0017861A
		public static void ApplyByDeathMarkForced(Hero victim, bool showNotification = false)
		{
			KillCharacterAction.ApplyInternal(victim, victim.DeathMarkKillerHero, victim.DeathMark, showNotification, true);
		}

		// Token: 0x06004AC1 RID: 19137 RVA: 0x0017A430 File Offset: 0x00178630
		public static void ApplyByPlayerIllness()
		{
			KillCharacterAction.ApplyInternal(Hero.MainHero, null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge, true, true);
		}

		// Token: 0x06004AC2 RID: 19138 RVA: 0x0017A440 File Offset: 0x00178640
		private static void MakeDead(Hero victim, bool disbandVictimParty = true)
		{
			victim.ChangeState(Hero.CharacterStates.Dead);
			victim.SetDeathDay(CampaignTime.Now);
			if (victim.PartyBelongedToAsPrisoner != null)
			{
				EndCaptivityAction.ApplyByDeath(victim);
			}
			if (victim.PartyBelongedTo != null)
			{
				MobileParty partyBelongedTo = victim.PartyBelongedTo;
				if (partyBelongedTo.LeaderHero == victim)
				{
					bool flag = false;
					if (!partyBelongedTo.IsMainParty)
					{
						foreach (TroopRosterElement troopRosterElement in partyBelongedTo.MemberRoster.GetTroopRoster())
						{
							if (troopRosterElement.Character.IsHero && troopRosterElement.Character != victim.CharacterObject)
							{
								partyBelongedTo.ChangePartyLeader(troopRosterElement.Character.HeroObject);
								flag = true;
								break;
							}
						}
					}
					if (!flag)
					{
						if (!partyBelongedTo.IsMainParty)
						{
							partyBelongedTo.RemovePartyLeader();
						}
						if (partyBelongedTo.IsActive && disbandVictimParty)
						{
							Hero owner = partyBelongedTo.Party.Owner;
							if (((owner != null) ? owner.CompanionOf : null) == Clan.PlayerClan)
							{
								partyBelongedTo.Party.SetCustomOwner(Hero.MainHero);
							}
							partyBelongedTo.MemberRoster.RemoveTroop(victim.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
							DisbandPartyAction.StartDisband(partyBelongedTo);
						}
					}
				}
				if (victim.PartyBelongedTo != null)
				{
					partyBelongedTo.MemberRoster.RemoveTroop(victim.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
				}
				if (partyBelongedTo.IsActive && partyBelongedTo.MemberRoster.TotalManCount == 0)
				{
					DestroyPartyAction.Apply(null, partyBelongedTo);
					return;
				}
			}
			else if (victim.IsHumanPlayerCharacter && !MobileParty.MainParty.IsActive)
			{
				DestroyPartyAction.Apply(null, MobileParty.MainParty);
			}
		}

		// Token: 0x06004AC3 RID: 19139 RVA: 0x0017A5D8 File Offset: 0x001787D8
		private static Clan SelectHeirClanForKingdom(Kingdom kingdom, bool exceptRulingClan)
		{
			Clan rulingClan = kingdom.RulingClan;
			Clan result = null;
			float num = 0f;
			IEnumerable<Clan> clans = kingdom.Clans;
			Func<Clan, bool> <>9__0;
			Func<Clan, bool> predicate;
			if ((predicate = <>9__0) == null)
			{
				predicate = (<>9__0 = ((Clan t) => t.Heroes.Any((Hero h) => h.IsAlive) && !t.IsMinorFaction && t != rulingClan));
			}
			foreach (Clan clan in clans.Where(predicate))
			{
				float clanStrength = Campaign.Current.Models.DiplomacyModel.GetClanStrength(clan);
				if (num <= clanStrength)
				{
					num = clanStrength;
					result = clan;
				}
			}
			return result;
		}

		// Token: 0x06004AC4 RID: 19140 RVA: 0x0017A684 File Offset: 0x00178884
		private static TextObject CreateObituary(Hero hero, KillCharacterAction.KillCharacterActionDetail detail)
		{
			TextObject textObject;
			if (hero.IsLord)
			{
				if (hero.Clan != null && hero.Clan.IsMinorFaction)
				{
					textObject = new TextObject("{=L7qd6qfv}{CHARACTER.FIRSTNAME} was a member of the {CHARACTER.FACTION}. {FURTHER_DETAILS}.", null);
				}
				else if (hero.Clan != null && hero.Clan.Leader == hero)
				{
					textObject = new TextObject("{=mfYzCeGR}{CHARACTER.NAME} was {TITLE} of the {CHARACTER_FACTION_SHORT}. {FURTHER_DETAILS}.", null);
					textObject.SetTextVariable("CHARACTER_FACTION_SHORT", hero.MapFaction.InformalName);
					textObject.SetTextVariable("TITLE", HeroHelper.GetTitleInIndefiniteCase(hero));
				}
				else
				{
					textObject = new TextObject("{=uWdj1X2c}{CHARACTER.NAME} was a member of the {CHARACTER_FACTION_SHORT}. {FURTHER_DETAILS}.", null);
					textObject.SetTextVariable("CHARACTER_FACTION_SHORT", hero.MapFaction.InformalName);
					textObject.SetTextVariable("TITLE", HeroHelper.GetTitleInIndefiniteCase(hero));
				}
			}
			else if (hero.HomeSettlement != null)
			{
				textObject = new TextObject("{=YNXK352h}{CHARACTER.NAME} was a prominent {.%}{PROFESSION}{.%} from {HOMETOWN}. {FURTHER_DETAILS}.", null);
				textObject.SetTextVariable("PROFESSION", HeroHelper.GetCharacterTypeName(hero));
				textObject.SetTextVariable("HOMETOWN", hero.HomeSettlement.Name);
			}
			else
			{
				textObject = new TextObject("{=!}{FURTHER_DETAILS}.", null);
			}
			StringHelpers.SetCharacterProperties("CHARACTER", hero.CharacterObject, textObject, true);
			TextObject textObject2 = TextObject.GetEmpty();
			if (detail == KillCharacterAction.KillCharacterActionDetail.DiedInBattle)
			{
				textObject2 = new TextObject("{=6pCABUme}{?CHARACTER.GENDER}She{?}He{\\?} died in battle in {YEAR} at the age of {CHARACTER.AGE}. {?CHARACTER.GENDER}She{?}He{\\?} was reputed to be {REPUTATION}", null);
			}
			else if (detail == KillCharacterAction.KillCharacterActionDetail.DiedInLabor)
			{
				textObject2 = new TextObject("{=7Vw6iYNI}{?CHARACTER.GENDER}She{?}He{\\?} died in childbirth in {YEAR} at the age of {CHARACTER.AGE}. {?CHARACTER.GENDER}She{?}He{\\?} was reputed to be {REPUTATION}", null);
			}
			else if (detail == KillCharacterAction.KillCharacterActionDetail.Executed || detail == KillCharacterAction.KillCharacterActionDetail.ExecutionAfterMapEvent)
			{
				textObject2 = new TextObject("{=9Tq3IAiz}{?CHARACTER.GENDER}She{?}He{\\?} was executed in {YEAR} at the age of {CHARACTER.AGE}. {?CHARACTER.GENDER}She{?}He{\\?} was reputed to be {REPUTATION}", null);
			}
			else if (detail == KillCharacterAction.KillCharacterActionDetail.Lost)
			{
				textObject2 = new TextObject("{=SausWqM5}{?CHARACTER.GENDER}She{?}He{\\?} disappeared in {YEAR} at the age of {CHARACTER.AGE}. {?CHARACTER.GENDER}She{?}He{\\?} was reputed to be {REPUTATION}", null);
			}
			else if (detail == KillCharacterAction.KillCharacterActionDetail.Murdered)
			{
				textObject2 = new TextObject("{=TUDAvcTR}{?CHARACTER.GENDER}She{?}He{\\?} was assassinated in {YEAR} at the age of {CHARACTER.AGE}. {?CHARACTER.GENDER}She{?}He{\\?} was reputed to be {REPUTATION}", null);
			}
			else if (detail == KillCharacterAction.KillCharacterActionDetail.WoundedInBattle)
			{
				textObject2 = new TextObject("{=LsBCQtVX}{?CHARACTER.GENDER}She{?}He{\\?} died of war-wounds in {YEAR} at the age of {CHARACTER.AGE}. {?CHARACTER.GENDER}She{?}He{\\?} was reputed to be {REPUTATION}", null);
			}
			else
			{
				textObject2 = new TextObject("{=HU5n5KTW}{?CHARACTER.GENDER}She{?}He{\\?} died of natural causes in {YEAR} at the age of {CHARACTER.AGE}. {?CHARACTER.GENDER}She{?}He{\\?} was reputed to be {REPUTATION}", null);
			}
			StringHelpers.SetCharacterProperties("CHARACTER", hero.CharacterObject, textObject2, true);
			textObject2.SetTextVariable("REPUTATION", CharacterHelper.GetReputationDescription(hero.CharacterObject));
			textObject2.SetTextVariable("YEAR", CampaignTime.Now.GetYear.ToString());
			textObject.SetTextVariable("FURTHER_DETAILS", textObject2);
			return textObject;
		}

		// Token: 0x02000899 RID: 2201
		public enum KillCharacterActionDetail
		{
			// Token: 0x040024C8 RID: 9416
			None,
			// Token: 0x040024C9 RID: 9417
			Murdered,
			// Token: 0x040024CA RID: 9418
			DiedInLabor,
			// Token: 0x040024CB RID: 9419
			DiedOfOldAge,
			// Token: 0x040024CC RID: 9420
			DiedInBattle,
			// Token: 0x040024CD RID: 9421
			WoundedInBattle,
			// Token: 0x040024CE RID: 9422
			Executed,
			// Token: 0x040024CF RID: 9423
			ExecutionAfterMapEvent,
			// Token: 0x040024D0 RID: 9424
			Lost
		}
	}
}
