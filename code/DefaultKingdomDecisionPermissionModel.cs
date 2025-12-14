using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x02000123 RID: 291
	public class DefaultKingdomDecisionPermissionModel : KingdomDecisionPermissionModel
	{
		// Token: 0x06001825 RID: 6181 RVA: 0x00074037 File Offset: 0x00072237
		public override bool IsPolicyDecisionAllowed(PolicyObject policy)
		{
			return true;
		}

		// Token: 0x06001826 RID: 6182 RVA: 0x0007403A File Offset: 0x0007223A
		public override bool IsWarDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
		{
			reason = null;
			return true;
		}

		// Token: 0x06001827 RID: 6183 RVA: 0x00074040 File Offset: 0x00072240
		public override bool IsPeaceDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
		{
			reason = null;
			if (!Campaign.Current.Models.DiplomacyModel.IsAtConstantWar(kingdom1, kingdom2))
			{
				IAllianceCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
				if (campaignBehavior == null || !campaignBehavior.IsAtWarByCallToWarAgreement(kingdom1, kingdom2))
				{
					if (!Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(kingdom1, kingdom2))
					{
						reason = new TextObject("{=JkQ7fmcX}The enemy is not open to negotiations.", null);
						return false;
					}
					return true;
				}
			}
			reason = new TextObject("{=eNPupZOp}These kingdoms can not declare peace at this time.", null);
			return false;
		}

		// Token: 0x06001828 RID: 6184 RVA: 0x000740B9 File Offset: 0x000722B9
		public override bool IsAnnexationDecisionAllowed(Settlement annexedSettlement)
		{
			return true;
		}

		// Token: 0x06001829 RID: 6185 RVA: 0x000740BC File Offset: 0x000722BC
		public override bool IsExpulsionDecisionAllowed(Clan expelledClan)
		{
			return true;
		}

		// Token: 0x0600182A RID: 6186 RVA: 0x000740BF File Offset: 0x000722BF
		public override bool IsKingSelectionDecisionAllowed(Kingdom kingdom)
		{
			return true;
		}

		// Token: 0x0600182B RID: 6187 RVA: 0x000740C2 File Offset: 0x000722C2
		public override bool IsStartAllianceDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2, out TextObject reason)
		{
			reason = null;
			return true;
		}
	}
}
