using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment
{
	// Token: 0x020000B3 RID: 179
	public class RecruitVolunteerVM : ViewModel
	{
		// Token: 0x170005A9 RID: 1449
		// (get) Token: 0x06001152 RID: 4434 RVA: 0x00044F1B File Offset: 0x0004311B
		// (set) Token: 0x06001153 RID: 4435 RVA: 0x00044F23 File Offset: 0x00043123
		public Hero OwnerHero { get; private set; }

		// Token: 0x170005AA RID: 1450
		// (get) Token: 0x06001154 RID: 4436 RVA: 0x00044F2C File Offset: 0x0004312C
		// (set) Token: 0x06001155 RID: 4437 RVA: 0x00044F34 File Offset: 0x00043134
		public List<CharacterObject> VolunteerTroops { get; private set; }

		// Token: 0x170005AB RID: 1451
		// (get) Token: 0x06001156 RID: 4438 RVA: 0x00044F3D File Offset: 0x0004313D
		public int GoldCost { get; }

		// Token: 0x06001157 RID: 4439 RVA: 0x00044F48 File Offset: 0x00043148
		public RecruitVolunteerVM(Hero owner, List<CharacterObject> troops, Action<RecruitVolunteerVM, RecruitVolunteerTroopVM> onRecruit, Action<RecruitVolunteerVM, RecruitVolunteerTroopVM> onRemoveFromCart)
		{
			this.OwnerHero = owner;
			this.VolunteerTroops = troops;
			this._onRecruit = onRecruit;
			this._onRemoveFromCart = onRemoveFromCart;
			this.Owner = new RecruitVolunteerOwnerVM(owner, (int)owner.GetRelationWithPlayer());
			this.Troops = new MBBindingList<RecruitVolunteerTroopVM>();
			int num = 0;
			foreach (CharacterObject characterObject in troops)
			{
				RecruitVolunteerTroopVM recruitVolunteerTroopVM = new RecruitVolunteerTroopVM(this, characterObject, num, new Action<RecruitVolunteerTroopVM>(this.ExecuteRecruit), new Action<RecruitVolunteerTroopVM>(this.ExecuteRemoveFromCart));
				recruitVolunteerTroopVM.CanBeRecruited = false;
				recruitVolunteerTroopVM.PlayerHasEnoughRelation = false;
				if (HeroHelper.HeroCanRecruitFromHero(Hero.MainHero, this.OwnerHero, num))
				{
					recruitVolunteerTroopVM.PlayerHasEnoughRelation = true;
					if (characterObject != null)
					{
						recruitVolunteerTroopVM.CanBeRecruited = true;
					}
				}
				num++;
				this.Troops.Add(recruitVolunteerTroopVM);
			}
			this.RecruitHint = new HintViewModel();
			this.RefreshProperties();
		}

		// Token: 0x06001158 RID: 4440 RVA: 0x00045048 File Offset: 0x00043248
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.RefreshProperties();
			RecruitVolunteerOwnerVM owner = this.Owner;
			if (owner != null)
			{
				owner.RefreshValues();
			}
			this.Troops.ApplyActionOnAllItems(delegate(RecruitVolunteerTroopVM x)
			{
				x.RefreshValues();
			});
		}

		// Token: 0x06001159 RID: 4441 RVA: 0x0004509C File Offset: 0x0004329C
		public void ExecuteRecruit(RecruitVolunteerTroopVM troop)
		{
			this._onRecruit(this, troop);
			this.RefreshProperties();
		}

		// Token: 0x0600115A RID: 4442 RVA: 0x000450B1 File Offset: 0x000432B1
		public void ExecuteRemoveFromCart(RecruitVolunteerTroopVM troop)
		{
			this._onRemoveFromCart(this, troop);
			this.RefreshProperties();
		}

		// Token: 0x0600115B RID: 4443 RVA: 0x000450C8 File Offset: 0x000432C8
		private void RefreshProperties()
		{
			this.RecruitText = this.GoldCost.ToString();
			if (this.RecruitableNumber == 0)
			{
				this.QuantityText = GameTexts.FindText("str_none", null).ToString();
				return;
			}
			GameTexts.SetVariable("QUANTITY", this.RecruitableNumber.ToString());
			this.QuantityText = GameTexts.FindText("str_x_quantity", null).ToString();
		}

		// Token: 0x0600115C RID: 4444 RVA: 0x00045134 File Offset: 0x00043334
		public void OnRecruitMoveToCart(RecruitVolunteerTroopVM troop)
		{
			MBInformationManager.HideInformations();
			this.Troops.RemoveAt(troop.Index);
			RecruitVolunteerTroopVM recruitVolunteerTroopVM = new RecruitVolunteerTroopVM(this, null, troop.Index, new Action<RecruitVolunteerTroopVM>(this.ExecuteRecruit), new Action<RecruitVolunteerTroopVM>(this.ExecuteRemoveFromCart));
			recruitVolunteerTroopVM.IsTroopEmpty = true;
			recruitVolunteerTroopVM.PlayerHasEnoughRelation = true;
			this.Troops.Insert(troop.Index, recruitVolunteerTroopVM);
		}

		// Token: 0x0600115D RID: 4445 RVA: 0x0004519D File Offset: 0x0004339D
		public void OnRecruitRemovedFromCart(RecruitVolunteerTroopVM troop)
		{
			this.Troops.RemoveAt(troop.Index);
			this.Troops.Insert(troop.Index, troop);
		}

		// Token: 0x170005AC RID: 1452
		// (get) Token: 0x0600115E RID: 4446 RVA: 0x000451C2 File Offset: 0x000433C2
		// (set) Token: 0x0600115F RID: 4447 RVA: 0x000451CA File Offset: 0x000433CA
		[DataSourceProperty]
		public MBBindingList<RecruitVolunteerTroopVM> Troops
		{
			get
			{
				return this._troops;
			}
			set
			{
				if (value != this._troops)
				{
					this._troops = value;
					base.OnPropertyChangedWithValue<MBBindingList<RecruitVolunteerTroopVM>>(value, "Troops");
				}
			}
		}

		// Token: 0x170005AD RID: 1453
		// (get) Token: 0x06001160 RID: 4448 RVA: 0x000451E8 File Offset: 0x000433E8
		// (set) Token: 0x06001161 RID: 4449 RVA: 0x000451F0 File Offset: 0x000433F0
		[DataSourceProperty]
		public RecruitVolunteerOwnerVM Owner
		{
			get
			{
				return this._owner;
			}
			set
			{
				if (value != this._owner)
				{
					this._owner = value;
					base.OnPropertyChangedWithValue<RecruitVolunteerOwnerVM>(value, "Owner");
				}
			}
		}

		// Token: 0x170005AE RID: 1454
		// (get) Token: 0x06001162 RID: 4450 RVA: 0x0004520E File Offset: 0x0004340E
		// (set) Token: 0x06001163 RID: 4451 RVA: 0x00045216 File Offset: 0x00043416
		[DataSourceProperty]
		public bool CanRecruit
		{
			get
			{
				return this._canRecruit;
			}
			set
			{
				if (value != this._canRecruit)
				{
					this._canRecruit = value;
					base.OnPropertyChangedWithValue(value, "CanRecruit");
				}
			}
		}

		// Token: 0x170005AF RID: 1455
		// (get) Token: 0x06001164 RID: 4452 RVA: 0x00045234 File Offset: 0x00043434
		// (set) Token: 0x06001165 RID: 4453 RVA: 0x0004523C File Offset: 0x0004343C
		[DataSourceProperty]
		public bool ButtonIsVisible
		{
			get
			{
				return this._buttonIsVisible;
			}
			set
			{
				if (value != this._buttonIsVisible)
				{
					this._buttonIsVisible = value;
					base.OnPropertyChangedWithValue(value, "ButtonIsVisible");
				}
			}
		}

		// Token: 0x170005B0 RID: 1456
		// (get) Token: 0x06001166 RID: 4454 RVA: 0x0004525A File Offset: 0x0004345A
		// (set) Token: 0x06001167 RID: 4455 RVA: 0x00045262 File Offset: 0x00043462
		[DataSourceProperty]
		public string QuantityText
		{
			get
			{
				return this._quantityText;
			}
			set
			{
				if (value != this._quantityText)
				{
					this._quantityText = value;
					base.OnPropertyChangedWithValue<string>(value, "QuantityText");
				}
			}
		}

		// Token: 0x170005B1 RID: 1457
		// (get) Token: 0x06001168 RID: 4456 RVA: 0x00045285 File Offset: 0x00043485
		// (set) Token: 0x06001169 RID: 4457 RVA: 0x0004528D File Offset: 0x0004348D
		[DataSourceProperty]
		public string RecruitText
		{
			get
			{
				return this._recruitText;
			}
			set
			{
				if (value != this._recruitText)
				{
					this._recruitText = value;
					base.OnPropertyChangedWithValue<string>(value, "RecruitText");
				}
			}
		}

		// Token: 0x170005B2 RID: 1458
		// (get) Token: 0x0600116A RID: 4458 RVA: 0x000452B0 File Offset: 0x000434B0
		// (set) Token: 0x0600116B RID: 4459 RVA: 0x000452B8 File Offset: 0x000434B8
		[DataSourceProperty]
		public HintViewModel RecruitHint
		{
			get
			{
				return this._recruitHint;
			}
			set
			{
				if (value != this._recruitHint)
				{
					this._recruitHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "RecruitHint");
				}
			}
		}

		// Token: 0x040007EB RID: 2027
		public int RecruitableNumber;

		// Token: 0x040007EC RID: 2028
		private readonly Action<RecruitVolunteerVM, RecruitVolunteerTroopVM> _onRecruit;

		// Token: 0x040007ED RID: 2029
		private readonly Action<RecruitVolunteerVM, RecruitVolunteerTroopVM> _onRemoveFromCart;

		// Token: 0x040007EE RID: 2030
		private string _quantityText;

		// Token: 0x040007EF RID: 2031
		private string _recruitText;

		// Token: 0x040007F0 RID: 2032
		private bool _canRecruit;

		// Token: 0x040007F1 RID: 2033
		private bool _buttonIsVisible;

		// Token: 0x040007F2 RID: 2034
		private HintViewModel _recruitHint;

		// Token: 0x040007F3 RID: 2035
		private RecruitVolunteerOwnerVM _owner;

		// Token: 0x040007F4 RID: 2036
		private MBBindingList<RecruitVolunteerTroopVM> _troops;
	}
}
