/* KatTradeManagerUI.cs - WPF UI partial class for KatTradeManager v0.70 (2026-07-28) */

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

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class KatTradeManager
	{
		#region WPF UI Construction & Handlers
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
				account = SelectAccount();
				if (account != null)
					Print(string.Format("[KatTradeManager] Account auto-recovered by watchdog: {0}", account.Name));
			}
			EnsureAccountEventSubscription();

			AttachHotkeyHandler();

			// Evaluate real-time daily risk protection limits
			EvaluateDailyRiskLimits();
			TrySubmitPendingRevert();

			// Enforce Freeze Trail if active
			CheckFreezeTrailEnforcement();

			if (!IsPanelVisible)
			{
				if (panelBorder != null) RemoveWpfControls();
				return;
			}


			// Sync UI control values to thread-safe cached fields
			SyncCachedValues();

			if (!IsPanelAttached())
			{
				RemoveWpfControls();
				CreateWpfControls();
			}
		}

		private void SyncCachedValues()
		{
			if (txtQuantity != null)
				cachedQuantity = int.TryParse(txtQuantity.Text, out int q) ? q : DefaultQuantity;
			if (atmSelector != null && atmSelector.SelectedItem != null)
				cachedAtmTemplate = atmSelector.SelectedItem.ToString();

			cachedTfIndex = (int)DefaultTimeframe;
			cachedBufferTicks = DefaultBufferTicks;
			cachedDistanceTicks = DefaultDistanceTicks;
			cachedPartialPercent = DefaultPartialCandlePercent > 0 ? DefaultPartialCandlePercent : 30;
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

		// Walk visual tree: click on Button text/ContentPresenter still counts as interactive.
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
				try
				{
					src = VisualTreeHelper.GetParent(src);
				}
				catch
				{
					src = LogicalTreeHelper.GetParent(src);
				}
			}
			return false;
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
					panelBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
					panelBorder.VerticalAlignment = VerticalAlignment.Bottom;
					panelBorder.Margin = new Thickness(0, 0, 0, 0);
					panelBorder.Cursor = Cursors.Arrow;
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

				panelBorder.Cursor = Cursors.SizeAll;
				hudCanvas.Children.Add(panelBorder);

				Point dragStart = new Point();
				double dragStartLeft = hasHudDragPosition ? hudDragLeft : 10;
				double dragStartTop = hasHudDragPosition ? hudDragTop : 10;
				bool isDragging = false;
				Canvas.SetLeft(panelBorder, dragStartLeft);
				Canvas.SetTop(panelBorder, hasHudDragPosition
					? dragStartTop
					: Math.Max(0, chartGrid.ActualHeight - panelBorder.ActualHeight - 10));
				panelBorder.Loaded += (s, ev) =>
				{
					if (!hasHudDragPosition)
						Canvas.SetTop(panelBorder, Math.Max(0, hudCanvas.ActualHeight - panelBorder.ActualHeight - 10));
				};

				// Use PREVIEW (tunneling) so we can bail BEFORE the event reaches buttons.
				// MouseLeftButtonDown (bubbling) was sometimes too late and could interfere with Button internal state.
				panelBorder.PreviewMouseLeftButtonDown += (s, ev) =>
				{
					// CRITICAL: do NOT capture mouse when clicking buttons/inputs.
					// CaptureMouse + Handled steals MouseUp from the Button → Click never fires.
					DependencyObject src = ev.OriginalSource as DependencyObject ?? ev.Source as DependencyObject;
					if (IsInteractiveVisual(src))
						return;

					dragStart = ev.GetPosition(hudCanvas);
					dragStartLeft = Canvas.GetLeft(panelBorder);
					dragStartTop = Canvas.GetTop(panelBorder);
					if (double.IsNaN(dragStartLeft)) dragStartLeft = 10;
					if (double.IsNaN(dragStartTop)) dragStartTop = 10;

					panelBorder.CaptureMouse();
					isDragging = true;
					ev.Handled = true;
				};

				panelBorder.PreviewMouseMove += (s, ev) =>
				{
					if (isDragging)
					{
						Point current = ev.GetPosition(hudCanvas);
						double newLeft = dragStartLeft + (current.X - dragStart.X);
						double newTop = dragStartTop + (current.Y - dragStart.Y);

						// Clamp: keep at least 40px of the panel reachable inside the chart
						const double minVisible = 40;
						double canvasWidth = hudCanvas.ActualWidth > 0 ? hudCanvas.ActualWidth : chartGrid.ActualWidth;
						double canvasHeight = hudCanvas.ActualHeight > 0 ? hudCanvas.ActualHeight : chartGrid.ActualHeight;
						double panelWidth = panelBorder.ActualWidth > 0 ? panelBorder.ActualWidth : panelBorder.Width;
						double panelHeight = panelBorder.ActualHeight > 0 ? panelBorder.ActualHeight : 40;
						newLeft = KatTradeCalculator.ClampHudCoordinate(newLeft, panelWidth, canvasWidth, minVisible);
						newTop = KatTradeCalculator.ClampHudCoordinate(newTop, panelHeight, canvasHeight, minVisible);

						Canvas.SetLeft(panelBorder, newLeft);
						Canvas.SetTop(panelBorder, newTop);
						hasHudDragPosition = true;
						hudDragLeft = newLeft;
						hudDragTop = newTop;
					}
				};

				panelBorder.LostMouseCapture += (s, ev) => isDragging = false;

				panelBorder.PreviewMouseLeftButtonUp += (s, ev) =>
				{
					if (isDragging)
					{
						panelBorder.ReleaseMouseCapture();
						isDragging = false;
						ev.Handled = true;
					}
				};

			}

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
				Foreground = Brushes.White,
				FontSize = 10,
				Margin = new Thickness(0, 0, 0, 6),
				HorizontalAlignment = HorizontalAlignment.Left,
				TextWrapping = TextWrapping.Wrap,
				Visibility = Visibility.Collapsed
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

			Grid paramGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
			paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			ComboBox accSelector = new ComboBox { FontSize = 11, Height = 22 };
			if (Account.All != null)
			{
				var allowedAccs = Account.All.Where(a => IsAccountAllowed(a.Name)).ToList();
				foreach (var acc in allowedAccs) accSelector.Items.Add(acc.Name);
				if (account != null && accSelector.Items.Contains(account.Name))
				{
					accSelector.SelectedItem = account.Name;
				}
				else if (allowedAccs.Count > 0)
				{
					accSelector.SelectedIndex = 0;
					account = allowedAccs[0]; // assign directly — SelectionChanged handler isn't attached yet
					EnsureAccountEventSubscription();
					Print(string.Format("[KatTradeManager] Defaulted account to first allowed: {0}", account.Name));
				}
			}
			accSelector.SelectionChanged += (s, ev) =>
			{
				if (accSelector.SelectedItem != null)
				{
					string selectedName = accSelector.SelectedItem.ToString();
					account = Account.All.FirstOrDefault(a => a.Name == selectedName);
					EnsureAccountEventSubscription();

					// Reset per-account state — otherwise the OLD account's realized PnL stays as the
					// session baseline (phantom daily PnL -> false/missed risk breach), and a stale
					// frozen stop from the old account would yank the new account's stops.
					isSessionStartCaptured = false;
					System.Threading.Interlocked.Exchange(ref dailyRiskFlattened, 0);
					frozenStopPrice = 0;

					Print(string.Format("[KatTradeManager] Account changed via UI to: {0}", selectedName));
				}
			};
			AddGridRow(paramGrid, "Acc:", accSelector);

			txtQuantity = new TextBox { Text = DefaultQuantity.ToString(), FontSize = 11, Height = 22, Background = Brushes.Black, Foreground = Brushes.White, BorderBrush = Brushes.Gray, Padding = new Thickness(4, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center };
			txtQuantity.PreviewKeyDown += (s, ev) =>
			{
				if (ev.Key == Key.Enter)
				{
					SyncCachedValues();
					if (ChartControl != null) ChartControl.Focus();
					ev.Handled = true;
				}
			};
			AddGridRow(paramGrid, "Contracts:", txtQuantity);

			sec1Panel.Children.Add(paramGrid);

			atmSelector = new ComboBox
			{
				FontSize = 11,
				Height = 22,
				Margin = new Thickness(0, 0, 0, 0),
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
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
			if (atmSelector.Items.Count > 0)
			{
				bool selected = false;
				for (int i = 0; i < atmSelector.Items.Count; i++)
				{
					if (atmSelector.Items[i].ToString().Equals(DefaultAtmTemplate, StringComparison.OrdinalIgnoreCase))
					{
						atmSelector.SelectedIndex = i;
						selected = true;
						break;
					}
				}
				if (!selected)
					atmSelector.SelectedIndex = 0;
			}
			if (atmSelector.SelectedItem != null)
			{
				cachedAtmTemplate = atmSelector.SelectedItem.ToString();
				LoadAtmTemplateSettings(cachedAtmTemplate);
			}
			atmSelector.SelectionChanged += (s, ev) =>
			{
				if (atmSelector.SelectedItem != null)
				{
					cachedAtmTemplate = atmSelector.SelectedItem.ToString();
					LoadAtmTemplateSettings(cachedAtmTemplate);
				}
			};

			sec1Panel.Children.Add(atmSelector);
			mainPanel.Children.Add(CreateSectionCard(sec1Panel, 6));


			// --- SECTION 2: EMA 34 & EMA 89 Touch/Cross Orders ---
			StackPanel sec2Panel = new StackPanel();

			SolidColorBrush buy34Bg  = new SolidColorBrush(Color.FromRgb(100, 115, 30));
			SolidColorBrush sell34Bg = new SolidColorBrush(Color.FromRgb(175, 75, 25));
			SolidColorBrush buy89Bg  = new SolidColorBrush(Color.FromRgb(35, 95, 110));
			SolidColorBrush sell89Bg = new SolidColorBrush(Color.FromRgb(130, 35, 95));

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
				btnDailyMaxProfit.Content = cachedIsDailyMaxProfit ? "Max Profit: ON" : "Max Profit: OFF";
				btnDailyMaxProfit.Background = cachedIsDailyMaxProfit ? dailyOnBg : dailyOffBg;
				btnDailyMaxProfit.Foreground = cachedIsDailyMaxProfit ? Brushes.White : Brushes.LightGray;

				// Instant effect on HUD click (Requirement 4)
				EvaluateDailyRiskLimits();
			};
			Grid.SetColumn(btnDailyMaxProfit, 2);
			dailyRiskGrid.Children.Add(btnDailyMaxProfit);

			sec3Panel.Children.Add(dailyRiskGrid);


			Grid orderBtnGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush buyPrevBg  = new SolidColorBrush(Color.FromRgb(34, 112, 62));
			SolidColorBrush buyCurrBg  = new SolidColorBrush(Color.FromRgb(24, 82, 45));
			SolidColorBrush buyFixedBg = new SolidColorBrush(Color.FromRgb(16, 56, 30));
			SolidColorBrush sellPrevBg = new SolidColorBrush(Color.FromRgb(148, 48, 54));
			SolidColorBrush sellCurrBg = new SolidColorBrush(Color.FromRgb(110, 32, 38));
			SolidColorBrush sellFixedBg = new SolidColorBrush(Color.FromRgb(75, 20, 24));

			StackPanel buyCol = new StackPanel();
			Button btnBuyPrev = CreateButton("BUY previous", buyPrevBg, (s, ev) => PlaceOrder(OrderAction.Buy, false), 48, 12);
			btnBuyPrev.Margin = new Thickness(0, 0, 0, 4);
			Button btnBuyCurr = CreateButton("BUY current", buyCurrBg, (s, ev) => PlaceOrder(OrderAction.Buy, true), 24, 10);
			btnBuyCurr.Margin = new Thickness(0, 0, 0, 4);
			Button btnBuyDist = CreateButton("BUY +distance", buyFixedBg, (s, ev) => PlaceFixedDistanceOrder(OrderAction.Buy), 24, 10);

			buyCol.Children.Add(btnBuyPrev);
			buyCol.Children.Add(btnBuyCurr);
			buyCol.Children.Add(btnBuyDist);
			Grid.SetColumn(buyCol, 2);

			StackPanel sellCol = new StackPanel();
			Button btnSellPrev = CreateButton("SELL previous", sellPrevBg, (s, ev) => PlaceOrder(OrderAction.Sell, false), 48, 12);
			btnSellPrev.Margin = new Thickness(0, 0, 0, 4);
			Button btnSellCurr = CreateButton("SELL current", sellCurrBg, (s, ev) => PlaceOrder(OrderAction.Sell, true), 24, 10);
			btnSellCurr.Margin = new Thickness(0, 0, 0, 4);
			Button btnSellDist = CreateButton("SELL -distance", sellFixedBg, (s, ev) => PlaceFixedDistanceOrder(OrderAction.Sell), 24, 10);

			sellCol.Children.Add(btnSellPrev);
			sellCol.Children.Add(btnSellCurr);
			sellCol.Children.Add(btnSellDist);
			Grid.SetColumn(sellCol, 0);

			orderBtnGrid.Children.Add(sellCol);
			orderBtnGrid.Children.Add(buyCol);
			sec3Panel.Children.Add(orderBtnGrid);
			mainPanel.Children.Add(CreateSectionCard(sec3Panel, 6));


			// --- SECTION 4: Market Orders & Position Management (Darker than BUY/SELL Distance) ---
			StackPanel sec4Panel = new StackPanel();

			Grid mktBtnGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			mktBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			mktBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			mktBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush buyMktBg  = new SolidColorBrush(Color.FromRgb(12, 48, 25)); // Deep dark green (darker than BUY Distance 16,56,30)
			SolidColorBrush sellMktBg = new SolidColorBrush(Color.FromRgb(55, 15, 18)); // Deep dark red (darker than SELL Distance 75,20,24)

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
			Button btnClose = CreateButton("Close/flatten", closeBg, (s, ev) => ClosePosition(), 33, 15);
			sec4Panel.Children.Add(btnClose);


			mainPanel.Children.Add(CreateSectionCard(sec4Panel, 0));


			panelBorder.Child = mainPanel;
		}

		private void AddGridRow(Grid grid, string labelText, FrameworkElement inputElement)
		{
			int rowIdx = grid.RowDefinitions.Count;
			grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

			TextBlock label = new TextBlock
			{
				Text = labelText,
				Foreground = Brushes.LightGray,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				FontSize = 11
			};
			Grid.SetRow(label, rowIdx);
			Grid.SetColumn(label, 0);
			grid.Children.Add(label);

			inputElement.VerticalAlignment = VerticalAlignment.Center;
			inputElement.HorizontalAlignment = HorizontalAlignment.Stretch;
			inputElement.Height = 22;
			Grid.SetRow(inputElement, rowIdx);
			Grid.SetColumn(inputElement, 1);
			grid.Children.Add(inputElement);
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
				hudStatusText.TextWrapping = TextWrapping.Wrap;
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
							hudStatusText.Visibility = Visibility.Collapsed;
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
			if (hudStatusTimer != null)
			{
				hudStatusTimer.Stop();
				hudStatusTimer = null;
			}
			if (panelBorder != null)
			{
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
			else if (key == HotkeyBuyDist && HotkeyBuyDist != Key.None)
			{
				PlaceFixedDistanceOrder(OrderAction.Buy);
				handled = true;
			}
			else if (key == HotkeySellDist && HotkeySellDist != Key.None)
			{
				PlaceFixedDistanceOrder(OrderAction.Sell);
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
				ClosePosition();
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
