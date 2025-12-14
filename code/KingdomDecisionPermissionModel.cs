using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ComponentInterfaces
{
	// Token: 0x020001A7 RID: 423
	public abstract class KingdomDecisionPermissionModel : MBGameModel<KingdomDecisionPermissionModel>
	{
		// Token: 0x06001CB5 RID: 7349
		public abstract bool IsPolicyDecisionAllowed(PolicyObject policy);

		// Token: 0x06001CB6 RID: 7350
		public abstract bool IsWarDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2, out TextObject reason);

		// Token: 0x06001CB7 RID: 7351
		public abstract bool IsPeaceDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2, out TextObject reason);

		// Token: 0x06001CB8 RID: 7352
		public abstract bool IsStartAllianceDecisionAllowedBetweenKingdoms(Kingdom kingdom1, Kingdom kingdom2, out TextObject reason);

		// Token: 0x06001CB9 RID: 7353
		public abstract bool IsAnnexationDecisionAllowed(Settlement annexedSettlement);

		// Token: 0x06001CBA RID: 7354
		public abstract bool IsExpulsionDecisionAllowed(Clan expelledClan);

		// Token: 0x06001CBB RID: 7355
		public abstract bool IsKingSelectionDecisionAllowed(Kingdom kingdom);
	}
}
