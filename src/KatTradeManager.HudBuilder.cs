 /* KatTradeManager.HudBuilder.cs - HUD builder + visual tree helpers (partial class) v1.98 (2026-08-08) */
// ponytail: extracted from KatTradeManagerUI.cs 981-1737 — IsPanelAttached + CreateWpfControls + visual helpers. UI god 1803->~700L.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Chart;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
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

		// ponytail: HUD drag extracted to KatTradeManager.HudDrag.cs (partial class)

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
				Padding = new Thickness(HudGap),
				Margin = new Thickness(HudGap),
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};

			bool isChartTraderAttached = false;

			if (PanelLocation == KatHudLocation.ChartTrader)
			{
				var ctControl = GetChartTraderControl();
				var ctPanel = ctControl != null ? FindChartTraderPanel(ctControl) : null;

				if (ctPanel != null)
				{
					panelBorder.Width = HudPanelWidth;
					panelBorder.HorizontalAlignment = HorizontalAlignment.Left;
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
				panelBorder.Width = HudPanelWidth;
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
			mainPanel = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };

			// HUD title at very top, then account info black board immediately below title — double breathing space, 70% transparent sunk
			hudHeaderText = new TextBlock
			{
				Text = string.Format("⚡ KAT TradeManager v{0}", VERSION),
				Foreground = new SolidColorBrush(Color.FromRgb(70, 130, 160)),
				FontWeight = FontWeights.Normal,
				FontSize = 12,
				Margin = new Thickness(0, HudGap * 2, 0, HudGap * 2),
				HorizontalAlignment = HorizontalAlignment.Left,
				Opacity = 0.3
			};
			mainPanel.Children.Add(hudHeaderText);
			mainPanel.Children.Add(CreateAccountInfoSection());

			// --- SECTION 1: Parameters & ATM Selection ---
			StackPanel sec1Panel = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };

			hudStatusText = new TextBlock
			{
				Background = Brushes.Transparent,
				Foreground = Brushes.White,
				FontSize = 10,
				Margin = new Thickness(0, 0, 0, HudGap),
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

			// --- Trading Profile quick presets (8 buttons, 2 rows x 4 cols, above account, align left) ---
			tradingProfileButtons = new Button[8];
			for (int prow = 0; prow < 2; prow++)
			{
				Grid rowGrid = CreateFourColumnGrid(HudGap, HudGap, HudGap);
				for (int cc = 0; cc < 4; cc++)
				{
					int idx = cc * 2 + prow; // pairs 1,2 | 3,4 | 5,6 | 7,8 → row0:1,3,5,7 row1:2,4,6,8 per request
					double _fsProg = Math.Min(14, GetQuickSetFontSize() + 2);
					Brush progBrush = GetProgramLabelBrush(); // 80% transparent default per new setting
					Button pBtn = CreateButton("", profileOffBg, null, 22, _fsProg);
					pBtn.Foreground = progBrush;
					pBtn.FontSize = _fsProg;
					SetButtonLabel(pBtn, GetTradingProfileName(idx));
					pBtn.HorizontalContentAlignment = HorizontalAlignment.Left;
					pBtn.Padding = new Thickness(4, 0, 2, 0);
					if (pBtn.Content is TextBlock _pTb) { _pTb.TextAlignment = TextAlignment.Left; _pTb.HorizontalAlignment = HorizontalAlignment.Left; _pTb.Margin = new Thickness(4, 0, 0, 0); _pTb.FontSize = _fsProg; _pTb.Foreground = progBrush; _pTb.Opacity = 1; }
					int captured = idx;
					pBtn.Click += (s, ev) => ApplyTradingProfile(captured);
					Grid.SetColumn(pBtn, cc * 2);
					tradingProfileButtons[idx] = pBtn;
					rowGrid.Children.Add(pBtn);
				}
				sec1Panel.Children.Add(rowGrid);
			}
			UpdateTradingProfileButtons();
			// update tooltips with live profile snapshot (account/ATM/DD/Profit) after buttons created
			try
			{
				for (int i = 0; i < 8; i++) if (tradingProfileButtons[i] != null)
				{
					string tAcc = GetTradingProfileAccount(i);
					string tAtm = GetTradingProfileAtm(i);
					if (IsNoAtmSelection(tAtm)) tAtm = "None";
					tradingProfileButtons[i].ToolTip = string.Format("{0}: {1} / {2}  DD {3}  TP {4}", GetTradingProfileName(i), string.IsNullOrWhiteSpace(tAcc) ? "(no acc)" : tAcc, tAtm, GetTradingProfileDailyMaxDD(i), GetTradingProfileDailyMaxProfit(i));
				}
			} catch {}

			accSelector = new ComboBox { FontSize = 11, Height = 22, Margin = new Thickness(0, 0, 0, HudGap), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(4, 0, 0, 0), UseLayoutRounding = true, SnapsToDevicePixels = true, Background = new SolidColorBrush(Color.FromRgb(0, 0, 0)), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(35, 42, 56)), BorderThickness = new Thickness(1) };
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
					try { UpdateAccountInfoSection(); } catch {}
				}
			};
			sec1Panel.Children.Add(accSelector);

			atmSelector = new ComboBox
			{
				FontSize = 11,
				Height = 22,
				Margin = new Thickness(0, 0, 0, HudGap),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			atmSelector.Items.Add(NoAtmTemplateLabel); // first item, also the fallback when no template matches
			foreach (var name in GetCachedAtmTemplateNames())
				atmSelector.Items.Add(name);
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

			// --- ATM Quick Set buttons (A–H), 8 in single row — robust quick-set template (TextBlock bound to Content)
			atmSetButtons = new Button[8];
			Grid atmSetGrid = CreateEightColumnGrid(0, HudGap, HudGap);
			for (int i = 0; i < 8; i++)
			{
				int setIdx = i;
				double fsAtmBase = GetQuickSetFontSize();
				double fsAtm = Math.Min(14, fsAtmBase + 2);
				string atmLabel = GetAtmSetName(setIdx);
				Button setBtn = CreateButton(atmLabel, atmSetOffBg, null, 22, fsAtm);
				setBtn.Template = GetQuickSetButtonTemplate();
				setBtn.Foreground = Brushes.White;
				setBtn.FontSize = fsAtm;
				setBtn.FontWeight = FontWeights.SemiBold;
				setBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
				setBtn.VerticalContentAlignment = VerticalAlignment.Center;
				setBtn.Padding = new Thickness(1, 0, 1, 0);
				setBtn.BorderThickness = new Thickness(0);
				setBtn.Content = atmLabel;
				setBtn.Click += (s, ev) => ApplyAtmSetSelection(setIdx);
				Grid.SetColumn(setBtn, setIdx * 2);
				atmSetButtons[setIdx] = setBtn;
				atmSetGrid.Children.Add(setBtn);
			}
			sec1Panel.Children.Add(atmSetGrid);
			UpdateAtmSetButtons();
			mainPanel.Children.Add(CreateSectionCard(sec1Panel, HudGap));


			// --- SECTION 2: EMA 34 & EMA 89 Touch/Cross Orders ---
			StackPanel sec2Panel = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };

			SolidColorBrush buy34Bg  = new SolidColorBrush(Color.FromRgb(20, 60, 75)); // much darker cyan vs Buy 89
			SolidColorBrush sell34Bg = new SolidColorBrush(Color.FromRgb(85, 25, 65)); // much darker pink vs Sell 89
			SolidColorBrush buy89Bg  = new SolidColorBrush(Color.FromRgb(35, 95, 110));
			SolidColorBrush sell89Bg = new SolidColorBrush(Color.FromRgb(130, 35, 95));
			SolidColorBrush entryShiftBg = shiftControlBg;

			Grid entryShiftGrid = CreateTwoColumnGrid(HudGap, HudGap);

			Button btnEntryBack = CreateButton("◀ Entry 89/34", entryShiftBg, (s, ev) => ShiftEmaEntry(false), 30, 12);
			btnEntryBack.Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255));
			Grid.SetColumn(btnEntryBack, 0);
			entryShiftGrid.Children.Add(btnEntryBack);

			Button btnEntryRedo = CreateButton("Entry 89/34 ▶", entryShiftBg, (s, ev) => ShiftEmaEntry(true), 30, 12);
			btnEntryRedo.Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255));
			Grid.SetColumn(btnEntryRedo, 2);
			entryShiftGrid.Children.Add(btnEntryRedo);

			sec2Panel.Children.Add(entryShiftGrid);

			Grid candleShiftGrid = CreateTwoColumnGrid(HudGap, HudGap);

			SolidColorBrush candleShiftBg = shiftControlBg;

			Button btnCandleBack = CreateButton("◀ Entry candle", candleShiftBg, (s, ev) => ShiftCandleEntry(false), 30, 12);
			btnCandleBack.Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255));
			Grid.SetColumn(btnCandleBack, 0);
			candleShiftGrid.Children.Add(btnCandleBack);

			Button btnCandleRedo = CreateButton("Entry candle ▶", candleShiftBg, (s, ev) => ShiftCandleEntry(true), 30, 12);
			btnCandleRedo.Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255));
			Grid.SetColumn(btnCandleRedo, 2);
			candleShiftGrid.Children.Add(btnCandleRedo);

			sec2Panel.Children.Add(candleShiftGrid);

			Grid ema34Grid = CreateTwoColumnGrid(HudGap, HudGap);

			Button btnSell34 = CreateButton("Sell last 34", sell34Bg, (s, ev) => PlaceEmaOrder(OrderAction.Sell, 34), 43, 12);
			Grid.SetColumn(btnSell34, 0);
			ema34Grid.Children.Add(btnSell34);

			Button btnBuy34 = CreateButton("Buy last 34", buy34Bg, (s, ev) => PlaceEmaOrder(OrderAction.Buy, 34), 43, 12);
			Grid.SetColumn(btnBuy34, 2);
			ema34Grid.Children.Add(btnBuy34);

			sec2Panel.Children.Add(ema34Grid);

			Grid ema89Grid = CreateTwoColumnGrid(0, HudGap);

			Button btnSell89 = CreateButton("Sell last 89", sell89Bg, (s, ev) => PlaceEmaOrder(OrderAction.Sell, 89), 43, 12);
			Grid.SetColumn(btnSell89, 0);
			ema89Grid.Children.Add(btnSell89);

			Button btnBuy89 = CreateButton("Buy last 89", buy89Bg, (s, ev) => PlaceEmaOrder(OrderAction.Buy, 89), 43, 12);
			Grid.SetColumn(btnBuy89, 2);
			ema89Grid.Children.Add(btnBuy89);

			sec2Panel.Children.Add(ema89Grid);

			mainPanel.Children.Add(CreateSectionCard(sec2Panel, HudGap));


			// --- SECTION 3: Market & Candle Orders + Position Management ---
			StackPanel sec3Panel = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };

			// --- Market Orders (top of execution section) ---
			Grid mktBtnGrid = CreateTwoColumnGrid(HudGap, HudGap);

			SolidColorBrush buyMktBg  = new SolidColorBrush(Color.FromRgb(7, 30, 16)); // Deep dark green - darker to distinguish from Buy current (16,55,30)
			SolidColorBrush sellMktBg = new SolidColorBrush(Color.FromRgb(55, 15, 18)); // Deep dark red

			Button btnSellMkt = CreateButton("SELL MARKET", sellMktBg, (s, ev) => PlaceMarketOrder(OrderAction.Sell), 43, 12);
			Grid.SetColumn(btnSellMkt, 0);
			mktBtnGrid.Children.Add(btnSellMkt);

			Button btnBuyMkt = CreateButton("BUY MARKET", buyMktBg, (s, ev) => PlaceMarketOrder(OrderAction.Buy), 43, 12);
			Grid.SetColumn(btnBuyMkt, 2);
			mktBtnGrid.Children.Add(btnBuyMkt);

			sec3Panel.Children.Add(mktBtnGrid);

			SolidColorBrush buyPrevBg  = new SolidColorBrush(Color.FromRgb(34, 112, 62));
			SolidColorBrush buyCurrBg  = new SolidColorBrush(Color.FromRgb(16, 55, 30));
			SolidColorBrush sellPrevBg = new SolidColorBrush(Color.FromRgb(148, 48, 54));
			SolidColorBrush sellCurrBg = new SolidColorBrush(Color.FromRgb(75, 22, 28));

			Grid currOrderGrid = CreateTwoColumnGrid(HudGap, HudGap);
			Button btnSellCurr = CreateButton("Sell current", sellCurrBg, (s, ev) => PlaceOrder(OrderAction.Sell, true), 43, 12);
			Grid.SetColumn(btnSellCurr, 0);
			currOrderGrid.Children.Add(btnSellCurr);

			Button btnBuyCurr = CreateButton("Buy current", buyCurrBg, (s, ev) => PlaceOrder(OrderAction.Buy, true), 43, 12);
			Grid.SetColumn(btnBuyCurr, 2);
			currOrderGrid.Children.Add(btnBuyCurr);
			sec3Panel.Children.Add(currOrderGrid);

			Grid prevOrderGrid = CreateTwoColumnGrid(0, HudGap);
			Button btnSellPrev = CreateButton("Sell previous", sellPrevBg, (s, ev) => PlaceOrder(OrderAction.Sell, false), 43, 12);
			Grid.SetColumn(btnSellPrev, 0);
			prevOrderGrid.Children.Add(btnSellPrev);

			Button btnBuyPrev = CreateButton("Buy previous", buyPrevBg, (s, ev) => PlaceOrder(OrderAction.Buy, false), 43, 12);
			Grid.SetColumn(btnBuyPrev, 2);
			prevOrderGrid.Children.Add(btnBuyPrev);
			sec3Panel.Children.Add(prevOrderGrid);

			mainPanel.Children.Add(CreateSectionCard(sec3Panel, HudGap));

			// --- SECTION 3b: BE / Revert / Close (separate for clarity) ---
			StackPanel sec3bPanel = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };
			Grid swingSlGrid = CreateTwoColumnGrid(HudGap, HudGap);

			SolidColorBrush swingSlBg = shiftControlBg;

			Button btnSlBack = CreateButton("◀ SL", swingSlBg, (s, ev) => ShiftSlToSwing(false), 30, 12);
			btnSlBack.Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255));
			Grid.SetColumn(btnSlBack, 0);
			swingSlGrid.Children.Add(btnSlBack);

			Button btnSlRedo = CreateButton("SL ▶", swingSlBg, (s, ev) => ShiftSlToSwing(true), 30, 12);
			btnSlRedo.Foreground = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255));
			Grid.SetColumn(btnSlRedo, 2);
			swingSlGrid.Children.Add(btnSlRedo);

			sec3bPanel.Children.Add(swingSlGrid);

			Grid beRevertGrid = CreateTwoColumnGrid(HudGap, HudGap);

			SolidColorBrush beBg     = new SolidColorBrush(Color.FromRgb(22, 22, 22)); // very dark gray near black
			SolidColorBrush revertBg = new SolidColorBrush(Color.FromRgb(22, 22, 22)); // very dark gray near black

			Button btnBE = CreateButton("Break Even", beBg, (s, ev) => SetBreakeven(), 43, 12);
			Grid.SetColumn(btnBE, 2);
			beRevertGrid.Children.Add(btnBE);

			Button btnRevert = CreateButton("Revert", revertBg, (s, ev) => RevertPosition(), 43, 12);
			Grid.SetColumn(btnRevert, 0);
			beRevertGrid.Children.Add(btnRevert);

			sec3bPanel.Children.Add(beRevertGrid);

			SolidColorBrush closeBg = new SolidColorBrush(Color.FromRgb(10, 10, 10)); // darker than Revert/BE 22,22,22
			Button btnClose = CreateButton("Close/flatten", closeBg, (s, ev) => FlattenAllPositions(), 59, 15);
			sec3bPanel.Children.Add(btnClose);

			mainPanel.Children.Add(CreateSectionCard(sec3bPanel, HudGap));


			// --- SECTION 4: ON/OFF Toggles ---
			StackPanel sec4Panel = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };

			SolidColorBrush toggleOffBg   = new SolidColorBrush(Color.FromRgb(45, 50, 65));
			SolidColorBrush stopLimitOnBg = new SolidColorBrush(Color.FromRgb(18, 6, 48)); // extra dark purple — distinct from Max DD/Profit purple

			btnStopLimit = CreateButton(cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF",
				cachedIsStopLimit ? stopLimitOnBg : toggleOffBg, null, 24, 10);
			btnStopLimit.Foreground = cachedIsStopLimit ? new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)) : Brushes.LightGray;
			btnStopLimit.HorizontalAlignment = HorizontalAlignment.Stretch;
			btnStopLimit.Margin = new Thickness(0, 0, 0, HudGap);
			btnStopLimit.Click += (s, ev) =>
			{
				cachedIsStopLimit = !cachedIsStopLimit;
				StopLimitEnabled = cachedIsStopLimit;
				SetButtonLabel(btnStopLimit, cachedIsStopLimit ? "Stop-Limit: ON" : "Stop-Limit: OFF");
				btnStopLimit.Background = cachedIsStopLimit ? stopLimitOnBg : toggleOffBg;
				btnStopLimit.Foreground = cachedIsStopLimit ? new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)) : Brushes.LightGray;
				try { UpdateTradingProfileButtons(); } catch {}
			};
			sec4Panel.Children.Add(btnStopLimit);

			// Daily Max DD + Daily Max Profit side-by-side — share 6-col base with preset row for pixel-perfect center
			Grid dailyRiskGrid = CreateSixColumnGrid(HudGap, HudGap, HudGap);

			SolidColorBrush dailyOnBg = new SolidColorBrush(Color.FromRgb(58, 19, 107)); // opaque — quick-set ON is transparent, toggle stays solid per correction

			Button btnDailyMaxDD = CreateButton(cachedIsDailyMaxDD ? "Max DD: ON" : "Max DD: OFF",
				cachedIsDailyMaxDD ? dailyOnBg : toggleOffBg, null, 24, 10);
			btnDailyMaxDD.Foreground = cachedIsDailyMaxDD ? Brushes.White : Brushes.LightGray;

			btnDailyMaxDD.Click += (s, ev) =>
			{
				cachedIsDailyMaxDD = !cachedIsDailyMaxDD;
				// Persist to the NinjaScript property — a script refresh/reload re-reads the property,
				// so a volatile-only OFF was silently re-enabled and could flatten on the next breach.
				DailyMaxDDEnabled = cachedIsDailyMaxDD;
				SetButtonLabel(btnDailyMaxDD, cachedIsDailyMaxDD ? "Max DD: ON" : "Max DD: OFF");
				btnDailyMaxDD.Background = cachedIsDailyMaxDD ? dailyOnBg : toggleOffBg;
				btnDailyMaxDD.Foreground = cachedIsDailyMaxDD ? Brushes.White : Brushes.LightGray;

				// Instant effect on HUD click (Requirement 4)
				EvaluateDailyRiskLimits();
				try { UpdateTradingProfileButtons(); } catch {}
			};
			Grid.SetColumn(btnDailyMaxDD, 0);
			Grid.SetColumnSpan(btnDailyMaxDD, 5);
			dailyRiskGrid.Children.Add(btnDailyMaxDD);

			Button btnDailyMaxProfit = CreateButton(cachedIsDailyMaxProfit ? "Max Profit: ON" : "Max Profit: OFF",
				cachedIsDailyMaxProfit ? dailyOnBg : toggleOffBg, null, 24, 10);
			btnDailyMaxProfit.Foreground = cachedIsDailyMaxProfit ? Brushes.White : Brushes.LightGray;

			btnDailyMaxProfit.Click += (s, ev) =>
			{
				cachedIsDailyMaxProfit = !cachedIsDailyMaxProfit;
				DailyMaxProfitEnabled = cachedIsDailyMaxProfit; // persist — survives script refresh/reload
				SetButtonLabel(btnDailyMaxProfit, cachedIsDailyMaxProfit ? "Max Profit: ON" : "Max Profit: OFF");
				btnDailyMaxProfit.Background = cachedIsDailyMaxProfit ? dailyOnBg : toggleOffBg;
				btnDailyMaxProfit.Foreground = cachedIsDailyMaxProfit ? Brushes.White : Brushes.LightGray;

				// Instant effect on HUD click (Requirement 4)
				EvaluateDailyRiskLimits();
				try { UpdateTradingProfileButtons(); } catch {}
			};
			Grid.SetColumn(btnDailyMaxProfit, 6);
			Grid.SetColumnSpan(btnDailyMaxProfit, 5);
			dailyRiskGrid.Children.Add(btnDailyMaxProfit);

			sec4Panel.Children.Add(dailyRiskGrid);

			// Daily Risk Quick Set buttons: robust quick-set template
			dailyRiskPresetButtons = new Button[6];
			Grid dailyRiskPresetGrid = CreateSixColumnGrid(0, HudGap, HudGap);
			for (int i = 0; i < 6; i++)
			{
				int presetIdx = i;
				double fsDrBase = GetQuickSetFontSize();
				double fsDr = Math.Min(14, fsDrBase + 2);
				string drLabel = GetDailyRiskPresetName(presetIdx);
				Button presetButton = CreateButton(drLabel, dailyRiskPresetOffBg, null, 24, fsDr);
				presetButton.Template = GetQuickSetButtonTemplate();
				presetButton.Foreground = Brushes.White;
				presetButton.FontSize = fsDr;
				presetButton.FontWeight = FontWeights.SemiBold;
				presetButton.HorizontalContentAlignment = HorizontalAlignment.Center;
				presetButton.VerticalContentAlignment = VerticalAlignment.Center;
				presetButton.Padding = new Thickness(1, 0, 1, 0);
				presetButton.BorderThickness = new Thickness(0);
				presetButton.Content = drLabel;
				presetButton.Click += (s, ev) => ApplyDailyRiskPreset(presetIdx);
				Grid.SetColumn(presetButton, presetIdx * 2);
				dailyRiskPresetButtons[presetIdx] = presetButton;
				dailyRiskPresetGrid.Children.Add(presetButton);
			}
			sec4Panel.Children.Add(dailyRiskPresetGrid);
			UpdateDailyRiskPresetButtons();

			mainPanel.Children.Add(CreateSectionCard(sec4Panel, HudGap));

			// --- SECTION 5: Discipline Protects (bottom) ---
			StackPanel sec5Panel = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };
			disciplineButtons = new Button[6];
			string[] discLabels = new[] { "Fix size", "No SL-pull", "No loss-DCA", "No TP-early", "StopWhenLoss", "TradingWindows" };
			bool[] discStates = new[] { cachedSizingProtect, cachedSlPullProtect, cachedLossDcaProtect, cachedTpEarlyProtect, cachedLossTimesProtect, cachedTimingProtect };

			// Row 0: DISCIPLINED / UN-DISCIPLINED toggle + EmaZoneOnly (replaces Un-Discipline) — DISCIPLINE controls all 7 (6 discipline + EmaZoneOnly)
			bool allOnInit = cachedIsEmaPlace && cachedSizingProtect && cachedSlPullProtect && cachedLossDcaProtect && cachedTpEarlyProtect && cachedLossTimesProtect && cachedTimingProtect;
			Grid allToggleGrid = CreateTwoColumnGrid(HudGap, HudGap);

			// Discipline All — ON: blaze orange + gold border, OFF: plain + purple border — height 24 sync with other toggle rows (was 26)
			if (allOnInit)
			{
				btnDisciplineAll = CreateButton("DISCIPLINED", disciplineAllOnBg, null, 24, 11);
				StackPanel spInit = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
				TextBlock iconInit = new TextBlock { Text = "⚡", Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 0)), FontSize = 11, Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
				TextBlock labelInit = new TextBlock { Text = "DISCIPLINED", Foreground = Brushes.White, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
				spInit.Children.Add(iconInit);
				spInit.Children.Add(labelInit);
				btnDisciplineAll.Content = spInit;
				btnDisciplineAll.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 215, 0));
				btnDisciplineAll.BorderThickness = new Thickness(1);
			}
			else
			{
				btnDisciplineAll = CreateButton("UN-DISCIPLINED", disciplineAllOffBg, null, 24, 11);
				btnDisciplineAll.BorderBrush = new SolidColorBrush(Color.FromRgb(75, 30, 110));
				btnDisciplineAll.BorderThickness = new Thickness(1);
			}
			btnDisciplineAll.Foreground = Brushes.White;
			btnDisciplineAll.FontWeight = FontWeights.Normal;
			btnDisciplineAll.Click += (s, ev) => SetAllDiscipline(!IsDisciplineAllOn());
			Grid.SetColumn(btnDisciplineAll, 0);
			allToggleGrid.Children.Add(btnDisciplineAll);
			btnEmaPlace = CreateButton("EmaZoneOnly", cachedIsEmaPlace ? disciplineAllOnBg : disciplineOffBg, null, 24, 11);
			btnEmaPlace.Foreground = cachedIsEmaPlace ? Brushes.White : Brushes.LightGray;
			btnEmaPlace.FontWeight = FontWeights.Normal;
			if (cachedIsEmaPlace)
			{
				btnEmaPlace.BorderBrush = Brushes.Transparent;
				btnEmaPlace.BorderThickness = new Thickness(0);
			}
			else
			{
				btnEmaPlace.BorderBrush = new SolidColorBrush(Color.FromRgb(75, 30, 110));
				btnEmaPlace.BorderThickness = new Thickness(1);
			}
			btnEmaPlace.Click += (s, ev) =>
			{
				cachedIsEmaPlace = !cachedIsEmaPlace;
				EmaProtectEnabled = cachedIsEmaPlace;
				UpdateEmaPlaceButton();
				try { UpdateDisciplineAllButton(); } catch {}
				try { UpdateTradingProfileButtons(); } catch {}
			};
			Grid.SetColumn(btnEmaPlace, 2);
			allToggleGrid.Children.Add(btnEmaPlace);
			sec5Panel.Children.Add(allToggleGrid);

			// 3 rows x 2 cols for 6 protects — same shade per row (2 buttons/row share color)
			for (int row = 0; row < 3; row++)
			{
				Grid rowGrid = CreateTwoColumnGrid(row == 2 ? 0 : HudGap, HudGap);
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

			mainPanel.Children.Add(CreateSectionCard(sec5Panel, HudGap * 2));

			panelBorder.Child = mainPanel;
		}
	}
}