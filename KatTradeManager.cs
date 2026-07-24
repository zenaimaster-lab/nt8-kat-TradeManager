/*
 * KatTradeManager.cs
 * Version: 0.02 (2026-07-24)
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
		public const string VERSION = "0.02";
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

		private Order entryOrder = null;
		private Order stopLossOrder = null;
		private Order takeProfitOrder = null;

		private double highestPriceSinceEntry = 0;
		private double lowestPriceSinceEntry = double.MaxValue;
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
				DefaultQuantity						= 1;
				DefaultStopLossTicks				= 20;
				DefaultTakeProfitTicks				= 40;
				DefaultTrailingSLTicks				= 15;
			}
			else if (State == State.Configure)
			{
				// Add Secondary Data Series for 30s, 1m, 2m
				AddDataSeries(BarsPeriodType.Second, 30); // BarsArray[1]
				AddDataSeries(BarsPeriodType.Minute, 1);  // BarsArray[2]
				AddDataSeries(BarsPeriodType.Minute, 2);  // BarsArray[3]
			}
			else if (State == State.DataLoaded)
			{
				if (Account.All != null && Account.All.Count > 0)
					account = Account.All.FirstOrDefault(a => a.Name == "Sim301") ?? Account.All.FirstOrDefault();

				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						CreateWpfControls();
					});
				}
			}
			else if (State == State.Terminated)
			{
				if (ChartControl != null)
				{
					ChartControl.Dispatcher.InvokeAsync(() =>
					{
						RemoveWpfControls();
					});
				}
			}
		}

		#region WPF UI Construction
		private void CreateWpfControls()
		{
			chartGrid = (Grid)ChartControl.Parent;
			if (chartGrid == null) return;

			panelBorder = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(220, 20, 24, 33)),
				BorderBrush = Brushes.DodgerBlue,
				BorderThickness = new Thickness(1.5),
				CornerRadius = new CornerRadius(6),
				Margin = new Thickness(10, 30, 0, 0),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Padding = new Thickness(8),
				Width = 220
			};

			mainPanel = new StackPanel();

			// Header Title
			TextBlock header = new TextBlock
			{
				Text = string.Format("⚡ KAT TradeManager v{0}", VERSION),
				Foreground = Brushes.Cyan,
				FontWeight = FontWeights.Bold,
				FontSize = 12,
				Margin = new Thickness(0, 0, 0, 8),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			mainPanel.Children.Add(header);

			// Timeframe Selection
			StackPanel tfPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
			tfPanel.Children.Add(new TextBlock { Text = "TF: ", Foreground = Brushes.White, Width = 60, VerticalAlignment = VerticalAlignment.Center });
			tfSelector = new ComboBox { Width = 130, Height = 22 };
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
			mainPanel.Children.Add(CreateButton("🟢 BUY STOP (Prev High)", Brushes.LimeGreen, (s, e) => PlacePendingStop(OrderAction.Buy, false)));
			mainPanel.Children.Add(CreateButton("🟢 BUY STOP (Curr High)", Brushes.ForestGreen, (s, e) => PlacePendingStop(OrderAction.Buy, true)));
			mainPanel.Children.Add(CreateButton("🔴 SELL STOP (Prev Low)", Brushes.Crimson, (s, e) => PlacePendingStop(OrderAction.Sell, false)));
			mainPanel.Children.Add(CreateButton("🔴 SELL STOP (Curr Low)", Brushes.DarkRed, (s, e) => PlacePendingStop(OrderAction.Sell, true)));

			// Management Buttons
			StackPanel mgrPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
			Button btnCancel = CreateSmallButton("Cancel Orders", Brushes.Orange, (s, e) => CancelAllOrders());
			Button btnClose = CreateSmallButton("Close Pos", Brushes.Red, (s, e) => ClosePosition());
			mgrPanel.Children.Add(btnCancel);
			mgrPanel.Children.Add(btnClose);
			mainPanel.Children.Add(mgrPanel);

			panelBorder.Child = mainPanel;
			chartGrid.Children.Add(panelBorder);
		}

		private UIElement CreateInputRow(string label, out TextBox textBox, string defaultValue)
		{
			StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
			row.Children.Add(new TextBlock { Text = label, Foreground = Brushes.LightGray, Width = 110, VerticalAlignment = VerticalAlignment.Center });
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
				Width = 98,
				Height = 24,
				Margin = new Thickness(2, 0, 2, 0),
				BorderThickness = new Thickness(0)
			};
			btn.Click += handler;
			return btn;
		}

		private void RemoveWpfControls()
		{
			if (chartGrid != null && panelBorder != null)
			{
				chartGrid.Children.Remove(panelBorder);
			}
		}
		#endregion

		#region Trading Operations
		private int GetBarsInProgressIndex()
		{
			if (tfSelector == null) return 0;
			switch (tfSelector.SelectedIndex)
			{
				case 1: return 1; // 30s
				case 2: return 2; // 1m
				case 3: return 3; // 2m
				default: return 0; // Chart TF
			}
		}

		private void PlacePendingStop(OrderAction action, bool isCurrentCandle)
		{
			if (account == null || Instrument == null) return;

			int barIdx = GetBarsInProgressIndex();
			int shift = isCurrentCandle ? 0 : 1;

			double triggerPrice = 0;

			if (action == OrderAction.Buy)
			{
				triggerPrice = Highs[barIdx][shift] + TickSize;
			}
			else
			{
				triggerPrice = Lows[barIdx][shift] - TickSize;
			}

			int qty = int.TryParse(txtQuantity.Text, out int q) ? q : DefaultQuantity;
			string entryName = action == OrderAction.Buy ? "KAT_BUY_STOP" : "KAT_SELL_STOP";

			entryOrder = account.CreateOrder(Instrument, action, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, qty, 0, triggerPrice, "", entryName, DateTime.MaxValue, null);
			account.Submit(new[] { entryOrder });
			Print(string.Format("[KatTradeManager] Submitted {0} Stop Order at {1} (BarIdx: {2}, Shift: {3})", action, triggerPrice, barIdx, shift));
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
			if (BarsInProgress != 0 || account == null || Instrument == null) return;

			// Handle Trailing SL logic on price updates
			if (chkEnableTrailing != null && chkEnableTrailing.IsChecked == true)
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos != null && pos.MarketPosition != MarketPosition.Flat)
				{
					int trailTicks = int.TryParse(txtTrailingSL.Text, out int t) ? t : DefaultTrailingSLTicks;
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
