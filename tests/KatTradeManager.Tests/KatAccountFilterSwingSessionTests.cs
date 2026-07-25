using System;
using System.Collections.Generic;
using NinjaTrader.NinjaScript.Indicators;
using Xunit;

namespace KatTradeManager.Tests
{
	public class KatAccountFilterSwingSessionTests
	{
		#region IsAccountAllowed
		[Fact]
		public void IsAccountAllowed_EmptyFilter_AllowsAnyAccount()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", ""));
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", null));
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "   "));
		}

		[Fact]
		public void IsAccountAllowed_NullOrEmptyAccountName_Rejected()
		{
			Assert.False(KatTradeCalculator.IsAccountAllowed(null, ""));
			Assert.False(KatTradeCalculator.IsAccountAllowed("", ""));
			Assert.False(KatTradeCalculator.IsAccountAllowed("  ", ""));
		}

		[Fact]
		public void IsAccountAllowed_IncludeTokens_MatchCaseInsensitiveSubstring()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "sim"));
			Assert.True(KatTradeCalculator.IsAccountAllowed("Account79424", "79424, Sim101"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("Playback101", "79424, Sim101"));
		}

		[Fact]
		public void IsAccountAllowed_ExcludeTokens_RejectMatches()
		{
			Assert.False(KatTradeCalculator.IsAccountAllowed("BX12345", "!BX, !LTE"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("MyLTEAccount", "!bx, !lte"));
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "!BX, !LTE"));
		}

		[Fact]
		public void IsAccountAllowed_ExcludeWinsOverInclude()
		{
			Assert.False(KatTradeCalculator.IsAccountAllowed("SimBX", "Sim, !BX"));
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "Sim, !BX"));
		}

		[Fact]
		public void IsAccountAllowed_OnlyExcludes_AllowsNonExcluded()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Anything", "!ZZZ"));
		}

		[Fact]
		public void IsAccountAllowed_BangAlone_IsIgnoredAsToken()
		{
			// "!" with no text is neither include nor exclude — treated as no-op
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "!"));
		}
		#endregion

		#region FindSwingPoints
		// Series indexed by barsAgo (0 = current bar)
		private static double[] BuildLowSeries()
		{
			return new double[] { 10, 10, 10, 11, 11, 9, 11, 11, 12, 12, 8, 12, 12, 12, 12 };
		}

		[Fact]
		public void FindSwingPoints_SwingLows_FoundMostRecentFirst()
		{
			List<double> swings = KatTradeCalculator.FindSwingPoints(BuildLowSeries(), true, 20, 2, 0.25);
			Assert.Equal(3, swings.Count);
			Assert.Equal(10, swings[0]);
			Assert.Equal(9, swings[1]);
			Assert.Equal(8, swings[2]);
		}

		[Fact]
		public void FindSwingPoints_SwingHighs_FoundOnInvertedLogic()
		{
			// idx5 high 13 with lower neighbors (strength 2)
			double[] highs = new double[] { 10, 10, 10, 11, 11, 13, 11, 11, 10, 10, 10, 10, 10, 10, 10 };
			List<double> swings = KatTradeCalculator.FindSwingPoints(highs, false, 20, 2, 0.25);
			Assert.Contains(13, swings);
			Assert.Equal(13, swings[0]);
		}

		[Fact]
		public void FindSwingPoints_DuplicatesWithinOneTick_Deduplicated()
		{
			// two identical swing lows 9 at idx5 and idx10 — second must be skipped
			double[] lows = new double[] { 12, 12, 12, 11, 11, 9, 11, 11, 11, 11, 9, 11, 11, 12, 12 };
			List<double> swings = KatTradeCalculator.FindSwingPoints(lows, true, 20, 2, 0.25);
			int count9 = 0;
			foreach (double s in swings) if (Math.Abs(s - 9) < 0.001) count9++;
			Assert.Equal(1, count9);
		}

		[Fact]
		public void FindSwingPoints_MaxSwings_Respected()
		{
			List<double> swings = KatTradeCalculator.FindSwingPoints(BuildLowSeries(), true, 1, 2, 0.25);
			Assert.Single(swings);
		}

		[Fact]
		public void FindSwingPoints_ShortOrNullSeries_ReturnsEmpty()
		{
			Assert.Empty(KatTradeCalculator.FindSwingPoints(null, true, 20, 2, 0.25));
			Assert.Empty(KatTradeCalculator.FindSwingPoints(new double[] { 1, 2, 3 }, true, 20, 2, 0.25));
			Assert.Empty(KatTradeCalculator.FindSwingPoints(BuildLowSeries(), true, 20, 0, 0.25));
		}
		#endregion

		#region GetNySessionStartUtc
		[Fact]
		public void GetNySessionStartUtc_After6pmNY_ReturnsSameDaySessionStart()
		{
			// 2026-07-25 23:00 UTC = 19:00 EDT (after 18:00 cutoff) — session started 18:00 EDT = 22:00 UTC
			DateTime nowUtc = new DateTime(2026, 7, 25, 23, 0, 0, DateTimeKind.Utc);
			DateTime sessionStart = KatTradeCalculator.GetNySessionStartUtc(nowUtc);
			Assert.Equal(new DateTime(2026, 7, 25, 22, 0, 0, DateTimeKind.Utc), sessionStart);
		}

		[Fact]
		public void GetNySessionStartUtc_Before6pmNY_ReturnsPreviousDaySessionStart()
		{
			// 2026-07-25 20:00 UTC = 16:00 EDT (before 18:00 cutoff) — session started 2026-07-24 18:00 EDT = 22:00 UTC
			DateTime nowUtc = new DateTime(2026, 7, 25, 20, 0, 0, DateTimeKind.Utc);
			DateTime sessionStart = KatTradeCalculator.GetNySessionStartUtc(nowUtc);
			Assert.Equal(new DateTime(2026, 7, 24, 22, 0, 0, DateTimeKind.Utc), sessionStart);
		}

		[Fact]
		public void GetNySessionStartUtc_ExactlyAt6pmNY_ReturnsSameDaySessionStart()
		{
			// 2026-07-25 22:00 UTC = exactly 18:00 EDT — boundary is inclusive (>= 18:00)
			DateTime nowUtc = new DateTime(2026, 7, 25, 22, 0, 0, DateTimeKind.Utc);
			DateTime sessionStart = KatTradeCalculator.GetNySessionStartUtc(nowUtc);
			Assert.Equal(new DateTime(2026, 7, 25, 22, 0, 0, DateTimeKind.Utc), sessionStart);
		}

		[Fact]
		public void GetNySessionStartUtc_WinterTime_UsesEstOffset()
		{
			// 2026-01-15 00:00 UTC = 2026-01-14 19:00 EST (after 18:00) — session start 18:00 EST = 23:00 UTC
			DateTime nowUtc = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
			DateTime sessionStart = KatTradeCalculator.GetNySessionStartUtc(nowUtc);
			Assert.Equal(new DateTime(2026, 1, 14, 23, 0, 0, DateTimeKind.Utc), sessionStart);
		}
		#endregion
	}
}
