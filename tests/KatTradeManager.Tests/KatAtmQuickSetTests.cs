using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatAtmQuickSetTests
	{
		[Fact]
		public void NormalizeAtmSetName_WithinLimit_Kept()
		{
			Assert.Equal("A", KatTradeCalculator.NormalizeAtmSetName("A", "F"));
			Assert.Equal("ABC", KatTradeCalculator.NormalizeAtmSetName("ABC", "F"));
			Assert.Equal("1x", KatTradeCalculator.NormalizeAtmSetName("1x", "F"));
		}

		[Fact]
		public void NormalizeAtmSetName_OverThreeChars_Truncated()
		{
			Assert.Equal("SCA", KatTradeCalculator.NormalizeAtmSetName("SCALP", "F"));
			Assert.Equal("ABC", KatTradeCalculator.NormalizeAtmSetName("ABCDE", "F"));
		}

		[Fact]
		public void NormalizeAtmSetName_EmptyOrWhitespace_FallsBack()
		{
			Assert.Equal("", KatTradeCalculator.NormalizeAtmSetName("", "B")); // explicit empty stays empty per request
			Assert.Equal("", KatTradeCalculator.NormalizeAtmSetName("   ", "C"));
			Assert.Equal("D", KatTradeCalculator.NormalizeAtmSetName(null, "D")); // only null falls back
		}

		[Fact]
		public void NormalizeAtmSetName_SurroundingWhitespace_Trimmed()
		{
			Assert.Equal("TP", KatTradeCalculator.NormalizeAtmSetName("  TP ", "F"));
			Assert.Equal("ABC", KatTradeCalculator.NormalizeAtmSetName(" ABCD ", "F"));
		}
	}
}
