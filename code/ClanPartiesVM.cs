using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories
{
	// Token: 0x0200013A RID: 314
	public class ClanPartiesVM : ViewModel
	{
		// Token: 0x17000A06 RID: 2566
		// (get) Token: 0x06001D71 RID: 7537 RVA: 0x0006BEFF File Offset: 0x0006A0FF
		// (set) Token: 0x06001D72 RID: 7538 RVA: 0x0006BF07 File Offset: 0x0006A107
		public int TotalExpense { get; private set; }

		// Token: 0x17000A07 RID: 2567
		// (get) Token: 0x06001D73 RID: 7539 RVA: 0x0006BF10 File Offset: 0x0006A110
		// (set) Token: 0x06001D74 RID: 7540 RVA: 0x0006BF18 File Offset: 0x0006A118
		public int TotalIncome { get; private set; }

		// Token: 0x06001D75 RID: 7541 RVA: 0x0006BF24 File Offset: 0x0006A124
		public ClanPartiesVM(Action onExpenseChange, Action<Hero> openPartyAsManage, Action onRefresh, Action<ClanCardSelectionInfo> openCardSelectionPopup)
		{
			this._onExpenseChange = onExpenseChange;
			this._onRefresh = onRefresh;
			this._disbandBehavior = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
			this._teleportationBehavior = Campaign.Current.GetCampaignBehavior<ITeleportationCampaignBehavior>();
			this._openPartyAsManage = openPartyAsManage;
			this._openCardSelectionPopup = openCardSelectionPopup;
			this._faction = Hero.MainHero.Clan;
			this.Parties = new MBBindingList<ClanPartyItemVM>();
			this.Garrisons = new MBBindingList<ClanPartyItemVM>();
			this.Caravans = new MBBindingList<ClanPartyItemVM>();
			MBBindingList<MBBindingList<ClanPartyItemVM>> listsToControl = new MBBindingList<MBBindingList<ClanPartyItemVM>>
			{
				this.Parties,
				this.Garrisons,
				this.Caravans
			};
			this.SortController = new ClanPartiesSortControllerVM(listsToControl);
			this.CreateNewPartyActionHint = new HintViewModel();
			this.RefreshPartiesList();
			this.RefreshValues();
		}

		// Token: 0x06001D76 RID: 7542 RVA: 0x0006C02C File Offset: 0x0006A22C
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.SizeText = GameTexts.FindText("str_clan_party_size", null).ToString();
			this.MoraleText = GameTexts.FindText("str_morale", null).ToString();
			this.LocationText = GameTexts.FindText("str_tooltip_label_location", null).ToString();
			this.NameText = GameTexts.FindText("str_sort_by_name_label", null).ToString();
			this.CreateNewPartyText = GameTexts.FindText("str_clan_create_new_party", null).ToString();
			this.GarrisonsText = GameTexts.FindText("str_clan_garrisons", null).ToString();
			this.CaravansText = GameTexts.FindText("str_clan_caravans", null).ToString();
			this.RefreshPartiesList();
			this.Parties.ApplyActionOnAllItems(delegate(ClanPartyItemVM x)
			{
				x.RefreshValues();
			});
			this.Garrisons.ApplyActionOnAllItems(delegate(ClanPartyItemVM x)
			{
				x.RefreshValues();
			});
			this.Caravans.ApplyActionOnAllItems(delegate(ClanPartyItemVM x)
			{
				x.RefreshValues();
			});
			this.SortController.RefreshValues();
		}

		// Token: 0x06001D77 RID: 7543 RVA: 0x0006C168 File Offset: 0x0006A368
		public void RefreshTotalExpense()
		{
			IEnumerable<ClanPartyItemVM> enumerable = from p in this.Parties.Union(this.Garrisons).Union(this.Caravans)
			where p.ShouldPartyHaveExpense
			select p;
			int totalExpense;
			if (enumerable == null)
			{
				totalExpense = 0;
			}
			else
			{
				totalExpense = enumerable.Sum((ClanPartyItemVM p) => p.Expense);
			}
			this.TotalExpense = totalExpense;
			this.TotalIncome = this.Caravans.Sum((ClanPartyItemVM p) => p.Income);
		}

		// Token: 0x06001D78 RID: 7544 RVA: 0x0006C218 File Offset: 0x0006A418
		public void RefreshPartiesList()
		{
			this.Parties.Clear();
			this.Garrisons.Clear();
			this.Caravans.Clear();
			this.SortController.ResetAllStates();
			foreach (WarPartyComponent warPartyComponent in this._faction.WarPartyComponents)
			{
				if (warPartyComponent.MobileParty == MobileParty.MainParty)
				{
					this.Parties.Insert(0, new ClanPartyItemVM(warPartyComponent.Party, new Action<ClanPartyItemVM>(this.OnPartySelection), new Action(this.OnAnyExpenseChange), new Action(this.OnShowChangeLeaderPopup), ClanPartyItemVM.ClanPartyType.Main, this._disbandBehavior, this._teleportationBehavior));
				}
				else
				{
					this.Parties.Add(new ClanPartyItemVM(warPartyComponent.Party, new Action<ClanPartyItemVM>(this.OnPartySelection), new Action(this.OnAnyExpenseChange), new Action(this.OnShowChangeLeaderPopup), ClanPartyItemVM.ClanPartyType.Member, this._disbandBehavior, this._teleportationBehavior));
				}
			}
			using (IEnumerator<CaravanPartyComponent> enumerator2 = this._faction.Heroes.SelectMany((Hero h) => h.OwnedCaravans).GetEnumerator())
			{
				while (enumerator2.MoveNext())
				{
					CaravanPartyComponent party = enumerator2.Current;
					if (!this.Caravans.Any((ClanPartyItemVM c) => c.Party.MobileParty == party.MobileParty))
					{
						this.Caravans.Add(new ClanPartyItemVM(party.Party, new Action<ClanPartyItemVM>(this.OnPartySelection), new Action(this.OnAnyExpenseChange), new Action(this.OnShowChangeLeaderPopup), ClanPartyItemVM.ClanPartyType.Caravan, this._disbandBehavior, this._teleportationBehavior));
					}
				}
			}
			using (IEnumerator<MobileParty> enumerator3 = (from a in this._faction.Settlements
			where a.Town != null
			select a into s
			select s.Town.GarrisonParty).GetEnumerator())
			{
				while (enumerator3.MoveNext())
				{
					MobileParty garrison = enumerator3.Current;
					if (garrison != null && !this.Garrisons.Any((ClanPartyItemVM c) => c.Party == garrison.Party))
					{
						this.Garrisons.Add(new ClanPartyItemVM(garrison.Party, new Action<ClanPartyItemVM>(this.OnPartySelection), new Action(this.OnAnyExpenseChange), new Action(this.OnShowChangeLeaderPopup), ClanPartyItemVM.ClanPartyType.Garrison, this._disbandBehavior, this._teleportationBehavior));
					}
				}
			}
			int count = this._faction.WarPartyComponents.Count;
			(from h in this._faction.Heroes
			where !h.IsDisabled
			select h).Union(this._faction.Companions).Any((Hero h) => h.IsActive && h.PartyBelongedToAsPrisoner == null && !h.IsChild && h.CanLeadParty() && (h.PartyBelongedTo == null || h.PartyBelongedTo.LeaderHero != h));
			TextObject hintText;
			this.CanCreateNewParty = this.GetCanCreateNewParty(out hintText);
			this.CreateNewPartyActionHint.HintText = hintText;
			GameTexts.SetVariable("CURRENT", count);
			GameTexts.SetVariable("LIMIT", this._faction.CommanderLimit);
			this.PartiesText = GameTexts.FindText("str_clan_parties", null).ToString();
			GameTexts.SetVariable("CURRENT", this.Caravans.Count);
			this.CaravansText = GameTexts.FindText("str_clan_caravans", null).ToString();
			GameTexts.SetVariable("CURRENT", this.Garrisons.Count);
			this.GarrisonsText = GameTexts.FindText("str_clan_garrisons", null).ToString();
			this.OnPartySelection(this.GetDefaultMember());
		}

		// Token: 0x06001D79 RID: 7545 RVA: 0x0006C644 File Offset: 0x0006A844
		private bool GetCanCreateNewParty(out TextObject disabledReason)
		{
			IEnumerable<Hero> source = from h in (from h in this._faction.Heroes
			where !h.IsDisabled
			select h).Union(this._faction.Companions)
			where h.IsActive && h.PartyBelongedToAsPrisoner == null && !h.IsChild && h.CanLeadParty() && (h.PartyBelongedTo == null || h.PartyBelongedTo.LeaderHero != h)
			select h;
			bool flag = !source.IsEmpty<Hero>();
			int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
			bool flag2 = source.Any((Hero h) => Hero.MainHero.Gold > partyGoldLowerThreshold - h.Gold);
			TextObject textObject;
			if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			if (MobileParty.MainParty.IsCurrentlyAtSea || MobileParty.MainParty.IsInRaftState)
			{
				disabledReason = GameTexts.FindText("str_cannot_perform_action_while_sailing", null);
				return false;
			}
			if (this._faction.CommanderLimit - this._faction.WarPartyComponents.Count <= 0)
			{
				disabledReason = GameTexts.FindText("str_clan_doesnt_have_empty_party_slots", null);
				return false;
			}
			if (!flag)
			{
				disabledReason = GameTexts.FindText("str_clan_doesnt_have_available_heroes", null);
				return false;
			}
			if (!flag2)
			{
				disabledReason = new TextObject("{=VSUqbvbE}You don't have enough gold to create a new party.", null);
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x06001D7A RID: 7546 RVA: 0x0006C77F File Offset: 0x0006A97F
		private void OnAnyExpenseChange()
		{
			this.RefreshTotalExpense();
			this._onExpenseChange();
		}

		// Token: 0x06001D7B RID: 7547 RVA: 0x0006C792 File Offset: 0x0006A992
		private ClanPartyItemVM GetDefaultMember()
		{
			return this.Parties.FirstOrDefault<ClanPartyItemVM>();
		}

		// Token: 0x06001D7C RID: 7548 RVA: 0x0006C79F File Offset: 0x0006A99F
		public void ExecuteCreateNewParty()
		{
			if (this.CanCreateNewParty)
			{
				if (this.GetNewPartyLeaderCandidates().Any<ClanCardSelectionItemInfo>())
				{
					this.OnShowNewPartyPopup();
					return;
				}
				MBInformationManager.AddQuickInformation(new TextObject("{=qZvNIVGV}There is no one available in your clan who can lead a party right now.", null), 0, null, null, "");
			}
		}

		// Token: 0x06001D7D RID: 7549 RVA: 0x0006C7D8 File Offset: 0x0006A9D8
		public void SelectParty(PartyBase party)
		{
			foreach (ClanPartyItemVM clanPartyItemVM in this.Parties)
			{
				if (clanPartyItemVM.Party == party)
				{
					this.OnPartySelection(clanPartyItemVM);
					break;
				}
			}
			foreach (ClanPartyItemVM clanPartyItemVM2 in this.Caravans)
			{
				if (clanPartyItemVM2.Party == party)
				{
					this.OnPartySelection(clanPartyItemVM2);
					break;
				}
			}
		}

		// Token: 0x06001D7E RID: 7550 RVA: 0x0006C878 File Offset: 0x0006AA78
		private void OnPartySelection(ClanPartyItemVM party)
		{
			if (this.CurrentSelectedParty != null)
			{
				this.CurrentSelectedParty.IsSelected = false;
			}
			this.CurrentSelectedParty = party;
			if (party != null)
			{
				party.IsSelected = true;
			}
		}

		// Token: 0x06001D7F RID: 7551 RVA: 0x0006C8A0 File Offset: 0x0006AAA0
		public override void OnFinalize()
		{
			base.OnFinalize();
			this.Parties.ApplyActionOnAllItems(delegate(ClanPartyItemVM p)
			{
				p.OnFinalize();
			});
			this.Garrisons.ApplyActionOnAllItems(delegate(ClanPartyItemVM p)
			{
				p.OnFinalize();
			});
			this.Caravans.ApplyActionOnAllItems(delegate(ClanPartyItemVM p)
			{
				p.OnFinalize();
			});
		}

		// Token: 0x06001D80 RID: 7552 RVA: 0x0006C934 File Offset: 0x0006AB34
		public void OnShowNewPartyPopup()
		{
			ClanCardSelectionInfo obj = new ClanCardSelectionInfo(new TextObject("{=0Q4Xo2BQ}Select the Leader of the New Party", null), this.GetNewPartyLeaderCandidates(), new Action<List<object>, Action>(this.OnNewPartyCreationOver), false, 1, 0);
			Action<ClanCardSelectionInfo> openCardSelectionPopup = this._openCardSelectionPopup;
			if (openCardSelectionPopup == null)
			{
				return;
			}
			openCardSelectionPopup(obj);
		}

		// Token: 0x06001D81 RID: 7553 RVA: 0x0006C979 File Offset: 0x0006AB79
		private IEnumerable<ClanCardSelectionItemInfo> GetNewPartyLeaderCandidates()
		{
			int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
			foreach (Hero hero in (from h in this._faction.Heroes
			where !h.IsDisabled
			select h).Union(this._faction.Companions))
			{
				if ((hero.IsActive || hero.IsReleased || hero.IsFugitive) && !hero.IsChild && hero != Hero.MainHero && hero.CanBeGovernorOrHavePartyRole())
				{
					bool flag = false;
					TextObject textObject = TextObject.GetEmpty();
					if (hero.PartyBelongedToAsPrisoner != null)
					{
						textObject = new TextObject("{=vOojEcIf}You cannot assign a prisoner member as a new party leader", null);
					}
					else if (hero.IsReleased)
					{
						textObject = new TextObject("{=OhNYkblK}This hero has just escaped from captors and will be available after some time.", null);
					}
					else if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero == hero)
					{
						textObject = new TextObject("{=aFYwbosi}This hero is already leading a party.", null);
					}
					else if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero != Hero.MainHero)
					{
						textObject = new TextObject("{=FjJi1DJb}This hero is already a part of an another party.", null);
					}
					else if (hero.GovernorOf != null)
					{
						textObject = new TextObject("{=Hz8XO8wk}Governors cannot lead a mobile party and be a governor at the same time.", null);
					}
					else if (hero.HeroState == Hero.CharacterStates.Disabled)
					{
						textObject = new TextObject("{=slzfQzl3}This hero is lost", null);
					}
					else if (hero.HeroState == Hero.CharacterStates.Fugitive)
					{
						textObject = new TextObject("{=dD3kRDHi}This hero is a fugitive and running from their captors. They will be available after some time.", null);
					}
					else if (partyGoldLowerThreshold - hero.Gold > Hero.MainHero.Gold)
					{
						textObject = new TextObject("{=xpCdwmlX}You don't have enough gold to make {HERO.NAME} a party leader.", null);
						textObject.SetCharacterProperties("HERO", hero.CharacterObject, false);
					}
					else if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.IsCurrentlyAtSea)
					{
						textObject = new TextObject("{=1ELK1UbN}{HERO.NAME} is currently sailing.", null);
						textObject.SetCharacterProperties("HERO", hero.CharacterObject, false);
					}
					else
					{
						flag = true;
					}
					yield return new ClanCardSelectionItemInfo(hero, hero.Name, new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(hero.CharacterObject, false)), CardSelectionItemSpriteType.None, null, null, this.GetNewPartyLeaderCandidateProperties(hero), !flag, textObject, null);
				}
			}
			IEnumerator<Hero> enumerator = null;
			yield break;
			yield break;
		}

		// Token: 0x06001D82 RID: 7554 RVA: 0x0006C989 File Offset: 0x0006AB89
		private IEnumerable<ClanCardSelectionItemPropertyInfo> GetNewPartyLeaderCandidateProperties(Hero hero)
		{
			yield return new ClanCardSelectionItemPropertyInfo(TextObject.GetEmpty());
			TextObject textObject = new TextObject("{=hwrQqWir}No Skills", null);
			int num = 0;
			foreach (SkillObject skillObject in this._leaderAssignmentRelevantSkills)
			{
				TextObject textObject2 = new TextObject("{=!}{SKILL_VALUE}", null);
				textObject2.SetTextVariable("SKILL_VALUE", hero.GetSkillValue(skillObject));
				TextObject textObject3 = ClanCardSelectionItemPropertyInfo.CreateLabeledValueText(skillObject.Name, textObject2);
				if (num == 0)
				{
					textObject = textObject3;
				}
				else
				{
					TextObject textObject4 = GameTexts.FindText("str_string_newline_newline_string", null);
					textObject4.SetTextVariable("STR1", textObject);
					textObject4.SetTextVariable("STR2", textObject3);
					textObject = textObject4;
				}
				num++;
			}
			yield return new ClanCardSelectionItemPropertyInfo(GameTexts.FindText("str_skills", null), textObject);
			yield break;
		}

		// Token: 0x06001D83 RID: 7555 RVA: 0x0006C9A0 File Offset: 0x0006ABA0
		private void OnNewPartyCreationOver(List<object> selectedItems, Action closePopup)
		{
			if (selectedItems.Count == 1)
			{
				Hero newLeader = selectedItems.FirstOrDefault<object>() as Hero;
				int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
				if (newLeader.Gold < partyGoldLowerThreshold)
				{
					string titleText = new TextObject("{=DAYoD0aW}Create Party", null).ToString();
					string text = new TextObject("{=fRz2DJf4}Creating the party will cost you {PARTY_COST}{GOLD_ICON}. Are you sure?", null).SetTextVariable("PARTY_COST", partyGoldLowerThreshold - newLeader.Gold).SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">").ToString();
					InformationManager.ShowInquiry(new InquiryData(titleText, text, true, true, new TextObject("{=aeouhelq}Yes", null).ToString(), new TextObject("{=3CpNUnVl}Cancel", null).ToString(), delegate()
					{
						Action closePopup4 = closePopup;
						if (closePopup4 != null)
						{
							closePopup4();
						}
						this.CreateNewClanParty(newLeader, partyGoldLowerThreshold);
					}, null, "", 0f, null, null, null), false, false);
					return;
				}
				Action closePopup2 = closePopup;
				if (closePopup2 != null)
				{
					closePopup2();
				}
				this.CreateNewClanParty(newLeader, partyGoldLowerThreshold);
				return;
			}
			else
			{
				Action closePopup3 = closePopup;
				if (closePopup3 == null)
				{
					return;
				}
				closePopup3();
				return;
			}
		}

		// Token: 0x06001D84 RID: 7556 RVA: 0x0006CAEC File Offset: 0x0006ACEC
		private void CreateNewClanParty(Hero newLeader, int partyGoldLowerThreshold)
		{
			if (newLeader.PartyBelongedTo == MobileParty.MainParty)
			{
				this._openPartyAsManage(newLeader);
				return;
			}
			MobileParty mobileParty = MobilePartyHelper.CreateNewClanMobileParty(newLeader, this._faction);
			if (newLeader.Gold < partyGoldLowerThreshold)
			{
				GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, newLeader, partyGoldLowerThreshold - newLeader.Gold, false);
			}
			mobileParty.SetMoveModeHold();
			this._onRefresh();
		}

		// Token: 0x06001D85 RID: 7557 RVA: 0x0006CB50 File Offset: 0x0006AD50
		public void OnShowChangeLeaderPopup()
		{
			ClanPartyItemVM currentSelectedParty = this.CurrentSelectedParty;
			bool flag;
			if (currentSelectedParty == null)
			{
				flag = (null != null);
			}
			else
			{
				PartyBase party = currentSelectedParty.Party;
				flag = (((party != null) ? party.MobileParty : null) != null);
			}
			if (flag)
			{
				ClanCardSelectionInfo obj = new ClanCardSelectionInfo(GameTexts.FindText("str_change_party_leader", null), this.GetChangeLeaderCandidates(), new Action<List<object>, Action>(this.OnChangeLeaderOver), false, 1, 0);
				Action<ClanCardSelectionInfo> openCardSelectionPopup = this._openCardSelectionPopup;
				if (openCardSelectionPopup == null)
				{
					return;
				}
				openCardSelectionPopup(obj);
			}
		}

		// Token: 0x06001D86 RID: 7558 RVA: 0x0006CBB5 File Offset: 0x0006ADB5
		private IEnumerable<ClanCardSelectionItemInfo> GetChangeLeaderCandidates()
		{
			TextObject disabledReason;
			bool canDisbandParty = this.GetCanDisbandParty(out disabledReason);
			yield return new ClanCardSelectionItemInfo(GameTexts.FindText("str_disband_party", null), !canDisbandParty, disabledReason, null);
			foreach (Hero hero in (from h in this._faction.Heroes
			where !h.IsDisabled
			select h).Union(this._faction.Companions))
			{
				if ((hero.IsActive || hero.IsReleased || hero.IsFugitive || hero.IsTraveling) && !hero.IsChild && hero != Hero.MainHero && hero.CanLeadParty())
				{
					Hero hero2 = hero;
					ClanPartyMemberItemVM leaderMember = this.CurrentSelectedParty.LeaderMember;
					if (hero2 != ((leaderMember != null) ? leaderMember.HeroObject : null))
					{
						TextObject disabledReason2;
						bool flag = FactionHelper.IsMainClanMemberAvailableForPartyLeaderChange(hero, true, this.CurrentSelectedParty.Party.MobileParty, out disabledReason2);
						CharacterImageIdentifier image = new CharacterImageIdentifier(CampaignUIHelper.GetCharacterCode(hero.CharacterObject, false));
						yield return new ClanCardSelectionItemInfo(hero, hero.Name, image, CardSelectionItemSpriteType.None, null, null, this.GetChangeLeaderCandidateProperties(hero), !flag, disabledReason2, null);
					}
				}
			}
			IEnumerator<Hero> enumerator = null;
			yield break;
			yield break;
		}

		// Token: 0x06001D87 RID: 7559 RVA: 0x0006CBC5 File Offset: 0x0006ADC5
		private IEnumerable<ClanCardSelectionItemPropertyInfo> GetChangeLeaderCandidateProperties(Hero hero)
		{
			TextObject teleportationDelayText = CampaignUIHelper.GetTeleportationDelayText(hero, this.CurrentSelectedParty.Party);
			yield return new ClanCardSelectionItemPropertyInfo(teleportationDelayText);
			TextObject textObject = new TextObject("{=hwrQqWir}No Skills", null);
			int num = 0;
			foreach (SkillObject skillObject in this._leaderAssignmentRelevantSkills)
			{
				TextObject textObject2 = new TextObject("{=!}{SKILL_VALUE}", null);
				textObject2.SetTextVariable("SKILL_VALUE", hero.GetSkillValue(skillObject));
				TextObject textObject3 = ClanCardSelectionItemPropertyInfo.CreateLabeledValueText(skillObject.Name, textObject2);
				if (num == 0)
				{
					textObject = textObject3;
				}
				else
				{
					TextObject textObject4 = GameTexts.FindText("str_string_newline_newline_string", null);
					textObject4.SetTextVariable("STR1", textObject);
					textObject4.SetTextVariable("STR2", textObject3);
					textObject = textObject4;
				}
				num++;
			}
			yield return new ClanCardSelectionItemPropertyInfo(GameTexts.FindText("str_skills", null), textObject);
			yield break;
		}

		// Token: 0x06001D88 RID: 7560 RVA: 0x0006CBDC File Offset: 0x0006ADDC
		private void OnChangeLeaderOver(List<object> selectedItems, Action closePopup)
		{
			if (selectedItems.Count == 1)
			{
				Hero newLeader = selectedItems.FirstOrDefault<object>() as Hero;
				bool isDisband = newLeader == null;
				int partyGoldLowerThreshold = Campaign.Current.Models.ClanFinanceModel.PartyGoldLowerThreshold;
				ClanPartyItemVM currentSelectedParty = this.CurrentSelectedParty;
				PartyBase partyBase = (currentSelectedParty != null) ? currentSelectedParty.Party : null;
				MobileParty mobileParty = (partyBase != null) ? partyBase.MobileParty : null;
				DelayedTeleportationModel delayedTeleportationModel = Campaign.Current.Models.DelayedTeleportationModel;
				int num = (!isDisband && mobileParty != null) ? ((int)Math.Ceiling((double)delayedTeleportationModel.GetTeleportationDelayAsHours(newLeader, mobileParty.Party).ResultNumber)) : 0;
				MBTextManager.SetTextVariable("TRAVEL_DURATION", CampaignUIHelper.GetHoursAndDaysTextFromHourValue(num).ToString(), false);
				Hero newLeader2 = newLeader;
				if (((newLeader2 != null) ? newLeader2.CharacterObject : null) != null)
				{
					StringHelpers.SetCharacterProperties("LEADER", newLeader.CharacterObject, null, false);
					MBTextManager.SetTextVariable("PARTY_COST", partyGoldLowerThreshold - newLeader.Gold);
					MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">", false);
					MBTextManager.SetTextVariable("DOES_LEADER_NEED_GOLD", (partyGoldLowerThreshold > newLeader.Gold) ? 1 : 0);
				}
				if (isDisband && partyBase != null && partyBase.Ships.Count > 0)
				{
					MBTextManager.SetTextVariable("DOES_DISBANDING_PARTY_HAVE_SHIP", true);
				}
				object obj = GameTexts.FindText(isDisband ? "str_disband_party" : "str_change_clan_party_leader", null);
				TextObject textObject = GameTexts.FindText(isDisband ? "str_disband_party_inquiry" : ((num == 0) ? "str_change_clan_party_leader_instantly_inquiry" : "str_change_clan_party_leader_inquiry"), null);
				InformationManager.ShowInquiry(new InquiryData(obj.ToString(), textObject.ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate()
				{
					Action closePopup3 = closePopup;
					if (closePopup3 != null)
					{
						closePopup3();
					}
					this.OnPartyLeaderChanged(newLeader);
					if (isDisband)
					{
						this.OnDisbandCurrentParty();
					}
					else if (newLeader.Gold < partyGoldLowerThreshold)
					{
						GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, newLeader, partyGoldLowerThreshold - newLeader.Gold, false);
					}
					Action onRefresh = this._onRefresh;
					if (onRefresh == null)
					{
						return;
					}
					onRefresh();
				}, null, "", 0f, null, null, null), false, false);
				return;
			}
			Action closePopup2 = closePopup;
			if (closePopup2 == null)
			{
				return;
			}
			closePopup2();
		}

		// Token: 0x06001D89 RID: 7561 RVA: 0x0006CE14 File Offset: 0x0006B014
		private void OnPartyLeaderChanged(Hero newLeader)
		{
			ClanPartyItemVM currentSelectedParty = this.CurrentSelectedParty;
			bool flag;
			if (currentSelectedParty == null)
			{
				flag = (null != null);
			}
			else
			{
				PartyBase party = currentSelectedParty.Party;
				flag = (((party != null) ? party.LeaderHero : null) != null);
			}
			if (flag)
			{
				if (newLeader == null)
				{
					Hero leaderHero = this.CurrentSelectedParty.Party.LeaderHero;
					this.CurrentSelectedParty.Party.MobileParty.RemovePartyLeader();
					MakeHeroFugitiveAction.Apply(leaderHero, false);
				}
				else
				{
					TeleportHeroAction.ApplyDelayedTeleportToParty(this.CurrentSelectedParty.Party.LeaderHero, MobileParty.MainParty);
				}
			}
			if (newLeader != null)
			{
				TeleportHeroAction.ApplyDelayedTeleportToPartyAsPartyLeader(newLeader, this.CurrentSelectedParty.Party.MobileParty);
			}
		}

		// Token: 0x06001D8A RID: 7562 RVA: 0x0006CEA4 File Offset: 0x0006B0A4
		private void OnDisbandCurrentParty()
		{
			DisbandPartyAction.StartDisband(this.CurrentSelectedParty.Party.MobileParty);
		}

		// Token: 0x06001D8B RID: 7563 RVA: 0x0006CEBC File Offset: 0x0006B0BC
		private bool GetCanDisbandParty(out TextObject cannotDisbandReason)
		{
			bool result = false;
			cannotDisbandReason = TextObject.GetEmpty();
			ClanPartyItemVM currentSelectedParty = this.CurrentSelectedParty;
			MobileParty mobileParty;
			if (currentSelectedParty == null)
			{
				mobileParty = null;
			}
			else
			{
				PartyBase party = currentSelectedParty.Party;
				mobileParty = ((party != null) ? party.MobileParty : null);
			}
			MobileParty mobileParty2 = mobileParty;
			if (mobileParty2 != null)
			{
				TextObject textObject;
				if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out textObject))
				{
					cannotDisbandReason = textObject;
				}
				else if (mobileParty2.IsMilitia)
				{
					cannotDisbandReason = GameTexts.FindText("str_cannot_disband_milita_party", null);
				}
				else if (mobileParty2.IsGarrison)
				{
					cannotDisbandReason = GameTexts.FindText("str_cannot_disband_garrison_party", null);
				}
				else if (mobileParty2.IsMainParty)
				{
					cannotDisbandReason = GameTexts.FindText("str_cannot_disband_main_party", null);
				}
				else if (this.CurrentSelectedParty.IsDisbanding)
				{
					cannotDisbandReason = GameTexts.FindText("str_cannot_disband_already_disbanding_party", null);
				}
				else if (mobileParty2.MapEvent != null || mobileParty2.SiegeEvent != null)
				{
					cannotDisbandReason = GameTexts.FindText("str_cannot_disband_during_battle", null);
				}
				else
				{
					result = true;
				}
			}
			return result;
		}

		// Token: 0x17000A08 RID: 2568
		// (get) Token: 0x06001D8C RID: 7564 RVA: 0x0006CF8B File Offset: 0x0006B18B
		// (set) Token: 0x06001D8D RID: 7565 RVA: 0x0006CF93 File Offset: 0x0006B193
		[DataSourceProperty]
		public HintViewModel CreateNewPartyActionHint
		{
			get
			{
				return this._createNewPartyActionHint;
			}
			set
			{
				if (value != this._createNewPartyActionHint)
				{
					this._createNewPartyActionHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "CreateNewPartyActionHint");
				}
			}
		}

		// Token: 0x17000A09 RID: 2569
		// (get) Token: 0x06001D8E RID: 7566 RVA: 0x0006CFB1 File Offset: 0x0006B1B1
		// (set) Token: 0x06001D8F RID: 7567 RVA: 0x0006CFB9 File Offset: 0x0006B1B9
		[DataSourceProperty]
		public bool IsAnyValidPartySelected
		{
			get
			{
				return this._isAnyValidPartySelected;
			}
			set
			{
				if (value != this._isAnyValidPartySelected)
				{
					this._isAnyValidPartySelected = value;
					base.OnPropertyChangedWithValue(value, "IsAnyValidPartySelected");
				}
			}
		}

		// Token: 0x17000A0A RID: 2570
		// (get) Token: 0x06001D90 RID: 7568 RVA: 0x0006CFD7 File Offset: 0x0006B1D7
		// (set) Token: 0x06001D91 RID: 7569 RVA: 0x0006CFDF File Offset: 0x0006B1DF
		[DataSourceProperty]
		public string NameText
		{
			get
			{
				return this._nameText;
			}
			set
			{
				if (value != this._nameText)
				{
					this._nameText = value;
					base.OnPropertyChangedWithValue<string>(value, "NameText");
				}
			}
		}

		// Token: 0x17000A0B RID: 2571
		// (get) Token: 0x06001D92 RID: 7570 RVA: 0x0006D002 File Offset: 0x0006B202
		// (set) Token: 0x06001D93 RID: 7571 RVA: 0x0006D00A File Offset: 0x0006B20A
		[DataSourceProperty]
		public string CaravansText
		{
			get
			{
				return this._caravansText;
			}
			set
			{
				if (value != this._caravansText)
				{
					this._caravansText = value;
					base.OnPropertyChangedWithValue<string>(value, "CaravansText");
				}
			}
		}

		// Token: 0x17000A0C RID: 2572
		// (get) Token: 0x06001D94 RID: 7572 RVA: 0x0006D02D File Offset: 0x0006B22D
		// (set) Token: 0x06001D95 RID: 7573 RVA: 0x0006D035 File Offset: 0x0006B235
		[DataSourceProperty]
		public string GarrisonsText
		{
			get
			{
				return this._garrisonsText;
			}
			set
			{
				if (value != this._garrisonsText)
				{
					this._garrisonsText = value;
					base.OnPropertyChangedWithValue<string>(value, "GarrisonsText");
				}
			}
		}

		// Token: 0x17000A0D RID: 2573
		// (get) Token: 0x06001D96 RID: 7574 RVA: 0x0006D058 File Offset: 0x0006B258
		// (set) Token: 0x06001D97 RID: 7575 RVA: 0x0006D060 File Offset: 0x0006B260
		[DataSourceProperty]
		public string PartiesText
		{
			get
			{
				return this._partiesText;
			}
			set
			{
				if (value != this._partiesText)
				{
					this._partiesText = value;
					base.OnPropertyChangedWithValue<string>(value, "PartiesText");
				}
			}
		}

		// Token: 0x17000A0E RID: 2574
		// (get) Token: 0x06001D98 RID: 7576 RVA: 0x0006D083 File Offset: 0x0006B283
		// (set) Token: 0x06001D99 RID: 7577 RVA: 0x0006D08B File Offset: 0x0006B28B
		[DataSourceProperty]
		public string MoraleText
		{
			get
			{
				return this._moraleText;
			}
			set
			{
				if (value != this._moraleText)
				{
					this._moraleText = value;
					base.OnPropertyChangedWithValue<string>(value, "MoraleText");
				}
			}
		}

		// Token: 0x17000A0F RID: 2575
		// (get) Token: 0x06001D9A RID: 7578 RVA: 0x0006D0AE File Offset: 0x0006B2AE
		// (set) Token: 0x06001D9B RID: 7579 RVA: 0x0006D0B6 File Offset: 0x0006B2B6
		[DataSourceProperty]
		public string LocationText
		{
			get
			{
				return this._locationText;
			}
			set
			{
				if (value != this._locationText)
				{
					this._locationText = value;
					base.OnPropertyChangedWithValue<string>(value, "LocationText");
				}
			}
		}

		// Token: 0x17000A10 RID: 2576
		// (get) Token: 0x06001D9C RID: 7580 RVA: 0x0006D0D9 File Offset: 0x0006B2D9
		// (set) Token: 0x06001D9D RID: 7581 RVA: 0x0006D0E1 File Offset: 0x0006B2E1
		[DataSourceProperty]
		public string CreateNewPartyText
		{
			get
			{
				return this._createNewPartyText;
			}
			set
			{
				if (value != this._createNewPartyText)
				{
					this._createNewPartyText = value;
					base.OnPropertyChangedWithValue<string>(value, "CreateNewPartyText");
				}
			}
		}

		// Token: 0x17000A11 RID: 2577
		// (get) Token: 0x06001D9E RID: 7582 RVA: 0x0006D104 File Offset: 0x0006B304
		// (set) Token: 0x06001D9F RID: 7583 RVA: 0x0006D10C File Offset: 0x0006B30C
		[DataSourceProperty]
		public string SizeText
		{
			get
			{
				return this._sizeText;
			}
			set
			{
				if (value != this._sizeText)
				{
					this._sizeText = value;
					base.OnPropertyChangedWithValue<string>(value, "SizeText");
				}
			}
		}

		// Token: 0x17000A12 RID: 2578
		// (get) Token: 0x06001DA0 RID: 7584 RVA: 0x0006D12F File Offset: 0x0006B32F
		// (set) Token: 0x06001DA1 RID: 7585 RVA: 0x0006D137 File Offset: 0x0006B337
		[DataSourceProperty]
		public bool IsSelected
		{
			get
			{
				return this._isSelected;
			}
			set
			{
				if (value != this._isSelected)
				{
					this._isSelected = value;
					base.OnPropertyChangedWithValue(value, "IsSelected");
				}
			}
		}

		// Token: 0x17000A13 RID: 2579
		// (get) Token: 0x06001DA2 RID: 7586 RVA: 0x0006D155 File Offset: 0x0006B355
		// (set) Token: 0x06001DA3 RID: 7587 RVA: 0x0006D15D File Offset: 0x0006B35D
		[DataSourceProperty]
		public bool CanCreateNewParty
		{
			get
			{
				return this._canCreateNewParty;
			}
			set
			{
				if (value != this._canCreateNewParty)
				{
					this._canCreateNewParty = value;
					base.OnPropertyChangedWithValue(value, "CanCreateNewParty");
				}
			}
		}

		// Token: 0x17000A14 RID: 2580
		// (get) Token: 0x06001DA4 RID: 7588 RVA: 0x0006D17B File Offset: 0x0006B37B
		// (set) Token: 0x06001DA5 RID: 7589 RVA: 0x0006D183 File Offset: 0x0006B383
		[DataSourceProperty]
		public MBBindingList<ClanPartyItemVM> Parties
		{
			get
			{
				return this._parties;
			}
			set
			{
				if (value != this._parties)
				{
					this._parties = value;
					base.OnPropertyChangedWithValue<MBBindingList<ClanPartyItemVM>>(value, "Parties");
				}
			}
		}

		// Token: 0x17000A15 RID: 2581
		// (get) Token: 0x06001DA6 RID: 7590 RVA: 0x0006D1A1 File Offset: 0x0006B3A1
		// (set) Token: 0x06001DA7 RID: 7591 RVA: 0x0006D1A9 File Offset: 0x0006B3A9
		[DataSourceProperty]
		public MBBindingList<ClanPartyItemVM> Caravans
		{
			get
			{
				return this._caravans;
			}
			set
			{
				if (value != this._caravans)
				{
					this._caravans = value;
					base.OnPropertyChangedWithValue<MBBindingList<ClanPartyItemVM>>(value, "Caravans");
				}
			}
		}

		// Token: 0x17000A16 RID: 2582
		// (get) Token: 0x06001DA8 RID: 7592 RVA: 0x0006D1C7 File Offset: 0x0006B3C7
		// (set) Token: 0x06001DA9 RID: 7593 RVA: 0x0006D1CF File Offset: 0x0006B3CF
		[DataSourceProperty]
		public MBBindingList<ClanPartyItemVM> Garrisons
		{
			get
			{
				return this._garrisons;
			}
			set
			{
				if (value != this._garrisons)
				{
					this._garrisons = value;
					base.OnPropertyChangedWithValue<MBBindingList<ClanPartyItemVM>>(value, "Garrisons");
				}
			}
		}

		// Token: 0x17000A17 RID: 2583
		// (get) Token: 0x06001DAA RID: 7594 RVA: 0x0006D1ED File Offset: 0x0006B3ED
		// (set) Token: 0x06001DAB RID: 7595 RVA: 0x0006D1F5 File Offset: 0x0006B3F5
		[DataSourceProperty]
		public ClanPartyItemVM CurrentSelectedParty
		{
			get
			{
				return this._currentSelectedParty;
			}
			set
			{
				if (value != this._currentSelectedParty)
				{
					this._currentSelectedParty = value;
					base.OnPropertyChangedWithValue<ClanPartyItemVM>(value, "CurrentSelectedParty");
					this.IsAnyValidPartySelected = (value != null);
				}
			}
		}

		// Token: 0x17000A18 RID: 2584
		// (get) Token: 0x06001DAC RID: 7596 RVA: 0x0006D21D File Offset: 0x0006B41D
		// (set) Token: 0x06001DAD RID: 7597 RVA: 0x0006D225 File Offset: 0x0006B425
		[DataSourceProperty]
		public ClanPartiesSortControllerVM SortController
		{
			get
			{
				return this._sortController;
			}
			set
			{
				if (value != this._sortController)
				{
					this._sortController = value;
					base.OnPropertyChangedWithValue<ClanPartiesSortControllerVM>(value, "SortController");
				}
			}
		}

		// Token: 0x04000DC8 RID: 3528
		private Action _onExpenseChange;

		// Token: 0x04000DC9 RID: 3529
		private Action<Hero> _openPartyAsManage;

		// Token: 0x04000DCA RID: 3530
		private Action<ClanCardSelectionInfo> _openCardSelectionPopup;

		// Token: 0x04000DCB RID: 3531
		private readonly IDisbandPartyCampaignBehavior _disbandBehavior;

		// Token: 0x04000DCC RID: 3532
		private readonly ITeleportationCampaignBehavior _teleportationBehavior;

		// Token: 0x04000DCD RID: 3533
		private readonly Action _onRefresh;

		// Token: 0x04000DCE RID: 3534
		private readonly Clan _faction;

		// Token: 0x04000DCF RID: 3535
		private readonly IEnumerable<SkillObject> _leaderAssignmentRelevantSkills = new List<SkillObject>
		{
			DefaultSkills.Engineering,
			DefaultSkills.Steward,
			DefaultSkills.Scouting,
			DefaultSkills.Medicine
		};

		// Token: 0x04000DD0 RID: 3536
		private MBBindingList<ClanPartyItemVM> _parties;

		// Token: 0x04000DD1 RID: 3537
		private MBBindingList<ClanPartyItemVM> _garrisons;

		// Token: 0x04000DD2 RID: 3538
		private MBBindingList<ClanPartyItemVM> _caravans;

		// Token: 0x04000DD3 RID: 3539
		private ClanPartyItemVM _currentSelectedParty;

		// Token: 0x04000DD4 RID: 3540
		private HintViewModel _createNewPartyActionHint;

		// Token: 0x04000DD5 RID: 3541
		private bool _canCreateNewParty;

		// Token: 0x04000DD6 RID: 3542
		private bool _isSelected;

		// Token: 0x04000DD7 RID: 3543
		private string _nameText;

		// Token: 0x04000DD8 RID: 3544
		private string _moraleText;

		// Token: 0x04000DD9 RID: 3545
		private string _locationText;

		// Token: 0x04000DDA RID: 3546
		private string _sizeText;

		// Token: 0x04000DDB RID: 3547
		private string _createNewPartyText;

		// Token: 0x04000DDC RID: 3548
		private string _partiesText;

		// Token: 0x04000DDD RID: 3549
		private string _caravansText;

		// Token: 0x04000DDE RID: 3550
		private string _garrisonsText;

		// Token: 0x04000DDF RID: 3551
		private bool _isAnyValidPartySelected;

		// Token: 0x04000DE0 RID: 3552
		private ClanPartiesSortControllerVM _sortController;
	}
}
