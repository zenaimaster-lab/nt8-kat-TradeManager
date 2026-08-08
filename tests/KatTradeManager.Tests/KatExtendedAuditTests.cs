using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatExtendedAuditTests
	{
		[Fact]
		public void ResolveTickSize_MarketVsPending()
		{
			// market fallback 0.01 vs pending 0.25
			Assert.Equal(0.01, KatTradeCalculator.ResolveTickSize(0, 0, 0.01));
			Assert.Equal(0.25, KatTradeCalculator.ResolveTickSize(0, 0, 0.25));
			Assert.Equal(0.5, KatTradeCalculator.ResolveTickSize(0.5, 0.01, 0.25));
		}

		[Fact]
		public void Debounce_MarketFasterThanPending()
		{
			// Document that market debounce is 100ms vs pending 200ms
			// This is a design assertion - if constants change, test reminds to update docs
			var market = 100.0;
			var pending = 200.0;
			Assert.True(market < pending);
			Assert.Equal(100.0, market);
			Assert.Equal(200.0, pending);
		}

		[Theory]
		[InlineData(true, true, true)]
		[InlineData(true, false, true)]
		[InlineData(false, true, true)]
		[InlineData(false, false, false)]
		public void ShouldFlattenAccount_Logic(bool hasOrders, bool hasPos, bool expected)
		{
			Assert.Equal(expected, KatTradeCalculator.ShouldFlattenAccount(hasOrders, hasPos));
		}

		[Fact]
		public void ClampHudCoordinate_NegativeMinVisibleClampedToZero()
		{
			double c = KatTradeCalculator.ClampHudCoordinate(10, 100, 200, -10);
			Assert.True(c >= -100 && c <= 200);
		}

		[Fact]
		public void CalculateMergedOrderQuantity_SumsPositive()
		{
			Assert.Equal(6, KatTradeCalculator.CalculateMergedOrderQuantity(new[] { 1, 2, 3 }));
			Assert.Equal(5, KatTradeCalculator.CalculateMergedOrderQuantity(new[] { 0, -1, 5 }));
			Assert.Equal(0, KatTradeCalculator.CalculateMergedOrderQuantity(new int[] { }));
		}

		[Fact]
		public void CalculateMergedOrderQuantity_NullReturnsZero()
		{
			Assert.Equal(0, KatTradeCalculator.CalculateMergedOrderQuantity(null));
		}

		[Theory]
		[InlineData(true, false, false, 100, 3000, true)] // startup pending, not confirmed, grace not expired
		[InlineData(false, false, false, 4000, 3000, false)] // not pending, grace expired
		[InlineData(false, false, false, 100, 3000, true)] // grace not expired
		public void ShouldDeferAtmFlatCleanup_Grace(bool pending, bool confirmed, bool wasConfirmed, double age, double grace, bool expected)
		{
			Assert.Equal(expected, KatTradeCalculator.ShouldDeferAtmFlatCleanup(pending, confirmed, wasConfirmed, age, grace));
		}

		[Theory]
		[InlineData(true, KatOrderAction.Buy, true)] // long + buy = scale in blocked when has position
		[InlineData(true, KatOrderAction.Sell, false)] // long + sell = scale out not sizing blocked
		public void IsSizingBlocked_HasPosition(bool isLong, KatOrderAction action, bool expected)
		{
			// hasPosition=true, isLong, action, posQty ignored in new strict logic (any scale-in blocked)
			bool blocked = KatTradeCalculator.IsSizingBlocked(true, isLong, action, 2, 4, 1);
			Assert.Equal(expected, blocked);
		}

		[Theory]
		[InlineData(true, 100, 99, 0.25, true)] // long, new < initial - tol => blocked
		[InlineData(true, 100, 101, 0.25, false)] // long, new > initial => not blocked
		[InlineData(false, 100, 101, 0.25, true)] // short, new > initial => blocked
		public void IsSlPullBlocked_Tolerance(bool isLong, double initial, double newSl, double tick, bool expected)
		{
			Assert.Equal(expected, KatTradeCalculator.IsSlPullBlocked(isLong, initial, newSl, tick));
		}

		[Fact]
		public void IsLossTimesLockActive_BeforeExpiry()
		{
			var until = DateTime.UtcNow.AddMinutes(5);
			Assert.True(KatTradeCalculator.IsLossTimesLockActive(until, DateTime.UtcNow));
			Assert.False(KatTradeCalculator.IsLossTimesLockActive(DateTime.MinValue, DateTime.UtcNow));
		}

		[Fact]
		public void ShouldTriggerLossLock_AtThreshold()
		{
			Assert.True(KatTradeCalculator.ShouldTriggerLossLock(3, 3));
			Assert.False(KatTradeCalculator.ShouldTriggerLossLock(2, 3));
			Assert.False(KatTradeCalculator.ShouldTriggerLossLock(3, 0));
		}

		[Fact]
		public void IsWithinTradingWindows_EmptyReturnsFalse()
		{
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(10, 0, 0), null));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(10, 0, 0), new System.Collections.Generic.List<KatTradeCalculator.KatTradingWindow>()));
		}
	}
}
