/*
 * KatTradeManager.cs
 * Version: 0.44 (2026-07-25)
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

public enum KatTimeframe
{
	[Display(Name = "Chart TF")]
	ChartTF = 0,
	[Display(Name = "30s")]
	Sec30 = 1,
	[Display(Name = "1m")]
	Min1 = 2,
	[Display(Name = "2m")]
	Min2 = 3
}

public enum KatEmaTimeframe
{
	[Display(Name = "Chart TF")]
	ChartTF = 0,
	[Display(Name = "30s")]
	Sec30 = 1,
	[Display(Name = "1m")]
	Min1 = 2,
	[Display(Name = "2m")]
	Min2 = 3,
	[Display(Name = "3m")]
	Min3 = 4,
	[Display(Name = "5m")]
	Min5 = 5,
	[Display(Name = "15m")]
	Min15 = 6,
	[Display(Name = "30m")]
	Min30 = 7,
	[Display(Name = "60m")]
	Min60 = 8
}

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class KatTradeManager : Indicator
	{
		#region Metadata & Variables
		public const string VERSION = "0.44";
		public const string RELEASE_DATE = "2026-07-25";

		private volatile Account account;
		private Grid chartGrid;
		private Border panelBorder;
		private StackPanel mainPanel;
		private TextBox txtQuantity;
		private ComboBox atmSelector;
		private System.Windows.Threading.DispatcherTimer panelWatchdog;
		private bool isTerminated;

		// EMA indicators for multi-timeframe candle scanning
		private EMA[] ema34Series;
		private EMA[] ema89Series;

		// EMA series and series bar indices for EMA Place & EMA Angle filter validation
		private EMA[] emaPlaceFilterSeries;
		private int[] emaPlaceFilterBarIdx;
		private EMA[] emaAngleFilterSeries;
		private int[] emaAngleFilterBarIdx;

		// HUD toggle state for EMA Place & EMA Angle (default ON)
		private volatile bool cachedIsEmaPlace = true;
		private volatile bool cachedIsEmaAngle = true;

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

		// Partial Candle toggle state, Pullback %, & Renko chart detection
		private volatile bool cachedIsPartialCandle = false;
		private volatile int cachedPartialPercent = 30;
		private bool isRenkoChart = false;

		// Thread synchronization lock for bar price caching
		private readonly object priceLock = new object();
		private const int NUM_SERIES = 9; // chart + 30s + 1m + 2m + 3m + 5m + 15m + 30m + 60m
		private double[] cachedCurrentHigh  = new double[NUM_SERIES];
		private double[] cachedCurrentLow   = new double[NUM_SERIES];
		private double[] cachedCurrentOpen  = new double[NUM_SERIES];
		private double[] cachedCurrentClose = new double[NUM_SERIES];
		private double[] cachedPrevHigh     = new double[NUM_SERIES];
		private double[] cachedPrevLow      = new double[NUM_SERIES];
		private double[] cachedPrevOpen     = new double[NUM_SERIES];
		private double[] cachedPrevClose    = new double[NUM_SERIES];
		private double cachedTickSize;
		private double cachedCurrentPrice;
		#endregion

		#region Indicator Lifecycle
		private int GetBarsArraySeriesIndex(KatEmaTimeframe tf)
		{
			switch (tf)
			{
				case KatEmaTimeframe.Sec30: return 1;
				case KatEmaTimeframe.Min1:  return 2;
				case KatEmaTimeframe.Min2:  return 3;
				case KatEmaTimeframe.Min3:  return 4;
				case KatEmaTimeframe.Min5:  return 5;
				case KatEmaTimeframe.Min15: return 6;
				case KatEmaTimeframe.Min30: return 7;
				case KatEmaTimeframe.Min60: return 8;
				default: return 0; // Chart TF
			}
		}

		private bool IsAccountAllowed(string accName)
		{
			if (string.IsNullOrWhiteSpace(accName)) return false;
			if (string.IsNullOrWhiteSpace(AccountFilter)) return true;
			string[] tokens = AccountFilter.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToArray();
			if (tokens.Length == 0) return true;

			var excludes = tokens.Where(t => t.StartsWith("!") && t.Length > 1).Select(t => t.Substring(1).Trim()).ToList();
			var includes = tokens.Where(t => !t.StartsWith("!") && t.Length > 0).ToList();

			if (excludes.Any(ex => accName.IndexOf(ex, StringComparison.OrdinalIgnoreCase) >= 0))
				return false;

			if (includes.Count > 0)
				return includes.Any(inc => accName.IndexOf(inc, StringComparison.OrdinalIgnoreCase) >= 0);

			return true;
		}

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
				AccountFilter						= "";
				DefaultTimeframe                    = KatTimeframe.ChartTF;
				DefaultBufferTicks                  = 2;
				DefaultDistanceTicks                = 320; // Default 80 points = 320 ticks
				DefaultAtmTemplate                  = "Sim101_ATM";
				DefaultPartialCandlePercent         = 30;

				// EMA Place Filter Defaults
				EmaPlace1Enabled                    = true;
				EmaPlace1Period                     = 9;
				EmaPlace1Timeframe                  = KatEmaTimeframe.Min5;
				EmaPlace2Enabled                    = true;
				EmaPlace2Period                     = 34;
				EmaPlace2Timeframe                  = KatEmaTimeframe.Min5;
				EmaPlace3Enabled                    = true;
				EmaPlace3Period                     = 89;
				EmaPlace3Timeframe                  = KatEmaTimeframe.Min5;

				// EMA Angle Filter Defaults
				EmaAngle1Enabled                    = true;
				EmaAngle1Period                     = 9;
				EmaAngle1Timeframe                  = KatEmaTimeframe.Min5;
				EmaAngle1MinAngle                   = 35.0;
				EmaAngle2Enabled                    = true;
				EmaAngle2Period                     = 34;
				EmaAngle2Timeframe                  = KatEmaTimeframe.Min5;
				EmaAngle2MinAngle                   = 30.0;
				EmaAngle3Enabled                    = true;
				EmaAngle3Period                     = 89;
				EmaAngle3Timeframe                  = KatEmaTimeframe.Min5;
				EmaAngle3MinAngle                   = 15.0;

				// Hotkey Defaults
				HotkeyEnabled                       = true;
				HotkeyBuyEma34                      = Key.None;
				HotkeySellEma34                     = Key.None;
				HotkeyBuyEma89                      = Key.None;
				HotkeySellEma89                     = Key.None;
				HotkeyBuyPrev                       = Key.None;
				HotkeySellPrev                      = Key.None;
				HotkeyBuyCurr                       = Key.None;
				HotkeySellCurr                      = Key.None;
				HotkeyBuyDist                       = Key.None;
				HotkeySellDist                      = Key.None;
				HotkeyBuyMarket                     = Key.None;
				HotkeySellMarket                    = Key.None;
				HotkeyBE                            = Key.None;
				HotkeyRevert                        = Key.None;
				HotkeyClose                         = Key.None;
			}

			else if (State == State.Configure)
			{
				AddDataSeries(BarsPeriodType.Second, 30);  // idx 1
				AddDataSeries(BarsPeriodType.Minute, 1);   // idx 2
				AddDataSeries(BarsPeriodType.Minute, 2);   // idx 3
				AddDataSeries(BarsPeriodType.Minute, 3);   // idx 4
				AddDataSeries(BarsPeriodType.Minute, 5);   // idx 5
				AddDataSeries(BarsPeriodType.Minute, 15);  // idx 6
				AddDataSeries(BarsPeriodType.Minute, 30);  // idx 7
				AddDataSeries(BarsPeriodType.Minute, 60);  // idx 8
			}
			else if (State == State.DataLoaded)
			{
				isTerminated = false;
				cachedQuantity = DefaultQuantity;
				cachedTfIndex = (int)DefaultTimeframe;
				cachedTickSize = TickSize;
				cachedBufferTicks = DefaultBufferTicks;
				cachedDistanceTicks = DefaultDistanceTicks;
				cachedAtmTemplate = DefaultAtmTemplate;
				isRenkoChart = BarsPeriod.BarsPeriodType == BarsPeriodType.Renko
				               || (BarsPeriod.BarsPeriodTypeName != null && BarsPeriod.BarsPeriodTypeName.IndexOf("Renko", StringComparison.OrdinalIgnoreCase) >= 0)
				               || BarsPeriod.BarsPeriodType.ToString().IndexOf("Renko", StringComparison.OrdinalIgnoreCase) >= 0;

				ema34Series = new EMA[NUM_SERIES];
				ema89Series = new EMA[NUM_SERIES];
				for (int i = 0; i < NUM_SERIES; i++)
				{
					ema34Series[i] = EMA(BarsArray[i], 34);
					ema89Series[i] = EMA(BarsArray[i], 89);
				}

				// Initialize per-EMA series and series bar indices for EMA Place filter
				emaPlaceFilterSeries = new EMA[3];
				emaPlaceFilterBarIdx = new int[3];

				if (EmaPlace1Enabled)
				{
					int idx = GetBarsArraySeriesIndex(EmaPlace1Timeframe);
					emaPlaceFilterSeries[0] = EMA(BarsArray[idx], EmaPlace1Period);
					emaPlaceFilterBarIdx[0] = idx;
				}
				if (EmaPlace2Enabled)
				{
					int idx = GetBarsArraySeriesIndex(EmaPlace2Timeframe);
					emaPlaceFilterSeries[1] = EMA(BarsArray[idx], EmaPlace2Period);
					emaPlaceFilterBarIdx[1] = idx;
				}
				if (EmaPlace3Enabled)
				{
					int idx = GetBarsArraySeriesIndex(EmaPlace3Timeframe);
					emaPlaceFilterSeries[2] = EMA(BarsArray[idx], EmaPlace3Period);
					emaPlaceFilterBarIdx[2] = idx;
				}

				// Initialize per-EMA series and series bar indices for EMA Angle filter
				emaAngleFilterSeries = new EMA[3];
				emaAngleFilterBarIdx = new int[3];

				if (EmaAngle1Enabled)
				{
					int idx = GetBarsArraySeriesIndex(EmaAngle1Timeframe);
					emaAngleFilterSeries[0] = EMA(BarsArray[idx], EmaAngle1Period);
					emaAngleFilterBarIdx[0] = idx;
				}
				if (EmaAngle2Enabled)
				{
					int idx = GetBarsArraySeriesIndex(EmaAngle2Timeframe);
					emaAngleFilterSeries[1] = EMA(BarsArray[idx], EmaAngle2Period);
					emaAngleFilterBarIdx[1] = idx;
				}
				if (EmaAngle3Enabled)
				{
					int idx = GetBarsArraySeriesIndex(EmaAngle3Timeframe);
					emaAngleFilterSeries[2] = EMA(BarsArray[idx], EmaAngle3Period);
					emaAngleFilterBarIdx[2] = idx;
				}

				Print(string.Format("[KatTradeManager] v{0} loaded — cached mode active (Renko: {1})", VERSION, isRenkoChart));




				if (Account.All != null && Account.All.Count > 0)
				{
					Print("[KatTradeManager] Available Accounts:");
					foreach (var acc in Account.All)
					{
						Print(string.Format("  - {0} ({1})", acc.Name, acc.Connection != null ? "Connected" : "Disconnected"));
					}

					var allowedAccs = Account.All.Where(a => IsAccountAllowed(a.Name)).ToList();
					if (allowedAccs.Count == 0) allowedAccs = Account.All.ToList();

					account = allowedAccs.FirstOrDefault(a => a.Name.Equals(AccountName, StringComparison.OrdinalIgnoreCase))
					          ?? allowedAccs.FirstOrDefault(a => a.Name == "Sim101")
					          ?? allowedAccs.FirstOrDefault(a => a.Name == "Sim301")
					          ?? allowedAccs.FirstOrDefault(a => a.Connection != null)
					          ?? allowedAccs.FirstOrDefault();

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
						cachedCurrentHigh[bip]  = Highs[bip][0];
						cachedCurrentLow[bip]   = Lows[bip][0];
						cachedCurrentOpen[bip]  = Opens[bip][0];
						cachedCurrentClose[bip] = Closes[bip][0];
						if (bip == 0)
						{
							cachedCurrentPrice = Closes[0][0];
						}
						if (CurrentBars[bip] >= 1)
						{
							cachedPrevHigh[bip]  = Highs[bip][1];
							cachedPrevLow[bip]   = Lows[bip][1];
							cachedPrevOpen[bip]  = Opens[bip][1];
							cachedPrevClose[bip] = Closes[bip][1];
						}
					}
				}

				if (bip != 0 || account == null || Instrument == null) return;

				// Process pending remove request (from CancelAllOrders on UI thread)
				if (pendingRemoveLines)
				{
					pendingRemoveLines = false;
					RemoveExpectedLines();
				}

				// Process pending draw request (from PlaceOrderInternal on UI thread)
				if (pendingDrawRequest)
				{
					pendingDrawRequest = false;
					DrawExpectedLines();
				}

				// Auto-remove lines only on terminal states, not on transient states
				if (entryOrder != null)
				{
					var state = entryOrder.OrderState;
					if (state == OrderState.Filled || state == OrderState.Cancelled || state == OrderState.Rejected)
					{
						if (isExpectedLinesDrawn)
							RemoveExpectedLines();
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
		private static KatOrderAction ToKatAction(OrderAction action) => action == OrderAction.Buy ? KatOrderAction.Buy : KatOrderAction.Sell;
		private static OrderType ToNtOrderType(KatOrderType type) => type == KatOrderType.StopMarket ? OrderType.StopMarket : OrderType.Limit;

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
				KatOrderAction katAction = ToKatAction(action);

				lock (priceLock)
				{
					double high  = isCurrentCandle ? cachedCurrentHigh[barIdx]  : cachedPrevHigh[barIdx];
					double low   = isCurrentCandle ? cachedCurrentLow[barIdx]   : cachedPrevLow[barIdx];
					double open  = isCurrentCandle ? cachedCurrentOpen[barIdx]  : cachedPrevOpen[barIdx];
					double close = isCurrentCandle ? cachedCurrentClose[barIdx] : cachedPrevClose[barIdx];

					basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, cachedIsPartialCandle, cachedPartialPercent, high, low, open, close, barIdx == 0 && isRenkoChart, cachedTickSize);
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}

				if (basePrice <= 0) return;

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

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
							currentPx = cachedCurrentClose[barIdx] > 0 ? cachedCurrentClose[barIdx] : 0;
					}
				}

				if (currentPx <= 0) return;

				int distTicks = cachedDistanceTicks > 0 ? cachedDistanceTicks : DefaultDistanceTicks;
				KatOrderAction katAction = ToKatAction(action);
				double triggerPrice = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(katAction, currentPx, distTicks, cachedTickSize);

				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing fixed distance order");
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing fixed distance order: {0}", ex.ToString()));
			}
		}

		private void PlaceEmaOrder(OrderAction action, int emaPeriod)
		{
			if (account == null || Instrument == null) return;

			try
			{
				int barIdx = GetBarsInProgressIndex();
				if (barIdx < 0 || barIdx >= NUM_SERIES) return;

				double basePrice = 0;
				double currentPx = 0;
				KatOrderAction katAction = ToKatAction(action);
				int foundBarsAgo = -1;

				lock (priceLock)
				{
					EMA targetEma = (emaPeriod == 34) ? (ema34Series != null && barIdx < ema34Series.Length ? ema34Series[barIdx] : null)
					                                  : (ema89Series != null && barIdx < ema89Series.Length ? ema89Series[barIdx] : null);

					int maxBars = CurrentBars[barIdx];
					if (targetEma != null)
					{
						for (int barsAgo = 0; barsAgo <= maxBars && barsAgo < 500; barsAgo++)
						{
							double h = Highs[barIdx][barsAgo];
							double l = Lows[barIdx][barsAgo];
							double emaVal = targetEma[barsAgo];

							if (KatTradeCalculator.IsEmaTouchBar(h, l, emaVal))
							{
								foundBarsAgo = barsAgo;
								double open  = Opens[barIdx][barsAgo];
								double close = Closes[barIdx][barsAgo];
								basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, cachedIsPartialCandle, cachedPartialPercent, h, l, open, close, barIdx == 0 && isRenkoChart, cachedTickSize);
								break;
							}
						}
					}
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}


				if (foundBarsAgo < 0 || basePrice <= 0)
				{
					Print(string.Format("[KatTradeManager] No candle found touching/crossing EMA {0} on TF index {1}", emaPeriod, barIdx));
					return;
				}

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, string.Format("placing EMA {0} order", emaPeriod));
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing EMA {0} order: {1}", emaPeriod, ex.ToString()));
			}
		}


		private void PlaceOrderInternal(OrderAction action, double triggerPrice, OrderType orderType, double limitPrice, double stopPrice, string errorContext)
		{
			if (account == null || Instrument == null) return;

			try
			{
				KatOrderAction katAction = ToKatAction(action);
				double checkPrice = (orderType == OrderType.Market) ? (cachedCurrentPrice > 0 ? cachedCurrentPrice : triggerPrice) : triggerPrice;

				lock (priceLock)
				{
					// Validation 1: EMA Place Check
					if (cachedIsEmaPlace)
					{
						double[] emaVals = new double[3];
						int valCount = 0;
						if (EmaPlace1Enabled && emaPlaceFilterSeries != null && emaPlaceFilterSeries[0] != null && CurrentBars[emaPlaceFilterBarIdx[0]] >= 0)
							emaVals[valCount++] = emaPlaceFilterSeries[0][0];
						if (EmaPlace2Enabled && emaPlaceFilterSeries != null && emaPlaceFilterSeries[1] != null && CurrentBars[emaPlaceFilterBarIdx[1]] >= 0)
							emaVals[valCount++] = emaPlaceFilterSeries[1][0];
						if (EmaPlace3Enabled && emaPlaceFilterSeries != null && emaPlaceFilterSeries[2] != null && CurrentBars[emaPlaceFilterBarIdx[2]] >= 0)
							emaVals[valCount++] = emaPlaceFilterSeries[2][0];

						if (valCount > 0 && !KatTradeCalculator.ValidateEmaPlace(katAction, checkPrice, emaVals.Take(valCount).ToArray(), out string errPlace))
						{
							Print(string.Format("[KatTradeManager] Order REJECTED by EMA Place: {0}", errPlace));
							return;
						}
					}

					// Validation 2: EMA Angle Check
					if (cachedIsEmaAngle)
					{
						double[] currEmas = new double[3];
						double[] prevEmas = new double[3];
						double[] minAngles = new double[3];
						int angleCount = 0;

						if (EmaAngle1Enabled && emaAngleFilterSeries != null && emaAngleFilterSeries[0] != null && CurrentBars[emaAngleFilterBarIdx[0]] >= 1)
						{
							currEmas[angleCount] = emaAngleFilterSeries[0][0];
							prevEmas[angleCount] = emaAngleFilterSeries[0][1];
							minAngles[angleCount++] = EmaAngle1MinAngle;
						}
						if (EmaAngle2Enabled && emaAngleFilterSeries != null && emaAngleFilterSeries[1] != null && CurrentBars[emaAngleFilterBarIdx[1]] >= 1)
						{
							currEmas[angleCount] = emaAngleFilterSeries[1][0];
							prevEmas[angleCount] = emaAngleFilterSeries[1][1];
							minAngles[angleCount++] = EmaAngle2MinAngle;
						}
						if (EmaAngle3Enabled && emaAngleFilterSeries != null && emaAngleFilterSeries[2] != null && CurrentBars[emaAngleFilterBarIdx[2]] >= 1)
						{
							currEmas[angleCount] = emaAngleFilterSeries[2][0];
							prevEmas[angleCount] = emaAngleFilterSeries[2][1];
							minAngles[angleCount++] = EmaAngle3MinAngle;
						}

						if (angleCount > 0 && !KatTradeCalculator.ValidateEmaAngle(katAction, currEmas.Take(angleCount).ToArray(), prevEmas.Take(angleCount).ToArray(), minAngles.Take(angleCount).ToArray(), cachedTickSize, out string errAngle))
						{
							Print(string.Format("[KatTradeManager] Order REJECTED by EMA Angle: {0}", errAngle));
							return;
						}
					}
				}


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
							katAction, triggerPrice, atmStopLoss, atmTarget, atmBETrigger, atmSL1Trigger, atmSL2Trigger, cachedTickSize);

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
			pendingRemoveLines = true; // ponytail: single removal path — OnBarUpdate (data thread) executes it
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

		private void PlaceMarketOrder(OrderAction action)
		{
			if (account == null || Instrument == null) return;
			try
			{
				int qty = cachedQuantity > 0 ? cachedQuantity : DefaultQuantity;
				string entryName = action == OrderAction.Buy ? "MarketBuy" : "MarketSell";

				entryOrder = account.CreateOrder(Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, qty, 0, 0, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
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
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing market order: {0}", ex.ToString()));
			}
		}

		private void SetBreakeven()
		{
			if (account == null || Instrument == null) return;
			try
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] BE: No active position to set Breakeven.");
					return;
				}

				double tickSize = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
				int bufferTicks = cachedBufferTicks >= 0 ? cachedBufferTicks : DefaultBufferTicks;
				KatOrderAction katAction = pos.MarketPosition == MarketPosition.Long ? KatOrderAction.Buy : KatOrderAction.Sell;

				double bePrice = KatTradeCalculator.CalculateBreakevenPrice(katAction, pos.AveragePrice, bufferTicks, tickSize);

				var workingStops = account.Orders.Where(o => o.Instrument == Instrument &&
					(o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();

				if (workingStops.Count > 0)
				{
					foreach (Order stopOrder in workingStops)
					{
						stopOrder.StopPrice = bePrice;
						account.Change(new[] { stopOrder });
					}
					Print(string.Format("[KatTradeManager] Moved {0} Stop Loss order(s) to Breakeven @ {1} (Buffer: {2} ticks)", workingStops.Count, bePrice, bufferTicks));
				}
				else
				{
					OrderAction slAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
					Order slOrder = account.CreateOrder(Instrument, slAction, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, bePrice, "", "KAT_SL_BE", NinjaTrader.Core.Globals.MaxDate, null);
					account.Submit(new[] { slOrder });
					Print(string.Format("[KatTradeManager] Submitted Breakeven Stop Loss @ {0} (Buffer: {1} ticks)", bePrice, bufferTicks));
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error setting Breakeven: {0}", ex.ToString()));
			}
		}

		private void RevertPosition()
		{
			if (account == null || Instrument == null) return;
			try
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] Revert: No active position to revert.");
					return;
				}

				OrderAction oppositeAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.Buy;
				ClosePosition();
				PlaceMarketOrder(oppositeAction);
				Print(string.Format("[KatTradeManager] Reverted position to {0}", oppositeAction));
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error reverting position: {0}", ex.ToString()));
			}
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
			RemoveExpectedLines(); // ponytail: clear previous line objects first to prevent lingering tags

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

			int startBar = KatTradeCalculator.GetLineStartBar(CurrentBar, 20);

			// Entry price line (always drawn)
			Draw.Line(this, KatTradeCalculator.LineTags[0], false, startBar, entryPx, -5, entryPx, Brushes.Gold, DashStyleHelper.Solid, 2);

			if (sl > 0)
				Draw.Line(this, KatTradeCalculator.LineTags[1], false, startBar, levels.SlPrice, -5, levels.SlPrice, Brushes.Red, DashStyleHelper.Dash, 2);
			if (tp > 0)
				Draw.Line(this, KatTradeCalculator.LineTags[2], false, startBar, levels.TpPrice, -5, levels.TpPrice, Brushes.Green, DashStyleHelper.Dash, 2);
			if (be > 0)
				Draw.Line(this, KatTradeCalculator.LineTags[3], false, startBar, levels.BePrice, -5, levels.BePrice, Brushes.DeepSkyBlue, DashStyleHelper.DashDot, 1);
			if (sl1 > 0)
				Draw.Line(this, KatTradeCalculator.LineTags[4], false, startBar, levels.Sl1Price, -5, levels.Sl1Price, Brushes.Orange, DashStyleHelper.Dot, 1);
			if (sl2 > 0)
				Draw.Line(this, KatTradeCalculator.LineTags[5], false, startBar, levels.Sl2Price, -5, levels.Sl2Price, Brushes.Magenta, DashStyleHelper.Dot, 1);

			isExpectedLinesDrawn = true;
			ForceRefresh();
		}

		private void RemoveExpectedLines()
		{
			foreach (string tag in KatTradeCalculator.LineTags)
				RemoveDrawObject(tag);
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
		[Display(Name="Account Filter (Keywords)", Order=3, GroupName="Parameters", Description="Filter accounts in HUD by comma-separated keywords (e.g. '79424, Sim101' or '!BX, !LTE'). Empty = show all.")]
		public string AccountFilter { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Timeframe", Order=3, GroupName="Parameters")]
		public KatTimeframe DefaultTimeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name="Default Buffer (Ticks)", Order=4, GroupName="Parameters")]
		public int DefaultBufferTicks { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Default ATM Template", Order=5, GroupName="Parameters")]
		public string DefaultAtmTemplate { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10000)]
		[Display(Name="Default Distance (Ticks)", Order=6, GroupName="Parameters")]
		public int DefaultDistanceTicks { get; set; }

		[NinjaScriptProperty]
		[Range(1, 99)]
		[Display(Name="Partial Candle Pullback (%)", Order=7, GroupName="Parameters")]
		public int DefaultPartialCandlePercent { get; set; }

		#region EMA Place Filter Properties
		[NinjaScriptProperty]
		[Display(Name="1st EMA Place Enabled", Order=10, GroupName="EMA Place Filter")]
		public bool EmaPlace1Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="1st EMA Place Period", Order=11, GroupName="EMA Place Filter")]
		public int EmaPlace1Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="1st EMA Place Timeframe", Order=12, GroupName="EMA Place Filter")]
		public KatEmaTimeframe EmaPlace1Timeframe { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Place Enabled", Order=13, GroupName="EMA Place Filter")]
		public bool EmaPlace2Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="2nd EMA Place Period", Order=14, GroupName="EMA Place Filter")]
		public int EmaPlace2Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Place Timeframe", Order=15, GroupName="EMA Place Filter")]
		public KatEmaTimeframe EmaPlace2Timeframe { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Place Enabled", Order=16, GroupName="EMA Place Filter")]
		public bool EmaPlace3Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="3rd EMA Place Period", Order=17, GroupName="EMA Place Filter")]
		public int EmaPlace3Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Place Timeframe", Order=18, GroupName="EMA Place Filter")]
		public KatEmaTimeframe EmaPlace3Timeframe { get; set; }
		#endregion

		#region EMA Angle Filter Properties
		[NinjaScriptProperty]
		[Display(Name="1st EMA Angle Enabled", Order=20, GroupName="EMA Angle Filter")]
		public bool EmaAngle1Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="1st EMA Angle Period", Order=21, GroupName="EMA Angle Filter")]
		public int EmaAngle1Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="1st EMA Angle Timeframe", Order=22, GroupName="EMA Angle Filter")]
		public KatEmaTimeframe EmaAngle1Timeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 90.0)]
		[Display(Name="1st EMA Angle Min Angle (°)", Order=23, GroupName="EMA Angle Filter")]
		public double EmaAngle1MinAngle { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Angle Enabled", Order=24, GroupName="EMA Angle Filter")]
		public bool EmaAngle2Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="2nd EMA Angle Period", Order=25, GroupName="EMA Angle Filter")]
		public int EmaAngle2Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="2nd EMA Angle Timeframe", Order=26, GroupName="EMA Angle Filter")]
		public KatEmaTimeframe EmaAngle2Timeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 90.0)]
		[Display(Name="2nd EMA Angle Min Angle (°)", Order=27, GroupName="EMA Angle Filter")]
		public double EmaAngle2MinAngle { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Angle Enabled", Order=28, GroupName="EMA Angle Filter")]
		public bool EmaAngle3Enabled { get; set; }

		[NinjaScriptProperty]
		[Range(1, 500)]
		[Display(Name="3rd EMA Angle Period", Order=29, GroupName="EMA Angle Filter")]
		public int EmaAngle3Period { get; set; }

		[NinjaScriptProperty]
		[Display(Name="3rd EMA Angle Timeframe", Order=30, GroupName="EMA Angle Filter")]
		public KatEmaTimeframe EmaAngle3Timeframe { get; set; }

		[NinjaScriptProperty]
		[Range(0.0, 90.0)]
		[Display(Name="3rd EMA Angle Min Angle (°)", Order=31, GroupName="EMA Angle Filter")]
		public double EmaAngle3MinAngle { get; set; }
		#endregion

		#region Hotkey Properties
		[NinjaScriptProperty]
		[Display(Name="Enable Hotkeys", Order=40, GroupName="Hotkeys")]
		public bool HotkeyEnabled { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy EMA 34", Order=41, GroupName="Hotkeys")]
		public Key HotkeyBuyEma34 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell EMA 34", Order=42, GroupName="Hotkeys")]
		public Key HotkeySellEma34 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy EMA 89", Order=43, GroupName="Hotkeys")]
		public Key HotkeyBuyEma89 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell EMA 89", Order=44, GroupName="Hotkeys")]
		public Key HotkeySellEma89 { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy Previous Candle", Order=45, GroupName="Hotkeys")]
		public Key HotkeyBuyPrev { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell Previous Candle", Order=46, GroupName="Hotkeys")]
		public Key HotkeySellPrev { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy Current Candle", Order=47, GroupName="Hotkeys")]
		public Key HotkeyBuyCurr { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell Current Candle", Order=48, GroupName="Hotkeys")]
		public Key HotkeySellCurr { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy +Distance", Order=49, GroupName="Hotkeys")]
		public Key HotkeyBuyDist { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell -Distance", Order=50, GroupName="Hotkeys")]
		public Key HotkeySellDist { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Buy Market", Order=51, GroupName="Hotkeys")]
		public Key HotkeyBuyMarket { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Sell Market", Order=52, GroupName="Hotkeys")]
		public Key HotkeySellMarket { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Set Breakeven (BE)", Order=53, GroupName="Hotkeys")]
		public Key HotkeyBE { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Revert Position", Order=54, GroupName="Hotkeys")]
		public Key HotkeyRevert { get; set; }

		[NinjaScriptProperty]
		[Display(Name="Close / Flatten", Order=55, GroupName="Hotkeys")]
		public Key HotkeyClose { get; set; }
		#endregion
		#endregion
	}
}



