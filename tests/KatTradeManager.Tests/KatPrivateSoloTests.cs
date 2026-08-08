using System;
using System.Collections.Generic;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatPrivateSoloTests
	{
		// ----- IsStopOnValidSide edge -----
		[Theory]
		[InlineData(true, 100, 101, true)]   // long: stop below market valid
		[InlineData(true, 102, 101, false)]  // long: stop above market invalid
		[InlineData(false, 102, 101, true)]  // short: stop above market valid
		[InlineData(false, 100, 101, false)] // short: stop below invalid
		[InlineData(true, 0, 101, false)]    // zero stop
		[InlineData(true, 100, 0, false)]   // zero market
		public void IsStopOnValidSide_Edges(bool isLong, double stop, double market, bool expected)
		{
			Assert.Equal(expected, KatTradeCalculator.IsStopOnValidSide(isLong, stop, market));
		}

		// ----- IsLossDcaBlocked -----
		[Theory]
		[InlineData(true, 100, 99.5, 0.25, true)]  // long underwater vs entry => blocked
		[InlineData(true, 100, 100.5, 0.25, false)] // long above entry => not blocked
		[InlineData(false, 100, 100.5, 0.25, true)] // short underwater (price above entry) => blocked
		[InlineData(false, 100, 99.5, 0.25, false)]
		[InlineData(true, 0, 99, 0.25, false)] // zero entry
		[InlineData(true, 100, 0, 0.25, false)] // zero cur
		public void IsLossDcaBlocked_Threshold(bool isLong, double entry, double cur, double tick, bool expected)
		{
			Assert.Equal(expected, KatTradeCalculator.IsLossDcaBlocked(isLong, entry, cur, tick));
		}

		// ----- IsScaleOut -----
		[Theory]
		[InlineData(true, KatOrderAction.Sell, true)]
		[InlineData(true, KatOrderAction.Buy, false)]
		[InlineData(false, KatOrderAction.Buy, true)]
		[InlineData(false, KatOrderAction.Sell, false)]
		public void IsScaleOut_Logic(bool isLong, KatOrderAction action, bool expected)
		{
			Assert.Equal(expected, KatTradeCalculator.IsScaleOut(isLong, action));
		}

		// ----- CalculateTriggerPrice rounding + buffer negative guard -----
		[Fact]
		public void CalculateTriggerPrice_BufferNegativeClamped()
		{
			double px = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, 100, -5, 0.25);
			Assert.Equal(100, px);
		}

		[Fact]
		public void CalculateTriggerPrice_TickZeroReturnsBase()
		{
			double px = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, 100, 10, 0);
			Assert.Equal(100, px);
		}

		[Fact]
		public void CalculateTriggerPrice_RoundsToTick()
		{
			double px = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, 100.0, 1, 0.25);
			Assert.Equal(100.25, px);
			px = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Sell, 100.0, 1, 0.25);
			Assert.Equal(99.75, px);
		}

		// ----- DetermineOrderType Stop vs Limit flip -----
		[Fact]
		public void DetermineOrderType_BuyStopAboveMarket()
		{
			var type = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, 101, 100, 0.25, out double limit, out double stop);
			Assert.Equal(KatOrderType.StopMarket, type);
			Assert.Equal(101, stop);
		}

		[Fact]
		public void DetermineOrderType_BuyLimitBelowMarket()
		{
			var type = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, 99, 100, 0.25, out double limit, out double stop);
			Assert.Equal(KatOrderType.Limit, type);
			Assert.Equal(99, limit);
		}

		// ----- CalculateAtmLevels both sides -----
		[Fact]
		public void CalculateAtmLevels_LongAndShort()
		{
			var longLevels = KatTradeCalculator.CalculateAtmLevels(KatOrderAction.Buy, 100, 20, 40, 10, 15, 25, 0.25);
			Assert.Equal(95, longLevels.SlPrice);
			Assert.Equal(110, longLevels.TpPrice);
			var shortLevels = KatTradeCalculator.CalculateAtmLevels(KatOrderAction.Sell, 100, 20, 40, 10, 15, 25, 0.25);
			Assert.Equal(105, shortLevels.SlPrice);
			Assert.Equal(90, shortLevels.TpPrice);
		}

		[Fact]
		public void CalculateAtmLevels_ZeroTriggerReturnsEmpty()
		{
			var l = KatTradeCalculator.CalculateAtmLevels(KatOrderAction.Buy, 0, 20, 40, 10, 15, 25, 0.25);
			Assert.Equal(0, l.SlPrice);
		}

		// ----- EvaluateDailyRiskBreach profit side + DD priority -----
		[Fact]
		public void EvaluateDailyRiskBreach_ProfitSide()
		{
			bool hit = KatTradeCalculator.EvaluateDailyRiskBreach(false, 0, true, 1000, 1200, out string r);
			Assert.True(hit);
			Assert.Contains("Profit", r);
		}

		[Fact]
		public void EvaluateDailyRiskBreach_NoBreachInsideLimits()
		{
			bool hit = KatTradeCalculator.EvaluateDailyRiskBreach(true, 500, true, 1000, 100, out string r);
			Assert.False(hit);
		}

		// ----- IsWithinTradingWindows multiple windows + disabled -----
		[Fact]
		public void IsWithinTradingWindows_MultipleOneEnabled()
		{
			var wins = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = false, StartHour = 0, StartMinute = 0, EndHour = 23, EndMinute = 59 },
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 9, StartMinute = 0, EndHour = 10, EndMinute = 0 },
			};
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(9, 30, 0), wins));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(11, 0, 0), wins));
		}

		// ----- GetNySessionStartUtc moves at 18:00 NY -----
		[Fact]
		public void GetNySessionStartUtc_BeforeAndAfter18Ny()
		{
			// Use known UTC that is definitely after 18 NY and before 18 NY
			// NY is UTC-5 or UTC-4 (DST). Use June (EDT UTC-4) => 22:00 UTC = 18 EDT
			var utcAfter = new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc); // 19 EDT -> after 18
			var utcBefore = new DateTime(2026, 6, 15, 21, 0, 0, DateTimeKind.Utc); // 17 EDT -> before 18
			var sessAfter = KatTradeCalculator.GetNySessionStartUtc(utcAfter);
			var sessBefore = KatTradeCalculator.GetNySessionStartUtc(utcBefore);
			Assert.True(sessAfter > sessBefore);
			Assert.True(sessAfter <= utcAfter);
			Assert.True(sessBefore <= utcBefore);
		}

		// ----- ClampHudCoordinate with large panel -----
		[Fact]
		public void ClampHudCoordinate_LargePanelNegativeMin()
		{
			double c = KatTradeCalculator.ClampHudCoordinate(0, 500, 400, 40);
			Assert.True(c <= 360); // max = 400-40
			c = KatTradeCalculator.ClampHudCoordinate(-1000, 500, 400, 40);
			Assert.Equal(-460, c); // min = -(500-40)
		}

		// ----- PlanAtmBracketMerge liveQty 0 returns noop -----
		[Fact]
		public void PlanAtmBracketMerge_ZeroLiveQtyNoop()
		{
			var orders = new List<KatTradeCalculator.KatAtmBracketOrder>
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "A", IsStop = true, Quantity = 2, Price = 99 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "A", IsStop = false, Quantity = 2, Price = 101 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 0);
			Assert.True(plan.IsNoop);
		}

		// ----- IsAccountAllowed edge: whitespace filter, null name -----
		[Theory]
		[InlineData(null, "Sim101", false)]
		[InlineData("", "Sim101", false)]
		[InlineData("   ", "Sim101", false)]
		public void IsAccountAllowed_NullOrWhitespaceNameDenied(string name, string filter, bool expected)
		{
			Assert.Equal(expected, KatTradeCalculator.IsAccountAllowed(name, filter));
		}

		[Fact]
		public void IsAccountAllowed_WhitespaceFilterAllowsAll()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "   "));
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", " , ; "));
		}

		// ----- Normalize edge: whitespace trimmed, exact boundary -----
		[Fact]
		public void NormalizeAtmSetName_WhitespaceTrimmed()
		{
			Assert.Equal("AB", KatTradeCalculator.NormalizeAtmSetName("  AB  ", "A"));
			Assert.Equal("ABC", KatTradeCalculator.NormalizeAtmSetName("ABCDEF", "A"));
		}
	}
}
