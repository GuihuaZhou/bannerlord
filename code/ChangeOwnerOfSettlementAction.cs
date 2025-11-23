using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace TaleWorlds.CampaignSystem.Actions
{
	// Token: 0x02000499 RID: 1177
	public static class ChangeOwnerOfSettlementAction
	{
		// Token: 0x06004937 RID: 18743 RVA: 0x001706DC File Offset: 0x0016E8DC
		private static void ApplyInternal(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
		{
			Clan ownerClan = settlement.OwnerClan;
			Hero oldOwner = (ownerClan != null) ? ownerClan.Leader : null;
			if (settlement.Town != null)
			{
				settlement.Town.IsOwnerUnassigned = false;
			}
			if (settlement.IsFortification)
			{
				settlement.Town.OwnerClan = newOwner.Clan;
			}
			if (settlement.IsFortification)
			{
				if (settlement.Town.GarrisonParty == null)
				{
					settlement.AddGarrisonParty();
				}
				ChangeGovernorAction.RemoveGovernorOfIfExists(settlement.Town);
			}
			settlement.Party.SetVisualAsDirty();
			foreach (Village village in settlement.BoundVillages)
			{
				village.Settlement.Party.SetVisualAsDirty();
				if (village.VillagerPartyComponent != null && newOwner != null)
				{
					foreach (MobileParty mobileParty in MobileParty.All)
					{
						if (mobileParty.MapEvent == null && mobileParty != MobileParty.MainParty && mobileParty.ShortTermTargetParty == village.VillagerPartyComponent.MobileParty && !mobileParty.MapFaction.IsAtWarWith(newOwner.MapFaction))
						{
							mobileParty.SetMoveModeHold();
						}
					}
				}
			}
			bool openToClaim = (detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege || detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByClanDestruction || detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByLeaveFaction) && settlement.IsFortification;
			if (newOwner != null)
			{
				IFaction mapFaction = newOwner.MapFaction;
				if (settlement.Party.MapEvent != null && !settlement.Party.MapEvent.AttackerSide.LeaderParty.MapFaction.IsAtWarWith(mapFaction) && settlement.Party.MapEvent.Winner == null)
				{
					settlement.Party.MapEvent.DiplomaticallyFinished = true;
					foreach (WarPartyComponent warPartyComponent in settlement.MapFaction.WarPartyComponents)
					{
						MobileParty mobileParty2 = warPartyComponent.MobileParty;
						if (mobileParty2.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty2.TargetSettlement == settlement && mobileParty2.CurrentSettlement == null)
						{
							mobileParty2.SetMoveModeHold();
						}
					}
					settlement.Party.MapEvent.Update();
				}
				foreach (Clan clan in Clan.NonBanditFactions)
				{
					if (mapFaction == null || (clan.Kingdom == null && !clan.IsAtWarWith(mapFaction)) || (clan.Kingdom != null && !clan.Kingdom.IsAtWarWith(mapFaction)))
					{
						foreach (WarPartyComponent warPartyComponent2 in clan.WarPartyComponents)
						{
							MobileParty mobileParty3 = warPartyComponent2.MobileParty;
							if (mobileParty3.BesiegedSettlement != settlement && (mobileParty3.DefaultBehavior == AiBehavior.RaidSettlement || mobileParty3.DefaultBehavior == AiBehavior.BesiegeSettlement || mobileParty3.DefaultBehavior == AiBehavior.AssaultSettlement) && mobileParty3.TargetSettlement == settlement)
							{
								Army army = mobileParty3.Army;
								if (army != null)
								{
									army.FinishArmyObjective();
								}
								mobileParty3.SetMoveModeHold();
							}
						}
					}
				}
			}
			CampaignEventDispatcher.Instance.OnSettlementOwnerChanged(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail);
		}

		// Token: 0x06004938 RID: 18744 RVA: 0x00170A48 File Offset: 0x0016EC48
		public static void ApplyByDefault(Hero hero, Settlement settlement)
		{
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, hero, null, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.Default);
		}

		// Token: 0x06004939 RID: 18745 RVA: 0x00170A53 File Offset: 0x0016EC53
		public static void ApplyByKingDecision(Hero hero, Settlement settlement)
		{
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, hero, null, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision);
			if (settlement.Town != null)
			{
				settlement.Town.IsOwnerUnassigned = false;
			}
		}

		// Token: 0x0600493A RID: 18746 RVA: 0x00170A72 File Offset: 0x0016EC72
		public static void ApplyBySiege(Hero newOwner, Hero capturerHero, Settlement settlement)
		{
			if (settlement.Town != null)
			{
				settlement.Town.LastCapturedBy = capturerHero.Clan;
			}
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, newOwner, capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege);
		}

		// Token: 0x0600493B RID: 18747 RVA: 0x00170A96 File Offset: 0x0016EC96
		public static void ApplyByLeaveFaction(Hero hero, Settlement settlement)
		{
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, hero, null, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByLeaveFaction);
		}

		// Token: 0x0600493C RID: 18748 RVA: 0x00170AA1 File Offset: 0x0016ECA1
		public static void ApplyByBarter(Hero hero, Settlement settlement)
		{
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, hero, null, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter);
		}

		// Token: 0x0600493D RID: 18749 RVA: 0x00170AAC File Offset: 0x0016ECAC
		public static void ApplyByRebellion(Hero hero, Settlement settlement)
		{
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, hero, hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByRebellion);
		}

		// Token: 0x0600493E RID: 18750 RVA: 0x00170AB7 File Offset: 0x0016ECB7
		public static void ApplyByDestroyClan(Settlement settlement, Hero newOwner)
		{
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, newOwner, null, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByClanDestruction);
		}

		// Token: 0x0600493F RID: 18751 RVA: 0x00170AC2 File Offset: 0x0016ECC2
		public static void ApplyByGift(Settlement settlement, Hero newOwner)
		{
			ChangeOwnerOfSettlementAction.ApplyInternal(settlement, newOwner, null, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByGift);
		}

		// Token: 0x02000875 RID: 2165
		public enum ChangeOwnerOfSettlementDetail
		{
			// Token: 0x040023C0 RID: 9152
			Default,
			// Token: 0x040023C1 RID: 9153
			BySiege,
			// Token: 0x040023C2 RID: 9154
			ByBarter,
			// Token: 0x040023C3 RID: 9155
			ByLeaveFaction,
			// Token: 0x040023C4 RID: 9156
			ByKingDecision,
			// Token: 0x040023C5 RID: 9157
			ByGift,
			// Token: 0x040023C6 RID: 9158
			ByRebellion,
			// Token: 0x040023C7 RID: 9159
			ByClanDestruction
		}
	}
}
