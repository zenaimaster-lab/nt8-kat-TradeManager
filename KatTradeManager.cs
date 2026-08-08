/*
 * KatTradeManager.cs
 * Version: 1.79 (2026-08-08)
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

public enum KatHudLocation
{
	[Display(Name = "Chart Trader")]
	ChartTrader = 0,
	[Display(Name = "In Chart")]
	InChart = 1
}

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager : Indicator
	{
		#region Metadata & Variables
		public const string VERSION = "1.79";
		public const string RELEASE_DATE = "2026-08-08";

		private volatile Account account;
		private Account subscribedAccount;
		private Grid chartGrid;
		private Border panelBorder;
		private StackPanel mainPanel;
		private ComboBox atmSelector;
		private System.Windows.Threading.DispatcherTimer panelWatchdog;
		private volatile bool isTerminated;

		// Daily Risk Control cached states & fields (default ON)
		private volatile bool cachedIsDailyMaxDD = true;
		private volatile bool cachedIsDailyMaxProfit = true;
		private double cachedDailyMaxDD = 500.0;
		private double cachedDailyMaxProfit = 1000.0;
		private DateTime lastSessionStartUtc = DateTime.MinValue;
		private double sessionStartRealizedPnL = 0.0;
		private bool isSessionStartCaptured = false;
		private int dailyRiskFlattened; // 0 = flat, 1 = flattened — Interlocked guard (evaluated from 2 threads)


		// EMA indicators for multi-timeframe candle scanning
		private EMA[] ema34Series;
		private EMA[] ema89Series;

		// EMA series and series bar indices for EMA Place filter validation
		private EMA[] emaPlaceFilterSeries;
		private int[] emaPlaceFilterBarIdx;
		private readonly double[] cachedEmaPlaceValues = new double[3];

		// HUD toggle state for EMA Protect (default ON)
		private volatile bool cachedIsEmaPlace = true;

		// Pending stop order mode (default OFF = StopMarket)
		private volatile bool cachedIsStopLimit = false;

		// Discipline Protects cached toggles (default ON)
		private volatile bool cachedSizingProtect = true;
		private volatile bool cachedSlPullProtect = true;
		private volatile bool cachedLossDcaProtect = true;
		private volatile bool cachedTpEarlyProtect = true;
		private volatile bool cachedLossTimesProtect = true;
		private volatile bool cachedTimingProtect = true;
		private volatile int cachedLossTimesMaxLosses = 3;
		private volatile int cachedLossTimesLockMinutes = 30;
		private volatile bool cachedTw1Enabled = true;
		private volatile int cachedTw1StartHour = 2;
		private volatile int cachedTw1StartMinute = 0;
		private volatile int cachedTw1EndHour = 15;
		private volatile int cachedTw1EndMinute = 0;
		private volatile bool cachedTw2Enabled = false;
		private volatile int cachedTw2StartHour = 12;
		private volatile int cachedTw2StartMinute = 0;
		private volatile int cachedTw2EndHour = 13;
		private volatile int cachedTw2EndMinute = 0;
		private volatile bool cachedTw3Enabled = false;
		private volatile int cachedTw3StartHour = 0;
		private volatile int cachedTw3StartMinute = 0;
		private volatile int cachedTw3EndHour = 0;
		private volatile int cachedTw3EndMinute = 0;

		// MERGE scale-ins use plain entry orders, then resize first ATM bracket after fills.
		private sealed class AtmScaleInState
		{
			public Order Order;
			public int AppliedFilled;
		}

		private readonly object atmScaleInLock = new object();
		private readonly List<AtmScaleInState> atmScaleInStates = new List<AtmScaleInState>();
		private Order atmMergeStopAnchor;
		private Order atmMergeTargetAnchor;
		private int atmMergeStopQuantity;
		private int atmMergeTargetQuantity;
		private MarketPosition atmMergePosition = MarketPosition.Flat;
		private int atmMergeScheduled;

		// Thread-safe cached values from UI controls (synced by watchdog on UI thread)
		private volatile int cachedTfIndex;
		private volatile int cachedBufferTicks;
		private volatile string cachedAtmTemplate = "";
		private volatile int cachedHudLeftInset = 10;
		private volatile bool cachedHudDragEnabled = true;

		private volatile Order entryOrder = null;
		private volatile Order pendingDrawOrder = null;

		// Parsed ATM parameters for line drawing
		private int atmStopLoss = 0;
		private int atmTarget = 0;
		private int atmBETrigger = 0;
		private int atmSL1Trigger = 0;
		private int atmSL2Trigger = 0;
		private volatile int atmQuantity = 0;

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

		// Renko chart detection
		private bool isRenkoChart = false;

		// Thread synchronization lock for bar price caching
		private readonly object priceLock = new object();
		private const int NUM_SERIES = 9; // chart + 30s + 1m + 2m + 3m + 5m + 15m + 30m + 60m
		private double[] cachedCurrentHigh  = new double[NUM_SERIES];
		private double[] cachedCurrentLow   = new double[NUM_SERIES];
		private double[] cachedCurrentClose = new double[NUM_SERIES];
		private double[] cachedPrevHigh     = new double[NUM_SERIES];
		private double[] cachedPrevLow      = new double[NUM_SERIES];
		private double cachedTickSize;
		private double cachedCurrentPrice;
		private readonly double[] cachedSwingHighs = new double[501];
		private readonly double[] cachedSwingLows = new double[501];
		private volatile int cachedSwingBars = -1;

		// EMA touch snapshots. WPF button handlers read these under priceLock; series access stays on data thread.
		private readonly int[] ema34TouchBarsAgo = new int[NUM_SERIES];
		private readonly double[] ema34TouchHigh = new double[NUM_SERIES];
		private readonly double[] ema34TouchLow = new double[NUM_SERIES];
		private readonly int[] ema89TouchBarsAgo = new int[NUM_SERIES];
		private readonly double[] ema89TouchHigh = new double[NUM_SERIES];
		private readonly double[] ema89TouchLow = new double[NUM_SERIES];

		// Bar-time + bar-list snapshots: UI-thread handlers must never touch Times/Highs directly
		// (NT8 throws ArgumentOutOfRangeException off the data thread — v0.11 lesson, regressed in v1.00-v1.03).
		private readonly DateTime[] cachedCurrentBarTime = new DateTime[NUM_SERIES];
		private readonly DateTime[] cachedPrevBarTime = new DateTime[NUM_SERIES];
		private readonly DateTime[] ema34TouchTime = new DateTime[NUM_SERIES];
		private readonly DateTime[] ema89TouchTime = new DateTime[NUM_SERIES];
		private readonly List<EmaTouchBarInfo>[] ema34TouchLists = new List<EmaTouchBarInfo>[NUM_SERIES];
		private readonly List<EmaTouchBarInfo>[] ema89TouchLists = new List<EmaTouchBarInfo>[NUM_SERIES];
		private readonly List<CandleBarInfo>[] candleBarLists = new List<CandleBarInfo>[NUM_SERIES];
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

		private void UpdateEmaFilterCache(int bip)
		{
			lock (priceLock)
			{
				for (int i = 0; i < 3; i++)
				{
					if (emaPlaceFilterBarIdx[i] == bip && emaPlaceFilterSeries[i] != null && CurrentBars[bip] >= 0)
						cachedEmaPlaceValues[i] = emaPlaceFilterSeries[i][0];
				}
			}
		}

		private bool IsAccountAllowed(string accName)
		{
			return KatTradeCalculator.IsAccountAllowed(accName, AccountFilter);
		}

		// ponytail: single place for tickSize fallback — delegates to pure KatTradeCalculator.ResolveTickSize for testability
		private double GetEffectiveTickSize(double fallback = 0.25)
		{
			double instrumentTick = 0;
			try { if (Instrument != null && Instrument.MasterInstrument != null) instrumentTick = Instrument.MasterInstrument.TickSize; } catch {}
			return KatTradeCalculator.ResolveTickSize(cachedTickSize, instrumentTick, fallback);
		}

		// Shared account-selection chain — used in DataLoaded and in the watchdog auto-recovery.
		// Returns null only when Account.All is empty (accounts not connected yet).
		private Account SelectAccount()
		{
			if (Account.All == null || Account.All.Count == 0) return null;
			var allowed = Account.All.Where(a => IsAccountAllowed(a.Name)).ToList();
			if (allowed.Count == 0) allowed = Account.All.ToList();
			return allowed.FirstOrDefault(a => a.Name.Equals(AccountName, StringComparison.OrdinalIgnoreCase))
			       ?? allowed.FirstOrDefault(a => a.Name == "Sim101")
			       ?? allowed.FirstOrDefault(a => a.Name == "Sim301")
			       ?? allowed.FirstOrDefault(a => a.Connection != null)
			       ?? allowed.FirstOrDefault();
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
				PanelLocation							= KatHudLocation.InChart;
				HudLeftInset                            = 10;
				HudDragEnabled                         = true;
				QuickSetFontSize                       = 8;
				QuickSetLabelColor                     = new SolidColorBrush(Color.FromRgb(255, 255, 255));
				QuickSetLabelOpacityPercent            = 50;
				DefaultQuantity						= 1;
				AccountName							= "Sim101";
				AccountFilter						= "";
				DefaultTimeframe                    = KatTimeframe.ChartTF;
				DefaultBufferTicks                  = 2;
				DefaultAtmTemplate                  = "Sim101_ATM";

				// Daily Risk Control Defaults
				DailyMaxDDEnabled                   = true;
				DailyMaxDD                          = 500.0;
				DailyMaxProfitEnabled               = true;
				DailyMaxProfit                      = 1000.0;

				// Daily Risk Quick Set Defaults
				DailyRiskSet1Name                   = "1";
				DailyRiskSet1MaxDD                  = 200.0;
				DailyRiskSet1MaxProfit              = 500.0;
				DailyRiskSet2Name                   = "2";
				DailyRiskSet2MaxDD                  = 100.0;
				DailyRiskSet2MaxProfit              = 300.0;
				DailyRiskSet3Name                   = "3";
				DailyRiskSet3MaxDD                  = 500.0;
				DailyRiskSet3MaxProfit              = 1000.0;
				DailyRiskSet4Name                   = "4";
				DailyRiskSet4MaxDD                  = 1000.0;
				DailyRiskSet4MaxProfit              = 2000.0;
				DailyRiskSet5Name                   = "5";
				DailyRiskSet5MaxDD                  = 1500.0;
				DailyRiskSet5MaxProfit              = 3000.0;
				DailyRiskSet6Name                   = "6";
				DailyRiskSet6MaxDD                  = 2000.0;
				DailyRiskSet6MaxProfit              = 5000.0;

				// Discipline Protects Defaults (all ON)
				SizingProtectEnabled                = true;
				SlPullProtectEnabled                = true;
				LossDcaProtectEnabled               = true;
				TpEarlyProtectEnabled               = true;
				LossTimesProtectEnabled             = true;
				TimingWindowsProtectEnabled         = true;
				LossTimesMaxLosses                  = 3;
				LossTimesLockMinutes                = 30;

				// Trading Windows Defaults (NY time)
				TradingWindow1Enabled               = true;
				TradingWindow1StartHour             = 2;
				TradingWindow1StartMinute           = 0;
				TradingWindow1EndHour               = 15;
				TradingWindow1EndMinute             = 0;
				TradingWindow2Enabled               = false;
				TradingWindow2StartHour             = 12;
				TradingWindow2StartMinute           = 0;
				TradingWindow2EndHour               = 13;
				TradingWindow2EndMinute             = 0;
				TradingWindow3Enabled               = false;
				TradingWindow3StartHour             = 0;
				TradingWindow3StartMinute           = 0;
				TradingWindow3EndHour               = 0;
				TradingWindow3EndMinute             = 0;

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

				// Trading Profile Defaults — 6 presets, each 2 rows share row colors; values mirror global defaults
				TradingProfile1Name                 = "P1";
				TradingProfile1Account              = "Sim101";
				TradingProfile1Atm                  = "";
				TradingProfile1Quantity             = 1;
				TradingProfile1Timeframe            = KatTimeframe.ChartTF;
				TradingProfile1BufferTicks          = 2;
				TradingProfile1StopLimitEnabled     = false;
				TradingProfile1EmaProtectEnabled    = true;
				TradingProfile1DailyMaxDDEnabled    = true;
				TradingProfile1DailyMaxDD           = 500.0;
				TradingProfile1DailyMaxProfitEnabled = true;
				TradingProfile1DailyMaxProfit       = 1000.0;
				TradingProfile1SizingProtect        = true;
				TradingProfile1SlPullProtect        = true;
				TradingProfile1LossDcaProtect       = true;
				TradingProfile1TpEarlyProtect       = true;
				TradingProfile1LossTimesProtect     = true;
				TradingProfile1TimingProtect        = true;
				TradingProfile1LossTimesMaxLosses   = 3;
				TradingProfile1LossTimesLockMinutes = 30;

				TradingProfile2Name                 = "P2";
				TradingProfile2Account              = "Sim101";
				TradingProfile2Atm                  = "";
				TradingProfile2Quantity             = 1;
				TradingProfile2Timeframe            = KatTimeframe.ChartTF;
				TradingProfile2BufferTicks          = 2;
				TradingProfile2StopLimitEnabled     = false;
				TradingProfile2EmaProtectEnabled    = true;
				TradingProfile2DailyMaxDDEnabled    = true;
				TradingProfile2DailyMaxDD           = 500.0;
				TradingProfile2DailyMaxProfitEnabled = true;
				TradingProfile2DailyMaxProfit       = 1000.0;
				TradingProfile2SizingProtect        = true;
				TradingProfile2SlPullProtect        = true;
				TradingProfile2LossDcaProtect       = true;
				TradingProfile2TpEarlyProtect       = true;
				TradingProfile2LossTimesProtect     = true;
				TradingProfile2TimingProtect        = true;
				TradingProfile2LossTimesMaxLosses   = 3;
				TradingProfile2LossTimesLockMinutes = 30;

				TradingProfile3Name                 = "P3";
				TradingProfile3Account              = "Sim101";
				TradingProfile3Atm                  = "";
				TradingProfile3Quantity             = 1;
				TradingProfile3Timeframe            = KatTimeframe.ChartTF;
				TradingProfile3BufferTicks          = 2;
				TradingProfile3StopLimitEnabled     = false;
				TradingProfile3EmaProtectEnabled    = true;
				TradingProfile3DailyMaxDDEnabled    = true;
				TradingProfile3DailyMaxDD           = 500.0;
				TradingProfile3DailyMaxProfitEnabled = true;
				TradingProfile3DailyMaxProfit       = 1000.0;
				TradingProfile3SizingProtect        = true;
				TradingProfile3SlPullProtect        = true;
				TradingProfile3LossDcaProtect       = true;
				TradingProfile3TpEarlyProtect       = true;
				TradingProfile3LossTimesProtect     = true;
				TradingProfile3TimingProtect        = true;
				TradingProfile3LossTimesMaxLosses   = 3;
				TradingProfile3LossTimesLockMinutes = 30;

				TradingProfile4Name                 = "P4";
				TradingProfile4Account              = "Sim101";
				TradingProfile4Atm                  = "";
				TradingProfile4Quantity             = 1;
				TradingProfile4Timeframe            = KatTimeframe.ChartTF;
				TradingProfile4BufferTicks          = 2;
				TradingProfile4StopLimitEnabled     = false;
				TradingProfile4EmaProtectEnabled    = true;
				TradingProfile4DailyMaxDDEnabled    = true;
				TradingProfile4DailyMaxDD           = 500.0;
				TradingProfile4DailyMaxProfitEnabled = true;
				TradingProfile4DailyMaxProfit       = 1000.0;
				TradingProfile4SizingProtect        = true;
				TradingProfile4SlPullProtect        = true;
				TradingProfile4LossDcaProtect       = true;
				TradingProfile4TpEarlyProtect       = true;
				TradingProfile4LossTimesProtect     = true;
				TradingProfile4TimingProtect        = true;
				TradingProfile4LossTimesMaxLosses   = 3;
				TradingProfile4LossTimesLockMinutes = 30;

				TradingProfile5Name                 = "P5";
				TradingProfile5Account              = "Sim101";
				TradingProfile5Atm                  = "";
				TradingProfile5Quantity             = 1;
				TradingProfile5Timeframe            = KatTimeframe.ChartTF;
				TradingProfile5BufferTicks          = 2;
				TradingProfile5StopLimitEnabled     = false;
				TradingProfile5EmaProtectEnabled    = true;
				TradingProfile5DailyMaxDDEnabled    = true;
				TradingProfile5DailyMaxDD           = 500.0;
				TradingProfile5DailyMaxProfitEnabled = true;
				TradingProfile5DailyMaxProfit       = 1000.0;
				TradingProfile5SizingProtect        = true;
				TradingProfile5SlPullProtect        = true;
				TradingProfile5LossDcaProtect       = true;
				TradingProfile5TpEarlyProtect       = true;
				TradingProfile5LossTimesProtect     = true;
				TradingProfile5TimingProtect        = true;
				TradingProfile5LossTimesMaxLosses   = 3;
				TradingProfile5LossTimesLockMinutes = 30;

				TradingProfile6Name                 = "P6";
				TradingProfile6Account              = "Sim101";
				TradingProfile6Atm                  = "";
				TradingProfile6Quantity             = 1;
				TradingProfile6Timeframe            = KatTimeframe.ChartTF;
				TradingProfile6BufferTicks          = 2;
				TradingProfile6StopLimitEnabled     = false;
				TradingProfile6EmaProtectEnabled    = true;
				TradingProfile6DailyMaxDDEnabled    = true;
				TradingProfile6DailyMaxDD           = 500.0;
				TradingProfile6DailyMaxProfitEnabled = true;
				TradingProfile6DailyMaxProfit       = 1000.0;
				TradingProfile6SizingProtect        = true;
				TradingProfile6SlPullProtect        = true;
				TradingProfile6LossDcaProtect       = true;
				TradingProfile6TpEarlyProtect       = true;
				TradingProfile6LossTimesProtect     = true;
				TradingProfile6TimingProtect        = true;
				TradingProfile6LossTimesMaxLosses   = 3;
				TradingProfile6LossTimesLockMinutes = 30;

				TradingProfile7Name                 = "P7";
				TradingProfile7Account              = "Sim101";
				TradingProfile7Atm                  = "";
				TradingProfile7Quantity             = 1;
				TradingProfile7Timeframe            = KatTimeframe.ChartTF;
				TradingProfile7BufferTicks          = 2;
				TradingProfile7StopLimitEnabled     = false;
				TradingProfile7EmaProtectEnabled    = true;
				TradingProfile7DailyMaxDDEnabled    = true;
				TradingProfile7DailyMaxDD           = 500.0;
				TradingProfile7DailyMaxProfitEnabled = true;
				TradingProfile7DailyMaxProfit       = 1000.0;
				TradingProfile7SizingProtect        = true;
				TradingProfile7SlPullProtect        = true;
				TradingProfile7LossDcaProtect       = true;
				TradingProfile7TpEarlyProtect       = true;
				TradingProfile7LossTimesProtect     = true;
				TradingProfile7TimingProtect        = true;
				TradingProfile7LossTimesMaxLosses   = 3;
				TradingProfile7LossTimesLockMinutes = 30;

				TradingProfile8Name                 = "P8";
				TradingProfile8Account              = "Sim101";
				TradingProfile8Atm                  = "";
				TradingProfile8Quantity             = 1;
				TradingProfile8Timeframe            = KatTimeframe.ChartTF;
				TradingProfile8BufferTicks          = 2;
				TradingProfile8StopLimitEnabled     = false;
				TradingProfile8EmaProtectEnabled    = true;
				TradingProfile8DailyMaxDDEnabled    = true;
				TradingProfile8DailyMaxDD           = 500.0;
				TradingProfile8DailyMaxProfitEnabled = true;
				TradingProfile8DailyMaxProfit       = 1000.0;
				TradingProfile8SizingProtect        = true;
				TradingProfile8SlPullProtect        = true;
				TradingProfile8LossDcaProtect       = true;
				TradingProfile8TpEarlyProtect       = true;
				TradingProfile8LossTimesProtect     = true;
				TradingProfile8TimingProtect        = true;
				TradingProfile8LossTimesMaxLosses   = 3;
				TradingProfile8LossTimesLockMinutes = 30;

				// HUD Master Toggles Defaults
				StopLimitEnabled                    = false;
				EmaProtectEnabled                   = true;

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
				cachedTfIndex = (int)DefaultTimeframe;
				cachedTickSize = TickSize;
				cachedBufferTicks = DefaultBufferTicks;
				cachedAtmTemplate = DefaultAtmTemplate;
				cachedHudLeftInset = Math.Max(0, HudLeftInset);
				cachedHudDragEnabled = HudDragEnabled;
				// Max DD / Max Profit always start ON every session regardless of the persisted toggle.
				DailyMaxDDEnabled = true;
				cachedIsDailyMaxDD = true;
				cachedDailyMaxDD = DailyMaxDD;
				DailyMaxProfitEnabled = true;
				cachedIsDailyMaxProfit = true;
				cachedDailyMaxProfit = DailyMaxProfit;
				// Discipline protects cache from persisted properties (default ON)
				cachedSizingProtect = SizingProtectEnabled;
				cachedSlPullProtect = SlPullProtectEnabled;
				cachedLossDcaProtect = LossDcaProtectEnabled;
				cachedTpEarlyProtect = TpEarlyProtectEnabled;
				cachedLossTimesProtect = LossTimesProtectEnabled;
				cachedTimingProtect = TimingWindowsProtectEnabled;
				cachedLossTimesMaxLosses = Math.Max(1, LossTimesMaxLosses);
				cachedLossTimesLockMinutes = Math.Max(1, LossTimesLockMinutes);
				cachedTw1Enabled = TradingWindow1Enabled;
				cachedTw1StartHour = TradingWindow1StartHour;
				cachedTw1StartMinute = TradingWindow1StartMinute;
				cachedTw1EndHour = TradingWindow1EndHour;
				cachedTw1EndMinute = TradingWindow1EndMinute;
				cachedTw2Enabled = TradingWindow2Enabled;
				cachedTw2StartHour = TradingWindow2StartHour;
				cachedTw2StartMinute = TradingWindow2StartMinute;
				cachedTw2EndHour = TradingWindow2EndHour;
				cachedTw2EndMinute = TradingWindow2EndMinute;
				cachedTw3Enabled = TradingWindow3Enabled;
				cachedTw3StartHour = TradingWindow3StartHour;
				cachedTw3StartMinute = TradingWindow3StartMinute;
				cachedTw3EndHour = TradingWindow3EndHour;
				cachedTw3EndMinute = TradingWindow3EndMinute;
				// HUD master toggles
				cachedIsStopLimit = StopLimitEnabled;
				cachedIsEmaPlace = EmaProtectEnabled;
				// DISCIPLINE (6 protects + EmaZoneOnly) always start ON every session (default ON)
				SizingProtectEnabled = true; SlPullProtectEnabled = true; LossDcaProtectEnabled = true; TpEarlyProtectEnabled = true; LossTimesProtectEnabled = true; TimingWindowsProtectEnabled = true; EmaProtectEnabled = true;
				cachedSizingProtect = true; cachedSlPullProtect = true; cachedLossDcaProtect = true; cachedTpEarlyProtect = true; cachedLossTimesProtect = true; cachedTimingProtect = true; cachedIsEmaPlace = true;
				// Migration: pre-v1.25 instances have all discipline props 0/false -> force ON defaults
				if (LossTimesMaxLosses == 0 && LossTimesLockMinutes == 0 && !SizingProtectEnabled && !SlPullProtectEnabled && !LossDcaProtectEnabled && !TpEarlyProtectEnabled && !LossTimesProtectEnabled && !TimingWindowsProtectEnabled)
				{
					SizingProtectEnabled = true; SlPullProtectEnabled = true; LossDcaProtectEnabled = true; TpEarlyProtectEnabled = true; LossTimesProtectEnabled = true; TimingWindowsProtectEnabled = true;
					LossTimesMaxLosses = 3; LossTimesLockMinutes = 30;
					TradingWindow1Enabled = true; TradingWindow1StartHour = 2; TradingWindow1StartMinute = 0; TradingWindow1EndHour = 15; TradingWindow1EndMinute = 0;
					TradingWindow2Enabled = false; TradingWindow3Enabled = false;
					cachedSizingProtect = true; cachedSlPullProtect = true; cachedLossDcaProtect = true; cachedTpEarlyProtect = true; cachedLossTimesProtect = true; cachedTimingProtect = true;
					cachedLossTimesMaxLosses = 3; cachedLossTimesLockMinutes = 30;
					cachedTw1Enabled = true; cachedTw1StartHour = 2; cachedTw1StartMinute = 0; cachedTw1EndHour = 15; cachedTw1EndMinute = 0;
					cachedTw2Enabled = false; cachedTw3Enabled = false;
				}
				// QuickSet appearance migration: ensure defaults for charts saved before v1.50
				if (QuickSetFontSize < 6 || QuickSetFontSize > 14) QuickSetFontSize = 8;
				if (QuickSetLabelColor == null) QuickSetLabelColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));
				if (QuickSetLabelOpacityPercent < 10 || QuickSetLabelOpacityPercent > 100) QuickSetLabelOpacityPercent = 50;
				// Migration: pre-v1.33 charts have profile quantities 0 -> seed defaults (ponytail: one-shot repair, no persist storm)
				if (TradingProfile1Quantity == 0 && TradingProfile2Quantity == 0 && TradingProfile3Quantity == 0 && TradingProfile4Quantity == 0 && TradingProfile5Quantity == 0 && TradingProfile6Quantity == 0)
				{
					TradingProfile1Name = "P1"; TradingProfile1Quantity = 1; TradingProfile1BufferTicks = 2; TradingProfile1DailyMaxDD = 500; TradingProfile1DailyMaxProfit = 1000; TradingProfile1DailyMaxDDEnabled = true; TradingProfile1DailyMaxProfitEnabled = true; TradingProfile1SizingProtect = true; TradingProfile1SlPullProtect = true; TradingProfile1LossDcaProtect = true; TradingProfile1TpEarlyProtect = true; TradingProfile1LossTimesProtect = true; TradingProfile1TimingProtect = true; TradingProfile1LossTimesMaxLosses = 3; TradingProfile1LossTimesLockMinutes = 30; TradingProfile1EmaProtectEnabled = true;
					TradingProfile2Name = "P2"; TradingProfile2Quantity = 1; TradingProfile2BufferTicks = 2; TradingProfile2DailyMaxDD = 500; TradingProfile2DailyMaxProfit = 1000; TradingProfile2DailyMaxDDEnabled = true; TradingProfile2DailyMaxProfitEnabled = true; TradingProfile2SizingProtect = true; TradingProfile2SlPullProtect = true; TradingProfile2LossDcaProtect = true; TradingProfile2TpEarlyProtect = true; TradingProfile2LossTimesProtect = true; TradingProfile2TimingProtect = true; TradingProfile2LossTimesMaxLosses = 3; TradingProfile2LossTimesLockMinutes = 30; TradingProfile2EmaProtectEnabled = true;
					TradingProfile3Name = "P3"; TradingProfile3Quantity = 1; TradingProfile3BufferTicks = 2; TradingProfile3DailyMaxDD = 500; TradingProfile3DailyMaxProfit = 1000; TradingProfile3DailyMaxDDEnabled = true; TradingProfile3DailyMaxProfitEnabled = true; TradingProfile3SizingProtect = true; TradingProfile3SlPullProtect = true; TradingProfile3LossDcaProtect = true; TradingProfile3TpEarlyProtect = true; TradingProfile3LossTimesProtect = true; TradingProfile3TimingProtect = true; TradingProfile3LossTimesMaxLosses = 3; TradingProfile3LossTimesLockMinutes = 30; TradingProfile3EmaProtectEnabled = true;
					TradingProfile4Name = "P4"; TradingProfile4Quantity = 1; TradingProfile4BufferTicks = 2; TradingProfile4DailyMaxDD = 500; TradingProfile4DailyMaxProfit = 1000; TradingProfile4DailyMaxDDEnabled = true; TradingProfile4DailyMaxProfitEnabled = true; TradingProfile4SizingProtect = true; TradingProfile4SlPullProtect = true; TradingProfile4LossDcaProtect = true; TradingProfile4TpEarlyProtect = true; TradingProfile4LossTimesProtect = true; TradingProfile4TimingProtect = true; TradingProfile4LossTimesMaxLosses = 3; TradingProfile4LossTimesLockMinutes = 30; TradingProfile4EmaProtectEnabled = true;
					TradingProfile5Name = "P5"; TradingProfile5Quantity = 1; TradingProfile5BufferTicks = 2; TradingProfile5DailyMaxDD = 500; TradingProfile5DailyMaxProfit = 1000; TradingProfile5DailyMaxDDEnabled = true; TradingProfile5DailyMaxProfitEnabled = true; TradingProfile5SizingProtect = true; TradingProfile5SlPullProtect = true; TradingProfile5LossDcaProtect = true; TradingProfile5TpEarlyProtect = true; TradingProfile5LossTimesProtect = true; TradingProfile5TimingProtect = true; TradingProfile5LossTimesMaxLosses = 3; TradingProfile5LossTimesLockMinutes = 30; TradingProfile5EmaProtectEnabled = true;
					TradingProfile6Name = "P6"; TradingProfile6Quantity = 1; TradingProfile6BufferTicks = 2; TradingProfile6DailyMaxDD = 500; TradingProfile6DailyMaxProfit = 1000; TradingProfile6DailyMaxDDEnabled = true; TradingProfile6DailyMaxProfitEnabled = true; TradingProfile6SizingProtect = true; TradingProfile6SlPullProtect = true; TradingProfile6LossDcaProtect = true; TradingProfile6TpEarlyProtect = true; TradingProfile6LossTimesProtect = true; TradingProfile6TimingProtect = true; TradingProfile6LossTimesMaxLosses = 3; TradingProfile6LossTimesLockMinutes = 30; TradingProfile6EmaProtectEnabled = true;
					if (string.IsNullOrWhiteSpace(TradingProfile1Account)) TradingProfile1Account = "Sim101";
					if (string.IsNullOrWhiteSpace(TradingProfile2Account)) TradingProfile2Account = "Sim101";
					if (string.IsNullOrWhiteSpace(TradingProfile3Account)) TradingProfile3Account = "Sim101";
					if (string.IsNullOrWhiteSpace(TradingProfile4Account)) TradingProfile4Account = "Sim101";
					if (string.IsNullOrWhiteSpace(TradingProfile5Account)) TradingProfile5Account = "Sim101";
					if (string.IsNullOrWhiteSpace(TradingProfile6Account)) TradingProfile6Account = "Sim101";
					// HUD master toggles migration for old charts (default Ema ON, StopLimit OFF)
					StopLimitEnabled = false; EmaProtectEnabled = true;
					cachedIsStopLimit = false; cachedIsEmaPlace = true;
				}
				// Additional migration for P7/P8 on pre-v1.50 charts (quantity 0 means never configured)
				if (TradingProfile7Quantity == 0)
				{
					TradingProfile7Name = "P7"; TradingProfile7Quantity = 1; TradingProfile7BufferTicks = 2; TradingProfile7DailyMaxDD = 500; TradingProfile7DailyMaxProfit = 1000; TradingProfile7DailyMaxDDEnabled = true; TradingProfile7DailyMaxProfitEnabled = true; TradingProfile7SizingProtect = true; TradingProfile7SlPullProtect = true; TradingProfile7LossDcaProtect = true; TradingProfile7TpEarlyProtect = true; TradingProfile7LossTimesProtect = true; TradingProfile7TimingProtect = true; TradingProfile7LossTimesMaxLosses = 3; TradingProfile7LossTimesLockMinutes = 30; TradingProfile7EmaProtectEnabled = true;
					if (string.IsNullOrWhiteSpace(TradingProfile7Account)) TradingProfile7Account = "Sim101";
				}
				if (TradingProfile8Quantity == 0)
				{
					TradingProfile8Name = "P8"; TradingProfile8Quantity = 1; TradingProfile8BufferTicks = 2; TradingProfile8DailyMaxDD = 500; TradingProfile8DailyMaxProfit = 1000; TradingProfile8DailyMaxDDEnabled = true; TradingProfile8DailyMaxProfitEnabled = true; TradingProfile8SizingProtect = true; TradingProfile8SlPullProtect = true; TradingProfile8LossDcaProtect = true; TradingProfile8TpEarlyProtect = true; TradingProfile8LossTimesProtect = true; TradingProfile8TimingProtect = true; TradingProfile8LossTimesMaxLosses = 3; TradingProfile8LossTimesLockMinutes = 30; TradingProfile8EmaProtectEnabled = true;
					if (string.IsNullOrWhiteSpace(TradingProfile8Account)) TradingProfile8Account = "Sim101";
				}
				isRenkoChart = BarsPeriod.BarsPeriodType == BarsPeriodType.Renko
				               || BarsPeriod.BarsPeriodType.ToString().IndexOf("Renko", StringComparison.OrdinalIgnoreCase) >= 0;

				ema34Series = new EMA[NUM_SERIES];
				ema89Series = new EMA[NUM_SERIES];
				for (int i = 0; i < NUM_SERIES; i++)
				{
					ema34TouchBarsAgo[i] = -1;
					ema89TouchBarsAgo[i] = -1;
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

				Print(string.Format("[KatTradeManager] v{0} loaded — cached mode active (Renko: {1})", VERSION, isRenkoChart));




				if (Account.All != null && Account.All.Count > 0)
				{
					Print("[KatTradeManager] Available Accounts:");
					foreach (var acc in Account.All)
					{
						Print(string.Format("  - {0} ({1})", acc.Name, acc.Connection != null ? "Connected" : "Disconnected"));
					}

					account = SelectAccount();
					EnsureAccountEventSubscription();

					if (account != null)
					{
						Print(string.Format("[KatTradeManager] Selected Account: {0}", account.Name));
					}
					else
					{
						Print("[KatTradeManager] WARNING: No account selected — buttons disabled until accounts connect.");
					}
				}
				else
				{
					Print("[KatTradeManager] WARNING: Account.All empty at load — watchdog will auto-recover when accounts connect.");
				}

				if (ChartControl != null)
					ChartControl.Dispatcher.InvokeAsync(StartPanelWatchdog);
			}
			else if (State == State.Terminated)
			{
				isTerminated = true;
				RemoveAccountEventSubscription();

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

		private void UpdateEmaTouchCache(int bip, EMA targetEma, int[] barsAgoCache, double[] highCache, double[] lowCache, DateTime[] timeCache, List<EmaTouchBarInfo>[] touchLists)
		{
			if (targetEma == null || CurrentBars[bip] < 0) return;

			int maxBars = Math.Min(CurrentBars[bip], 500);
			int foundBarsAgo = -1;
			double foundHigh = 0;
			double foundLow = 0;
			DateTime foundTime = DateTime.MinValue;
			List<EmaTouchBarInfo> touchBars = new List<EmaTouchBarInfo>();

			for (int barsAgo = 0; barsAgo < maxBars; barsAgo++)
			{
				double high = Highs[bip][barsAgo];
				double low = Lows[bip][barsAgo];
				if (!KatTradeCalculator.IsEmaTouchBar(high, low, targetEma[barsAgo])) continue;

				if (foundBarsAgo < 0)
				{
					foundBarsAgo = barsAgo;
					foundHigh = high;
					foundLow = low;
					foundTime = Times[bip][barsAgo];
				}
				touchBars.Add(new EmaTouchBarInfo
				{
					BarsAgo = barsAgo,
					Time = Times[bip][barsAgo],
					High = high,
					Low = low
				});
			}

			lock (priceLock)
			{
				barsAgoCache[bip] = foundBarsAgo;
				highCache[bip] = foundHigh;
				lowCache[bip] = foundLow;
				timeCache[bip] = foundTime;
				touchLists[bip] = touchBars;
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
					cachedCurrentClose[bip] = Closes[bip][0];
					cachedCurrentBarTime[bip] = Times[bip][0];
					if (bip == 0)
					{
						cachedCurrentPrice = Closes[0][0];
					}
					if (CurrentBars[bip] >= 1)
					{
						cachedPrevHigh[bip]  = Highs[bip][1];
						cachedPrevLow[bip]   = Lows[bip][1];
						cachedPrevBarTime[bip] = Times[bip][1];
					}
						if (bip == 0)
						{
							int maxSwingBars = Math.Min(CurrentBars[0], 500);
							for (int barsAgo = 0; barsAgo <= maxSwingBars; barsAgo++)
							{
								cachedSwingHighs[barsAgo] = Highs[0][barsAgo];
								cachedSwingLows[barsAgo] = Lows[0][barsAgo];
							}
							cachedSwingBars = maxSwingBars;
						}
					}
				}

				if (bip < NUM_SERIES && ema34Series != null && ema89Series != null)
				{
					UpdateEmaTouchCache(bip, ema34Series[bip], ema34TouchBarsAgo, ema34TouchHigh, ema34TouchLow, ema34TouchTime, ema34TouchLists);
					UpdateEmaTouchCache(bip, ema89Series[bip], ema89TouchBarsAgo, ema89TouchHigh, ema89TouchLow, ema89TouchTime, ema89TouchLists);
				}
				if (bip < NUM_SERIES && CurrentBars[bip] >= 0)
				{
					List<CandleBarInfo> candleBars = new List<CandleBarInfo>();
					int maxCandleBars = Math.Min(CurrentBars[bip], 500);
					for (int barsAgo = 0; barsAgo < maxCandleBars; barsAgo++)
					{
						candleBars.Add(new CandleBarInfo
						{
							BarsAgo = barsAgo,
							Time = Times[bip][barsAgo],
							High = Highs[bip][barsAgo],
							Low = Lows[bip][barsAgo]
						});
					}
					lock (priceLock)
					{
						candleBarLists[bip] = candleBars;
					}
				}
				if (bip < NUM_SERIES && emaPlaceFilterSeries != null)
					UpdateEmaFilterCache(bip);
				if (bip != 0 || account == null || Instrument == null) return;

				// Daily Risk Control evaluation
				EvaluateDailyRiskLimits();

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
					Order drawOrder = pendingDrawOrder;
					pendingDrawOrder = null;
					if (drawOrder != null
						&& drawOrder.OrderState != OrderState.Cancelled
						&& drawOrder.OrderState != OrderState.Rejected
						&& KatTradeCalculator.ShouldDrawExpectedLines(
							true,
							drawOrder.OrderType == OrderType.Market
								? KatOrderType.Market
								: (drawOrder.OrderType == OrderType.StopMarket ? KatOrderType.StopMarket : KatOrderType.Limit)))
					{
						DrawExpectedLines();
					}
				}

				// Auto-remove lines only on terminal states, not on transient states
				var trackedOrder = entryOrder; // local copy — UI thread may null it mid-check
				if (trackedOrder != null)
				{
					var state = trackedOrder.OrderState;
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

				TrySubmitPendingRevert();
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] OnBarUpdate error: {0}", ex.Message));
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

		// ponytail: NinjaScript properties extracted to src/KatTradeManager.Properties.cs (partial class)
	}
}
