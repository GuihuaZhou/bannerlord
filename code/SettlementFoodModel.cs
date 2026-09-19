using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TaleWorlds.CampaignSystem.ComponentInterfaces
{
	// Token: 0x020001C4 RID: 452
	public abstract class SettlementFoodModel : MBGameModel<SettlementFoodModel>
	{
		// Token: 0x17000761 RID: 1889
		// (get) Token: 0x06001DF7 RID: 7671
		public abstract int FoodStocksUpperLimit { get; }

		// Token: 0x17000762 RID: 1890
		// (get) Token: 0x06001DF8 RID: 7672
		public abstract int NumberOfProsperityToEatOneFood { get; }

		// Token: 0x17000763 RID: 1891
		// (get) Token: 0x06001DF9 RID: 7673
		public abstract int NumberOfMenOnGarrisonToEatOneFood { get; }

		// Token: 0x17000764 RID: 1892
		// (get) Token: 0x06001DFA RID: 7674
		public abstract int CastleFoodStockUpperLimitBonus { get; }

		// Token: 0x06001DFB RID: 7675
		public abstract ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false);
	}
}
