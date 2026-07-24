/*
 * KatTradeManager.cs
 * Version: 0.04 (2026-07-24)
 * NinjaTrader 8 TradeManager Indicator
 */

#region Using declarations
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
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
	public class KatTradeManager : Indicator
	{
		public const string VERSION = "0.12";
		public const string RELEASE_DATE = "2026-07-24";

		#region Variables
		private volatile Account account;
		private Grid chartGrid;
		private Border panelBorder;
		private StackPanel mainPanel;
		private ComboBox tfSelector;
		private TextBox txtQuantity;
		private TextBox txtBuffer;
		private TextBox txtSL;
		private TextBox txtTP;
		private ComboBox atmSelector;
		private System.Windows.Threading.DispatcherTimer panelWatchdog;
		private bool isTerminated;

		// Thread-safe cached values from UI controls (synced by watchdog on UI thread)
		private volatile int cachedQuantity;
		private volatile int cachedTfIndex;
		private volatile int cachedBufferTicks;
		private volatile string cachedAtmTemplate = "";

		private Order entryOrder = null;

		// expected SL/TP price levels (only for drawing lines)
		private double expectedSLPrice = 0;
		private double expectedTPPrice = 0;
		private bool isExpectedLinesDrawn = false;

		// ponytail: cached bar prices updated on data thread, read from UI thread — avoids barsAgo exception
		private const int NUM_SERIES = 4; // chart + 30s + 1m + 2m
		private double[] cachedCurrentHigh = new double[NUM_SERIES];
		private double[] cachedCurrentLow  = new double[NUM_SERIES];
		private double[] cachedPrevHigh    = new double[NUM_SERIES];
		private double[] cachedPrevLow     = new double[NUM_SERIES];
		private double cachedTickSize;
		#endregion

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description							= @"Kat TradeManager v" + VERSION + @" for NinjaTrader 8 with Candle-based Pending Stop Orders and Trailing SL.";
				Name								= "KatTradeManager";
				Calculate							= Calculate.OnPriceChange;
				IsOverlay							= true;
				DisplayInDataBox					= false;
				DrawHorizontalGridLines				= false;
				DrawVerticalGridLines				= false;
				IsAutoScale							= false;

				// Default Settings
				IsPanelVisible						= true;
				DefaultQuantity						= 1;
				DefaultStopLossTicks				= 20;
				DefaultTakeProfitTicks				= 40;
				AccountName							= "Sim101";
				DefaultBufferTicks                  = 2;
				DefaultAtmTemplate                  = "Sim101_ATM";
			}
			else if (State == State.Configure)
			{
				AddDataSeries(BarsPeriodType.Second, 30);
				AddDataSeries(BarsPeriodType.Minute, 1);
				AddDataSeries(BarsPeriodType.Minute, 2);
			}
			else if (State == State.DataLoaded)
			{
				isTerminated = false;
				cachedQuantity = DefaultQuantity;
				cachedTfIndex = 0;
				cachedTickSize = TickSize;
				cachedBufferTicks = DefaultBufferTicks;
				cachedAtmTemplate = DefaultAtmTemplate;
				Print(string.Format("[KatTradeManager] v{0} loaded — cached mode active", VERSION));

				if (Account.All != null && Account.All.Count > 0)
				{
					Print("[KatTradeManager] Available Accounts:");
					foreach (var acc in Account.All)
					{
						Print(string.Format("  - {0} ({1})", acc.Name, acc.Connection != null ? "Connected" : "Disconnected"));
					}

					account = Account.All.FirstOrDefault(a => a.Name.Equals(AccountName, StringComparison.OrdinalIgnoreCase))
					          ?? Account.All.FirstOrDefault(a => a.Name == "Sim101")
					          ?? Account.All.FirstOrDefault(a => a.Name == "Sim301")
					          ?? Account.All.FirstOrDefault(a => a.Connection != null)
					          ?? Account.All.FirstOrDefault();

					if (account != null)
					{
						Print(string.Format("[KatTradeManager] Selected Account: {0}", account.Name));
					}
				}

				if (ChartControl != null)
					ChartControl.Dispatcher.InvokeAsync(StartPanelWatchdog);
			}
			else if (State == State.Terminated)
			{
				isTerminated = true;

				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						StopPanelWatchdog();
						RemoveWpfControls();
					});
				}
			}
		}

		#region WPF UI Construction

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
				return;
			}

			chartGrid = ChartControl.Parent as Grid;
			if (chartGrid == null) return;

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

		// ponytail: Sync WPF control values → plain volatile fields so OnBarUpdate (data thread) never touches WPF.
		private void SyncCachedValues()
		{
			if (txtQuantity != null)
				cachedQuantity = int.TryParse(txtQuantity.Text, out int q) ? q : DefaultQuantity;
			if (tfSelector != null)
				cachedTfIndex = tfSelector.SelectedIndex;
			if (txtBuffer != null)
				cachedBufferTicks = int.TryParse(txtBuffer.Text, out int b) ? b : DefaultBufferTicks;
			if (atmSelector != null && atmSelector.SelectedItem != null)
				cachedAtmTemplate = atmSelector.SelectedItem.ToString();
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
				BorderBrush = Brushes.DodgerBlue,
				BorderThickness = new Thickness(1.5),
				CornerRadius = new CornerRadius(6),
				Width = 210,
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

			// Header Title
			mainPanel.Children.Add(new TextBlock
			{
				Text = string.Format("⚡ KAT TradeManager v{0}", VERSION),
				Foreground = Brushes.Cyan,
				FontWeight = FontWeights.Bold,
				FontSize = 12,
				Margin = new Thickness(0, 0, 0, 8),
				HorizontalAlignment = HorizontalAlignment.Center
			});

			// Account Selection
			StackPanel accPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
			accPanel.Children.Add(new TextBlock { Text = "Acc: ", Foreground = Brushes.White, Width = 55, VerticalAlignment = VerticalAlignment.Center });
			ComboBox accSelector = new ComboBox { Width = 125, Height = 22 };
			if (Account.All != null)
			{
				foreach (var acc in Account.All)
				{
					accSelector.Items.Add(acc.Name);
				}
				if (account != null)
				{
					accSelector.SelectedItem = account.Name;
				}
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
			accPanel.Children.Add(accSelector);
			mainPanel.Children.Add(accPanel);

			// Timeframe Selection
			StackPanel tfPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
			tfPanel.Children.Add(new TextBlock { Text = "TF: ", Foreground = Brushes.White, Width = 55, VerticalAlignment = VerticalAlignment.Center });
			tfSelector = new ComboBox { Width = 125, Height = 22 };
			tfSelector.Items.Add("Chart TF");
			tfSelector.Items.Add("30s");
			tfSelector.Items.Add("1m");
			tfSelector.Items.Add("2m");
			tfSelector.SelectedIndex = 0;
			tfPanel.Children.Add(tfSelector);
			mainPanel.Children.Add(tfPanel);

			// Quantity & SL/TP Inputs
			mainPanel.Children.Add(CreateInputRow("Contracts:", out txtQuantity, DefaultQuantity.ToString()));
			mainPanel.Children.Add(CreateInputRow("Buffer (Ticks):", out txtBuffer, DefaultBufferTicks.ToString()));
			mainPanel.Children.Add(CreateInputRow("SL (Ticks):", out txtSL, DefaultStopLossTicks.ToString()));
			mainPanel.Children.Add(CreateInputRow("TP (Ticks):", out txtTP, DefaultTakeProfitTicks.ToString()));

			// ATM Selection Dropdown
			StackPanel atmPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
			atmPanel.Children.Add(new TextBlock { Text = "ATM: ", Foreground = Brushes.White, Width = 55, VerticalAlignment = VerticalAlignment.Center });
			atmSelector = new ComboBox { Width = 125, Height = 22 };
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
			atmSelector.SelectionChanged += (s, ev) =>
			{
				if (atmSelector.SelectedItem != null)
				{
					cachedAtmTemplate = atmSelector.SelectedItem.ToString();
				}
			};
			atmPanel.Children.Add(atmSelector);
			mainPanel.Children.Add(atmPanel);

			// Buttons Section
			mainPanel.Children.Add(CreateButton("🟢 BUY STOP (Prev High)", Brushes.LimeGreen, (s, ev) => PlacePendingStop(OrderAction.Buy, false)));
			mainPanel.Children.Add(CreateButton("🟢 BUY STOP (Curr High)", Brushes.ForestGreen, (s, ev) => PlacePendingStop(OrderAction.Buy, true)));
			mainPanel.Children.Add(CreateButton("🔴 SELL STOP (Prev Low)", Brushes.Crimson, (s, ev) => PlacePendingStop(OrderAction.Sell, false)));
			mainPanel.Children.Add(CreateButton("🔴 SELL STOP (Curr Low)", Brushes.DarkRed, (s, ev) => PlacePendingStop(OrderAction.Sell, true)));

			// Management Buttons
			StackPanel mgrPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
			mgrPanel.Children.Add(CreateSmallButton("Cancel", Brushes.Orange, (s, ev) => CancelAllOrders()));
			mgrPanel.Children.Add(CreateSmallButton("Close", Brushes.Red, (s, ev) => ClosePosition()));
			mainPanel.Children.Add(mgrPanel);

			panelBorder.Child = mainPanel;

			// Attach to chartGrid
			Grid.SetColumnSpan(panelBorder, 3);
			chartGrid.Children.Add(panelBorder);
		}

		private UIElement CreateInputRow(string label, out TextBox textBox, string defaultValue)
		{
			StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
			row.Children.Add(new TextBlock { Text = label, Foreground = Brushes.LightGray, Width = 100, VerticalAlignment = VerticalAlignment.Center });
			textBox = new TextBox { Text = defaultValue, Width = 80, Height = 20, Background = Brushes.Black, Foreground = Brushes.White, BorderBrush = Brushes.Gray };
			row.Children.Add(textBox);
			return row;
		}

		private Button CreateButton(string text, Brush bg, RoutedEventHandler handler)
		{
			Button btn = new Button
			{
				Content = text,
				Background = bg,
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0, 2, 0, 2),
				Padding = new Thickness(4),
				Height = 26,
				BorderThickness = new Thickness(0)
			};
			btn.Click += handler;
			return btn;
		}

		private Button CreateSmallButton(string text, Brush bg, RoutedEventHandler handler)
		{
			Button btn = new Button
			{
				Content = text,
				Background = bg,
				Foreground = Brushes.White,
				Width = 90,
				Height = 24,
				Margin = new Thickness(2, 0, 2, 0),
				BorderThickness = new Thickness(0)
			};
			btn.Click += handler;
			return btn;
		}

		private void RemoveWpfControls()
		{
			if (panelBorder != null && chartGrid != null && chartGrid.Children.Contains(panelBorder))
			{
				chartGrid.Children.Remove(panelBorder);
			}
			panelBorder = null;
		}
		#endregion

		#region Trading Operations
		private int GetBarsInProgressIndex()
		{
			// ponytail: reads cached value (synced from UI thread) instead of WPF control
			switch (cachedTfIndex)
			{
				case 1: return 1; // 30s
				case 2: return 2; // 1m
				case 3: return 3; // 2m
				default: return 0; // Chart TF
			}
		}

		// ponytail: reads only cached values — zero bar data access from UI thread
		private void PlacePendingStop(OrderAction action, bool isCurrentCandle)
		{
			if (account == null || Instrument == null) return;

			try
			{
				Print(string.Format("[KatTradeManager] Debug: Account={0}, Connection={1}, Instrument={2}, MasterInstrument={3}",
					account.Name,
					account.Connection != null ? "not null" : "null",
					Instrument.FullName,
					Instrument.MasterInstrument != null ? Instrument.MasterInstrument.Name : "null"
				));
				int barIdx = GetBarsInProgressIndex();
				if (barIdx < 0 || barIdx >= NUM_SERIES) return;

				double basePrice = 0;
				if (action == OrderAction.Buy)
				{
					basePrice = isCurrentCandle ? cachedCurrentHigh[barIdx] : cachedPrevHigh[barIdx];
				}
				else
				{
					basePrice = isCurrentCandle ? cachedCurrentLow[barIdx] : cachedPrevLow[barIdx];
				}

				if (basePrice <= 0)
				{
					Print(string.Format("[KatTradeManager] No valid cached price for series {0} yet", barIdx));
					return;
				}

				double triggerPrice = action == OrderAction.Buy 
					? basePrice + (cachedBufferTicks * cachedTickSize) 
					: basePrice - (cachedBufferTicks * cachedTickSize);

				int qty = cachedQuantity > 0 ? cachedQuantity : DefaultQuantity;
				string entryName = "Entry"; // Required for ATM linkage

				entryOrder = account.CreateOrder(Instrument, action, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, qty, 0, triggerPrice, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
				if (entryOrder != null)
				{
					// Start ATM Strategy
					if (!string.IsNullOrEmpty(cachedAtmTemplate))
					{
						NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(cachedAtmTemplate, entryOrder);
						Print(string.Format("[KatTradeManager] Started ATM Strategy '{0}' for Stop Order at {1} (BarIdx: {2})", cachedAtmTemplate, triggerPrice, barIdx));
					}
					else
					{
						account.Submit(new[] { entryOrder });
						Print(string.Format("[KatTradeManager] Submitted Stop Order at {0} (BarIdx: {1}, No ATM)", triggerPrice, barIdx));
					}

					// Draw visual SL/TP lines
					int slTicks = int.TryParse(txtSL.Text, out int s) ? s : DefaultStopLossTicks;
					int tpTicks = int.TryParse(txtTP.Text, out int t) ? t : DefaultTakeProfitTicks;

					if (action == OrderAction.Buy)
					{
						expectedSLPrice = triggerPrice - (slTicks * cachedTickSize);
						expectedTPPrice = triggerPrice + (tpTicks * cachedTickSize);
					}
					else
					{
						expectedSLPrice = triggerPrice + (slTicks * cachedTickSize);
						expectedTPPrice = triggerPrice - (tpTicks * cachedTickSize);
					}

					if (slTicks > 0)
						Draw.Line(this, "KAT_SL_LINE", false, 15, expectedSLPrice, -10, expectedSLPrice, Brushes.Red, DashStyleHelper.Dash, 2);
					if (tpTicks > 0)
						Draw.Line(this, "KAT_TP_LINE", false, 15, expectedTPPrice, -10, expectedTPPrice, Brushes.Green, DashStyleHelper.Dash, 2);

					isExpectedLinesDrawn = true;
					ForceRefresh();
				}
				else
				{
					Print("[KatTradeManager] Error: CreateOrder returned null");
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing order: {0}", ex.ToString()));
			}
		}

		private void CancelAllOrders()
		{
			if (account == null) return;
			foreach (Order order in account.Orders.Where(o => o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
			{
				account.Cancel(new[] { order });
			}
			RemoveDrawObject("KAT_SL_LINE");
			RemoveDrawObject("KAT_TP_LINE");
			isExpectedLinesDrawn = false;
			expectedSLPrice = 0;
			expectedTPPrice = 0;
			ForceRefresh();
		}

		private void ClosePosition()
		{
			if (account == null || Instrument == null) return;
			Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
			if (pos != null && pos.MarketPosition != MarketPosition.Flat)
			{
				OrderAction action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.Buy;
				Order closeOrder = account.CreateOrder(Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, 0, "", "KAT_CLOSE", NinjaTrader.Core.Globals.MaxDate, null);
				account.Submit(new[] { closeOrder });
			}
			CancelAllOrders();
		}
		#endregion

		#region OnBarUpdate & Visual Lines Auto-clean
		protected override void OnBarUpdate()
		{
			try
			{
				// Cache bar prices for ALL series so UI thread can read them safely
				int bip = BarsInProgress;
				if (bip < NUM_SERIES && CurrentBars[bip] >= 0)
				{
					cachedCurrentHigh[bip] = Highs[bip][0];
					cachedCurrentLow[bip]  = Lows[bip][0];
					if (CurrentBars[bip] >= 1)
					{
						cachedPrevHigh[bip] = Highs[bip][1];
						cachedPrevLow[bip]  = Lows[bip][1];
					}
				}

				if (bip != 0 || account == null || Instrument == null) return;

				// Monitor entryOrder to auto-remove SL/TP lines when no longer working
				if (entryOrder != null)
				{
					bool isWorking = entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted;
					if (!isWorking && isExpectedLinesDrawn)
					{
						RemoveDrawObject("KAT_SL_LINE");
						RemoveDrawObject("KAT_TP_LINE");
						isExpectedLinesDrawn = false;
						expectedSLPrice = 0;
						expectedTPPrice = 0;
					}
				}
				else if (isExpectedLinesDrawn)
				{
					RemoveDrawObject("KAT_SL_LINE");
					RemoveDrawObject("KAT_TP_LINE");
					isExpectedLinesDrawn = false;
					expectedSLPrice = 0;
					expectedTPPrice = 0;
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] OnBarUpdate error: {0}", ex.Message));
			}
		}
		#endregion

		#region Properties
		[NinjaScriptProperty]
		[Display(Name="Show Control Panel", Order=0, GroupName="Parameters")]
		public bool IsPanelVisible { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Default Quantity", Order=1, GroupName="Parameters")]
		public int DefaultQuantity { get; set; }

		[NinjaScriptProperty]
		[Range(1, 1000)]
		[Display(Name="Default StopLoss (Ticks)", Order=2, GroupName="Parameters")]
		public int DefaultStopLossTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 1000)]
		[Display(Name="Default TakeProfit (Ticks)", Order=3, GroupName="Parameters")]
		public int DefaultTakeProfitTicks { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Account Name", Order=4, GroupName="Parameters")]
		public string AccountName { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Default Buffer (Ticks)", Order=5, GroupName="Parameters")]
		public int DefaultBufferTicks { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Default ATM Template", Order=6, GroupName="Parameters")]
		public string DefaultAtmTemplate { get; set; }
		#endregion
	}
}



#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private KatTradeManager[] cacheKatTradeManager;
		public KatTradeManager KatTradeManager(bool isPanelVisible, int defaultQuantity, int defaultStopLossTicks, int defaultTakeProfitTicks, string accountName, int defaultBufferTicks, string defaultAtmTemplate)
		{
			return KatTradeManager(Input, isPanelVisible, defaultQuantity, defaultStopLossTicks, defaultTakeProfitTicks, accountName, defaultBufferTicks, defaultAtmTemplate);
		}

		public KatTradeManager KatTradeManager(ISeries<double> input, bool isPanelVisible, int defaultQuantity, int defaultStopLossTicks, int defaultTakeProfitTicks, string accountName, int defaultBufferTicks, string defaultAtmTemplate)
		{
			if (cacheKatTradeManager != null)
				for (int idx = 0; idx < cacheKatTradeManager.Length; idx++)
					if (cacheKatTradeManager[idx] != null && cacheKatTradeManager[idx].IsPanelVisible == isPanelVisible && cacheKatTradeManager[idx].DefaultQuantity == defaultQuantity && cacheKatTradeManager[idx].DefaultStopLossTicks == defaultStopLossTicks && cacheKatTradeManager[idx].DefaultTakeProfitTicks == defaultTakeProfitTicks && cacheKatTradeManager[idx].AccountName == accountName && cacheKatTradeManager[idx].DefaultBufferTicks == defaultBufferTicks && cacheKatTradeManager[idx].DefaultAtmTemplate == defaultAtmTemplate && cacheKatTradeManager[idx].EqualsInput(input))
						return cacheKatTradeManager[idx];
			return CacheIndicator<KatTradeManager>(new KatTradeManager(){ IsPanelVisible = isPanelVisible, DefaultQuantity = defaultQuantity, DefaultStopLossTicks = defaultStopLossTicks, DefaultTakeProfitTicks = defaultTakeProfitTicks, AccountName = accountName, DefaultBufferTicks = defaultBufferTicks, DefaultAtmTemplate = defaultAtmTemplate }, input, ref cacheKatTradeManager);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.KatTradeManager KatTradeManager(bool isPanelVisible, int defaultQuantity, int defaultStopLossTicks, int defaultTakeProfitTicks, string accountName, int defaultBufferTicks, string defaultAtmTemplate)
		{
			return indicator.KatTradeManager(Input, isPanelVisible, defaultQuantity, defaultStopLossTicks, defaultTakeProfitTicks, accountName, defaultBufferTicks, defaultAtmTemplate);
		}

		public Indicators.KatTradeManager KatTradeManager(ISeries<double> input , bool isPanelVisible, int defaultQuantity, int defaultStopLossTicks, int defaultTakeProfitTicks, string accountName, int defaultBufferTicks, string defaultAtmTemplate)
		{
			return indicator.KatTradeManager(input, isPanelVisible, defaultQuantity, defaultStopLossTicks, defaultTakeProfitTicks, accountName, defaultBufferTicks, defaultAtmTemplate);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.KatTradeManager KatTradeManager(bool isPanelVisible, int defaultQuantity, int defaultStopLossTicks, int defaultTakeProfitTicks, string accountName, int defaultBufferTicks, string defaultAtmTemplate)
		{
			return indicator.KatTradeManager(Input, isPanelVisible, defaultQuantity, defaultStopLossTicks, defaultTakeProfitTicks, accountName, defaultBufferTicks, defaultAtmTemplate);
		}

		public Indicators.KatTradeManager KatTradeManager(ISeries<double> input , bool isPanelVisible, int defaultQuantity, int defaultStopLossTicks, int defaultTakeProfitTicks, string accountName, int defaultBufferTicks, string defaultAtmTemplate)
		{
			return indicator.KatTradeManager(input, isPanelVisible, defaultQuantity, defaultStopLossTicks, defaultTakeProfitTicks, accountName, defaultBufferTicks, defaultAtmTemplate);
		}
	}
}

#endregion
