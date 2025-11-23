using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TaleWorlds.CampaignSystem.ComponentInterfaces
{
	// Token: 0x020001CB RID: 459
	public abstract class SettlementTaxModel : MBGameModel<SettlementTaxModel>
	{
		// Token: 0x17000779 RID: 1913
		// (get) Token: 0x06001DDF RID: 7647
		public abstract float SettlementCommissionRateTown { get; }

		// Token: 0x1700077A RID: 1914
		// (get) Token: 0x06001DE0 RID: 7648
		public abstract float SettlementCommissionRateVillage { get; }

		// Token: 0x1700077B RID: 1915
		// (get) Token: 0x06001DE1 RID: 7649
		public abstract int SettlementCommissionDecreaseSecurityThreshold { get; }

		// Token: 0x1700077C RID: 1916
		// (get) Token: 0x06001DE2 RID: 7650
		public abstract int MaximumDecreaseBasedOnSecuritySecurity { get; }

		// Token: 0x06001DE3 RID: 7651
		public abstract float GetTownTaxRatio(Town town);

		// Token: 0x06001DE4 RID: 7652
		public abstract float GetVillageTaxRatio(Village village);

		// Token: 0x06001DE5 RID: 7653
		public abstract float GetTownCommissionChangeBasedOnSecurity(Town town, float commission);

		// Token: 0x06001DE6 RID: 7654
		public abstract ExplainedNumber CalculateTownTax(Town town, bool includeDescriptions = false);

		// Token: 0x06001DE7 RID: 7655
		public abstract int CalculateVillageTaxFromIncome(Village village, int marketIncome);
	}
}
