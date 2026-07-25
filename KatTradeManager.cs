/*
 * KatTradeManager.cs
 * Version: 0.23 (2026-07-25)
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
	public partial class KatTradeManager : Indicator
	{
		#region Metadata & Variables
		public const string VERSION = "0.23";
		public const string RELEASE_DATE = "2026-07-25";

		private volatile Account account;
		private Grid chartGrid;
		private Border panelBorder;
		private StackPanel mainPanel;
		private ComboBox tfSelector;
		private TextBox txtQuantity;
		private TextBox txtBuffer;
		private TextBox txtDistance;
		private ComboBox atmSelector;
		private System.Windows.Threading.DispatcherTimer panelWatchdog;
		private bool isTerminated;

		// Thread-safe cached values from UI controls (synced by watchdog on UI thread)
		private volatile int cachedQuantity;
		private volatile int cachedTfIndex;
		private volatile int cachedBufferTicks;
		private volatile int cachedDistanceTicks;
		private volatile string cachedAtmTemplate = "";

		private volatile Order entryOrder = null;

		// Parsed ATM parameters for line drawing
		private int atmStopLoss = 0;
		private int atmTarget = 0;
		private int atmBETrigger = 0;
		private int atmSL1Trigger = 0;
		private int atmSL2Trigger = 0;
		private int atmQuantity = 1;

		private bool isExpectedLinesDrawn = false;

		// Pending-draw state: UI thread sets flags, OnBarUpdate (data thread) executes Draw calls
		private volatile bool pendingDrawRequest = false;
		private volatile bool pendingRemoveLines = false;
		private KatTradeCalculator.AtmLevels pendingLevels;
		private double pendingEntryPrice;
		private int pendingAtmStopLoss;
		private int pendingAtmTarget;
		private int pendingAtmBETrigger;
		private int pendingAtmSL1Trigger;
		private int pendingAtmSL2Trigger;

		// Thread synchronization lock for bar price caching
		private readonly object priceLock = new object();
		private const int NUM_SERIES = 4; // chart + 30s + 1m + 2m
		private double[] cachedCurrentHigh = new double[NUM_SERIES];
		private double[] cachedCurrentLow  = new double[NUM_SERIES];
		private double[] cachedPrevHigh    = new double[NUM_SERIES];
		private double[] cachedPrevLow     = new double[NUM_SERIES];
		private double cachedTickSize;
		private double cachedCurrentPrice;
		#endregion

		#region Indicator Lifecycle
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
				AccountName							= "Sim101";
				DefaultBufferTicks                  = 2;
				DefaultDistanceTicks                = 320; // Default 80 points = 320 ticks
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
				cachedDistanceTicks = DefaultDistanceTicks;
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
		#endregion

		// ponytail: WPF UI methods extracted to src/KatTradeManagerUI.cs (partial class)

		#region Price Caching & OnBarUpdate
		protected override void OnBarUpdate()
		{
			try
			{
				int bip = BarsInProgress;
				if (bip < NUM_SERIES && CurrentBars[bip] >= 0)
				{
					lock (priceLock)
					{
						cachedCurrentHigh[bip] = Highs[bip][0];
						cachedCurrentLow[bip]  = Lows[bip][0];
						if (bip == 0)
						{
							cachedCurrentPrice = Closes[0][0];
						}
						if (CurrentBars[bip] >= 1)
						{
							cachedPrevHigh[bip] = Highs[bip][1];
							cachedPrevLow[bip]  = Lows[bip][1];
						}
					}
				}

				if (bip != 0 || account == null || Instrument == null) return;

				// Process pending remove request (from CancelAllOrders on UI thread)
				if (pendingRemoveLines)
				{
					pendingRemoveLines = false;
					pendingDrawRequest = false; // cancel any pending draw too
					RemoveExpectedLines();
				}

				// Process pending draw request (from PlaceOrderInternal on UI thread)
				if (pendingDrawRequest)
				{
					pendingDrawRequest = false;
					DrawExpectedLines();
				}

				// Auto-remove lines when order reaches terminal state
				if (entryOrder != null)
				{
					var state = entryOrder.OrderState;
					bool isWorking = state == OrderState.Working || state == OrderState.Accepted;
					bool isTerminal = state == OrderState.Filled || state == OrderState.Cancelled || state == OrderState.Rejected;
					if (!isWorking && isExpectedLinesDrawn)
					{
						RemoveExpectedLines();
					}
					if (isTerminal)
					{
						entryOrder = null;
					}
				}
				else if (isExpectedLinesDrawn)
				{
					RemoveExpectedLines();
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] OnBarUpdate error: {0}", ex.Message));
			}
		}
		#endregion

		#region Order Execution & Trading Operations
		private int GetBarsInProgressIndex()
		{
			switch (cachedTfIndex)
			{
				case 1: return 1; // 30s
				case 2: return 2; // 1m
				case 3: return 3; // 2m
				default: return 0; // Chart TF
			}
		}

		private void PlaceOrder(OrderAction action, bool isCurrentCandle)
		{
			if (account == null || Instrument == null) return;

			try
			{
				int barIdx = GetBarsInProgressIndex();
				if (barIdx < 0 || barIdx >= NUM_SERIES) return;

				double basePrice = 0;
				double currentPx = 0;

				lock (priceLock)
				{
					if (action == OrderAction.Buy)
					{
						basePrice = isCurrentCandle ? cachedCurrentHigh[barIdx] : cachedPrevHigh[barIdx];
					}
					else
					{
						basePrice = isCurrentCandle ? cachedCurrentLow[barIdx] : cachedPrevLow[barIdx];
					}
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}

				if (basePrice <= 0) return;

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(action, basePrice, cachedBufferTicks, cachedTickSize);
				OrderType orderType = KatTradeCalculator.DetermineOrderType(action, triggerPrice, currentPx, out double limitPrice, out double stopPrice);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing order");
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing order: {0}", ex.ToString()));
			}
		}

		private void PlaceFixedDistanceOrder(OrderAction action)
		{
			if (account == null || Instrument == null) return;

			try
			{
				double currentPx = 0;
				lock (priceLock)
				{
					currentPx = cachedCurrentPrice;
					if (currentPx <= 0)
					{
						int barIdx = GetBarsInProgressIndex();
						if (barIdx >= 0 && barIdx < NUM_SERIES)
							currentPx = cachedCurrentHigh[barIdx] > 0 ? cachedCurrentHigh[barIdx] : 0;
					}
				}

				if (currentPx <= 0) return;

				int distTicks = cachedDistanceTicks > 0 ? cachedDistanceTicks : DefaultDistanceTicks;
				double triggerPrice = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(action, currentPx, distTicks, cachedTickSize);

				OrderType orderType = KatTradeCalculator.DetermineOrderType(action, triggerPrice, currentPx, out double limitPrice, out double stopPrice);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing fixed distance order");
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing fixed distance order: {0}", ex.ToString()));
			}
		}

		private void PlaceOrderInternal(OrderAction action, double triggerPrice, OrderType orderType, double limitPrice, double stopPrice, string errorContext)
		{
			if (account == null || Instrument == null) return;

			try
			{
				int qty = cachedQuantity > 0 ? cachedQuantity : DefaultQuantity;
				string entryName = "Entry";

				entryOrder = account.CreateOrder(Instrument, action, orderType, OrderEntry.Manual, TimeInForce.Gtc, qty, limitPrice, stopPrice, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
				if (entryOrder != null)
				{
					if (!string.IsNullOrEmpty(cachedAtmTemplate))
					{
						NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(cachedAtmTemplate, entryOrder);
					}
					else
					{
						account.Submit(new[] { entryOrder });
					}

					// Store pending draw request — OnBarUpdate (data thread) will execute the actual Draw calls
					lock (priceLock)
					{
						pendingLevels = KatTradeCalculator.CalculateAtmLevels(
							action, triggerPrice, atmStopLoss, atmTarget, atmBETrigger, atmSL1Trigger, atmSL2Trigger, cachedTickSize);
						pendingEntryPrice = triggerPrice;
						pendingAtmStopLoss = atmStopLoss;
						pendingAtmTarget = atmTarget;
						pendingAtmBETrigger = atmBETrigger;
						pendingAtmSL1Trigger = atmSL1Trigger;
						pendingAtmSL2Trigger = atmSL2Trigger;
					}
					pendingDrawRequest = true;
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error {0}: {1}", errorContext, ex.ToString()));
			}
		}

		private void CancelAllOrders()
		{
			if (account == null) return;
			foreach (Order order in account.Orders.Where(o => o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
			{
				account.Cancel(new[] { order });
			}
			entryOrder = null;
			pendingRemoveLines = true; // ponytail: let OnBarUpdate handle removal on data thread
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

		#region ATM XML Template Parsing
		private void LoadAtmTemplateSettings(string templateName)
		{
			atmStopLoss = 0;
			atmTarget = 0;
			atmBETrigger = 0;
			atmSL1Trigger = 0;
			atmSL2Trigger = 0;
			atmQuantity = 0;

			if (string.IsNullOrEmpty(templateName)) return;

			try
			{
				string path = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy", templateName + ".xml");
				AtmTemplateData data = KatAtmXmlParser.ParseFile(path);

				atmStopLoss = data.StopLoss;
				atmTarget = data.Target;
				atmBETrigger = data.BETrigger;
				atmSL1Trigger = data.SL1Trigger;
				atmSL2Trigger = data.SL2Trigger;
				atmQuantity = data.Quantity;

				if (txtQuantity != null && atmQuantity > 0)
				{
					txtQuantity.Text = atmQuantity.ToString();
					cachedQuantity = atmQuantity;
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error parsing ATM XML: {0}", ex.Message));
			}
		}
		#endregion

		#region Chart Visuals & Line Drawing
		/// <summary>
		/// Executes Draw.Line calls on the data thread (called from OnBarUpdate).
		/// Reads pending state set by PlaceOrderInternal on the UI thread.
		/// </summary>
		private void DrawExpectedLines()
		{
			KatTradeCalculator.AtmLevels levels;
			double entryPx;
			int sl, tp, be, sl1, sl2;

			lock (priceLock)
			{
				levels = pendingLevels;
				entryPx = pendingEntryPrice;
				sl = pendingAtmStopLoss;
				tp = pendingAtmTarget;
				be = pendingAtmBETrigger;
				sl1 = pendingAtmSL1Trigger;
				sl2 = pendingAtmSL2Trigger;
			}

			// Entry price line (always drawn)
			Draw.Line(this, "KAT_ENTRY_LINE", false, 20, entryPx, -5, entryPx, Brushes.Gold, DashStyleHelper.Solid, 2);

			if (sl > 0)
				Draw.Line(this, "KAT_SL_LINE", false, 20, levels.SlPrice, -5, levels.SlPrice, Brushes.Red, DashStyleHelper.Dash, 2);
			if (tp > 0)
				Draw.Line(this, "KAT_TP_LINE", false, 20, levels.TpPrice, -5, levels.TpPrice, Brushes.Green, DashStyleHelper.Dash, 2);
			if (be > 0)
				Draw.Line(this, "KAT_BE_LINE", false, 20, levels.BePrice, -5, levels.BePrice, Brushes.DeepSkyBlue, DashStyleHelper.DashDot, 1);
			if (sl1 > 0)
				Draw.Line(this, "KAT_SL1_LINE", false, 20, levels.Sl1Price, -5, levels.Sl1Price, Brushes.Orange, DashStyleHelper.Dot, 1);
			if (sl2 > 0)
				Draw.Line(this, "KAT_SL2_LINE", false, 20, levels.Sl2Price, -5, levels.Sl2Price, Brushes.Magenta, DashStyleHelper.Dot, 1);

			isExpectedLinesDrawn = true;
			ForceRefresh();
		}

		private void RemoveExpectedLines()
		{
			RemoveDrawObject("KAT_ENTRY_LINE");
			RemoveDrawObject("KAT_SL_LINE");
			RemoveDrawObject("KAT_TP_LINE");
			RemoveDrawObject("KAT_BE_LINE");
			RemoveDrawObject("KAT_SL1_LINE");
			RemoveDrawObject("KAT_SL2_LINE");
			isExpectedLinesDrawn = false;
			ForceRefresh();
		}
		#endregion

		#region NinjaScript Properties
		[NinjaScriptProperty]
		[Display(Name="Show Control Panel", Order=0, GroupName="Parameters")]
		public bool IsPanelVisible { get; set; }

		[NinjaScriptProperty]
		[Range(1, 100)]
		[Display(Name="Default Quantity", Order=1, GroupName="Parameters")]
		public int DefaultQuantity { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Account Name", Order=2, GroupName="Parameters")]
		public string AccountName { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Default Buffer (Ticks)", Order=3, GroupName="Parameters")]
		public int DefaultBufferTicks { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Default ATM Template", Order=4, GroupName="Parameters")]
		public string DefaultAtmTemplate { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10000)]
		[Display(Name="Default Distance (Ticks)", Order=5, GroupName="Parameters")]
		public int DefaultDistanceTicks { get; set; }
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private KatTradeManager[] cacheKatTradeManager;
		public KatTradeManager KatTradeManager(bool isPanelVisible, int defaultQuantity, string accountName, int defaultBufferTicks, string defaultAtmTemplate, int defaultDistanceTicks)
		{
			return KatTradeManager(Input, isPanelVisible, defaultQuantity, accountName, defaultBufferTicks, defaultAtmTemplate, defaultDistanceTicks);
		}

		public KatTradeManager KatTradeManager(ISeries<double> input, bool isPanelVisible, int defaultQuantity, string accountName, int defaultBufferTicks, string defaultAtmTemplate, int defaultDistanceTicks)
		{
			if (cacheKatTradeManager != null)
				for (int idx = 0; idx < cacheKatTradeManager.Length; idx++)
					if (cacheKatTradeManager[idx] != null && cacheKatTradeManager[idx].IsPanelVisible == isPanelVisible && cacheKatTradeManager[idx].DefaultQuantity == defaultQuantity && cacheKatTradeManager[idx].AccountName == accountName && cacheKatTradeManager[idx].DefaultBufferTicks == defaultBufferTicks && cacheKatTradeManager[idx].DefaultAtmTemplate == defaultAtmTemplate && cacheKatTradeManager[idx].DefaultDistanceTicks == defaultDistanceTicks && cacheKatTradeManager[idx].EqualsInput(input))
						return cacheKatTradeManager[idx];
			return CacheIndicator<KatTradeManager>(new KatTradeManager(){ IsPanelVisible = isPanelVisible, DefaultQuantity = defaultQuantity, AccountName = accountName, DefaultBufferTicks = defaultBufferTicks, DefaultAtmTemplate = defaultAtmTemplate, DefaultDistanceTicks = defaultDistanceTicks }, input, ref cacheKatTradeManager);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.KatTradeManager KatTradeManager(bool isPanelVisible, int defaultQuantity, string accountName, int defaultBufferTicks, string defaultAtmTemplate, int defaultDistanceTicks)
		{
			return indicator.KatTradeManager(Input, isPanelVisible, defaultQuantity, accountName, defaultBufferTicks, defaultAtmTemplate, defaultDistanceTicks);
		}

		public Indicators.KatTradeManager KatTradeManager(ISeries<double> input , bool isPanelVisible, int defaultQuantity, string accountName, int defaultBufferTicks, string defaultAtmTemplate, int defaultDistanceTicks)
		{
			return indicator.KatTradeManager(input, isPanelVisible, defaultQuantity, accountName, defaultBufferTicks, defaultAtmTemplate, defaultDistanceTicks);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.KatTradeManager KatTradeManager(bool isPanelVisible, int defaultQuantity, string accountName, int defaultBufferTicks, string defaultAtmTemplate, int defaultDistanceTicks)
		{
			return indicator.KatTradeManager(Input, isPanelVisible, defaultQuantity, accountName, defaultBufferTicks, defaultAtmTemplate, defaultDistanceTicks);
		}

		public Indicators.KatTradeManager KatTradeManager(ISeries<double> input , bool isPanelVisible, int defaultQuantity, string accountName, int defaultBufferTicks, string defaultAtmTemplate, int defaultDistanceTicks)
		{
			return indicator.KatTradeManager(input, isPanelVisible, defaultQuantity, accountName, defaultBufferTicks, defaultAtmTemplate, defaultDistanceTicks);
		}
	}
}

#endregion
