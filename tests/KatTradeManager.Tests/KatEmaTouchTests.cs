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
		public void EmaOrderPrice_CandleAnchors_GenerateStopMarketOrders()
		{
			double high = 110.0;
			double low = 100.0;
			double tickSize = 0.25;
			int bufferTicks = 2; // +0.50
			double currentPrice = 105.0;

			// Buy: Base = High (110.0), Trigger = 110.50 -> StopMarket (110.50 > 105.0)
			double buyBase = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, high, low);
			double buyTrigger = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, buyBase, bufferTicks, tickSize);
			KatOrderType buyType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, buyTrigger, currentPrice, tickSize, out double buyLimit, out double buyStop);

			Assert.Equal(110.0, buyBase);
			Assert.Equal(110.50, buyTrigger);
			Assert.Equal(KatOrderType.StopMarket, buyType);
			Assert.Equal(110.50, buyStop);
			Assert.Equal(0, buyLimit);

			// Sell: Base = Low (100.0), Trigger = 99.50 -> StopMarket (99.50 < 105.0)
			double sellBase = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, high, low);
			double sellTrigger = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Sell, sellBase, bufferTicks, tickSize);
			KatOrderType sellType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, sellTrigger, currentPrice, tickSize, out double sellLimit, out double sellStop);

			Assert.Equal(100.0, sellBase);
			Assert.Equal(99.50, sellTrigger);
			Assert.Equal(KatOrderType.StopMarket, sellType);
			Assert.Equal(99.50, sellStop);
			Assert.Equal(0, sellLimit);
		}
	}
}
