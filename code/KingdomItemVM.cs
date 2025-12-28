using System;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement
{
	// Token: 0x02000064 RID: 100
	public abstract class KingdomItemVM : ViewModel
	{
		// Token: 0x06000774 RID: 1908 RVA: 0x0002322A File Offset: 0x0002142A
		protected virtual void OnSelect()
		{
			this.IsSelected = true;
		}

		// Token: 0x17000216 RID: 534
		// (get) Token: 0x06000775 RID: 1909 RVA: 0x00023233 File Offset: 0x00021433
		// (set) Token: 0x06000776 RID: 1910 RVA: 0x0002323B File Offset: 0x0002143B
		[DataSourceProperty]
		public bool IsNew
		{
			get
			{
				return this._isNew;
			}
			set
			{
				if (value != this._isNew)
				{
					this._isNew = value;
					base.OnPropertyChangedWithValue(value, "IsNew");
				}
			}
		}

		// Token: 0x17000217 RID: 535
		// (get) Token: 0x06000777 RID: 1911 RVA: 0x00023259 File Offset: 0x00021459
		// (set) Token: 0x06000778 RID: 1912 RVA: 0x00023261 File Offset: 0x00021461
		[DataSourceProperty]
		public bool IsSelected
		{
			get
			{
				return this._isSelected;
			}
			set
			{
				if (value != this._isSelected)
				{
					this._isSelected = value;
					base.OnPropertyChangedWithValue(value, "IsSelected");
				}
			}
		}

		// Token: 0x0400033C RID: 828
		private bool _isSelected;

		// Token: 0x0400033D RID: 829
		private bool _isNew;
	}
}
