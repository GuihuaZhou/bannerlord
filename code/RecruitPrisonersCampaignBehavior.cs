using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x0200043A RID: 1082
	public class RecruitPrisonersCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x060044D1 RID: 17617 RVA: 0x00152538 File Offset: 0x00150738
		public override void RegisterEvents()
		{
			CampaignEvents.OnMainPartyPrisonerRecruitedEvent.AddNonSerializedListener(this, new Action<FlattenedTroopRoster>(this.OnMainPartyPrisonerRecruited));
			CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.DailyTickAIMobileParty));
			CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(this.HourlyTickMainParty));
		}

		// Token: 0x060044D2 RID: 17618 RVA: 0x0015258C File Offset: 0x0015078C
		private void HourlyTickMainParty()
		{
			MobileParty mainParty = MobileParty.MainParty;
			TroopRoster memberRoster = mainParty.MemberRoster;
			TroopRoster prisonRoster = mainParty.PrisonRoster;
			if (memberRoster.Count != 0 && memberRoster.TotalManCount > 0 && prisonRoster.Count != 0 && prisonRoster.TotalRegulars > 0 && mainParty.MapEvent == null)
			{
				int num = MBRandom.RandomInt(0, prisonRoster.Count);
				bool flag = false;
				for (int i = num; i < prisonRoster.Count + num; i++)
				{
					int index = i % prisonRoster.Count;
					CharacterObject characterAtIndex = prisonRoster.GetCharacterAtIndex(index);
					if (characterAtIndex.IsRegular)
					{
						CharacterObject characterObject = characterAtIndex;
						int elementNumber = mainParty.PrisonRoster.GetElementNumber(index);
						int num2 = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.CalculateRecruitableNumber(mainParty.Party, characterObject);
						if (!flag && num2 < elementNumber)
						{
							flag = this.GenerateConformityForTroop(mainParty, characterObject, 1);
						}
					}
					if (flag)
					{
						break;
					}
				}
			}
		}

		// Token: 0x060044D3 RID: 17619 RVA: 0x00152678 File Offset: 0x00150878
		private void DailyTickAIMobileParty(MobileParty mobileParty)
		{
			TroopRoster prisonRoster = mobileParty.PrisonRoster;
			if (!mobileParty.IsMainParty && mobileParty.IsLordParty && prisonRoster.Count != 0 && prisonRoster.TotalRegulars > 0 && mobileParty.MapEvent == null)
			{
				int num = MBRandom.RandomInt(0, prisonRoster.Count);
				bool flag = false;
				for (int i = num; i < prisonRoster.Count + num; i++)
				{
					int index = i % prisonRoster.Count;
					CharacterObject characterAtIndex = prisonRoster.GetCharacterAtIndex(index);
					if (characterAtIndex.IsRegular)
					{
						CharacterObject characterObject = characterAtIndex;
						int elementNumber = mobileParty.PrisonRoster.GetElementNumber(index);
						int num2 = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.CalculateRecruitableNumber(mobileParty.Party, characterObject);
						if (!flag && num2 < elementNumber)
						{
							flag = this.GenerateConformityForTroop(mobileParty, characterObject, CampaignTime.HoursInDay);
						}
						if (Campaign.Current.Models.PrisonerRecruitmentCalculationModel.ShouldPartyRecruitPrisoners(mobileParty.Party))
						{
							int conformityCost;
							if (this.IsPrisonerRecruitable(mobileParty, characterObject, out conformityCost))
							{
								int num3 = mobileParty.Party.PartySizeLimit - mobileParty.MemberRoster.TotalManCount;
								int num4 = MathF.Min((num3 > 0) ? ((num3 > num2) ? num2 : num3) : 0, prisonRoster.GetElementNumber(characterObject));
								int characterWage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(characterObject);
								num4 = MathF.Min(num4, mobileParty.GetAvailableWageBudget() / characterWage);
								if (num4 > 0)
								{
									this.RecruitPrisonersAi(mobileParty, characterObject, num4, conformityCost);
								}
							}
						}
						else if (flag)
						{
							break;
						}
					}
				}
			}
		}

		// Token: 0x060044D4 RID: 17620 RVA: 0x00152800 File Offset: 0x00150A00
		private bool GenerateConformityForTroop(MobileParty mobileParty, CharacterObject troop, int hours = 1)
		{
			int xpAmount = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetConformityChangePerHour(mobileParty.Party, troop).RoundedResultNumber * hours;
			mobileParty.PrisonRoster.AddXpToTroop(troop, xpAmount);
			return true;
		}

		// Token: 0x060044D5 RID: 17621 RVA: 0x00152844 File Offset: 0x00150A44
		private void ApplyPrisonerRecruitmentEffects(MobileParty mobileParty, CharacterObject troop, int num)
		{
			int prisonerRecruitmentMoraleEffect = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect(mobileParty.Party, troop, num);
			mobileParty.RecentEventsMorale += (float)prisonerRecruitmentMoraleEffect;
		}

		// Token: 0x060044D6 RID: 17622 RVA: 0x00152880 File Offset: 0x00150A80
		private void RecruitPrisonersAi(MobileParty mobileParty, CharacterObject troop, int num, int conformityCost)
		{
			mobileParty.PrisonRoster.GetElementNumber(troop);
			mobileParty.PrisonRoster.GetElementXp(troop);
			mobileParty.PrisonRoster.AddToCounts(troop, -num, false, 0, -conformityCost * num, true, -1);
			mobileParty.MemberRoster.AddToCounts(troop, num, false, 0, 0, true, -1);
			CampaignEventDispatcher.Instance.OnTroopRecruited(mobileParty.LeaderHero, null, null, troop, num);
			this.ApplyPrisonerRecruitmentEffects(mobileParty, troop, num);
		}

		// Token: 0x060044D7 RID: 17623 RVA: 0x001528EF File Offset: 0x00150AEF
		private bool IsPrisonerRecruitable(MobileParty mobileParty, CharacterObject character, out int conformityNeeded)
		{
			return Campaign.Current.Models.PrisonerRecruitmentCalculationModel.IsPrisonerRecruitable(mobileParty.Party, character, out conformityNeeded);
		}

		// Token: 0x060044D8 RID: 17624 RVA: 0x00152910 File Offset: 0x00150B10
		private void OnMainPartyPrisonerRecruited(FlattenedTroopRoster flattenedTroopRosters)
		{
			foreach (CharacterObject characterObject in flattenedTroopRosters.Troops)
			{
				CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
				this.ApplyPrisonerRecruitmentEffects(MobileParty.MainParty, characterObject, 1);
			}
		}

		// Token: 0x060044D9 RID: 17625 RVA: 0x00152970 File Offset: 0x00150B70
		public override void SyncData(IDataStore dataStore)
		{
		}
	}
}
