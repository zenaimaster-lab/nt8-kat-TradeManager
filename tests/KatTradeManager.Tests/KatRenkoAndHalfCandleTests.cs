using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatRenkoAndHalfCandleTests
	{
		[Theory]
		[InlineData(100.0, 90.0, 0.25, 95.0)]
		[InlineData(100.50, 90.0, 0.25, 95.25)]
		[InlineData(2000.0, 1990.0, 0.25, 1995.0)]
		public void CalculateHalfCandlePrice_ReturnsExpectedMidpoint(double high, double low, double tickSize, double expectedMid)
		{
			double result = KatTradeCalculator.CalculateHalfCandlePrice(high, low, tickSize);
			Assert.Equal(expectedMid, result, 4);
		}

		[Theory]
		[InlineData(KatOrderAction.Buy, 100.0, 90.0, 30.0, 0.25, 97.0)]
		[InlineData(KatOrderAction.Sell, 100.0, 90.0, 30.0, 0.25, 93.0)]
		[InlineData(KatOrderAction.Buy, 100.0, 90.0, 50.0, 0.25, 95.0)]
		[InlineData(KatOrderAction.Sell, 100.0, 90.0, 50.0, 0.25, 95.0)]
		[InlineData(KatOrderAction.Buy, 100.0, 90.0, 10.0, 0.25, 99.0)]
		[InlineData(KatOrderAction.Sell, 100.0, 90.0, 10.0, 0.25, 91.0)]
		[InlineData(KatOrderAction.Buy, 100.0, 90.0, 33.0, 0.25, 96.75)] // 100 - 3.3 = 96.7 -> rounded to 96.75
		[InlineData(KatOrderAction.Sell, 100.0, 90.0, 17.0, 0.25, 91.75)] // 90 + 1.7 = 91.7 -> rounded to 91.75
		public void CalculatePartialCandlePrice_CalculatesCorrectPullback(KatOrderAction action, double high, double low, double pct, double tickSize, double expected)
		{
			double result = KatTradeCalculator.CalculatePartialCandlePrice(action, high, low, pct, tickSize);
			Assert.Equal(expected, result, 4);
		}


		[Fact]
		public void CalculateCandlePrice_PartialCandle_Returns30PercentPullbackByDefault()
		{
			double high = 100.0;
			double low = 90.0;
			double open = 92.0;
			double close = 98.0;
			double tickSize = 0.25;

			double buyPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, true, 30.0, high, low, open, close, false, tickSize);
			double sellPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, true, 30.0, high, low, open, close, false, tickSize);

			Assert.Equal(97.0, buyPrice, 4);
			Assert.Equal(93.0, sellPrice, 4);
		}


		[Fact]
		public void CalculateCandlePrice_StandardCandle_ReturnsHighOrLow()
		{
			double high = 100.0;
			double low = 90.0;
			double open = 92.0;
			double close = 98.0;
			double tickSize = 0.25;

			double buyPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, false, high, low, open, close, false, tickSize);
			double sellPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, false, high, low, open, close, false, tickSize);

			Assert.Equal(100.0, buyPrice, 4);
			Assert.Equal(90.0, sellPrice, 4);
		}

		[Fact]
		public void CalculateCandlePrice_RenkoCandle_UsesRenkoBoxHighLow()
		{
			double high = 102.0;
			double low = 88.0;
			double open = 90.0;
			double close = 100.0;
			double tickSize = 0.25;

			double buyPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, false, high, low, open, close, true, tickSize);
			double sellPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, false, high, low, open, close, true, tickSize);

			Assert.Equal(102.0, buyPrice, 4);
			Assert.Equal(88.0, sellPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_WithTickRounding_BuyAboveMarket_ReturnsStopMarket()
		{
			double triggerPrice = 1000.50;
			double currentPrice = 1000.00;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.StopMarket, orderType);
			Assert.Equal(1000.50, stopPrice, 4);
			Assert.Equal(0.0, limitPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_WithTickRounding_BuyBelowMarket_ReturnsLimit()
		{
			double triggerPrice = 999.50;
			double currentPrice = 1000.00;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(999.50, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_WithTickRounding_SellBelowMarket_ReturnsStopMarket()
		{
			double triggerPrice = 999.50;
			double currentPrice = 1000.00;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.StopMarket, orderType);
			Assert.Equal(999.50, stopPrice, 4);
			Assert.Equal(0.0, limitPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_WithTickRounding_SellAboveMarket_ReturnsLimit()
		{
			double triggerPrice = 1000.50;
			double currentPrice = 1000.00;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(1000.50, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_FloatingPointPrecision_RoundsToTickSize()
		{
			double triggerPrice = 1000.000000001;
			double currentPrice = 1000.0;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(1000.00, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_BuyTriggerEqualCurrent_ReturnsLimitAtTickRoundedPrice()
		{
			double triggerPrice = 2050.25;
			double currentPrice = 2050.25;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(2050.25, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_SellTriggerEqualCurrent_ReturnsLimitAtTickRoundedPrice()
		{
			double triggerPrice = 2050.25;
			double currentPrice = 2050.25;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(2050.25, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}
	}
}
