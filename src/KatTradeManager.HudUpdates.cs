/* KatTradeManager.HudUpdates.cs - HUD quicksets/profile/toggle updates (partial class) v1.98 (2026-08-08) */
// ponytail: extracted from KatTradeManagerUI.cs 349-884 — AtmSets/DailyRisk/Profiles/Discipline update+apply. UI god 1803→~1100L.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
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
				case 5: return AtmSet6Atm;
				case 6: return AtmSet7Atm;
				default: return AtmSet8Atm;
			}
		}

		private string GetAtmSetName(int idx)
		{
			switch (idx)
			{
				case 0: return string.IsNullOrWhiteSpace(AtmSet1Name) ? "A" : AtmSet1Name;
				case 1: return string.IsNullOrWhiteSpace(AtmSet2Name) ? "B" : AtmSet2Name;
				case 2: return string.IsNullOrWhiteSpace(AtmSet3Name) ? "C" : AtmSet3Name;
				case 3: return string.IsNullOrWhiteSpace(AtmSet4Name) ? "D" : AtmSet4Name;
				case 4: return string.IsNullOrWhiteSpace(AtmSet5Name) ? "E" : AtmSet5Name;
				case 5: return string.IsNullOrWhiteSpace(AtmSet6Name) ? "F" : AtmSet6Name;
				case 6: return string.IsNullOrWhiteSpace(AtmSet7Name) ? "G" : AtmSet7Name;
				default: return string.IsNullOrWhiteSpace(AtmSet8Name) ? "H" : AtmSet8Name;
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
		// triệt để: mirror Program's UpdateTradingProfileButtons — explicit TextBlock sync every tick (bypass template inheritance)
		private void UpdateAtmSetButtons()
		{
			if (atmSetButtons == null) return;
			double fs = GetQuickSetFontSize();
			double fsUse = Math.Min(14, fs + 2);
			for (int i = 0; i < atmSetButtons.Length; i++)
			{
				if (atmSetButtons[i] == null) continue;
				string tpl = GetAtmSetTemplate(i);
				bool on = !string.IsNullOrEmpty(cachedAtmTemplate)
					&& !string.IsNullOrEmpty(tpl)
					&& tpl.Equals(cachedAtmTemplate, StringComparison.OrdinalIgnoreCase);
				atmSetButtons[i].Background = on ? atmSetOnBg : atmSetOffBg;
				atmSetButtons[i].Foreground = Brushes.White;
				atmSetButtons[i].FontSize = fsUse;
				atmSetButtons[i].FontWeight = FontWeights.SemiBold;
				atmSetButtons[i].HorizontalContentAlignment = HorizontalAlignment.Center;
				atmSetButtons[i].VerticalContentAlignment = VerticalAlignment.Center;
				atmSetButtons[i].Padding = new Thickness(1, 0, 1, 0);
				atmSetButtons[i].BorderThickness = new Thickness(0);
				string expected = GetAtmSetName(i);
				// triệt để: always set string Content — bypass TextBlock/template inherit bug; default template renders string reliably
				try
				{
					if (!string.Equals(atmSetButtons[i].Content as string, expected, StringComparison.Ordinal))
						atmSetButtons[i].Content = expected;
					// if still TextBlock from old template, replace with string
					if (atmSetButtons[i].Content is TextBlock)
						atmSetButtons[i].Content = expected;
				} catch { atmSetButtons[i].Content = expected; }
			}
		}

		private string GetDailyRiskPresetName(int idx)
		{
			switch (idx)
			{
				case 0: return string.IsNullOrWhiteSpace(DailyRiskSet1Name) ? "1" : DailyRiskSet1Name;
				case 1: return string.IsNullOrWhiteSpace(DailyRiskSet2Name) ? "2" : DailyRiskSet2Name;
				case 2: return string.IsNullOrWhiteSpace(DailyRiskSet3Name) ? "3" : DailyRiskSet3Name;
				case 3: return string.IsNullOrWhiteSpace(DailyRiskSet4Name) ? "4" : DailyRiskSet4Name;
				case 4: return string.IsNullOrWhiteSpace(DailyRiskSet5Name) ? "5" : DailyRiskSet5Name;
				default: return string.IsNullOrWhiteSpace(DailyRiskSet6Name) ? "6" : DailyRiskSet6Name;
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
			double fs = GetQuickSetFontSize();
			double fsUse = Math.Min(14, fs + 2);
			for (int i = 0; i < dailyRiskPresetButtons.Length; i++)
			{
				if (dailyRiskPresetButtons[i] == null) continue;
				bool on = DailyMaxDD == GetDailyRiskPresetMaxDD(i)
					&& DailyMaxProfit == GetDailyRiskPresetMaxProfit(i);
				dailyRiskPresetButtons[i].Background = on ? dailyRiskPresetOnBg : dailyRiskPresetOffBg;
				dailyRiskPresetButtons[i].Foreground = Brushes.White;
				dailyRiskPresetButtons[i].FontSize = fsUse;
				dailyRiskPresetButtons[i].FontWeight = FontWeights.SemiBold;
				dailyRiskPresetButtons[i].HorizontalContentAlignment = HorizontalAlignment.Center;
				dailyRiskPresetButtons[i].VerticalContentAlignment = VerticalAlignment.Center;
				dailyRiskPresetButtons[i].Padding = new Thickness(1, 0, 1, 0);
				dailyRiskPresetButtons[i].BorderThickness = new Thickness(0);
				string expected = GetDailyRiskPresetName(i);
				try
				{
					if (!string.Equals(dailyRiskPresetButtons[i].Content as string, expected, StringComparison.Ordinal))
						dailyRiskPresetButtons[i].Content = expected;
					if (dailyRiskPresetButtons[i].Content is TextBlock)
						dailyRiskPresetButtons[i].Content = expected;
				} catch { dailyRiskPresetButtons[i].Content = expected; }
			}
		}

		// ponytail: profile helpers extracted to src/KatTradeManager.ProfileOps.cs — keeps UI file focused on rendering only
		private void UpdateTradingProfileButtons()
		{
			if (tradingProfileButtons == null) return;
			// highlight the single profile that matches live config (covers both post-restart and manual edit to match other profile)
			int uniqueMatch = -1;
			{
				int matches = 0;
				for (int j = 0; j < 8; j++) if (IsTradingProfileActive(j)) { matches++; uniqueMatch = j; }
				if (matches != 1) uniqueMatch = -1;
			}
			Brush labelBrush = GetProgramLabelBrush(); // 80% transparent default via Program setting
			double fs = GetQuickSetFontSize();
			double fsProg = Math.Min(14, fs + 2); // Program larger than quick-set base per request
			for (int i = 0; i < tradingProfileButtons.Length; i++)
			{
				if (tradingProfileButtons[i] == null) continue;
				bool on = (uniqueMatch != -1 && i == uniqueMatch) || (uniqueMatch == -1 && activeTradingProfile == i && IsTradingProfileActive(i));
				if (on)
				{
					int parity = i % 2; // 0: P1,P3,P5,P7 cyan, 1: P2,P4,P6,P8 pink
					tradingProfileButtons[i].Background = profileRowOnBgs[parity];
					tradingProfileButtons[i].Foreground = labelBrush;
				}
				else
				{
					tradingProfileButtons[i].Background = profileOffBg;
					tradingProfileButtons[i].Foreground = labelBrush;
				}
				tradingProfileButtons[i].FontSize = fsProg;
				// Program buttons: label left-aligned + slightly inset from left edge per request — 50% transparent
				string expected = GetTradingProfileName(i);
				if (GetButtonLabel(tradingProfileButtons[i]) != expected)
					SetButtonLabel(tradingProfileButtons[i], expected);
				tradingProfileButtons[i].HorizontalContentAlignment = HorizontalAlignment.Left;
				tradingProfileButtons[i].Padding = new Thickness(4, 0, 2, 0);
				if (tradingProfileButtons[i].Content is TextBlock _tbU) { _tbU.TextAlignment = TextAlignment.Left; _tbU.HorizontalAlignment = HorizontalAlignment.Left; _tbU.Margin = new Thickness(4, 0, 0, 0); _tbU.FontSize = fsProg; _tbU.Foreground = labelBrush; _tbU.Opacity = 1; }
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
			SetButtonLabel(btnStopLimit, cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF");
			btnStopLimit.Background = cachedIsStopLimit ? stopLimitOnBgStatic : toggleOffBgStatic;
			btnStopLimit.Foreground = cachedIsStopLimit ? stopLimitOnFgStatic : Brushes.LightGray;
		}

		private void UpdateEmaPlaceButton()
		{
			if (btnEmaPlace == null) return;
			SetButtonLabel(btnEmaPlace, "EmaZoneOnly");
			btnEmaPlace.Background = cachedIsEmaPlace ? emaOnBgStatic : toggleOffBgStatic;
			btnEmaPlace.Foreground = cachedIsEmaPlace ? Brushes.White : Brushes.LightGray;
			// ON: no bright purple border
			if (cachedIsEmaPlace)
			{
				btnEmaPlace.BorderThickness = new Thickness(0);
				btnEmaPlace.BorderBrush = Brushes.Transparent;
			}
			else
			{
				btnEmaPlace.BorderBrush = disciplinePurpleBorderStatic;
				btnEmaPlace.BorderThickness = new Thickness(1);
			}
		}

		private bool IsDisciplineAllOn()
		{
			return cachedIsEmaPlace && cachedSizingProtect && cachedSlPullProtect && cachedLossDcaProtect && cachedTpEarlyProtect && cachedLossTimesProtect && cachedTimingProtect;
		}

		private void UpdateDisciplineAllButton()
		{
			if (btnDisciplineAll == null) return;
			bool allOn = IsDisciplineAllOn();
			if (allOn)
			{
				if (disciplineAllOnPanel == null)
				{
					disciplineAllOnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
					TextBlock icon = new TextBlock { Text = "⚡", Foreground = blazeOrangeBrushStatic, FontSize = 11, Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
					TextBlock label = new TextBlock { Text = "DISCIPLINED", Foreground = Brushes.White, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
					disciplineAllOnPanel.Children.Add(icon);
					disciplineAllOnPanel.Children.Add(label);
				}
				btnDisciplineAll.Content = disciplineAllOnPanel;
				btnDisciplineAll.Background = disciplineAllOnBg;
				btnDisciplineAll.Foreground = Brushes.White;
				btnDisciplineAll.BorderBrush = goldBorderBrushStatic;
				btnDisciplineAll.BorderThickness = new Thickness(1);
			}
			else
			{
				// ponytail: bypass SetButtonLabel StackPanel guard — must overwrite blaze panel when switching OFF
				if (disciplineAllOffTextBlock == null)
				{
					disciplineAllOffTextBlock = new TextBlock { Text = "UN-DISCIPLINED", TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
				}
				btnDisciplineAll.Content = disciplineAllOffTextBlock;
				btnDisciplineAll.HorizontalContentAlignment = HorizontalAlignment.Center;
				btnDisciplineAll.VerticalContentAlignment = VerticalAlignment.Center;
				btnDisciplineAll.Padding = new Thickness(2, 0, 2, 0);
				btnDisciplineAll.Background = disciplineAllOffBg;
				btnDisciplineAll.Foreground = Brushes.White;
				btnDisciplineAll.BorderBrush = disciplinePurpleBorderStatic;
				btnDisciplineAll.BorderThickness = new Thickness(1);
			}
		}

		private void ApplyTradingProfile(int idx)
		{
			if (idx < 0 || idx >= 8) return;
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
			try { UpdateDisciplineAllButton(); } catch {}
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
			try { UpdateDisciplineAllButton(); } catch {}
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
			SetButtonLabel(btn, isOn ? labels[idx] : labels[idx] + ": OFF");
			int row = idx / 2;
			btn.Background = isOn ? disciplineRowBgs[row] : disciplineOffBg;
			btn.Foreground = isOn ? Brushes.White : Brushes.LightGray;
		}

		private void SetAllDiscipline(bool isOn)
		{
			cachedIsEmaPlace = isOn; EmaProtectEnabled = isOn;
			cachedSizingProtect = isOn; SizingProtectEnabled = isOn;
			cachedSlPullProtect = isOn; SlPullProtectEnabled = isOn;
			cachedLossDcaProtect = isOn; LossDcaProtectEnabled = isOn;
			cachedTpEarlyProtect = isOn; TpEarlyProtectEnabled = isOn;
			cachedLossTimesProtect = isOn; LossTimesProtectEnabled = isOn;
			cachedTimingProtect = isOn; TimingWindowsProtectEnabled = isOn;
			for (int i = 0; i < 6; i++) UpdateDisciplineButton(i);
			try { UpdateEmaPlaceButton(); } catch {}
			try { UpdateDisciplineAllButton(); } catch {}
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
	}
}
