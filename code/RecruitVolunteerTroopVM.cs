using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment
{
	// Token: 0x020000B2 RID: 178
	public class RecruitVolunteerTroopVM : ViewModel
	{
		// Token: 0x06001131 RID: 4401 RVA: 0x00044920 File Offset: 0x00042B20
		public RecruitVolunteerTroopVM(RecruitVolunteerVM owner, CharacterObject character, int index, Action<RecruitVolunteerTroopVM> onClick, Action<RecruitVolunteerTroopVM> onRemoveFromCart)
		{
			if (character != null)
			{
				this.NameText = character.Name.ToString();
				this._character = character;
				GameTexts.SetVariable("LEVEL", character.Level);
				this.Level = GameTexts.FindText("str_level_with_value", null).ToString();
				this.Character = character;
				this.Wage = this.Character.TroopWage;
				this.Cost = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(this.Character, Hero.MainHero, false).RoundedResultNumber;
				this.IsTroopEmpty = false;
				CharacterCode characterCode = CampaignUIHelper.GetCharacterCode(character, false);
				this.ImageIdentifier = new CharacterImageIdentifierVM(characterCode);
				this.TierIconData = CampaignUIHelper.GetCharacterTierData(character, false);
				this.TypeIconData = CampaignUIHelper.GetCharacterTypeData(character, false);
			}
			else
			{
				this.IsTroopEmpty = true;
			}
			this.Owner = owner;
			if (this.Owner != null)
			{
				this._currentRelation = Hero.MainHero.GetRelation(this.Owner.OwnerHero);
			}
			this._maximumIndexCanBeRecruit = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(Hero.MainHero, this.Owner.OwnerHero, -101);
			for (int i = -100; i < 100; i++)
			{
				if (index < Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(Hero.MainHero, this.Owner.OwnerHero, i))
				{
					this._requiredRelation = i;
					break;
				}
			}
			this._onClick = onClick;
			this.Index = index;
			this._onRemoveFromCart = onRemoveFromCart;
			this.RefreshValues();
		}

		// Token: 0x06001132 RID: 4402 RVA: 0x00044AB0 File Offset: 0x00042CB0
		public override void RefreshValues()
		{
			base.RefreshValues();
			if (this._character != null)
			{
				this.NameText = this._character.Name.ToString();
				GameTexts.SetVariable("LEVEL", this._character.Level);
				this.Level = GameTexts.FindText("str_level_with_value", null).ToString();
			}
		}

		// Token: 0x06001133 RID: 4403 RVA: 0x00044B0C File Offset: 0x00042D0C
		public void ExecuteRecruit()
		{
			if (this.CanBeRecruited)
			{
				this._onClick(this);
				return;
			}
			if (this.IsInCart)
			{
				this._onRemoveFromCart(this);
			}
		}

		// Token: 0x06001134 RID: 4404 RVA: 0x00044B37 File Offset: 0x00042D37
		public void ExecuteOpenEncyclopedia()
		{
			if (this.Character != null)
			{
				Campaign.Current.EncyclopediaManager.GoToLink(this.Character.EncyclopediaLink);
			}
		}

		// Token: 0x06001135 RID: 4405 RVA: 0x00044B5B File Offset: 0x00042D5B
		public void ExecuteRemoveFromCart()
		{
			if (this.IsInCart)
			{
				this._onRemoveFromCart(this);
			}
		}

		// Token: 0x06001136 RID: 4406 RVA: 0x00044B74 File Offset: 0x00042D74
		public virtual void ExecuteBeginHint()
		{
			if (this._character != null)
			{
				if (this.PlayerHasEnoughRelation)
				{
					InformationManager.ShowTooltip(typeof(CharacterObject), new object[]
					{
						this._character
					});
					return;
				}
				List<TooltipProperty> list = new List<TooltipProperty>();
				string text = "";
				list.Add(new TooltipProperty(text, this._character.Name.ToString(), 1, false, TooltipProperty.TooltipPropertyFlags.None));
				list.Add(new TooltipProperty(text, text, -1, false, TooltipProperty.TooltipPropertyFlags.None));
				GameTexts.SetVariable("LEVEL", this._character.Level);
				GameTexts.SetVariable("newline", "\n");
				list.Add(new TooltipProperty(text, GameTexts.FindText("str_level_with_value", null).ToString(), 0, false, TooltipProperty.TooltipPropertyFlags.None));
				GameTexts.SetVariable("REL1", this._currentRelation);
				GameTexts.SetVariable("REL2", this._requiredRelation);
				list.Add(new TooltipProperty(text, GameTexts.FindText("str_recruit_volunteers_not_enough_relation", null).ToString(), 0, false, TooltipProperty.TooltipPropertyFlags.None));
				InformationManager.ShowTooltip(typeof(List<TooltipProperty>), new object[]
				{
					list
				});
				return;
			}
			else
			{
				if (this.PlayerHasEnoughRelation)
				{
					MBInformationManager.ShowHint(GameTexts.FindText("str_recruit_volunteers_new_troop", null).ToString());
					return;
				}
				GameTexts.SetVariable("newline", "\n");
				GameTexts.SetVariable("REL1", this._currentRelation);
				GameTexts.SetVariable("REL2", this._requiredRelation);
				GameTexts.SetVariable("STR1", GameTexts.FindText("str_recruit_volunteers_new_troop", null));
				GameTexts.SetVariable("STR2", GameTexts.FindText("str_recruit_volunteers_not_enough_relation", null));
				MBInformationManager.ShowHint(GameTexts.FindText("str_string_newline_string", null).ToString());
				return;
			}
		}

		// Token: 0x06001137 RID: 4407 RVA: 0x00044D16 File Offset: 0x00042F16
		public virtual void ExecuteEndHint()
		{
			MBInformationManager.HideInformations();
		}

		// Token: 0x06001138 RID: 4408 RVA: 0x00044D1D File Offset: 0x00042F1D
		public void ExecuteFocus()
		{
			if (!this.IsTroopEmpty)
			{
				Action<RecruitVolunteerTroopVM> onFocused = RecruitVolunteerTroopVM.OnFocused;
				if (onFocused == null)
				{
					return;
				}
				onFocused(this);
			}
		}

		// Token: 0x06001139 RID: 4409 RVA: 0x00044D37 File Offset: 0x00042F37
		public void ExecuteUnfocus()
		{
			Action<RecruitVolunteerTroopVM> onFocused = RecruitVolunteerTroopVM.OnFocused;
			if (onFocused == null)
			{
				return;
			}
			onFocused(null);
		}

		// Token: 0x1700059D RID: 1437
		// (get) Token: 0x0600113A RID: 4410 RVA: 0x00044D49 File Offset: 0x00042F49
		// (set) Token: 0x0600113B RID: 4411 RVA: 0x00044D51 File Offset: 0x00042F51
		[DataSourceProperty]
		public string Level
		{
			get
			{
				return this._level;
			}
			set
			{
				if (value != this._level)
				{
					this._level = value;
					base.OnPropertyChangedWithValue<string>(value, "Level");
				}
			}
		}

		// Token: 0x1700059E RID: 1438
		// (get) Token: 0x0600113C RID: 4412 RVA: 0x00044D74 File Offset: 0x00042F74
		// (set) Token: 0x0600113D RID: 4413 RVA: 0x00044D7C File Offset: 0x00042F7C
		[DataSourceProperty]
		public bool CanBeRecruited
		{
			get
			{
				return this._canBeRecruited;
			}
			set
			{
				if (value != this._canBeRecruited)
				{
					this._canBeRecruited = value;
					base.OnPropertyChangedWithValue(value, "CanBeRecruited");
				}
			}
		}

		// Token: 0x1700059F RID: 1439
		// (get) Token: 0x0600113E RID: 4414 RVA: 0x00044D9A File Offset: 0x00042F9A
		// (set) Token: 0x0600113F RID: 4415 RVA: 0x00044DA2 File Offset: 0x00042FA2
		[DataSourceProperty]
		public bool IsHiglightEnabled
		{
			get
			{
				return this._isHiglightEnabled;
			}
			set
			{
				if (value != this._isHiglightEnabled)
				{
					this._isHiglightEnabled = value;
					base.OnPropertyChangedWithValue(value, "IsHiglightEnabled");
				}
			}
		}

		// Token: 0x170005A0 RID: 1440
		// (get) Token: 0x06001140 RID: 4416 RVA: 0x00044DC0 File Offset: 0x00042FC0
		// (set) Token: 0x06001141 RID: 4417 RVA: 0x00044DC8 File Offset: 0x00042FC8
		[DataSourceProperty]
		public int Wage
		{
			get
			{
				return this._wage;
			}
			set
			{
				if (value != this._wage)
				{
					this._wage = value;
					base.OnPropertyChangedWithValue(value, "Wage");
				}
			}
		}

		// Token: 0x170005A1 RID: 1441
		// (get) Token: 0x06001142 RID: 4418 RVA: 0x00044DE6 File Offset: 0x00042FE6
		// (set) Token: 0x06001143 RID: 4419 RVA: 0x00044DEE File Offset: 0x00042FEE
		[DataSourceProperty]
		public int Cost
		{
			get
			{
				return this._cost;
			}
			set
			{
				if (value != this._cost)
				{
					this._cost = value;
					base.OnPropertyChangedWithValue(value, "Cost");
				}
			}
		}

		// Token: 0x170005A2 RID: 1442
		// (get) Token: 0x06001144 RID: 4420 RVA: 0x00044E0C File Offset: 0x0004300C
		// (set) Token: 0x06001145 RID: 4421 RVA: 0x00044E14 File Offset: 0x00043014
		[DataSourceProperty]
		public bool IsInCart
		{
			get
			{
				return this._isInCart;
			}
			set
			{
				if (value != this._isInCart)
				{
					this._isInCart = value;
					base.OnPropertyChangedWithValue(value, "IsInCart");
				}
			}
		}

		// Token: 0x170005A3 RID: 1443
		// (get) Token: 0x06001146 RID: 4422 RVA: 0x00044E32 File Offset: 0x00043032
		// (set) Token: 0x06001147 RID: 4423 RVA: 0x00044E3A File Offset: 0x0004303A
		[DataSourceProperty]
		public bool IsTroopEmpty
		{
			get
			{
				return this._isTroopEmpty;
			}
			set
			{
				if (value != this._isTroopEmpty)
				{
					this._isTroopEmpty = value;
					base.OnPropertyChangedWithValue(value, "IsTroopEmpty");
				}
			}
		}

		// Token: 0x170005A4 RID: 1444
		// (get) Token: 0x06001148 RID: 4424 RVA: 0x00044E58 File Offset: 0x00043058
		// (set) Token: 0x06001149 RID: 4425 RVA: 0x00044E60 File Offset: 0x00043060
		[DataSourceProperty]
		public bool PlayerHasEnoughRelation
		{
			get
			{
				return this._playerHasEnoughRelation;
			}
			set
			{
				if (value != this._playerHasEnoughRelation)
				{
					this._playerHasEnoughRelation = value;
					base.OnPropertyChangedWithValue(value, "PlayerHasEnoughRelation");
				}
			}
		}

		// Token: 0x170005A5 RID: 1445
		// (get) Token: 0x0600114A RID: 4426 RVA: 0x00044E7E File Offset: 0x0004307E
		// (set) Token: 0x0600114B RID: 4427 RVA: 0x00044E86 File Offset: 0x00043086
		[DataSourceProperty]
		public CharacterImageIdentifierVM ImageIdentifier
		{
			get
			{
				return this._imageIdentifier;
			}
			set
			{
				if (value != this._imageIdentifier)
				{
					this._imageIdentifier = value;
					base.OnPropertyChangedWithValue<CharacterImageIdentifierVM>(value, "ImageIdentifier");
				}
			}
		}

		// Token: 0x170005A6 RID: 1446
		// (get) Token: 0x0600114C RID: 4428 RVA: 0x00044EA4 File Offset: 0x000430A4
		// (set) Token: 0x0600114D RID: 4429 RVA: 0x00044EAC File Offset: 0x000430AC
		[DataSourceProperty]
		public string NameText
		{
			get
			{
				return this._nameText;
			}
			set
			{
				if (value != this._nameText)
				{
					this._nameText = value;
					base.OnPropertyChangedWithValue<string>(value, "NameText");
				}
			}
		}

		// Token: 0x170005A7 RID: 1447
		// (get) Token: 0x0600114E RID: 4430 RVA: 0x00044ECF File Offset: 0x000430CF
		// (set) Token: 0x0600114F RID: 4431 RVA: 0x00044ED7 File Offset: 0x000430D7
		[DataSourceProperty]
		public StringItemWithHintVM TierIconData
		{
			get
			{
				return this._tierIconData;
			}
			set
			{
				if (value != this._tierIconData)
				{
					this._tierIconData = value;
					base.OnPropertyChangedWithValue<StringItemWithHintVM>(value, "TierIconData");
				}
			}
		}

		// Token: 0x170005A8 RID: 1448
		// (get) Token: 0x06001150 RID: 4432 RVA: 0x00044EF5 File Offset: 0x000430F5
		// (set) Token: 0x06001151 RID: 4433 RVA: 0x00044EFD File Offset: 0x000430FD
		[DataSourceProperty]
		public StringItemWithHintVM TypeIconData
		{
			get
			{
				return this._typeIconData;
			}
			set
			{
				if (value != this._typeIconData)
				{
					this._typeIconData = value;
					base.OnPropertyChangedWithValue<StringItemWithHintVM>(value, "TypeIconData");
				}
			}
		}

		// Token: 0x040007D2 RID: 2002
		public static Action<RecruitVolunteerTroopVM> OnFocused;

		// Token: 0x040007D3 RID: 2003
		private readonly Action<RecruitVolunteerTroopVM> _onClick;

		// Token: 0x040007D4 RID: 2004
		private readonly Action<RecruitVolunteerTroopVM> _onRemoveFromCart;

		// Token: 0x040007D5 RID: 2005
		private CharacterObject _character;

		// Token: 0x040007D6 RID: 2006
		public CharacterObject Character;

		// Token: 0x040007D7 RID: 2007
		public int Index;

		// Token: 0x040007D8 RID: 2008
		private int _maximumIndexCanBeRecruit;

		// Token: 0x040007D9 RID: 2009
		private int _requiredRelation;

		// Token: 0x040007DA RID: 2010
		public RecruitVolunteerVM Owner;

		// Token: 0x040007DB RID: 2011
		private CharacterImageIdentifierVM _imageIdentifier;

		// Token: 0x040007DC RID: 2012
		private string _nameText;

		// Token: 0x040007DD RID: 2013
		private string _level;

		// Token: 0x040007DE RID: 2014
		private bool _canBeRecruited;

		// Token: 0x040007DF RID: 2015
		private bool _isInCart;

		// Token: 0x040007E0 RID: 2016
		private int _wage;

		// Token: 0x040007E1 RID: 2017
		private int _cost;

		// Token: 0x040007E2 RID: 2018
		private bool _isTroopEmpty;

		// Token: 0x040007E3 RID: 2019
		private bool _playerHasEnoughRelation;

		// Token: 0x040007E4 RID: 2020
		private int _currentRelation;

		// Token: 0x040007E5 RID: 2021
		private bool _isHiglightEnabled;

		// Token: 0x040007E6 RID: 2022
		private StringItemWithHintVM _tierIconData;

		// Token: 0x040007E7 RID: 2023
		private StringItemWithHintVM _typeIconData;
	}
}
