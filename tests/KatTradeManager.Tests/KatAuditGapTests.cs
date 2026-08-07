using System;
using System.Collections.Generic;
using NinjaTrader.NinjaScript.Indicators;
using Xunit;

namespace KatTradeManager.Tests
{
	public class KatAuditGapTests
	{
		#region NormalizeProfileName
		[Fact]
		public void NormalizeProfileName_WithinLimit_Kept()
		{
			Assert.Equal("P1", KatTradeCalculator.NormalizeProfileName("P1", "P1"));
			Assert.Equal("MyProf", KatTradeCalculator.NormalizeProfileName("MyProf", "P1"));
			Assert.Equal("12345678", KatTradeCalculator.NormalizeProfileName("12345678", "P1"));
		}
		[Fact]
		public void NormalizeProfileName_OverEight_Truncated()
		{
			Assert.Equal("12345678", KatTradeCalculator.NormalizeProfileName("1234567890", "P1"));
			Assert.Equal("VeryLong", KatTradeCalculator.NormalizeProfileName("VeryLongName", "P1"));
		}
		[Fact]
		public void NormalizeProfileName_EmptyOrWhitespace_Fallback()
		{
			Assert.Equal("P1", KatTradeCalculator.NormalizeProfileName("", "P1"));
			Assert.Equal("P2", KatTradeCalculator.NormalizeProfileName("   ", "P2"));
			Assert.Equal("P3", KatTradeCalculator.NormalizeProfileName(null, "P3"));
		}
		[Fact]
		public void NormalizeProfileName_Trimmed()
		{
			Assert.Equal("AB", KatTradeCalculator.NormalizeProfileName("  AB ", "P1"));
		}
		#endregion

