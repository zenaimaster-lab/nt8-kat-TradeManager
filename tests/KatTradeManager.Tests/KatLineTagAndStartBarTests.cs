using System.Linq;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatLineTagAndStartBarTests
	{
		[Fact]
		public void LineTags_AllUnique_NoDuplicates()
		{
			string[] tags = KatTradeCalculator.LineTags;
			Assert.Equal(tags.Length, tags.Distinct().Count());
		}

		[Fact]
		public void LineTags_ContainsAllSixOrderLines()
		{
			Assert.Equal(6, KatTradeCalculator.LineTags.Length);
			Assert.Contains("KAT_ENTRY_LINE", KatTradeCalculator.LineTags);
			Assert.Contains("KAT_SL_LINE", KatTradeCalculator.LineTags);
			Assert.Contains("KAT_TP_LINE", KatTradeCalculator.LineTags);
			Assert.Contains("KAT_BE_LINE", KatTradeCalculator.LineTags);
			Assert.Contains("KAT_SL1_LINE", KatTradeCalculator.LineTags);
			Assert.Contains("KAT_SL2_LINE", KatTradeCalculator.LineTags);
		}

		[Fact]
		public void LineTags_EntryIsFirst_MatchesDrawOrder()
		{
			// DrawExpectedLines relies on index 0 = entry tag
			Assert.Equal("KAT_ENTRY_LINE", KatTradeCalculator.LineTags[0]);
		}

		[Theory]
		[InlineData(0, 20, 0)]     // first bar: anchor must not exceed currentBar
		[InlineData(1, 20, 1)]
		[InlineData(5, 20, 5)]
		[InlineData(20, 20, 20)]
		[InlineData(100, 20, 20)]  // clamped to max
		[InlineData(-3, 20, 0)]    // invalid bar -> safe 0
		public void GetLineStartBar_NeverExceedsCurrentBar_NeverNegative(int currentBar, int maxBarsAgo, int expected)
		{
			int result = KatTradeCalculator.GetLineStartBar(currentBar, maxBarsAgo);
			Assert.Equal(expected, result);
			Assert.InRange(result, 0, maxBarsAgo);
			if (currentBar >= 0)
				Assert.True(result <= currentBar, "barsAgo anchor must be <= currentBar");
		}
	}
}
