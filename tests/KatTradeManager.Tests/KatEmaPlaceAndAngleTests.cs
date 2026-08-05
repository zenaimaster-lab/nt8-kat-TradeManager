using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatEmaPlaceAndAngleTests
	{
		[Fact]
		public void ValidateEmaPlace_BuyOrder_ValidWhenEntryAboveAllEmas()
		{
			double entryPrice = 105.0;
			double[] emas = new double[] { 100.0, 102.0, 104.0 };

			bool result = KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Buy, entryPrice, emas, out string err);

			Assert.True(result);
			Assert.Null(err);
		}

		[Fact]
		public void ValidateEmaPlace_BuyOrder_InvalidWhenEntryBelowAnyEma()
		{
			double entryPrice = 103.0;
			double[] emas = new double[] { 100.0, 102.0, 104.0 }; // 104 is above entry

			bool result = KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Buy, entryPrice, emas, out string err);

			Assert.False(result);
			Assert.NotNull(err);
			Assert.Contains("104", err);
		}

		[Fact]
		public void ValidateEmaPlace_SellOrder_ValidWhenEntryBelowAllEmas()
		{
			double entryPrice = 95.0;
			double[] emas = new double[] { 100.0, 102.0, 104.0 };

			bool result = KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Sell, entryPrice, emas, out string err);

			Assert.True(result);
			Assert.Null(err);
		}

		[Fact]
		public void ValidateEmaPlace_SellOrder_InvalidWhenEntryAboveAnyEma()
		{
			double entryPrice = 101.0;
			double[] emas = new double[] { 100.0, 102.0, 104.0 }; // 100 is below entry

			bool result = KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Sell, entryPrice, emas, out string err);

			Assert.False(result);
			Assert.NotNull(err);
			Assert.Contains("100", err);
		}
	}
}
