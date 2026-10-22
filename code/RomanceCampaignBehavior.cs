using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x0200043E RID: 1086
	public class RomanceCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x17000E4B RID: 3659
		// (get) Token: 0x060045A2 RID: 17826 RVA: 0x0015802D File Offset: 0x0015622D
		private CampaignTime RomanceCourtshipAttemptCooldown
		{
			get
			{
				return CampaignTime.DaysFromNow(-1f);
			}
		}

		// Token: 0x060045A3 RID: 17827 RVA: 0x00158039 File Offset: 0x00156239
		public RomanceCampaignBehavior()
		{
			this._previousRomancePersuasionAttempts = new List<PersuasionAttempt>();
		}

		// Token: 0x060045A4 RID: 17828 RVA: 0x0015804C File Offset: 0x0015624C
		public override void RegisterEvents()
		{
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));
			CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, new Action<Clan>(this.DailyTickClan));
		}

		// Token: 0x060045A5 RID: 17829 RVA: 0x0015809E File Offset: 0x0015629E
		private void DailyTickClan(Clan clan)
		{
			this.CheckNpcMarriages(clan);
		}

		// Token: 0x060045A6 RID: 17830 RVA: 0x001580A8 File Offset: 0x001562A8
		private void CheckNpcMarriages(Clan consideringClan)
		{
			if (this.IsClanSuitableForNpcMarriage(consideringClan))
			{
				MarriageModel marriageModel = Campaign.Current.Models.MarriageModel;
				foreach (Hero hero in consideringClan.AliveLords.ToList<Hero>())
				{
					if (hero.CanMarry())
					{
						Clan clan = Clan.All[MBRandom.RandomInt(Clan.All.Count)];
						if (this.IsClanSuitableForNpcMarriage(clan) && marriageModel.ShouldNpcMarriageBetweenClansBeAllowed(consideringClan, clan))
						{
							foreach (Hero hero2 in clan.AliveLords.ToList<Hero>())
							{
								float num = marriageModel.NpcCoupleMarriageChance(hero, hero2);
								if (num > 0f && MBRandom.RandomFloat < num)
								{
									bool flag = false;
									foreach (Romance.RomanticState romanticState in Romance.RomanticStateList)
									{
										if (romanticState.Level >= Romance.RomanceLevelEnum.MatchMadeByFamily && (romanticState.Person1 == hero || romanticState.Person2 == hero || romanticState.Person1 == hero2 || romanticState.Person2 == hero2))
										{
											flag = true;
											break;
										}
									}
									if (!flag)
									{
										MarriageAction.Apply(hero, hero2, true);
										return;
									}
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x060045A7 RID: 17831 RVA: 0x0015826C File Offset: 0x0015646C
		private bool IsClanSuitableForNpcMarriage(Clan clan)
		{
			return clan != Clan.PlayerClan && Campaign.Current.Models.MarriageModel.IsClanSuitableForMarriage(clan);
		}

		// Token: 0x060045A8 RID: 17832 RVA: 0x0015828D File Offset: 0x0015648D
		public override void SyncData(IDataStore dataStore)
		{
			dataStore.SyncData<List<PersuasionAttempt>>("previousRomancePersuasionAttempts", ref this._previousRomancePersuasionAttempts);
		}

		// Token: 0x060045A9 RID: 17833 RVA: 0x001582A1 File Offset: 0x001564A1
		public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
		{
			this.AddDialogs(campaignGameStarter);
		}

		// Token: 0x060045AA RID: 17834 RVA: 0x001582AC File Offset: 0x001564AC
		private PersuasionTask GetCurrentPersuasionTask()
		{
			foreach (PersuasionTask persuasionTask in this._allReservations)
			{
				if (!persuasionTask.Options.All((PersuasionOptionArgs x) => x.IsBlocked))
				{
					return persuasionTask;
				}
			}
			return this._allReservations[this._allReservations.Count - 1];
		}

		// Token: 0x060045AB RID: 17835 RVA: 0x00158348 File Offset: 0x00156548
		private void RemoveUnneededPersuasionAttempts()
		{
			foreach (PersuasionAttempt persuasionAttempt in this._previousRomancePersuasionAttempts.ToList<PersuasionAttempt>())
			{
				if (persuasionAttempt.PersuadedHero.Spouse != null || !persuasionAttempt.PersuadedHero.IsAlive)
				{
					this._previousRomancePersuasionAttempts.Remove(persuasionAttempt);
				}
			}
		}

		// Token: 0x060045AC RID: 17836 RVA: 0x001583C0 File Offset: 0x001565C0
		protected void AddDialogs(CampaignGameStarter starter)
		{
			starter.AddPlayerLine("lord_special_request_flirt", "lord_talk_speak_diplomacy_2", "lord_start_courtship_response", "{=!}{FLIRTATION_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_player_can_open_courtship_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_player_opens_courtship_on_consequence), 100, null, null);
			starter.AddPlayerLine("hero_romance_task_pt1", "hero_main_options", "hero_courtship_task_1_begin_reservations", "{=bHZyublA}So... I'm glad to have the chance to spend some time together.", new ConversationSentence.OnConditionDelegate(this.conversation_romance_at_stage_1_discussions_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_start_courtship_persuasion_pt1_on_consequence), 100, null, null);
			starter.AddPlayerLine("hero_romance_task_pt2", "hero_main_options", "hero_courtship_task_2_begin_reservations", "{=nGsQeTll}Perhaps we should discuss a future together...", new ConversationSentence.OnConditionDelegate(this.conversation_romance_at_stage_2_discussions_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_continue_courtship_stage_2_on_consequence), 100, null, null);
			starter.AddPlayerLine("hero_romance_task_pt3a", "hero_main_options", "hero_courtship_final_barter", "{=2aW6NC3Q}Let us discuss the final terms of our marriage.", new ConversationSentence.OnConditionDelegate(this.conversation_finalize_courtship_for_hero_on_condition), null, 100, null, null);
			starter.AddPlayerLine("hero_romance_task_pt3b", "hero_main_options", "hero_courtship_final_barter", "{=jd4qUGEA}I wish to discuss the final terms of my marriage with {COURTSHIP_PARTNER}.", new ConversationSentence.OnConditionDelegate(this.conversation_finalize_courtship_for_other_on_condition), null, 100, null, null);
			starter.AddPlayerLine("hero_romance_task_blocked", "hero_main_options", "hero_courtship_task_blocked", "{=OaRB1oVI}So... Earlier, we had discussed the possibility of marriage.", new ConversationSentence.OnConditionDelegate(this.conversation_romance_blocked_on_condition), null, 100, null, null);
			starter.AddDialogLine("hero_courtship_persuasion_fail", "hero_courtship_task_blocked", "lord_pretalk", "{=!}{ROMANCE_BLOCKED_REASON}", null, null, 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_fail_2", "hero_courtship_task_1_begin_reservations", "lord_pretalk", "{=!}{FAILED_PERSUASION_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_lord_player_has_failed_in_courtship_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_fail_courtship_on_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_start", "hero_courtship_task_1_begin_reservations", "hero_courtship_task_1_next_reservation", "{=bW3ygxro}Yes, it's good to have a chance to get to know each other.", null, null, 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_fail_3", "hero_courtship_task_1_next_reservation", "lord_pretalk", "{=!}{FAILED_PERSUASION_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_lord_player_has_failed_in_courtship_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_fail_courtship_on_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_attempt", "hero_courtship_task_1_next_reservation", "hero_courtship_argument", "{=!}{PERSUASION_TASK_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_check_if_unmet_reservation_on_condition), null, 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_success", "hero_courtship_task_1_next_reservation", "lord_conclude_courtship_stage_1", "{=YcdQ1MWq}Well.. It seems we have a fair amount in common.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_courtship_stage_1_success_on_consequence), 100, null);
			starter.AddDialogLine("persuasion_leave_faction_npc_result_success_2_1", "lord_conclude_courtship_stage_1", "close_window", "{=SP7I61x2}Perhaps we can talk more when we meet again.", null, new ConversationSentence.OnConsequenceDelegate(this.courtship_conversation_leave_on_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_2_start", "hero_courtship_task_2_begin_reservations", "hero_courtship_task_2_next_reservation", "{=VNFKqpyV}Yes, well, I've been thinking about that.", null, null, 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_2_fail", "hero_courtship_task_2_next_reservation", "lord_pretalk", "{=!}{FAILED_PERSUASION_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_lord_player_has_failed_in_courtship_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_fail_courtship_on_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_2_attempt", "hero_courtship_task_2_next_reservation", "hero_courtship_argument", "{=!}{PERSUASION_TASK_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_check_if_unmet_reservation_on_condition), null, 100, null);
			starter.AddDialogLine("hero_courtship_persuasion_2_success", "hero_courtship_task_2_next_reservation", "lord_conclude_courtship_stage_2", "{=xwS10c1b}Yes... I think I would be honored to accept your proposal.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_courtship_stage_2_success_on_consequence), 100, null);
			starter.AddDialogLine("persuasion_leave_faction_npc_result_success_2_2", "lord_conclude_courtship_stage_2", "close_window", "{=pvnY5Jwv}{CLAN_LEADER.LINK}, as head of our family, needs to give {?CLAN_LEADER.GENDER}her{?}his{\\?} blessing. There are usually financial arrangements to be made.", new ConversationSentence.OnConditionDelegate(this.courtship_hero_not_clan_leader_on_condition), new ConversationSentence.OnConsequenceDelegate(this.courtship_conversation_leave_on_consequence), 100, null);
			starter.AddDialogLine("persuasion_leave_faction_npc_result_success_2_3", "lord_conclude_courtship_stage_2", "close_window", "{=nnutwjOZ}We'll need to work out the details of how we divide our property.", null, new ConversationSentence.OnConsequenceDelegate(this.courtship_conversation_leave_on_consequence), 100, null);
			string id = "hero_courtship_argument_1";
			string inputToken = "hero_courtship_argument";
			string outputToken = "hero_courtship_reaction";
			string text = "{=!}{ROMANCE_PERSUADE_ATTEMPT_1}";
			ConversationSentence.OnConditionDelegate conditionDelegate = new ConversationSentence.OnConditionDelegate(this.conversation_courtship_persuasion_option_1_on_condition);
			ConversationSentence.OnConsequenceDelegate consequenceDelegate = new ConversationSentence.OnConsequenceDelegate(this.conversation_romance_1_persuade_option_on_consequence);
			int priority = 100;
			ConversationSentence.OnPersuasionOptionDelegate persuasionOptionDelegate = new ConversationSentence.OnPersuasionOptionDelegate(this.SetupCourtshipPersuasionOption1);
			starter.AddPlayerLine(id, inputToken, outputToken, text, conditionDelegate, consequenceDelegate, priority, new ConversationSentence.OnClickableConditionDelegate(this.RomancePersuasionOption1ClickableOnCondition1), persuasionOptionDelegate);
			string id2 = "hero_courtship_argument_2";
			string inputToken2 = "hero_courtship_argument";
			string outputToken2 = "hero_courtship_reaction";
			string text2 = "{=!}{ROMANCE_PERSUADE_ATTEMPT_2}";
			ConversationSentence.OnConditionDelegate conditionDelegate2 = new ConversationSentence.OnConditionDelegate(this.conversation_courtship_persuasion_option_2_on_condition);
			ConversationSentence.OnConsequenceDelegate consequenceDelegate2 = new ConversationSentence.OnConsequenceDelegate(this.conversation_romance_2_persuade_option_on_consequence);
			int priority2 = 100;
			persuasionOptionDelegate = new ConversationSentence.OnPersuasionOptionDelegate(this.SetupCourtshipPersuasionOption2);
			starter.AddPlayerLine(id2, inputToken2, outputToken2, text2, conditionDelegate2, consequenceDelegate2, priority2, new ConversationSentence.OnClickableConditionDelegate(this.RomancePersuasionOption2ClickableOnCondition2), persuasionOptionDelegate);
			string id3 = "hero_courtship_argument_3";
			string inputToken3 = "hero_courtship_argument";
			string outputToken3 = "hero_courtship_reaction";
			string text3 = "{=!}{ROMANCE_PERSUADE_ATTEMPT_3}";
			ConversationSentence.OnConditionDelegate conditionDelegate3 = new ConversationSentence.OnConditionDelegate(this.conversation_courtship_persuasion_option_3_on_condition);
			ConversationSentence.OnConsequenceDelegate consequenceDelegate3 = new ConversationSentence.OnConsequenceDelegate(this.conversation_romance_3_persuade_option_on_consequence);
			int priority3 = 100;
			persuasionOptionDelegate = new ConversationSentence.OnPersuasionOptionDelegate(this.SetupCourtshipPersuasionOption3);
			starter.AddPlayerLine(id3, inputToken3, outputToken3, text3, conditionDelegate3, consequenceDelegate3, priority3, new ConversationSentence.OnClickableConditionDelegate(this.RomancePersuasionOption3ClickableOnCondition3), persuasionOptionDelegate);
			string id4 = "hero_courtship_argument_4";
			string inputToken4 = "hero_courtship_argument";
			string outputToken4 = "hero_courtship_reaction";
			string text4 = "{=!}{ROMANCE_PERSUADE_ATTEMPT_4}";
			ConversationSentence.OnConditionDelegate conditionDelegate4 = new ConversationSentence.OnConditionDelegate(this.conversation_courtship_persuasion_option_4_on_condition);
			ConversationSentence.OnConsequenceDelegate consequenceDelegate4 = new ConversationSentence.OnConsequenceDelegate(this.conversation_romance_4_persuade_option_on_consequence);
			int priority4 = 100;
			persuasionOptionDelegate = new ConversationSentence.OnPersuasionOptionDelegate(this.SetupCourtshipPersuasionOption4);
			starter.AddPlayerLine(id4, inputToken4, outputToken4, text4, conditionDelegate4, consequenceDelegate4, priority4, new ConversationSentence.OnClickableConditionDelegate(this.RomancePersuasionOption4ClickableOnCondition4), persuasionOptionDelegate);
			starter.AddPlayerLine("lord_ask_recruit_argument_no_answer_2", "hero_courtship_argument", "lord_pretalk", "{=!}{TRY_HARDER_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_courtship_try_later_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_fail_courtship_on_consequence), 100, null, null);
			starter.AddDialogLine("lord_ask_recruit_argument_reaction_1", "hero_courtship_reaction", "hero_courtship_task_1_next_reservation", "{=!}{PERSUASION_REACTION}", new ConversationSentence.OnConditionDelegate(this.conversation_courtship_reaction_stage_1_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_lord_persuade_option_reaction_on_consequence), 100, null);
			starter.AddDialogLine("lord_ask_recruit_argument_reaction_2", "hero_courtship_reaction", "hero_courtship_task_2_next_reservation", "{=!}{PERSUASION_REACTION}", new ConversationSentence.OnConditionDelegate(this.conversation_courtship_reaction_stage_2_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_lord_persuade_option_reaction_on_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_end_conversation", "hero_courtship_end_conversation", "close_window", "{=Mk9k8Sec}As always, it is a delight to speak to you.", null, new ConversationSentence.OnConsequenceDelegate(this.courtship_conversation_leave_on_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_final_barter", "hero_courtship_final_barter", "hero_courtship_final_barter_setup", "{=0UPds9x3}Very well, then...", null, null, 100, null);
			starter.AddDialogLine("hero_courtship_final_barter_setup", "hero_courtship_final_barter_setup", "hero_courtship_final_barter_conclusion", "{=qqzJTfo0}Barter line goes here.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_finalize_marriage_barter_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_final_barter_setup_2", "hero_courtship_final_barter_conclusion", "close_window", "{=FGVzQUao}Congratulations, and may the Heavens bless you.", new ConversationSentence.OnConditionDelegate(this.conversation_marriage_barter_successful_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_marriage_barter_successful_on_consequence), 100, null);
			starter.AddDialogLine("hero_courtship_final_barter_setup_3", "hero_courtship_final_barter_conclusion", "close_window", "{=iunPaMFv}I guess we should put this aside, for now. But perhaps we can speak again at a later date.", () => !this.conversation_marriage_barter_successful_on_condition(), null, 100, null);
			starter.AddPlayerLine("lord_propose_marriage_conv_general_proposal", "lord_talk_speak_diplomacy_2", "lord_propose_marriage_to_clan_leader", "{=v9tQv4eN}I would like to propose an alliance between our families through marriage.", new ConversationSentence.OnConditionDelegate(this.conversation_discuss_marriage_alliance_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_find_player_relatives_eligible_for_marriage_on_consequence), 120, null, null);
			starter.AddDialogLine("lord_propose_marriage_conv_general_proposal_response", "lord_propose_marriage_to_clan_leader", "lord_propose_marriage_to_clan_leader_options", "{=MhPAHpND}And whose hand are you offering?", null, null, 100, null);
			starter.AddPlayerLine("lord_propose_marriage_conv_general_proposal_2_1", "lord_propose_marriage_to_clan_leader_options", "lord_propose_marriage_to_clan_leader_response", "{=N1Ue4Blt}My own hand.", new ConversationSentence.OnConditionDelegate(this.conversation_player_eligible_for_marriage_with_hero_rltv_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_player_nominates_self_for_marriage_on_consequence), 120, null, null);
			starter.AddRepeatablePlayerLine("lord_propose_marriage_conv_general_proposal_2", "lord_propose_marriage_to_clan_leader_options", "lord_propose_marriage_to_clan_leader_response", "{=QGj8zQIc}The hand of {MARRIAGE_CANDIDATE.NAME}.", "I am thinking of a different person.", "lord_propose_marriage_to_clan_leader", new ConversationSentence.OnConditionDelegate(this.conversation_player_relative_eligible_for_marriage_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_player_nominates_marriage_relative_on_consequence), 100, null);
			starter.AddPlayerLine("lord_propose_marriage_conv_general_proposal_3", "lord_propose_marriage_to_clan_leader_options", "lord_pretalk", "{=D33fIGQe}Never mind.", null, null, 120, null, null);
			starter.AddDialogLine("lord_propose_marriage_to_clan_leader_response", "lord_propose_marriage_to_clan_leader_response", "lord_propose_marriage_to_clan_leader_response_self", "{=DdtrRYEM}Well yes. I was looking for a suitable match.", new ConversationSentence.OnConditionDelegate(this.conversation_propose_clan_leader_for_player_nomination_on_condition), null, 100, null);
			starter.AddPlayerLine("lord_propose_marriage_to_clan_leader_response_yes", "lord_propose_marriage_to_clan_leader_response_self", "lord_start_courtship_response", "{=bx4MiPqN}Yes. I would be honored to be considered.", new ConversationSentence.OnConditionDelegate(this.conversation_player_opens_courtship_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_player_opens_courtship_on_consequence), 100, null, null);
			starter.AddPlayerLine("lord_propose_marriage_to_clan_leader_response_plyr_rltv_yes", "lord_propose_marriage_to_clan_leader_response_self", "lord_propose_marriage_to_clan_leader_confirm", "{=ziA4catk}Very good.", new ConversationSentence.OnConditionDelegate(this.conversation_player_rltv_agrees_on_courtship_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_player_agrees_on_courtship_on_consequence), 100, null, null);
			starter.AddPlayerLine("lord_propose_marriage_to_clan_leader_response_no", "lord_propose_marriage_to_clan_leader_response_self", "lord_pretalk", "{=Zw95lDI3}Hmm.. That might not work out.", null, null, 100, null, null);
			starter.AddDialogLine("lord_propose_marriage_to_clan_leader_response_2", "lord_propose_marriage_to_clan_leader_response", "lord_propose_marriage_to_clan_leader_response_other", "{=!}{ARRANGE_MARRIAGE_LINE}", new ConversationSentence.OnConditionDelegate(this.conversation_propose_spouse_for_player_nomination_on_condition), null, 100, null);
			starter.AddPlayerLine("lord_propose_marriage_to_clan_leader_response_plyr_yes", "lord_propose_marriage_to_clan_leader_response_other", "lord_propose_marriage_to_clan_leader_confirm", "{=ziA4catk}Very good.", new ConversationSentence.OnConditionDelegate(this.conversation_player_rltv_agrees_on_courtship_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_player_agrees_on_courtship_on_consequence), 100, null, null);
			starter.AddPlayerLine("lord_propose_marriage_to_clan_leader_response_plyr_no", "lord_propose_marriage_to_clan_leader_response_other", "lord_pretalk", "{=Zw95lDI3}Hmm.. That might not work out.", null, null, 100, null, null);
			starter.AddDialogLine("lord_propose_marriage_to_clan_leader_response_negative_plyr_response", "lord_propose_marriage_to_clan_leader_response", "lord_pretalk", "{=Zw95lDI3}Hmm.. That might not work out.", null, null, 100, null);
			starter.AddDialogLine("lord_propose_marriage_to_clan_leader_confirm", "lord_propose_marriage_to_clan_leader_confirm", "lord_start", "{=VJEM0IcV}Let's discuss the details then.", null, new ConversationSentence.OnConsequenceDelegate(this.conversation_lord_propose_marriage_to_clan_leader_confirm_consequences), 100, null);
			starter.AddDialogLine("lord_start_courtship_response", "lord_start_courtship_response", "lord_start_courtship_response_player_offer", "{=!}{INITIAL_COURTSHIP_REACTION}", new ConversationSentence.OnConditionDelegate(this.conversation_courtship_initial_reaction_on_condition), null, 100, null);
			starter.AddDialogLine("lord_start_courtship_response_decline", "lord_start_courtship_response", "lord_pretalk", "{=!}{COURTSHIP_DECLINE_REACTION}", new ConversationSentence.OnConditionDelegate(this.conversation_courtship_decline_reaction_to_player_on_condition), null, 100, null);
			starter.AddPlayerLine("lord_start_courtship_response_player_offer", "lord_start_courtship_response_player_offer", "lord_start_courtship_response_2", "{=cKtJBdPD}I wish to offer my hand in marriage.", new ConversationSentence.OnConditionDelegate(this.conversation_player_eligible_for_marriage_with_conversation_hero_on_condition), null, 120, null, null);
			starter.AddPlayerLine("lord_start_courtship_response_player_offer_2", "lord_start_courtship_response_player_offer", "lord_start_courtship_response_2", "{=gnXoIChw}Perhaps you and I...", new ConversationSentence.OnConditionDelegate(this.conversation_player_eligible_for_marriage_with_conversation_hero_on_condition), null, 120, null, null);
			starter.AddPlayerLine("lord_start_courtship_response_player_offer_nevermind", "lord_start_courtship_response_player_offer", "lord_pretalk", "{=D33fIGQe}Never mind.", null, null, 120, null, null);
			starter.AddDialogLine("lord_start_courtship_response_2", "lord_start_courtship_response_2", "lord_start_courtship_response_3", "{=!}{INITIAL_COURTSHIP_REACTION_TO_PLAYER}", new ConversationSentence.OnConditionDelegate(this.conversation_courtship_reaction_to_player_on_condition), null, 100, null);
			starter.AddDialogLine("lord_start_courtship_response_3", "lord_start_courtship_response_3", "close_window", "{=YHZsHohq}We meet from time to time, as is the custom, to see if we are right for each other. I hope to see you again soon.", null, new ConversationSentence.OnConsequenceDelegate(this.courtship_conversation_leave_on_consequence), 100, null);
			starter.AddDialogLine("lord_propose_marriage_conv_general_proposal_response_2", "lord_propose_general_proposal_response", "lord_propose_marriage_options", "{=k1hyviBO}Tell me, what is on your mind?", null, null, 100, null);
			starter.AddPlayerLine("lord_propose_marriage_conv_nevermind", "lord_propose_marriage_options", "lord_pretalk", "{=D33fIGQe}Never mind.", null, null, 100, null, null);
			starter.AddPlayerLine("lord_propose_marriage_conv_nevermind_2", "lord_propose_marry_our_children_options", "lord_pretalk", "{=D33fIGQe}Never mind.", null, null, 100, null, null);
			starter.AddPlayerLine("lord_propose_marriage_conv_nevermind_3", "lord_propose_marry_one_of_your_kind_options", "lord_pretalk", "{=D33fIGQe}Never mind.", null, null, 100, null, null);
		}

		// Token: 0x060045AD RID: 17837 RVA: 0x00158E08 File Offset: 0x00157008
		private bool courtship_hero_not_clan_leader_on_condition()
		{
			Hero leader = Hero.OneToOneConversationHero.Clan.Leader;
			if (leader == Hero.OneToOneConversationHero)
			{
				return false;
			}
			StringHelpers.SetCharacterProperties("CLAN_LEADER", leader.CharacterObject, null, false);
			return true;
		}

		// Token: 0x060045AE RID: 17838 RVA: 0x00158E43 File Offset: 0x00157043
		private void courtship_conversation_leave_on_consequence()
		{
			if (PlayerEncounter.Current != null)
			{
				PlayerEncounter.LeaveEncounter = true;
			}
		}

		// Token: 0x060045AF RID: 17839 RVA: 0x00158E54 File Offset: 0x00157054
		private void conversation_finalize_marriage_barter_consequence()
		{
			Hero heroBeingProposedTo = Hero.OneToOneConversationHero;
			foreach (Hero hero in Hero.OneToOneConversationHero.Clan.AliveLords)
			{
				if (Romance.GetRomanticLevel(Hero.MainHero, hero) == Romance.RomanceLevelEnum.CoupleAgreedOnMarriage)
				{
					heroBeingProposedTo = hero;
					break;
				}
			}
			MarriageBarterable marriageBarterable = new MarriageBarterable(Hero.MainHero, PartyBase.MainParty, heroBeingProposedTo, Hero.MainHero);
			BarterManager instance = BarterManager.Instance;
			Hero mainHero = Hero.MainHero;
			Hero oneToOneConversationHero = Hero.OneToOneConversationHero;
			PartyBase mainParty = PartyBase.MainParty;
			MobileParty partyBelongedTo = Hero.OneToOneConversationHero.PartyBelongedTo;
			instance.StartBarterOffer(mainHero, oneToOneConversationHero, mainParty, (partyBelongedTo != null) ? partyBelongedTo.Party : null, null, (Barterable barterable, BarterData _args, object obj) => BarterManager.Instance.InitializeMarriageBarterContext(barterable, _args, new Tuple<Hero, Hero>(heroBeingProposedTo, Hero.MainHero)), (int)Romance.GetRomanticState(Hero.MainHero, heroBeingProposedTo).ScoreFromPersuasion, false, new Barterable[]
			{
				marriageBarterable
			});
			if (PlayerEncounter.Current != null)
			{
				PlayerEncounter.LeaveEncounter = true;
			}
		}

		// Token: 0x060045B0 RID: 17840 RVA: 0x00158F58 File Offset: 0x00157158
		private void DailyTick()
		{
			foreach (Romance.RomanticState romanticState in Romance.RomanticStateList.ToList<Romance.RomanticState>())
			{
				if (romanticState.Person1.IsDead || romanticState.Person2.IsDead)
				{
					Romance.RomanticStateList.Remove(romanticState);
				}
			}
		}

		// Token: 0x060045B1 RID: 17841 RVA: 0x00158FD0 File Offset: 0x001571D0
		private IEnumerable<RomanceCampaignBehavior.RomanceReservationDescription> GetRomanceReservations(Hero wooed, Hero wooer)
		{
			List<RomanceCampaignBehavior.RomanceReservationDescription> list = new List<RomanceCampaignBehavior.RomanceReservationDescription>();
			bool flag = wooed.GetTraitLevel(DefaultTraits.Honor) + wooed.GetTraitLevel(DefaultTraits.Mercy) > 0;
			bool flag2 = wooed.GetTraitLevel(DefaultTraits.Honor) < 1 && wooed.GetTraitLevel(DefaultTraits.Valor) < 1 && wooed.GetTraitLevel(DefaultTraits.Calculating) < 1;
			bool flag3 = wooed.GetTraitLevel(DefaultTraits.Calculating) - wooed.GetTraitLevel(DefaultTraits.Mercy) >= 0;
			bool flag4 = wooed.GetTraitLevel(DefaultTraits.Valor) - wooed.GetTraitLevel(DefaultTraits.Calculating) > 0 && wooed.GetTraitLevel(DefaultTraits.Mercy) <= 0;
			if (flag)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityINeedSomeoneUpright);
			}
			else if (flag4 && wooed.IsFemale)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.CompatibiliyINeedSomeoneDangerous);
			}
			else
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityNeedSomethingInCommon);
			}
			int attractionValuePercentage = Campaign.Current.Models.RomanceModel.GetAttractionValuePercentage(Hero.OneToOneConversationHero, Hero.MainHero);
			if (attractionValuePercentage > 70)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.AttractionIAmDrawnToYou);
			}
			else if (attractionValuePercentage > 40)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.AttractionYoureGoodEnough);
			}
			else
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.AttractionYoureNotMyType);
			}
			List<Settlement> list2 = (from x in Settlement.All
			where x.OwnerClan == wooer.Clan
			select x).ToList<Settlement>();
			if (flag3 && wooer.IsFemale && list2.Count < 1)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.PropertyHowCanIMarryAnAdventuress);
			}
			else if (flag3 && list2.Count < 3)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.PropertyIWantRealWealth);
			}
			else if (list2.Count < 1)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.PropertyWeNeedToBeComfortable);
			}
			else
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.PropertyYouSeemRichEnough);
			}
			float unmodifiedClanLeaderRelationshipWithPlayer = Hero.OneToOneConversationHero.GetUnmodifiedClanLeaderRelationshipWithPlayer();
			if (unmodifiedClanLeaderRelationshipWithPlayer < -10f)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalHowCanYouBeEnemiesWithOurFamily);
			}
			else if (!flag2 && unmodifiedClanLeaderRelationshipWithPlayer < 10f)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalItWouldBeBestToBefriendOurFamily);
			}
			else if (flag2 && unmodifiedClanLeaderRelationshipWithPlayer < 10f)
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalYouNeedToBeFriendsWithOurFamily);
			}
			else
			{
				list.Add(RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalIAmGladYouAreFriendsWithOurFamily);
			}
			return list;
		}

		// Token: 0x060045B2 RID: 17842 RVA: 0x001591B8 File Offset: 0x001573B8
		private List<PersuasionTask> GetPersuasionTasksForCourtshipStage1(Hero wooed, Hero wooer)
		{
			StringHelpers.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject, null, false);
			List<PersuasionTask> list = new List<PersuasionTask>();
			PersuasionTask persuasionTask = new PersuasionTask(0);
			list.Add(persuasionTask);
			persuasionTask.FinalFailLine = new TextObject("{=dY2PzpIV}I'm not sure how much we have in common..", null);
			persuasionTask.TryLaterLine = new TextObject("{=PoDVgQaz}Well, it would take a bit long to discuss this.", null);
			persuasionTask.SpokenLine = Campaign.Current.ConversationManager.FindMatchingTextOrNull("str_courtship_travel_task", CharacterObject.OneToOneConversationCharacter);
			Tuple<TraitObject, int>[] traitCorrelations = this.GetTraitCorrelations(1, -1, 0, 0, 1);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations);
			PersuasionOptionArgs option = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits, false, new TextObject("{=YNBm3LkC}I feel lucky to live in a time where a valiant warrior can make a name for {?PLAYER.GENDER}herself{?}himself{\\?}.", null), traitCorrelations, false, true, false);
			persuasionTask.AddOptionToTask(option);
			Tuple<TraitObject, int>[] traitCorrelations2 = this.GetTraitCorrelations(1, -1, 0, 0, 1);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits2 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations2);
			PersuasionOptionArgs option2 = new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Valor, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits2, false, new TextObject("{=rtqD9cnu}Yeah, it's a rough world, but there are lots of opportunities to be seized right now if you're not afraid to get your hands a bit dirty.", null), traitCorrelations2, false, true, false);
			persuasionTask.AddOptionToTask(option2);
			Tuple<TraitObject, int>[] traitCorrelations3 = this.GetTraitCorrelations(0, 1, 1, 0, -1);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits3 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations3);
			PersuasionOptionArgs option3 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits3, false, new TextObject("{=rfyalLyY}What can I say? It's a beautiful world, but filled with so much suffering.", null), traitCorrelations3, false, true, false);
			persuasionTask.AddOptionToTask(option3);
			Tuple<TraitObject, int>[] traitCorrelations4 = this.GetTraitCorrelations(-1, 0, -1, -1, 0);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits4 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations4);
			PersuasionOptionArgs option4 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Negative, argumentStrengthBasedOnTargetTraits4, false, new TextObject("{=ja5bAOMr}The world's a dungheap, basically. The sooner I earn enough to retire, the better.", null), traitCorrelations4, false, true, false);
			persuasionTask.AddOptionToTask(option4);
			PersuasionTask persuasionTask2 = new PersuasionTask(1);
			list.Add(persuasionTask2);
			persuasionTask2.SpokenLine = new TextObject("{=5Vk6I1sf}Between your followers, your rivals and your enemies, you must have met a lot of interesting people...", null);
			persuasionTask2.FinalFailLine = new TextObject("{=lDJUL4lZ}I think we maybe see the world a bit differently.", null);
			persuasionTask2.TryLaterLine = new TextObject("{=ZmxbIXsp}I am sorry you feel that way. We can speak later.", null);
			Tuple<TraitObject, int>[] traitCorrelations5 = this.GetTraitCorrelations(1, 0, 1, 2, 0);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits5 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations5);
			PersuasionOptionArgs option5 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits5, false, new TextObject("{=8BnWa83o}I'm just honored to have fought alongside comrades who thought nothing of shedding their blood to keep me alive.", null), traitCorrelations5, false, true, false);
			persuasionTask2.AddOptionToTask(option5);
			Tuple<TraitObject, int>[] traitCorrelations6 = this.GetTraitCorrelations(0, 0, -1, 0, 1);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits6 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations6);
			PersuasionOptionArgs option6 = new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Calculating, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits6, false, new TextObject("{=QHG6LU1g}Ah yes, I've seen cruelty, degradation and degeneracy like you wouldn't believe. Fascinating stuff, all of it.", null), traitCorrelations6, false, true, false);
			persuasionTask2.AddOptionToTask(option6);
			Tuple<TraitObject, int>[] traitCorrelations7 = this.GetTraitCorrelations(0, 2, 0, 0, 0);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits7 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations7);
			PersuasionOptionArgs option7 = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Mercy, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits7, false, new TextObject("{=bwWdGLDv}I have seen great good and great evil, but I can only hope the good outweights the evil in most people's hearts.", null), traitCorrelations7, false, true, false);
			persuasionTask2.AddOptionToTask(option7);
			Tuple<TraitObject, int>[] traitCorrelations8 = this.GetTraitCorrelations(-1, 0, -1, -1, 0);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits8 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations8);
			PersuasionOptionArgs option8 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Negative, argumentStrengthBasedOnTargetTraits8, false, new TextObject("{=3skTM1DC}Most people would put a knife in your back for a few coppers. Have a few friends and keep them close, I guess.", null), traitCorrelations8, false, true, false);
			persuasionTask2.AddOptionToTask(option8);
			PersuasionTask persuasionTask3 = new PersuasionTask(2);
			list.Add(persuasionTask3);
			persuasionTask3.SpokenLine = Campaign.Current.ConversationManager.FindMatchingTextOrNull("str_courtship_aspirations_task", CharacterObject.OneToOneConversationCharacter);
			persuasionTask3.ImmediateFailLine = new TextObject("{=8hEVO9hw}Hmm. Perhaps you and I have different priorities in life.", null);
			persuasionTask3.FinalFailLine = new TextObject("{=HAtHptbV}In the end, I don't think we have that much in common.", null);
			persuasionTask3.TryLaterLine = new TextObject("{=PoDVgQaz}Well, it would take a bit long to discuss this.", null);
			Tuple<TraitObject, int>[] traitCorrelations9 = this.GetTraitCorrelations(0, 2, 1, 0, 0);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits9 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations9);
			PersuasionOptionArgs option9 = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Mercy, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits9, false, new TextObject("{=6kjacaiB}I hope I can bring peace to the land, and justice, and alleviate people's suffering.", null), traitCorrelations9, false, true, false);
			persuasionTask3.AddOptionToTask(option9);
			Tuple<TraitObject, int>[] traitCorrelations10 = this.GetTraitCorrelations(1, 1, 0, 2, 0);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits10 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations10);
			PersuasionOptionArgs option10 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits10, false, new TextObject("{=rrqCZa0H}I'll make sure those who stuck their necks out for me, who sweated and bled for me, get their due.", null), traitCorrelations10, false, true, false);
			persuasionTask3.AddOptionToTask(option10);
			Tuple<TraitObject, int>[] traitCorrelations11 = this.GetTraitCorrelations(0, 0, 0, 0, 2);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits11 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations11);
			PersuasionOptionArgs option11 = new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Calculating, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits11, false, new TextObject("{=ggKa4Bd8}Hmm... First thing to do after taking power is to work on your plan to remain in power.", null), traitCorrelations11, false, true, false);
			persuasionTask3.AddOptionToTask(option11);
			Tuple<TraitObject, int>[] traitCorrelations12 = this.GetTraitCorrelations(0, -2, 0, -1, 1);
			PersuasionArgumentStrength argumentStrengthBasedOnTargetTraits12 = Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(CharacterObject.OneToOneConversationCharacter, traitCorrelations12);
			PersuasionOptionArgs option12 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Calculating, TraitEffect.Positive, argumentStrengthBasedOnTargetTraits12, false, new TextObject("{=6L1b1nJa}Oh I have a long list of scores to settle. You can be sure of that.", null), traitCorrelations12, false, true, false);
			persuasionTask3.AddOptionToTask(option12);
			persuasionTask2.FinalFailLine = new TextObject("{=Ns315pxY}Perhaps we are not meant for each other.", null);
			persuasionTask2.TryLaterLine = new TextObject("{=PoDVgQaz}Well, it would take a bit long to discuss this.", null);
			return list;
		}

		// Token: 0x060045B3 RID: 17843 RVA: 0x00159718 File Offset: 0x00157918
		private Tuple<TraitObject, int>[] GetTraitCorrelations(int valor = 0, int mercy = 0, int honor = 0, int generosity = 0, int calculating = 0)
		{
			return new Tuple<TraitObject, int>[]
			{
				new Tuple<TraitObject, int>(DefaultTraits.Valor, valor),
				new Tuple<TraitObject, int>(DefaultTraits.Mercy, mercy),
				new Tuple<TraitObject, int>(DefaultTraits.Honor, honor),
				new Tuple<TraitObject, int>(DefaultTraits.Generosity, generosity),
				new Tuple<TraitObject, int>(DefaultTraits.Calculating, calculating)
			};
		}

		// Token: 0x060045B4 RID: 17844 RVA: 0x00159774 File Offset: 0x00157974
		private List<PersuasionTask> GetPersuasionTasksForCourtshipStage2(Hero wooed, Hero wooer)
		{
			StringHelpers.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject, null, false);
			List<PersuasionTask> list = new List<PersuasionTask>();
			IEnumerable<RomanceCampaignBehavior.RomanceReservationDescription> romanceReservations = this.GetRomanceReservations(wooed, wooer);
			bool flag = romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.AttractionIAmDrawnToYou);
			List<RomanceCampaignBehavior.RomanceReservationDescription> list2 = (from x in romanceReservations
			where x == RomanceCampaignBehavior.RomanceReservationDescription.CompatibiliyINeedSomeoneDangerous || x == RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityNeedSomethingInCommon || x == RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityINeedSomeoneUpright || x == RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityStrongPoliticalBeliefs
			select x).ToList<RomanceCampaignBehavior.RomanceReservationDescription>();
			if (list2.Count > 0)
			{
				RomanceCampaignBehavior.RomanceReservationDescription romanceReservationDescription = list2[0];
				PersuasionTask persuasionTask = new PersuasionTask(3);
				list.Add(persuasionTask);
				persuasionTask.SpokenLine = new TextObject("{=rtP6vnmj}I'm not sure we're compatible.", null);
				persuasionTask.FinalFailLine = new TextObject("{=bBTHy6f9}I just don't think that we would be happy together.", null);
				persuasionTask.TryLaterLine = new TextObject("{=o9ouu97M}I will endeavor to be worthy of your affections.", null);
				PersuasionArgumentStrength persuasionArgumentStrength = PersuasionArgumentStrength.Normal;
				if (romanceReservationDescription == RomanceCampaignBehavior.RomanceReservationDescription.CompatibiliyINeedSomeoneDangerous)
				{
					if (Hero.OneToOneConversationHero.IsFemale)
					{
						persuasionTask.SpokenLine = new TextObject("{=EkkNQb5N}I like a warrior who strikes fear in the hearts of his enemies. Are you that kind of man?", null);
					}
					else
					{
						persuasionTask.SpokenLine = new TextObject("{=3cw5pRFM}I had not thought that I might marry a shieldmaiden. But it is intriguing. Tell me, have you killed men in battle?", null);
					}
					PersuasionOptionArgs option = new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Valor, TraitEffect.Positive, persuasionArgumentStrength, false, new TextObject("{=FEmiPPbO}Perhaps you've heard the stories about me, then. They're all true.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option);
					PersuasionOptionArgs option2 = new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Calculating, TraitEffect.Positive, persuasionArgumentStrength + 1, false, new TextObject("{=Oe5Tf7OZ}My foes may not fear my sword, but they should fear my cunning.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option2);
					if (flag)
					{
						PersuasionOptionArgs option3 = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Calculating, TraitEffect.Negative, persuasionArgumentStrength - 1, true, new TextObject("{=zWTNOfHm}I want you and if you want me, that should be enough!", null), null, false, true, false);
						persuasionTask.AddOptionToTask(option3);
					}
					PersuasionOptionArgs option4 = new PersuasionOptionArgs(DefaultSkills.Roguery, DefaultTraits.Generosity, TraitEffect.Positive, persuasionArgumentStrength + 1, false, new TextObject("{=8a13MGzr}All I can say is that I try to repay good with good, and evil with evil.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option4);
				}
				if (romanceReservationDescription == RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityINeedSomeoneUpright)
				{
					persuasionTask.SpokenLine = new TextObject("{=lay7hKUK}I insist that my {?PLAYER.GENDER}wife{?}husband{\\?} conduct {?PLAYER.GENDER}herself{?}himself{\\?} according to the highest standards.", null);
					PersuasionOptionArgs option5 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("{=bOQEc7jA}I am a {?PLAYER.GENDER}woman{?}man{\\?} of my word. I hope that it is sufficient.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option5);
					PersuasionOptionArgs option6 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, PersuasionArgumentStrength.Normal, false, new TextObject("{=faa9sFfE}I do what I can to alleviate suffering in this world. I hope that is enough.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option6);
					if (flag)
					{
						PersuasionOptionArgs option7 = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Calculating, TraitEffect.Negative, persuasionArgumentStrength - 1, true, new TextObject("{=zWTNOfHm}I want you and if you want me, that should be enough!", null), null, false, true, false);
						persuasionTask.AddOptionToTask(option7);
					}
					PersuasionOptionArgs option8 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, PersuasionArgumentStrength.Hard, false, new TextObject("{=b2ePtImV}Those who are loyal to me, I am loyal to them.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option8);
				}
				if (romanceReservationDescription == RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityNeedSomethingInCommon)
				{
					persuasionTask.SpokenLine = new TextObject("{=ZsGqHBlR}I need a partner whom I can trust...", null);
					PersuasionOptionArgs option9 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, persuasionArgumentStrength - 1, false, new TextObject("{=LTUEFTaF}I hope that I am known as someone who understands the value of loyalty.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option9);
					PersuasionOptionArgs option10 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, persuasionArgumentStrength, false, new TextObject("{=9qoLQva5}Whatever oath I give to you, you may be sure that I will keep it.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option10);
					if (flag)
					{
						PersuasionOptionArgs option11 = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Calculating, TraitEffect.Negative, persuasionArgumentStrength - 1, true, new TextObject("{=zWTNOfHm}I want you and if you want me, that should be enough!", null), null, false, true, false);
						persuasionTask.AddOptionToTask(option11);
					}
					PersuasionOptionArgs option12 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, persuasionArgumentStrength, false, new TextObject("{=b2ePtImV}Those who are loyal to me, I am loyal to them.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option12);
				}
				if (romanceReservationDescription == RomanceCampaignBehavior.RomanceReservationDescription.CompatibilityStrongPoliticalBeliefs)
				{
					if (wooed.GetTraitLevel(DefaultTraits.Egalitarian) > 0)
					{
						persuasionTask.SpokenLine = new TextObject("{=s3Fna6wY}I've always seen myself as someone who sides with the weak of this realm. I don't want to find myself at odds with you.", null);
					}
					if (wooed.GetTraitLevel(DefaultTraits.Oligarchic) > 0)
					{
						persuasionTask.SpokenLine = new TextObject("{=DR2aK4aQ}I respect our ancient laws and traditions. I don't want to find myself at odds with you.", null);
					}
					if (wooed.GetTraitLevel(DefaultTraits.Authoritarian) > 0)
					{
						persuasionTask.SpokenLine = new TextObject("{=c2Yrci3B}I believe that we need a strong ruler in this realm. I don't want to find myself at odds with you.", null);
					}
					PersuasionOptionArgs option13 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, persuasionArgumentStrength, false, new TextObject("{=pVPkpP20}We may differ on politics, but I hope you'll think me a man with a good heart.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option13);
					if (flag)
					{
						PersuasionOptionArgs option14 = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Calculating, TraitEffect.Negative, persuasionArgumentStrength - 1, true, new TextObject("{=yghMrFdT}Put petty politics aside and trust your heart!", null), null, false, true, false);
						persuasionTask.AddOptionToTask(option14);
					}
					PersuasionOptionArgs option15 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, persuasionArgumentStrength, false, new TextObject("{=Tj8bGW4b}If a man and a woman respect each other, politics should not divide them.", null), null, false, true, false);
					persuasionTask.AddOptionToTask(option15);
				}
			}
			if ((from x in romanceReservations
			where x == RomanceCampaignBehavior.RomanceReservationDescription.AttractionYoureNotMyType
			select x).ToList<RomanceCampaignBehavior.RomanceReservationDescription>().Count > 0)
			{
				PersuasionTask persuasionTask2 = new PersuasionTask(4);
				list.Add(persuasionTask2);
				persuasionTask2.SpokenLine = new TextObject("{=cOyolp4F}I am just not... How can I say this? I am not attracted to you.", null);
				persuasionTask2.FinalFailLine = new TextObject("{=LjiYq9cH}I am sorry. I am not sure that I could ever love you.", null);
				persuasionTask2.TryLaterLine = new TextObject("{=E9s2bjqw}I can only hope that some day you could change your mind.", null);
				int num = 0;
				PersuasionArgumentStrength argumentStrength = (PersuasionArgumentStrength)(num - Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Calculating));
				PersuasionOptionArgs option16 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Calculating, TraitEffect.Positive, argumentStrength, false, new TextObject("{=hwjzKcUw}So what? This is supposed to be an alliance of our houses, not of our hearts.", null), null, false, true, false);
				persuasionTask2.AddOptionToTask(option16);
				PersuasionArgumentStrength persuasionArgumentStrength2 = (PersuasionArgumentStrength)(num - Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Generosity));
				PersuasionOptionArgs option17 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, persuasionArgumentStrength2 - 1, true, new TextObject("{=m3EkYCA6}Perhaps if you see how much I love you, you could come to love me over time.", null), null, false, true, false);
				persuasionTask2.AddOptionToTask(option17);
				PersuasionArgumentStrength argumentStrength2 = (PersuasionArgumentStrength)(num - Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Honor));
				PersuasionOptionArgs option18 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, argumentStrength2, false, new TextObject("{=LN7SGvnS}Love is but an infatuation. Judge me by my character.", null), null, false, true, false);
				persuasionTask2.AddOptionToTask(option18);
			}
			List<RomanceCampaignBehavior.RomanceReservationDescription> list3 = (from x in romanceReservations
			where x == RomanceCampaignBehavior.RomanceReservationDescription.PropertyHowCanIMarryAnAdventuress || x == RomanceCampaignBehavior.RomanceReservationDescription.PropertyIWantRealWealth || x == RomanceCampaignBehavior.RomanceReservationDescription.PropertyWeNeedToBeComfortable
			select x).ToList<RomanceCampaignBehavior.RomanceReservationDescription>();
			if (list3.Count > 0)
			{
				RomanceCampaignBehavior.RomanceReservationDescription romanceReservationDescription2 = list3[0];
				PersuasionTask persuasionTask3 = new PersuasionTask(6);
				list.Add(persuasionTask3);
				persuasionTask3.SpokenLine = new TextObject("{=beK0AZ2y}I am concerned that you do not have the means to support a family.", null);
				persuasionTask3.FinalFailLine = new TextObject("{=z6vJlozm}I am sorry. I don't believe you have the means to support a family.)", null);
				persuasionTask3.TryLaterLine = new TextObject("{=vaISh0sx}I will go off to make something of myself, then, and shall return to you.", null);
				PersuasionArgumentStrength persuasionArgumentStrength3 = PersuasionArgumentStrength.Normal;
				if (romanceReservationDescription2 == RomanceCampaignBehavior.RomanceReservationDescription.PropertyIWantRealWealth)
				{
					persuasionTask3.SpokenLine = new TextObject("{=pbqjBGk0}I will be honest. I have plans, and I expect the person I marry to have the income to support them.", null);
					persuasionArgumentStrength3 = PersuasionArgumentStrength.Hard;
				}
				else if (romanceReservationDescription2 == RomanceCampaignBehavior.RomanceReservationDescription.PropertyHowCanIMarryAnAdventuress)
				{
					persuasionTask3.SpokenLine = new TextObject("{=ZNfWXliN}I will be honest, my lady. You are but a common adventurer, and by marrying you I give up a chance to forge an alliance with a family of real influence and power.", null);
					persuasionArgumentStrength3 = PersuasionArgumentStrength.Normal;
				}
				PersuasionOptionArgs option19 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Calculating, TraitEffect.Positive, persuasionArgumentStrength3, false, new TextObject("{=erKuPRWA}I have a plan to rise in this world. I'm still only a little way up the ladder.", null), null, false, true, false);
				persuasionTask3.AddOptionToTask(option19);
				PersuasionOptionArgs option20 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Valor, TraitEffect.Positive, persuasionArgumentStrength3, false, (romanceReservationDescription2 == RomanceCampaignBehavior.RomanceReservationDescription.PropertyHowCanIMarryAnAdventuress) ? new TextObject("{=a2dJDUoL}My sword is my dowry. The gold and land will follow.", null) : new TextObject("{=DLc6NfiV}I shall win you the riches you deserve, or die in the attempt.", null), null, false, true, false);
				persuasionTask3.AddOptionToTask(option20);
				if (flag)
				{
					PersuasionArgumentStrength argumentStrength3 = persuasionArgumentStrength3 - Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Calculating);
					PersuasionOptionArgs option21 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, argumentStrength3, true, new TextObject("{=6LfkfJiJ}Can't your passion for me overcome such base feelings?", null), null, false, true, false);
					persuasionTask3.AddOptionToTask(option21);
				}
			}
			List<RomanceCampaignBehavior.RomanceReservationDescription> list4 = (from x in romanceReservations
			where x == RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalHowCanYouBeEnemiesWithOurFamily || x == RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalItWouldBeBestToBefriendOurFamily || x == RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalYouNeedToBeFriendsWithOurFamily
			select x).ToList<RomanceCampaignBehavior.RomanceReservationDescription>();
			if (list4.Count > 0 && list.Count < 3)
			{
				RomanceCampaignBehavior.RomanceReservationDescription romanceReservationDescription3 = list4[0];
				PersuasionTask persuasionTask4 = new PersuasionTask(5);
				list.Add(persuasionTask4);
				persuasionTask4.SpokenLine = new TextObject("{=fAdwIqbg}I think you should try to win my family's approval.", null);
				persuasionTask4.FinalFailLine = new TextObject("{=Xa7PsIao}I am sorry. I will not marry without my family's blessing.", null);
				persuasionTask4.TryLaterLine = new TextObject("{=44tA6fNa}I will try to earn your family's trust, then.", null);
				PersuasionArgumentStrength argumentStrength4 = PersuasionArgumentStrength.Normal;
				PersuasionOptionArgs option22 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, argumentStrength4, false, new TextObject("{=563qB3ar}I can only hope that if they come to know my loyalty, they will accept me.", null), null, false, true, false);
				persuasionTask4.AddOptionToTask(option22);
				if (flag)
				{
					PersuasionOptionArgs option23 = new PersuasionOptionArgs(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, argumentStrength4, true, new TextObject("{=LEsuGM8a}Let no one - not even your family - come between us!", null), null, false, true, false);
					persuasionTask4.AddOptionToTask(option23);
				}
				PersuasionOptionArgs option24 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Honor, TraitEffect.Positive, argumentStrength4, false, new TextObject("{=ZbvbsA4i}I can only hope that if they come to know my virtues, they will accept me.", null), null, false, true, false);
				persuasionTask4.AddOptionToTask(option24);
			}
			else if (list4.Count == 0 && list.Count < 3)
			{
				PersuasionTask persuasionTask5 = new PersuasionTask(7);
				list.Add(persuasionTask5);
				persuasionTask5.SpokenLine = new TextObject("{=HFkXIyCV}My family likes you...", null);
				persuasionTask5.FinalFailLine = new TextObject("{=3IBVEOwh}I still think we may not be ready yet.", null);
				persuasionTask5.TryLaterLine = new TextObject("{=44tA6fNa}I will try to earn your family's trust, then.", null);
				PersuasionArgumentStrength argumentStrength5 = PersuasionArgumentStrength.ExtremelyEasy;
				PersuasionOptionArgs option25 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Generosity, TraitEffect.Positive, argumentStrength5, false, new TextObject("{=2LrFafpB}And I will respect and cherish your family.", null), null, false, true, false);
				persuasionTask5.AddOptionToTask(option25);
				PersuasionOptionArgs option26 = new PersuasionOptionArgs(DefaultSkills.Charm, DefaultTraits.Calculating, TraitEffect.Positive, argumentStrength5, false, new TextObject("{=BaifRgT5}That's useful to know for when it comes time to discuss the exchange of dowries.", null), null, false, true, false);
				persuasionTask5.AddOptionToTask(option26);
			}
			return list;
		}

		// Token: 0x060045B5 RID: 17845 RVA: 0x0015A070 File Offset: 0x00158270
		private bool conversation_courtship_initial_reaction_on_condition()
		{
			IEnumerable<RomanceCampaignBehavior.RomanceReservationDescription> romanceReservations = this.GetRomanceReservations(Hero.OneToOneConversationHero, Hero.MainHero);
			if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInPracticalities || Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInCompatibility)
			{
				return false;
			}
			MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION", romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.AttractionIAmDrawnToYou) ? "{=WEkjz9tg}Ah! Yes... We are considering offers... Did you have someone in mind?" : "{=KdhnBhZ1}Yes, we are considering offers. These things are not rushed into.", false);
			return true;
		}

		// Token: 0x060045B6 RID: 17846 RVA: 0x0015A0F4 File Offset: 0x001582F4
		private bool conversation_courtship_decline_reaction_to_player_on_condition()
		{
			if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInPracticalities)
			{
				MBTextManager.SetTextVariable("COURTSHIP_DECLINE_REACTION", "{=emLBsWj6}I am terribly sorry. It is practically not possible for us to be married.", false);
				return true;
			}
			if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInCompatibility)
			{
				MBTextManager.SetTextVariable("COURTSHIP_DECLINE_REACTION", "{=s7idfhBO}I am terribly sorry. We are not really compatible with each other.", false);
				return true;
			}
			return false;
		}

		// Token: 0x060045B7 RID: 17847 RVA: 0x0015A14C File Offset: 0x0015834C
		private bool conversation_courtship_reaction_to_player_on_condition()
		{
			IEnumerable<RomanceCampaignBehavior.RomanceReservationDescription> romanceReservations = this.GetRomanceReservations(Hero.OneToOneConversationHero, Hero.MainHero);
			bool flag = Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Generosity) + Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Mercy) > 0;
			TraitObject persona = Hero.OneToOneConversationHero.CharacterObject.GetPersona();
			bool flag2 = ConversationTagHelper.UsesHighRegister(Hero.OneToOneConversationHero.CharacterObject);
			if (romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.AttractionIAmDrawnToYou))
			{
				if (persona == DefaultTraits.PersonaIronic && flag2)
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=5ao0RdRT}Well, I do not deny that there is something about you to which I am drawn.", false);
				}
				if (persona == DefaultTraits.PersonaIronic && !flag2)
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=r77ZrSUJ}You're straightforward. I like that.", false);
				}
				else if (persona == DefaultTraits.PersonaCurt)
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", Hero.MainHero.IsFemale ? "{=YXCGUSYd}Mm. Well, you'd make a very unusual match. But, well, I won't rule it out." : "{=iKYSgoZx}You're a handsome devil, I'll give you that.", false);
				}
				else if (persona == DefaultTraits.PersonaEarnest)
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=UCjFAPnk}I am flattered, {?PLAYER.GENDER}my lady{?}sir{\\?}.", false);
				}
				else
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=8PwNj5tR}Yes... Yes. We should, em, discuss this.", false);
				}
			}
			else if (romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.PropertyHowCanIMarryAnAdventuress))
			{
				MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=YRN4RBeI}Very well, madame, but I would have you know.... I intend to marry someone of my own rank.", false);
			}
			else
			{
				if (!flag)
				{
					if (romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.PropertyIWantRealWealth || x == RomanceCampaignBehavior.RomanceReservationDescription.PropertyWeNeedToBeComfortable))
					{
						MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=P407baEa}I think you would need to rise considerably in the world before I could consider such a thing...", false);
						return true;
					}
				}
				if (flag)
				{
					if (romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.PropertyIWantRealWealth))
					{
						MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=gS1noLvf}I do not know whether to find that charming or impertinent...", false);
						return true;
					}
				}
				if (romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.AttractionYoureNotMyType))
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=ltXu3DbR}Em... Yes, well, I suppose I can consider your offer.", false);
				}
				else if (romanceReservations.Any((RomanceCampaignBehavior.RomanceReservationDescription x) => x == RomanceCampaignBehavior.RomanceReservationDescription.FamilyApprovalIAmGladYouAreFriendsWithOurFamily))
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=UQtXV3kf}Certainly, you have always been close to our family.", false);
				}
				else
				{
					MBTextManager.SetTextVariable("INITIAL_COURTSHIP_REACTION_TO_PLAYER", "{=VYmQmqIv}We are considering many offers. You may certainly add your name to the list.", false);
				}
			}
			return true;
		}

		// Token: 0x060045B8 RID: 17848 RVA: 0x0015A3B4 File Offset: 0x001585B4
		private void conversation_fail_courtship_on_consequence()
		{
			if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.CourtshipStarted)
			{
				ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.FailedInCompatibility);
			}
			else if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible)
			{
				ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.FailedInPracticalities);
			}
			if (PlayerEncounter.Current != null)
			{
				PlayerEncounter.LeaveEncounter = true;
			}
			this._allReservations = null;
			ConversationManager.EndPersuasion();
		}

		// Token: 0x060045B9 RID: 17849 RVA: 0x0015A420 File Offset: 0x00158620
		private void conversation_start_courtship_persuasion_pt1_on_consequence()
		{
			if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.MatchMadeByFamily)
			{
				ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.CourtshipStarted);
			}
			Hero wooer = Hero.MainHero.MapFaction.Leader;
			if (Hero.MainHero.MapFaction == Hero.OneToOneConversationHero.MapFaction)
			{
				wooer = Hero.MainHero;
			}
			this._allReservations = this.GetPersuasionTasksForCourtshipStage1(Hero.OneToOneConversationHero, wooer);
			this._maximumScoreCap = (float)this._allReservations.Count * 1f;
			float num = 0f;
			foreach (PersuasionTask persuasionTask in this._allReservations)
			{
				foreach (PersuasionAttempt persuasionAttempt in this._previousRomancePersuasionAttempts)
				{
					if (persuasionAttempt.Matches(Hero.OneToOneConversationHero, persuasionTask.ReservationType))
					{
						switch (persuasionAttempt.Result)
						{
						case PersuasionOptionResult.CriticalFailure:
							num -= 2f;
							break;
						case PersuasionOptionResult.Failure:
							num -= 0f;
							break;
						case PersuasionOptionResult.Success:
							num += 1f;
							break;
						case PersuasionOptionResult.CriticalSuccess:
							num += 2f;
							break;
						}
					}
				}
			}
			this.RemoveUnneededPersuasionAttempts();
			ConversationManager.StartPersuasion(this._maximumScoreCap, 1f, 0f, 2f, 2f, num, PersuasionDifficulty.Medium);
		}

		// Token: 0x060045BA RID: 17850 RVA: 0x0015A5B0 File Offset: 0x001587B0
		private void conversation_courtship_stage_1_success_on_consequence()
		{
			Romance.RomanticState romanticState = Romance.GetRomanticState(Hero.MainHero, Hero.OneToOneConversationHero);
			float scoreFromPersuasion = ConversationManager.GetPersuasionProgress() - ConversationManager.GetPersuasionGoalValue();
			romanticState.ScoreFromPersuasion = scoreFromPersuasion;
			this._allReservations = null;
			ConversationManager.EndPersuasion();
			ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible);
		}

		// Token: 0x060045BB RID: 17851 RVA: 0x0015A5FC File Offset: 0x001587FC
		private void conversation_courtship_stage_2_success_on_consequence()
		{
			Romance.RomanticState romanticState = Romance.GetRomanticState(Hero.MainHero, Hero.OneToOneConversationHero);
			float num = ConversationManager.GetPersuasionProgress() - ConversationManager.GetPersuasionGoalValue();
			romanticState.ScoreFromPersuasion += num;
			this._allReservations = null;
			ConversationManager.EndPersuasion();
			ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.CoupleAgreedOnMarriage);
		}

		// Token: 0x060045BC RID: 17852 RVA: 0x0015A650 File Offset: 0x00158850
		private void conversation_continue_courtship_stage_2_on_consequence()
		{
			Hero wooer = Hero.MainHero.MapFaction.Leader;
			if (Hero.MainHero.MapFaction == Hero.OneToOneConversationHero.MapFaction)
			{
				wooer = Hero.MainHero;
			}
			this._allReservations = this.GetPersuasionTasksForCourtshipStage2(Hero.OneToOneConversationHero, wooer);
			this._maximumScoreCap = (float)this._allReservations.Count * 1f;
			float num = 0f;
			foreach (PersuasionTask persuasionTask in this._allReservations)
			{
				foreach (PersuasionAttempt persuasionAttempt in this._previousRomancePersuasionAttempts)
				{
					if (persuasionAttempt.Matches(Hero.OneToOneConversationHero, persuasionTask.ReservationType))
					{
						switch (persuasionAttempt.Result)
						{
						case PersuasionOptionResult.CriticalFailure:
							num -= 2f;
							break;
						case PersuasionOptionResult.Failure:
							num -= 0f;
							break;
						case PersuasionOptionResult.Success:
							num += 1f;
							break;
						case PersuasionOptionResult.CriticalSuccess:
							num += 2f;
							break;
						}
					}
				}
			}
			this.RemoveUnneededPersuasionAttempts();
			ConversationManager.StartPersuasion(this._maximumScoreCap, 1f, 0f, 2f, 2f, num, PersuasionDifficulty.Medium);
		}

		// Token: 0x060045BD RID: 17853 RVA: 0x0015A7C0 File Offset: 0x001589C0
		private bool conversation_check_if_unmet_reservation_on_condition()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask == this._allReservations[this._allReservations.Count - 1])
			{
				if (currentPersuasionTask.Options.All((PersuasionOptionArgs x) => x.IsBlocked))
				{
					return false;
				}
			}
			if (!ConversationManager.GetPersuasionProgressSatisfied())
			{
				MBTextManager.SetTextVariable("PERSUASION_TASK_LINE", currentPersuasionTask.SpokenLine, false);
				return true;
			}
			return false;
		}

		// Token: 0x060045BE RID: 17854 RVA: 0x0015A838 File Offset: 0x00158A38
		private bool conversation_lord_player_has_failed_in_courtship_on_condition()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.All((PersuasionOptionArgs x) => x.IsBlocked) && !ConversationManager.GetPersuasionProgressSatisfied())
			{
				MBTextManager.SetTextVariable("FAILED_PERSUASION_LINE", currentPersuasionTask.FinalFailLine, false);
				return true;
			}
			return false;
		}

		// Token: 0x060045BF RID: 17855 RVA: 0x0015A894 File Offset: 0x00158A94
		private bool conversation_courtship_persuasion_option_1_on_condition()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 0)
			{
				TextObject textObject = new TextObject("{=bSo9hKwr}{PERSUASION_OPTION_LINE} {SUCCESS_CHANCE}", null);
				textObject.SetTextVariable("SUCCESS_CHANCE", PersuasionHelper.ShowSuccess(currentPersuasionTask.Options.ElementAt(0), true));
				textObject.SetTextVariable("PERSUASION_OPTION_LINE", currentPersuasionTask.Options.ElementAt(0).Line);
				MBTextManager.SetTextVariable("ROMANCE_PERSUADE_ATTEMPT_1", textObject, false);
				return true;
			}
			return false;
		}

		// Token: 0x060045C0 RID: 17856 RVA: 0x0015A90C File Offset: 0x00158B0C
		private bool conversation_courtship_persuasion_option_2_on_condition()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 1)
			{
				TextObject textObject = new TextObject("{=bSo9hKwr}{PERSUASION_OPTION_LINE} {SUCCESS_CHANCE}", null);
				textObject.SetTextVariable("SUCCESS_CHANCE", PersuasionHelper.ShowSuccess(currentPersuasionTask.Options.ElementAt(1), true));
				textObject.SetTextVariable("PERSUASION_OPTION_LINE", currentPersuasionTask.Options.ElementAt(1).Line);
				MBTextManager.SetTextVariable("ROMANCE_PERSUADE_ATTEMPT_2", textObject, false);
				return true;
			}
			return false;
		}

		// Token: 0x060045C1 RID: 17857 RVA: 0x0015A984 File Offset: 0x00158B84
		private bool conversation_courtship_persuasion_option_3_on_condition()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 2)
			{
				TextObject textObject = new TextObject("{=bSo9hKwr}{PERSUASION_OPTION_LINE} {SUCCESS_CHANCE}", null);
				textObject.SetTextVariable("SUCCESS_CHANCE", PersuasionHelper.ShowSuccess(currentPersuasionTask.Options.ElementAt(2), true));
				textObject.SetTextVariable("PERSUASION_OPTION_LINE", currentPersuasionTask.Options.ElementAt(2).Line);
				MBTextManager.SetTextVariable("ROMANCE_PERSUADE_ATTEMPT_3", textObject, false);
				return true;
			}
			return false;
		}

		// Token: 0x060045C2 RID: 17858 RVA: 0x0015A9FC File Offset: 0x00158BFC
		private bool conversation_courtship_persuasion_option_4_on_condition()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 3)
			{
				TextObject textObject = new TextObject("{=bSo9hKwr}{PERSUASION_OPTION_LINE} {SUCCESS_CHANCE}", null);
				textObject.SetTextVariable("SUCCESS_CHANCE", PersuasionHelper.ShowSuccess(currentPersuasionTask.Options.ElementAt(3), true));
				textObject.SetTextVariable("PERSUASION_OPTION_LINE", currentPersuasionTask.Options.ElementAt(3).Line);
				MBTextManager.SetTextVariable("ROMANCE_PERSUADE_ATTEMPT_4", textObject, false);
				return true;
			}
			return false;
		}

		// Token: 0x060045C3 RID: 17859 RVA: 0x0015AA74 File Offset: 0x00158C74
		private void conversation_romance_1_persuade_option_on_consequence()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 0)
			{
				currentPersuasionTask.Options[0].BlockTheOption(true);
			}
		}

		// Token: 0x060045C4 RID: 17860 RVA: 0x0015AAA8 File Offset: 0x00158CA8
		private void conversation_romance_2_persuade_option_on_consequence()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 1)
			{
				currentPersuasionTask.Options[1].BlockTheOption(true);
			}
		}

		// Token: 0x060045C5 RID: 17861 RVA: 0x0015AADC File Offset: 0x00158CDC
		private void conversation_romance_3_persuade_option_on_consequence()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 2)
			{
				currentPersuasionTask.Options[2].BlockTheOption(true);
			}
		}

		// Token: 0x060045C6 RID: 17862 RVA: 0x0015AB10 File Offset: 0x00158D10
		private void conversation_romance_4_persuade_option_on_consequence()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 3)
			{
				currentPersuasionTask.Options[3].BlockTheOption(true);
			}
		}

		// Token: 0x060045C7 RID: 17863 RVA: 0x0015AB44 File Offset: 0x00158D44
		private bool RomancePersuasionOption1ClickableOnCondition1(out TextObject hintText)
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 0)
			{
				hintText = null;
				return !currentPersuasionTask.Options.ElementAt(0).IsBlocked;
			}
			hintText = new TextObject("{=9ACJsI6S}Blocked", null);
			return false;
		}

		// Token: 0x060045C8 RID: 17864 RVA: 0x0015AB8C File Offset: 0x00158D8C
		private bool RomancePersuasionOption2ClickableOnCondition2(out TextObject hintText)
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 1)
			{
				hintText = null;
				return !currentPersuasionTask.Options.ElementAt(1).IsBlocked;
			}
			hintText = new TextObject("{=9ACJsI6S}Blocked", null);
			return false;
		}

		// Token: 0x060045C9 RID: 17865 RVA: 0x0015ABD4 File Offset: 0x00158DD4
		private bool RomancePersuasionOption3ClickableOnCondition3(out TextObject hintText)
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 2)
			{
				hintText = null;
				return !currentPersuasionTask.Options.ElementAt(2).IsBlocked;
			}
			hintText = new TextObject("{=9ACJsI6S}Blocked", null);
			return false;
		}

		// Token: 0x060045CA RID: 17866 RVA: 0x0015AC1C File Offset: 0x00158E1C
		private bool RomancePersuasionOption4ClickableOnCondition4(out TextObject hintText)
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			if (currentPersuasionTask.Options.Count > 3)
			{
				hintText = null;
				return !currentPersuasionTask.Options.ElementAt(3).IsBlocked;
			}
			hintText = new TextObject("{=9ACJsI6S}Blocked", null);
			return false;
		}

		// Token: 0x060045CB RID: 17867 RVA: 0x0015AC64 File Offset: 0x00158E64
		private PersuasionOptionArgs SetupCourtshipPersuasionOption1()
		{
			return this.GetCurrentPersuasionTask().Options.ElementAt(0);
		}

		// Token: 0x060045CC RID: 17868 RVA: 0x0015AC77 File Offset: 0x00158E77
		private PersuasionOptionArgs SetupCourtshipPersuasionOption2()
		{
			return this.GetCurrentPersuasionTask().Options.ElementAt(1);
		}

		// Token: 0x060045CD RID: 17869 RVA: 0x0015AC8A File Offset: 0x00158E8A
		private PersuasionOptionArgs SetupCourtshipPersuasionOption3()
		{
			return this.GetCurrentPersuasionTask().Options.ElementAt(2);
		}

		// Token: 0x060045CE RID: 17870 RVA: 0x0015AC9D File Offset: 0x00158E9D
		private PersuasionOptionArgs SetupCourtshipPersuasionOption4()
		{
			return this.GetCurrentPersuasionTask().Options.ElementAt(3);
		}

		// Token: 0x060045CF RID: 17871 RVA: 0x0015ACB0 File Offset: 0x00158EB0
		private bool conversation_player_eligible_for_marriage_with_conversation_hero_on_condition()
		{
			return Hero.MainHero.Spouse == null && Hero.OneToOneConversationHero != null && this.MarriageCourtshipPossibility(Hero.MainHero, Hero.OneToOneConversationHero);
		}

		// Token: 0x060045D0 RID: 17872 RVA: 0x0015ACD7 File Offset: 0x00158ED7
		private bool conversation_player_eligible_for_marriage_with_hero_rltv_on_condition()
		{
			return Hero.MainHero.Spouse == null && Hero.OneToOneConversationHero != null;
		}

		// Token: 0x060045D1 RID: 17873 RVA: 0x0015ACEF File Offset: 0x00158EEF
		private void conversation_find_player_relatives_eligible_for_marriage_on_consequence()
		{
			ConversationSentence.SetObjectsToRepeatOver(this.FindPlayerRelativesEligibleForMarriage(Hero.OneToOneConversationHero.Clan).ToList<CharacterObject>(), 5);
		}

		// Token: 0x060045D2 RID: 17874 RVA: 0x0015AD0C File Offset: 0x00158F0C
		private void conversation_player_nominates_self_for_marriage_on_consequence()
		{
			this._playerProposalHero = Hero.MainHero;
		}

		// Token: 0x060045D3 RID: 17875 RVA: 0x0015AD1C File Offset: 0x00158F1C
		private void conversation_player_nominates_marriage_relative_on_consequence()
		{
			CharacterObject characterObject = ConversationSentence.SelectedRepeatObject as CharacterObject;
			this._playerProposalHero = characterObject.HeroObject;
		}

		// Token: 0x060045D4 RID: 17876 RVA: 0x0015AD40 File Offset: 0x00158F40
		private bool conversation_player_relative_eligible_for_marriage_on_condition()
		{
			CharacterObject characterObject = ConversationSentence.CurrentProcessedRepeatObject as CharacterObject;
			if (characterObject != null)
			{
				StringHelpers.SetRepeatableCharacterProperties("MARRIAGE_CANDIDATE", characterObject, false);
				return true;
			}
			return false;
		}

		// Token: 0x060045D5 RID: 17877 RVA: 0x0015AD6C File Offset: 0x00158F6C
		private bool conversation_propose_clan_leader_for_player_nomination_on_condition()
		{
			foreach (Hero hero in from x in Hero.OneToOneConversationHero.Clan.AliveLords
			orderby x.Age descending
			select x)
			{
				if (this.MarriageCourtshipPossibility(this._playerProposalHero, hero) && hero.CharacterObject == Hero.OneToOneConversationHero.CharacterObject)
				{
					this._proposedSpouseForPlayerRelative = hero;
					return true;
				}
			}
			return false;
		}

		// Token: 0x060045D6 RID: 17878 RVA: 0x0015AE10 File Offset: 0x00159010
		private bool conversation_propose_spouse_for_player_nomination_on_condition()
		{
			foreach (Hero hero in from x in Hero.OneToOneConversationHero.Clan.AliveLords
			orderby x.Age descending
			select x)
			{
				if (this.MarriageCourtshipPossibility(this._playerProposalHero, hero) && hero != Hero.OneToOneConversationHero)
				{
					this._proposedSpouseForPlayerRelative = hero;
					TextObject textObject = new TextObject("{=TjAQbTab}Well, yes, we are looking for a suitable marriage for { OTHER_CLAN_NOMINEE.LINK}.", null);
					hero.SetPropertiesToTextObject(textObject, "OTHER_CLAN_NOMINEE");
					MBTextManager.SetTextVariable("ARRANGE_MARRIAGE_LINE", textObject, false);
					hero.IsKnownToPlayer = true;
					return true;
				}
			}
			return false;
		}

		// Token: 0x060045D7 RID: 17879 RVA: 0x0015AED4 File Offset: 0x001590D4
		private bool conversation_player_rltv_agrees_on_courtship_on_condition()
		{
			Hero courtedHeroInOtherClan = Romance.GetCourtedHeroInOtherClan(this._playerProposalHero, this._proposedSpouseForPlayerRelative);
			return courtedHeroInOtherClan == null || courtedHeroInOtherClan == this._proposedSpouseForPlayerRelative;
		}

		// Token: 0x060045D8 RID: 17880 RVA: 0x0015AF01 File Offset: 0x00159101
		private void conversation_player_agrees_on_courtship_on_consequence()
		{
			ChangeRomanticStateAction.Apply(this._playerProposalHero, this._proposedSpouseForPlayerRelative, Romance.RomanceLevelEnum.MatchMadeByFamily);
		}

		// Token: 0x060045D9 RID: 17881 RVA: 0x0015AF18 File Offset: 0x00159118
		private void conversation_lord_propose_marriage_to_clan_leader_confirm_consequences()
		{
			MarriageBarterable marriageBarterable = new MarriageBarterable(Hero.MainHero, PartyBase.MainParty, this._playerProposalHero, this._proposedSpouseForPlayerRelative);
			BarterManager instance = BarterManager.Instance;
			Hero mainHero = Hero.MainHero;
			Hero oneToOneConversationHero = Hero.OneToOneConversationHero;
			PartyBase mainParty = PartyBase.MainParty;
			MobileParty partyBelongedTo = Hero.OneToOneConversationHero.PartyBelongedTo;
			instance.StartBarterOffer(mainHero, oneToOneConversationHero, mainParty, (partyBelongedTo != null) ? partyBelongedTo.Party : null, null, (Barterable barterableObj, BarterData _args, object obj) => BarterManager.Instance.InitializeMarriageBarterContext(barterableObj, _args, new Tuple<Hero, Hero>(this._playerProposalHero, this._proposedSpouseForPlayerRelative)), 0, false, new Barterable[]
			{
				marriageBarterable
			});
		}

		// Token: 0x060045DA RID: 17882 RVA: 0x0015AF8C File Offset: 0x0015918C
		private bool conversation_romance_blocked_on_condition()
		{
			if (Hero.OneToOneConversationHero == null)
			{
				return false;
			}
			Romance.RomanceLevelEnum romanticLevel = Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero);
			if (!this.MarriageCourtshipPossibility(Hero.MainHero, Hero.OneToOneConversationHero) && romanticLevel >= Romance.RomanceLevelEnum.MatchMadeByFamily && romanticLevel < Romance.RomanceLevelEnum.Marriage)
			{
				if (FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, Hero.OneToOneConversationHero.MapFaction))
				{
					MBTextManager.SetTextVariable("ROMANCE_BLOCKED_REASON", "{=wNxhmNOc}I am afraid I cannot entertain such a proposal so long as we are at war.", false);
					ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.FailedInCompatibility);
				}
				else if (Hero.OneToOneConversationHero.Clan.Leader == Hero.OneToOneConversationHero)
				{
					MBTextManager.SetTextVariable("ROMANCE_BLOCKED_REASON", "{=1FcxAGWU}Ah, yes. I am afraid I can no longer entertain such a proposal. I am now the head of my family, and the factors that we must consider have changed. You would need to place your property under my control, and I do not think that you would accept that.", false);
					ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.FailedInCompatibility);
				}
				else if (Hero.OneToOneConversationHero.PartyBelongedTo != null && Hero.OneToOneConversationHero.PartyBelongedTo.Army != null)
				{
					MBTextManager.SetTextVariable("ROMANCE_BLOCKED_REASON", "{=9LwYa3Tv}Ah, yes. My efforts are currently focused on this campaign, so it's best we discuss your proposal at a later time.", false);
				}
				else if (Hero.OneToOneConversationHero.PartyBelongedToAsPrisoner != null)
				{
					MBTextManager.SetTextVariable("ROMANCE_BLOCKED_REASON", "{=TuqmbbqB}Ah, yes. Unfortunately, this is no discussion to be had while I am captive. We shall discuss our future after I am freed from these chains.", false);
				}
				else if (MobileParty.MainParty.Army != null)
				{
					MBTextManager.SetTextVariable("ROMANCE_BLOCKED_REASON", "{=bLjYzudi}Let's discuss this matter at a later date, after your campaign has ended.", false);
				}
				else
				{
					MBTextManager.SetTextVariable("ROMANCE_BLOCKED_REASON", "{=BQn8yTs5}Ah, yes. I am afraid I can no longer entertain your proposal, at least not for now.", false);
					ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.FailedInCompatibility);
				}
				return true;
			}
			return false;
		}

		// Token: 0x060045DB RID: 17883 RVA: 0x0015B0E0 File Offset: 0x001592E0
		private bool conversation_romance_at_stage_1_discussions_on_condition()
		{
			if (Hero.OneToOneConversationHero == null)
			{
				return false;
			}
			Romance.RomanceLevelEnum romanticLevel = Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero);
			if (this.MarriageCourtshipPossibility(Hero.MainHero, Hero.OneToOneConversationHero) && (romanticLevel == Romance.RomanceLevelEnum.CourtshipStarted || romanticLevel == Romance.RomanceLevelEnum.MatchMadeByFamily))
			{
				List<PersuasionAttempt> list = (from x in this._previousRomancePersuasionAttempts
				where x.PersuadedHero == Hero.OneToOneConversationHero
				orderby x.GameTime descending
				select x).ToList<PersuasionAttempt>();
				if (list.Count == 0 || list[0].GameTime < this.RomanceCourtshipAttemptCooldown)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x060045DC RID: 17884 RVA: 0x0015B198 File Offset: 0x00159398
		private bool conversation_romance_at_stage_2_discussions_on_condition()
		{
			if (Hero.OneToOneConversationHero == null)
			{
				return false;
			}
			Romance.RomanceLevelEnum romanticLevel = Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero);
			if (this.MarriageCourtshipPossibility(Hero.MainHero, Hero.OneToOneConversationHero) && romanticLevel == Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible)
			{
				List<PersuasionAttempt> list = (from x in this._previousRomancePersuasionAttempts
				where x.PersuadedHero == Hero.OneToOneConversationHero
				orderby x.GameTime descending
				select x).ToList<PersuasionAttempt>();
				if (list.Count == 0 || list[0].GameTime < this.RomanceCourtshipAttemptCooldown)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x060045DD RID: 17885 RVA: 0x0015B24C File Offset: 0x0015944C
		private bool conversation_finalize_courtship_for_hero_on_condition()
		{
			return this.MarriageCourtshipPossibility(Hero.MainHero, Hero.OneToOneConversationHero) && Hero.OneToOneConversationHero.Clan.Leader == Hero.OneToOneConversationHero && Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.CoupleAgreedOnMarriage;
		}

		// Token: 0x060045DE RID: 17886 RVA: 0x0015B28C File Offset: 0x0015948C
		private bool conversation_finalize_courtship_for_other_on_condition()
		{
			if (Hero.OneToOneConversationHero != null)
			{
				Clan clan = Hero.OneToOneConversationHero.Clan;
				if (((clan != null) ? clan.Leader : null) == Hero.OneToOneConversationHero && !Hero.OneToOneConversationHero.IsPrisoner)
				{
					foreach (Hero hero in Hero.OneToOneConversationHero.Clan.AliveLords)
					{
						if (hero != Hero.OneToOneConversationHero && this.MarriageCourtshipPossibility(Hero.MainHero, hero) && Romance.GetRomanticLevel(Hero.MainHero, hero) == Romance.RomanceLevelEnum.CoupleAgreedOnMarriage)
						{
							MBTextManager.SetTextVariable("COURTSHIP_PARTNER", hero.Name, false);
							return true;
						}
					}
					return false;
				}
			}
			return false;
		}

		// Token: 0x060045DF RID: 17887 RVA: 0x0015B350 File Offset: 0x00159550
		private bool conversation_discuss_marriage_alliance_on_condition()
		{
			if (Hero.OneToOneConversationHero != null)
			{
				IFaction mapFaction = Hero.OneToOneConversationHero.MapFaction;
				if ((mapFaction == null || !mapFaction.IsMinorFaction) && !Hero.OneToOneConversationHero.IsPrisoner)
				{
					if (FactionManager.IsAtWarAgainstFaction(Hero.MainHero.MapFaction, Hero.OneToOneConversationHero.MapFaction))
					{
						return false;
					}
					if (Hero.OneToOneConversationHero.Clan == null || Hero.OneToOneConversationHero.Clan.Leader != Hero.OneToOneConversationHero)
					{
						return false;
					}
					bool result = false;
					foreach (Hero person in Hero.MainHero.Clan.Heroes)
					{
						foreach (Hero person2 in Hero.OneToOneConversationHero.Clan.AliveLords)
						{
							if (this.MarriageCourtshipPossibility(person, person2))
							{
								result = true;
							}
						}
					}
					return result;
				}
			}
			return false;
		}

		// Token: 0x060045E0 RID: 17888 RVA: 0x0015B468 File Offset: 0x00159668
		private bool conversation_player_can_open_courtship_on_condition()
		{
			if (Hero.OneToOneConversationHero != null)
			{
				IFaction mapFaction = Hero.OneToOneConversationHero.MapFaction;
				if ((mapFaction == null || !mapFaction.IsMinorFaction) && !Hero.OneToOneConversationHero.IsPrisoner)
				{
					if (this.MarriageCourtshipPossibility(Hero.MainHero, Hero.OneToOneConversationHero) && Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.Untested)
					{
						if (Hero.MainHero.IsFemale)
						{
							MBTextManager.SetTextVariable("FLIRTATION_LINE", "{=bjJs0eeB}My lord, I note that you have not yet taken a wife.", false);
						}
						else
						{
							MBTextManager.SetTextVariable("FLIRTATION_LINE", "{=v1hC6Aem}My lady, I wish to profess myself your most ardent admirer.", false);
						}
						return true;
					}
					if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInCompatibility || Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.FailedInPracticalities)
					{
						if (Hero.MainHero.IsFemale)
						{
							MBTextManager.SetTextVariable("FLIRTATION_LINE", "{=2WnhUBMM}My lord, may you give me another chance to prove myself?", false);
						}
						else
						{
							MBTextManager.SetTextVariable("FLIRTATION_LINE", "{=4iTaEZKg}My lady, may you give me another chance to prove myself?", false);
						}
						return true;
					}
					return false;
				}
			}
			return false;
		}

		// Token: 0x060045E1 RID: 17889 RVA: 0x0015B54A File Offset: 0x0015974A
		private bool conversation_player_opens_courtship_on_condition()
		{
			return this._playerProposalHero == Hero.MainHero;
		}

		// Token: 0x060045E2 RID: 17890 RVA: 0x0015B559 File Offset: 0x00159759
		private void conversation_player_opens_courtship_on_consequence()
		{
			if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) != Romance.RomanceLevelEnum.FailedInCompatibility && Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) != Romance.RomanceLevelEnum.FailedInPracticalities)
			{
				ChangeRomanticStateAction.Apply(Hero.MainHero, Hero.OneToOneConversationHero, Romance.RomanceLevelEnum.CourtshipStarted);
			}
		}

		// Token: 0x060045E3 RID: 17891 RVA: 0x0015B590 File Offset: 0x00159790
		private bool conversation_courtship_try_later_on_condition()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			MBTextManager.SetTextVariable("TRY_HARDER_LINE", currentPersuasionTask.TryLaterLine, false);
			return true;
		}

		// Token: 0x060045E4 RID: 17892 RVA: 0x0015B5B8 File Offset: 0x001597B8
		private bool conversation_courtship_reaction_stage_1_on_condition()
		{
			PersuasionOptionResult item = ConversationManager.GetPersuasionChosenOptions().Last<Tuple<PersuasionOptionArgs, PersuasionOptionResult>>().Item2;
			if (Romance.GetRomanticLevel(Hero.MainHero, Hero.OneToOneConversationHero) == Romance.RomanceLevelEnum.CourtshipStarted)
			{
				if ((item == PersuasionOptionResult.Failure || item == PersuasionOptionResult.CriticalFailure) && this.GetCurrentPersuasionTask().ImmediateFailLine != null)
				{
					MBTextManager.SetTextVariable("PERSUASION_REACTION", this.GetCurrentPersuasionTask().ImmediateFailLine, false);
					if (item != PersuasionOptionResult.CriticalFailure)
					{
						return true;
					}
					using (List<PersuasionTask>.Enumerator enumerator = this._allReservations.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							PersuasionTask persuasionTask = enumerator.Current;
							persuasionTask.BlockAllOptions();
						}
						return true;
					}
				}
				MBTextManager.SetTextVariable("PERSUASION_REACTION", PersuasionHelper.GetDefaultPersuasionOptionReaction(item), false);
				return true;
			}
			return false;
		}

		// Token: 0x060045E5 RID: 17893 RVA: 0x0015B674 File Offset: 0x00159874
		private bool conversation_marriage_barter_successful_on_condition()
		{
			return Campaign.Current.BarterManager.LastBarterIsAccepted;
		}

		// Token: 0x060045E6 RID: 17894 RVA: 0x0015B688 File Offset: 0x00159888
		private void conversation_marriage_barter_successful_on_consequence()
		{
			foreach (PersuasionAttempt persuasionAttempt in this._previousRomancePersuasionAttempts)
			{
				if (persuasionAttempt.PersuadedHero == Hero.OneToOneConversationHero || Hero.OneToOneConversationHero.Clan.AliveLords.Contains(persuasionAttempt.PersuadedHero))
				{
					PersuasionOptionResult result = persuasionAttempt.Result;
					if (result != PersuasionOptionResult.Success)
					{
						if (result == PersuasionOptionResult.CriticalSuccess)
						{
							int num = (persuasionAttempt.Args.ArgumentStrength < PersuasionArgumentStrength.Normal) ? (MathF.Abs((int)persuasionAttempt.Args.ArgumentStrength) * 50) : 50;
							SkillLevelingManager.OnPersuasionSucceeded(Hero.MainHero, persuasionAttempt.Args.SkillUsed, PersuasionDifficulty.Medium, 2 * num);
						}
					}
					else
					{
						int num = (persuasionAttempt.Args.ArgumentStrength < PersuasionArgumentStrength.Normal) ? (MathF.Abs((int)persuasionAttempt.Args.ArgumentStrength) * 50) : 50;
						SkillLevelingManager.OnPersuasionSucceeded(Hero.MainHero, persuasionAttempt.Args.SkillUsed, PersuasionDifficulty.Medium, num);
					}
				}
			}
		}

		// Token: 0x060045E7 RID: 17895 RVA: 0x0015B798 File Offset: 0x00159998
		private bool conversation_courtship_reaction_stage_2_on_condition()
		{
			PersuasionOptionResult item = ConversationManager.GetPersuasionChosenOptions().Last<Tuple<PersuasionOptionArgs, PersuasionOptionResult>>().Item2;
			if (item == PersuasionOptionResult.Success)
			{
				MBTextManager.SetTextVariable("PERSUASION_REACTION", "{=KWBzmJQl}I am happy to hear that.", false);
			}
			else if (item == PersuasionOptionResult.CriticalSuccess)
			{
				MBTextManager.SetTextVariable("PERSUASION_REACTION", "{=RGZWdKDx}Ah. It makes me so glad to hear you say that!", false);
			}
			else if ((item == PersuasionOptionResult.Failure || item == PersuasionOptionResult.CriticalFailure) && this.GetCurrentPersuasionTask().ImmediateFailLine != null)
			{
				MBTextManager.SetTextVariable("PERSUASION_REACTION", this.GetCurrentPersuasionTask().ImmediateFailLine, false);
			}
			else if (item == PersuasionOptionResult.Failure)
			{
				MBTextManager.SetTextVariable("PERSUASION_REACTION", "{=OqqUatT9}I... I think this will be difficult. Perhaps we are not meant for each other.", false);
			}
			else if (item == PersuasionOptionResult.CriticalFailure)
			{
				MBTextManager.SetTextVariable("PERSUASION_REACTION", "{=APSE3Q6r}What? No... I cannot, I cannot agree.", false);
				foreach (PersuasionTask persuasionTask in this._allReservations)
				{
					persuasionTask.BlockAllOptions();
				}
			}
			return true;
		}

		// Token: 0x060045E8 RID: 17896 RVA: 0x0015B888 File Offset: 0x00159A88
		private void conversation_lord_persuade_option_reaction_on_consequence()
		{
			PersuasionTask currentPersuasionTask = this.GetCurrentPersuasionTask();
			Tuple<PersuasionOptionArgs, PersuasionOptionResult> tuple = ConversationManager.GetPersuasionChosenOptions().Last<Tuple<PersuasionOptionArgs, PersuasionOptionResult>>();
			float difficulty = Campaign.Current.Models.PersuasionModel.GetDifficulty(PersuasionDifficulty.Medium);
			float moveToNextStageChance;
			float blockRandomOptionChance;
			Campaign.Current.Models.PersuasionModel.GetEffectChances(tuple.Item1, out moveToNextStageChance, out blockRandomOptionChance, difficulty);
			this.FindTaskOfOption(tuple.Item1).ApplyEffects(moveToNextStageChance, blockRandomOptionChance);
			PersuasionAttempt item = new PersuasionAttempt(Hero.OneToOneConversationHero, CampaignTime.Now, tuple.Item1, tuple.Item2, currentPersuasionTask.ReservationType);
			this._previousRomancePersuasionAttempts.Add(item);
		}

		// Token: 0x060045E9 RID: 17897 RVA: 0x0015B920 File Offset: 0x00159B20
		private PersuasionTask FindTaskOfOption(PersuasionOptionArgs optionChosenWithLine)
		{
			foreach (PersuasionTask persuasionTask in this._allReservations)
			{
				using (List<PersuasionOptionArgs>.Enumerator enumerator2 = persuasionTask.Options.GetEnumerator())
				{
					while (enumerator2.MoveNext())
					{
						if (enumerator2.Current.Line == optionChosenWithLine.Line)
						{
							return persuasionTask;
						}
					}
				}
			}
			return null;
		}

		// Token: 0x060045EA RID: 17898 RVA: 0x0015B9C0 File Offset: 0x00159BC0
		private List<CharacterObject> FindPlayerRelativesEligibleForMarriage(Clan withClan)
		{
			List<CharacterObject> list = new List<CharacterObject>();
			MarriageModel marriageModel = Campaign.Current.Models.MarriageModel;
			using (List<Hero>.Enumerator enumerator = Hero.MainHero.Clan.AliveLords.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					Hero characterRelative = enumerator.Current;
					IEnumerable<Hero> source = from x in withClan.AliveLords
					where marriageModel.IsCoupleSuitableForMarriage(x, characterRelative)
					select x;
					if (characterRelative != Hero.MainHero && source.Any<Hero>())
					{
						list.Add(characterRelative.CharacterObject);
					}
				}
			}
			return list;
		}

		// Token: 0x060045EB RID: 17899 RVA: 0x0015BA8C File Offset: 0x00159C8C
		private TextObject ShowSuccess(PersuasionOptionArgs optionArgs)
		{
			return TextObject.GetEmpty();
		}

		// Token: 0x060045EC RID: 17900 RVA: 0x0015BA93 File Offset: 0x00159C93
		private bool MarriageCourtshipPossibility(Hero person1, Hero person2)
		{
			return Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(person1, person2) && !FactionManager.IsAtWarAgainstFaction(person1.MapFaction, person2.MapFaction);
		}

		// Token: 0x0400139C RID: 5020
		private const PersuasionDifficulty _difficulty = PersuasionDifficulty.Medium;

		// Token: 0x0400139D RID: 5021
		private List<PersuasionTask> _allReservations;

		// Token: 0x0400139E RID: 5022
		[SaveableField(1)]
		private List<PersuasionAttempt> _previousRomancePersuasionAttempts;

		// Token: 0x0400139F RID: 5023
		private Hero _playerProposalHero;

		// Token: 0x040013A0 RID: 5024
		private Hero _proposedSpouseForPlayerRelative;

		// Token: 0x040013A1 RID: 5025
		private float _maximumScoreCap;

		// Token: 0x040013A2 RID: 5026
		private const float _successValue = 1f;

		// Token: 0x040013A3 RID: 5027
		private const float _criticalSuccessValue = 2f;

		// Token: 0x040013A4 RID: 5028
		private const float _criticalFailValue = 2f;

		// Token: 0x040013A5 RID: 5029
		private const float _failValue = 0f;

		// Token: 0x02000842 RID: 2114
		public enum RomanticPreference
		{
			// Token: 0x04002385 RID: 9093
			Conventional,
			// Token: 0x04002386 RID: 9094
			Moralist,
			// Token: 0x04002387 RID: 9095
			AttractedToBravery,
			// Token: 0x04002388 RID: 9096
			Macchiavellian,
			// Token: 0x04002389 RID: 9097
			Romantic,
			// Token: 0x0400238A RID: 9098
			Companionship,
			// Token: 0x0400238B RID: 9099
			MadAndBad,
			// Token: 0x0400238C RID: 9100
			Security,
			// Token: 0x0400238D RID: 9101
			PreferencesEnd
		}

		// Token: 0x02000843 RID: 2115
		private enum RomanceReservationType
		{
			// Token: 0x0400238F RID: 9103
			TravelChat,
			// Token: 0x04002390 RID: 9104
			TravelLesson,
			// Token: 0x04002391 RID: 9105
			Aspirations,
			// Token: 0x04002392 RID: 9106
			Compatibility,
			// Token: 0x04002393 RID: 9107
			Attraction,
			// Token: 0x04002394 RID: 9108
			Family,
			// Token: 0x04002395 RID: 9109
			MaterialWealth,
			// Token: 0x04002396 RID: 9110
			NoObjection
		}

		// Token: 0x02000844 RID: 2116
		private enum RomanceReservationDescription
		{
			// Token: 0x04002398 RID: 9112
			CompatibilityINeedSomeoneUpright,
			// Token: 0x04002399 RID: 9113
			CompatibilityNeedSomethingInCommon,
			// Token: 0x0400239A RID: 9114
			CompatibiliyINeedSomeoneDangerous,
			// Token: 0x0400239B RID: 9115
			CompatibilityStrongPoliticalBeliefs,
			// Token: 0x0400239C RID: 9116
			AttractionYoureNotMyType,
			// Token: 0x0400239D RID: 9117
			AttractionYoureGoodEnough,
			// Token: 0x0400239E RID: 9118
			AttractionIAmDrawnToYou,
			// Token: 0x0400239F RID: 9119
			PropertyYouSeemRichEnough,
			// Token: 0x040023A0 RID: 9120
			PropertyWeNeedToBeComfortable,
			// Token: 0x040023A1 RID: 9121
			PropertyIWantRealWealth,
			// Token: 0x040023A2 RID: 9122
			PropertyHowCanIMarryAnAdventuress,
			// Token: 0x040023A3 RID: 9123
			FamilyApprovalIAmGladYouAreFriendsWithOurFamily,
			// Token: 0x040023A4 RID: 9124
			FamilyApprovalYouNeedToBeFriendsWithOurFamily,
			// Token: 0x040023A5 RID: 9125
			FamilyApprovalHowCanYouBeEnemiesWithOurFamily,
			// Token: 0x040023A6 RID: 9126
			FamilyApprovalItWouldBeBestToBefriendOurFamily
		}
	}
}
