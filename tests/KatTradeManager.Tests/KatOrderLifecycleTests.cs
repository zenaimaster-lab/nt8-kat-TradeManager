using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatOrderLifecycleTests
	{
		[Fact]
		public void AtmFlatCleanup_PendingStartup_IsDeferred()
		{
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(true, false));
		}

		[Fact]
		public void AtmFlatCleanup_ConfirmedPosition_IsNotDeferred()
		{
			Assert.False(KatTradeCalculator.ShouldDeferAtmFlatCleanup(true, true));
			Assert.False(KatTradeCalculator.ShouldDeferAtmFlatCleanup(false, false));
		}

		[Fact]
		public void AtmFlatCleanup_StartupTerminalButPositionUpdateStillPending_IsDeferred()
		{
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(
				false,
				false,
				false,
				250.0,
				3000.0));
		}

		[Fact]
		public void AtmFlatCleanup_ScaleOutTransientFlatWithinActivityGrace_IsDeferred()
		{
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(
				false,
				false,
				true,
				500.0,
				3000.0));
		}

		[Fact]
		public void AtmFlatCleanup_StaleFlatPastActivityGrace_IsAllowed()
		{
			Assert.False(KatTradeCalculator.ShouldDeferAtmFlatCleanup(
				false,
				false,
				true,
				3500.0,
				3000.0));
		}

		[Fact]
		public void FlattenAccount_WithOrdersOrPositions_HasWork()
		{
			Assert.True(KatTradeCalculator.ShouldFlattenAccount(true, false));
			Assert.True(KatTradeCalculator.ShouldFlattenAccount(false, true));
			Assert.False(KatTradeCalculator.ShouldFlattenAccount(false, false));
		}
		[Fact]
		public void AtmLevels_ZeroTickSize_DefaultsToQuarterTick()
		{
			var levels = KatTradeCalculator.CalculateAtmLevels(
				KatOrderAction.Buy, 1000.0, 20, 40, 10, 15, 25, 0.0);

			Assert.Equal(995.0, levels.SlPrice, 4);
			Assert.Equal(1010.0, levels.TpPrice, 4);
			Assert.Equal(1002.5, levels.BePrice, 4);
		}

		[Fact]
		public void AtmLevels_ZeroTriggerPrice_ReturnsZeroLevels()
		{
			var levels = KatTradeCalculator.CalculateAtmLevels(
				KatOrderAction.Buy, 0.0, 20, 40, 10, 15, 25, 0.25);

			Assert.Equal(0.0, levels.SlPrice, 4);
			Assert.Equal(0.0, levels.TpPrice, 4);
			Assert.Equal(0.0, levels.BePrice, 4);
			Assert.Equal(0.0, levels.Sl1Price, 4);
			Assert.Equal(0.0, levels.Sl2Price, 4);
		}

		[Fact]
		public void AtmLevels_NegativeTriggerPrice_ReturnsZeroLevels()
		{
			var levels = KatTradeCalculator.CalculateAtmLevels(
				KatOrderAction.Buy, -500.0, 20, 40, 10, 15, 25, 0.25);

			Assert.Equal(0.0, levels.SlPrice, 4);
			Assert.Equal(0.0, levels.TpPrice, 4);
		}

		[Fact]
		public void AtmLevels_AllZeroTriggers_AllPricesEqualEntry()
		{
			var levels = KatTradeCalculator.CalculateAtmLevels(
				KatOrderAction.Sell, 2000.0, 0, 0, 0, 0, 0, 0.25);

			Assert.Equal(2000.0, levels.SlPrice, 4);
			Assert.Equal(2000.0, levels.TpPrice, 4);
			Assert.Equal(2000.0, levels.BePrice, 4);
			Assert.Equal(2000.0, levels.Sl1Price, 4);
			Assert.Equal(2000.0, levels.Sl2Price, 4);
		}

		[Fact]
		public void CalculateCandlePrice_RenkoBrick_ReturnsHighForBuy()
		{
			double buyResult = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Buy, 102.0, 98.0);

			Assert.Equal(102.0, buyResult, 4);
		}

		[Fact]
		public void CalculateCandlePrice_RenkoBrick_ReturnsLowForSell()
		{
			double sellResult = KatTradeCalculator.CalculateCandlePrice(KatOrderAction.Sell, 102.0, 98.0);

			Assert.Equal(98.0, sellResult, 4);
		}

		[Fact]
		public void CalculateTriggerPrice_ZeroBuffer_ReturnsBasePrice()
		{
			double buy = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, 1500.0, 0, 0.25);
			double sell = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Sell, 1500.0, 0, 0.25);

			Assert.Equal(1500.0, buy, 4);
			Assert.Equal(1500.0, sell, 4);
		}

		[Fact]
		public void CalculateTriggerPrice_NegativeBuffer_ClampedToZero()
		{
			double buy = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, 1500.0, -5, 0.25);
			double sell = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Sell, 1500.0, -5, 0.25);

			Assert.Equal(1500.0, buy, 4);
			Assert.Equal(1500.0, sell, 4);
		}

		[Fact]
		public void CalculateStopLimitPrices_BuyUsesOneTickAboveTrigger()
		{
			KatTradeCalculator.CalculateStopLimitPrices(KatOrderAction.Buy, 1000.0, 0.25, out double limitPrice, out double stopPrice);

			Assert.Equal(1000.25, limitPrice, 4);
			Assert.Equal(1000.0, stopPrice, 4);
		}

		[Fact]
		public void CalculateStopLimitPrices_SellUsesOneTickBelowTrigger()
		{
			KatTradeCalculator.CalculateStopLimitPrices(KatOrderAction.Sell, 1000.0, 0.25, out double limitPrice, out double stopPrice);

			Assert.Equal(999.75, limitPrice, 4);
			Assert.Equal(1000.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_SellBelowMarket_OneTickBelow_ReturnsStopMarket()
		{
			double triggerPrice = 999.75;
			double currentPrice = 1000.0;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Sell, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.StopMarket, orderType);
			Assert.Equal(0.0, limitPrice, 4);
			Assert.Equal(999.75, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_BuyAboveMarket_OneTickAbove_ReturnsStopMarket()
		{
			double triggerPrice = 1000.25;
			double currentPrice = 1000.0;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Buy, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.StopMarket, orderType);
			Assert.Equal(0.0, limitPrice, 4);
			Assert.Equal(1000.25, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_RoundedToSame_ReturnsLimit()
		{
			double triggerPrice = 1000.124;
			double currentPrice = 1000.126;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Buy, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(1000.0, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_HighPrecision_BuyBelow_ReturnsLimit()
		{
			double triggerPrice = 4505.75;
			double currentPrice = 4506.00;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Buy, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(4505.75, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Fact]
		public void DetermineOrderType_HighPrecision_SellAbove_ReturnsLimit()
		{
			double triggerPrice = 4506.00;
			double currentPrice = 4505.75;
			double tickSize = 0.25;

			KatOrderType orderType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Sell, triggerPrice, currentPrice, tickSize, out double limitPrice, out double stopPrice);

			Assert.Equal(KatOrderType.Limit, orderType);
			Assert.Equal(4506.00, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		[Theory]
		[InlineData(0.01)]
		[InlineData(0.05)]
		[InlineData(0.10)]
		[InlineData(0.25)]
		[InlineData(0.50)]
		[InlineData(1.0)]
		public void DetermineOrderType_VariousTickSizes_StopThenLimit(double tickSize)
		{
			double currentPrice = Math.Round(1000.0 / tickSize) * tickSize;

			// Buy stop: trigger above market
			double buyStopTrigger = currentPrice + (2 * tickSize);
			KatOrderType buyStopType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Buy, buyStopTrigger, currentPrice, tickSize, out _, out _);
			Assert.Equal(KatOrderType.StopMarket, buyStopType);

			// Buy limit: trigger below market
			double buyLimitTrigger = currentPrice - (2 * tickSize);
			KatOrderType buyLimitType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Buy, buyLimitTrigger, currentPrice, tickSize, out _, out _);
			Assert.Equal(KatOrderType.Limit, buyLimitType);

			// Sell stop: trigger below market
			double sellStopTrigger = currentPrice - (2 * tickSize);
			KatOrderType sellStopType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Sell, sellStopTrigger, currentPrice, tickSize, out _, out _);
			Assert.Equal(KatOrderType.StopMarket, sellStopType);

			// Sell limit: trigger above market
			double sellLimitTrigger = currentPrice + (2 * tickSize);
			KatOrderType sellLimitType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Sell, sellLimitTrigger, currentPrice, tickSize, out _, out _);
			Assert.Equal(KatOrderType.Limit, sellLimitType);
		}

		[Theory]
		[InlineData(0.25)]
		[InlineData(0.01)]
		[InlineData(1.0)]
		public void StopPrice_Returned_OnlyForStopMarket_LimitPrice_Returned_OnlyForLimit(double tickSize)
		{
			double currentPrice = Math.Round(2000.0 / tickSize) * tickSize;

			// Buy stop
			var stopType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Buy, currentPrice + tickSize, currentPrice, tickSize,
				out double limitPrice, out double stopPrice);
			Assert.Equal(KatOrderType.StopMarket, stopType);
			Assert.True(stopPrice > 0, $"Stop price should be > 0 for StopMarket, got {stopPrice}");
			Assert.True(limitPrice == 0, $"Limit price should be 0 for StopMarket, got {limitPrice}");

			// Buy limit
			var limitType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Buy, currentPrice - tickSize, currentPrice, tickSize,
				out double limitPrice2, out double stopPrice2);
			Assert.Equal(KatOrderType.Limit, limitType);
			Assert.True(limitPrice2 > 0, $"Limit price should be > 0 for Limit, got {limitPrice2}");
			Assert.True(stopPrice2 == 0, $"Stop price should be 0 for Limit, got {stopPrice2}");

			// Sell stop
			var sellStopType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Sell, currentPrice - tickSize, currentPrice, tickSize,
				out double limitPrice3, out double stopPrice3);
			Assert.Equal(KatOrderType.StopMarket, sellStopType);
			Assert.True(stopPrice3 > 0, $"Stop price should be > 0 for Sell StopMarket, got {stopPrice3}");
			Assert.True(limitPrice3 == 0, $"Limit price should be 0 for Sell StopMarket, got {limitPrice3}");

			// Sell limit
			var sellLimitType = KatTradeCalculator.DetermineOrderType(
				KatOrderAction.Sell, currentPrice + tickSize, currentPrice, tickSize,
				out double limitPrice4, out double stopPrice4);
			Assert.Equal(KatOrderType.Limit, sellLimitType);
			Assert.True(limitPrice4 > 0, $"Limit price should be > 0 for Sell Limit, got {limitPrice4}");
			Assert.True(stopPrice4 == 0, $"Stop price should be 0 for Sell Limit, got {stopPrice4}");
		}
	}
}
