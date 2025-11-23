using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem
{
	// Token: 0x02000057 RID: 87
	public class CampaignGameStarter : IGameStarter
	{
		// Token: 0x170001C8 RID: 456
		// (get) Token: 0x060008AB RID: 2219 RVA: 0x000267AE File Offset: 0x000249AE
		public ICollection<CampaignBehaviorBase> CampaignBehaviors
		{
			get
			{
				return this._campaignBehaviors;
			}
		}

		// Token: 0x170001C9 RID: 457
		// (get) Token: 0x060008AC RID: 2220 RVA: 0x000267B6 File Offset: 0x000249B6
		public IEnumerable<GameModel> Models
		{
			get
			{
				return this._models;
			}
		}

		// Token: 0x060008AD RID: 2221 RVA: 0x000267BE File Offset: 0x000249BE
		public CampaignGameStarter(GameMenuManager gameMenuManager, ConversationManager conversationManager)
		{
			this._conversationManager = conversationManager;
			this._gameMenuManager = gameMenuManager;
		}

		// Token: 0x060008AE RID: 2222 RVA: 0x000267EA File Offset: 0x000249EA
		public void UnregisterNonReadyObjects()
		{
			Game.Current.ObjectManager.UnregisterNonReadyObjects();
			this._gameMenuManager.UnregisterNonReadyObjects();
		}

		// Token: 0x060008AF RID: 2223 RVA: 0x00026806 File Offset: 0x00024A06
		public void AddBehavior(CampaignBehaviorBase campaignBehavior)
		{
			if (campaignBehavior != null)
			{
				this._campaignBehaviors.Add(campaignBehavior);
			}
		}

		// Token: 0x060008B0 RID: 2224 RVA: 0x00026818 File Offset: 0x00024A18
		public void RemoveBehaviors<T>() where T : CampaignBehaviorBase
		{
			for (int i = this._campaignBehaviors.Count - 1; i >= 0; i--)
			{
				if (this._campaignBehaviors[i] is T)
				{
					this._campaignBehaviors.RemoveAt(i);
				}
			}
		}

		// Token: 0x060008B1 RID: 2225 RVA: 0x0002685C File Offset: 0x00024A5C
		public bool RemoveBehavior<T>(T behavior) where T : CampaignBehaviorBase
		{
			return this._campaignBehaviors.Remove(behavior);
		}

		// Token: 0x060008B2 RID: 2226 RVA: 0x00026870 File Offset: 0x00024A70
		public T GetModel<T>() where T : GameModel
		{
			for (int i = this._models.Count - 1; i >= 0; i--)
			{
				T result;
				if ((result = (this._models[i] as T)) != null)
				{
					return result;
				}
			}
			return default(T);
		}

		// Token: 0x060008B3 RID: 2227 RVA: 0x000268BF File Offset: 0x00024ABF
		public void AddModel(GameModel gameModel)
		{
			this._models.Add(gameModel);
		}

		// Token: 0x060008B4 RID: 2228 RVA: 0x000268D0 File Offset: 0x00024AD0
		public void AddModel<T>(MBGameModel<T> gameModel) where T : GameModel
		{
			T model = this.GetModel<T>();
			gameModel.Initialize(model);
			this._models.Add(gameModel);
		}

		// Token: 0x060008B5 RID: 2229 RVA: 0x000268F7 File Offset: 0x00024AF7
		public void AddGameMenu(string menuId, string menuText, OnInitDelegate initDelegate, GameMenu.MenuOverlayType overlay = GameMenu.MenuOverlayType.None, GameMenu.MenuFlags menuFlags = GameMenu.MenuFlags.None, object relatedObject = null)
		{
			this.GetPresumedGameMenu(menuId).Initialize(new TextObject(menuText, null), initDelegate, overlay, menuFlags, relatedObject);
		}

		// Token: 0x060008B6 RID: 2230 RVA: 0x00026914 File Offset: 0x00024B14
		public void AddWaitGameMenu(string idString, string text, OnInitDelegate initDelegate, OnConditionDelegate condition, OnConsequenceDelegate consequence, OnTickDelegate tick, GameMenu.MenuAndOptionType type, GameMenu.MenuOverlayType overlay = GameMenu.MenuOverlayType.None, float targetWaitHours = 0f, GameMenu.MenuFlags flags = GameMenu.MenuFlags.None, object relatedObject = null)
		{
			this.GetPresumedGameMenu(idString).Initialize(new TextObject(text, null), initDelegate, condition, consequence, tick, type, overlay, targetWaitHours, flags, relatedObject);
		}

		// Token: 0x060008B7 RID: 2231 RVA: 0x00026948 File Offset: 0x00024B48
		public void AddGameMenuOption(string menuId, string optionId, string optionText, GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence, bool isLeave = false, int index = -1, bool isRepeatable = false, object relatedObject = null)
		{
			this.GetPresumedGameMenu(menuId).AddOption(optionId, new TextObject(optionText, null), condition, consequence, index, isLeave, isRepeatable, relatedObject);
		}

		// Token: 0x060008B8 RID: 2232 RVA: 0x00026978 File Offset: 0x00024B78
		public GameMenu GetPresumedGameMenu(string stringId)
		{
			GameMenu gameMenu = this._gameMenuManager.GetGameMenu(stringId);
			if (gameMenu == null)
			{
				gameMenu = new GameMenu(stringId);
				this._gameMenuManager.AddGameMenu(gameMenu);
			}
			return gameMenu;
		}

		// Token: 0x060008B9 RID: 2233 RVA: 0x000269A9 File Offset: 0x00024BA9
		private ConversationSentence AddDialogLine(ConversationSentence dialogLine)
		{
			this._conversationManager.AddDialogLine(dialogLine);
			return dialogLine;
		}

		// Token: 0x060008BA RID: 2234 RVA: 0x000269B9 File Offset: 0x00024BB9
		public void AddDialogFlow(DialogFlow dialogFlow, object relatedObject = null)
		{
			this._conversationManager.AddDialogFlow(dialogFlow, relatedObject);
		}

		// Token: 0x060008BB RID: 2235 RVA: 0x000269C8 File Offset: 0x00024BC8
		public ConversationSentence AddPlayerLine(string id, string inputToken, string outputToken, string text, ConversationSentence.OnConditionDelegate conditionDelegate, ConversationSentence.OnConsequenceDelegate consequenceDelegate, int priority = 100, ConversationSentence.OnClickableConditionDelegate clickableConditionDelegate = null, ConversationSentence.OnPersuasionOptionDelegate persuasionOptionDelegate = null)
		{
			return this.AddDialogLine(new ConversationSentence(id, new TextObject(text, null), inputToken, outputToken, conditionDelegate, clickableConditionDelegate, consequenceDelegate, 1U, priority, 0, 0, null, false, null, null, persuasionOptionDelegate));
		}

		// Token: 0x060008BC RID: 2236 RVA: 0x000269FC File Offset: 0x00024BFC
		public ConversationSentence AddRepeatablePlayerLine(string id, string inputToken, string outputToken, string text, string continueListingRepeatedObjectsText, string continueListingOptionOutputToken, ConversationSentence.OnConditionDelegate conditionDelegate, ConversationSentence.OnConsequenceDelegate consequenceDelegate, int priority = 100, ConversationSentence.OnClickableConditionDelegate clickableConditionDelegate = null)
		{
			ConversationSentence result = this.AddDialogLine(new ConversationSentence(id, new TextObject(text, null), inputToken, outputToken, conditionDelegate, clickableConditionDelegate, consequenceDelegate, 3U, priority, 0, 0, null, false, null, null, null));
			this.AddDialogLine(new ConversationSentence(id + "_continue", new TextObject(continueListingRepeatedObjectsText, null), inputToken, continueListingOptionOutputToken, new ConversationSentence.OnConditionDelegate(ConversationManager.IsThereMultipleRepeatablePages), null, new ConversationSentence.OnConsequenceDelegate(ConversationManager.DialogRepeatContinueListing), 1U, priority, 0, 0, null, false, null, null, null));
			return result;
		}

		// Token: 0x060008BD RID: 2237 RVA: 0x00026A74 File Offset: 0x00024C74
		public ConversationSentence AddDialogLineWithVariation(string id, string inputToken, string outputToken, ConversationSentence.OnConditionDelegate conditionDelegate, ConversationSentence.OnConsequenceDelegate consequenceDelegate, int priority = 100, string idleActionId = "", string idleFaceAnimId = "", string reactionId = "", string reactionFaceAnimId = "", ConversationSentence.OnClickableConditionDelegate clickableConditionDelegate = null)
		{
			return this.AddDialogLine(new ConversationSentence(id, new TextObject("{=!}{VARIATION_TEXT_TAGGED_LINE}", null), inputToken, outputToken, conditionDelegate, clickableConditionDelegate, consequenceDelegate, 0U, priority, 0, 0, null, true, null, null, null));
		}

		// Token: 0x060008BE RID: 2238 RVA: 0x00026AAC File Offset: 0x00024CAC
		public ConversationSentence AddDialogLine(string id, string inputToken, string outputToken, string text, ConversationSentence.OnConditionDelegate conditionDelegate, ConversationSentence.OnConsequenceDelegate consequenceDelegate, int priority = 100, ConversationSentence.OnClickableConditionDelegate clickableConditionDelegate = null)
		{
			return this.AddDialogLine(new ConversationSentence(id, new TextObject(text, null), inputToken, outputToken, conditionDelegate, clickableConditionDelegate, consequenceDelegate, 0U, priority, 0, 0, null, false, null, null, null));
		}

		// Token: 0x060008BF RID: 2239 RVA: 0x00026AE0 File Offset: 0x00024CE0
		public ConversationSentence AddDialogLineMultiAgent(string id, string inputToken, string outputToken, TextObject text, ConversationSentence.OnConditionDelegate conditionDelegate, ConversationSentence.OnConsequenceDelegate consequenceDelegate, int agentIndex, int nextAgentIndex, int priority = 100, ConversationSentence.OnClickableConditionDelegate clickableConditionDelegate = null)
		{
			return this.AddDialogLine(new ConversationSentence(id, text, inputToken, outputToken, conditionDelegate, clickableConditionDelegate, consequenceDelegate, 0U, priority, agentIndex, nextAgentIndex, null, false, null, null, null));
		}

		// Token: 0x040002B9 RID: 697
		private readonly GameMenuManager _gameMenuManager;

		// Token: 0x040002BA RID: 698
		private readonly ConversationManager _conversationManager;

		// Token: 0x040002BB RID: 699
		private readonly List<CampaignBehaviorBase> _campaignBehaviors = new List<CampaignBehaviorBase>();

		// Token: 0x040002BC RID: 700
		private readonly List<GameModel> _models = new List<GameModel>();
	}
}
