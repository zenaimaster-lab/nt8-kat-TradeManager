/* KatTradeManagerUI.cs - WPF UI partial class for KatTradeManager v1.37 (2026-08-08) */

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
		private Button[] dailyRiskPresetButtons;
		private readonly SolidColorBrush dailyRiskPresetOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
		private readonly SolidColorBrush dailyRiskPresetOnBg = new SolidColorBrush(Color.FromRgb(36, 7, 72)); // darker than Max DD purple
		private Button[] disciplineButtons;
		private Button btnDisciplineOnAll;
		private Button btnDisciplineOffAll;
		private readonly SolidColorBrush disciplineOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
		// Trading profiles — 6 buttons in 2 rows above account selector, row-based ON colors, height 22 same as ATM row
		private Button[] tradingProfileButtons;
		private ComboBox accSelector;
		private Button btnStopLimit;
		private Button btnEmaPlace;
		private volatile int activeTradingProfile = -1; // last applied profile index, -1 = none
		private DateTime lastProfileApplyUtc = DateTime.MinValue;
		private string pendingProfileAccount;
		private DateTime pendingProfileAccountSinceUtc;
		private readonly SolidColorBrush profileOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
		private readonly SolidColorBrush[] profileRowOnBgs = new SolidColorBrush[]
		{
			new SolidColorBrush(Color.FromRgb(20, 110, 110)), // Row0 (P1-P3) teal — distinct from discipline blues
			new SolidColorBrush(Color.FromRgb(135, 35, 65)),  // Row1 (P4-P6) rose — distinct from ATM amber
		};
		// Row-based ON colors: 2 buttons per row share same shade (3 rows)
		private readonly SolidColorBrush[] disciplineRowBgs = new SolidColorBrush[]
		{
			new SolidColorBrush(Color.FromRgb(22, 60, 92)),   // Row0: Fix size + No SL-pull
			new SolidColorBrush(Color.FromRgb(32, 88, 138)),  // Row1: No loss-DCA + No TP-early
			new SolidColorBrush(Color.FromRgb(48, 120, 180)), // Row2: StopWhenLoss + TradingWindows
		};
		// Top Discipline/Un-Discipline row — same dark purple (user wants dark, not saturated bright)
		private readonly SolidColorBrush onAllBg = new SolidColorBrush(Color.FromRgb(55, 20, 85)); // Discipline All - dark purple
		private readonly SolidColorBrush offAllBg = new SolidColorBrush(Color.FromRgb(55, 20, 85));  // Un-Discipline - same dark purple
		private bool isHotkeyAttached = false;
		private Window hotkeyWindow; // cached at attach — chart can move to a new window before detach
		private bool hasHudDragPosition;
		private double hudDragLeft;
		private double hudDragTop;
		private Canvas hudCanvas;
		private TextBlock hudStatusText;
		private string pendingHudStatusMessage;
		private Brush pendingHudStatusBrush;
		private bool pendingHudStatusMessageIsPersistent;
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
				// Pending profile account (selected but not yet connected) has priority — don't fallback to wrong account
				if (account == null)
				{
					if (!string.IsNullOrEmpty(pendingProfileAccount))
					{
						Account pending = null;
						if (Account.All != null)
							pending = Account.All.FirstOrDefault(a => a.Name.Equals(pendingProfileAccount, StringComparison.OrdinalIgnoreCase));
						if (pending != null)
						{
							SwitchAccount(pending);
							pendingProfileAccount = null;
							pendingProfileAccountSinceUtc = DateTime.MinValue;
							Print(string.Format("[KatTradeManager] Profile account connected, switched to {0}", pending.Name));
						}
						else if (pendingProfileAccountSinceUtc != DateTime.MinValue && (DateTime.UtcNow - pendingProfileAccountSinceUtc).TotalSeconds > 30)
						{
							// pending timed out (30s) — fallback to normal selection so HUD not stuck null
							Print(string.Format("[KatTradeManager] Profile account '{0}' not connected within 30s, fallback to available", pendingProfileAccount));
							pendingProfileAccount = null;
							pendingProfileAccountSinceUtc = DateTime.MinValue;
							SwitchAccount(SelectAccount());
							if (account != null)
								Print(string.Format("[KatTradeManager] Account auto-recovered by watchdog: {0}", account.Name));
						}
						// else keep waiting (account stays null, HUD shows pending account name)
					}
					else
					{
						SwitchAccount(SelectAccount()); // resets daily-risk baseline for the fresh account
						if (account != null)
							Print(string.Format("[KatTradeManager] Account auto-recovered by watchdog: {0}", account.Name));
					}
				}
				EnsureAccountEventSubscription();
				// Pump serialized account mutations; pending broker states are revisited on each watchdog tick.
				ScheduleAccountOperationPump();

				AttachHotkeyHandler();

				// Sync UI control values to thread-safe cached fields
				SyncCachedValues();

				// Evaluate real-time daily risk protection limits
				EvaluateDailyRiskLimits();
				TrySubmitPendingRevert();
				ScheduleAtmBracketMerge();
				try { UpdateDisciplineFromPosition(); } catch {}
				try { EvaluateDisciplineLockVisual(); } catch {}
				try { UpdateTradingProfileButtons(); } catch {}
				try { UpdateAtmSetButtons(); } catch {}
				try { UpdateDailyRiskPresetButtons(); } catch {}
				try { UpdateStopLimitButton(); } catch {}
				try { UpdateEmaPlaceButton(); } catch {}
				for (int _di = 0; _di < 6; _di++) try { UpdateDisciplineButton(_di); } catch {}

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
			cachedHudLeftInset = Math.Max(0, HudLeftInset);
			bool wasHudDragEnabled = cachedHudDragEnabled;
			cachedHudDragEnabled = HudDragEnabled;
			if (wasHudDragEnabled && !cachedHudDragEnabled && isHudDragging)
				StopHudDrag();
			cachedIsDailyMaxDD = DailyMaxDDEnabled;
			cachedDailyMaxDD = DailyMaxDD;
			cachedIsDailyMaxProfit = DailyMaxProfitEnabled;
			cachedDailyMaxProfit = DailyMaxProfit;
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
			cachedIsStopLimit = StopLimitEnabled;
			cachedIsEmaPlace = EmaProtectEnabled;
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
			try { UpdateTradingProfileButtons(); } catch {}
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

		private string GetDailyRiskPresetName(int idx)
		{
			switch (idx)
			{
				case 0: return DailyRiskSet1Name;
				case 1: return DailyRiskSet2Name;
				case 2: return DailyRiskSet3Name;
				case 3: return DailyRiskSet4Name;
				case 4: return DailyRiskSet5Name;
				default: return DailyRiskSet6Name;
			}
		}

		private double GetDailyRiskPresetMaxDD(int idx)
		{
			switch (idx)
			{
				case 0: return DailyRiskSet1MaxDD;
				case 1: return DailyRiskSet2MaxDD;
				case 2: return DailyRiskSet3MaxDD;
				case 3: return DailyRiskSet4MaxDD;
				case 4: return DailyRiskSet5MaxDD;
				default: return DailyRiskSet6MaxDD;
			}
		}

		private double GetDailyRiskPresetMaxProfit(int idx)
		{
			switch (idx)
			{
				case 0: return DailyRiskSet1MaxProfit;
				case 1: return DailyRiskSet2MaxProfit;
				case 2: return DailyRiskSet3MaxProfit;
				case 3: return DailyRiskSet4MaxProfit;
				case 4: return DailyRiskSet5MaxProfit;
				default: return DailyRiskSet6MaxProfit;
			}
		}

		private void ApplyDailyRiskPreset(int idx)
		{
			DailyMaxDD = GetDailyRiskPresetMaxDD(idx);
			DailyMaxProfit = GetDailyRiskPresetMaxProfit(idx);
			cachedDailyMaxDD = DailyMaxDD;
			cachedDailyMaxProfit = DailyMaxProfit;
			UpdateDailyRiskPresetButtons();
			try { UpdateTradingProfileButtons(); } catch {}
			EvaluateDailyRiskLimits();
		}

		private void UpdateDailyRiskPresetButtons()
		{
			if (dailyRiskPresetButtons == null) return;

			for (int i = 0; i < dailyRiskPresetButtons.Length; i++)
			{
				if (dailyRiskPresetButtons[i] == null) continue;
				bool on = DailyMaxDD == GetDailyRiskPresetMaxDD(i)
					&& DailyMaxProfit == GetDailyRiskPresetMaxProfit(i);
				dailyRiskPresetButtons[i].Background = on ? dailyRiskPresetOnBg : dailyRiskPresetOffBg;
				dailyRiskPresetButtons[i].Foreground = on ? Brushes.White : Brushes.LightGray;
			}
		}

		// ponytail: trading profile helpers — ceiling = per-profile TradingWindows + EmaPlace expansion needs extra ~30 props (ponytail: upgrade when requested)
		private string GetTradingProfileName(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Name; case 1: return TradingProfile2Name; case 2: return TradingProfile3Name; case 3: return TradingProfile4Name; case 4: return TradingProfile5Name; default: return TradingProfile6Name; }
		}
		private string GetTradingProfileAccount(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Account; case 1: return TradingProfile2Account; case 2: return TradingProfile3Account; case 3: return TradingProfile4Account; case 4: return TradingProfile5Account; default: return TradingProfile6Account; }
		}
		private string GetTradingProfileAtm(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Atm; case 1: return TradingProfile2Atm; case 2: return TradingProfile3Atm; case 3: return TradingProfile4Atm; case 4: return TradingProfile5Atm; default: return TradingProfile6Atm; }
		}
		private int GetTradingProfileQuantity(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Quantity; case 1: return TradingProfile2Quantity; case 2: return TradingProfile3Quantity; case 3: return TradingProfile4Quantity; case 4: return TradingProfile5Quantity; default: return TradingProfile6Quantity; }
		}
		private KatTimeframe GetTradingProfileTimeframe(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Timeframe; case 1: return TradingProfile2Timeframe; case 2: return TradingProfile3Timeframe; case 3: return TradingProfile4Timeframe; case 4: return TradingProfile5Timeframe; default: return TradingProfile6Timeframe; }
		}
		private int GetTradingProfileBufferTicks(int idx)
		{
			switch (idx) { case 0: return TradingProfile1BufferTicks; case 1: return TradingProfile2BufferTicks; case 2: return TradingProfile3BufferTicks; case 3: return TradingProfile4BufferTicks; case 4: return TradingProfile5BufferTicks; default: return TradingProfile6BufferTicks; }
		}
		private bool GetTradingProfileStopLimit(int idx)
		{
			switch (idx) { case 0: return TradingProfile1StopLimitEnabled; case 1: return TradingProfile2StopLimitEnabled; case 2: return TradingProfile3StopLimitEnabled; case 3: return TradingProfile4StopLimitEnabled; case 4: return TradingProfile5StopLimitEnabled; default: return TradingProfile6StopLimitEnabled; }
		}
		private bool GetTradingProfileEmaProtect(int idx)
		{
			switch (idx) { case 0: return TradingProfile1EmaProtectEnabled; case 1: return TradingProfile2EmaProtectEnabled; case 2: return TradingProfile3EmaProtectEnabled; case 3: return TradingProfile4EmaProtectEnabled; case 4: return TradingProfile5EmaProtectEnabled; default: return TradingProfile6EmaProtectEnabled; }
		}
		private bool GetTradingProfileDailyMaxDDEnabled(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxDDEnabled; case 1: return TradingProfile2DailyMaxDDEnabled; case 2: return TradingProfile3DailyMaxDDEnabled; case 3: return TradingProfile4DailyMaxDDEnabled; case 4: return TradingProfile5DailyMaxDDEnabled; default: return TradingProfile6DailyMaxDDEnabled; }
		}
		private double GetTradingProfileDailyMaxDD(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxDD; case 1: return TradingProfile2DailyMaxDD; case 2: return TradingProfile3DailyMaxDD; case 3: return TradingProfile4DailyMaxDD; case 4: return TradingProfile5DailyMaxDD; default: return TradingProfile6DailyMaxDD; }
		}
		private bool GetTradingProfileDailyMaxProfitEnabled(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxProfitEnabled; case 1: return TradingProfile2DailyMaxProfitEnabled; case 2: return TradingProfile3DailyMaxProfitEnabled; case 3: return TradingProfile4DailyMaxProfitEnabled; case 4: return TradingProfile5DailyMaxProfitEnabled; default: return TradingProfile6DailyMaxProfitEnabled; }
		}
		private double GetTradingProfileDailyMaxProfit(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxProfit; case 1: return TradingProfile2DailyMaxProfit; case 2: return TradingProfile3DailyMaxProfit; case 3: return TradingProfile4DailyMaxProfit; case 4: return TradingProfile5DailyMaxProfit; default: return TradingProfile6DailyMaxProfit; }
		}
		private bool GetTradingProfileSizing(int idx)
		{
			switch (idx) { case 0: return TradingProfile1SizingProtect; case 1: return TradingProfile2SizingProtect; case 2: return TradingProfile3SizingProtect; case 3: return TradingProfile4SizingProtect; case 4: return TradingProfile5SizingProtect; default: return TradingProfile6SizingProtect; }
		}
		private bool GetTradingProfileSlPull(int idx)
		{
			switch (idx) { case 0: return TradingProfile1SlPullProtect; case 1: return TradingProfile2SlPullProtect; case 2: return TradingProfile3SlPullProtect; case 3: return TradingProfile4SlPullProtect; case 4: return TradingProfile5SlPullProtect; default: return TradingProfile6SlPullProtect; }
		}
		private bool GetTradingProfileLossDca(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossDcaProtect; case 1: return TradingProfile2LossDcaProtect; case 2: return TradingProfile3LossDcaProtect; case 3: return TradingProfile4LossDcaProtect; case 4: return TradingProfile5LossDcaProtect; default: return TradingProfile6LossDcaProtect; }
		}
		private bool GetTradingProfileTpEarly(int idx)
		{
			switch (idx) { case 0: return TradingProfile1TpEarlyProtect; case 1: return TradingProfile2TpEarlyProtect; case 2: return TradingProfile3TpEarlyProtect; case 3: return TradingProfile4TpEarlyProtect; case 4: return TradingProfile5TpEarlyProtect; default: return TradingProfile6TpEarlyProtect; }
		}
		private bool GetTradingProfileLossTimes(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossTimesProtect; case 1: return TradingProfile2LossTimesProtect; case 2: return TradingProfile3LossTimesProtect; case 3: return TradingProfile4LossTimesProtect; case 4: return TradingProfile5LossTimesProtect; default: return TradingProfile6LossTimesProtect; }
		}
		private bool GetTradingProfileTiming(int idx)
		{
			switch (idx) { case 0: return TradingProfile1TimingProtect; case 1: return TradingProfile2TimingProtect; case 2: return TradingProfile3TimingProtect; case 3: return TradingProfile4TimingProtect; case 4: return TradingProfile5TimingProtect; default: return TradingProfile6TimingProtect; }
		}
		private int GetTradingProfileLossTimesMaxLosses(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossTimesMaxLosses; case 1: return TradingProfile2LossTimesMaxLosses; case 2: return TradingProfile3LossTimesMaxLosses; case 3: return TradingProfile4LossTimesMaxLosses; case 4: return TradingProfile5LossTimesMaxLosses; default: return TradingProfile6LossTimesMaxLosses; }
		}
		private int GetTradingProfileLossTimesLockMinutes(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossTimesLockMinutes; case 1: return TradingProfile2LossTimesLockMinutes; case 2: return TradingProfile3LossTimesLockMinutes; case 3: return TradingProfile4LossTimesLockMinutes; case 4: return TradingProfile5LossTimesLockMinutes; default: return TradingProfile6LossTimesLockMinutes; }
		}

		private bool IsTradingProfileConfigured(int idx)
		{
			string acc = GetTradingProfileAccount(idx);
			string atm = GetTradingProfileAtm(idx);
			return !string.IsNullOrWhiteSpace(acc) || !string.IsNullOrWhiteSpace(atm);
		}

		private bool IsTradingProfileActive(int idx)
		{
			if (!IsTradingProfileConfigured(idx)) return false;
			// compare live indicator properties/cached toggles to stored profile snapshot (clamp numeric to valid ranges as Apply does)
			if (!string.Equals(AccountName ?? string.Empty, GetTradingProfileAccount(idx) ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return false;
			string liveAtm = IsNoAtmSelection(DefaultAtmTemplate) ? string.Empty : (DefaultAtmTemplate ?? string.Empty);
			string profAtm = IsNoAtmSelection(GetTradingProfileAtm(idx)) ? string.Empty : (GetTradingProfileAtm(idx) ?? string.Empty);
			if (!string.Equals(liveAtm, profAtm, StringComparison.OrdinalIgnoreCase)) return false;
			int profQty = Math.Max(1, Math.Min(100, GetTradingProfileQuantity(idx)));
			if (DefaultQuantity != profQty) return false;
			if (DefaultTimeframe != GetTradingProfileTimeframe(idx)) return false;
			int profBuf = Math.Max(0, Math.Min(100, GetTradingProfileBufferTicks(idx)));
			if (DefaultBufferTicks != profBuf) return false;
			if (cachedIsStopLimit != GetTradingProfileStopLimit(idx)) return false;
			if (cachedIsEmaPlace != GetTradingProfileEmaProtect(idx)) return false;
			if (DailyMaxDDEnabled != GetTradingProfileDailyMaxDDEnabled(idx)) return false;
			if (Math.Abs(DailyMaxDD - GetTradingProfileDailyMaxDD(idx)) > 0.0001) return false;
			if (DailyMaxProfitEnabled != GetTradingProfileDailyMaxProfitEnabled(idx)) return false;
			if (Math.Abs(DailyMaxProfit - GetTradingProfileDailyMaxProfit(idx)) > 0.0001) return false;
			if (SizingProtectEnabled != GetTradingProfileSizing(idx)) return false;
			if (SlPullProtectEnabled != GetTradingProfileSlPull(idx)) return false;
			if (LossDcaProtectEnabled != GetTradingProfileLossDca(idx)) return false;
			if (TpEarlyProtectEnabled != GetTradingProfileTpEarly(idx)) return false;
			if (LossTimesProtectEnabled != GetTradingProfileLossTimes(idx)) return false;
			if (TimingWindowsProtectEnabled != GetTradingProfileTiming(idx)) return false;
			int profLossMax = Math.Max(1, Math.Min(20, GetTradingProfileLossTimesMaxLosses(idx)));
			if (LossTimesMaxLosses != profLossMax) return false;
			int profLock = Math.Max(1, Math.Min(1440, GetTradingProfileLossTimesLockMinutes(idx)));
			if (LossTimesLockMinutes != profLock) return false;
			return true;
		}

		private void UpdateTradingProfileButtons()
		{
			if (tradingProfileButtons == null) return;
			// highlight the single profile that matches live config (covers both post-restart and manual edit to match other profile)
			int uniqueMatch = -1;
			{
				int matches = 0;
				for (int j = 0; j < 6; j++) if (IsTradingProfileActive(j)) { matches++; uniqueMatch = j; }
				if (matches != 1) uniqueMatch = -1;
			}
			for (int i = 0; i < tradingProfileButtons.Length; i++)
			{
				if (tradingProfileButtons[i] == null) continue;
				bool on = (uniqueMatch != -1 && i == uniqueMatch) || (uniqueMatch == -1 && activeTradingProfile == i && IsTradingProfileActive(i));
				if (on)
				{
					int row = i / 3; // 0 for P1-P3, 1 for P4-P6
					tradingProfileButtons[i].Background = profileRowOnBgs[Math.Min(row, profileRowOnBgs.Length - 1)];
					tradingProfileButtons[i].Foreground = Brushes.White;
				}
				else
				{
					tradingProfileButtons[i].Background = profileOffBg;
					tradingProfileButtons[i].Foreground = Brushes.LightGray;
				}
				// keep label in sync if user changed name in settings without rebuild
				string expected = GetTradingProfileName(i);
				if (tradingProfileButtons[i].Content as string != expected)
					tradingProfileButtons[i].Content = expected;
				try
				{
					string tAcc2 = GetTradingProfileAccount(i);
					string tAtm2 = GetTradingProfileAtm(i);
					if (IsNoAtmSelection(tAtm2)) tAtm2 = "None";
					tradingProfileButtons[i].ToolTip = string.Format("{0}: {1} / {2}  DD {3}  TP {4}", expected, string.IsNullOrWhiteSpace(tAcc2) ? "(no acc)" : tAcc2, tAtm2, GetTradingProfileDailyMaxDD(i), GetTradingProfileDailyMaxProfit(i));
				} catch {}
			}
		}

		private void UpdateStopLimitButton()
		{
			if (btnStopLimit == null) return;
			SolidColorBrush offBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
			SolidColorBrush onBg = new SolidColorBrush(Color.FromRgb(180, 90, 20));
			btnStopLimit.Content = cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF";
			btnStopLimit.Background = cachedIsStopLimit ? onBg : offBg;
			btnStopLimit.Foreground = cachedIsStopLimit ? Brushes.White : Brushes.LightGray;
		}

		private void UpdateEmaPlaceButton()
		{
			if (btnEmaPlace == null) return;
			SolidColorBrush offBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
			SolidColorBrush onBg = new SolidColorBrush(Color.FromRgb(12, 35, 75));
			btnEmaPlace.Content = cachedIsEmaPlace ? "Ema protect: ON" : "Ema protect: OFF";
			btnEmaPlace.Background = cachedIsEmaPlace ? onBg : offBg;
			btnEmaPlace.Foreground = cachedIsEmaPlace ? Brushes.White : Brushes.LightGray;
		}

		private void ApplyTradingProfile(int idx)
		{
			if (idx < 0 || idx >= 6) return;
			// debounce: same profile double-click within 500ms ignored (anti-spam)
			if (activeTradingProfile == idx && (DateTime.UtcNow - lastProfileApplyUtc).TotalMilliseconds < 500) return;
			lastProfileApplyUtc = DateTime.UtcNow;
			string acc = GetTradingProfileAccount(idx);
			string atm = GetTradingProfileAtm(idx);
			if (string.IsNullOrWhiteSpace(acc) && string.IsNullOrWhiteSpace(atm))
			{
				ShowHudStatus(string.Format("Profile {0}: no account/ATM configured (Indicator Settings)", GetTradingProfileName(idx)), Brushes.OrangeRed);
				return;
			}
			// Quantity / timeframe / buffer — persisted props + cached (clamp to valid ranges)
			int qty = Math.Max(1, Math.Min(100, GetTradingProfileQuantity(idx)));
			DefaultQuantity = qty;
			KatTimeframe tf = GetTradingProfileTimeframe(idx);
			DefaultTimeframe = tf;
			cachedTfIndex = (int)tf;
			int buf = Math.Max(0, Math.Min(100, GetTradingProfileBufferTicks(idx)));
			DefaultBufferTicks = buf;
			cachedBufferTicks = buf;

			bool isStop = GetTradingProfileStopLimit(idx);
			cachedIsStopLimit = isStop; StopLimitEnabled = isStop;
			bool isEma = GetTradingProfileEmaProtect(idx);
			cachedIsEmaPlace = isEma; EmaProtectEnabled = isEma;

			// Daily risk — enabled + values
			bool ddEn = GetTradingProfileDailyMaxDDEnabled(idx);
			double dd = GetTradingProfileDailyMaxDD(idx);
			bool pfEn = GetTradingProfileDailyMaxProfitEnabled(idx);
			double pf = GetTradingProfileDailyMaxProfit(idx);
			DailyMaxDDEnabled = ddEn; DailyMaxDD = dd; cachedIsDailyMaxDD = ddEn; cachedDailyMaxDD = dd;
			DailyMaxProfitEnabled = pfEn; DailyMaxProfit = pf; cachedIsDailyMaxProfit = pfEn; cachedDailyMaxProfit = pf;

			// Discipline
			bool siz = GetTradingProfileSizing(idx);
			bool slPull = GetTradingProfileSlPull(idx);
			bool lossDca = GetTradingProfileLossDca(idx);
			bool tpEarly = GetTradingProfileTpEarly(idx);
			bool lossTimes = GetTradingProfileLossTimes(idx);
			bool timing = GetTradingProfileTiming(idx);
			int maxLosses = Math.Max(1, Math.Min(20, GetTradingProfileLossTimesMaxLosses(idx)));
			int lockMins = Math.Max(1, Math.Min(1440, GetTradingProfileLossTimesLockMinutes(idx)));
			SizingProtectEnabled = siz; cachedSizingProtect = siz;
			SlPullProtectEnabled = slPull; cachedSlPullProtect = slPull;
			LossDcaProtectEnabled = lossDca; cachedLossDcaProtect = lossDca;
			TpEarlyProtectEnabled = tpEarly; cachedTpEarlyProtect = tpEarly;
			LossTimesProtectEnabled = lossTimes; cachedLossTimesProtect = lossTimes;
			TimingWindowsProtectEnabled = timing; cachedTimingProtect = timing;
			LossTimesMaxLosses = maxLosses; cachedLossTimesMaxLosses = maxLosses;
			LossTimesLockMinutes = lockMins; cachedLossTimesLockMinutes = lockMins;

			// Discipline visuals + daily preset visuals + toggles (pre-switch for old account)
			for (int i = 0; i < 6; i++) UpdateDisciplineButton(i);
			UpdateDailyRiskPresetButtons();
			UpdateStopLimitButton();
			UpdateEmaPlaceButton();
			EvaluateDailyRiskLimits();
			try { UpdateDisciplineFromPosition(); } catch {}
			try { EvaluateDisciplineLockVisual(); } catch {}

			// Account — switch first so baseline resets before any PnL check
			if (!string.IsNullOrWhiteSpace(acc))
			{
				Account target = null;
				if (Account.All != null)
					target = Account.All.FirstOrDefault(a => a.Name.Equals(acc, StringComparison.OrdinalIgnoreCase));
				if (target != null)
				{
					SwitchAccount(target);
					AccountName = acc;
					pendingProfileAccount = null;
					pendingProfileAccountSinceUtc = DateTime.MinValue;
					if (accSelector != null)
					{
						for (int i = 0; i < accSelector.Items.Count; i++)
						{
							if (accSelector.Items[i].ToString().Equals(acc, StringComparison.OrdinalIgnoreCase))
							{ accSelector.SelectedItem = accSelector.Items[i]; break; }
						}
						// if account not in filtered list, add it visibly so HUD reflects profile
						if (accSelector.SelectedItem == null || !accSelector.SelectedItem.ToString().Equals(acc, StringComparison.OrdinalIgnoreCase))
						{
							if (!accSelector.Items.Contains(acc)) accSelector.Items.Add(acc);
							accSelector.SelectedItem = acc;
						}
					}
					SyncChartTraderAccount(acc);
					Print(string.Format("[KatTradeManager] Profile {0}: switched account to {1}", GetTradingProfileName(idx), acc));
				}
				else
				{
					// account not connected yet — clear live account so no orders go to stale account, persist name for watchdog auto-recovery
					SwitchAccount(null);
					AccountName = acc;
					pendingProfileAccount = acc;
					pendingProfileAccountSinceUtc = DateTime.UtcNow;
					if (accSelector != null)
					{
						if (!accSelector.Items.Contains(acc)) accSelector.Items.Add(acc);
						accSelector.SelectedItem = acc;
					}
					ShowHudStatus(string.Format("Profile {0}: account '{1}' not connected yet", GetTradingProfileName(idx), acc), Brushes.Orange);
				}
				// re-evaluate discipline & risk for newly switched account (position may differ)
				try { UpdateDisciplineFromPosition(); } catch {}
				try { EvaluateDisciplineLockVisual(); } catch {}
				try { EvaluateDailyRiskLimits(); } catch {}
			}

			// ATM — use same path as quick set (dropdown + ApplyAtmSelection) — "None" treated as empty (no ATM)
			if (!IsNoAtmSelection(atm))
			{
				bool found = false;
				if (atmSelector != null)
				{
					for (int i = 0; i < atmSelector.Items.Count; i++)
					{
						if (atmSelector.Items[i].ToString().Equals(atm, StringComparison.OrdinalIgnoreCase))
						{
							atmSelector.SelectedIndex = i;
							found = true; break;
						}
					}
					if (!found)
					{
						atmSelector.Items.Add(atm);
						atmSelector.SelectedItem = atm;
					}
				}
				ApplyAtmSelection(atm); // ensures cachedAtmTemplate + LoadAtmTemplateSettings + UpdateAtmSetButtons
				if (!found && !HasAtmTemplate(atm))
					ShowHudStatus(string.Format("Profile {0}: ATM '{1}' not found on disk (still selected)", GetTradingProfileName(idx), atm), Brushes.Orange);
			}
			else
			{
				// profile wants None (empty or "None")
				if (atmSelector != null) atmSelector.SelectedIndex = 0;
				ApplyAtmSelection(NoAtmTemplateLabel);
			}

			activeTradingProfile = idx;
			UpdateTradingProfileButtons();
			UpdateAtmSetButtons();
			// if ATM was missing we already showed orange status — keep it, don't overwrite with green; "None" is not missing
			bool atmMissing = !IsNoAtmSelection(atm) && !HasAtmTemplate(atm);
			if (!atmMissing)
				ShowHudStatus(string.Format("Profile {0} applied: {1} / {2}", GetTradingProfileName(idx), string.IsNullOrWhiteSpace(acc) ? "(no acc)" : acc, IsNoAtmSelection(atm) ? "None" : atm), Brushes.LightGreen);
		}

		private void ToggleDiscipline(int idx)
		{
			switch (idx)
			{
				case 0: cachedSizingProtect = !cachedSizingProtect; SizingProtectEnabled = cachedSizingProtect; break;
				case 1: cachedSlPullProtect = !cachedSlPullProtect; SlPullProtectEnabled = cachedSlPullProtect; break;
				case 2: cachedLossDcaProtect = !cachedLossDcaProtect; LossDcaProtectEnabled = cachedLossDcaProtect; break;
				case 3: cachedTpEarlyProtect = !cachedTpEarlyProtect; TpEarlyProtectEnabled = cachedTpEarlyProtect; break;
				case 4: cachedLossTimesProtect = !cachedLossTimesProtect; LossTimesProtectEnabled = cachedLossTimesProtect; break;
				case 5: cachedTimingProtect = !cachedTimingProtect; TimingWindowsProtectEnabled = cachedTimingProtect; break;
				default: return;
			}
			UpdateDisciplineButton(idx);
			try { UpdateTradingProfileButtons(); } catch {}
			// if disabling LossTimes while locked, clear persistent status immediately
			if (idx == 4 && !cachedLossTimesProtect && hudStatusText != null)
			{
				DisciplineState st = GetCurrentDisciplineState();
				bool locked = false;
				try { lock (disciplineLock) { locked = KatTradeCalculator.IsLossTimesLockActive(st.LockUntilUtc, DateTime.UtcNow); } } catch {}
				if (locked)
				{
					// keep lock data but visual will be suppressed because gate now OFF; clear HUD
					if (hudStatusTimer != null) hudStatusTimer.Stop();
					hudStatusText.Text = "LossTimes OFF - trading unlocked";
					hudStatusText.Foreground = Brushes.LightGray;
				}
			}
		}

		private void UpdateDisciplineButton(int idx)
		{
			if (disciplineButtons == null || idx < 0 || idx >= disciplineButtons.Length) return;
			Button btn = disciplineButtons[idx];
			if (btn == null) return;
			string[] labels = new[] { "Fix size", "No SL-pull", "No loss-DCA", "No TP-early", "StopWhenLoss", "TradingWindows" };
			bool isOn = false;
			switch (idx)
			{
				case 0: isOn = cachedSizingProtect; break;
				case 1: isOn = cachedSlPullProtect; break;
				case 2: isOn = cachedLossDcaProtect; break;
				case 3: isOn = cachedTpEarlyProtect; break;
				case 4: isOn = cachedLossTimesProtect; break;
				case 5: isOn = cachedTimingProtect; break;
			}
			btn.Content = isOn ? labels[idx] : labels[idx] + ": OFF";
			int row = idx / 2;
			btn.Background = isOn ? disciplineRowBgs[row] : disciplineOffBg;
			btn.Foreground = isOn ? Brushes.White : Brushes.LightGray;
		}

		private void SetAllDiscipline(bool isOn)
		{
			cachedSizingProtect = isOn; SizingProtectEnabled = isOn;
			cachedSlPullProtect = isOn; SlPullProtectEnabled = isOn;
			cachedLossDcaProtect = isOn; LossDcaProtectEnabled = isOn;
			cachedTpEarlyProtect = isOn; TpEarlyProtectEnabled = isOn;
			cachedLossTimesProtect = isOn; LossTimesProtectEnabled = isOn;
			cachedTimingProtect = isOn; TimingWindowsProtectEnabled = isOn;
			for (int i = 0; i < 6; i++) UpdateDisciplineButton(i);
			try { UpdateTradingProfileButtons(); } catch {}
			if (!isOn)
			{
				// clearing loss lock visual when OFF ALL disables it
				DisciplineState st = GetCurrentDisciplineState();
				bool locked = false;
				try { lock (disciplineLock) { locked = KatTradeCalculator.IsLossTimesLockActive(st.LockUntilUtc, DateTime.UtcNow); } } catch {}
				if (locked && hudStatusText != null)
				{
					if (hudStatusTimer != null) hudStatusTimer.Stop();
					hudStatusText.Text = "Discipline OFF ALL - all locks released";
					hudStatusText.Foreground = Brushes.LightGray;
				}
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

		// Mirrors the HUD account pick into Chart Trader's own account selector so chart order
		// rendering follows the HUD account. Locates the selector by item content (account names),
		// which survives NT8 template/layout changes better than hardcoded names.
		private void SyncChartTraderAccount(string accountName)
		{
			try
			{
				if (string.IsNullOrEmpty(accountName)) return;
				DependencyObject ctControl = GetChartTraderControl();
				if (ctControl == null) return;

				List<ComboBox> combos = new List<ComboBox>();
				FindAllVisualChildren<ComboBox>(ctControl, combos);
				foreach (ComboBox combo in combos)
					foreach (object item in combo.Items)
					{
						if (item == null) continue;
						// Rithmic accounts render as "name!connection!connection" in Chart Trader's
						// selector while Account.Name stays short — match on Name first, then on
						// exact/prefixed ToString. (Proven fix from nt8-kat-34-Scalper.)
						string itemText = item.ToString();
						bool match = (item as Account)?.Name.Equals(accountName, StringComparison.OrdinalIgnoreCase) == true
							|| itemText.Equals(accountName, StringComparison.OrdinalIgnoreCase)
							|| itemText.StartsWith(accountName + "!", StringComparison.OrdinalIgnoreCase);
						if (!match) continue;
						if (!ReferenceEquals(combo.SelectedItem, item))
							combo.SelectedItem = item;
						return;
					}
				// No match: Chart Trader's account selector (NinjaTrader.Gui.Tools.AccountSelector) only
				// lists accounts NT8 currently offers — connected-connection accounts, minus Backtest/Playback.
				// Report what it actually lists so the gap is diagnosable.
				List<string> listed = new List<string>();
				foreach (ComboBox combo in combos)
					foreach (object item in combo.Items)
						if (item is Account listedAcc && !listed.Contains(listedAcc.Name))
							listed.Add(listedAcc.Name);
				Print(string.Format("[KatTradeManager] Chart Trader sync skipped — '{0}' not in its account list (listed: {1})",
					accountName, listed.Count > 0 ? string.Join(", ", listed) : "none"));
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Chart Trader account sync failed: {0}", ex.Message));
			}
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
				bool wasPersistent = pendingHudStatusMessageIsPersistent;
				pendingHudStatusMessage = null;
				pendingHudStatusBrush = null;
				pendingHudStatusMessageIsPersistent = false;
				if (!wasPersistent)
				{
					if (hudStatusTimer == null)
					{
						hudStatusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
						hudStatusTimer.Tick += (s, e) =>
						{
							if (hudStatusText != null)
							{
								DisciplineState stChk2 = GetCurrentDisciplineState();
								bool stillLocked2 = false;
								try { lock (disciplineLock) { stillLocked2 = cachedLossTimesProtect && KatTradeCalculator.IsLossTimesLockActive(stChk2.LockUntilUtc, DateTime.UtcNow); } } catch {}
								if (stillLocked2) { EvaluateDisciplineLockVisual(); return; }
								hudStatusText.Text = string.Empty;
								hudStatusText.Foreground = Brushes.White;
							}
							hudStatusTimer.Stop();
						};
					}
					hudStatusTimer.Stop();
					hudStatusTimer.Start();
				}
			}

			// --- Trading Profile quick presets (6 buttons, 2 rows x 3 cols, above account) ---
			tradingProfileButtons = new Button[6];
			StackPanel profileStack = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
			for (int prow = 0; prow < 2; prow++)
			{
				Grid rowGrid = new Grid { Margin = new Thickness(0, 0, 0, prow == 0 ? 2 : 0) };
				for (int c = 0; c < 3; c++)
				{
					rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
					if (c < 2) rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
				}
				for (int cc = 0; cc < 3; cc++)
				{
					int idx = prow * 3 + cc;
					Button pBtn = CreateButton(GetTradingProfileName(idx), profileOffBg, null, 22, 10);
					pBtn.Foreground = Brushes.LightGray;
					pBtn.Margin = new Thickness(0);
					int captured = idx;
					pBtn.Click += (s, ev) => ApplyTradingProfile(captured);
					Grid.SetColumn(pBtn, cc * 2);
					tradingProfileButtons[idx] = pBtn;
					rowGrid.Children.Add(pBtn);
				}
				profileStack.Children.Add(rowGrid);
			}
			sec1Panel.Children.Add(profileStack);
			UpdateTradingProfileButtons();
			// update tooltips with live profile snapshot (account/ATM/DD/Profit) after buttons created
			try
			{
				for (int i = 0; i < 6; i++) if (tradingProfileButtons[i] != null)
				{
					string tAcc = GetTradingProfileAccount(i);
					string tAtm = GetTradingProfileAtm(i);
					if (IsNoAtmSelection(tAtm)) tAtm = "None";
					tradingProfileButtons[i].ToolTip = string.Format("{0}: {1} / {2}  DD {3}  TP {4}", GetTradingProfileName(i), string.IsNullOrWhiteSpace(tAcc) ? "(no acc)" : tAcc, tAtm, GetTradingProfileDailyMaxDD(i), GetTradingProfileDailyMaxProfit(i));
				}
			} catch {}

			accSelector = new ComboBox { FontSize = 11, Height = 22, Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
			if (Account.All != null)
			{
				var allowedAccs = Account.All.Where(a => IsAccountAllowed(a.Name)).ToList();
				foreach (var acc in allowedAccs) accSelector.Items.Add(acc.Name);
				string savedAccountName = AccountName;
				// profile may have set an account that is filtered out or not yet connected (pending) — still show it to preserve explicit selection
				if (!string.IsNullOrEmpty(savedAccountName) && !accSelector.Items.Contains(savedAccountName))
				{
					accSelector.Items.Add(savedAccountName);
				}
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
				else if (!string.IsNullOrEmpty(savedAccountName))
				{
					// fallback: filtered list empty but saved account exists — keep it visible
					if (!accSelector.Items.Contains(savedAccountName)) accSelector.Items.Add(savedAccountName);
					accSelector.SelectedItem = savedAccountName;
				}
			}
			else if (!string.IsNullOrEmpty(AccountName))
			{
				accSelector.Items.Add(AccountName);
				accSelector.SelectedItem = AccountName;
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
					pendingProfileAccount = null;
					pendingProfileAccountSinceUtc = DateTime.MinValue;
					// NT8 only renders chart orders for the account selected in Chart Trader — mirror the pick there.
					SyncChartTraderAccount(selectedName);
					Print(string.Format("[KatTradeManager] Account changed via UI to: {0}", selectedName));
					try { UpdateTradingProfileButtons(); } catch {}
					try { UpdateAtmSetButtons(); } catch {}
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
			bool atmFound = false;
			if (!string.IsNullOrEmpty(DefaultAtmTemplate))
			{
				for (int i = 0; i < atmSelector.Items.Count; i++)
				{
					if (atmSelector.Items[i].ToString().Equals(DefaultAtmTemplate, StringComparison.OrdinalIgnoreCase))
					{
						atmSelector.SelectedIndex = i;
						atmFound = true;
						break;
					}
				}
				if (!atmFound && !IsNoAtmSelection(DefaultAtmTemplate))
				{
					atmSelector.Items.Add(DefaultAtmTemplate);
					atmSelector.SelectedItem = DefaultAtmTemplate;
					atmFound = true;
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


			// --- SECTION 3: Market & Candle Orders + Position Management ---
			StackPanel sec3Panel = new StackPanel();

			// --- Market Orders (top of execution section) ---
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

			sec3Panel.Children.Add(mktBtnGrid);

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

			// --- BE / Revert row below BUY/SELL previous ---
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

			sec3Panel.Children.Add(beRevertGrid);

			SolidColorBrush closeBg = new SolidColorBrush(Color.FromRgb(20, 20, 20)); // Very dark gray (almost black)
			Button btnClose = CreateButton("Close/flatten", closeBg, (s, ev) => FlattenAllPositions(), 66, 15);
			sec3Panel.Children.Add(btnClose);

			mainPanel.Children.Add(CreateSectionCard(sec3Panel, 6));


			// --- SECTION 4: ON/OFF Toggles ---
			StackPanel sec4Panel = new StackPanel();

			// Stop-Limit + EMA Protect side-by-side
			Grid modeToggleGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			modeToggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			modeToggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			modeToggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush toggleOffBg   = new SolidColorBrush(Color.FromRgb(45, 50, 65));
			SolidColorBrush stopLimitOnBg = new SolidColorBrush(Color.FromRgb(180, 90, 20)); // Dark amber accent when active
			SolidColorBrush emaPlaceOnBg  = new SolidColorBrush(Color.FromRgb(12, 35, 75));  // Very dark blue

			btnStopLimit = CreateButton(cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF",
				cachedIsStopLimit ? stopLimitOnBg : toggleOffBg, null, 24, 10);
			btnStopLimit.Foreground = cachedIsStopLimit ? Brushes.White : Brushes.LightGray;
			btnStopLimit.Click += (s, ev) =>
			{
				cachedIsStopLimit = !cachedIsStopLimit;
				StopLimitEnabled = cachedIsStopLimit;
				btnStopLimit.Content = cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF";
				btnStopLimit.Background = cachedIsStopLimit ? stopLimitOnBg : toggleOffBg;
				btnStopLimit.Foreground = cachedIsStopLimit ? Brushes.White : Brushes.LightGray;
				try { UpdateTradingProfileButtons(); } catch {}
			};
			Grid.SetColumn(btnStopLimit, 0);
			modeToggleGrid.Children.Add(btnStopLimit);

			btnEmaPlace = CreateButton(cachedIsEmaPlace ? "Ema protect: ON" : "Ema protect: OFF",
				cachedIsEmaPlace ? emaPlaceOnBg : toggleOffBg, null, 24, 10);
			btnEmaPlace.Foreground = cachedIsEmaPlace ? Brushes.White : Brushes.LightGray;
			btnEmaPlace.Click += (s, ev) =>
			{
				cachedIsEmaPlace = !cachedIsEmaPlace;
				EmaProtectEnabled = cachedIsEmaPlace;
				btnEmaPlace.Content = cachedIsEmaPlace ? "Ema protect: ON" : "Ema protect: OFF";
				btnEmaPlace.Background = cachedIsEmaPlace ? emaPlaceOnBg : toggleOffBg;
				btnEmaPlace.Foreground = cachedIsEmaPlace ? Brushes.White : Brushes.LightGray;
				try { UpdateTradingProfileButtons(); } catch {}
			};
			Grid.SetColumn(btnEmaPlace, 2);
			modeToggleGrid.Children.Add(btnEmaPlace);

			sec4Panel.Children.Add(modeToggleGrid);

			// Daily Max DD + Daily Max Profit side-by-side
			Grid dailyRiskGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
			dailyRiskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			dailyRiskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			dailyRiskGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush dailyOnBg = new SolidColorBrush(Color.FromRgb(58, 19, 107)); // Darker purple (#3A136B)

			Button btnDailyMaxDD = CreateButton(cachedIsDailyMaxDD ? "Max DD: ON" : "Max DD: OFF",
				cachedIsDailyMaxDD ? dailyOnBg : toggleOffBg, null, 24, 10);
			btnDailyMaxDD.Foreground = cachedIsDailyMaxDD ? Brushes.White : Brushes.LightGray;

			btnDailyMaxDD.Click += (s, ev) =>
			{
				cachedIsDailyMaxDD = !cachedIsDailyMaxDD;
				// Persist to the NinjaScript property — a script refresh/reload re-reads the property,
				// so a volatile-only OFF was silently re-enabled and could flatten on the next breach.
				DailyMaxDDEnabled = cachedIsDailyMaxDD;
				btnDailyMaxDD.Content = cachedIsDailyMaxDD ? "Max DD: ON" : "Max DD: OFF";
				btnDailyMaxDD.Background = cachedIsDailyMaxDD ? dailyOnBg : toggleOffBg;
				btnDailyMaxDD.Foreground = cachedIsDailyMaxDD ? Brushes.White : Brushes.LightGray;

				// Instant effect on HUD click (Requirement 4)
				EvaluateDailyRiskLimits();
				try { UpdateTradingProfileButtons(); } catch {}
			};
			Grid.SetColumn(btnDailyMaxDD, 0);
			dailyRiskGrid.Children.Add(btnDailyMaxDD);

			Button btnDailyMaxProfit = CreateButton(cachedIsDailyMaxProfit ? "Max Profit: ON" : "Max Profit: OFF",
				cachedIsDailyMaxProfit ? dailyOnBg : toggleOffBg, null, 24, 10);
			btnDailyMaxProfit.Foreground = cachedIsDailyMaxProfit ? Brushes.White : Brushes.LightGray;

			btnDailyMaxProfit.Click += (s, ev) =>
			{
				cachedIsDailyMaxProfit = !cachedIsDailyMaxProfit;
				DailyMaxProfitEnabled = cachedIsDailyMaxProfit; // persist — survives script refresh/reload
				btnDailyMaxProfit.Content = cachedIsDailyMaxProfit ? "Max Profit: ON" : "Max Profit: OFF";
				btnDailyMaxProfit.Background = cachedIsDailyMaxProfit ? dailyOnBg : toggleOffBg;
				btnDailyMaxProfit.Foreground = cachedIsDailyMaxProfit ? Brushes.White : Brushes.LightGray;

				// Instant effect on HUD click (Requirement 4)
				EvaluateDailyRiskLimits();
				try { UpdateTradingProfileButtons(); } catch {}
			};
			Grid.SetColumn(btnDailyMaxProfit, 2);
			dailyRiskGrid.Children.Add(btnDailyMaxProfit);

			sec4Panel.Children.Add(dailyRiskGrid);

			// Daily Risk Quick Set buttons: values only; enabled states stay unchanged.
			dailyRiskPresetButtons = new Button[6];
			Grid dailyRiskPresetGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
			for (int i = 0; i < 6; i++)
			{
				dailyRiskPresetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				if (i < 5)
					dailyRiskPresetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
			}

			for (int i = 0; i < 6; i++)
			{
				int presetIdx = i;
				Button presetButton = CreateButton(GetDailyRiskPresetName(presetIdx), dailyRiskPresetOffBg, null, 22, 9);
				presetButton.Foreground = Brushes.LightGray;
				presetButton.Click += (s, ev) => ApplyDailyRiskPreset(presetIdx);
				Grid.SetColumn(presetButton, presetIdx * 2);
				dailyRiskPresetButtons[presetIdx] = presetButton;
				dailyRiskPresetGrid.Children.Add(presetButton);
			}
			sec4Panel.Children.Add(dailyRiskPresetGrid);
			UpdateDailyRiskPresetButtons();

			mainPanel.Children.Add(CreateSectionCard(sec4Panel, 6));

			// --- SECTION 5: Discipline Protects (bottom) ---
			StackPanel sec5Panel = new StackPanel();
			disciplineButtons = new Button[6];
			string[] discLabels = new[] { "Fix size", "No SL-pull", "No loss-DCA", "No TP-early", "StopWhenLoss", "TradingWindows" };
			bool[] discStates = new[] { cachedSizingProtect, cachedSlPullProtect, cachedLossDcaProtect, cachedTpEarlyProtect, cachedLossTimesProtect, cachedTimingProtect };

			// Row 0: ON ALL / OFF ALL (full width controls for all bottom protects)
			Grid allToggleGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
			allToggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			allToggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			allToggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			btnDisciplineOnAll = CreateButton("Discipline All", onAllBg, null, 26, 11);
			btnDisciplineOnAll.Foreground = Brushes.White;
			btnDisciplineOnAll.FontWeight = FontWeights.Bold;
			btnDisciplineOnAll.BorderBrush = new SolidColorBrush(Color.FromRgb(75, 30, 110));
			btnDisciplineOnAll.BorderThickness = new Thickness(1);
			btnDisciplineOnAll.Click += (s, ev) => SetAllDiscipline(true);
			Grid.SetColumn(btnDisciplineOnAll, 0);
			allToggleGrid.Children.Add(btnDisciplineOnAll);
			btnDisciplineOffAll = CreateButton("Un-Discipline", offAllBg, null, 26, 11);
			btnDisciplineOffAll.Foreground = Brushes.White;
			btnDisciplineOffAll.FontWeight = FontWeights.Bold;
			btnDisciplineOffAll.BorderBrush = new SolidColorBrush(Color.FromRgb(75, 30, 110));
			btnDisciplineOffAll.BorderThickness = new Thickness(1);
			btnDisciplineOffAll.Click += (s, ev) => SetAllDiscipline(false);
			Grid.SetColumn(btnDisciplineOffAll, 2);
			allToggleGrid.Children.Add(btnDisciplineOffAll);
			sec5Panel.Children.Add(allToggleGrid);

			// 3 rows x 2 cols for 6 protects — same shade per row (2 buttons/row share color)
			for (int row = 0; row < 3; row++)
			{
				Grid rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
				rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				for (int col = 0; col < 2; col++)
				{
					int idx = row * 2 + col;
					bool isOn = discStates[idx];
					Button discBtn = CreateButton(isOn ? discLabels[idx] : discLabels[idx] + ": OFF", isOn ? disciplineRowBgs[row] : disciplineOffBg, null, 24, 10);
					discBtn.Foreground = isOn ? Brushes.White : Brushes.LightGray;
					int capturedIdx = idx;
					discBtn.Click += (s, ev) => ToggleDiscipline(capturedIdx);
					disciplineButtons[idx] = discBtn;
					Grid.SetColumn(discBtn, col == 0 ? 0 : 2);
					rowGrid.Children.Add(discBtn);
				}
				sec5Panel.Children.Add(rowGrid);
			}

			mainPanel.Children.Add(CreateSectionCard(sec5Panel, 0));

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
			ShowHudStatus(message, foreground, false);
		}

		private void ShowHudStatus(string message, Brush foreground, bool isPersistent)
		{
			if (ChartControl == null || ChartControl.Dispatcher == null) return;

			Action update = () =>
			{
				if (hudStatusText == null)
				{
					pendingHudStatusMessage = message;
					pendingHudStatusBrush = foreground;
					pendingHudStatusMessageIsPersistent = isPersistent;
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
							// don't clear persistent LossTimes lock — watchdog re-asserts it, but flicker
							// if timer fires while lock active, keep it visible
							DisciplineState stChk = GetCurrentDisciplineState();
							bool stillLocked = false;
							try
							{
								lock (disciplineLock) { stillLocked = cachedLossTimesProtect && KatTradeCalculator.IsLossTimesLockActive(stChk.LockUntilUtc, DateTime.UtcNow); }
							}
							catch {}
							if (stillLocked)
							{
								EvaluateDisciplineLockVisual();
								return;
							}
							hudStatusText.Text = string.Empty;
							hudStatusText.Foreground = Brushes.White;
						}
						hudStatusTimer.Stop();
					};
				}

				hudStatusTimer.Stop();
				if (!isPersistent)
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
			tradingProfileButtons = null;
			accSelector = null;
			btnStopLimit = null;
			btnEmaPlace = null;
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
