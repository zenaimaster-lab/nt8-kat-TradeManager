/* KatTradeManagerUI.cs - WPF UI partial class for KatTradeManager v0.44 (2026-07-25) */

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

			AttachHotkeyHandler();

			if (!IsPanelVisible)
			{
				if (panelBorder != null) RemoveWpfControls();
				return;
			}

			// Sync UI control values to thread-safe cached fields
			SyncCachedValues();

			bool isAttached = panelBorder != null && chartGrid.Children.Contains(panelBorder);
			if (!isAttached)
			{
				panelBorder = null;
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


		private void CreateWpfControls()
		{
			if (ChartControl == null) return;
			chartGrid = ChartControl.Parent as Grid;
			if (chartGrid == null) return;

			panelBorder = new Border
			{
				Tag = "KatTradeManagerPanel",
				Background = new SolidColorBrush(Color.FromArgb(240, 20, 24, 33)),
				BorderBrush = Brushes.Transparent,
				BorderThickness = new Thickness(0),
				CornerRadius = new CornerRadius(6),
				Width = 240,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0, 30, 20, 0),
				Padding = new Thickness(8),
				Cursor = Cursors.SizeAll
			};
			System.Windows.Controls.Panel.SetZIndex(panelBorder, 9999);

			// Mouse Drag Support
			Point dragStart = new Point();
			bool isDragging = false;

			panelBorder.MouseLeftButtonDown += (s, ev) =>
			{
				dragStart = ev.GetPosition(chartGrid);
				panelBorder.CaptureMouse();
				isDragging = true;
				ev.Handled = true;
			};

			panelBorder.MouseMove += (s, ev) =>
			{
				if (isDragging)
				{
					Point current = ev.GetPosition(chartGrid);
					Thickness m = panelBorder.Margin;
					panelBorder.Margin = new Thickness(
						m.Left + (current.X - dragStart.X),
						m.Top + (current.Y - dragStart.Y), 0, 0);
					panelBorder.HorizontalAlignment = HorizontalAlignment.Left;
					dragStart = current;
				}
			};

			panelBorder.MouseLeftButtonUp += (s, ev) =>
			{
				if (isDragging)
				{
					panelBorder.ReleaseMouseCapture();
					isDragging = false;
				}
			};

			mainPanel = new StackPanel();

			// --- SECTION 1: Parameters & ATM Selection ---
			StackPanel sec1Panel = new StackPanel();

			sec1Panel.Children.Add(new TextBlock
			{
				Text = string.Format("⚡ KAT TradeManager v{0}", VERSION),
				Foreground = new SolidColorBrush(Color.FromRgb(70, 130, 160)),
				FontWeight = FontWeights.Bold,
				FontSize = 12,
				Margin = new Thickness(0, 0, 0, 6),
				HorizontalAlignment = HorizontalAlignment.Left
			});

			Grid paramGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
			paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			ComboBox accSelector = new ComboBox { FontSize = 11, Height = 22 };
			if (Account.All != null)
			{
				var allowedAccs = Account.All.Where(a => IsAccountAllowed(a.Name)).ToList();
				foreach (var acc in allowedAccs) accSelector.Items.Add(acc.Name);
				if (account != null && accSelector.Items.Contains(account.Name)) accSelector.SelectedItem = account.Name;
				else if (accSelector.Items.Count > 0) accSelector.SelectedIndex = 0;
			}
			accSelector.SelectionChanged += (s, ev) =>
			{
				if (accSelector.SelectedItem != null)
				{
					string selectedName = accSelector.SelectedItem.ToString();
					account = Account.All.FirstOrDefault(a => a.Name == selectedName);
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

			Grid ema89Grid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
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

			SolidColorBrush closeBg = new SolidColorBrush(Color.FromRgb(60, 14, 18)); // Deep dark crimson/maroon
			Button btnClose = CreateButton("Close/flatten", closeBg, (s, ev) => ClosePosition(), 33, 15);
			sec4Panel.Children.Add(btnClose);


			mainPanel.Children.Add(CreateSectionCard(sec4Panel, 0));

			panelBorder.Child = mainPanel;

			Grid.SetColumnSpan(panelBorder, 3);
			chartGrid.Children.Add(panelBorder);


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
			if (panelBorder != null && chartGrid != null && chartGrid.Children.Contains(panelBorder))
			{
				chartGrid.Children.Remove(panelBorder);
			}
			panelBorder = null;
		}

		private void AttachHotkeyHandler()
		{
			if (isHotkeyAttached || ChartControl == null) return;
			ChartControl.PreviewKeyDown += OnChartPreviewKeyDown;

			Window window = Window.GetWindow(ChartControl);
			if (window != null)
			{
				window.PreviewKeyDown -= OnChartPreviewKeyDown;
				window.PreviewKeyDown += OnChartPreviewKeyDown;
			}

			isHotkeyAttached = true;
		}

		private void DetachHotkeyHandler()
		{
			if (!isHotkeyAttached) return;
			if (ChartControl != null)
			{
				ChartControl.PreviewKeyDown -= OnChartPreviewKeyDown;
				Window window = Window.GetWindow(ChartControl);
				if (window != null)
				{
					window.PreviewKeyDown -= OnChartPreviewKeyDown;
				}
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
