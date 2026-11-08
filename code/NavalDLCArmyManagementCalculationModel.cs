using System;
using Helpers;
using NavalDLC.CharacterDevelopment;
using NavalDLC.Storyline;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace NavalDLC.GameComponents
{
	// Token: 0x02000108 RID: 264
	public class NavalDLCArmyManagementCalculationModel : ArmyManagementCalculationModel
	{
		// Token: 0x17000340 RID: 832
		// (get) Token: 0x06001337 RID: 4919 RVA: 0x0008C3DA File Offset: 0x0008A5DA
		public override float AIMobilePartySizeRatioToCallToArmy
		{
			get
			{
				return base.BaseModel.AIMobilePartySizeRatioToCallToArmy;
			}
		}

		// Token: 0x17000341 RID: 833
		// (get) Token: 0x06001338 RID: 4920 RVA: 0x0008C3E7 File Offset: 0x0008A5E7
		public override float PlayerMobilePartySizeRatioToCallToArmy
		{
			get
			{
				return base.BaseModel.PlayerMobilePartySizeRatioToCallToArmy;
			}
		}

		// Token: 0x17000342 RID: 834
		// (get) Token: 0x06001339 RID: 4921 RVA: 0x0008C3F4 File Offset: 0x0008A5F4
		public override float MinimumNeededFoodInDaysToCallToArmy
		{
			get
			{
				return base.BaseModel.MinimumNeededFoodInDaysToCallToArmy;
			}
		}

		// Token: 0x17000343 RID: 835
		// (get) Token: 0x0600133A RID: 4922 RVA: 0x0008C401 File Offset: 0x0008A601
		public override float MaximumDistanceToCallToArmy
		{
			get
			{
				return base.BaseModel.MaximumDistanceToCallToArmy;
			}
		}

		// Token: 0x17000344 RID: 836
		// (get) Token: 0x0600133B RID: 4923 RVA: 0x0008C40E File Offset: 0x0008A60E
		public override int InfluenceValuePerGold
		{
			get
			{
				return base.BaseModel.InfluenceValuePerGold;
			}
		}

		// Token: 0x17000345 RID: 837
		// (get) Token: 0x0600133C RID: 4924 RVA: 0x0008C41B File Offset: 0x0008A61B
		public override int AverageCallToArmyCost
		{
			get
			{
				return base.BaseModel.AverageCallToArmyCost;
			}
		}

		// Token: 0x17000346 RID: 838
		// (get) Token: 0x0600133D RID: 4925 RVA: 0x0008C428 File Offset: 0x0008A628
		public override int CohesionThresholdForDispersion
		{
			get
			{
				return base.BaseModel.CohesionThresholdForDispersion;
			}
		}

		// Token: 0x17000347 RID: 839
		// (get) Token: 0x0600133E RID: 4926 RVA: 0x0008C435 File Offset: 0x0008A635
		public override float MaximumWaitTime
		{
			get
			{
				return base.BaseModel.MaximumWaitTime;
			}
		}

		// Token: 0x0600133F RID: 4927 RVA: 0x0008C444 File Offset: 0x0008A644
		public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false)
		{
			ExplainedNumber result = base.BaseModel.CalculateDailyCohesionChange(army, includeDescriptions);
			if (army.LeaderParty != null && !army.LeaderParty.IsCurrentlyAtSea && PartyBaseHelper.HasFeat(army.LeaderParty.Party, NavalCulturalFeats.NordArmyCohesionFeat))
			{
				result.AddFactor(NavalCulturalFeats.NordArmyCohesionFeat.EffectBonus, GameTexts.FindText("str_culture", null));
			}
			return result;
		}

		// Token: 0x06001340 RID: 4928 RVA: 0x0008C4A8 File Offset: 0x0008A6A8
		public override int CalculateNewCohesion(Army army, PartyBase newParty, int calculatedCohesion, int sign)
		{
			return base.BaseModel.CalculateNewCohesion(army, newParty, calculatedCohesion, sign);
		}

		// Token: 0x06001341 RID: 4929 RVA: 0x0008C4BA File Offset: 0x0008A6BA
		public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
		{
			return base.BaseModel.CalculatePartyInfluenceCost(armyLeaderParty, party);
		}

		// Token: 0x06001342 RID: 4930 RVA: 0x0008C4C9 File Offset: 0x0008A6C9
		public override bool CanLordCreateArmy(MobileParty leaderParty, out MBList<MobileParty> possibleArmyMembers)
		{
			return base.BaseModel.CanLordCreateArmy(leaderParty, out possibleArmyMembers);
		}

		// Token: 0x06001343 RID: 4931 RVA: 0x0008C4D8 File Offset: 0x0008A6D8
		public override int CalculateTotalInfluenceCost(Army army, float percentage)
		{
			return base.BaseModel.CalculateTotalInfluenceCost(army, percentage);
		}

		// Token: 0x06001344 RID: 4932 RVA: 0x0008C4E8 File Offset: 0x0008A6E8
		public override bool CanPlayerCreateArmy(out TextObject disabledReason)
		{
			if (!NavalStorylineData.IsNavalStoryLineActive())
			{
				MenuContext currentMenuContext = Campaign.Current.CurrentMenuContext;
				string a;
				if (currentMenuContext == null)
				{
					a = null;
				}
				else
				{
					GameMenu gameMenu = currentMenuContext.GameMenu;
					a = ((gameMenu != null) ? gameMenu.StringId : null);
				}
				if (!(a == "naval_storyline_outside_town"))
				{
					return base.BaseModel.CanPlayerCreateArmy(out disabledReason);
				}
			}
			disabledReason = new TextObject("{=lwbwTg5b}You can't perform this action during this time.", null);
			return false;
		}

		// Token: 0x06001345 RID: 4933 RVA: 0x0008C545 File Offset: 0x0008A745
		public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
		{
			return base.BaseModel.CheckPartyEligibility(party, out explanation);
		}

		// Token: 0x06001346 RID: 4934 RVA: 0x0008C554 File Offset: 0x0008A754
		public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
		{
			return base.BaseModel.DailyBeingAtArmyInfluenceAward(armyMemberParty);
		}

		// Token: 0x06001347 RID: 4935 RVA: 0x0008C562 File Offset: 0x0008A762
		public override int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100)
		{
			return base.BaseModel.GetCohesionBoostInfluenceCost(army, percentageToBoost);
		}

		// Token: 0x06001348 RID: 4936 RVA: 0x0008C571 File Offset: 0x0008A771
		public override int GetPartyRelation(Hero hero)
		{
			return base.BaseModel.GetPartyRelation(hero);
		}

		// Token: 0x06001349 RID: 4937 RVA: 0x0008C57F File Offset: 0x0008A77F
		public override float GetPartySizeScore(MobileParty party)
		{
			return base.BaseModel.GetPartySizeScore(party);
		}
	}
}
