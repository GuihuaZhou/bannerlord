using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x0200013B RID: 315
	public class DefaultPartyTroopUpgradeModel : PartyTroopUpgradeModel
	{
		// Token: 0x06001914 RID: 6420 RVA: 0x0007CD80 File Offset: 0x0007AF80
		public override bool CanPartyUpgradeTroopToTarget(PartyBase upgradingParty, CharacterObject upgradeableCharacter, CharacterObject upgradeTarget)
		{
			bool flag = Campaign.Current.Models.PartyTroopUpgradeModel.DoesPartyHaveRequiredItemsForUpgrade(upgradingParty, upgradeTarget);
			PerkObject perkObject;
			bool flag2 = Campaign.Current.Models.PartyTroopUpgradeModel.DoesPartyHaveRequiredPerksForUpgrade(upgradingParty, upgradeableCharacter, upgradeTarget, out perkObject);
			return Campaign.Current.Models.PartyTroopUpgradeModel.IsTroopUpgradeable(upgradingParty, upgradeableCharacter) && upgradeableCharacter.UpgradeTargets.Contains(upgradeTarget) && flag2 && flag;
		}

		// Token: 0x06001915 RID: 6421 RVA: 0x0007CDE9 File Offset: 0x0007AFE9
		public override bool IsTroopUpgradeable(PartyBase party, CharacterObject character)
		{
			return !character.IsHero && character.UpgradeTargets.Length != 0;
		}

		// Token: 0x06001916 RID: 6422 RVA: 0x0007CE00 File Offset: 0x0007B000
		public override int GetXpCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
		{
			if (upgradeTarget != null && characterObject.UpgradeTargets.Contains(upgradeTarget))
			{
				int tier = upgradeTarget.Tier;
				int num = 0;
				for (int i = characterObject.Tier + 1; i <= tier; i++)
				{
					if (i <= 1)
					{
						num += 100;
					}
					else if (i == 2)
					{
						num += 300;
					}
					else if (i == 3)
					{
						num += 550;
					}
					else if (i == 4)
					{
						num += 900;
					}
					else if (i == 5)
					{
						num += 1300;
					}
					else if (i == 6)
					{
						num += 1700;
					}
					else if (i == 7)
					{
						num += 2100;
					}
					else
					{
						int num2 = upgradeTarget.Level + 4;
						num += (int)(1.333f * (float)num2 * (float)num2);
					}
				}
				return num;
			}
			return 100000000;
		}

		// Token: 0x06001917 RID: 6423 RVA: 0x0007CEC0 File Offset: 0x0007B0C0
		public override ExplainedNumber GetGoldCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
		{
			PartyWageModel partyWageModel = Campaign.Current.Models.PartyWageModel;
			int roundedResultNumber = partyWageModel.GetTroopRecruitmentCost(upgradeTarget, null, true).RoundedResultNumber;
			int roundedResultNumber2 = partyWageModel.GetTroopRecruitmentCost(characterObject, null, true).RoundedResultNumber;
			bool flag = characterObject.Occupation == Occupation.Mercenary || characterObject.Occupation == Occupation.Gangster;
			ExplainedNumber result = new ExplainedNumber((float)(roundedResultNumber - roundedResultNumber2) / ((!flag) ? 2f : 3f), false, null);
			if (party.MobileParty.HasPerk(DefaultPerks.Steward.SoundReserves, false))
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.SoundReserves, party.MobileParty, true, ref result, false);
			}
			if (characterObject.IsRanged && party.MobileParty.HasPerk(DefaultPerks.Bow.RenownedArcher, true))
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Bow.RenownedArcher, party.MobileParty, false, ref result, false);
			}
			if (characterObject.IsMounted && PartyBaseHelper.HasFeat(party, DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat))
			{
				result.AddFactor(DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture", null));
			}
			if (flag && party.MobileParty.HasPerk(DefaultPerks.Steward.Contractors, false))
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.Contractors, party.MobileParty, true, ref result, false);
			}
			return result;
		}

		// Token: 0x06001918 RID: 6424 RVA: 0x0007CFE7 File Offset: 0x0007B1E7
		public override int GetSkillXpFromUpgradingTroops(PartyBase party, CharacterObject troop, int numberOfTroops)
		{
			return (troop.Level + 10) * numberOfTroops;
		}

		// Token: 0x06001919 RID: 6425 RVA: 0x0007CFF4 File Offset: 0x0007B1F4
		public override bool DoesPartyHaveRequiredItemsForUpgrade(PartyBase party, CharacterObject upgradeTarget)
		{
			ItemCategory upgradeRequiresItemFromCategory = upgradeTarget.UpgradeRequiresItemFromCategory;
			if (upgradeRequiresItemFromCategory != null)
			{
				int num = 0;
				for (int i = 0; i < party.ItemRoster.Count; i++)
				{
					ItemRosterElement itemRosterElement = party.ItemRoster[i];
					if (itemRosterElement.EquipmentElement.Item.ItemCategory == upgradeRequiresItemFromCategory)
					{
						num += itemRosterElement.Amount;
					}
				}
				return num > 0;
			}
			return true;
		}

		// Token: 0x0600191A RID: 6426 RVA: 0x0007D058 File Offset: 0x0007B258
		public override bool DoesPartyHaveRequiredPerksForUpgrade(PartyBase party, CharacterObject character, CharacterObject upgradeTarget, out PerkObject requiredPerk)
		{
			requiredPerk = null;
			if (character.Culture.IsBandit && !upgradeTarget.Culture.IsBandit)
			{
				requiredPerk = DefaultPerks.Leadership.VeteransRespect;
				return party.MobileParty.HasPerk(requiredPerk, true);
			}
			return true;
		}

		// Token: 0x0600191B RID: 6427 RVA: 0x0007D094 File Offset: 0x0007B294
		public override float GetUpgradeChanceForTroopUpgrade(PartyBase party, CharacterObject troop, int upgradeTargetIndex)
		{
			float result = 1f;
			int num = troop.UpgradeTargets.Length;
			if (num > 1 && upgradeTargetIndex >= 0 && upgradeTargetIndex < num)
			{
				if (party.LeaderHero != null && party.LeaderHero.PreferredUpgradeFormation != FormationClass.NumberOfAllFormations)
				{
					FormationClass preferredUpgradeFormation = party.LeaderHero.PreferredUpgradeFormation;
					if (CharacterHelper.SearchForFormationInTroopTree(troop.UpgradeTargets[upgradeTargetIndex], preferredUpgradeFormation))
					{
						result = 9999f;
					}
				}
				else
				{
					Hero leaderHero = party.LeaderHero;
					int num2 = (leaderHero != null) ? leaderHero.RandomValue : party.Id.GetHashCode();
					int deterministicHashCode = troop.StringId.GetDeterministicHashCode();
					uint num3 = (uint)(num2 >> (troop.Tier * 3 & 31) ^ deterministicHashCode);
					if ((long)upgradeTargetIndex == (long)((ulong)num3 % (ulong)((long)num)))
					{
						result = 9999f;
					}
				}
			}
			return result;
		}
	}
}
