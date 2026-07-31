using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatDailyRiskTests
	{
		[Fact]
		public void EvaluateDailyRiskBreach_OffToggles_NeverBreach()
		{
			// Regression: OFF toggles must never flatten, no matter how deep the drawdown/profit.
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(false, 500.0, false, 1000.0, -50000.0, out _));
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(false, 500.0, false, 1000.0, 99999.0, out _));
		}

		[Fact]
		public void EvaluateDailyRiskBreach_MaxDDBreach_WhenEnabledAndBeyondLimit()
		{
			Assert.True(KatTradeCalculator.EvaluateDailyRiskBreach(true, 500.0, false, 1000.0, -500.0, out string reason));
			Assert.Contains("Max DD", reason);
			Assert.True(KatTradeCalculator.EvaluateDailyRiskBreach(true, 500.0, false, 1000.0, -750.25, out _));
		}

		[Fact]
		public void EvaluateDailyRiskBreach_MaxDDNoBreach_WithinLimit()
		{
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(true, 500.0, false, 1000.0, -499.99, out _));
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(true, 500.0, false, 1000.0, 0.0, out _));
		}

		[Fact]
		public void EvaluateDailyRiskBreach_MaxProfitBreach_WhenEnabledAndReached()
		{
			Assert.True(KatTradeCalculator.EvaluateDailyRiskBreach(false, 500.0, true, 1000.0, 1000.0, out string reason));
			Assert.Contains("Max Profit", reason);
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(false, 500.0, true, 1000.0, 999.99, out _));
		}

		[Fact]
		public void EvaluateDailyRiskBreach_ZeroLimits_NeverBreach()
		{
			// Limit of 0 = disabled even when the toggle is ON.
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(true, 0.0, true, 0.0, -100000.0, out _));
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(true, 0.0, true, 0.0, 100000.0, out _));
		}

		[Fact]
		public void EvaluateDailyRiskBreach_NegativeDDLimit_DisabledLikeZero()
		{
			// Property is Range(0, 1000000); a non-positive limit means disabled, matching the legacy gate.
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(true, -500.0, false, 0.0, -50000.0, out _));
		}

		[Fact]
		public void EvaluateDailyRiskBreach_NoBreach_EmptyReason()
		{
			Assert.False(KatTradeCalculator.EvaluateDailyRiskBreach(true, 500.0, true, 1000.0, 100.0, out string reason));
			Assert.Equal(string.Empty, reason);
		}
	}
}
