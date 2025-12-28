using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace TaleWorlds.CampaignSystem.BarterSystem
{
	// Token: 0x0200047A RID: 1146
	public class BarterManager
	{
		// Token: 0x17000E42 RID: 3650
		// (get) Token: 0x060047EB RID: 18411 RVA: 0x0016BAE2 File Offset: 0x00169CE2
		public static BarterManager Instance
		{
			get
			{
				return Campaign.Current.BarterManager;
			}
		}

		// Token: 0x17000E43 RID: 3651
		// (get) Token: 0x060047EC RID: 18412 RVA: 0x0016BAEE File Offset: 0x00169CEE
		// (set) Token: 0x060047ED RID: 18413 RVA: 0x0016BAF6 File Offset: 0x00169CF6
		[SaveableProperty(1)]
		public bool LastBarterIsAccepted { get; internal set; }

		// Token: 0x060047EE RID: 18414 RVA: 0x0016BAFF File Offset: 0x00169CFF
		public BarterManager()
		{
			this._barteredHeroes = new Dictionary<Hero, CampaignTime>();
		}

		// Token: 0x060047EF RID: 18415 RVA: 0x0016BB12 File Offset: 0x00169D12
		public void BeginPlayerBarter(BarterData args)
		{
			if (this.BarterBegin != null)
			{
				this.BarterBegin(args);
			}
			ICampaignMission campaignMission = CampaignMission.Current;
			if (campaignMission == null)
			{
				return;
			}
			campaignMission.SetMissionMode(MissionMode.Barter, false);
		}

		// Token: 0x060047F0 RID: 18416 RVA: 0x0016BB3C File Offset: 0x00169D3C
		private void AddBaseBarterables(BarterData args, IEnumerable<Barterable> defaultBarterables)
		{
			if (defaultBarterables != null)
			{
				bool flag = false;
				foreach (Barterable barterable in defaultBarterables)
				{
					if (!flag)
					{
						args.AddBarterGroup(new DefaultsBarterGroup());
						flag = true;
					}
					barterable.SetIsOffered(true);
					args.AddBarterable<OtherBarterGroup>(barterable, true);
					barterable.SetIsOffered(true);
				}
			}
		}

		// Token: 0x060047F1 RID: 18417 RVA: 0x0016BBA8 File Offset: 0x00169DA8
		public void StartBarterOffer(Hero offerer, Hero other, PartyBase offererParty, PartyBase otherParty, Hero beneficiaryOfOtherHero = null, BarterManager.BarterContextInitializer InitContext = null, int persuasionCostReduction = 0, bool isAIBarter = false, IEnumerable<Barterable> defaultBarterables = null)
		{
			this.LastBarterIsAccepted = false;
			if (offerer == Hero.MainHero && other != null && InitContext == null)
			{
				if (!this.CanPlayerBarterWithHero(other))
				{
					Debug.FailedAssert("Barter with the hero is on cooldown.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\BarterSystem\\BarterManager.cs", "StartBarterOffer", 83);
					return;
				}
				this.ClearHeroCooldowns();
			}
			BarterData args = new BarterData(offerer, beneficiaryOfOtherHero ?? other, offererParty, otherParty, InitContext, persuasionCostReduction, false);
			this.AddBaseBarterables(args, defaultBarterables);
			CampaignEventDispatcher.Instance.OnBarterablesRequested(args);
			Campaign.Current.ConversationManager.CurrentConversationIsFirst = false;
			if (!isAIBarter)
			{
				Campaign.Current.BarterManager.BeginPlayerBarter(args);
			}
		}

		// Token: 0x060047F2 RID: 18418 RVA: 0x0016BC40 File Offset: 0x00169E40
		public void ExecuteAiBarter(IFaction faction1, IFaction faction2, Hero faction1Hero, Hero faction2Hero, Barterable barterable)
		{
			this.ExecuteAiBarter(faction1, faction2, faction1Hero, faction2Hero, new Barterable[]
			{
				barterable
			});
		}

		// Token: 0x060047F3 RID: 18419 RVA: 0x0016BC64 File Offset: 0x00169E64
		public void ExecuteAiBarter(IFaction faction1, IFaction faction2, Hero faction1Hero, Hero faction2Hero, IEnumerable<Barterable> baseBarterables)
		{
			BarterData barterData = new BarterData(faction1.Leader, faction2.Leader, null, null, null, 0, true);
			barterData.AddBarterGroup(new DefaultsBarterGroup());
			foreach (Barterable barterable in baseBarterables)
			{
				barterable.SetIsOffered(true);
				barterData.AddBarterable<DefaultsBarterGroup>(barterable, true);
			}
			CampaignEventDispatcher.Instance.OnBarterablesRequested(barterData);
			Campaign.Current.BarterManager.ExecuteAIBarter(barterData, faction1, faction2, faction1Hero, faction2Hero);
		}

		// Token: 0x060047F4 RID: 18420 RVA: 0x0016BCF8 File Offset: 0x00169EF8
		public void ExecuteAIBarter(BarterData barterData, IFaction faction1, IFaction faction2, Hero faction1Hero, Hero faction2Hero)
		{
			this.MakeBalanced(barterData, faction1, faction2, faction2Hero, 1f);
			this.MakeBalanced(barterData, faction2, faction1, faction1Hero, 1f);
			float offerValueForFaction = this.GetOfferValueForFaction(barterData, faction1);
			float offerValueForFaction2 = this.GetOfferValueForFaction(barterData, faction2);
			if (offerValueForFaction >= 0f && offerValueForFaction2 >= 0f)
			{
				this.ApplyBarterOffer(barterData.OffererHero, barterData.OtherHero, barterData.GetOfferedBarterables());
			}
		}

		// Token: 0x060047F5 RID: 18421 RVA: 0x0016BD60 File Offset: 0x00169F60
		private void MakeBalanced(BarterData args, IFaction faction1, IFaction faction2, Hero faction2Hero, float fulfillRatio)
		{
			foreach (ValueTuple<Barterable, int> valueTuple in BarterHelper.GetAutoBalanceBarterablesAdd(args, faction1, faction2, faction2Hero, fulfillRatio))
			{
				Barterable item = valueTuple.Item1;
				int item2 = valueTuple.Item2;
				if (!item.IsOffered)
				{
					item.SetIsOffered(true);
					item.CurrentAmount = 0;
				}
				item.CurrentAmount += item2;
			}
		}

		// Token: 0x060047F6 RID: 18422 RVA: 0x0016BDDC File Offset: 0x00169FDC
		public void Close()
		{
			if (CampaignMission.Current != null)
			{
				CampaignMission.Current.SetMissionMode(MissionMode.Conversation, false);
			}
			if (this.Closed != null)
			{
				this.Closed();
			}
		}

		// Token: 0x060047F7 RID: 18423 RVA: 0x0016BE04 File Offset: 0x0016A004
		public bool IsOfferAcceptable(BarterData args, Hero hero, PartyBase party)
		{
			return this.GetOfferValue(hero, party, args.OffererParty, args.GetOfferedBarterables()) > -0.01f;
		}

		// Token: 0x060047F8 RID: 18424 RVA: 0x0016BE24 File Offset: 0x0016A024
		public float GetOfferValueForFaction(BarterData barterData, IFaction faction)
		{
			int num = 0;
			foreach (Barterable barterable in barterData.GetOfferedBarterables())
			{
				num += barterable.GetValueForFaction(faction);
			}
			return (float)num;
		}

		// Token: 0x060047F9 RID: 18425 RVA: 0x0016BE80 File Offset: 0x0016A080
		public float GetOfferValue(Hero selfHero, PartyBase selfParty, PartyBase offererParty, IEnumerable<Barterable> offeredBarters)
		{
			float num = 0f;
			IFaction faction;
			if (((selfHero != null) ? selfHero.Clan : null) != null)
			{
				IFaction clan = selfHero.Clan;
				faction = clan;
			}
			else
			{
				faction = selfParty.MapFaction;
			}
			IFaction faction2 = faction;
			foreach (Barterable barterable in offeredBarters)
			{
				num += (float)barterable.GetValueForFaction(faction2);
			}
			this._overpayAmount = ((num > 0f) ? num : 0f);
			return num;
		}

		// Token: 0x060047FA RID: 18426 RVA: 0x0016BF0C File Offset: 0x0016A10C
		public void ApplyAndFinalizePlayerBarter(Hero offererHero, Hero otherHero, BarterData barterData)
		{
			this.LastBarterIsAccepted = true;
			this.ApplyBarterOffer(offererHero, otherHero, barterData.GetOfferedBarterables());
			if (otherHero != null)
			{
				this.HandleHeroCooldown(otherHero);
			}
		}

		// Token: 0x060047FB RID: 18427 RVA: 0x0016BF2D File Offset: 0x0016A12D
		public void CancelAndFinalizePlayerBarter(Hero offererHero, Hero otherHero, BarterData barterData)
		{
			this.CancelBarter(offererHero, otherHero, barterData.GetOfferedBarterables());
		}

		// Token: 0x060047FC RID: 18428 RVA: 0x0016BF40 File Offset: 0x0016A140
		private void ApplyBarterOffer(Hero offererHero, Hero otherHero, List<Barterable> barters)
		{
			foreach (Barterable barterable in barters)
			{
				barterable.Apply();
			}
			CampaignEventDispatcher.Instance.OnBarterAccepted(offererHero, otherHero, barters);
			if (offererHero == Hero.MainHero)
			{
				if (this._overpayAmount > 0f && otherHero != null)
				{
					this.ApplyOverpayBonus(otherHero);
				}
				this.Close();
				if (Campaign.Current.ConversationManager.IsConversationInProgress)
				{
					Campaign.Current.ConversationManager.ContinueConversation();
				}
				MBInformationManager.AddQuickInformation(GameTexts.FindText("str_offer_accepted", null), 0, null, null, "");
			}
		}

		// Token: 0x060047FD RID: 18429 RVA: 0x0016BFF8 File Offset: 0x0016A1F8
		private void CancelBarter(Hero offererHero, Hero otherHero, List<Barterable> offeredBarters)
		{
			this.Close();
			MBInformationManager.AddQuickInformation(GameTexts.FindText("str_offer_rejected", null), 0, null, null, "");
			CampaignEventDispatcher.Instance.OnBarterCanceled(offererHero, otherHero, offeredBarters);
			Campaign.Current.ConversationManager.ContinueConversation();
		}

		// Token: 0x060047FE RID: 18430 RVA: 0x0016C034 File Offset: 0x0016A234
		private void ApplyOverpayBonus(Hero otherHero)
		{
			if (otherHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
			{
				return;
			}
			int num = Campaign.Current.Models.BarterModel.CalculateOverpayRelationIncreaseCosts(otherHero, this._overpayAmount);
			if (num > 0)
			{
				ChangeRelationAction.ApplyRelationChangeBetweenHeroes(Hero.MainHero, otherHero, num, true);
			}
		}

		// Token: 0x060047FF RID: 18431 RVA: 0x0016C088 File Offset: 0x0016A288
		public bool CanPlayerBarterWithHero(Hero hero)
		{
			CampaignTime campaignTime;
			return !this._barteredHeroes.TryGetValue(hero, out campaignTime) || campaignTime.IsPast;
		}

		// Token: 0x06004800 RID: 18432 RVA: 0x0016C0B0 File Offset: 0x0016A2B0
		private void HandleHeroCooldown(Hero hero)
		{
			CampaignTime value = CampaignTime.Now + CampaignTime.Days((float)Campaign.Current.Models.BarterModel.BarterCooldownWithHeroInDays);
			if (!this._barteredHeroes.ContainsKey(hero))
			{
				this._barteredHeroes.Add(hero, value);
				return;
			}
			this._barteredHeroes[hero] = value;
		}

		// Token: 0x06004801 RID: 18433 RVA: 0x0016C10C File Offset: 0x0016A30C
		private void ClearHeroCooldowns()
		{
			foreach (KeyValuePair<Hero, CampaignTime> keyValuePair in new Dictionary<Hero, CampaignTime>(this._barteredHeroes))
			{
				if (keyValuePair.Value.IsPast)
				{
					this._barteredHeroes.Remove(keyValuePair.Key);
				}
			}
		}

		// Token: 0x06004802 RID: 18434 RVA: 0x0016C184 File Offset: 0x0016A384
		public bool InitializeMarriageBarterContext(Barterable barterable, BarterData args, object obj)
		{
			Hero hero = null;
			Hero hero2 = null;
			if (obj != null)
			{
				Tuple<Hero, Hero> tuple = obj as Tuple<Hero, Hero>;
				if (tuple != null)
				{
					hero = tuple.Item1;
					hero2 = tuple.Item2;
				}
			}
			MarriageBarterable marriageBarterable = barterable as MarriageBarterable;
			return marriageBarterable != null && hero != null && hero2 != null && marriageBarterable.ProposingHero == hero2 && marriageBarterable.HeroBeingProposedTo == hero;
		}

		// Token: 0x06004803 RID: 18435 RVA: 0x0016C1D4 File Offset: 0x0016A3D4
		public bool InitializeJoinFactionBarterContext(Barterable barterable, BarterData args, object obj)
		{
			return barterable.GetType() == typeof(JoinKingdomAsClanBarterable) && barterable.OriginalOwner == Hero.OneToOneConversationHero;
		}

		// Token: 0x06004804 RID: 18436 RVA: 0x0016C1FC File Offset: 0x0016A3FC
		public bool InitializeMakePeaceBarterContext(Barterable barterable, BarterData args, object obj)
		{
			return barterable.GetType() == typeof(PeaceBarterable) && barterable.OriginalOwner == args.OtherHero;
		}

		// Token: 0x06004805 RID: 18437 RVA: 0x0016C225 File Offset: 0x0016A425
		public bool InitializeSafePassageBarterContext(Barterable barterable, BarterData args, object obj)
		{
			if (barterable.GetType() == typeof(SafePassageBarterable))
			{
				PartyBase originalParty = barterable.OriginalParty;
				MobileParty conversationParty = MobileParty.ConversationParty;
				return originalParty == ((conversationParty != null) ? conversationParty.Party : null);
			}
			return false;
		}

		// Token: 0x06004806 RID: 18438 RVA: 0x0016C259 File Offset: 0x0016A459
		internal static void AutoGeneratedStaticCollectObjectsBarterManager(object o, List<object> collectedObjects)
		{
			((BarterManager)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
		}

		// Token: 0x06004807 RID: 18439 RVA: 0x0016C267 File Offset: 0x0016A467
		protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
		{
			collectedObjects.Add(this._barteredHeroes);
		}

		// Token: 0x06004808 RID: 18440 RVA: 0x0016C275 File Offset: 0x0016A475
		internal static object AutoGeneratedGetMemberValueLastBarterIsAccepted(object o)
		{
			return ((BarterManager)o).LastBarterIsAccepted;
		}

		// Token: 0x06004809 RID: 18441 RVA: 0x0016C287 File Offset: 0x0016A487
		internal static object AutoGeneratedGetMemberValue_barteredHeroes(object o)
		{
			return ((BarterManager)o)._barteredHeroes;
		}

		// Token: 0x040013CA RID: 5066
		public BarterManager.BarterCloseEventDelegate Closed;

		// Token: 0x040013CB RID: 5067
		public BarterManager.BarterBeginEventDelegate BarterBegin;

		// Token: 0x040013CC RID: 5068
		[SaveableField(2)]
		private readonly Dictionary<Hero, CampaignTime> _barteredHeroes;

		// Token: 0x040013CD RID: 5069
		private float _overpayAmount;

		// Token: 0x0200086B RID: 2155
		// (Invoke) Token: 0x06006749 RID: 26441
		public delegate bool BarterContextInitializer(Barterable barterable, BarterData args, object obj = null);

		// Token: 0x0200086C RID: 2156
		// (Invoke) Token: 0x0600674D RID: 26445
		public delegate void BarterCloseEventDelegate();

		// Token: 0x0200086D RID: 2157
		// (Invoke) Token: 0x06006751 RID: 26449
		public delegate void BarterBeginEventDelegate(BarterData args);
	}
}
