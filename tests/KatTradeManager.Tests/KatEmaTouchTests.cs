using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatEmaTouchTests
	{
		[Theory]
		[InlineData(110.0, 100.0, 105.0, true)]  // Spans across EMA
		[InlineData(105.0, 95.0, 105.0, true)]   // Touches at High
		[InlineData(115.0, 105.0, 105.0, true)]  // Touches at Low
		[InlineData(100.0, 90.0, 105.0, false)]  // Completely below EMA
		[InlineData(120.0, 110.0, 105.0, false)] // Completely above EMA
		public void IsEmaTouchBar_ValidatesTouchAndCrossCorrectly(double high, double low, double ema, bool expected)
		{
			bool result = KatTradeCalculator.IsEmaTouchBar(high, low, ema);
			Assert.Equal(expected, result);
		}

		[Fact]
		public void FindLastEmaTouchBar_ReturnsMostRecentMatchingBarIndex()
		{
			double[] highs = new double[] { 120.0, 105.0, 110.0, 95.0 };
			double[] lows  = new double[] { 110.0,  95.0,  90.0, 85.0 };
			double[] emas  = new double[] { 100.0, 100.0, 100.0, 100.0 };

			// Bar 0: H=120, L=110, EMA=100 -> false
			// Bar 1: H=105, L=95, EMA=100 -> true (first match)
			int index = KatTradeCalculator.FindLastEmaTouchBar(highs, lows, emas, 4);

			Assert.Equal(1, index);
		}

		[Fact]
		public void FindLastEmaTouchBar_ReturnsNegativeOne_WhenNoBarMatches()
		{
			double[] highs = new double[] { 120.0, 130.0 };
			double[] lows  = new double[] { 110.0, 105.0 };
			double[] emas  = new double[] { 100.0, 100.0 };

			int index = KatTradeCalculator.FindLastEmaTouchBar(highs, lows, emas, 2);

			Assert.Equal(-1, index);
		}

		[Fact]
		public void EmaOrderPrice_HalfCandleOff_GeneratesStopMarketOrders()
		{
			double high = 110.0;
			double low = 100.0;
			double open = 102.0;
			double close = 108.0;
			double tickSize = 0.25;
			int bufferTicks = 2; // +0.50
			double currentPrice = 105.0;

			// Buy: Base = High (110.0), Trigger = 110.50 -> StopMarket (110.50 > 105.0)
			double buyBase = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, false, high, low, open, close, false, tickSize);
			double buyTrigger = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, buyBase, bufferTicks, tickSize);
			KatOrderType buyType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, buyTrigger, currentPrice, tickSize, out double buyLimit, out double buyStop);

			Assert.Equal(110.0, buyBase);
			Assert.Equal(110.50, buyTrigger);
			Assert.Equal(KatOrderType.StopMarket, buyType);
			Assert.Equal(110.50, buyStop);
			Assert.Equal(0, buyLimit);

			// Sell: Base = Low (100.0), Trigger = 99.50 -> StopMarket (99.50 < 105.0)
			double sellBase = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, false, high, low, open, close, false, tickSize);
			double sellTrigger = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Sell, sellBase, bufferTicks, tickSize);
			KatOrderType sellType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, sellTrigger, currentPrice, tickSize, out double sellLimit, out double sellStop);

			Assert.Equal(100.0, sellBase);
			Assert.Equal(99.50, sellTrigger);
			Assert.Equal(KatOrderType.StopMarket, sellType);
			Assert.Equal(99.50, sellStop);
			Assert.Equal(0, sellLimit);
		}

		[Fact]
		public void EmaOrderPrice_HalfCandleOn_ConvertsToLimitOrder_WhenTriggerPassesCurrentPrice()
		{
			double high = 110.0;
			double low = 100.0;
			double open = 102.0;
			double close = 108.0;
			double tickSize = 0.25;
			int bufferTicks = 2; // +0.50

			// Half candle base (50% pullback) = (110 + 100) / 2 = 105.0
			double halfBase = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, true, 50.0, high, low, open, close, false, tickSize);
			Assert.Equal(105.0, halfBase);


			// Buy: Trigger = 105.50. If currentPrice = 108.0 (price ran above midpoint)
			// Trigger (105.50) <= Current (108.0) -> Limit order at 105.50
			double buyTrigger = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, halfBase, bufferTicks, tickSize);
			KatOrderType buyType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, buyTrigger, 108.0, tickSize, out double buyLimit, out double buyStop);

			Assert.Equal(105.50, buyTrigger);
			Assert.Equal(KatOrderType.Limit, buyType);
			Assert.Equal(105.50, buyLimit);
			Assert.Equal(0, buyStop);

			// Sell: Trigger = 104.50. If currentPrice = 102.0 (price ran below midpoint)
			// Trigger (104.50) >= Current (102.0) -> Limit order at 104.50
			double sellTrigger = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Sell, halfBase, bufferTicks, tickSize);
			KatOrderType sellType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, sellTrigger, 102.0, tickSize, out double sellLimit, out double sellStop);

			Assert.Equal(104.50, sellTrigger);
			Assert.Equal(KatOrderType.Limit, sellType);
			Assert.Equal(104.50, sellLimit);
			Assert.Equal(0, sellStop);
		}
	}
}
