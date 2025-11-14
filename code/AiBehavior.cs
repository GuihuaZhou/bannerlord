using System;

namespace TaleWorlds.CampaignSystem.Party
{
	// Token: 0x020002F2 RID: 754
	public enum AiBehavior
	{
		// Token: 0x04000C64 RID: 3172
		Hold,
		// Token: 0x04000C65 RID: 3173
		None,
		// Token: 0x04000C66 RID: 3174
		GoToSettlement,
		// Token: 0x04000C67 RID: 3175
		AssaultSettlement,
		// Token: 0x04000C68 RID: 3176
		RaidSettlement,
		// Token: 0x04000C69 RID: 3177
		BesiegeSettlement,
		// Token: 0x04000C6A RID: 3178
		EngageParty,
		// Token: 0x04000C6B RID: 3179
		JoinParty,
		// Token: 0x04000C6C RID: 3180
		GoAroundParty,
		// Token: 0x04000C6D RID: 3181
		GoToPoint,
		// Token: 0x04000C6E RID: 3182
		FleeToPoint,
		// Token: 0x04000C6F RID: 3183
		FleeToGate,
		// Token: 0x04000C70 RID: 3184
		FleeToParty,
		// Token: 0x04000C71 RID: 3185
		PatrolAroundPoint,
		// Token: 0x04000C72 RID: 3186
		EscortParty,
		// Token: 0x04000C73 RID: 3187
		DefendSettlement,
		// Token: 0x04000C74 RID: 3188
		DoOperation,
		// Token: 0x04000C75 RID: 3189
		MoveToNearestLandOrPort,
		// Token: 0x04000C76 RID: 3190
		NumAiBehaviors
	}
}