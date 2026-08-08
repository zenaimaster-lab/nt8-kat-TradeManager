/* KatTradeManagerUI.cs - WPF UI partial class for KatTradeManager v1.97 (2026-08-08) */
// ponytail: many catch{} for UI button updates are expected (control not yet created, dispatcher not ready) — silent. Critical watchdog tick already logs.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
		private readonly SolidColorBrush atmSetOffBg = new SolidColorBrush(Color.FromArgb(128, 45, 50, 65)); // gray OFF 50% transparent per request (dim)
		private readonly SolidColorBrush atmSetOnBg = new SolidColorBrush(Color.FromArgb(51, 180, 90, 20)); // amber ON — 80% transparent (alpha 51) per request very faint
		private Button[] dailyRiskPresetButtons;
		private readonly SolidColorBrush dailyRiskPresetOffBg = new SolidColorBrush(Color.FromArgb(128, 45, 50, 65)); // gray OFF 50% transparent per request (dim)
		private readonly SolidColorBrush dailyRiskPresetOnBg = new SolidColorBrush(Color.FromArgb(51, 36, 7, 72)); // 80% transparent ON per request
		private static ControlTemplate _quickSetButtonTemplate;
		private Button[] disciplineButtons;
		private Button btnDisciplineAll;
		private readonly SolidColorBrush disciplineOffBg = new SolidColorBrush(Color.FromRgb(45, 50, 65));
		private StackPanel disciplineAllOnPanel;
		private TextBlock disciplineAllOffTextBlock;
		// ponytail: shift controls reuse same dark bg — single static to avoid 3 allocs per rebuild
		private static readonly SolidColorBrush shiftControlBg = CreateFrozenBrush(Color.FromRgb(20, 20, 20));
		private static readonly SolidColorBrush toggleOffBgStatic = CreateFrozenBrush(Color.FromRgb(45, 50, 65));
		private static readonly SolidColorBrush stopLimitOnBgStatic = CreateFrozenBrush(Color.FromRgb(18, 6, 48));
		private static readonly SolidColorBrush emaOnBgStatic = CreateFrozenBrush(Color.FromRgb(12, 35, 75));
		private static readonly SolidColorBrush stopLimitOnFgStatic = CreateFrozenBrush(Color.FromArgb(128, 255, 255, 255));
		private static readonly SolidColorBrush disciplinePurpleBorderStatic = CreateFrozenBrush(Color.FromRgb(75, 30, 110));
		private static readonly SolidColorBrush goldBorderBrushStatic = CreateFrozenBrush(Color.FromRgb(255, 215, 0));
		private static readonly SolidColorBrush blazeOrangeBrushStatic = CreateFrozenBrush(Color.FromRgb(255, 140, 0));
		private static SolidColorBrush CreateFrozenBrush(Color c) { var b = new SolidColorBrush(c); if (b.CanFreeze) b.Freeze(); return b; }
		// Trading profiles — 8 buttons in 2 rows x4 above account selector, row-based ON colors, height 22 same as ATM row
		private Button[] tradingProfileButtons;

		// Quick-set label styling — smaller font + 50% transparent color for ATM/DailyRisk/Profile buttons only
		private double GetQuickSetFontSize()
		{
			double sz = QuickSetFontSize;
			if (sz < 6) sz = 6;
			if (sz > 14) sz = 14;
			if (sz <= 0) sz = 8;
			return sz;
		}
		private static Brush BuildLabelBrush(Brush src, int pct, int defaultPct, byte fallbackAlpha)
		{
			try
			{
				Brush baseBrush = src ?? Brushes.White;
				Color baseColor = Colors.White;
				if (baseBrush is SolidColorBrush scb) baseColor = scb.Color;
				if (pct == 0) pct = defaultPct;
				if (pct < 10) pct = 10;
				if (pct > 100) pct = 100;
				byte alpha = (byte)(pct * 255 / 100);
				Color c = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
				var nb = new SolidColorBrush(c);
				if (nb.CanFreeze) nb.Freeze();
				return nb;
			}
			catch { var fb = new SolidColorBrush(Color.FromArgb(fallbackAlpha, 255, 255, 255)); if (fb.CanFreeze) fb.Freeze(); return fb; }
		}
		private Brush GetQuickSetLabelBrush() => BuildLabelBrush(QuickSetLabelColor, QuickSetLabelOpacityPercent, 50, 128);
		private Brush GetProgramLabelBrush() => BuildLabelBrush(ProgramLabelColor, ProgramLabelOpacityPercent, 20, 51);
		private Brush GetSmallQuickSetLabelBrush() => BuildLabelBrush(QuickSetLabelColor, 100, 100, 255);
		private void SetButtonLabel(Button btn, string text)
		{
			if (btn == null) return;
			if (btn.Content is TextBlock tb)
			{
				if (tb.Text != text) tb.Text = text;
				tb.TextAlignment = TextAlignment.Center;
				tb.HorizontalAlignment = HorizontalAlignment.Center;
				tb.VerticalAlignment = VerticalAlignment.Center;
				tb.TextTrimming = TextTrimming.CharacterEllipsis;
				tb.TextWrapping = TextWrapping.NoWrap;
				// triệt để: bypass Foreground/FontSize inheritance via template — explicit sync (Program pattern reference)
				try { if (btn.Foreground != null) tb.Foreground = btn.Foreground; } catch {}
				try { if (btn.FontSize > 0) tb.FontSize = btn.FontSize; } catch {}
				tb.Margin = new Thickness(0);
				tb.Padding = new Thickness(0);
			}
			else if (btn.Content is StackPanel)
			{
				// DISCIPLINED blaze panel — do not overwrite
				return;
			}
			else
			{
				var nTb = new TextBlock { Text = text, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap, Margin = new Thickness(0), Padding = new Thickness(0) };
				try { if (btn.Foreground != null) nTb.Foreground = btn.Foreground; } catch {}
				try { if (btn.FontSize > 0) nTb.FontSize = btn.FontSize; } catch {}
				btn.Content = nTb;
			}
			btn.HorizontalContentAlignment = HorizontalAlignment.Center;
			btn.VerticalContentAlignment = VerticalAlignment.Center;
			btn.Padding = new Thickness(2, 0, 2, 0);
		}
		private string GetButtonLabel(Button btn)
		{
			if (btn == null) return null;
			if (btn.Content is TextBlock tb) return tb.Text;
			return btn.Content as string;
		}
		private ComboBox accSelector;
		private Button btnStopLimit;
		private Button btnEmaPlace;
		private volatile int activeTradingProfile = -1; // last applied profile index, -1 = none
		private DateTime lastProfileApplyUtc = DateTime.MinValue;
		private string pendingProfileAccount;
		private DateTime pendingProfileAccountSinceUtc;
		private readonly SolidColorBrush profileOffBg = new SolidColorBrush(Color.FromArgb(128, 45, 50, 65)); // gray OFF 50% transparent per request (dim)
		private readonly SolidColorBrush[] profileRowOnBgs = new SolidColorBrush[]
		{
			new SolidColorBrush(Color.FromRgb(20, 110, 110)), // odd P1,P3,P5,P7 — cyan/teal (ex Row0)
			new SolidColorBrush(Color.FromRgb(135, 35, 65)),  // even P2,P4,P6,P8 — rose/pink (ex Row1)
		};
		// Row-based ON colors: 2 buttons per row share same shade (3 rows)
		private readonly SolidColorBrush[] disciplineRowBgs = new SolidColorBrush[]
		{
			new SolidColorBrush(Color.FromRgb(22, 60, 92)),   // Row0: Fix size + No SL-pull
			new SolidColorBrush(Color.FromRgb(32, 88, 138)),  // Row1: No loss-DCA + No TP-early
			new SolidColorBrush(Color.FromRgb(48, 120, 180)), // Row2: StopWhenLoss + TradingWindows
		};
		// Discipline All toggle — ON = dark blue (same as EmaZone ON), OFF = dark purple
		private readonly SolidColorBrush disciplineAllOnBg = new SolidColorBrush(Color.FromRgb(12, 35, 75)); // DISCIPLINED - dark blue
		private readonly SolidColorBrush disciplineAllOffBg = new SolidColorBrush(Color.FromRgb(55, 20, 85)); // UN-DISCIPLINED - dark purple
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
		private const double HudGap = 2; // uniform gap — horizontal, vertical, inner/outer — matches quick-set intra-column gap
		private const double HudPanelWidth = 250; // 250 outer => 238 inner (250-6-6) = 22+24k perfect for gap2 across 2/4/6/8 cols

		// ponytail: AccountInfo extracted to KatTradeManager.AccountInfo.cs

		// ponytail: unified via KatAtmTemplateService (single 5s listing)
		private List<string> GetCachedAtmTemplateNames() => KatAtmTemplateService.GetNames();

		// ponytail: AccountInfo extracted to KatTradeManager.AccountInfo.cs

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
				try { UpdateDisciplineFromPosition(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateDisciplineFromPosition: {0}", ex.Message)); }
				try { EvaluateDisciplineLockVisual(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog EvaluateDisciplineLockVisual: {0}", ex.Message)); }
				try { UpdateTradingProfileButtons(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateTradingProfileButtons: {0}", ex.Message)); }
				try { UpdateAtmSetButtons(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateAtmSetButtons: {0}", ex.Message)); }
				try { UpdateDailyRiskPresetButtons(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateDailyRiskPresetButtons: {0}", ex.Message)); }
				try { UpdateStopLimitButton(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateStopLimitButton: {0}", ex.Message)); }
				try { UpdateEmaPlaceButton(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateEmaPlaceButton: {0}", ex.Message)); }
				for (int _di = 0; _di < 6; _di++) try { UpdateDisciplineButton(_di); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateDisciplineButton {0}: {1}", _di, ex.Message)); }
				try { UpdateDisciplineAllButton(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateDisciplineAllButton: {0}", ex.Message)); }
				try { UpdateAccountInfoSection(); } catch (Exception ex) { Print(string.Format("[KatTradeManager] Watchdog UpdateAccountInfoSection: {0}", ex.Message)); }

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

		// ponytail: HUD quicksets/profile/discipline + ChartTrader helpers extracted to KatTradeManager.HudUpdates.cs (340-980)

		// ponytail: HUD builder (CreateWpfControls + visual helpers) extracted to KatTradeManager.HudBuilder.cs (981-1737)

		// ponytail: CreateButton/Grid/Card/Templates extracted to src/KatTradeManager.HudFactory.cs — keep UI.cs focused on wiring

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
			atmSetButtons = null;
			dailyRiskPresetButtons = null;
			disciplineButtons = null;
			accSelector = null;
			btnStopLimit = null;
			btnEmaPlace = null;
			btnDisciplineAll = null;
			accountInfoCard = null;
			accountInfoDateTimeText = null;
			accountDateRun = null;
			accountTimeHmRun = null;
			accountTimeSRun = null;
			accountAmPmRun = null;
			accountNytRun = null;
			accountBalanceText = null;
			accountBalanceLabelRun = null;
			accountBalanceValueRun = null;
			accountUnrealText = null;
			accountRealText = null;
			accountUnrealLabelRun = null;
			accountUnrealValueRun = null;
			accountRealLabelRun = null;
			accountRealValueRun = null;
			hudHeaderText = null;
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
