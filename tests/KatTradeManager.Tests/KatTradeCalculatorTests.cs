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
		public void PlanAtmBracketMerge_ChoosesLargestCompleteOcoPair_Only()
		{
			// stopA(5)+targetA(5) is a full pair; stopB(70) alone is incomplete; targetC(70) alone is incomplete.
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Id = "stopA", Oco = "oco1", IsStop = true, Quantity = 5, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Id = "targetA", Oco = "oco1", IsStop = false, Quantity = 5, Price = 110.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Id = "stopB", Oco = "oco2", IsStop = true, Quantity = 70, Price = 99.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Id = "targetC", Oco = "oco3", IsStop = false, Quantity = 70, Price = 120.0 },
			};

			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 70);

			Assert.Equal("stopA", plan.KeepStopId);
			Assert.Equal("targetA", plan.KeepTargetId);
			Assert.Equal(70, plan.DesiredStopQuantity);
			Assert.Equal(70, plan.DesiredTargetQuantity);
			Assert.Equal(new[] { "stopA", "targetA" }, plan.ChangeIds);
			Assert.Equal(new[] { "stopB", "targetC" }, plan.CancelIds);
		}

		[Fact]
		public void PlanAtmBracketMerge_NoCompletePair_ReturnsEmptyPlan()
		{
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Id = "stop1", Oco = "oco1", IsStop = true, Quantity = 70, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Id = "stop2", Oco = "oco2", IsStop = true, Quantity = 70, Price = 99.0 },
			};

			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 70);

			Assert.True(plan.IsNoop);
			Assert.Empty(plan.ChangeIds);
			Assert.Empty(plan.CancelIds);
		}

		[Fact]
		public void PlanAtmBracketMerge_DifferentOco_NeverMerged()
		{
			// stop1 in oco1, target1 in oco2 — must NOT pair across OCO.
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Id = "stop1", Oco = "oco1", IsStop = true, Quantity = 70, Price = 100.0 },
				new KatTradeCalculator.KatAtmBracketOrder { Id = "target1", Oco = "oco2", IsStop = false, Quantity = 70, Price = 110.0 },
			};

			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 70);

			Assert.True(plan.IsNoop);
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
	}
}
