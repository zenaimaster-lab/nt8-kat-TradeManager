/* KatTradeManager.AccountInfo.cs - Account info header (partial class) v1.94 (2026-08-08) */
// ponytail: split from KatTradeManagerUI.cs 165-350 — pure UI sub-section, no logic change, keeps HUD design intact.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		// Account info header (top black section) — realtime NY time + Balance / Unrealized / Realized
		private Border accountInfoCard;
		private TextBlock accountInfoDateTimeText;
		private Run accountDateRun;
		private Run accountTimeHmRun;
		private Run accountTimeSRun;
		private Run accountAmPmRun;
		private Run accountNytRun;
		private TextBlock accountBalanceText;
		private Run accountBalanceLabelRun;
		private Run accountBalanceValueRun;
		private TextBlock accountUnrealText;
		private TextBlock accountRealText;
		private Run accountUnrealLabelRun;
		private Run accountUnrealValueRun;
		private Run accountRealLabelRun;
		private Run accountRealValueRun;
		private TextBlock hudHeaderText;
		private readonly SolidColorBrush accountDateBrush = new SolidColorBrush(Color.FromRgb(180, 100, 255)); // purple
		private readonly SolidColorBrush accountTimeBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // orange
		private readonly SolidColorBrush accountGrayBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160)); // gray for labels / pm / (NYT)
		private readonly SolidColorBrush pnlPositiveBrush = new SolidColorBrush(Color.FromRgb(40, 200, 80)); // green
		private readonly SolidColorBrush pnlNegativeBrush = new SolidColorBrush(Color.FromRgb(220, 50, 50)); // red

		private Border CreateAccountInfoSection()
		{
			StackPanel inner = new StackPanel { UseLayoutRounding = true, SnapsToDevicePixels = true };

			accountDateRun = new Run("") { Foreground = accountDateBrush };
			accountTimeHmRun = new Run("") { Foreground = accountTimeBrush, FontWeight = FontWeights.Bold };
			accountTimeSRun = new Run("") { Foreground = accountTimeBrush, FontWeight = FontWeights.Normal };
			accountAmPmRun = new Run("") { Foreground = accountGrayBrush };
			accountNytRun = new Run(" (NYT)") { Foreground = accountGrayBrush };
			accountInfoDateTimeText = new TextBlock
			{
				FontSize = 11,
				Margin = new Thickness(0, 0, 0, HudGap),
				HorizontalAlignment = HorizontalAlignment.Left,
				TextWrapping = TextWrapping.NoWrap,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			accountInfoDateTimeText.Inlines.Add(accountDateRun);
			accountInfoDateTimeText.Inlines.Add(new Run("   ") { Foreground = accountGrayBrush });
			accountInfoDateTimeText.Inlines.Add(accountTimeHmRun);
			accountInfoDateTimeText.Inlines.Add(accountTimeSRun);
			accountInfoDateTimeText.Inlines.Add(accountAmPmRun);
			accountInfoDateTimeText.Inlines.Add(accountNytRun);
			inner.Children.Add(accountInfoDateTimeText);

			accountBalanceLabelRun = new Run("Balance: ") { Foreground = accountGrayBrush };
			accountBalanceValueRun = new Run("--") { Foreground = accountGrayBrush };
			accountBalanceText = new TextBlock
			{
				FontSize = 11,
				Margin = new Thickness(0, 0, 0, HudGap),
				HorizontalAlignment = HorizontalAlignment.Left,
				TextWrapping = TextWrapping.NoWrap,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			accountBalanceText.Inlines.Add(accountBalanceLabelRun);
			accountBalanceText.Inlines.Add(accountBalanceValueRun);
			inner.Children.Add(accountBalanceText);

			accountUnrealLabelRun = new Run("U: ") { Foreground = accountGrayBrush };
			accountUnrealValueRun = new Run("--") { Foreground = accountGrayBrush };
			accountUnrealText = new TextBlock
			{
				FontSize = 11,
				HorizontalAlignment = HorizontalAlignment.Left,
				TextWrapping = TextWrapping.NoWrap,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			accountUnrealText.Inlines.Add(accountUnrealLabelRun);
			accountUnrealText.Inlines.Add(accountUnrealValueRun);

			accountRealLabelRun = new Run("R: ") { Foreground = accountGrayBrush };
			accountRealValueRun = new Run("--") { Foreground = accountGrayBrush };
			accountRealText = new TextBlock
			{
				FontSize = 11,
				HorizontalAlignment = HorizontalAlignment.Left,
				TextWrapping = TextWrapping.NoWrap,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			accountRealText.Inlines.Add(accountRealLabelRun);
			accountRealText.Inlines.Add(accountRealValueRun);

			Grid pnlGrid = CreateTwoColumnGrid(0, HudGap);
			Grid.SetColumn(accountUnrealText, 0);
			Grid.SetColumn(accountRealText, 2);
			pnlGrid.Children.Add(accountUnrealText);
			pnlGrid.Children.Add(accountRealText);
			inner.Children.Add(pnlGrid);

			var accContentHost = new Border
			{
				Padding = new Thickness(HudGap, HudGap + 4, HudGap, HudGap + 4),
				Background = Brushes.Transparent,
				Child = inner,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			var accFooter = new Border
			{
				Height = 10,
				Background = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
				CornerRadius = new CornerRadius(0, 0, 4, 4),
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			var accInner = new Grid { UseLayoutRounding = true, SnapsToDevicePixels = true };
			accInner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			accInner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
			Grid.SetRow(accContentHost, 0);
			Grid.SetRow(accFooter, 1);
			accInner.Children.Add(accContentHost);
			accInner.Children.Add(accFooter);
			accountInfoCard = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(0, 0, 0)),
				BorderBrush = new SolidColorBrush(Color.FromRgb(35, 42, 56)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(5),
				Margin = new Thickness(0, 0, 0, HudGap),
				Child = accInner,
				UseLayoutRounding = true,
				SnapsToDevicePixels = true
			};
			UpdateAccountInfoSection();
			return accountInfoCard;
		}

		private void UpdateAccountInfoSection()
		{
			if (accountInfoDateTimeText == null || accountDateRun == null) return;
			try
			{
				DateTime nyTime = KatTradeCalculator.GetNyTime(DateTime.UtcNow);
				string dateStr = nyTime.ToString("dddd dd, MMM", CultureInfo.InvariantCulture);
				string timeStr = nyTime.ToString("hh:mm:ss", CultureInfo.InvariantCulture);
				string amPmStr = nyTime.ToString("tt", CultureInfo.InvariantCulture).ToLowerInvariant();
				string hmStr = timeStr.Length >= 5 ? timeStr.Substring(0, 5) : timeStr;
				string sStr = timeStr.Length > 5 ? timeStr.Substring(5) : "";
				if (accountDateRun.Text != dateStr) accountDateRun.Text = dateStr;
				if (accountTimeHmRun.Text != hmStr) accountTimeHmRun.Text = hmStr;
				if (accountTimeSRun.Text != sStr) accountTimeSRun.Text = sStr;
				string amPmWithSpace = " " + amPmStr;
				if (accountAmPmRun.Text != amPmWithSpace) accountAmPmRun.Text = amPmWithSpace;
			}
			catch {}
			if (account == null)
			{
				try
				{
					if (accountBalanceValueRun.Text != "--") accountBalanceValueRun.Text = "--";
					accountBalanceValueRun.Foreground = accountGrayBrush;
					if (accountUnrealValueRun.Text != "--") accountUnrealValueRun.Text = "--";
					accountUnrealValueRun.Foreground = accountGrayBrush;
					if (accountRealValueRun.Text != "--") accountRealValueRun.Text = "--";
					accountRealValueRun.Foreground = accountGrayBrush;
				} catch {}
				return;
			}
			double balance = double.NaN;
			try { balance = account.Get(AccountItem.CashValue, Currency.UsDollar); } catch {}
			if (double.IsNaN(balance) || double.IsInfinity(balance)) try { balance = account.Get(AccountItem.TotalCashBalance, Currency.UsDollar); } catch {}
			if (double.IsNaN(balance) || double.IsInfinity(balance)) try { balance = account.Get(AccountItem.NetLiquidation, Currency.UsDollar); } catch {}
			if (double.IsNaN(balance) || double.IsInfinity(balance)) balance = 0;
			double unreal = 0;
			try { unreal = account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar); } catch {}
			double realized = double.NaN;
			try { realized = account.Get(AccountItem.GrossRealizedProfitLoss, Currency.UsDollar); } catch {}
			if (double.IsNaN(realized) || double.IsInfinity(realized)) try { realized = account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar); } catch {}
			if (double.IsNaN(realized) || double.IsInfinity(realized)) realized = 0;
			try
			{
				string balStr = balance.ToString("N0", CultureInfo.InvariantCulture);
				if (accountBalanceValueRun.Text != balStr) accountBalanceValueRun.Text = balStr;
				accountBalanceValueRun.Foreground = accountGrayBrush;
			} catch {}
			try
			{
				string uStr; Brush uBrush;
				if (unreal > 0.005) { uStr = "+" + unreal.ToString("N0", CultureInfo.InvariantCulture); uBrush = pnlPositiveBrush; }
				else if (unreal < -0.005) { uStr = unreal.ToString("N0", CultureInfo.InvariantCulture); uBrush = pnlNegativeBrush; }
				else { uStr = "0"; uBrush = accountGrayBrush; }
				if (accountUnrealValueRun.Text != uStr) accountUnrealValueRun.Text = uStr;
				accountUnrealValueRun.Foreground = uBrush;
			} catch {}
			try
			{
				string rStr; Brush rBrush;
				if (realized > 0.005) { rStr = "+" + realized.ToString("N0", CultureInfo.InvariantCulture); rBrush = pnlPositiveBrush; }
				else if (realized < -0.005) { rStr = realized.ToString("N0", CultureInfo.InvariantCulture); rBrush = pnlNegativeBrush; }
				else { rStr = "0"; rBrush = accountGrayBrush; }
				if (accountRealValueRun.Text != rStr) accountRealValueRun.Text = rStr;
				accountRealValueRun.Foreground = rBrush;
			} catch {}
		}
	}
}
