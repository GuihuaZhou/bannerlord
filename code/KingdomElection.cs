using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace TaleWorlds.CampaignSystem.Election
{
	// Token: 0x020002CE RID: 718
	public class KingdomElection
	{
		// Token: 0x17000982 RID: 2434
		// (get) Token: 0x060026E6 RID: 9958 RVA: 0x000A266E File Offset: 0x000A086E
		public MBReadOnlyList<DecisionOutcome> PossibleOutcomes
		{
			get
			{
				return this._possibleOutcomes;
			}
		}

		// Token: 0x17000983 RID: 2435
		// (get) Token: 0x060026E7 RID: 9959 RVA: 0x000A2676 File Offset: 0x000A0876
		// (set) Token: 0x060026E8 RID: 9960 RVA: 0x000A267E File Offset: 0x000A087E
		[SaveableProperty(7)]
		public bool IsCancelled { get; private set; }

		// Token: 0x17000984 RID: 2436
		// (get) Token: 0x060026E9 RID: 9961 RVA: 0x000A2687 File Offset: 0x000A0887
		public bool IsPlayerSupporter
		{
			get
			{
				return this.PlayerAsSupporter != null;
			}
		}

		// Token: 0x17000985 RID: 2437
		// (get) Token: 0x060026EA RID: 9962 RVA: 0x000A2692 File Offset: 0x000A0892
		private Supporter PlayerAsSupporter
		{
			get
			{
				return this._supporters.FirstOrDefault((Supporter x) => x.IsPlayer);
			}
		}

		// Token: 0x17000986 RID: 2438
		// (get) Token: 0x060026EB RID: 9963 RVA: 0x000A26BE File Offset: 0x000A08BE
		public bool IsPlayerChooser
		{
			get
			{
				return this._chooser.Leader.IsHumanPlayerCharacter;
			}
		}

		// Token: 0x060026EC RID: 9964 RVA: 0x000A26D0 File Offset: 0x000A08D0
		public KingdomElection(KingdomDecision decision)
		{
			this._decision = decision;
			this.Setup();
		}

		// Token: 0x060026ED RID: 9965 RVA: 0x000A26E8 File Offset: 0x000A08E8
		private void Setup()
		{
			MBList<DecisionOutcome> initialCandidates = this._decision.DetermineInitialCandidates().ToMBList<DecisionOutcome>();
			this._possibleOutcomes = this._decision.NarrowDownCandidates(initialCandidates, 3);
			this._supporters = this._decision.DetermineSupporters().ToList<Supporter>();
			this._chooser = this._decision.DetermineChooser();
			this._decision.DetermineSponsors(this._possibleOutcomes);
			this._hasPlayerVoted = false;
			this.IsCancelled = false;
			foreach (DecisionOutcome decisionOutcome in this._possibleOutcomes)
			{
				decisionOutcome.InitialSupport = this.DetermineInitialSupport(decisionOutcome);
			}
			float num = this._possibleOutcomes.Sum((DecisionOutcome x) => x.InitialSupport);
			foreach (DecisionOutcome decisionOutcome2 in this._possibleOutcomes)
			{
				decisionOutcome2.Likelihood = ((num == 0f) ? 0f : (decisionOutcome2.InitialSupport / num));
			}
		}

		// Token: 0x060026EE RID: 9966 RVA: 0x000A2834 File Offset: 0x000A0A34
		public void StartElection()
		{
			this.Setup();
			this.DetermineSupport(this._possibleOutcomes, false);
			this._decision.DetermineSponsors(this._possibleOutcomes);
			this.UpdateSupport(this._possibleOutcomes);
			if (this._decision.ShouldBeCancelled())
			{
				Debug.Print("SELIM_DEBUG - " + this._decision.GetSupportTitle() + " has been cancelled", 0, Debug.DebugColor.White, 17592186044416UL);
				this.IsCancelled = true;
				bool flag;
				if (!this._decision.DetermineChooser().Leader.IsHumanPlayerCharacter)
				{
					flag = this._decision.DetermineSupporters().Any((Supporter x) => x.IsPlayer);
				}
				else
				{
					flag = true;
				}
				bool isPlayerInvolved = flag;
				CampaignEventDispatcher.Instance.OnKingdomDecisionCancelled(this._decision, isPlayerInvolved);
				return;
			}
			if (!this.IsPlayerSupporter || this._ignorePlayerSupport)
			{
				this.ReadyToAiChoose();
				return;
			}
			if (this._decision.IsSingleClanDecision())
			{
				this._chosenOutcome = this._possibleOutcomes.FirstOrDefault((DecisionOutcome t) => t.SponsorClan != null && t.SponsorClan == Clan.PlayerClan);
				Supporter supporter = new Supporter(Clan.PlayerClan);
				supporter.SupportWeight = Supporter.SupportWeights.FullyPush;
				this._chosenOutcome.AddSupport(supporter);
			}
		}

		// Token: 0x060026EF RID: 9967 RVA: 0x000A2981 File Offset: 0x000A0B81
		public static KingdomElection.ElectionOutcomeSupport GetElectionOutcomeSupport(KingdomDecision decision, Clan sponsor)
		{
			KingdomElection kingdomElection = new KingdomElection(decision);
			kingdomElection.SetupResultWithoutPlayerSupport();
			return kingdomElection.GetDecisionOutcomeSupportForSponsor(sponsor);
		}

		// Token: 0x060026F0 RID: 9968 RVA: 0x000A2995 File Offset: 0x000A0B95
		public void SetupResultWithoutPlayerSupport()
		{
			this.DetermineSupport(this._possibleOutcomes, false);
			this._decision.DetermineSponsors(this._possibleOutcomes);
			this.UpdateSupport(this._possibleOutcomes);
			this.DetermineOfficialSupport();
		}

		// Token: 0x060026F1 RID: 9969 RVA: 0x000A29C8 File Offset: 0x000A0BC8
		private float DetermineInitialSupport(DecisionOutcome possibleOutcome)
		{
			float num = 0f;
			foreach (Supporter supporter in this._supporters)
			{
				if (!supporter.IsPlayer)
				{
					num += MathF.Clamp(this._decision.DetermineSupport(supporter.Clan, possibleOutcome), 0f, 100f);
				}
			}
			return num;
		}

		// Token: 0x060026F2 RID: 9970 RVA: 0x000A2A48 File Offset: 0x000A0C48
		public void StartElectionWithoutPlayer()
		{
			this._ignorePlayerSupport = true;
			this.StartElection();
		}

		// Token: 0x060026F3 RID: 9971 RVA: 0x000A2A58 File Offset: 0x000A0C58
		public float GetLikelihoodForSponsor(Clan sponsor)
		{
			foreach (DecisionOutcome decisionOutcome in this._possibleOutcomes)
			{
				if (decisionOutcome.SponsorClan == sponsor)
				{
					return decisionOutcome.Likelihood;
				}
			}
			Debug.FailedAssert("This clan is not a sponsor of any of the outcomes.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Election\\KingdomDecisionMaker.cs", "GetLikelihoodForSponsor", 174);
			return -1f;
		}

		// Token: 0x060026F4 RID: 9972 RVA: 0x000A2AD8 File Offset: 0x000A0CD8
		public float GetWinChanceForSponsor(Clan sponsor)
		{
			foreach (DecisionOutcome decisionOutcome in this._possibleOutcomes)
			{
				if (decisionOutcome.SponsorClan == sponsor)
				{
					return decisionOutcome.WinChance;
				}
			}
			Debug.FailedAssert("This clan is not a sponsor of any of the outcomes.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Election\\KingdomDecisionMaker.cs", "GetWinChanceForSponsor", 189);
			return -1f;
		}

		// Token: 0x060026F5 RID: 9973 RVA: 0x000A2B58 File Offset: 0x000A0D58
		public KingdomElection.ElectionOutcomeSupport GetDecisionOutcomeSupportForSponsor(Clan sponsor)
		{
			float winChanceForSponsor = this.GetWinChanceForSponsor(sponsor);
			if (winChanceForSponsor > 0.75f)
			{
				return KingdomElection.ElectionOutcomeSupport.StrongSupport;
			}
			if (winChanceForSponsor > 0.5f)
			{
				return KingdomElection.ElectionOutcomeSupport.GoodSupport;
			}
			if (winChanceForSponsor > 0.2f)
			{
				return KingdomElection.ElectionOutcomeSupport.SlightSupport;
			}
			return KingdomElection.ElectionOutcomeSupport.LowSupport;
		}

		// Token: 0x060026F6 RID: 9974 RVA: 0x000A2B8C File Offset: 0x000A0D8C
		private void DetermineSupport(MBReadOnlyList<DecisionOutcome> possibleOutcomes, bool calculateRelationshipEffect)
		{
			foreach (Supporter supporter in this._supporters)
			{
				if (!supporter.IsPlayer)
				{
					Supporter.SupportWeights supportWeight = Supporter.SupportWeights.StayNeutral;
					DecisionOutcome decisionOutcome = this._decision.DetermineSupportOption(supporter, possibleOutcomes, out supportWeight, calculateRelationshipEffect);
					if (decisionOutcome != null)
					{
						supporter.SupportWeight = supportWeight;
						decisionOutcome.AddSupport(supporter);
					}
				}
			}
		}

		// Token: 0x060026F7 RID: 9975 RVA: 0x000A2C04 File Offset: 0x000A0E04
		private void UpdateSupport(MBReadOnlyList<DecisionOutcome> possibleOutcomes)
		{
			foreach (DecisionOutcome decisionOutcome in this._possibleOutcomes)
			{
				foreach (Supporter supporter in new List<Supporter>(decisionOutcome.SupporterList))
				{
					decisionOutcome.ResetSupport(supporter);
				}
			}
			this.DetermineSupport(possibleOutcomes, true);
		}

		// Token: 0x060026F8 RID: 9976 RVA: 0x000A2CA0 File Offset: 0x000A0EA0
		private void ReadyToAiChoose()
		{
			this._chosenOutcome = this.GetAiChoice(this._possibleOutcomes);
			if (this._decision.OnShowDecision())
			{
				this.ApplyChosenOutcome();
			}
		}

		// Token: 0x060026F9 RID: 9977 RVA: 0x000A2CC8 File Offset: 0x000A0EC8
		private void ApplyChosenOutcome()
		{
			this._decision.ApplyChosenOutcome(this._chosenOutcome);
			this._decision.SupportStatusOfFinalDecision = this.GetSupportStatusOfDecisionOutcome(this._chosenOutcome);
			this.HandleInfluenceCosts();
			this.ApplySecondaryEffects(this._possibleOutcomes, this._chosenOutcome);
			for (int i = 0; i < this._possibleOutcomes.Count; i++)
			{
				if (this._possibleOutcomes[i].SponsorClan != null)
				{
					foreach (Supporter supporter in this._possibleOutcomes[i].SupporterList)
					{
						if (supporter.Clan.Leader != this._possibleOutcomes[i].SponsorClan.Leader && supporter.Clan == Clan.PlayerClan)
						{
							int num = this.GetRelationChangeWithSponsor(supporter.Clan.Leader, supporter.SupportWeight, false);
							if (num != 0)
							{
								num *= ((this._possibleOutcomes.Count > 2) ? 2 : 1);
								ChangeRelationAction.ApplyRelationChangeBetweenHeroes(supporter.Clan.Leader, this._possibleOutcomes[i].SponsorClan.Leader, num, true);
							}
						}
					}
					for (int j = 0; j < this._possibleOutcomes.Count; j++)
					{
						if (i != j)
						{
							foreach (Supporter supporter2 in this._possibleOutcomes[j].SupporterList)
							{
								if (supporter2.Clan.Leader != this._possibleOutcomes[i].SponsorClan.Leader && supporter2.Clan == Clan.PlayerClan)
								{
									int relationChangeWithSponsor = this.GetRelationChangeWithSponsor(supporter2.Clan.Leader, supporter2.SupportWeight, true);
									if (relationChangeWithSponsor != 0)
									{
										ChangeRelationAction.ApplyRelationChangeBetweenHeroes(supporter2.Clan.Leader, this._possibleOutcomes[i].SponsorClan.Leader, relationChangeWithSponsor, true);
									}
								}
							}
						}
					}
				}
			}
			this._decision.Kingdom.RemoveDecision(this._decision);
			this._decision.Kingdom.OnKingdomDecisionConcluded();
			CampaignEventDispatcher.Instance.OnKingdomDecisionConcluded(this._decision, this._chosenOutcome, this.IsPlayerChooser || this._hasPlayerVoted);
		}

		// Token: 0x060026FA RID: 9978 RVA: 0x000A2F60 File Offset: 0x000A1160
		public int GetRelationChangeWithSponsor(Hero opposerOrSupporter, Supporter.SupportWeights supportWeight, bool isOpposingSides)
		{
			int num = 0;
			Clan clan = opposerOrSupporter.Clan;
			if (supportWeight == Supporter.SupportWeights.FullyPush)
			{
				num = (int)((float)this._decision.GetInfluenceCostOfSupport(clan, Supporter.SupportWeights.FullyPush) / 20f);
			}
			else if (supportWeight == Supporter.SupportWeights.StronglyFavor)
			{
				num = (int)((float)this._decision.GetInfluenceCostOfSupport(clan, Supporter.SupportWeights.StronglyFavor) / 20f);
			}
			else if (supportWeight == Supporter.SupportWeights.SlightlyFavor)
			{
				num = (int)((float)this._decision.GetInfluenceCostOfSupport(clan, Supporter.SupportWeights.SlightlyFavor) / 20f);
			}
			int num2 = isOpposingSides ? (num * -1) : (num * 2);
			if (isOpposingSides && opposerOrSupporter.Culture.HasFeat(DefaultCulturalFeats.SturgianDecisionPenaltyFeat))
			{
				num2 += (int)((float)num2 * DefaultCulturalFeats.SturgianDecisionPenaltyFeat.EffectBonus);
			}
			return num2;
		}

		// Token: 0x060026FB RID: 9979 RVA: 0x000A2FFC File Offset: 0x000A11FC
		private void HandleInfluenceCosts()
		{
			DecisionOutcome decisionOutcome = this._possibleOutcomes[0];
			foreach (DecisionOutcome decisionOutcome2 in this._possibleOutcomes)
			{
				if (decisionOutcome2.TotalSupportPoints > decisionOutcome.TotalSupportPoints)
				{
					decisionOutcome = decisionOutcome2;
				}
				for (int i = 0; i < decisionOutcome2.SupporterList.Count; i++)
				{
					Clan clan = decisionOutcome2.SupporterList[i].Clan;
					int num = this._decision.GetInfluenceCost(decisionOutcome2, clan, decisionOutcome2.SupporterList[i].SupportWeight);
					if (this._supporters.Count == 1)
					{
						num = 0;
					}
					if (this._chosenOutcome != decisionOutcome2)
					{
						num /= 2;
					}
					if (decisionOutcome2 == this._chosenOutcome || !clan.Leader.GetPerkValue(DefaultPerks.Charm.GoodNatured))
					{
						ChangeClanInfluenceAction.Apply(clan, (float)(-(float)num));
					}
				}
			}
			if (this._chosenOutcome != decisionOutcome)
			{
				int influenceRequiredToOverrideKingdomDecision = Campaign.Current.Models.ClanPoliticsModel.GetInfluenceRequiredToOverrideKingdomDecision(decisionOutcome, this._chosenOutcome, this._decision);
				ChangeClanInfluenceAction.Apply(this._chooser, (float)(-(float)influenceRequiredToOverrideKingdomDecision));
			}
		}

		// Token: 0x060026FC RID: 9980 RVA: 0x000A3140 File Offset: 0x000A1340
		private void ApplySecondaryEffects(MBReadOnlyList<DecisionOutcome> possibleOutcomes, DecisionOutcome chosenOutcome)
		{
			this._decision.ApplySecondaryEffects(possibleOutcomes, chosenOutcome);
		}

		// Token: 0x060026FD RID: 9981 RVA: 0x000A314F File Offset: 0x000A134F
		private int GetInfluenceRequiredToOverrideDecision(DecisionOutcome popularOutcome, DecisionOutcome overridingOutcome)
		{
			return Campaign.Current.Models.ClanPoliticsModel.GetInfluenceRequiredToOverrideKingdomDecision(popularOutcome, overridingOutcome, this._decision);
		}

		// Token: 0x060026FE RID: 9982 RVA: 0x000A3170 File Offset: 0x000A1370
		private DecisionOutcome GetAiChoice(MBReadOnlyList<DecisionOutcome> possibleOutcomes)
		{
			this.DetermineOfficialSupport();
			DecisionOutcome decisionOutcome = possibleOutcomes.MaxBy((DecisionOutcome t) => t.TotalSupportPoints);
			DecisionOutcome result = decisionOutcome;
			if (this._decision.IsKingsVoteAllowed)
			{
				DecisionOutcome decisionOutcome2 = possibleOutcomes.MaxBy((DecisionOutcome t) => this._decision.DetermineSupport(this._chooser, t));
				float num = this._decision.DetermineSupport(this._chooser, decisionOutcome2);
				float num2 = this._decision.DetermineSupport(this._chooser, decisionOutcome);
				float num3 = num - num2;
				num3 = MathF.Min(num3, this._chooser.Influence);
				if (num3 > 10f)
				{
					float num4 = 300f + (float)this.GetInfluenceRequiredToOverrideDecision(decisionOutcome, decisionOutcome2);
					if (num3 > num4)
					{
						float num5 = num4 / num3;
						if (MBRandom.RandomFloat > num5)
						{
							result = decisionOutcome2;
						}
					}
				}
			}
			return result;
		}

		// Token: 0x060026FF RID: 9983 RVA: 0x000A3240 File Offset: 0x000A1440
		public TextObject GetChosenOutcomeText()
		{
			return this._decision.GetChosenOutcomeText(this._chosenOutcome, this._decision.SupportStatusOfFinalDecision, false);
		}

		// Token: 0x06002700 RID: 9984 RVA: 0x000A3260 File Offset: 0x000A1460
		private KingdomDecision.SupportStatus GetSupportStatusOfDecisionOutcome(DecisionOutcome chosenOutcome)
		{
			KingdomDecision.SupportStatus result = KingdomDecision.SupportStatus.Equal;
			float num = chosenOutcome.WinChance * 100f;
			int num2 = 50;
			if (num > (float)(num2 + 5))
			{
				result = KingdomDecision.SupportStatus.Majority;
			}
			else if (num < (float)(num2 - 5))
			{
				result = KingdomDecision.SupportStatus.Minority;
			}
			return result;
		}

		// Token: 0x06002701 RID: 9985 RVA: 0x000A3294 File Offset: 0x000A1494
		public void DetermineOfficialSupport()
		{
			float num = 0.001f;
			foreach (DecisionOutcome decisionOutcome in this._possibleOutcomes)
			{
				float num2 = 0f;
				foreach (Supporter supporter in decisionOutcome.SupporterList)
				{
					num2 += (float)MathF.Max(0, supporter.SupportWeight - Supporter.SupportWeights.StayNeutral);
				}
				decisionOutcome.TotalSupportPoints = num2;
				num += decisionOutcome.TotalSupportPoints;
			}
			foreach (DecisionOutcome decisionOutcome2 in this._possibleOutcomes)
			{
				decisionOutcome2.TotalSupportPoints /= num;
			}
		}

		// Token: 0x06002702 RID: 9986 RVA: 0x000A3394 File Offset: 0x000A1594
		public float GetWinChanceWithPlayerSupport(DecisionOutcome supportedOutcome, Supporter.SupportWeights supportWeight)
		{
			if (this._possibleOutcomes.Contains(supportedOutcome))
			{
				float num;
				if (supportedOutcome.WinChance != 0f)
				{
					num = supportedOutcome.TotalSupportPoints / supportedOutcome.WinChance;
				}
				else
				{
					num = this._possibleOutcomes.First((DecisionOutcome x) => x != supportedOutcome).TotalSupportPoints;
				}
				int num2 = MathF.Max(0, supportWeight - Supporter.SupportWeights.StayNeutral);
				supportedOutcome.TotalSupportPoints += (float)num2;
				num += (float)num2;
				return supportedOutcome.TotalSupportPoints / num;
			}
			return 0f;
		}

		// Token: 0x06002703 RID: 9987 RVA: 0x000A3449 File Offset: 0x000A1649
		public int GetInfluenceCostOfOutcome(DecisionOutcome outcome, Clan supporter, Supporter.SupportWeights weight)
		{
			return this._decision.GetInfluenceCostOfSupport(supporter, weight);
		}

		// Token: 0x06002704 RID: 9988 RVA: 0x000A3458 File Offset: 0x000A1658
		public TextObject GetSecondaryEffects()
		{
			return this._decision.GetSecondaryEffects();
		}

		// Token: 0x06002705 RID: 9989 RVA: 0x000A3468 File Offset: 0x000A1668
		public void OnPlayerSupport(DecisionOutcome decisionOutcome, Supporter.SupportWeights supportWeight)
		{
			if (!this.IsPlayerChooser)
			{
				foreach (DecisionOutcome decisionOutcome2 in this._possibleOutcomes)
				{
					decisionOutcome2.ResetSupport(this.PlayerAsSupporter);
				}
				this._hasPlayerVoted = true;
				if (decisionOutcome != null)
				{
					this.PlayerAsSupporter.SupportWeight = supportWeight;
					decisionOutcome.AddSupport(this.PlayerAsSupporter);
					return;
				}
			}
			else
			{
				this._chosenOutcome = decisionOutcome;
			}
		}

		// Token: 0x06002706 RID: 9990 RVA: 0x000A34F0 File Offset: 0x000A16F0
		public void OnPlayerAbstainedAsRuler()
		{
			this._chosenOutcome = this.GetAiChoice(this._possibleOutcomes);
		}

		// Token: 0x06002707 RID: 9991 RVA: 0x000A3504 File Offset: 0x000A1704
		public void ApplySelection()
		{
			if (!this.IsCancelled)
			{
				if (this._chooser != Clan.PlayerClan)
				{
					this.ReadyToAiChoose();
					return;
				}
				this.ApplyChosenOutcome();
			}
		}

		// Token: 0x06002708 RID: 9992 RVA: 0x000A3528 File Offset: 0x000A1728
		public MBList<DecisionOutcome> GetSortedDecisionOutcomes()
		{
			return this._decision.SortDecisionOutcomes(this._possibleOutcomes);
		}

		// Token: 0x06002709 RID: 9993 RVA: 0x000A353B File Offset: 0x000A173B
		public TextObject GetGeneralTitle()
		{
			return this._decision.GetGeneralTitle();
		}

		// Token: 0x0600270A RID: 9994 RVA: 0x000A3548 File Offset: 0x000A1748
		public TextObject GetTitle()
		{
			if (this.IsPlayerChooser)
			{
				return this._decision.GetChooseTitle();
			}
			return this._decision.GetSupportTitle();
		}

		// Token: 0x0600270B RID: 9995 RVA: 0x000A3569 File Offset: 0x000A1769
		public TextObject GetDescription()
		{
			if (this.IsPlayerChooser)
			{
				return this._decision.GetChooseDescription();
			}
			return this._decision.GetSupportDescription();
		}

		// Token: 0x04000B73 RID: 2931
		private const float StrongSupportThreshold = 0.75f;

		// Token: 0x04000B74 RID: 2932
		private const float GoodSupportThreshold = 0.5f;

		// Token: 0x04000B75 RID: 2933
		private const float SlightSupportThreshold = 0.2f;

		// Token: 0x04000B76 RID: 2934
		[SaveableField(0)]
		private readonly KingdomDecision _decision;

		// Token: 0x04000B77 RID: 2935
		private MBList<DecisionOutcome> _possibleOutcomes;

		// Token: 0x04000B78 RID: 2936
		[SaveableField(2)]
		private List<Supporter> _supporters;

		// Token: 0x04000B79 RID: 2937
		[SaveableField(3)]
		private Clan _chooser;

		// Token: 0x04000B7A RID: 2938
		[SaveableField(4)]
		private DecisionOutcome _chosenOutcome;

		// Token: 0x04000B7B RID: 2939
		[SaveableField(5)]
		private bool _ignorePlayerSupport;

		// Token: 0x04000B7C RID: 2940
		[SaveableField(6)]
		private bool _hasPlayerVoted;

		// Token: 0x0200067A RID: 1658
		public enum ElectionOutcomeSupport
		{
			// Token: 0x04001A41 RID: 6721
			LowSupport,
			// Token: 0x04001A42 RID: 6722
			SlightSupport,
			// Token: 0x04001A43 RID: 6723
			GoodSupport,
			// Token: 0x04001A44 RID: 6724
			StrongSupport
		}
	}
}
