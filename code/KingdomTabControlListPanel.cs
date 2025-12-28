using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TaleWorlds.MountAndBlade.GauntletUI.Widgets.Kingdom
{
	// Token: 0x02000135 RID: 309
	public class KingdomTabControlListPanel : ListPanel
	{
		// Token: 0x0600102C RID: 4140 RVA: 0x0002C35C File Offset: 0x0002A55C
		public KingdomTabControlListPanel(UIContext context) : base(context)
		{
		}

		// Token: 0x0600102D RID: 4141 RVA: 0x0002C368 File Offset: 0x0002A568
		protected override void OnLateUpdate(float dt)
		{
			base.OnLateUpdate(dt);
			this.FiefsButton.IsSelected = this.FiefsPanel.IsVisible;
			this.PoliciesButton.IsSelected = this.PoliciesPanel.IsVisible;
			this.ClansButton.IsSelected = this.ClansPanel.IsVisible;
			this.ArmiesButton.IsSelected = this.ArmiesPanel.IsVisible;
			this.DiplomacyButton.IsSelected = this.DiplomacyPanel.IsVisible;
		}

		// Token: 0x170005BC RID: 1468
		// (get) Token: 0x0600102E RID: 4142 RVA: 0x0002C3EA File Offset: 0x0002A5EA
		// (set) Token: 0x0600102F RID: 4143 RVA: 0x0002C3F2 File Offset: 0x0002A5F2
		[Editor(false)]
		public Widget DiplomacyPanel
		{
			get
			{
				return this._diplomacyPanel;
			}
			set
			{
				if (this._diplomacyPanel != value)
				{
					this._diplomacyPanel = value;
					base.OnPropertyChanged<Widget>(value, "DiplomacyPanel");
				}
			}
		}

		// Token: 0x170005BD RID: 1469
		// (get) Token: 0x06001030 RID: 4144 RVA: 0x0002C410 File Offset: 0x0002A610
		// (set) Token: 0x06001031 RID: 4145 RVA: 0x0002C418 File Offset: 0x0002A618
		[Editor(false)]
		public Widget ArmiesPanel
		{
			get
			{
				return this._armiesPanel;
			}
			set
			{
				if (this._armiesPanel != value)
				{
					this._armiesPanel = value;
					base.OnPropertyChanged<Widget>(value, "ArmiesPanel");
				}
			}
		}

		// Token: 0x170005BE RID: 1470
		// (get) Token: 0x06001032 RID: 4146 RVA: 0x0002C436 File Offset: 0x0002A636
		// (set) Token: 0x06001033 RID: 4147 RVA: 0x0002C43E File Offset: 0x0002A63E
		[Editor(false)]
		public Widget ClansPanel
		{
			get
			{
				return this._clansPanel;
			}
			set
			{
				if (this._clansPanel != value)
				{
					this._clansPanel = value;
					base.OnPropertyChanged<Widget>(value, "ClansPanel");
				}
			}
		}

		// Token: 0x170005BF RID: 1471
		// (get) Token: 0x06001034 RID: 4148 RVA: 0x0002C45C File Offset: 0x0002A65C
		// (set) Token: 0x06001035 RID: 4149 RVA: 0x0002C464 File Offset: 0x0002A664
		[Editor(false)]
		public Widget PoliciesPanel
		{
			get
			{
				return this._policiesPanel;
			}
			set
			{
				if (this._policiesPanel != value)
				{
					this._policiesPanel = value;
					base.OnPropertyChanged<Widget>(value, "PoliciesPanel");
				}
			}
		}

		// Token: 0x170005C0 RID: 1472
		// (get) Token: 0x06001036 RID: 4150 RVA: 0x0002C482 File Offset: 0x0002A682
		// (set) Token: 0x06001037 RID: 4151 RVA: 0x0002C48A File Offset: 0x0002A68A
		[Editor(false)]
		public Widget FiefsPanel
		{
			get
			{
				return this._fiefsPanel;
			}
			set
			{
				if (this._fiefsPanel != value)
				{
					this._fiefsPanel = value;
					base.OnPropertyChanged<Widget>(value, "FiefsPanel");
				}
			}
		}

		// Token: 0x170005C1 RID: 1473
		// (get) Token: 0x06001038 RID: 4152 RVA: 0x0002C4A8 File Offset: 0x0002A6A8
		// (set) Token: 0x06001039 RID: 4153 RVA: 0x0002C4B0 File Offset: 0x0002A6B0
		[Editor(false)]
		public ButtonWidget FiefsButton
		{
			get
			{
				return this._fiefsButton;
			}
			set
			{
				if (this._fiefsButton != value)
				{
					this._fiefsButton = value;
					base.OnPropertyChanged<ButtonWidget>(value, "FiefsButton");
				}
			}
		}

		// Token: 0x170005C2 RID: 1474
		// (get) Token: 0x0600103A RID: 4154 RVA: 0x0002C4CE File Offset: 0x0002A6CE
		// (set) Token: 0x0600103B RID: 4155 RVA: 0x0002C4D6 File Offset: 0x0002A6D6
		[Editor(false)]
		public ButtonWidget PoliciesButton
		{
			get
			{
				return this._policiesButton;
			}
			set
			{
				if (this._policiesButton != value)
				{
					this._policiesButton = value;
					base.OnPropertyChanged<ButtonWidget>(value, "PoliciesButton");
				}
			}
		}

		// Token: 0x170005C3 RID: 1475
		// (get) Token: 0x0600103C RID: 4156 RVA: 0x0002C4F4 File Offset: 0x0002A6F4
		// (set) Token: 0x0600103D RID: 4157 RVA: 0x0002C4FC File Offset: 0x0002A6FC
		[Editor(false)]
		public ButtonWidget ClansButton
		{
			get
			{
				return this._clansButton;
			}
			set
			{
				if (this._clansButton != value)
				{
					this._clansButton = value;
					base.OnPropertyChanged<ButtonWidget>(value, "ClansButton");
				}
			}
		}

		// Token: 0x170005C4 RID: 1476
		// (get) Token: 0x0600103E RID: 4158 RVA: 0x0002C51A File Offset: 0x0002A71A
		// (set) Token: 0x0600103F RID: 4159 RVA: 0x0002C522 File Offset: 0x0002A722
		[Editor(false)]
		public ButtonWidget ArmiesButton
		{
			get
			{
				return this._armiesButton;
			}
			set
			{
				if (this._armiesButton != value)
				{
					this._armiesButton = value;
					base.OnPropertyChanged<ButtonWidget>(value, "ArmiesButton");
				}
			}
		}

		// Token: 0x170005C5 RID: 1477
		// (get) Token: 0x06001040 RID: 4160 RVA: 0x0002C540 File Offset: 0x0002A740
		// (set) Token: 0x06001041 RID: 4161 RVA: 0x0002C548 File Offset: 0x0002A748
		[Editor(false)]
		public ButtonWidget DiplomacyButton
		{
			get
			{
				return this._diplomacyButton;
			}
			set
			{
				if (this._diplomacyButton != value)
				{
					this._diplomacyButton = value;
					base.OnPropertyChanged<ButtonWidget>(value, "DiplomacyButton");
				}
			}
		}

		// Token: 0x04000754 RID: 1876
		private Widget _armiesPanel;

		// Token: 0x04000755 RID: 1877
		private Widget _clansPanel;

		// Token: 0x04000756 RID: 1878
		private Widget _policiesPanel;

		// Token: 0x04000757 RID: 1879
		private Widget _fiefsPanel;

		// Token: 0x04000758 RID: 1880
		private Widget _diplomacyPanel;

		// Token: 0x04000759 RID: 1881
		private ButtonWidget _fiefsButton;

		// Token: 0x0400075A RID: 1882
		private ButtonWidget _clansButton;

		// Token: 0x0400075B RID: 1883
		private ButtonWidget _policiesButton;

		// Token: 0x0400075C RID: 1884
		private ButtonWidget _armiesButton;

		// Token: 0x0400075D RID: 1885
		private ButtonWidget _diplomacyButton;
	}
}
