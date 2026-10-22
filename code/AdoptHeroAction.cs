using System;

namespace TaleWorlds.CampaignSystem.Actions
{
	// Token: 0x02000492 RID: 1170
	public static class AdoptHeroAction
	{
		// Token: 0x06004A0B RID: 18955 RVA: 0x00176539 File Offset: 0x00174739
		private static void ApplyInternal(Hero adoptedHero)
		{
			if (Hero.MainHero.IsFemale)
			{
				adoptedHero.Mother = Hero.MainHero;
			}
			else
			{
				adoptedHero.Father = Hero.MainHero;
			}
			adoptedHero.Clan = Clan.PlayerClan;
		}

		// Token: 0x06004A0C RID: 18956 RVA: 0x0017656A File Offset: 0x0017476A
		public static void Apply(Hero adoptedHero)
		{
			AdoptHeroAction.ApplyInternal(adoptedHero);
		}
	}
}
