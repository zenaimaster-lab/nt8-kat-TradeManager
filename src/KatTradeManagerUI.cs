/* KatTradeManagerUI.cs - WPF UI partial class for KatTradeManager v0.23 (2026-07-25) */
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

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class KatTradeManager : Indicator
	{
		#region WPF UI Construction & Handlers
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

		private void SyncCachedValues()
		{
			if (txtQuantity != null)
				cachedQuantity = int.TryParse(txtQuantity.Text, out int q) ? q : DefaultQuantity;
			if (tfSelector != null)
				cachedTfIndex = tfSelector.SelectedIndex;
			if (txtBuffer != null)
				cachedBufferTicks = int.TryParse(txtBuffer.Text, out int b) ? b : DefaultBufferTicks;
			if (txtDistance != null)
				cachedDistanceTicks = int.TryParse(txtDistance.Text, out int d) ? d : DefaultDistanceTicks;
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
				Width = 240,
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

			// Layout Grid for Parameters
			Grid paramGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
			paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
			paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			// Account Selection
			ComboBox accSelector = new ComboBox { FontSize = 11, Height = 22 };
			if (Account.All != null)
			{
				foreach (var acc in Account.All) accSelector.Items.Add(acc.Name);
				if (account != null) accSelector.SelectedItem = account.Name;
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
			AddGridRow(paramGrid, "Acc:", accSelector);

			// Timeframe Selection
			tfSelector = new ComboBox { FontSize = 11, Height = 22 };
			tfSelector.Items.Add("Chart TF");
			tfSelector.Items.Add("30s");
			tfSelector.Items.Add("1m");
			tfSelector.Items.Add("2m");
			tfSelector.SelectedIndex = 0;
			AddGridRow(paramGrid, "TF:", tfSelector);

			// Quantity, Buffer & Fixed Distance Inputs
			txtQuantity = new TextBox { Text = DefaultQuantity.ToString(), FontSize = 11, Height = 22, Background = Brushes.Black, Foreground = Brushes.White, BorderBrush = Brushes.Gray, Padding = new Thickness(4, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center };
			txtQuantity.PreviewKeyDown += (s, ev) =>
			{
				if (ev.Key == Key.Enter)
				{
					SyncCachedValues();
					if (ChartControl != null) ChartControl.Focus();
					ev.Handled = true;
				}
			};
			AddGridRow(paramGrid, "Contracts:", txtQuantity);

			txtBuffer = new TextBox { Text = DefaultBufferTicks.ToString(), FontSize = 11, Height = 22, Background = Brushes.Black, Foreground = Brushes.White, BorderBrush = Brushes.Gray, Padding = new Thickness(4, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center };
			txtBuffer.PreviewKeyDown += (s, ev) =>
			{
				if (ev.Key == Key.Enter)
				{
					SyncCachedValues();
					if (ChartControl != null) ChartControl.Focus();
					ev.Handled = true;
				}
			};
			AddGridRow(paramGrid, "Buffer (Ticks):", txtBuffer);

			txtDistance = new TextBox { Text = DefaultDistanceTicks.ToString(), FontSize = 11, Height = 22, Background = Brushes.Black, Foreground = Brushes.White, BorderBrush = Brushes.Gray, Padding = new Thickness(4, 0, 4, 0), VerticalContentAlignment = VerticalAlignment.Center };
			txtDistance.PreviewKeyDown += (s, ev) =>
			{
				if (ev.Key == Key.Enter)
				{
					SyncCachedValues();
					if (ChartControl != null) ChartControl.Focus();
					ev.Handled = true;
				}
			};
			AddGridRow(paramGrid, "Dist (Ticks):", txtDistance);

			// ATM Selection Dropdown
			atmSelector = new ComboBox { FontSize = 11, Height = 22 };
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
			if (atmSelector.SelectedItem != null)
			{
				cachedAtmTemplate = atmSelector.SelectedItem.ToString();
				LoadAtmTemplateSettings(cachedAtmTemplate);
			}
			atmSelector.SelectionChanged += (s, ev) =>
			{
				if (atmSelector.SelectedItem != null)
				{
					cachedAtmTemplate = atmSelector.SelectedItem.ToString();
					LoadAtmTemplateSettings(cachedAtmTemplate);
				}
			};
			AddGridRow(paramGrid, "ATM:", atmSelector);

			mainPanel.Children.Add(paramGrid);

			// Buttons Section
			Grid orderBtnGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
			orderBtnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			SolidColorBrush buyPrevBg  = new SolidColorBrush(Color.FromRgb(34, 112, 62));
			SolidColorBrush buyCurrBg  = new SolidColorBrush(Color.FromRgb(24, 82, 45));
			SolidColorBrush buyFixedBg = new SolidColorBrush(Color.FromRgb(16, 56, 30));
			SolidColorBrush sellPrevBg = new SolidColorBrush(Color.FromRgb(148, 48, 54));
			SolidColorBrush sellCurrBg = new SolidColorBrush(Color.FromRgb(110, 32, 38));
			SolidColorBrush sellFixedBg = new SolidColorBrush(Color.FromRgb(75, 20, 24));
			SolidColorBrush cancelBg   = new SolidColorBrush(Color.FromRgb(160, 90, 25));
			SolidColorBrush closeBg    = new SolidColorBrush(Color.FromRgb(140, 35, 35));

			StackPanel buyCol = new StackPanel();
			buyCol.Children.Add(CreateButton("BUY Previous", buyPrevBg, (s, ev) => PlaceOrder(OrderAction.Buy, false), 48, 12));
			buyCol.Children.Add(CreateButton("BUY Current", buyCurrBg, (s, ev) => PlaceOrder(OrderAction.Buy, true), 24, 10));
			buyCol.Children.Add(CreateButton("BUY +Distance", buyFixedBg, (s, ev) => PlaceFixedDistanceOrder(OrderAction.Buy), 24, 10));
			Grid.SetColumn(buyCol, 0);

			StackPanel sellCol = new StackPanel();
			sellCol.Children.Add(CreateButton("SELL Previous", sellPrevBg, (s, ev) => PlaceOrder(OrderAction.Sell, false), 48, 12));
			sellCol.Children.Add(CreateButton("SELL Current", sellCurrBg, (s, ev) => PlaceOrder(OrderAction.Sell, true), 24, 10));
			sellCol.Children.Add(CreateButton("SELL -Distance", sellFixedBg, (s, ev) => PlaceFixedDistanceOrder(OrderAction.Sell), 24, 10));
			Grid.SetColumn(sellCol, 2);

			orderBtnGrid.Children.Add(buyCol);
			orderBtnGrid.Children.Add(sellCol);
			mainPanel.Children.Add(orderBtnGrid);

			// Management Buttons
			Grid mgrGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
			mgrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			mgrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
			mgrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			Button btnCancel = CreateButton("Cancel", cancelBg, (s, ev) => CancelAllOrders(), 22, 10);
			Grid.SetColumn(btnCancel, 0);
			mgrGrid.Children.Add(btnCancel);

			Button btnClose = CreateButton("Close", closeBg, (s, ev) => ClosePosition(), 22, 10);
			Grid.SetColumn(btnClose, 2);
			mgrGrid.Children.Add(btnClose);

			mainPanel.Children.Add(mgrGrid);
			panelBorder.Child = mainPanel;

			Grid.SetColumnSpan(panelBorder, 3);
			chartGrid.Children.Add(panelBorder);
		}

		private void AddGridRow(Grid grid, string labelText, FrameworkElement inputElement)
		{
			int rowIdx = grid.RowDefinitions.Count;
			grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });

			TextBlock label = new TextBlock
			{
				Text = labelText,
				Foreground = Brushes.LightGray,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				FontSize = 11
			};
			Grid.SetRow(label, rowIdx);
			Grid.SetColumn(label, 0);
			grid.Children.Add(label);

			inputElement.VerticalAlignment = VerticalAlignment.Center;
			inputElement.HorizontalAlignment = HorizontalAlignment.Stretch;
			inputElement.Height = 22;
			Grid.SetRow(inputElement, rowIdx);
			Grid.SetColumn(inputElement, 1);
			grid.Children.Add(inputElement);
		}

		private Button CreateButton(string text, Brush bg, RoutedEventHandler handler, double height = 24, double fontSize = 10)
		{
			Button btn = new Button
			{
				Content = text,
				Background = bg,
				Foreground = Brushes.White,
				FontWeight = FontWeights.Bold,
				FontSize = fontSize,
				Margin = new Thickness(0, 2, 0, 2),
				Padding = new Thickness(2),
				Height = height,
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
	}
}
