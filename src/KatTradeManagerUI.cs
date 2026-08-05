/* KatTradeManagerUI.cs - WPF UI partial class for KatTradeManager v0.94 (2026-07-31) */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		#region WPF UI Construction & Handlers
		private Button[] atmSetButtons;
		private readonly SolidColorBrush atmSetOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65)); // same gray as other OFF buttons
		private readonly SolidColorBrush atmSetOnBg = new SolidColorBrush(Color.FromRgb(180, 90, 20)); // amber when its ATM is selected
		private bool isHotkeyAttached = false;
		private Window hotkeyWindow; // cached at attach — chart can move to a new window before detach
		private bool hasHudDragPosition;
		private double hudDragLeft;
		private double hudDragTop;
		private Canvas hudCanvas;
		private TextBlock hudStatusText;
		private string pendingHudStatusMessage;
		private Brush pendingHudStatusBrush;
		private System.Windows.Threading.DispatcherTimer hudStatusTimer;
		private Point hudDragStart;
		private double hudDragStartLeft;
		private double hudDragStartTop;
		private IInputElement hudDragCoordinateHost;
		private UIElement hudDragEventHost;
		private bool isHudDragging;
		private const double DefaultHudLeft = 10;

		private void StartPanelWatchdog()
		{
			if (panelWatchdog != null) return;

			panelWatchdog = new System.Windows.Threading.DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(500)
			};
			panelWatchdog.Tick += OnPanelWatchdogTick;
			panelWatchdog.Start();
		}

		private double GetHudLeftInset()
		{
			return Math.Max(0, cachedHudLeftInset);
		}

		private void StopPanelWatchdog()
		{
			if (panelWatchdog != null)
			{
				panelWatchdog.Stop();
				panelWatchdog.Tick -= OnPanelWatchdogTick;
				panelWatchdog = null;
			}
		}

		private void OnPanelWatchdogTick(object sender, EventArgs e)
		{
			// Boundary catch: NT8 property getters (Instrument etc.) can throw transient
			// IndexOutOfRange while the platform reloads bars/instruments during overnight session
			// maintenance. One bad tick must never pop an unhandled-exception dialog or kill the
			// timer — log it and let the next tick (500 ms) retry.
			try
			{
				if (!IsPanelVisible)
				{
					RemoveWpfControls();
					return;
				}
				if (isTerminated || ChartControl == null)
				{
					StopPanelWatchdog();
					DetachHotkeyHandler();
					return;
				}

				chartGrid = ChartControl.Parent as Grid;
				if (chartGrid == null) return;

				// Auto-recover account if DataLoaded ran before accounts connected (root cause of "buttons don't work")
				if (account == null)
				{
					SwitchAccount(SelectAccount()); // resets daily-risk baseline for the fresh account
					if (account != null)
						Print(string.Format("[KatTradeManager] Account auto-recovered by watchdog: {0}", account.Name));
				}
				EnsureAccountEventSubscription();
				// Pump serialized account mutations; pending broker states are revisited on each watchdog tick.
				ScheduleAccountOperationPump();

				AttachHotkeyHandler();

				// Evaluate real-time daily risk protection limits
				EvaluateDailyRiskLimits();
				TrySubmitPendingRevert();
				ScheduleAtmBracketMerge();

				// Freeze Trail: take over ATM protection while ON, clean up static exits once flat
				ProcessFreezeTrail();


				// Sync UI control values to thread-safe cached fields
				SyncCachedValues();

				if (!IsPanelAttached())
				{
					RemoveWpfControls();
					CreateWpfControls();
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Watchdog tick error (will retry next tick): {0}", ex.Message));
			}
		}

		private void SyncCachedValues()
		{
			if (atmSelector != null && atmSelector.SelectedItem != null)
			{
				string selectedAtm = atmSelector.SelectedItem.ToString();
				cachedAtmTemplate = IsNoAtmSelection(selectedAtm) ? string.Empty : selectedAtm;
			}

			cachedTfIndex = (int)DefaultTimeframe;
			cachedBufferTicks = DefaultBufferTicks;
			cachedPartialPercent = DefaultPartialCandlePercent > 0 ? DefaultPartialCandlePercent : 30;
			cachedHudLeftInset = Math.Max(0, HudLeftInset);
			bool wasHudDragEnabled = cachedHudDragEnabled;
			cachedHudDragEnabled = HudDragEnabled;
			if (wasHudDragEnabled && !cachedHudDragEnabled && isHudDragging)
				StopHudDrag();
		}

		// "None" = trade without ATM, matching NT8 Chart Trader's own None selection. Empty cachedAtmTemplate
		// makes SubmitOrder use a plain submit and stops the HUD from managing brackets it does not own.
		// ponytail: an ATM template literally named "None" collides; ceiling = non-string sentinel item.
		private const string NoAtmTemplateLabel = "None";

		private static bool IsNoAtmSelection(string value)
		{
			return string.IsNullOrEmpty(value) || value.Equals(NoAtmTemplateLabel, StringComparison.OrdinalIgnoreCase);
		}

		private void ApplyAtmSelection(object selectedItem)
		{
			string selected = selectedItem != null ? selectedItem.ToString() : string.Empty;
			cachedAtmTemplate = IsNoAtmSelection(selected) ? string.Empty : selected;
			DefaultAtmTemplate = cachedAtmTemplate;
			LoadAtmTemplateSettings(cachedAtmTemplate); // empty name clears parsed ATM levels
			UpdateAtmSetButtons();
		}

		private string GetAtmSetTemplate(int idx)
		{
			switch (idx)
			{
				case 0: return AtmSet1Atm;
				case 1: return AtmSet2Atm;
				case 2: return AtmSet3Atm;
				case 3: return AtmSet4Atm;
				case 4: return AtmSet5Atm;
				default: return AtmSet6Atm;
			}
		}

		private string GetAtmSetName(int idx)
		{
			switch (idx)
			{
				case 0: return AtmSet1Name;
				case 1: return AtmSet2Name;
				case 2: return AtmSet3Name;
				case 3: return AtmSet4Name;
				case 4: return AtmSet5Name;
				default: return AtmSet6Name;
			}
		}

		// Quick-set click: select the assigned ATM immediately (same as picking it from the dropdown).
		private void ApplyAtmSetSelection(int idx)
		{
			string tpl = GetAtmSetTemplate(idx);
			if (string.IsNullOrEmpty(tpl))
			{
				ShowHudStatus(string.Format("Set {0}: no ATM assigned (Indicator Settings)", GetAtmSetName(idx)), Brushes.OrangeRed);
				return;
			}

			if (atmSelector != null)
			{
				bool found = false;
				for (int i = 0; i < atmSelector.Items.Count; i++)
				{
					if (atmSelector.Items[i].ToString().Equals(tpl, StringComparison.OrdinalIgnoreCase))
					{
						atmSelector.SelectedIndex = i; // dropdown shows it; SelectionChanged fires ApplyAtmSelection
						found = true;
						break;
					}
				}
				if (!found)
				{
					ShowHudStatus(string.Format("Set {0}: ATM '{1}' not found on disk", GetAtmSetName(idx), tpl), Brushes.OrangeRed);
					return;
				}
			}
			ApplyAtmSelection(tpl); // idempotent when the dropdown handler already ran
		}

		// Exactly one set button is ON: the one whose assigned ATM equals the current selection.
		// ATM None (empty) turns every button OFF.
		private void UpdateAtmSetButtons()
		{
			if (atmSetButtons == null) return;
			for (int i = 0; i < atmSetButtons.Length; i++)
			{
				if (atmSetButtons[i] == null) continue;
				string tpl = GetAtmSetTemplate(i);
				bool on = !string.IsNullOrEmpty(cachedAtmTemplate)
					&& !string.IsNullOrEmpty(tpl)
					&& tpl.Equals(cachedAtmTemplate, StringComparison.OrdinalIgnoreCase);
				atmSetButtons[i].Background = on ? atmSetOnBg : atmSetOffBg;
				atmSetButtons[i].Foreground = on ? Brushes.White : Brushes.LightGray;
			}
		}

		// ponytail: uses visual tree type name matching for ChartTraderControl; fallback to chart grid if hidden
		private DependencyObject GetChartTraderControl()
		{
			if (ChartControl == null) return null;

			if (ChartControl.OwnerChart != null && ChartControl.OwnerChart.ChartTrader != null)
			{
				var ct = ChartControl.OwnerChart.ChartTrader;
				if (ct.Visibility == Visibility.Visible) return ct;
			}

			Window window = Window.GetWindow(ChartControl);
			if (window != null)
			{
				var ct = FindVisualChildByTypeName(window, "ChartTraderControl") ?? FindVisualChildByTypeName(window, "ChartTrader");
				if (ct is FrameworkElement fe && fe.Visibility == Visibility.Visible) return ct;
			}

			return null;
		}

		private DependencyObject FindVisualChildByTypeName(DependencyObject parent, string typeName)
		{
			if (parent == null) return null;
			int count = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, i);
				if (child != null && child.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
					return child;
				DependencyObject result = FindVisualChildByTypeName(child, typeName);
				if (result != null) return result;
			}
			return null;
		}

		private void FindAllVisualChildren<T>(DependencyObject parent, List<T> results) where T : DependencyObject
		{
			if (parent == null) return;
			int count = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, i);
				if (child is T typedChild)
					results.Add(typedChild);
				FindAllVisualChildren<T>(child, results);
			}
		}

		private int GetVisualDepth(DependencyObject element)
		{
			int depth = 0;
			DependencyObject parent = VisualTreeHelper.GetParent(element);
			while (parent != null)
			{
				depth++;
				parent = VisualTreeHelper.GetParent(parent);
			}
			return depth;
		}

		private Panel FindChartTraderPanel(DependencyObject ctControl)
		{
			if (ctControl == null) return null;

			// 1. Direct Content of ContentControl (UserControl) if Grid or Panel
			if (ctControl is Grid directGrid)
				return directGrid;

			if (ctControl is ContentControl cc && cc.Content is FrameworkElement contentFe)
			{
				if (contentFe is Grid contentGrid)
					return contentGrid;
				if (contentFe is Panel contentPanel)
					return contentPanel;
			}

			// 2. Find Grids and pick the top-most (shallowest depth)
			List<Grid> grids = new List<Grid>();
			FindAllVisualChildren<Grid>(ctControl, grids);
			var topGrid = grids.OrderBy(GetVisualDepth).FirstOrDefault();
			if (topGrid != null)
				return topGrid;

			return null;
		}

		private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
		{
			if (parent == null) return null;
			int count = VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < count; i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, i);
				if (child is T typedChild)
					return typedChild;
				T result = FindVisualChild<T>(child);
				if (result != null) return result;
			}
			return null;
		}

		private void DetachFromParent(UIElement element)
		{
			if (element == null) return;
			DependencyObject parent = LogicalTreeHelper.GetParent(element) ?? VisualTreeHelper.GetParent(element);
			if (parent is Panel panel)
			{
				panel.Children.Remove(element);
			}
			else if (parent is ContentControl contentControl)
			{
				contentControl.Content = null;
			}
			else if (parent is Decorator decorator)
			{
				decorator.Child = null;
			}
		}

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
				DependencyObject visualParent = VisualTreeHelper.GetParent(element);
				if (visualParent != null) return visualParent;
			}
			catch
			{
				// ContentElement/Run can exist outside VisualTreeHelper.
			}

			try
			{
				return LogicalTreeHelper.GetParent(element);
			}
			catch
			{
				return null;
			}
		}

		// Walk visual/logical/content tree: click on Button/ContentPresenter still counts as interactive.
		// Used so panel drag does not steal MouseUp from buttons (Click never fires otherwise).
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
			hudDragCoordinateHost = hudCanvas as IInputElement
				?? panelBorder.Parent as IInputElement
				?? chartGrid as IInputElement;
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
				Print(string.Format("[KatTradeManager] HUD drag capture FAILED: source={0} mode={1}",
					source != null ? source.GetType().Name : "null", PanelLocation));
				return;
			}
			Print(string.Format("[KatTradeManager] HUD drag started: source={0} mode={1} parent={2}",
				source != null ? source.GetType().Name : "null",
				PanelLocation,
				panelBorder.Parent != null ? panelBorder.Parent.GetType().Name : "null"));
			e.Handled = true;
		}

		private void OnHudPreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (!cachedHudDragEnabled || !isHudDragging || panelBorder == null) return;
			if (e.LeftButton != MouseButtonState.Pressed)
			{
				StopHudDrag();
				return;
			}
			IInputElement coordinateHost = hudDragCoordinateHost
				?? hudCanvas as IInputElement
				?? chartGrid as IInputElement;
			if (coordinateHost == null) return;
			Point current = e.GetPosition(coordinateHost);
			double newLeft = hudDragStartLeft + (current.X - hudDragStart.X);
			double newTop = hudDragStartTop + (current.Y - hudDragStart.Y);

			const double minVisible = 40;
			FrameworkElement parent = panelBorder.Parent as FrameworkElement;
			double canvasWidth = hudCanvas != null
				? (hudCanvas.ActualWidth > 0 ? hudCanvas.ActualWidth : chartGrid.ActualWidth)
				: (parent != null && parent.ActualWidth > 0 ? parent.ActualWidth : chartGrid.ActualWidth);
			double canvasHeight = hudCanvas != null
				? (hudCanvas.ActualHeight > 0 ? hudCanvas.ActualHeight : chartGrid.ActualHeight)
				: (parent != null && parent.ActualHeight > 0 ? parent.ActualHeight : chartGrid.ActualHeight);
			double panelWidth = panelBorder.ActualWidth > 0 ? panelBorder.ActualWidth : panelBorder.Width;
			double panelHeight = panelBorder.ActualHeight > 0 ? panelBorder.ActualHeight : 40;
			newLeft = KatTradeCalculator.ClampHudCoordinate(newLeft, panelWidth, canvasWidth, minVisible);
			newTop = KatTradeCalculator.ClampHudCoordinate(newTop, panelHeight, canvasHeight, minVisible);
			if (hudCanvas != null)
			{
				Canvas.SetLeft(panelBorder, newLeft);
				Canvas.SetTop(panelBorder, newTop);
			}
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
			if (Mouse.Captured == panelBorder)
				Mouse.Capture(null);
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

		private bool IsPanelAttached()
		{
			if (panelBorder == null) return false;

			if (PanelLocation == KatHudLocation.ChartTrader)
			{
				var ctControl = GetChartTraderControl();
				if (ctControl != null)
				{
					var ctPanel = FindChartTraderPanel(ctControl);
					if (ctPanel != null)
						return ctPanel.Children.Contains(panelBorder); // CT available -> panel must live there, not in chartGrid fallback
				}
			}

			return hudCanvas != null && hudCanvas.Children.Contains(panelBorder);
		}

		private void CreateWpfControls()
		{
			if (ChartControl == null) return;
			chartGrid = ChartControl.Parent as Grid;
			if (chartGrid == null) return;

			DetachFromParent(panelBorder);
			DetachFromParent(hudCanvas);

			panelBorder = new Border
			{
				Tag = "KatTradeManagerPanel",
				Background = new SolidColorBrush(Color.FromArgb(240, 20, 24, 33)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(35, 42, 56)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(8),
				Margin = new Thickness(2, 4, 2, 4)
			};

			bool isChartTraderAttached = false;

			if (PanelLocation == KatHudLocation.ChartTrader)
			{
				var ctControl = GetChartTraderControl();
				var ctPanel = ctControl != null ? FindChartTraderPanel(ctControl) : null;

				if (ctPanel != null)
				{
					panelBorder.Width = double.NaN;
					panelBorder.HorizontalAlignment = hasHudDragPosition ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
					panelBorder.VerticalAlignment = hasHudDragPosition ? VerticalAlignment.Top : VerticalAlignment.Bottom;
					panelBorder.Margin = hasHudDragPosition
						? new Thickness(hudDragLeft, hudDragTop, 0, 0)
						: new Thickness(GetHudLeftInset(), 0, 0, 0);
					panelBorder.Cursor = cachedHudDragEnabled ? Cursors.SizeAll : Cursors.Arrow;
					System.Windows.Controls.Panel.SetZIndex(panelBorder, 99999);

					if (ctControl is FrameworkElement ctFe)
						ctFe.ClipToBounds = false;
					if (ctPanel is FrameworkElement ctPanelFe)
						ctPanelFe.ClipToBounds = false;

					if (ctPanel is Grid g)
					{
						Grid.SetColumn(panelBorder, 0);
						Grid.SetColumnSpan(panelBorder, Math.Max(1, g.ColumnDefinitions.Count > 0 ? g.ColumnDefinitions.Count : 99));
						Grid.SetRow(panelBorder, 0);
						Grid.SetRowSpan(panelBorder, Math.Max(1, g.RowDefinitions.Count > 0 ? g.RowDefinitions.Count : 99));
					}

					ctPanel.Children.Add(panelBorder);
					isChartTraderAttached = true;
				}
			}

			if (!isChartTraderAttached)
			{
				// InChart mode or fallback: use absolute Canvas coordinates for drag.
				hudCanvas = new Canvas
				{
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Stretch,
					ClipToBounds = false
				};
				System.Windows.Controls.Panel.SetZIndex(hudCanvas, 9999);
				Grid.SetColumnSpan(hudCanvas, 3);
				chartGrid.Children.Add(hudCanvas);
				panelBorder.Width = 240;
				panelBorder.HorizontalAlignment = HorizontalAlignment.Left;
				panelBorder.VerticalAlignment = VerticalAlignment.Top;
				panelBorder.Margin = new Thickness(0);

				panelBorder.Cursor = cachedHudDragEnabled ? Cursors.SizeAll : Cursors.Arrow;
				hudCanvas.Children.Add(panelBorder);
				double dragStartLeft = hasHudDragPosition ? hudDragLeft : GetHudLeftInset();
				double dragStartTop = hasHudDragPosition ? hudDragTop : 10;
				Canvas.SetLeft(panelBorder, dragStartLeft);
				Canvas.SetTop(panelBorder, hasHudDragPosition
					? dragStartTop
					: Math.Max(0, chartGrid.ActualHeight - panelBorder.ActualHeight - 10));
				panelBorder.Loaded += (s, ev) =>
				{
					if (!hasHudDragPosition)
						Canvas.SetTop(panelBorder, Math.Max(0, hudCanvas.ActualHeight - panelBorder.ActualHeight - 10));
				};


			}

			// Register on Border and its actual host. ChartTrader can route input through
			// a host grid instead of the panel Border; handledEventsToo keeps controls clickable.
			AttachHudDragHandlers(
				hudCanvas as UIElement
				?? panelBorder.Parent as UIElement
				?? chartGrid as UIElement);
			mainPanel = new StackPanel();

			// --- SECTION 1: Parameters & ATM Selection ---
			StackPanel sec1Panel = new StackPanel();

			TextBlock hudHeader = new TextBlock
			{
				Text = string.Format("⚡ KAT TradeManager v{0}", VERSION),
				Foreground = new SolidColorBrush(Color.FromRgb(70, 130, 160)),
				FontWeight = FontWeights.Bold,
				FontSize = 12,
				Margin = new Thickness(0, 0, 0, 6),
				HorizontalAlignment = HorizontalAlignment.Left
			};
			sec1Panel.Children.Add(hudHeader);

			hudStatusText = new TextBlock
			{
				Background = Brushes.Transparent,
				Foreground = Brushes.White,
				FontSize = 10,
				Margin = new Thickness(0, 0, 0, 6),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Height = 16,
				MinHeight = 16,
				MaxHeight = 16,
				TextWrapping = TextWrapping.NoWrap,
				TextTrimming = TextTrimming.CharacterEllipsis,
				Visibility = Visibility.Visible,
				Text = string.Empty
			};
			sec1Panel.Children.Add(hudStatusText);

			if (!string.IsNullOrEmpty(pendingHudStatusMessage))
			{
				hudStatusText.Text = pendingHudStatusMessage;
				hudStatusText.Foreground = pendingHudStatusBrush ?? Brushes.White;
				hudStatusText.Visibility = Visibility.Visible;
				pendingHudStatusMessage = null;
				pendingHudStatusBrush = null;
			}

			ComboBox accSelector = new ComboBox { FontSize = 11, Height = 22, Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
			if (Account.All != null)
			{
				var allowedAccs = Account.All.Where(a => IsAccountAllowed(a.Name)).ToList();
				foreach (var acc in allowedAccs) accSelector.Items.Add(acc.Name);
				string savedAccountName = AccountName;
				if (!string.IsNullOrEmpty(savedAccountName) && accSelector.Items.Contains(savedAccountName))
				{
					accSelector.SelectedItem = savedAccountName;
					SwitchAccount(Account.All.FirstOrDefault(a => a.Name.Equals(savedAccountName, StringComparison.OrdinalIgnoreCase)));
				}
				else if (account != null && accSelector.Items.Contains(account.Name))
				{
					accSelector.SelectedItem = account.Name;
				}
				else if (allowedAccs.Count > 0)
				{
					accSelector.SelectedIndex = 0;
					SwitchAccount(allowedAccs[0]); // SelectionChanged handler isn't attached yet
					AccountName = allowedAccs[0].Name;
					Print(string.Format("[KatTradeManager] Defaulted account to first allowed: {0}", account.Name));
				}
			}
			accSelector.SelectionChanged += (s, ev) =>
			{
				if (accSelector.SelectedItem != null)
				{
					string selectedName = accSelector.SelectedItem.ToString();
					// SwitchAccount resets the per-account session baseline — otherwise the OLD account's
					// realized PnL stays as the baseline (phantom daily PnL -> false/missed risk breach).
					SwitchAccount(Account.All.FirstOrDefault(a => a.Name == selectedName));
					AccountName = selectedName;
					Print(string.Format("[KatTradeManager] Account changed via UI to: {0}", selectedName));
				}
			};
			sec1Panel.Children.Add(accSelector);

			atmSelector = new ComboBox
			{
				FontSize = 11,
				Height = 22,
				Margin = new Thickness(0, 0, 0, 0),
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			atmSelector.Items.Add(NoAtmTemplateLabel); // first item, also the fallback when no template matches
			string atmDir = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy");
			if (System.IO.Directory.Exists(atmDir))
			{
				var files = System.IO.Directory.GetFiles(atmDir, "*.xml");
				Array.Sort(files, StringComparer.OrdinalIgnoreCase); // deterministic order -> deterministic default selection
				foreach (var file in files)
				{
					string name = System.IO.Path.GetFileNameWithoutExtension(file);
					atmSelector.Items.Add(name);
				}
			}
			atmSelector.SelectedIndex = 0;
			if (!string.IsNullOrEmpty(DefaultAtmTemplate))
			{
				for (int i = 0; i < atmSelector.Items.Count; i++)
				{
					if (atmSelector.Items[i].ToString().Equals(DefaultAtmTemplate, StringComparison.OrdinalIgnoreCase))
					{
						atmSelector.SelectedIndex = i;
						break;
					}
				}
			}
			ApplyAtmSelection(atmSelector.SelectedItem);
			atmSelector.SelectionChanged += (s, ev) => ApplyAtmSelection(atmSelector.SelectedItem);

			sec1Panel.Children.Add(atmSelector);

			// --- ATM Quick Set buttons (A–F), one-click ATM selection ---
			atmSetButtons = new Button[6];
			Grid atmSetGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
			for (int i = 0; i < 6; i++)
			{
				atmSetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				if (i < 5)
					atmSetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
			}

			for (int i = 0; i < 6; i++)
			{
				int setIdx = i;
				Button setBtn = CreateButton(GetAtmSetName(setIdx), atmSetOffBg, null, 22, 10);
				setBtn.Foreground = Brushes.LightGray;
				setBtn.Margin = new Thickness(0);
				setBtn.Click += (s, ev) => ApplyAtmSetSelection(setIdx);
				Grid.SetColumn(setBtn, setIdx * 2);
				atmSetButtons[setIdx] = setBtn;
				atmSetGrid.Children.Add(setBtn);
			}
			sec1Panel.Children.Add(atmSetGrid);
			UpdateAtmSetButtons();
			mainPanel.Children.Add(CreateSectionCard(sec1Panel, 6));


			// --- SECTION 2: EMA 34 & EMA 89 Touch/Cross Orders ---
			StackPanel sec2Panel = new StackPanel();

			SolidColorBrush buy34Bg  = new SolidColorBrush(Color.FromRgb(100, 115, 30));
			SolidColorBrush sell34Bg = new SolidColorBrush(Color.FromRgb(175, 75, 25));
			SolidColorBrush buy89Bg  = new SolidColorBrush(Color.FromRgb(35, 95, 110));
			SolidColorBrush sell89Bg = new SolidColorBrush(Color.FromRgb(130, 35, 95));
			SolidColorBrush entryShiftBg = new SolidColorBrush(Color.FromRgb(20, 20, 20));

			Grid entryShiftGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			entryShiftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			entryShiftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			entryShiftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			Button btnEntryBack = CreateButton("◀ Entry 89/34", entryShiftBg, (s, ev) => ShiftEmaEntry(false), 33, 12);
			Grid.SetColumn(btnEntryBack, 0);
			entryShiftGrid.Children.Add(btnEntryBack);

			Button btnEntryRedo = CreateButton("Entry 89/34 ▶", entryShiftBg, (s, ev) => ShiftEmaEntry(true), 33, 12);
			Grid.SetColumn(btnEntryRedo, 2);
			entryShiftGrid.Children.Add(btnEntryRedo);

			sec2Panel.Children.Add(entryShiftGrid);

			Grid ema34Grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			ema34Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			ema34Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			ema34Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			Button btnSell34 = CreateButton("SELL last 34", sell34Bg, (s, ev) => PlaceEmaOrder(OrderAction.Sell, 34), 48, 12);
			Grid.SetColumn(btnSell34, 0);
			ema34Grid.Children.Add(btnSell34);

			Button btnBuy34 = CreateButton("BUY last 34", buy34Bg, (s, ev) => PlaceEmaOrder(OrderAction.Buy, 34), 48, 12);
			Grid.SetColumn(btnBuy34, 2);
			ema34Grid.Children.Add(btnBuy34);

			sec2Panel.Children.Add(ema34Grid);

			Grid ema89Grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			ema89Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			ema89Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			ema89Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			Button btnSell89 = CreateButton("SELL last 89", sell89Bg, (s, ev) => PlaceEmaOrder(OrderAction.Sell, 89), 48, 12);
			Grid.SetColumn(btnSell89, 0);
			ema89Grid.Children.Add(btnSell89);

			Button btnBuy89 = CreateButton("BUY last 89", buy89Bg, (s, ev) => PlaceEmaOrder(OrderAction.Buy, 89), 48, 12);
			Grid.SetColumn(btnBuy89, 2);
			ema89Grid.Children.Add(btnBuy89);

			sec2Panel.Children.Add(ema89Grid);

			// --- SECTION 2b: Swing Stop Loss Shift Controls ---
			Grid swingSlGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
			swingSlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			swingSlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			swingSlGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush swingSlBg = new SolidColorBrush(Color.FromRgb(20, 20, 20)); // Same dark color as Close/flatten

			Button btnSlBack = CreateButton("◀ SL", swingSlBg, (s, ev) => ShiftSlToSwing(false), 33, 12);
			Grid.SetColumn(btnSlBack, 0);
			swingSlGrid.Children.Add(btnSlBack);

			Button btnSlRedo = CreateButton("SL ▶", swingSlBg, (s, ev) => ShiftSlToSwing(true), 33, 12);
			Grid.SetColumn(btnSlRedo, 2);
			swingSlGrid.Children.Add(btnSlRedo);

			sec2Panel.Children.Add(swingSlGrid);
			mainPanel.Children.Add(CreateSectionCard(sec2Panel, 6));


			// --- SECTION 3: Partial Candle & Candle Pending Orders ---
			StackPanel sec3Panel = new StackPanel();

			SolidColorBrush partialOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
			SolidColorBrush partialOnBg  = new SolidColorBrush(Color.FromRgb(0, 122, 204));
			string partialOnText  = string.Format("⚡ Partial {0}%: ON", cachedPartialPercent);
			string partialOffText = "Partial Candle: OFF";

			Button btnPartialCandle = CreateButton(cachedIsPartialCandle ? partialOnText : partialOffText,
				cachedIsPartialCandle ? partialOnBg : partialOffBg, null, 24, 11);
			btnPartialCandle.Foreground = cachedIsPartialCandle ? Brushes.White : Brushes.LightGray;
			btnPartialCandle.Margin = new Thickness(0, 0, 0, 4);

			btnPartialCandle.Click += (s, ev) =>
			{
				cachedIsPartialCandle = !cachedIsPartialCandle;
				if (cachedIsPartialCandle)
				{
					btnPartialCandle.Content = string.Format("⚡ Partial {0}%: ON", cachedPartialPercent);
					btnPartialCandle.Background = partialOnBg;
					btnPartialCandle.Foreground = Brushes.White;
				}
				else
				{
					btnPartialCandle.Content = "Partial Candle: OFF";
					btnPartialCandle.Background = partialOffBg;
					btnPartialCandle.Foreground = Brushes.LightGray;
				}
			};
			sec3Panel.Children.Add(btnPartialCandle);

			// --- EMA Place & EMA Angle filter buttons (side-by-side below Partial Candle) ---
			Grid emaFilterGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			emaFilterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			emaFilterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			emaFilterGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush emaOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
			SolidColorBrush emaOnBg  = new SolidColorBrush(Color.FromRgb(12, 35, 75)); // Very dark blue

			Button btnEmaPlace = CreateButton(cachedIsEmaPlace ? "Ema place: ON" : "Ema place: OFF",
				cachedIsEmaPlace ? emaOnBg : emaOffBg, null, 24, 10);
			btnEmaPlace.Foreground = cachedIsEmaPlace ? Brushes.White : Brushes.LightGray;

			btnEmaPlace.Click += (s, ev) =>
			{
				cachedIsEmaPlace = !cachedIsEmaPlace;
				btnEmaPlace.Content = cachedIsEmaPlace ? "Ema place: ON" : "Ema place: OFF";
				btnEmaPlace.Background = cachedIsEmaPlace ? emaOnBg : emaOffBg;
				btnEmaPlace.Foreground = cachedIsEmaPlace ? Brushes.White : Brushes.LightGray;
			};
			Grid.SetColumn(btnEmaPlace, 0);
			emaFilterGrid.Children.Add(btnEmaPlace);

			Button btnEmaAngle = CreateButton(cachedIsEmaAngle ? "Ema angle: ON" : "Ema angle: OFF",
				cachedIsEmaAngle ? emaOnBg : emaOffBg, null, 24, 10);
			btnEmaAngle.Foreground = cachedIsEmaAngle ? Brushes.White : Brushes.LightGray;

			btnEmaAngle.Click += (s, ev) =>
			{
				cachedIsEmaAngle = !cachedIsEmaAngle;
				btnEmaAngle.Content = cachedIsEmaAngle ? "Ema angle: ON" : "Ema angle: OFF";
				btnEmaAngle.Background = cachedIsEmaAngle ? emaOnBg : emaOffBg;
				btnEmaAngle.Foreground = cachedIsEmaAngle ? Brushes.White : Brushes.LightGray;
			};
			Grid.SetColumn(btnEmaAngle, 2);
			emaFilterGrid.Children.Add(btnEmaAngle);

			sec3Panel.Children.Add(emaFilterGrid);

			// --- Daily Max DD & Daily Max Profit toggle buttons (side-by-side below EMA Filter) ---
			Grid dailyRiskGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			dailyRiskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			dailyRiskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			dailyRiskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush dailyOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
			SolidColorBrush dailyOnBg  = new SolidColorBrush(Color.FromRgb(58, 19, 107)); // Darker purple (#3A136B)

			Button btnDailyMaxDD = CreateButton(cachedIsDailyMaxDD ? "Max DD: ON" : "Max DD: OFF",
				cachedIsDailyMaxDD ? dailyOnBg : dailyOffBg, null, 24, 10);
			btnDailyMaxDD.Foreground = cachedIsDailyMaxDD ? Brushes.White : Brushes.LightGray;

			btnDailyMaxDD.Click += (s, ev) =>
			{
				cachedIsDailyMaxDD = !cachedIsDailyMaxDD;
				// Persist to the NinjaScript property — a script refresh/reload re-reads the property,
				// so a volatile-only OFF was silently re-enabled and could flatten on the next breach.
				DailyMaxDDEnabled = cachedIsDailyMaxDD;
				btnDailyMaxDD.Content = cachedIsDailyMaxDD ? "Max DD: ON" : "Max DD: OFF";
				btnDailyMaxDD.Background = cachedIsDailyMaxDD ? dailyOnBg : dailyOffBg;
				btnDailyMaxDD.Foreground = cachedIsDailyMaxDD ? Brushes.White : Brushes.LightGray;

				// Instant effect on HUD click (Requirement 4)
				EvaluateDailyRiskLimits();
			};
			Grid.SetColumn(btnDailyMaxDD, 0);
			dailyRiskGrid.Children.Add(btnDailyMaxDD);

			Button btnDailyMaxProfit = CreateButton(cachedIsDailyMaxProfit ? "Max Profit: ON" : "Max Profit: OFF",
				cachedIsDailyMaxProfit ? dailyOnBg : dailyOffBg, null, 24, 10);
			btnDailyMaxProfit.Foreground = cachedIsDailyMaxProfit ? Brushes.White : Brushes.LightGray;

			btnDailyMaxProfit.Click += (s, ev) =>
			{
				cachedIsDailyMaxProfit = !cachedIsDailyMaxProfit;
				DailyMaxProfitEnabled = cachedIsDailyMaxProfit; // persist — survives script refresh/reload
				btnDailyMaxProfit.Content = cachedIsDailyMaxProfit ? "Max Profit: ON" : "Max Profit: OFF";
				btnDailyMaxProfit.Background = cachedIsDailyMaxProfit ? dailyOnBg : dailyOffBg;
				btnDailyMaxProfit.Foreground = cachedIsDailyMaxProfit ? Brushes.White : Brushes.LightGray;

				// Instant effect on HUD click (Requirement 4)
				EvaluateDailyRiskLimits();
			};
			Grid.SetColumn(btnDailyMaxProfit, 2);
			dailyRiskGrid.Children.Add(btnDailyMaxProfit);

			sec3Panel.Children.Add(dailyRiskGrid);

			// --- Candle Entry Shift Controls ---
			Grid candleShiftGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			candleShiftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			candleShiftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			candleShiftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush candleShiftBg = new SolidColorBrush(Color.FromRgb(20, 20, 20)); // Same dark color as SL moving buttons

			Button btnCandleBack = CreateButton("◀ Entry candle", candleShiftBg, (s, ev) => ShiftCandleEntry(false), 33, 12);
			Grid.SetColumn(btnCandleBack, 0);
			candleShiftGrid.Children.Add(btnCandleBack);

			Button btnCandleRedo = CreateButton("Entry candle ▶", candleShiftBg, (s, ev) => ShiftCandleEntry(true), 33, 12);
			Grid.SetColumn(btnCandleRedo, 2);
			candleShiftGrid.Children.Add(btnCandleRedo);

			sec3Panel.Children.Add(candleShiftGrid);


			Grid orderBtnGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush buyPrevBg  = new SolidColorBrush(Color.FromRgb(34, 112, 62));
			SolidColorBrush buyCurrBg  = new SolidColorBrush(Color.FromRgb(24, 82, 45));
			SolidColorBrush sellPrevBg = new SolidColorBrush(Color.FromRgb(148, 48, 54));
			SolidColorBrush sellCurrBg = new SolidColorBrush(Color.FromRgb(110, 32, 38));

			StackPanel buyCol = new StackPanel();
			Button btnBuyPrev = CreateButton("BUY previous", buyPrevBg, (s, ev) => PlaceOrder(OrderAction.Buy, false), 48, 12);
			btnBuyPrev.Margin = new Thickness(0, 0, 0, 4);
			Button btnBuyCurr = CreateButton("BUY current", buyCurrBg, (s, ev) => PlaceOrder(OrderAction.Buy, true), 48, 12);
			btnBuyCurr.Margin = new Thickness(0, 0, 0, 4);

			buyCol.Children.Add(btnBuyCurr);
			buyCol.Children.Add(btnBuyPrev);
			Grid.SetColumn(buyCol, 2);

			StackPanel sellCol = new StackPanel();
			Button btnSellPrev = CreateButton("SELL previous", sellPrevBg, (s, ev) => PlaceOrder(OrderAction.Sell, false), 48, 12);
			btnSellPrev.Margin = new Thickness(0, 0, 0, 4);
			Button btnSellCurr = CreateButton("SELL current", sellCurrBg, (s, ev) => PlaceOrder(OrderAction.Sell, true), 48, 12);
			btnSellCurr.Margin = new Thickness(0, 0, 0, 4);

			sellCol.Children.Add(btnSellCurr);
			sellCol.Children.Add(btnSellPrev);
			Grid.SetColumn(sellCol, 0);

			orderBtnGrid.Children.Add(sellCol);
			orderBtnGrid.Children.Add(buyCol);
			sec3Panel.Children.Add(orderBtnGrid);
			mainPanel.Children.Add(CreateSectionCard(sec3Panel, 6));


			// --- SECTION 4: Market Orders & Position Management ---
			StackPanel sec4Panel = new StackPanel();

			Grid mktBtnGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			mktBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			mktBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			mktBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush buyMktBg  = new SolidColorBrush(Color.FromRgb(12, 48, 25)); // Deep dark green
			SolidColorBrush sellMktBg = new SolidColorBrush(Color.FromRgb(55, 15, 18)); // Deep dark red

			Button btnSellMkt = CreateButton("SELL market", sellMktBg, (s, ev) => PlaceMarketOrder(OrderAction.Sell), 48, 12);
			Grid.SetColumn(btnSellMkt, 0);
			mktBtnGrid.Children.Add(btnSellMkt);

			Button btnBuyMkt = CreateButton("BUY market", buyMktBg, (s, ev) => PlaceMarketOrder(OrderAction.Buy), 48, 12);
			Grid.SetColumn(btnBuyMkt, 2);
			mktBtnGrid.Children.Add(btnBuyMkt);

			sec4Panel.Children.Add(mktBtnGrid);

			Grid beRevertGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			beRevertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			beRevertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			beRevertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush beBg     = new SolidColorBrush(Color.FromRgb(14, 48, 62)); // Deep dark slate teal
			SolidColorBrush revertBg = new SolidColorBrush(Color.FromRgb(75, 42, 10)); // Deep dark amber

			Button btnBE = CreateButton("BE", beBg, (s, ev) => SetBreakeven(), 33, 12);
			Grid.SetColumn(btnBE, 0);
			beRevertGrid.Children.Add(btnBE);

			Button btnRevert = CreateButton("Revert", revertBg, (s, ev) => RevertPosition(), 33, 12);
			Grid.SetColumn(btnRevert, 2);
			beRevertGrid.Children.Add(btnRevert);

			sec4Panel.Children.Add(beRevertGrid);

			SolidColorBrush freezeOffBg = new SolidColorBrush(Color.FromRgb(35, 40, 52)); // Darker gray than Partial Candle (45,50,65)
			SolidColorBrush freezeOnBg  = new SolidColorBrush(Color.FromRgb(180, 90, 20));  // Dark amber accent when active

			Button btnFreezeTrail = CreateButton(cachedIsFreezeTrail ? "⚡ Freeze Trail: ON" : "Freeze Trail: OFF",
				cachedIsFreezeTrail ? freezeOnBg : freezeOffBg, null, 24, 10);
			btnFreezeTrail.Foreground = cachedIsFreezeTrail ? Brushes.White : Brushes.LightGray;
			btnFreezeTrail.Margin = new Thickness(0, 0, 0, 4);

			btnFreezeTrail.Click += (s, ev) =>
			{
				cachedIsFreezeTrail = !cachedIsFreezeTrail;
				btnFreezeTrail.Content = cachedIsFreezeTrail ? "⚡ Freeze Trail: ON" : "Freeze Trail: OFF";
				btnFreezeTrail.Background = cachedIsFreezeTrail ? freezeOnBg : freezeOffBg;
				btnFreezeTrail.Foreground = cachedIsFreezeTrail ? Brushes.White : Brushes.LightGray;

				if (cachedIsFreezeTrail)
					FreezeCurrentStopLoss();
				else
					ShowHudStatus("Freeze OFF: static SL/TP kept — new entries use ATM again", Brushes.LightGray);
			};
			sec4Panel.Children.Add(btnFreezeTrail);

			Button btnStopLimit = CreateButton(cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF",
				cachedIsStopLimit ? freezeOnBg : freezeOffBg, null, 24, 10);
			btnStopLimit.Foreground = cachedIsStopLimit ? Brushes.White : Brushes.LightGray;
			btnStopLimit.Margin = new Thickness(0, 0, 0, 4);
			btnStopLimit.Click += (s, ev) =>
			{
				cachedIsStopLimit = !cachedIsStopLimit;
				btnStopLimit.Content = cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF";
				btnStopLimit.Background = cachedIsStopLimit ? freezeOnBg : freezeOffBg;
				btnStopLimit.Foreground = cachedIsStopLimit ? Brushes.White : Brushes.LightGray;
			};
			sec4Panel.Children.Add(btnStopLimit);

			SolidColorBrush closeBg = new SolidColorBrush(Color.FromRgb(20, 20, 20)); // Very dark gray (almost black)
			Button btnClose = CreateButton("Close/flatten", closeBg, (s, ev) => FlattenAllPositions(), 33, 15);
			sec4Panel.Children.Add(btnClose);


			mainPanel.Children.Add(CreateSectionCard(sec4Panel, 0));


			panelBorder.Child = mainPanel;
		}

		private Button CreateButton(string text, Brush bg, RoutedEventHandler handler, double height = 24, double fontSize = 10)
		{
			Button btn = new Button
			{
				Content = text,
				Background = bg,
				Foreground = Brushes.White,
				FontWeight = FontWeights.Normal,
				FontSize = fontSize,
				Margin = new Thickness(0),
				Padding = new Thickness(2),
				Height = height,
				BorderThickness = new Thickness(0)
			};
			if (handler != null)
				btn.Click += handler;
			return btn;
		}

		private void ShowHudStatus(string message, Brush foreground)
		{
			if (ChartControl == null || ChartControl.Dispatcher == null) return;

			Action update = () =>
			{
				if (hudStatusText == null)
				{
					pendingHudStatusMessage = message;
					pendingHudStatusBrush = foreground;
					return;
				}

				hudStatusText.Text = message;
				hudStatusText.Foreground = foreground ?? Brushes.White;
				hudStatusText.Visibility = Visibility.Visible;

				if (hudStatusTimer == null)
				{
					hudStatusTimer = new System.Windows.Threading.DispatcherTimer
					{
						Interval = TimeSpan.FromSeconds(5)
					};
					hudStatusTimer.Tick += (s, e) =>
					{
						if (hudStatusText != null)
						{
							hudStatusText.Text = string.Empty;
							hudStatusText.Foreground = Brushes.White;
						}
						hudStatusTimer.Stop();
					};
				}

				hudStatusTimer.Stop();
				hudStatusTimer.Start();
			};

			if (ChartControl.Dispatcher.CheckAccess())
				update();
			else
				ChartControl.Dispatcher.BeginInvoke(update);
		}


		private Border CreateSectionCard(FrameworkElement child, double bottomMargin = 6)
		{
			return new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(10, 12, 18)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(35, 42, 56)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(5),
				Padding = new Thickness(6),
				Margin = new Thickness(0, 0, 0, bottomMargin),
				Child = child
			};
		}

		private void RemoveWpfControls()
		{
			DetachHotkeyHandler();
			StopHudDrag();
			if (hudStatusTimer != null)
			{
				hudStatusTimer.Stop();
				hudStatusTimer = null;
			}
			if (panelBorder != null)
			{
				DetachHudDragHandlers();
				DetachFromParent(panelBorder);
				panelBorder = null;
			}
			if (hudCanvas != null)
			{
				DetachFromParent(hudCanvas);
				hudCanvas = null;
			}
			hudStatusText = null;
		}

		private void AttachHotkeyHandler()
		{
			if (ChartControl == null) return;

			// Chart dragged to a different window while attached -> move handler to the new window
			if (isHotkeyAttached)
			{
				Window current = Window.GetWindow(ChartControl);
				if (current != hotkeyWindow)
				{
					DetachHotkeyHandler();
				}
				else
				{
					return;
				}
			}

			ChartControl.PreviewKeyDown += OnChartPreviewKeyDown;

			hotkeyWindow = Window.GetWindow(ChartControl);
			if (hotkeyWindow != null)
			{
				hotkeyWindow.PreviewKeyDown -= OnChartPreviewKeyDown;
				hotkeyWindow.PreviewKeyDown += OnChartPreviewKeyDown;
			}

			isHotkeyAttached = true;
		}

		private void DetachHotkeyHandler()
		{
			if (!isHotkeyAttached) return;
			if (ChartControl != null)
				ChartControl.PreviewKeyDown -= OnChartPreviewKeyDown;
			if (hotkeyWindow != null)
			{
				hotkeyWindow.PreviewKeyDown -= OnChartPreviewKeyDown;
				hotkeyWindow = null;
			}
			isHotkeyAttached = false;
		}

		private void OnChartPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (isTerminated || !HotkeyEnabled) return;
			if (e.IsRepeat) return;
			if (Keyboard.FocusedElement is TextBox) return;

			Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
			if (key == Key.None) return;

			bool handled = false;

			if (key == HotkeyBuyEma34 && HotkeyBuyEma34 != Key.None)
			{
				PlaceEmaOrder(OrderAction.Buy, 34);
				handled = true;
			}
			else if (key == HotkeySellEma34 && HotkeySellEma34 != Key.None)
			{
				PlaceEmaOrder(OrderAction.Sell, 34);
				handled = true;
			}
			else if (key == HotkeyBuyEma89 && HotkeyBuyEma89 != Key.None)
			{
				PlaceEmaOrder(OrderAction.Buy, 89);
				handled = true;
			}
			else if (key == HotkeySellEma89 && HotkeySellEma89 != Key.None)
			{
				PlaceEmaOrder(OrderAction.Sell, 89);
				handled = true;
			}
			else if (key == HotkeyBuyPrev && HotkeyBuyPrev != Key.None)
			{
				PlaceOrder(OrderAction.Buy, false);
				handled = true;
			}
			else if (key == HotkeySellPrev && HotkeySellPrev != Key.None)
			{
				PlaceOrder(OrderAction.Sell, false);
				handled = true;
			}
			else if (key == HotkeyBuyCurr && HotkeyBuyCurr != Key.None)
			{
				PlaceOrder(OrderAction.Buy, true);
				handled = true;
			}
			else if (key == HotkeySellCurr && HotkeySellCurr != Key.None)
			{
				PlaceOrder(OrderAction.Sell, true);
				handled = true;
			}
			else if (key == HotkeyBuyMarket && HotkeyBuyMarket != Key.None)
			{
				PlaceMarketOrder(OrderAction.Buy);
				handled = true;
			}
			else if (key == HotkeySellMarket && HotkeySellMarket != Key.None)
			{
				PlaceMarketOrder(OrderAction.Sell);
				handled = true;
			}
			else if (key == HotkeyBE && HotkeyBE != Key.None)
			{
				SetBreakeven();
				handled = true;
			}
			else if (key == HotkeyRevert && HotkeyRevert != Key.None)
			{
				RevertPosition();
				handled = true;
			}
			else if (key == HotkeyClose && HotkeyClose != Key.None)
			{
				FlattenAllPositions();
				handled = true;
			}

			if (handled)
			{
				e.Handled = true;
			}
		}
		#endregion
	}
}
