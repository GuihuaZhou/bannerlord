using System;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement
{
	// Token: 0x02000062 RID: 98
	public abstract class KingdomCategoryVM : ViewModel
	{
		// Token: 0x17000201 RID: 513
		// (get) Token: 0x0600073A RID: 1850 RVA: 0x00022A85 File Offset: 0x00020C85
		// (set) Token: 0x0600073B RID: 1851 RVA: 0x00022A8D File Offset: 0x00020C8D
		[DataSourceProperty]
		public string CategoryNameText
		{
			get
			{
				return this._categoryNameText;
			}
			set
			{
				if (value != this._categoryNameText)
				{
					this._categoryNameText = value;
					base.OnPropertyChanged("NameText");
				}
			}
		}

		// Token: 0x17000202 RID: 514
		// (get) Token: 0x0600073C RID: 1852 RVA: 0x00022AAF File Offset: 0x00020CAF
		// (set) Token: 0x0600073D RID: 1853 RVA: 0x00022AB7 File Offset: 0x00020CB7
		[DataSourceProperty]
		public string NoItemSelectedText
		{
			get
			{
				return this._noItemSelectedText;
			}
			set
			{
				if (value != this._noItemSelectedText)
				{
					this._noItemSelectedText = value;
					base.OnPropertyChangedWithValue<string>(value, "NoItemSelectedText");
				}
			}
		}

		// Token: 0x17000203 RID: 515
		// (get) Token: 0x0600073E RID: 1854 RVA: 0x00022ADA File Offset: 0x00020CDA
		// (set) Token: 0x0600073F RID: 1855 RVA: 0x00022AE2 File Offset: 0x00020CE2
		[DataSourceProperty]
		public bool IsAcceptableItemSelected
		{
			get
			{
				return this._isAcceptableItemSelected;
			}
			set
			{
				if (value != this._isAcceptableItemSelected)
				{
					this._isAcceptableItemSelected = value;
					base.OnPropertyChangedWithValue(value, "IsAcceptableItemSelected");
				}
			}
		}

		// Token: 0x17000204 RID: 516
		// (get) Token: 0x06000740 RID: 1856 RVA: 0x00022B00 File Offset: 0x00020D00
		// (set) Token: 0x06000741 RID: 1857 RVA: 0x00022B08 File Offset: 0x00020D08
		[DataSourceProperty]
		public int NotificationCount
		{
			get
			{
				return this._notificationCount;
			}
			set
			{
				if (value != this._notificationCount)
				{
					this._notificationCount = value;
					base.OnPropertyChanged("NotificationCount");
				}
			}
		}

		// Token: 0x17000205 RID: 517
		// (get) Token: 0x06000742 RID: 1858 RVA: 0x00022B25 File Offset: 0x00020D25
		// (set) Token: 0x06000743 RID: 1859 RVA: 0x00022B2D File Offset: 0x00020D2D
		[DataSourceProperty]
		public bool Show
		{
			get
			{
				return this._show;
			}
			set
			{
				if (value != this._show)
				{
					this._show = value;
					base.OnPropertyChanged("Show");
				}
			}
		}

		// Token: 0x04000322 RID: 802
		private int _notificationCount;

		// Token: 0x04000323 RID: 803
		private string _categoryNameText;

		// Token: 0x04000324 RID: 804
		private string _noItemSelectedText;

		// Token: 0x04000325 RID: 805
		private bool _show;

		// Token: 0x04000326 RID: 806
		private bool _isAcceptableItemSelected;
	}
}
