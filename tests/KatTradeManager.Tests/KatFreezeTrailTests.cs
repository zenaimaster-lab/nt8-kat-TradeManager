using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatFreezeTrailTests
	{
		[Fact]
		public void CalculateFrozenStopLimitPrice_LongPosition_PreservesLimitBelowFrozenStop()
		{
			double limit = KatTradeCalculator.CalculateFrozenStopLimitPrice(
				true, 100.0, 102.0, 101.0, 0.25);

			Assert.Equal(99.0, limit, 8);
		}

		[Fact]
		public void CalculateFrozenStopLimitPrice_ShortPosition_PreservesLimitAboveFrozenStop()
		{
			double limit = KatTradeCalculator.CalculateFrozenStopLimitPrice(
				false, 100.0, 98.0, 99.0, 0.25);

			Assert.Equal(101.0, limit, 8);
		}

		[Fact]
		public void CalculateFrozenStopLimitPrice_MultiTickOffset_PreservesExistingOffset()
		{
			double longLimit = KatTradeCalculator.CalculateFrozenStopLimitPrice(
				true, 4000.0, 4010.0, 4007.5, 0.25);
			double shortLimit = KatTradeCalculator.CalculateFrozenStopLimitPrice(
				false, 4000.0, 3990.0, 3992.5, 0.25);

			Assert.Equal(3997.5, longLimit, 8);
			Assert.Equal(4002.5, shortLimit, 8);
		}

		[Fact]
		public void CalculateFrozenStopLimitPrice_ZeroOffset_UsesTickFallback()
		{
			double longLimit = KatTradeCalculator.CalculateFrozenStopLimitPrice(
				true, 100.0, 102.0, 102.0, 0.25);
			double shortLimit = KatTradeCalculator.CalculateFrozenStopLimitPrice(
				false, 100.0, 98.0, 98.0, 0.25);

			Assert.Equal(99.75, longLimit, 8);
			Assert.Equal(100.25, shortLimit, 8);
		}

		[Fact]
		public void CalculateFrozenStopLimitPrice_InvalidTickFallback_UsesHundredth()
		{
			double limit = KatTradeCalculator.CalculateFrozenStopLimitPrice(
				true, 100.0, 102.0, 102.0, 0.0);

			Assert.Equal(99.99, limit, 8);
		}
	}
}
