using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatFreezeTrailTests
	{
		[Fact]
		public void IsPreferredFreezePrice_LongStops_KeepsTightestProtection()
		{
			Assert.True(KatTradeCalculator.IsPreferredFreezePrice(true, 4010.0, 4000.0));
			Assert.False(KatTradeCalculator.IsPreferredFreezePrice(true, 3990.0, 4000.0));
		}

		[Fact]
		public void IsPreferredFreezePrice_ShortStops_KeepsTightestProtection()
		{
			Assert.True(KatTradeCalculator.IsPreferredFreezePrice(false, 3990.0, 4000.0));
			Assert.False(KatTradeCalculator.IsPreferredFreezePrice(false, 4010.0, 4000.0));
		}

		[Fact]
		public void IsPreferredFreezePrice_Targets_KeepsFarthestExit()
		{
			// Same comparison serves targets: Long keeps the higher price, Short the lower one.
			Assert.True(KatTradeCalculator.IsPreferredFreezePrice(true, 4025.0, 4015.0));
			Assert.True(KatTradeCalculator.IsPreferredFreezePrice(false, 3975.0, 3985.0));
		}

		[Fact]
		public void IsPreferredFreezePrice_FirstValidCandidate_ReplacesMissingPrice()
		{
			Assert.True(KatTradeCalculator.IsPreferredFreezePrice(true, 4000.0, 0.0));
			Assert.True(KatTradeCalculator.IsPreferredFreezePrice(false, 4000.0, -1.0));
		}

		[Fact]
		public void IsPreferredFreezePrice_InvalidCandidate_IsRejected()
		{
			Assert.False(KatTradeCalculator.IsPreferredFreezePrice(true, 0.0, 4000.0));
			Assert.False(KatTradeCalculator.IsPreferredFreezePrice(true, double.NaN, 4000.0));
			Assert.False(KatTradeCalculator.IsPreferredFreezePrice(false, double.PositiveInfinity, 4000.0));
		}

		[Fact]
		public void ShouldAdjustFreezeQuantity_MirrorsPositionQuantity()
		{
			Assert.True(KatTradeCalculator.ShouldAdjustFreezeQuantity(2, 4));  // scale-in
			Assert.True(KatTradeCalculator.ShouldAdjustFreezeQuantity(4, 2));  // scale-out
			Assert.False(KatTradeCalculator.ShouldAdjustFreezeQuantity(3, 3));
			Assert.False(KatTradeCalculator.ShouldAdjustFreezeQuantity(3, 0)); // flat handled by cleanup
		}

		[Fact]
		public void ShouldCancelFreezeOrphans_WaitsOutTransientFlat()
		{
			Assert.False(KatTradeCalculator.ShouldCancelFreezeOrphans(false, 10000, 3000));
			Assert.False(KatTradeCalculator.ShouldCancelFreezeOrphans(true, 1500, 3000));
			Assert.False(KatTradeCalculator.ShouldCancelFreezeOrphans(true, -1, 3000));
			Assert.True(KatTradeCalculator.ShouldCancelFreezeOrphans(true, 3000, 3000));
			Assert.True(KatTradeCalculator.ShouldCancelFreezeOrphans(true, 9000, 3000));
		}

		[Fact]
		public void ShouldSubmitFreezeLeg_NeverDuplicatesActiveLeg()
		{
			// Regression: re-detach on every ATM re-trail used to stack duplicate SL/TP pairs.
			Assert.False(KatTradeCalculator.ShouldSubmitFreezeLeg(true, true, true));   // already protected
			Assert.True(KatTradeCalculator.ShouldSubmitFreezeLeg(false, true, true));   // fresh submit
			Assert.False(KatTradeCalculator.ShouldSubmitFreezeLeg(false, false, true)); // nothing captured
			Assert.False(KatTradeCalculator.ShouldSubmitFreezeLeg(false, true, false)); // stale price, would be rejected
		}

		[Fact]
		public void IsLimitOnValidSide_LongTarget_MustBeAboveMarket()
		{
			Assert.True(KatTradeCalculator.IsLimitOnValidSide(true, 4010.0, 4000.0));
			Assert.False(KatTradeCalculator.IsLimitOnValidSide(true, 3990.0, 4000.0));
			Assert.False(KatTradeCalculator.IsLimitOnValidSide(true, 4000.0, 4000.0));
		}

		[Fact]
		public void IsLimitOnValidSide_ShortTarget_MustBeBelowMarket()
		{
			Assert.True(KatTradeCalculator.IsLimitOnValidSide(false, 3990.0, 4000.0));
			Assert.False(KatTradeCalculator.IsLimitOnValidSide(false, 4010.0, 4000.0));
			Assert.False(KatTradeCalculator.IsLimitOnValidSide(false, 4000.0, 4000.0));
		}

		[Fact]
		public void IsLimitOnValidSide_InvalidInputs_AreRejected()
		{
			Assert.False(KatTradeCalculator.IsLimitOnValidSide(true, 0.0, 4000.0));
			Assert.False(KatTradeCalculator.IsLimitOnValidSide(true, 4010.0, 0.0));
			Assert.False(KatTradeCalculator.IsLimitOnValidSide(false, -5.0, 4000.0));
		}
	}
}
