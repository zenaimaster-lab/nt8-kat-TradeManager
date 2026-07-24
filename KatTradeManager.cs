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
		public const string VERSION = "0.11";
		public const string RELEASE_DATE = "2026-07-24";

		#region Variables
		private Account account;
		private Grid chartGrid;
		private Border panelBorder;
		private StackPanel mainPanel;
		private ComboBox tfSelector;
		private TextBox txtQuantity;
		private TextBox txtSL;
		private TextBox txtTP;
		private TextBox txtTrailingSL;
		private CheckBox chkEnableTrailing;
		private System.Windows.Threading.DispatcherTimer panelWatchdog;
		private bool isTerminated;

		// Thread-safe cached values from UI controls (synced by watchdog on UI thread)
		private volatile bool cachedTrailingEnabled = true;
		private volatile int cachedTrailTicks;
		private volatile int cachedQuantity;
		private volatile int cachedTfIndex;

		private Order entryOrder = null;
		private Order stopLossOrder = null;
		private Order takeProfitOrder = null;

		private double highestPriceSinceEntry = 0;
		private double lowestPriceSinceEntry = double.MaxValue;

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
				DefaultTrailingSLTicks				= 15;
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
				cachedTrailTicks = DefaultTrailingSLTicks;
				cachedQuantity = DefaultQuantity;
				cachedTfIndex = 0;
				cachedTickSize = TickSize;
				Print(string.Format("[KatTradeManager] v{0} loaded — cached mode active", VERSION));

				if (Account.All != null && Account.All.Count > 0)
					account = Account.All.FirstOrDefault(a => a.Name == "Sim301") ?? Account.All.FirstOrDefault();

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
			if (chkEnableTrailing != null)
				cachedTrailingEnabled = chkEnableTrailing.IsChecked == true;
			if (txtTrailingSL != null)
				cachedTrailTicks = int.TryParse(txtTrailingSL.Text, out int t) ? t : DefaultTrailingSLTicks;
			if (txtQuantity != null)
				cachedQuantity = int.TryParse(txtQuantity.Text, out int q) ? q : DefaultQuantity;
			if (tfSelector != null)
				cachedTfIndex = tfSelector.SelectedIndex;
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
			mainPanel.Children.Add(CreateInputRow("SL (Ticks):", out txtSL, DefaultStopLossTicks.ToString()));
			mainPanel.Children.Add(CreateInputRow("TP (Ticks):", out txtTP, DefaultTakeProfitTicks.ToString()));
			mainPanel.Children.Add(CreateInputRow("Trail (Ticks):", out txtTrailingSL, DefaultTrailingSLTicks.ToString()));

			// Enable Trailing Checkbox
			chkEnableTrailing = new CheckBox
			{
				Content = "Enable Trailing SL",
				Foreground = Brushes.Yellow,
				IsChecked = true,
				Margin = new Thickness(0, 5, 0, 8)
			};
			mainPanel.Children.Add(chkEnableTrailing);

			// Buttons Section
			mainPanel.Children.Add(CreateButton("🟢 BUY STOP (Prev High)", Brushes.LimeGreen, (s, ev) => PlacePendingStop(OrderAction.Buy, false)));
			mainPanel.Children.Add(CreateButton("🟢 BUY STOP (Curr High)", Brushes.ForestGreen, (s, ev) => PlacePendingStop(OrderAction.Buy, true)));
			mainPanel.Children.Add(CreateButton("🔴 SELL STOP (Prev Low)", Brushes.Crimson, (s, ev) => PlacePendingStop(OrderAction.Sell, false)));
			mainPanel.Children.Add(CreateButton("🔴 SELL STOP (Curr Low)", Brushes.DarkRed, (s, ev) => PlacePendingStop(OrderAction.Sell, true)));

			// Management Buttons
			StackPanel mgrPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
			mgrPanel.Children.Add(CreateSmallButton("Cancel Orders", Brushes.Orange, (s, ev) => CancelAllOrders()));
			mgrPanel.Children.Add(CreateSmallButton("Close Pos", Brushes.Red, (s, ev) => ClosePosition()));
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

				double triggerPrice = action == OrderAction.Buy ? basePrice + cachedTickSize : basePrice - cachedTickSize;
				int qty = cachedQuantity > 0 ? cachedQuantity : DefaultQuantity;
				string entryName = action == OrderAction.Buy ? "KAT_BUY_STOP" : "KAT_SELL_STOP";

				entryOrder = account.CreateOrder(Instrument, action, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, qty, 0, triggerPrice, "", entryName, DateTime.MaxValue, null);
				if (entryOrder != null)
				{
					account.Submit(new[] { entryOrder });
					Print(string.Format("[KatTradeManager] Submitted {0} Stop Order at {1} (BarIdx: {2})", action, triggerPrice, barIdx));
				}
				else
				{
					Print("[KatTradeManager] Error: CreateOrder returned null");
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing order: {0}", ex.Message));
			}
		}

		private void CancelAllOrders()
		{
			if (account == null) return;
			foreach (Order order in account.Orders.Where(o => o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
			{
				account.Cancel(new[] { order });
			}
		}

		private void ClosePosition()
		{
			if (account == null || Instrument == null) return;
			Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
			if (pos != null && pos.MarketPosition != MarketPosition.Flat)
			{
				OrderAction action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.Buy;
				Order closeOrder = account.CreateOrder(Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, 0, "", "KAT_CLOSE", DateTime.MaxValue, null);
				account.Submit(new[] { closeOrder });
			}
			CancelAllOrders();
		}
		#endregion

		#region OnBarUpdate & Trailing SL
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

				// ponytail: reads cached volatile fields only — never touch WPF controls from data thread
				if (cachedTrailingEnabled)
				{
					Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
					if (pos != null && pos.MarketPosition != MarketPosition.Flat)
					{
						int trailTicks = cachedTrailTicks > 0 ? cachedTrailTicks : DefaultTrailingSLTicks;
						double currentPrice = Close[0];

						if (pos.MarketPosition == MarketPosition.Long)
						{
							highestPriceSinceEntry = Math.Max(highestPriceSinceEntry, currentPrice);
							double newSLPrice = highestPriceSinceEntry - (trailTicks * TickSize);
							UpdateStopLoss(pos, newSLPrice, true);
						}
						else if (pos.MarketPosition == MarketPosition.Short)
						{
							lowestPriceSinceEntry = Math.Min(lowestPriceSinceEntry, currentPrice);
							double newSLPrice = lowestPriceSinceEntry + (trailTicks * TickSize);
							UpdateStopLoss(pos, newSLPrice, false);
						}
					}
					else
					{
						highestPriceSinceEntry = 0;
						lowestPriceSinceEntry = double.MaxValue;
					}
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] OnBarUpdate error: {0}", ex.Message));
			}
		}

		private void UpdateStopLoss(Position pos, double newSLPrice, bool isLong)
		{
			Order slOrder = account.Orders.FirstOrDefault(o => o.Instrument == Instrument && o.OrderState == OrderState.Working && o.Name.Contains("Stop"));
			if (slOrder != null)
			{
				if ((isLong && newSLPrice > slOrder.StopPrice) || (!isLong && newSLPrice < slOrder.StopPrice))
				{
					slOrder.StopPrice = newSLPrice;
					account.Change(new[] { slOrder });
					Print(string.Format("[KatTradeManager] Trailed SL to {0}", newSLPrice));
				}
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
		[Range(1, 1000)]
		[Display(Name="Default Trailing SL (Ticks)", Order=4, GroupName="Parameters")]
		public int DefaultTrailingSLTicks { get; set; }
		#endregion
	}
}