		#region IsWithinTradingWindows overnight
		[Fact]
		public void IsWithinTradingWindows_OvernightWrap_Correct()
		{
			var windows = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 22, StartMinute = 0, EndHour = 2, EndMinute = 0 },
			};
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(23, 0, 0), windows));
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(1, 0, 0), windows));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(3, 0, 0), windows));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(21, 59, 0), windows));
		}
		[Fact]
		public void IsWithinTradingWindows_DayWindow_BoundaryInclusiveExclusive()
		{
			var windows = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 9, StartMinute = 30, EndHour = 16, EndMinute = 0 },
			};
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(9, 30, 0), windows));
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(15, 59, 59), windows));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(16, 0, 0), windows));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(9, 29, 59), windows));
		}
		[Fact]
		public void IsWithinTradingWindows_NoWindowEnabled_ReturnsFalse()
		{
			var windows = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = false, StartHour = 9, StartMinute = 0, EndHour = 17, EndMinute = 0 },
			};
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(12, 0, 0), windows));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(12, 0, 0), null));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(12, 0, 0), new List<KatTradeCalculator.KatTradingWindow>()));
		}
		[Fact]
		public void IsWithinTradingWindows_ZeroLengthWindow_Skipped()
		{
			var windows = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 12, StartMinute = 0, EndHour = 12, EndMinute = 0 },
			};
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(12, 0, 0), windows));
		}
		[Fact]
		public void IsWithinTradingWindows_MultipleWindows_AnyInsideTrue()
		{
			var windows = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 2, StartMinute = 0, EndHour = 5, EndMinute = 0 },
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 13, StartMinute = 0, EndHour = 15, EndMinute = 0 },
			};
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(3, 0, 0), windows));
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(14, 0, 0), windows));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(12, 0, 0), windows));
		}
		#endregion

		#region Discipline pure gates
		[Fact]
		public void IsSizingBlocked_Strict_AnySameDirectionAddBlocked()
		{
			Assert.True(KatTradeCalculator.IsSizingBlocked(true, true, KatOrderAction.Buy, 1, 1, 1));
			Assert.True(KatTradeCalculator.IsSizingBlocked(true, true, KatOrderAction.Buy, 4, 4, 1));
			Assert.False(KatTradeCalculator.IsSizingBlocked(true, true, KatOrderAction.Sell, 1, 1, 1));
			Assert.False(KatTradeCalculator.IsSizingBlocked(false, true, KatOrderAction.Buy, 0, 0, 1));
			Assert.False(KatTradeCalculator.IsSizingBlocked(true, false, KatOrderAction.Buy, 1, 1, 1));
		}
		[Fact]
		public void IsSlPullBlocked_ToleranceHalfTick()
		{
			double tick = 0.25;
			// Long: new < initial - tol = 100 - 0.125 = 99.875 -> blocked
			Assert.True(KatTradeCalculator.IsSlPullBlocked(true, 100.0, 99.8, tick));
			Assert.False(KatTradeCalculator.IsSlPullBlocked(true, 100.0, 99.9, tick));
			Assert.False(KatTradeCalculator.IsSlPullBlocked(true, 100.0, 100.1, tick));
			// Short: new > initial + tol
			Assert.True(KatTradeCalculator.IsSlPullBlocked(false, 100.0, 100.2, tick));
			Assert.False(KatTradeCalculator.IsSlPullBlocked(false, 100.0, 100.1, tick));
			// zero initial -> never blocked
			Assert.False(KatTradeCalculator.IsSlPullBlocked(true, 0, 90, tick));
		}
		[Fact]
		public void IsLossDcaBlocked_AgainstEntry_Blocked()
		{
			double tick = 0.25;
			Assert.True(KatTradeCalculator.IsLossDcaBlocked(true, 100.0, 99.0, tick));
			Assert.False(KatTradeCalculator.IsLossDcaBlocked(true, 100.0, 101.0, tick));
			Assert.True(KatTradeCalculator.IsLossDcaBlocked(false, 100.0, 101.0, tick));
			Assert.False(KatTradeCalculator.IsLossDcaBlocked(false, 100.0, 99.0, tick));
			Assert.False(KatTradeCalculator.IsLossDcaBlocked(true, 0, 99, tick));
		}
		[Fact]
		public void IsScaleIn_Out_Mapping()
		{
			Assert.True(KatTradeCalculator.IsScaleIn(true, KatOrderAction.Buy));
			Assert.False(KatTradeCalculator.IsScaleIn(true, KatOrderAction.Sell));
			Assert.True(KatTradeCalculator.IsScaleIn(false, KatOrderAction.Sell));
			Assert.True(KatTradeCalculator.IsScaleOut(true, KatOrderAction.Sell));
			Assert.True(KatTradeCalculator.IsScaleOut(false, KatOrderAction.Buy));
		}
		[Fact]
		public void IsLossTimesLockActive_AndShouldTrigger()
		{
			DateTime now = DateTime.UtcNow;
			Assert.True(KatTradeCalculator.IsLossTimesLockActive(now.AddMinutes(5), now));
			Assert.False(KatTradeCalculator.IsLossTimesLockActive(now.AddMinutes(-1), now));
			Assert.False(KatTradeCalculator.IsLossTimesLockActive(DateTime.MinValue, now));
			Assert.True(KatTradeCalculator.ShouldTriggerLossLock(3, 3));
			Assert.False(KatTradeCalculator.ShouldTriggerLossLock(2, 3));
			Assert.False(KatTradeCalculator.ShouldTriggerLossLock(10, 0));
		}
		#endregion

		#region ShouldDeferAtmFlatCleanup
		[Fact]
		public void ShouldDefer_WhenPositionConfirmed_NeverDefer()
		{
			Assert.False(KatTradeCalculator.ShouldDeferAtmFlatCleanup(true, true));
			Assert.False(KatTradeCalculator.ShouldDeferAtmFlatCleanup(true, true, true, 100, 3000));
		}
		[Fact]
		public void ShouldDefer_GraceWindow()
		{
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(true, false, false, 1000, 3000));
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(true, false, false, 4000, 3000)); // startupPending && !wasConfirmed -> always defer
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(true, false, true, 100, 3000));
			Assert.False(KatTradeCalculator.ShouldDeferAtmFlatCleanup(false, false, true, 4000, 3000));
		}
		[Fact]
		public void ShouldDefer_NaNOrNegative_Defer()
		{
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(false, false, false, double.NaN, 3000));
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(false, false, false, -1, 3000));
		}
		#endregion

		#region ShouldFlattenAccount & ClampHud
		[Fact]
		public void ShouldFlattenAccount_TrueIfAnyWork()
		{
			Assert.True(KatTradeCalculator.ShouldFlattenAccount(true, false));
			Assert.True(KatTradeCalculator.ShouldFlattenAccount(false, true));
			Assert.False(KatTradeCalculator.ShouldFlattenAccount(false, false));
		}
		[Fact]
		public void ClampHudCoordinate_KeepsMinVisible()
		{
			Assert.Equal(5, KatTradeCalculator.ClampHudCoordinate(5, 200, 1000, 40));
			Assert.Equal(-160, KatTradeCalculator.ClampHudCoordinate(-500, 200, 1000, 40), 3);
			Assert.Equal(960, KatTradeCalculator.ClampHudCoordinate(2000, 200, 1000, 40), 3);
			Assert.Equal(0, KatTradeCalculator.ClampHudCoordinate(-10, 0, 0, 40));
		}
		#endregion

		#region CalculateStopLimitPrices
		[Fact]
		public void CalculateStopLimitPrices_OffsetOneTick()
		{
			KatTradeCalculator.CalculateStopLimitPrices(KatOrderAction.Buy, 100.0, 0.25, out double limitBuy, out double stopBuy);
			Assert.Equal(100.0, stopBuy);
			Assert.Equal(100.25, limitBuy);
			KatTradeCalculator.CalculateStopLimitPrices(KatOrderAction.Sell, 100.0, 0.25, out double limitSell, out double stopSell);
			Assert.Equal(100.0, stopSell);
			Assert.Equal(99.75, limitSell);
		}
		#endregion

		#region PlanAtmBracketMerge edge
		[Fact]
		public void PlanAtmBracketMerge_LiveQtyZero_IsNoop()
		{
			var orders = new[] { new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = true, Quantity = 1, Price = 100 }, new KatTradeCalculator.KatAtmBracketOrder { Oco = "a", IsStop = false, Quantity = 1, Price = 110 } };
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 0);
			Assert.True(plan.IsNoop);
			plan = KatTradeCalculator.PlanAtmBracketMerge(null, 5);
			Assert.True(plan.IsNoop);
		}
		#endregion

		#region XmlResolver still parses
		[Fact]
		public void ParseXml_AfterResolverFix_StillParses()
		{
			string xml = "<AtmStrategy><Brackets><Bracket><StopLoss>20</StopLoss><Target>40</Target></Bracket></Brackets></AtmStrategy>";
			var data = KatAtmXmlParser.ParseXml(xml);
			Assert.Equal(20, data.StopLoss);
			Assert.Equal(40, data.Target);
		}
		#endregion
	}
}
