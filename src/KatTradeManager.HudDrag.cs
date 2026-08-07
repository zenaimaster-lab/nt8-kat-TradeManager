/* KatTradeManager.HudDrag.cs - HUD drag handling (partial class) v1.40 (2026-08-08) */
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		private static DependencyObject GetHudParent(DependencyObject element)
		{
			if (element == null) return null;
			if (element is System.Windows.ContentElement contentElement)
			{
				DependencyObject contentParent = System.Windows.ContentOperations.GetParent(contentElement);
				if (contentParent != null) return contentParent;
				if (element is FrameworkContentElement frameworkContentElement && frameworkContentElement.Parent != null)
					return frameworkContentElement.Parent;
			}
			try
			{
				DependencyObject visualParent = System.Windows.Media.VisualTreeHelper.GetParent(element);
				if (visualParent != null) return visualParent;
			}
			catch { }
			try { return System.Windows.LogicalTreeHelper.GetParent(element); }
			catch { return null; }
		}

		private static bool IsInteractiveVisual(DependencyObject src)
		{
			while (src != null)
			{
				if (src is System.Windows.Controls.Primitives.ButtonBase
					|| src is TextBox
					|| src is ComboBox
					|| src is System.Windows.Controls.Primitives.Selector
					|| src is System.Windows.Controls.Primitives.Thumb)
					return true;
				src = GetHudParent(src);
			}
			return false;
		}

		private bool IsHudDragSource(DependencyObject source)
		{
			if (source == null || panelBorder == null) return false;
			DependencyObject current = source;
			while (current != null)
			{
				if (ReferenceEquals(current, panelBorder))
					return !IsInteractiveVisual(source);
				DependencyObject parent = GetHudParent(current);
				if (ReferenceEquals(parent, current)) break;
				current = parent;
			}
			return false;
		}

		private void OnHudPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (!cachedHudDragEnabled || isHudDragging) return;
			DependencyObject source = e.OriginalSource as DependencyObject ?? e.Source as DependencyObject;
			if (!IsHudDragSource(source)) return;
			hudDragCoordinateHost = hudCanvas as IInputElement ?? panelBorder.Parent as IInputElement ?? chartGrid as IInputElement;
			if (hudDragCoordinateHost == null) return;
			hudDragStart = e.GetPosition(hudDragCoordinateHost);
			if (hudCanvas != null)
			{
				hudDragStartLeft = Canvas.GetLeft(panelBorder);
				hudDragStartTop = Canvas.GetTop(panelBorder);
				if (double.IsNaN(hudDragStartLeft)) hudDragStartLeft = DefaultHudLeft;
				if (double.IsNaN(hudDragStartTop)) hudDragStartTop = 10;
			}
			else
			{
				hudDragStartLeft = double.IsNaN(panelBorder.Margin.Left) ? 0 : panelBorder.Margin.Left;
				hudDragStartTop = double.IsNaN(panelBorder.Margin.Top) ? 0 : panelBorder.Margin.Top;
			}
			if (double.IsNaN(hudDragStartTop)) hudDragStartTop = 10;
			isHudDragging = true;
			if (!Mouse.Capture(panelBorder, CaptureMode.SubTree))
			{
				isHudDragging = false;
				hudDragCoordinateHost = null;
				Print(string.Format("[KatTradeManager] HUD drag capture FAILED: source={0} mode={1}", source != null ? source.GetType().Name : "null", PanelLocation));
				return;
			}
			Print(string.Format("[KatTradeManager] HUD drag started: source={0} mode={1} parent={2}", source != null ? source.GetType().Name : "null", PanelLocation, panelBorder.Parent != null ? panelBorder.Parent.GetType().Name : "null"));
			e.Handled = true;
		}

		private void OnHudPreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (!cachedHudDragEnabled || !isHudDragging || panelBorder == null) return;
			if (e.LeftButton != MouseButtonState.Pressed) { StopHudDrag(); return; }
			IInputElement coordinateHost = hudDragCoordinateHost ?? hudCanvas as IInputElement ?? chartGrid as IInputElement;
			if (coordinateHost == null) return;
			Point current = e.GetPosition(coordinateHost);
			double newLeft = hudDragStartLeft + (current.X - hudDragStart.X);
			double newTop = hudDragStartTop + (current.Y - hudDragStart.Y);
			const double minVisible = 40;
			FrameworkElement parent = panelBorder.Parent as FrameworkElement;
			double canvasWidth = hudCanvas != null ? (hudCanvas.ActualWidth > 0 ? hudCanvas.ActualWidth : chartGrid.ActualWidth) : (parent != null && parent.ActualWidth > 0 ? parent.ActualWidth : chartGrid.ActualWidth);
			double canvasHeight = hudCanvas != null ? (hudCanvas.ActualHeight > 0 ? hudCanvas.ActualHeight : chartGrid.ActualHeight) : (parent != null && parent.ActualHeight > 0 ? parent.ActualHeight : chartGrid.ActualHeight);
			double panelWidth = panelBorder.ActualWidth > 0 ? panelBorder.ActualWidth : panelBorder.Width;
			double panelHeight = panelBorder.ActualHeight > 0 ? panelBorder.ActualHeight : 40;
			newLeft = KatTradeCalculator.ClampHudCoordinate(newLeft, panelWidth, canvasWidth, minVisible);
			newTop = KatTradeCalculator.ClampHudCoordinate(newTop, panelHeight, canvasHeight, minVisible);
			if (hudCanvas != null) { Canvas.SetLeft(panelBorder, newLeft); Canvas.SetTop(panelBorder, newTop); }
			else
			{
				panelBorder.HorizontalAlignment = HorizontalAlignment.Left;
				panelBorder.VerticalAlignment = VerticalAlignment.Top;
				panelBorder.Margin = new Thickness(newLeft, newTop, 0, 0);
			}
			hasHudDragPosition = true;
			hudDragLeft = newLeft;
			hudDragTop = newTop;
			e.Handled = true;
		}

		private void OnHudPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (!cachedHudDragEnabled || !isHudDragging) return;
			StopHudDrag();
			e.Handled = true;
		}

		private void StopHudDrag()
		{
			isHudDragging = false;
			hudDragCoordinateHost = null;
			if (Mouse.Captured == panelBorder) Mouse.Capture(null);
		}

		private void OnHudLostMouseCapture(object sender, MouseEventArgs e)
		{
			isHudDragging = false;
			hudDragCoordinateHost = null;
		}

		private void AttachHudDragHandlers(UIElement eventHost)
		{
			if (panelBorder == null) return;
			MouseButtonEventHandler down = OnHudPreviewMouseLeftButtonDown;
			MouseEventHandler move = OnHudPreviewMouseMove;
			MouseButtonEventHandler up = OnHudPreviewMouseLeftButtonUp;
			panelBorder.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, down, true);
			panelBorder.AddHandler(UIElement.PreviewMouseMoveEvent, move, true);
			panelBorder.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, up, true);
			panelBorder.LostMouseCapture += OnHudLostMouseCapture;
			if (eventHost != null && !ReferenceEquals(eventHost, panelBorder))
			{
				eventHost.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, down, true);
				eventHost.AddHandler(UIElement.PreviewMouseMoveEvent, move, true);
				eventHost.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, up, true);
				hudDragEventHost = eventHost;
			}
		}

		private void DetachHudDragHandlers()
		{
			if (panelBorder == null) return;
			MouseButtonEventHandler down = OnHudPreviewMouseLeftButtonDown;
			MouseEventHandler move = OnHudPreviewMouseMove;
			MouseButtonEventHandler up = OnHudPreviewMouseLeftButtonUp;
			panelBorder.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, down);
			panelBorder.RemoveHandler(UIElement.PreviewMouseMoveEvent, move);
			panelBorder.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, up);
			panelBorder.LostMouseCapture -= OnHudLostMouseCapture;
			if (hudDragEventHost != null)
			{
				hudDragEventHost.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, down);
				hudDragEventHost.RemoveHandler(UIElement.PreviewMouseMoveEvent, move);
				hudDragEventHost.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, up);
				hudDragEventHost = null;
			}
		}
	}
}
