using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatEmaPlaceAndAngleTests
	{
		[Fact]
		public void CalculateEmaAngle_UpwardSlope_ReturnsPositiveAngle()
		{
			// 1 tick increase over 1 bar with tickSize = 0.25 -> deltaTicks = 1 -> tan(1) = 45 degrees
			double angle = KatTradeCalculator.CalculateEmaAngle(100.25, 100.00, 0.25);
			Assert.Equal(45.0, angle, 1);
		}

		[Fact]
		public void CalculateEmaAngle_DownwardSlope_ReturnsNegativeAngle()
		{
			// 1 tick decrease over 1 bar with tickSize = 0.25 -> deltaTicks = -1 -> tan(-1) = -45 degrees
			double angle = KatTradeCalculator.CalculateEmaAngle(99.75, 100.00, 0.25);
			Assert.Equal(-45.0, angle, 1);
		}

		[Fact]
		public void CalculateEmaAngle_FlatSlope_ReturnsZero()
		{
			double angle = KatTradeCalculator.CalculateEmaAngle(100.00, 100.00, 0.25);
			Assert.Equal(0.0, angle, 1);
		}

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

		[Fact]
		public void ValidateEmaAngle_BuyOrder_ValidWhenSlopeExceedsMinAngles()
		{
			// 1 tick diff -> 45 deg, min required = 35 deg, 30 deg, 15 deg
			double[] currEmas = new double[] { 100.25, 100.25, 100.25 };
			double[] prevEmas = new double[] { 100.00, 100.00, 100.00 };
			double[] minAngles = new double[] { 35.0, 30.0, 15.0 };

			bool result = KatTradeCalculator.ValidateEmaAngle(KatOrderAction.Buy, currEmas, prevEmas, minAngles, 0.25, out string err);

			Assert.True(result);
			Assert.Null(err);
		}

		[Fact]
		public void ValidateEmaAngle_BuyOrder_InvalidWhenSlopeBelowMinAngle()
		{
			// Flat slope (0 deg), min required = 35 deg
			double[] currEmas = new double[] { 100.00, 100.00, 100.00 };
			double[] prevEmas = new double[] { 100.00, 100.00, 100.00 };
			double[] minAngles = new double[] { 35.0, 30.0, 15.0 };

			bool result = KatTradeCalculator.ValidateEmaAngle(KatOrderAction.Buy, currEmas, prevEmas, minAngles, 0.25, out string err);

			Assert.False(result);
			Assert.NotNull(err);
			Assert.Contains("angle 0.0° < required 35°", err);
		}

		[Fact]
		public void ValidateEmaAngle_SellOrder_ValidWhenDownwardSlopeExceedsMinAngles()
		{
			// Downward slope: curr < prev -> 1 tick drop -> downward angle = 45 deg
			double[] currEmas = new double[] { 99.75, 99.75, 99.75 };
			double[] prevEmas = new double[] { 100.00, 100.00, 100.00 };
			double[] minAngles = new double[] { 35.0, 30.0, 15.0 };

			bool result = KatTradeCalculator.ValidateEmaAngle(KatOrderAction.Sell, currEmas, prevEmas, minAngles, 0.25, out string err);

			Assert.True(result);
			Assert.Null(err);
		}
	}
}
