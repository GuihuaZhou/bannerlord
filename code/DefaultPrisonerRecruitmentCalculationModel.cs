using System;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x0200014B RID: 331
	public class DefaultPrisonerRecruitmentCalculationModel : PrisonerRecruitmentCalculationModel
	{
		// Token: 0x06001A43 RID: 6723 RVA: 0x000848FD File Offset: 0x00082AFD
		public override int GetConformityNeededToRecruitPrisoner(CharacterObject character)
		{
			return (character.Level + 6) * (character.Level + 6) - 10;
		}

		// Token: 0x06001A44 RID: 6724 RVA: 0x00084914 File Offset: 0x00082B14
		public override ExplainedNumber GetConformityChangePerHour(PartyBase party, CharacterObject troopToBoost)
		{
			ExplainedNumber result = new ExplainedNumber(10f, false, null);
			if (party.LeaderHero != null)
			{
				result.Add((float)party.LeaderHero.GetSkillValue(DefaultSkills.Leadership) * 0.05f, null, null);
			}
			if (troopToBoost.Tier <= 3)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Leadership.FerventAttacker, party.MobileParty, false, ref result, BattleEnvironment.Any);
			}
			if (troopToBoost.Tier >= 4)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Leadership.StoutDefender, party.MobileParty, false, ref result, BattleEnvironment.Any);
			}
			if (troopToBoost.Occupation != Occupation.Bandit)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Leadership.LoyaltyAndHonor, party.MobileParty, false, ref result, BattleEnvironment.Any);
			}
			if (troopToBoost.IsInfantry)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Leadership.LeadByExample, party.MobileParty, true, ref result, BattleEnvironment.Any);
			}
			if (troopToBoost.IsRanged)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Leadership.TrustedCommander, party.MobileParty, true, ref result, BattleEnvironment.Any);
			}
			if (troopToBoost.Occupation == Occupation.Bandit)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Roguery.Promises, party.MobileParty, false, ref result, BattleEnvironment.Any);
			}
			return result;
		}

		// Token: 0x06001A45 RID: 6725 RVA: 0x00084A0C File Offset: 0x00082C0C
		public override float GetPrisonerRecruitmentMoraleEffect(PartyBase party, CharacterObject character, int num)
		{
			CultureObject culture = character.Culture;
			Hero leaderHero = party.LeaderHero;
			bool flag = culture == ((leaderHero != null) ? leaderHero.Culture : null) && party.MobileParty != null && party.MobileParty.HasPerk(DefaultPerks.Leadership.Presence, true);
			bool flag2 = character.Occupation == Occupation.Bandit && party.MobileParty != null && party.MobileParty.HasPerk(DefaultPerks.Roguery.TwoFaced, true);
			if (flag || flag2)
			{
				return 0f;
			}
			float num2;
			if (character.Occupation == Occupation.Bandit)
			{
				num2 = -2f;
			}
			else
			{
				num2 = -1f;
			}
			float num3 = num2 * (float)num;
			if (party.LeaderHero != null)
			{
				float traitEffectBonus = TraitEffectHelper.GetTraitEffectBonus(party.LeaderHero, DefaultPersonalityTraitEffects.HonorRecruitPenaltyReductionEffect);
				if (traitEffectBonus != 0f)
				{
					num3 *= 1f + traitEffectBonus;
				}
			}
			return num3;
		}

		// Token: 0x06001A46 RID: 6726 RVA: 0x00084ACC File Offset: 0x00082CCC
		public override bool IsPrisonerRecruitable(PartyBase party, CharacterObject character, out int conformityNeeded)
		{
			if (!character.IsRegular || character.Tier > Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier || character.Tier < 2 || character.Culture.IsBandit)
			{
				conformityNeeded = 0;
				return false;
			}
			int elementXp = party.MobileParty.PrisonRoster.GetElementXp(character);
			conformityNeeded = this.GetConformityNeededToRecruitPrisoner(character);
			return elementXp >= conformityNeeded;
		}

		// Token: 0x06001A47 RID: 6727 RVA: 0x00084B3C File Offset: 0x00082D3C
		public override bool ShouldPartyRecruitPrisoners(PartyBase party)
		{
			return party.IsMobile && party.PartySizeLimit > party.MobileParty.MemberRoster.TotalManCount && !party.MobileParty.IsWageLimitExceeded() && !party.MobileParty.IsPatrolParty && (party.MobileParty.Morale > 30f || DefaultPrisonerRecruitmentCalculationModel.ShouldRecruitDueToPresencePerk(party.MobileParty));
		}

		// Token: 0x06001A48 RID: 6728 RVA: 0x00084BA4 File Offset: 0x00082DA4
		public override int CalculateRecruitableNumber(PartyBase party, CharacterObject character)
		{
			if (character.IsHero || party.PrisonRoster.Count == 0 || party.PrisonRoster.TotalRegulars <= 0)
			{
				return 0;
			}
			int conformityNeededToRecruitPrisoner = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetConformityNeededToRecruitPrisoner(character);
			int elementXp = party.PrisonRoster.GetElementXp(character);
			int elementNumber = party.PrisonRoster.GetElementNumber(character);
			return MathF.Min(elementXp / conformityNeededToRecruitPrisoner, elementNumber);
		}

		// Token: 0x06001A49 RID: 6729 RVA: 0x00084C0D File Offset: 0x00082E0D
		private static bool ShouldRecruitDueToPresencePerk(MobileParty mobileParty)
		{
			return mobileParty.HasPerk(DefaultPerks.Leadership.Presence, true);
		}

		// Token: 0x040008B2 RID: 2226
		private const int AILordMinTierRequirementForRecruitPrisoners = 2;
	}
}
