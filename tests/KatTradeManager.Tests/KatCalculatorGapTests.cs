using System;
using NinjaTrader.NinjaScript.Indicators;
using Xunit;

namespace KatTradeManager.Tests
{
	/// <summary>Gap coverage: edge paths not exercised by the existing suites.</summary>
	public class KatCalculatorGapTests
	{
		#region GetNySessionStartUtc — summer (EDT, UTC-4)
		[Fact]
		public void GetNySessionStartUtc_SummerTime_UsesEdtOffset()
		{
			// 2026-07-15 12:00 UTC = 08:00 EDT (before 6pm) -> session started previous day 18:00 EDT = 22:00 UTC
			DateTime nowUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
			DateTime sessionStart = KatTradeCalculator.GetNySessionStartUtc(nowUtc);
			Assert.Equal(new DateTime(2026, 7, 14, 22, 0, 0), sessionStart);
		}

		[Fact]
		public void GetNySessionStartUtc_SummerAfter6pmNY_ReturnsSameDaySessionStart()
		{
			// 2026-07-16 01:00 UTC = 2026-07-15 21:00 EDT (after 6pm) -> session start 2026-07-15 18:00 EDT = 22:00 UTC
			DateTime nowUtc = new DateTime(2026, 7, 16, 1, 0, 0, DateTimeKind.Utc);
			DateTime sessionStart = KatTradeCalculator.GetNySessionStartUtc(nowUtc);
			Assert.Equal(new DateTime(2026, 7, 15, 22, 0, 0), sessionStart);
		}
		#endregion

		#region ValidateEmaPlace — defensive edges
		[Fact]
		public void ValidateEmaPlace_NullOrEmptyArray_AlwaysValid()
		{
			Assert.True(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Buy, 100.0, null, out string err1));
			Assert.Null(err1);
			Assert.True(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Sell, 100.0, new double[0], out string err2));
			Assert.Null(err2);
		}

		[Fact]
		public void ValidateEmaPlace_ZeroOrNegativeEmaValues_AreSkipped()
		{
			// EMA not yet initialized returns 0 — must not reject a valid entry
			Assert.True(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Buy, 100.0, new[] { 0.0, -5.0 }, out _));
			Assert.True(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Sell, 100.0, new[] { 0.0 }, out _));
		}

		[Fact]
		public void ValidateEmaPlace_MixedValidAndUninitialized_OnlyChecksValid()
		{
			Assert.True(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Buy, 100.0, new[] { 0.0, 90.0 }, out _));
			Assert.False(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Buy, 100.0, new[] { 0.0, 110.0 }, out _));
		}
		#endregion

		#region ValidateEmaAngle — defensive edges
		[Fact]
		public void ValidateEmaAngle_NullArrays_AlwaysValid()
		{
			Assert.True(KatTradeCalculator.ValidateEmaAngle(KatOrderAction.Buy, null, null, null, 0.25, out string err));
			Assert.Null(err);
		}

		[Fact]
		public void ValidateEmaAngle_ZeroMinAngle_SkipsCheck()
		{
			// Flat EMA would fail any positive threshold, but 0° requirement = disabled
			Assert.True(KatTradeCalculator.ValidateEmaAngle(KatOrderAction.Buy,
				new[] { 100.0 }, new[] { 100.0 }, new[] { 0.0 }, 0.25, out _));
		}

		[Fact]
		public void ValidateEmaAngle_MismatchedArrayLengths_UsesShortest()
		{
			// Extra entries beyond shortest length are ignored, no out-of-range
			Assert.True(KatTradeCalculator.ValidateEmaAngle(KatOrderAction.Buy,
				new[] { 100.25, 1.0 }, new[] { 100.0 }, new[] { 30.0, 99.0, 99.0 }, 0.25, out _));
		}
		#endregion

		#region CalculateEmaAngle — fallback & exact values
		[Fact]
		public void CalculateEmaAngle_OneTickPerBar_Returns45Degrees()
		{
			double angle = KatTradeCalculator.CalculateEmaAngle(100.25, 100.0, 0.25);
			Assert.Equal(45.0, angle);
		}

		[Fact]
		public void CalculateEmaAngle_ZeroTickSize_FallsBackToQuarterTick()
		{
			// 0.25 rise with fallback tick 0.25 -> atan(1) = 45°
			double angle = KatTradeCalculator.CalculateEmaAngle(100.25, 100.0, 0.0);
			Assert.Equal(45.0, angle);
		}
		#endregion

		#region IsAccountAllowed — separator edges
		[Fact]
		public void IsAccountAllowed_SemicolonSeparator_WorksLikeComma()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "Playback;Sim"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("BX999", "Sim;Playback"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("BX999", "Sim;!BX"));
		}

		[Fact]
		public void IsAccountAllowed_WhitespaceOnlyFilter_AllowsAll()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "  , ; ,  "));
		}
		#endregion

		#region CalculatePartialCandlePrice — percent edges
		[Fact]
		public void CalculatePartialCandlePrice_ZeroOrNegativePercent_DefaultsTo30()
		{
			double expected = KatTradeCalculator.CalculatePartialCandlePrice(KatOrderAction.Buy, 110.0, 100.0, 30.0, 0.25);
			Assert.Equal(expected, KatTradeCalculator.CalculatePartialCandlePrice(KatOrderAction.Buy, 110.0, 100.0, 0.0, 0.25));
			Assert.Equal(expected, KatTradeCalculator.CalculatePartialCandlePrice(KatOrderAction.Buy, 110.0, 100.0, -15.0, 0.25));
		}

		[Fact]
		public void CalculatePartialCandlePrice_PercentAbove100_ExtrapolatesBeyondRange()
		{
			// Buy 150%: high - range * 1.5 = 110 - 15 = 95 (below the low) — formula must not clamp
			double price = KatTradeCalculator.CalculatePartialCandlePrice(KatOrderAction.Buy, 110.0, 100.0, 150.0, 0.25);
			Assert.Equal(95.0, price);
		}
		#endregion
	}
}
