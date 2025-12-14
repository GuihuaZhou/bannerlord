using System;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ComponentInterfaces
{
	// Token: 0x020001CF RID: 463
	public abstract class ClanTierModel : MBGameModel<ClanTierModel>
	{
		// Token: 0x1700077E RID: 1918
		// (get) Token: 0x06001DF1 RID: 7665
		public abstract int MinClanTier { get; }

		// Token: 0x1700077F RID: 1919
		// (get) Token: 0x06001DF2 RID: 7666
		public abstract int MaxClanTier { get; }

		// Token: 0x17000780 RID: 1920
		// (get) Token: 0x06001DF3 RID: 7667
		public abstract int MercenaryEligibleTier { get; }

		// Token: 0x17000781 RID: 1921
		// (get) Token: 0x06001DF4 RID: 7668
		public abstract int VassalEligibleTier { get; }

		// Token: 0x17000782 RID: 1922
		// (get) Token: 0x06001DF5 RID: 7669
		public abstract int BannerEligibleTier { get; }

		// Token: 0x17000783 RID: 1923
		// (get) Token: 0x06001DF6 RID: 7670
		public abstract int RebelClanStartingTier { get; }

		// Token: 0x17000784 RID: 1924
		// (get) Token: 0x06001DF7 RID: 7671
		public abstract int CompanionToLordClanStartingTier { get; }

		// Token: 0x06001DF8 RID: 7672
		public abstract int CalculateInitialRenown(Clan clan);

		// Token: 0x06001DF9 RID: 7673
		public abstract int CalculateInitialInfluence(Clan clan);

		// Token: 0x06001DFA RID: 7674
		public abstract int CalculateTier(Clan clan);

		// Token: 0x06001DFB RID: 7675
		public abstract ValueTuple<ExplainedNumber, bool> HasUpcomingTier(Clan clan, out TextObject extraExplanation, bool includeDescriptions = false);

		// Token: 0x06001DFC RID: 7676
		public abstract int GetRequiredRenownForTier(int tier);

		// Token: 0x06001DFD RID: 7677
		public abstract int GetPartyLimitForTier(Clan clan, int clanTierToCheck);

		// Token: 0x06001DFE RID: 7678
		public abstract int GetCompanionLimit(Clan clan);
	}
}
