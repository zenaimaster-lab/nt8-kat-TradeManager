using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatRenkoAndHalfCandleTests
	{
		[Fact]
		public void CalculateCandlePrice_StandardCandle_ReturnsHighOrLow()
		{
			double buyPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, 100.0, 90.0);
			double sellPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, 100.0, 90.0);

			Assert.Equal(100.0, buyPrice, 4);
			Assert.Equal(90.0, sellPrice, 4);
		}

		[Fact]
		public void CalculateCandlePrice_RenkoCandle_UsesRenkoBoxHighLow()
		{
			// Renko bricks have no wicks: the box high/low IS the anchor (same formula as standard candles)
			double buyPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, 102.0, 88.0);
			double sellPrice = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, 102.0, 88.0);

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
