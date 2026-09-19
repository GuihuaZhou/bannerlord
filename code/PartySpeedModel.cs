using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TaleWorlds.CampaignSystem.ComponentInterfaces
{
	// Token: 0x02000193 RID: 403
	public abstract class PartySpeedModel : MBGameModel<PartySpeedModel>
	{
		// Token: 0x17000716 RID: 1814
		// (get) Token: 0x06001C51 RID: 7249
		public abstract float BaseSpeed { get; }

		// Token: 0x17000717 RID: 1815
		// (get) Token: 0x06001C52 RID: 7250
		public abstract float MinimumSpeed { get; }

		// Token: 0x06001C53 RID: 7251
		public abstract ExplainedNumber CalculateBaseSpeed(MobileParty party, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0);

		// Token: 0x06001C54 RID: 7252
		public abstract ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed);
	}
}
