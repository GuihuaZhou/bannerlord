using System;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x0200040F RID: 1039
	public interface ITradeAgreementsCampaignBehavior
	{
		// Token: 0x06004177 RID: 16759
		void MakeTradeAgreement(Kingdom kingdom1, Kingdom kingdom2, CampaignTime duration);

		// Token: 0x06004178 RID: 16760
		bool HasTradeAgreement(Kingdom kingdom, Kingdom other, out TradeAgreementsCampaignBehavior.TradeAgreement tradeAgreement);

		// Token: 0x06004179 RID: 16761
		void EndTradeAgreement(Kingdom kingdom, Kingdom other);

		// Token: 0x0600417A RID: 16762
		void OnTradeAgreementOfferedToPlayer(Kingdom fromKingdom);

		// Token: 0x0600417B RID: 16763
		CampaignTime GetTradeAgreementEndDate(Kingdom kingdom, Kingdom other);

		// Token: 0x0600417C RID: 16764
		void OnTradeGoldDistributedInKingdom(Kingdom kingdom1, Kingdom kingdom2, Clan clan, int share);
	}
}
