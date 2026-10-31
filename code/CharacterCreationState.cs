using System;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TaleWorlds.CampaignSystem.CharacterCreationContent
{
	// Token: 0x0200020D RID: 525
	public class CharacterCreationState : PlayerGameState
	{
		// Token: 0x170007EA RID: 2026
		// (get) Token: 0x06002010 RID: 8208 RVA: 0x00090DD8 File Offset: 0x0008EFD8
		// (set) Token: 0x06002011 RID: 8209 RVA: 0x00090DE0 File Offset: 0x0008EFE0
		public CharacterCreationManager CharacterCreationManager
		{
			get
			{
				return this._characterCreationManager;
			}
			private set
			{
				this._characterCreationManager = value;
			}
		}

		// Token: 0x170007EB RID: 2027
		// (get) Token: 0x06002012 RID: 8210 RVA: 0x00090DE9 File Offset: 0x0008EFE9
		// (set) Token: 0x06002013 RID: 8211 RVA: 0x00090DF1 File Offset: 0x0008EFF1
		public ICharacterCreationStateHandler Handler
		{
			get
			{
				return this._handler;
			}
			set
			{
				this._handler = value;
			}
		}

		// Token: 0x06002014 RID: 8212 RVA: 0x00090DFA File Offset: 0x0008EFFA
		public CharacterCreationState()
		{
			this.CharacterCreationManager = new CharacterCreationManager(this);
		}

		// Token: 0x06002015 RID: 8213 RVA: 0x00090E0E File Offset: 0x0008F00E
		protected override void OnInitialize()
		{
			base.OnInitialize();
			Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);
		}

		// Token: 0x06002016 RID: 8214 RVA: 0x00090E26 File Offset: 0x0008F026
		protected override void OnActivate()
		{
			base.OnActivate();
			this.CharacterCreationManager.OnStateActivated();
		}

		// Token: 0x06002017 RID: 8215 RVA: 0x00090E3C File Offset: 0x0008F03C
		public void FinalizeCharacterCreationState()
		{
			Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this);
			Game.Current.GameStateManager.CleanAndPushState(Game.Current.GameStateManager.CreateState<MapState>(), 0);
			PartyBase.MainParty.SetVisualAsDirty();
			ICharacterCreationStateHandler handler = this._handler;
			if (handler != null)
			{
				handler.OnCharacterCreationFinalized();
			}
			CampaignEventDispatcher.Instance.OnCharacterCreationIsOver();
		}

		// Token: 0x06002018 RID: 8216 RVA: 0x00090E9D File Offset: 0x0008F09D
		public void Refresh()
		{
			ICharacterCreationStateHandler handler = this._handler;
			if (handler == null)
			{
				return;
			}
			handler.OnRefresh();
		}

		// Token: 0x06002019 RID: 8217 RVA: 0x00090EAF File Offset: 0x0008F0AF
		public void OnStageActivated(CharacterCreationStageBase stage)
		{
			ICharacterCreationStateHandler handler = this._handler;
			if (handler == null)
			{
				return;
			}
			handler.OnStageCreated(stage);
		}

		// Token: 0x0400096A RID: 2410
		private CharacterCreationManager _characterCreationManager;

		// Token: 0x0400096B RID: 2411
		private ICharacterCreationStateHandler _handler;
	}
}
