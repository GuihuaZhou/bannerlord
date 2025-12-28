using System;
using System.Collections.Generic;
using TaleWorlds.GauntletUI.GamepadNavigation;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace TaleWorlds.GauntletUI.BaseTypes
{
	// Token: 0x02000061 RID: 97
	public class ScrollablePanel : Widget
	{
		// Token: 0x1400000E RID: 14
		// (add) Token: 0x06000677 RID: 1655 RVA: 0x0001C83C File Offset: 0x0001AA3C
		// (remove) Token: 0x06000678 RID: 1656 RVA: 0x0001C874 File Offset: 0x0001AA74
		public event Action<float> OnScroll;

		// Token: 0x170001CA RID: 458
		// (get) Token: 0x06000679 RID: 1657 RVA: 0x0001C8A9 File Offset: 0x0001AAA9
		// (set) Token: 0x0600067A RID: 1658 RVA: 0x0001C8B1 File Offset: 0x0001AAB1
		public Widget ClipRect { get; set; }

		// Token: 0x170001CB RID: 459
		// (get) Token: 0x0600067B RID: 1659 RVA: 0x0001C8BA File Offset: 0x0001AABA
		// (set) Token: 0x0600067C RID: 1660 RVA: 0x0001C8C2 File Offset: 0x0001AAC2
		public Widget InnerPanel
		{
			get
			{
				return this._innerPanel;
			}
			set
			{
				if (value != this._innerPanel)
				{
					this._innerPanel = value;
					this.OnInnerPanelValueChanged();
				}
			}
		}

		// Token: 0x170001CC RID: 460
		// (get) Token: 0x0600067D RID: 1661 RVA: 0x0001C8DA File Offset: 0x0001AADA
		public ScrollbarWidget ActiveScrollbar
		{
			get
			{
				return this.VerticalScrollbar ?? this.HorizontalScrollbar;
			}
		}

		// Token: 0x170001CD RID: 461
		// (get) Token: 0x0600067E RID: 1662 RVA: 0x0001C8EC File Offset: 0x0001AAEC
		// (set) Token: 0x0600067F RID: 1663 RVA: 0x0001C8F4 File Offset: 0x0001AAF4
		public bool UpdateScrollbarVisibility { get; set; } = true;

		// Token: 0x170001CE RID: 462
		// (get) Token: 0x06000680 RID: 1664 RVA: 0x0001C8FD File Offset: 0x0001AAFD
		// (set) Token: 0x06000681 RID: 1665 RVA: 0x0001C905 File Offset: 0x0001AB05
		public Widget FixedHeader { get; set; }

		// Token: 0x170001CF RID: 463
		// (get) Token: 0x06000682 RID: 1666 RVA: 0x0001C90E File Offset: 0x0001AB0E
		// (set) Token: 0x06000683 RID: 1667 RVA: 0x0001C916 File Offset: 0x0001AB16
		public Widget ScrolledHeader { get; set; }

		// Token: 0x06000684 RID: 1668 RVA: 0x0001C920 File Offset: 0x0001AB20
		public ScrollablePanel(UIContext context) : base(context)
		{
			this._verticalScrollbarInterpolationController = new ScrollablePanel.ScrollbarInterpolationController();
			this._horizontalScrollbarInterpolationController = new ScrollablePanel.ScrollbarInterpolationController();
		}

		// Token: 0x06000685 RID: 1669 RVA: 0x0001C979 File Offset: 0x0001AB79
		public void ResetTweenSpeed()
		{
			this._verticalScrollVelocity = 0f;
			this._horizontalScrollVelocity = 0f;
		}

		// Token: 0x06000686 RID: 1670 RVA: 0x0001C991 File Offset: 0x0001AB91
		protected override bool OnPreviewMouseScroll()
		{
			return !this.OnlyAcceptScrollEventIfCanScroll || this._canScrollHorizontal || this._canScrollVertical;
		}

		// Token: 0x06000687 RID: 1671 RVA: 0x0001C9AC File Offset: 0x0001ABAC
		protected override bool OnPreviewRightStickMovement()
		{
			return (!this.OnlyAcceptScrollEventIfCanScroll || this._canScrollHorizontal || this._canScrollVertical) && !GauntletGamepadNavigationManager.Instance.IsCursorMovingForNavigation && !GauntletGamepadNavigationManager.Instance.AnyWidgetUsingNavigation && base.EventManager.HoveredView != null && (base.CheckIsMyChildRecursive(base.EventManager.HoveredView) || (this.ActiveScrollbar != null && this.ActiveScrollbar.CheckIsMyChildRecursive(base.EventManager.HoveredView)));
		}

		// Token: 0x06000688 RID: 1672 RVA: 0x0001CA30 File Offset: 0x0001AC30
		protected internal override void OnMouseScroll()
		{
			float num = base.EventManager.DeltaMouseScroll * this.MouseScrollSpeed;
			if ((Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift) || this.VerticalScrollbar == null) && this.HorizontalScrollbar != null)
			{
				this._horizontalScrollVelocity += num;
			}
			else if (this.VerticalScrollbar != null)
			{
				this._verticalScrollVelocity += num;
			}
			this.StopAllInterpolations();
			Action<float> onScroll = this.OnScroll;
			if (onScroll == null)
			{
				return;
			}
			onScroll(num);
		}

		// Token: 0x06000689 RID: 1673 RVA: 0x0001CAB0 File Offset: 0x0001ACB0
		protected internal override void OnRightStickMovement()
		{
			float num = -base.EventManager.RightStickHorizontalScrollAmount * this.ControllerScrollSpeed;
			float num2 = base.EventManager.RightStickVerticalScrollAmount * this.ControllerScrollSpeed;
			this._horizontalScrollVelocity += num;
			this._verticalScrollVelocity += num2;
			this.StopAllInterpolations();
			Action<float> onScroll = this.OnScroll;
			if (onScroll == null)
			{
				return;
			}
			onScroll(Mathf.Max(num, num2));
		}

		// Token: 0x0600068A RID: 1674 RVA: 0x0001CB1D File Offset: 0x0001AD1D
		private void StopAllInterpolations()
		{
			this._verticalScrollbarInterpolationController.StopInterpolation();
			this._horizontalScrollbarInterpolationController.StopInterpolation();
		}

		// Token: 0x0600068B RID: 1675 RVA: 0x0001CB35 File Offset: 0x0001AD35
		private void OnInnerPanelChildAddedEventFire(Widget widget, string eventName, object[] eventArgs)
		{
			if ((eventName == "ItemAdd" || eventName == "AfterItemRemove") && eventArgs.Length != 0 && eventArgs[0] is ScrollablePanelFixedHeaderWidget)
			{
				this.RefreshFixedHeaders();
				this.StopAllInterpolations();
			}
		}

		// Token: 0x0600068C RID: 1676 RVA: 0x0001CB6B File Offset: 0x0001AD6B
		private void OnInnerPanelValueChanged()
		{
			if (this.InnerPanel != null)
			{
				this.InnerPanel.EventFire += this.OnInnerPanelChildAddedEventFire;
				this.RefreshFixedHeaders();
				this.StopAllInterpolations();
			}
		}

		// Token: 0x0600068D RID: 1677 RVA: 0x0001CB98 File Offset: 0x0001AD98
		private void OnFixedHeaderPropertyChangedEventFire(Widget widget, string eventName, object[] eventArgs)
		{
			if (eventName == "FixedHeaderPropertyChanged")
			{
				this.RefreshFixedHeaders();
				this.StopAllInterpolations();
			}
		}

		// Token: 0x0600068E RID: 1678 RVA: 0x0001CBB4 File Offset: 0x0001ADB4
		private void RefreshFixedHeaders()
		{
			foreach (ScrollablePanelFixedHeaderWidget scrollablePanelFixedHeaderWidget in this._fixedHeaders)
			{
				scrollablePanelFixedHeaderWidget.EventFire -= this.OnFixedHeaderPropertyChangedEventFire;
			}
			this._fixedHeaders.Clear();
			float num = 0f;
			for (int i = 0; i < this.InnerPanel.ChildCount; i++)
			{
				ScrollablePanelFixedHeaderWidget scrollablePanelFixedHeaderWidget2;
				if ((scrollablePanelFixedHeaderWidget2 = (this.InnerPanel.GetChild(i) as ScrollablePanelFixedHeaderWidget)) != null && scrollablePanelFixedHeaderWidget2.IsRelevant)
				{
					num += scrollablePanelFixedHeaderWidget2.AdditionalTopOffset;
					scrollablePanelFixedHeaderWidget2.TopOffset = num;
					num += scrollablePanelFixedHeaderWidget2.SuggestedHeight;
					this._fixedHeaders.Add(scrollablePanelFixedHeaderWidget2);
					scrollablePanelFixedHeaderWidget2.EventFire += this.OnFixedHeaderPropertyChangedEventFire;
				}
			}
			float num2 = 0f;
			for (int j = this._fixedHeaders.Count - 1; j >= 0; j--)
			{
				num2 += this._fixedHeaders[j].AdditionalBottomOffset;
				this._fixedHeaders[j].BottomOffset = num2;
				num2 += this._fixedHeaders[j].SuggestedHeight;
			}
		}

		// Token: 0x0600068F RID: 1679 RVA: 0x0001CCF4 File Offset: 0x0001AEF4
		private void AdjustVerticalScrollBar()
		{
			if (this.VerticalScrollbar != null)
			{
				if (this.InnerPanel.VerticalAlignment == VerticalAlignment.Bottom)
				{
					this.VerticalScrollbar.ValueFloat = this.VerticalScrollbar.MaxValue - this.InnerPanel.ScaledPositionOffset.Y;
					return;
				}
				this.VerticalScrollbar.ValueFloat = -this.InnerPanel.ScaledPositionOffset.Y;
			}
		}

		// Token: 0x06000690 RID: 1680 RVA: 0x0001CD5C File Offset: 0x0001AF5C
		private void AdjustHorizontalScrollBar()
		{
			if (this.HorizontalScrollbar != null)
			{
				if (this.InnerPanel.HorizontalAlignment == HorizontalAlignment.Right)
				{
					this.HorizontalScrollbar.ValueFloat = this.HorizontalScrollbar.MaxValue - this.InnerPanel.ScaledPositionOffset.X;
					return;
				}
				this.HorizontalScrollbar.ValueFloat = -this.InnerPanel.ScaledPositionOffset.X;
			}
		}

		// Token: 0x06000691 RID: 1681 RVA: 0x0001CDC3 File Offset: 0x0001AFC3
		protected override void OnLateUpdate(float dt)
		{
			base.OnLateUpdate(dt);
			this.UpdateScrollInterpolation(dt);
			this.UpdateScrollablePanel(dt);
		}

		// Token: 0x06000692 RID: 1682 RVA: 0x0001CDDA File Offset: 0x0001AFDA
		protected void SetActiveCursor(UIContext.MouseCursors cursor)
		{
			base.Context.ActiveCursorOfContext = cursor;
		}

		// Token: 0x06000693 RID: 1683 RVA: 0x0001CDE8 File Offset: 0x0001AFE8
		private void UpdateScrollInterpolation(float dt)
		{
			this._verticalScrollbarInterpolationController.Tick(dt);
			this._horizontalScrollbarInterpolationController.Tick(dt);
		}

		// Token: 0x06000694 RID: 1684 RVA: 0x0001CE04 File Offset: 0x0001B004
		private void UpdateScrollablePanel(float dt)
		{
			if (this.InnerPanel != null && this.ClipRect != null)
			{
				this._canScrollHorizontal = false;
				this._canScrollVertical = false;
				if (this.HorizontalScrollbar != null)
				{
					bool flag = base.IsVisible;
					bool flag2 = base.IsVisible;
					float num = this.InnerPanel.ScaledPositionXOffset - this.InnerPanel.Left;
					float num2 = this.HorizontalScrollbar.ValueFloat;
					this.InnerPanel.ScaledPositionXOffset = -num2;
					this._scrollOffset = this.InnerPanel.ScaledPositionOffset.X;
					this.HorizontalScrollbar.ReverseDirection = false;
					this.HorizontalScrollbar.MinValue = 0f;
					if (this.FixedHeader != null && this.ScrolledHeader != null)
					{
						if (this.FixedHeader.GlobalPosition.Y > this.ScrolledHeader.GlobalPosition.Y)
						{
							this.FixedHeader.IsVisible = true;
						}
						else
						{
							this.FixedHeader.IsVisible = false;
						}
					}
					float num3 = this.InnerPanel.Size.X + this.InnerPanel.ScaledMarginLeft + this.InnerPanel.ScaledMarginRight;
					if (MathF.Floor(num3) > MathF.Ceiling(this.ClipRect.Size.X))
					{
						this._canScrollHorizontal = true;
						this.HorizontalScrollbar.MaxValue = MathF.Max(1f, num3 - this.ClipRect.Size.X);
						if (this._horizontalScrollbarChangedThisFrame && this.ReverseInitialScrollBarAlignment)
						{
							num2 = this.HorizontalScrollbar.MaxValue;
						}
						if (this.InnerPanel.HorizontalAlignment == HorizontalAlignment.Right)
						{
							this._scrollOffset = this.HorizontalScrollbar.MaxValue - num2;
						}
						if (this.AutoAdjustScrollbarHandleSize && this.HorizontalScrollbar.Handle != null)
						{
							this.HorizontalScrollbar.Handle.ScaledSuggestedWidth = this.HorizontalScrollbar.Size.X * (this.ClipRect.Size.X / num3);
						}
						if (MathF.Abs(this._horizontalScrollVelocity) > 0.01f)
						{
							this._scrollOffset += this._horizontalScrollVelocity * (dt / 0.016f) * (Input.Resolution.X / 1920f);
							this._horizontalScrollVelocity = MathF.Lerp(this._horizontalScrollVelocity, 0f, 1f - MathF.Pow(0.001f, dt), 1E-05f);
						}
						else
						{
							this._horizontalScrollVelocity = 0f;
						}
						this.InnerPanel.ScaledPositionXOffset = this._scrollOffset;
						this.AdjustHorizontalScrollBar();
						if (this.InnerPanel.HorizontalAlignment == HorizontalAlignment.Center)
						{
							this.InnerPanel.ScaledPositionXOffset += num;
						}
					}
					else
					{
						this.HorizontalScrollbar.Handle.ScaledSuggestedWidth = this.HorizontalScrollbar.Size.X;
						this.InnerPanel.ScaledPositionXOffset = 0f;
						this.HorizontalScrollbar.ValueFloat = 0f;
						this._horizontalScrollVelocity = 0f;
						this._scrollOffset = 0f;
						if (this.AutoHideScrollBars)
						{
							flag = false;
						}
						if (this.AutoHideScrollBarHandle)
						{
							flag2 = false;
						}
					}
					if (this.UpdateScrollbarVisibility)
					{
						this.HorizontalScrollbar.IsVisible = flag;
						this.HorizontalScrollbar.Handle.IsVisible = (flag2 && flag);
					}
				}
				if (this.VerticalScrollbar != null)
				{
					float num4 = this.VerticalScrollbar.ValueFloat;
					bool flag3 = base.IsVisible;
					bool flag4 = base.IsVisible;
					this.InnerPanel.ScaledPositionYOffset = -num4;
					this._scrollOffset = this.InnerPanel.ScaledPositionOffset.Y;
					this.VerticalScrollbar.ReverseDirection = false;
					this.VerticalScrollbar.MinValue = 0f;
					if (this.FixedHeader != null && this.ScrolledHeader != null)
					{
						if (this.FixedHeader.GlobalPosition.Y >= this.ScrolledHeader.GlobalPosition.Y)
						{
							this.FixedHeader.IsVisible = true;
						}
						else
						{
							this.FixedHeader.IsVisible = false;
						}
					}
					float num5 = this.InnerPanel.Size.Y + this.InnerPanel.ScaledMarginTop + this.InnerPanel.ScaledMarginBottom;
					if (MathF.Floor(num5) > MathF.Ceiling(this.ClipRect.Size.Y))
					{
						this._canScrollVertical = true;
						this.VerticalScrollbar.MaxValue = MathF.Max(1f, num5 - this.ClipRect.Size.Y);
						if (this._verticalScrollbarChangedThisFrame && this.ReverseInitialScrollBarAlignment)
						{
							num4 = this.VerticalScrollbar.MaxValue;
						}
						if (this.InnerPanel.VerticalAlignment == VerticalAlignment.Bottom)
						{
							this._scrollOffset = this.VerticalScrollbar.MaxValue - num4;
						}
						if (this.AutoAdjustScrollbarHandleSize && this.VerticalScrollbar.Handle != null)
						{
							this.VerticalScrollbar.Handle.ScaledSuggestedHeight = this.VerticalScrollbar.Size.Y * (this.ClipRect.Size.Y / num5);
						}
						if (MathF.Abs(this._verticalScrollVelocity) > 0.01f)
						{
							this._scrollOffset += this._verticalScrollVelocity * (dt / 0.016f) * (Input.Resolution.Y / 1080f);
							this._verticalScrollVelocity = MathF.Lerp(this._verticalScrollVelocity, 0f, 1f - MathF.Pow(0.001f, dt), 1E-05f);
						}
						else
						{
							this._verticalScrollVelocity = 0f;
						}
						this.InnerPanel.ScaledPositionYOffset = this._scrollOffset;
						this.AdjustVerticalScrollBar();
					}
					else
					{
						if (this.AutoAdjustScrollbarHandleSize && this.VerticalScrollbar.Handle != null)
						{
							this.VerticalScrollbar.Handle.ScaledSuggestedHeight = this.VerticalScrollbar.Size.Y;
						}
						this.InnerPanel.ScaledPositionYOffset = 0f;
						this.VerticalScrollbar.ValueFloat = 0f;
						this._verticalScrollVelocity = 0f;
						this._scrollOffset = 0f;
						if (this.AutoHideScrollBars)
						{
							flag3 = false;
						}
						if (this.AutoHideScrollBarHandle)
						{
							flag4 = false;
						}
					}
					foreach (ScrollablePanelFixedHeaderWidget scrollablePanelFixedHeaderWidget in this._fixedHeaders)
					{
						if (scrollablePanelFixedHeaderWidget != null && scrollablePanelFixedHeaderWidget.FixedHeader != null && base.MeasuredSize != Vec2.Zero)
						{
							scrollablePanelFixedHeaderWidget.FixedHeader.ScaledPositionYOffset = MathF.Clamp(scrollablePanelFixedHeaderWidget.LocalPosition.Y + this._scrollOffset, scrollablePanelFixedHeaderWidget.TopOffset * base._scaleToUse, base.MeasuredSize.Y - scrollablePanelFixedHeaderWidget.BottomOffset * base._scaleToUse);
						}
					}
					if (this.UpdateScrollbarVisibility)
					{
						this.VerticalScrollbar.IsVisible = flag3;
						this.VerticalScrollbar.Handle.IsVisible = (flag4 && flag3);
					}
				}
			}
			this._horizontalScrollbarChangedThisFrame = false;
			this._verticalScrollbarChangedThisFrame = false;
		}

		// Token: 0x06000695 RID: 1685 RVA: 0x0001D500 File Offset: 0x0001B700
		protected float GetScrollYValueForWidget(Widget widget, float widgetTargetYValue, float offset)
		{
			float amount = MBMath.ClampFloat(widgetTargetYValue, 0f, 1f);
			float value = Mathf.Lerp(widget.GlobalPosition.Y + offset, widget.GlobalPosition.Y - this.ClipRect.Size.Y + widget.Size.Y + offset, amount);
			float num = this.InnerPanel.Size.Y + this.InnerPanel.ScaledMarginTop + this.InnerPanel.ScaledMarginBottom;
			float num2 = this.InverseLerp(this.InnerPanel.GlobalPosition.Y, this.InnerPanel.GlobalPosition.Y + num - this.ClipRect.Size.Y, value);
			num2 = MathF.Clamp(num2, 0f, 1f);
			return MathF.Lerp(this.VerticalScrollbar.MinValue, this.VerticalScrollbar.MaxValue, num2, 1E-05f);
		}

		// Token: 0x06000696 RID: 1686 RVA: 0x0001D5F4 File Offset: 0x0001B7F4
		protected float GetScrollXValueForWidget(Widget widget, float widgetTargetXValue, float offset)
		{
			float amount = MBMath.ClampFloat(widgetTargetXValue, 0f, 1f);
			float value = Mathf.Lerp(widget.GlobalPosition.X + offset, widget.GlobalPosition.X - this.ClipRect.Size.X + widget.Size.X + offset, amount);
			float num = this.InnerPanel.Size.X + this.InnerPanel.ScaledMarginLeft + this.InnerPanel.ScaledMarginRight;
			float num2 = this.InverseLerp(this.InnerPanel.GlobalPosition.X, this.InnerPanel.GlobalPosition.X + num - this.ClipRect.Size.X, value);
			num2 = MathF.Clamp(num2, 0f, 1f);
			return MathF.Lerp(this.HorizontalScrollbar.MinValue, this.HorizontalScrollbar.MaxValue, num2, 1E-05f);
		}

		// Token: 0x06000697 RID: 1687 RVA: 0x0001D6E6 File Offset: 0x0001B8E6
		private float InverseLerp(float fromValue, float toValue, float value)
		{
			if (fromValue == toValue)
			{
				return 0f;
			}
			return (value - fromValue) / (toValue - fromValue);
		}

		// Token: 0x06000698 RID: 1688 RVA: 0x0001D6FC File Offset: 0x0001B8FC
		public void ScrollToChild(Widget targetWidget, ScrollablePanel.AutoScrollParameters scrollParameters = null)
		{
			if (scrollParameters == null)
			{
				scrollParameters = new ScrollablePanel.AutoScrollParameters(0f, 0f, 0f, 0f, -1f, -1f, 0f);
			}
			if (this.ClipRect != null && this.InnerPanel != null && base.CheckIsMyChildRecursive(targetWidget))
			{
				if (this.VerticalScrollbar != null)
				{
					bool flag = targetWidget.GlobalPosition.Y - scrollParameters.TopOffset - base.ExtendCursorAreaTop < this.ClipRect.GlobalPosition.Y;
					bool flag2 = targetWidget.GlobalPosition.Y + targetWidget.Size.Y + scrollParameters.BottomOffset + base.ExtendCursorAreaBottom > this.ClipRect.GlobalPosition.Y + this.ClipRect.Size.Y;
					if (flag || flag2)
					{
						if (scrollParameters.VerticalScrollTarget == -1f)
						{
							scrollParameters.VerticalScrollTarget = (flag ? 0f : 1f);
						}
						float scrollYValueForWidget = this.GetScrollYValueForWidget(targetWidget, scrollParameters.VerticalScrollTarget, flag ? (-scrollParameters.TopOffset) : scrollParameters.BottomOffset);
						if (scrollParameters.InterpolationTime <= 1E-45f)
						{
							this.VerticalScrollbar.ValueFloat = scrollYValueForWidget;
						}
						else
						{
							this._verticalScrollbarInterpolationController.StartInterpolation(scrollYValueForWidget, scrollParameters.InterpolationTime);
						}
					}
				}
				if (this.HorizontalScrollbar != null)
				{
					bool flag3 = targetWidget.GlobalPosition.X - scrollParameters.LeftOffset - base.ExtendCursorAreaLeft < this.ClipRect.GlobalPosition.X;
					bool flag4 = targetWidget.GlobalPosition.X + targetWidget.Size.X + scrollParameters.RightOffset + base.ExtendCursorAreaRight > this.ClipRect.GlobalPosition.X + this.ClipRect.Size.X;
					if (flag3 || flag4)
					{
						if (scrollParameters.HorizontalScrollTarget == -1f)
						{
							scrollParameters.HorizontalScrollTarget = (flag3 ? 0f : 1f);
						}
						float scrollXValueForWidget = this.GetScrollXValueForWidget(targetWidget, scrollParameters.HorizontalScrollTarget, flag3 ? (-scrollParameters.LeftOffset) : scrollParameters.RightOffset);
						if (scrollParameters.InterpolationTime <= 1E-45f)
						{
							this.HorizontalScrollbar.ValueFloat = scrollXValueForWidget;
							return;
						}
						this._horizontalScrollbarInterpolationController.StartInterpolation(scrollXValueForWidget, scrollParameters.InterpolationTime);
					}
				}
			}
		}

		// Token: 0x06000699 RID: 1689 RVA: 0x0001D944 File Offset: 0x0001BB44
		public void SetVerticalScrollTarget(float targetValue, float interpolationDuration)
		{
			this._verticalScrollbarInterpolationController.StartInterpolation(targetValue, interpolationDuration);
		}

		// Token: 0x0600069A RID: 1690 RVA: 0x0001D953 File Offset: 0x0001BB53
		public void SetHorizontalScrollTarget(float targetValue, float interpolationDuration)
		{
			this._horizontalScrollbarInterpolationController.StartInterpolation(targetValue, interpolationDuration);
		}

		// Token: 0x170001D0 RID: 464
		// (get) Token: 0x0600069B RID: 1691 RVA: 0x0001D962 File Offset: 0x0001BB62
		// (set) Token: 0x0600069C RID: 1692 RVA: 0x0001D96A File Offset: 0x0001BB6A
		[Editor(false)]
		public bool AutoHideScrollBars
		{
			get
			{
				return this._autoHideScrollBars;
			}
			set
			{
				if (this._autoHideScrollBars != value)
				{
					this._autoHideScrollBars = value;
					base.OnPropertyChanged(value, "AutoHideScrollBars");
				}
			}
		}

		// Token: 0x170001D1 RID: 465
		// (get) Token: 0x0600069D RID: 1693 RVA: 0x0001D988 File Offset: 0x0001BB88
		// (set) Token: 0x0600069E RID: 1694 RVA: 0x0001D990 File Offset: 0x0001BB90
		[Editor(false)]
		public bool AutoHideScrollBarHandle
		{
			get
			{
				return this._autoHideScrollBarHandle;
			}
			set
			{
				if (this._autoHideScrollBarHandle != value)
				{
					this._autoHideScrollBarHandle = value;
					base.OnPropertyChanged(value, "AutoHideScrollBarHandle");
				}
			}
		}

		// Token: 0x170001D2 RID: 466
		// (get) Token: 0x0600069F RID: 1695 RVA: 0x0001D9AE File Offset: 0x0001BBAE
		// (set) Token: 0x060006A0 RID: 1696 RVA: 0x0001D9B6 File Offset: 0x0001BBB6
		[Editor(false)]
		public bool AutoAdjustScrollbarHandleSize
		{
			get
			{
				return this._autoAdjustScrollbarHandleSize;
			}
			set
			{
				if (this._autoAdjustScrollbarHandleSize != value)
				{
					this._autoAdjustScrollbarHandleSize = value;
					base.OnPropertyChanged(value, "AutoAdjustScrollbarHandleSize");
				}
			}
		}

		// Token: 0x170001D3 RID: 467
		// (get) Token: 0x060006A1 RID: 1697 RVA: 0x0001D9D4 File Offset: 0x0001BBD4
		// (set) Token: 0x060006A2 RID: 1698 RVA: 0x0001D9DC File Offset: 0x0001BBDC
		[Editor(false)]
		public bool OnlyAcceptScrollEventIfCanScroll
		{
			get
			{
				return this._onlyAcceptScrollEventIfCanScroll;
			}
			set
			{
				if (this._onlyAcceptScrollEventIfCanScroll != value)
				{
					this._onlyAcceptScrollEventIfCanScroll = value;
					base.OnPropertyChanged(value, "OnlyAcceptScrollEventIfCanScroll");
				}
			}
		}

		// Token: 0x170001D4 RID: 468
		// (get) Token: 0x060006A3 RID: 1699 RVA: 0x0001D9FA File Offset: 0x0001BBFA
		// (set) Token: 0x060006A4 RID: 1700 RVA: 0x0001DA02 File Offset: 0x0001BC02
		[Editor(false)]
		public bool ReverseInitialScrollBarAlignment
		{
			get
			{
				return this._reverseInitialScrollBarAlignment;
			}
			set
			{
				if (this._reverseInitialScrollBarAlignment != value)
				{
					this._reverseInitialScrollBarAlignment = value;
					base.OnPropertyChanged(value, "ReverseInitialScrollBarAlignment");
				}
			}
		}

		// Token: 0x170001D5 RID: 469
		// (get) Token: 0x060006A5 RID: 1701 RVA: 0x0001DA20 File Offset: 0x0001BC20
		// (set) Token: 0x060006A6 RID: 1702 RVA: 0x0001DA28 File Offset: 0x0001BC28
		public ScrollbarWidget HorizontalScrollbar
		{
			get
			{
				return this._horizontalScrollbar;
			}
			set
			{
				if (value != this._horizontalScrollbar)
				{
					this._horizontalScrollbar = value;
					this._horizontalScrollbarInterpolationController.SetControlledScrollbar(value);
					base.OnPropertyChanged<ScrollbarWidget>(value, "HorizontalScrollbar");
					this._horizontalScrollbarChangedThisFrame = true;
				}
			}
		}

		// Token: 0x170001D6 RID: 470
		// (get) Token: 0x060006A7 RID: 1703 RVA: 0x0001DA59 File Offset: 0x0001BC59
		// (set) Token: 0x060006A8 RID: 1704 RVA: 0x0001DA61 File Offset: 0x0001BC61
		public ScrollbarWidget VerticalScrollbar
		{
			get
			{
				return this._verticalScrollbar;
			}
			set
			{
				if (value != this._verticalScrollbar)
				{
					this._verticalScrollbar = value;
					this._verticalScrollbarInterpolationController.SetControlledScrollbar(value);
					base.OnPropertyChanged<ScrollbarWidget>(value, "VerticalScrollbar");
					this._verticalScrollbarChangedThisFrame = true;
				}
			}
		}

		// Token: 0x04000309 RID: 777
		private Widget _innerPanel;

		// Token: 0x0400030D RID: 781
		protected bool _canScrollHorizontal;

		// Token: 0x0400030E RID: 782
		protected bool _canScrollVertical;

		// Token: 0x0400030F RID: 783
		public float ControllerScrollSpeed = 0.2f;

		// Token: 0x04000310 RID: 784
		public float MouseScrollSpeed = 0.2f;

		// Token: 0x04000311 RID: 785
		public AlignmentAxis MouseScrollAxis;

		// Token: 0x04000312 RID: 786
		private float _verticalScrollVelocity;

		// Token: 0x04000313 RID: 787
		private float _horizontalScrollVelocity;

		// Token: 0x04000314 RID: 788
		private bool _horizontalScrollbarChangedThisFrame;

		// Token: 0x04000315 RID: 789
		private bool _verticalScrollbarChangedThisFrame;

		// Token: 0x04000316 RID: 790
		protected float _scrollOffset;

		// Token: 0x04000317 RID: 791
		protected ScrollablePanel.ScrollbarInterpolationController _verticalScrollbarInterpolationController;

		// Token: 0x04000318 RID: 792
		protected ScrollablePanel.ScrollbarInterpolationController _horizontalScrollbarInterpolationController;

		// Token: 0x04000319 RID: 793
		private List<ScrollablePanelFixedHeaderWidget> _fixedHeaders = new List<ScrollablePanelFixedHeaderWidget>();

		// Token: 0x0400031A RID: 794
		private ScrollbarWidget _horizontalScrollbar;

		// Token: 0x0400031B RID: 795
		private ScrollbarWidget _verticalScrollbar;

		// Token: 0x0400031C RID: 796
		private bool _autoHideScrollBars;

		// Token: 0x0400031D RID: 797
		private bool _autoHideScrollBarHandle;

		// Token: 0x0400031E RID: 798
		private bool _autoAdjustScrollbarHandleSize = true;

		// Token: 0x0400031F RID: 799
		private bool _onlyAcceptScrollEventIfCanScroll;

		// Token: 0x04000320 RID: 800
		private bool _reverseInitialScrollBarAlignment;

		// Token: 0x02000093 RID: 147
		protected class ScrollbarInterpolationController
		{
			// Token: 0x17000298 RID: 664
			// (get) Token: 0x06000910 RID: 2320 RVA: 0x00023D3F File Offset: 0x00021F3F
			public bool IsInterpolating
			{
				get
				{
					return this._isInterpolating;
				}
			}

			// Token: 0x06000912 RID: 2322 RVA: 0x00023D4F File Offset: 0x00021F4F
			public void SetControlledScrollbar(ScrollbarWidget scrollbar)
			{
				this._scrollbar = scrollbar;
			}

			// Token: 0x06000913 RID: 2323 RVA: 0x00023D58 File Offset: 0x00021F58
			public void StartInterpolation(float targetValue, float duration)
			{
				ScrollbarWidget scrollbar = this._scrollbar;
				this._interpolationInitialValue = ((scrollbar != null) ? scrollbar.ValueFloat : 0f);
				this._targetValue = targetValue;
				this._duration = duration;
				this._timer = 0f;
				this._isInterpolating = true;
			}

			// Token: 0x06000914 RID: 2324 RVA: 0x00023D96 File Offset: 0x00021F96
			public void StopInterpolation()
			{
				this._isInterpolating = false;
				this._targetValue = 0f;
				this._duration = 0f;
				this._timer = 0f;
			}

			// Token: 0x06000915 RID: 2325 RVA: 0x00023DC0 File Offset: 0x00021FC0
			public float GetValue()
			{
				ScrollbarWidget scrollbar = this._scrollbar;
				if (scrollbar == null)
				{
					return 0f;
				}
				return scrollbar.ValueFloat;
			}

			// Token: 0x06000916 RID: 2326 RVA: 0x00023DD8 File Offset: 0x00021FD8
			public void Tick(float dt)
			{
				if (this._isInterpolating && this._scrollbar != null)
				{
					this._timer += dt;
					if (this._duration == 0f || this._timer > this._duration || float.IsNaN(this._timer) || float.IsNaN(this._duration))
					{
						this._scrollbar.ValueFloat = this._targetValue;
						this.StopInterpolation();
						return;
					}
					float ratio = MathF.Clamp(this._timer / this._duration, 0f, 1f);
					this._scrollbar.ValueFloat = MathF.Lerp(this._interpolationInitialValue, this._targetValue, AnimationInterpolation.Ease(AnimationInterpolation.Type.EaseInOut, AnimationInterpolation.Function.Sine, ratio), 1E-05f);
					if (this._scrollbar.ValueFloat == this._scrollbar.MinValue || this._scrollbar.ValueFloat == this._scrollbar.MaxValue)
					{
						this.StopInterpolation();
						return;
					}
				}
			}

			// Token: 0x0400048B RID: 1163
			private ScrollbarWidget _scrollbar;

			// Token: 0x0400048C RID: 1164
			private float _targetValue;

			// Token: 0x0400048D RID: 1165
			private float _duration;

			// Token: 0x0400048E RID: 1166
			private bool _isInterpolating;

			// Token: 0x0400048F RID: 1167
			private float _interpolationInitialValue;

			// Token: 0x04000490 RID: 1168
			private float _timer;
		}

		// Token: 0x02000094 RID: 148
		public class AutoScrollParameters
		{
			// Token: 0x06000917 RID: 2327 RVA: 0x00023ED3 File Offset: 0x000220D3
			public AutoScrollParameters(float topOffset = 0f, float bottomOffset = 0f, float leftOffset = 0f, float rightOffset = 0f, float horizontalScrollTarget = -1f, float verticalScrollTarget = -1f, float interpolationTime = 0f)
			{
				this.TopOffset = topOffset;
				this.BottomOffset = bottomOffset;
				this.LeftOffset = leftOffset;
				this.RightOffset = rightOffset;
				this.HorizontalScrollTarget = horizontalScrollTarget;
				this.VerticalScrollTarget = verticalScrollTarget;
				this.InterpolationTime = interpolationTime;
			}

			// Token: 0x04000491 RID: 1169
			public float TopOffset;

			// Token: 0x04000492 RID: 1170
			public float BottomOffset;

			// Token: 0x04000493 RID: 1171
			public float LeftOffset;

			// Token: 0x04000494 RID: 1172
			public float RightOffset;

			// Token: 0x04000495 RID: 1173
			public float HorizontalScrollTarget;

			// Token: 0x04000496 RID: 1174
			public float VerticalScrollTarget;

			// Token: 0x04000497 RID: 1175
			public float InterpolationTime;
		}
	}
}
