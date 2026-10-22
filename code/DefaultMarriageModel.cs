using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x0200012B RID: 299
	public class DefaultMarriageModel : MarriageModel
	{
		// Token: 0x17000685 RID: 1669
		// (get) Token: 0x060018CE RID: 6350 RVA: 0x00078C1C File Offset: 0x00076E1C
		public override int MinimumMarriageAgeMale
		{
			get
			{
				return 18;
			}
		}

		// Token: 0x17000686 RID: 1670
		// (get) Token: 0x060018CF RID: 6351 RVA: 0x00078C20 File Offset: 0x00076E20
		public override int MinimumMarriageAgeFemale
		{
			get
			{
				return 18;
			}
		}

		// Token: 0x060018D0 RID: 6352 RVA: 0x00078C24 File Offset: 0x00076E24
		public override bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero)
		{
			if (this.IsClanSuitableForMarriage(firstHero.Clan) && this.IsClanSuitableForMarriage(secondHero.Clan))
			{
				Clan clan = firstHero.Clan;
				if (((clan != null) ? clan.Leader : null) == firstHero)
				{
					Clan clan2 = secondHero.Clan;
					if (((clan2 != null) ? clan2.Leader : null) == secondHero)
					{
						return false;
					}
				}
				if (firstHero.IsFemale != secondHero.IsFemale && !this.AreHeroesRelated(firstHero, secondHero, 3))
				{
					Hero courtedHeroInOtherClan = Romance.GetCourtedHeroInOtherClan(firstHero, secondHero);
					if (courtedHeroInOtherClan != null && courtedHeroInOtherClan != secondHero)
					{
						return false;
					}
					Hero courtedHeroInOtherClan2 = Romance.GetCourtedHeroInOtherClan(secondHero, firstHero);
					return (courtedHeroInOtherClan2 == null || courtedHeroInOtherClan2 == firstHero) && firstHero.CanMarry() && secondHero.CanMarry();
				}
			}
			return false;
		}

		// Token: 0x060018D1 RID: 6353 RVA: 0x00078CC4 File Offset: 0x00076EC4
		public override bool IsClanSuitableForMarriage(Clan clan)
		{
			return clan != null && !clan.IsBanditFaction && !clan.IsRebelClan && !clan.IsEliminated;
		}

		// Token: 0x060018D2 RID: 6354 RVA: 0x00078CE4 File Offset: 0x00076EE4
		public override float NpcCoupleMarriageChance(Hero firstHero, Hero secondHero)
		{
			if (this.IsCoupleSuitableForMarriage(firstHero, secondHero))
			{
				float num = 0.002f;
				num *= 1f + (firstHero.Age - (float)Campaign.Current.Models.AgeModel.HeroComesOfAge) / 50f;
				num *= 1f + (secondHero.Age - (float)Campaign.Current.Models.AgeModel.HeroComesOfAge) / 50f;
				num *= 1f - MathF.Abs(secondHero.Age - firstHero.Age) / 50f;
				if (firstHero.Clan.Kingdom != secondHero.Clan.Kingdom)
				{
					num *= 0.5f;
				}
				float num2 = 0.5f + (float)firstHero.Clan.GetRelationWithClan(secondHero.Clan) / 200f;
				return num * num2;
			}
			return 0f;
		}

		// Token: 0x060018D3 RID: 6355 RVA: 0x00078DC3 File Offset: 0x00076FC3
		public override bool ShouldNpcMarriageBetweenClansBeAllowed(Clan consideringClan, Clan targetClan)
		{
			return targetClan != consideringClan && !consideringClan.IsAtWarWith(targetClan) && consideringClan.GetRelationWithClan(targetClan) >= -50;
		}

		// Token: 0x060018D4 RID: 6356 RVA: 0x00078DE4 File Offset: 0x00076FE4
		public override List<Hero> GetAdultChildrenSuitableForMarriage(Hero hero)
		{
			List<Hero> list = new List<Hero>();
			foreach (Hero hero2 in hero.Children)
			{
				if (hero2.CanMarry())
				{
					list.Add(hero2);
				}
			}
			return list;
		}

		// Token: 0x060018D5 RID: 6357 RVA: 0x00078E48 File Offset: 0x00077048
		private bool AreHeroesRelatedAux1(Hero firstHero, Hero secondHero, int ancestorDepth)
		{
			return firstHero == secondHero || (ancestorDepth > 0 && ((secondHero.Mother != null && this.AreHeroesRelatedAux1(firstHero, secondHero.Mother, ancestorDepth - 1)) || (secondHero.Father != null && this.AreHeroesRelatedAux1(firstHero, secondHero.Father, ancestorDepth - 1))));
		}

		// Token: 0x060018D6 RID: 6358 RVA: 0x00078E98 File Offset: 0x00077098
		private bool AreHeroesRelatedAux2(Hero firstHero, Hero secondHero, int ancestorDepth, int secondAncestorDepth)
		{
			return this.AreHeroesRelatedAux1(firstHero, secondHero, secondAncestorDepth) || (ancestorDepth > 0 && ((firstHero.Mother != null && this.AreHeroesRelatedAux2(firstHero.Mother, secondHero, ancestorDepth - 1, secondAncestorDepth)) || (firstHero.Father != null && this.AreHeroesRelatedAux2(firstHero.Father, secondHero, ancestorDepth - 1, secondAncestorDepth))));
		}

		// Token: 0x060018D7 RID: 6359 RVA: 0x00078EF3 File Offset: 0x000770F3
		private bool AreHeroesRelated(Hero firstHero, Hero secondHero, int ancestorDepth)
		{
			return this.AreHeroesRelatedAux2(firstHero, secondHero, ancestorDepth, ancestorDepth);
		}

		// Token: 0x060018D8 RID: 6360 RVA: 0x00078F00 File Offset: 0x00077100
		public override int GetEffectiveRelationIncrease(Hero firstHero, Hero secondHero)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(20f, false, null);
			SkillHelper.AddSkillBonusForCharacter(DefaultSkillEffects.CharmRelationBonus, firstHero.IsFemale ? secondHero.CharacterObject : firstHero.CharacterObject, ref explainedNumber);
			return MathF.Round(explainedNumber.ResultNumber);
		}

		// Token: 0x060018D9 RID: 6361 RVA: 0x00078F4C File Offset: 0x0007714C
		public override bool IsSuitableForMarriage(Hero maidenOrSuitor)
		{
			if (maidenOrSuitor.IsActive && maidenOrSuitor.Spouse == null && maidenOrSuitor.IsLord && !maidenOrSuitor.IsMinorFactionHero && !maidenOrSuitor.IsNotable && !maidenOrSuitor.IsTemplate)
			{
				MobileParty partyBelongedTo = maidenOrSuitor.PartyBelongedTo;
				if (((partyBelongedTo != null) ? partyBelongedTo.MapEvent : null) == null)
				{
					MobileParty partyBelongedTo2 = maidenOrSuitor.PartyBelongedTo;
					if (((partyBelongedTo2 != null) ? partyBelongedTo2.Army : null) == null)
					{
						IMarriageOfferCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IMarriageOfferCampaignBehavior>();
						if (campaignBehavior != null && campaignBehavior.IsHeroEngaged(maidenOrSuitor))
						{
							return false;
						}
						if (maidenOrSuitor.IsFemale)
						{
							return maidenOrSuitor.CharacterObject.Age >= (float)this.MinimumMarriageAgeFemale;
						}
						return maidenOrSuitor.CharacterObject.Age >= (float)this.MinimumMarriageAgeMale;
					}
				}
			}
			return false;
		}

		// Token: 0x060018DA RID: 6362 RVA: 0x00079014 File Offset: 0x00077214
		public override Clan GetClanAfterMarriage(Hero firstHero, Hero secondHero)
		{
			if (firstHero.IsHumanPlayerCharacter)
			{
				return firstHero.Clan;
			}
			if (secondHero.IsHumanPlayerCharacter)
			{
				return secondHero.Clan;
			}
			if (firstHero.Clan.Leader == firstHero)
			{
				return firstHero.Clan;
			}
			if (secondHero.Clan.Leader == secondHero)
			{
				return secondHero.Clan;
			}
			if (!firstHero.IsFemale)
			{
				return firstHero.Clan;
			}
			return secondHero.Clan;
		}

		// Token: 0x0400081D RID: 2077
		private const float BaseMarriageChanceForNpcs = 0.002f;
	}
}
