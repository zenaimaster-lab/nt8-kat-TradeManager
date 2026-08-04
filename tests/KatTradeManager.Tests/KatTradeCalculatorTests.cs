using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatTradeCalculatorTests
	{
		[Fact]
		public void CalculateTriggerPrice_BuyOrder_AddsBuffer()
		{
			// Arrange
			double basePrice = 1000.0;
			int bufferTicks = 2;
			double tickSize = 0.25;

			// Act
			double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, basePrice, bufferTicks, tickSize);

			// Assert
			Assert.Equal(1000.50, triggerPrice, 4);
		}

		[Fact]
		public void CalculateTriggerPrice_SellOrder_SubtractsBuffer()
		{
			// Arrange
			double basePrice = 1000.0;
			int bufferTicks = 2;
			double tickSize = 0.25;

			// Act
			double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Sell, basePrice, bufferTicks, tickSize);

			// Assert
			Assert.Equal(999.50, triggerPrice, 4);
		}

		[Fact]
		public void CalculateBreakevenPrice_LongPosition_AddsBuffer()
		{
			// Arrange
			double entryPrice = 20000.0;
			int bufferTicks = 2;
			double tickSize = 0.25;

			// Act
			double bePrice = KatTradeCalculator.CalculateBreakevenPrice(KatOrderAction.Buy, entryPrice, bufferTicks, tickSize);

			// Assert
			Assert.Equal(20000.50, bePrice, 4);
		}

		[Fact]
		public void CalculateBreakevenPrice_ShortPosition_SubtractsBuffer()
		{
			// Arrange
			double entryPrice = 20000.0;
			int bufferTicks = 2;
			double tickSize = 0.25;

			// Act
			double bePrice = KatTradeCalculator.CalculateBreakevenPrice(KatOrderAction.Sell, entryPrice, bufferTicks, tickSize);

			// Assert
			Assert.Equal(19999.50, bePrice, 4);
		}

		[Fact]
		public void CalculateFixedDistanceTriggerPrice_BuyOrder_AddsDistance()
		{
			// Arrange
			double currentPrice = 20000.0;
			int distanceTicks = 320;
			double tickSize = 0.25;

			// Act
			double triggerPrice = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(KatOrderAction.Buy, currentPrice, distanceTicks, tickSize);

			// Assert
			Assert.Equal(20080.0, triggerPrice, 4);
		}

		[Fact]
		public void CalculateFixedDistanceTriggerPrice_SellOrder_SubtractsDistance()
		{
			// Arrange
			double currentPrice = 20000.0;
			int distanceTicks = 320;
			double tickSize = 0.25;

			// Act
			double triggerPrice = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(KatOrderAction.Sell, currentPrice, distanceTicks, tickSize);

			// Assert
			Assert.Equal(19920.0, triggerPrice, 4);
		}

		[Fact]
		public void CalculateMergedOrderQuantity_SumsPositiveBracketQuantities()
		{
			int merged = KatTradeCalculator.CalculateMergedOrderQuantity(new[] { 1, 1, 2, -1, 0 });

			Assert.Equal(4, merged);
		}

		[Fact]
		public void CalculateMergedOrderQuantity_NullOrOverflow_IsSafe()
		{
			Assert.Equal(0, KatTradeCalculator.CalculateMergedOrderQuantity(null));
			Assert.Equal(int.MaxValue, KatTradeCalculator.CalculateMergedOrderQuantity(new[] { int.MaxValue, 1 }));
		}

		[Fact]
		public void DetermineOrderType_BuyOrder_AboveMarket_SelectsStopMarket()
		{
			// Arrange
			double triggerPrice = 1005.0;
			double currentPrice = 1000.0;

			// Act
			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, triggerPrice, currentPrice, out double limitPrice, out double stopPrice);

			// Assert
			Assert.Equal(KatOrderType.StopMarket, orderType);
			Assert.Equal(1005.0, stopPrice, 4);
			Assert.Equal(0.0, limitPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_BuyOrder_BelowMarket_SelectsLimit()
		{
			// Arrange
			double triggerPrice = 995.0;
			double currentPrice = 1000.0;

			// Act
			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, triggerPrice, currentPrice, out double limitPrice, out double stopPrice);

			// Assert
			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(995.0, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_SellOrder_BelowMarket_SelectsStopMarket()
		{
			// Arrange
			double triggerPrice = 995.0;
			double currentPrice = 1000.0;

			// Act
			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, triggerPrice, currentPrice, out double limitPrice, out double stopPrice);

			// Assert
			Assert.Equal(KatOrderType.StopMarket, orderType);
			Assert.Equal(995.0, stopPrice, 4);
			Assert.Equal(0.0, limitPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_SellOrder_AboveMarket_SelectsLimit()
		{
			// Arrange
			double triggerPrice = 1005.0;
			double currentPrice = 1000.0;

			// Act
			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(KatOrderAction.Sell, triggerPrice, currentPrice, out double limitPrice, out double stopPrice);

			// Assert
			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(1005.0, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void CalculateAtmLevels_BuyOrder_CalculatesCorrectTargetPrices()
		{
			// Arrange
			double triggerPrice = 1000.0;
			int slTicks = 20;
			int tpTicks = 40;
			int beTicks = 10;
			int sl1Ticks = 15;
			int sl2Ticks = 25;
			double tickSize = 0.25;

			// Act
			var levels = KatTradeCalculator.CalculateAtmLevels(KatOrderAction.Buy, triggerPrice, slTicks, tpTicks, beTicks, sl1Ticks, sl2Ticks, tickSize);

			// Assert
			Assert.Equal(995.0, levels.SlPrice, 4);
			Assert.Equal(1010.0, levels.TpPrice, 4);
			Assert.Equal(1002.5, levels.BePrice, 4);
			Assert.Equal(1003.75, levels.Sl1Price, 4);
			Assert.Equal(1006.25, levels.Sl2Price, 4);
		}

		[Fact]
		public void CalculateAtmLevels_SellOrder_CalculatesCorrectTargetPrices()
		{
			// Arrange
			double triggerPrice = 1000.0;
			int slTicks = 20;
			int tpTicks = 40;
			int beTicks = 10;
			int sl1Ticks = 15;
			int sl2Ticks = 25;
			double tickSize = 0.25;

			// Act
			var levels = KatTradeCalculator.CalculateAtmLevels(KatOrderAction.Sell, triggerPrice, slTicks, tpTicks, beTicks, sl1Ticks, sl2Ticks, tickSize);

			// Assert
			Assert.Equal(1005.0, levels.SlPrice, 4);
			Assert.Equal(990.0, levels.TpPrice, 4);
			Assert.Equal(997.5, levels.BePrice, 4);
			Assert.Equal(996.25, levels.Sl1Price, 4);
			Assert.Equal(993.75, levels.Sl2Price, 4);
		}

		[Fact]
		public void FindLastEmaTouchBar_ScansAndFindsTouchCandle()
		{
			double[] highs = new double[] { 105.0, 102.0, 98.0 };
			double[] lows  = new double[] { 101.0,  99.0, 94.0 };
			double[] emas  = new double[] { 100.0, 100.0, 100.0 };

			// Index 1 has High 102 >= 100 and Low 99 <= 100 -> Touch!
			int foundBarsAgo = KatTradeCalculator.FindLastEmaTouchBar(highs, lows, emas, 3);

			Assert.Equal(1, foundBarsAgo);
		}
	}
}
