using System;
using System.Collections.Generic;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x020003FA RID: 1018
	public interface IAllianceCampaignBehavior
	{
		// Token: 0x06004039 RID: 16441
		void OnAllianceOfferedToPlayerKingdom(Kingdom proposerKingdom);

		// Token: 0x0600403A RID: 16442
		void OnAllianceOfferedToPlayer(Kingdom proposerKingdom);

		// Token: 0x0600403B RID: 16443
		void OnCallToWarAgreementProposedToPlayerKingdom(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst);

		// Token: 0x0600403C RID: 16444
		void OnCallToWarAgreementProposedByPlayerKingdom(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst);

		// Token: 0x0600403D RID: 16445
		void OnCallToWarAgreementProposedToPlayer(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst);

		// Token: 0x0600403E RID: 16446
		void OnCallToWarAgreementProposedByPlayer(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst);

		// Token: 0x0600403F RID: 16447
		bool IsAllyWithKingdom(Kingdom kingdom1, Kingdom kingdom2);

		// Token: 0x06004040 RID: 16448
		void StartAlliance(Kingdom proposerKingdom, Kingdom receiverKingdom);

		// Token: 0x06004041 RID: 16449
		void EndAlliance(Kingdom kingdom1, Kingdom kingdom2);

		// Token: 0x06004042 RID: 16450
		bool HasCalledToWar(Kingdom callingKingdom, Kingdom calledKingdom);

		// Token: 0x06004043 RID: 16451
		bool IsAtWarByCallToWarAgreement(Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, out Kingdom callingKingdom);

		// Token: 0x06004044 RID: 16452
		void StartCallToWarAgreement(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, int callToWarCost, bool isPlayerPaying = false);

		// Token: 0x06004045 RID: 16453
		void EndCallToWarAgreement(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst);

		// Token: 0x06004046 RID: 16454
		List<Kingdom> GetKingdomsToCallToWarAgainst(Kingdom callingKingdom, Kingdom calledKingdom);

		// Token: 0x06004047 RID: 16455
		CampaignTime GetAllianceEndDate(Kingdom kingdom1, Kingdom kingdom2);

		// Token: 0x06004048 RID: 16456
		void DenyCallToWarAgreement(Kingdom callingKingdom, Kingdom calledKingdom);
	}
}
