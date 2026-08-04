using System;
using System.Collections.Generic;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatEntryShiftTests
	{
		private static readonly DateTime T0 = new DateTime(2026, 8, 3, 10, 0, 0);
		private static readonly DateTime T1 = new DateTime(2026, 8, 3, 10, 1, 0);
		private static readonly DateTime T2 = new DateTime(2026, 8, 3, 10, 2, 0);
		private static readonly DateTime T3 = new DateTime(2026, 8, 3, 10, 3, 0);

		[Fact]
		public void CalculateShiftedBarIndex_ShiftBackward_IncrementsIndex()
		{
			List<DateTime> times = new List<DateTime> { T3, T2, T1, T0 };
			// Start at T3 (index 0). Shift backward (isForward = false) -> target index 1 (T2)
			int targetIdx = KatTradeCalculator.CalculateShiftedBarIndex(times, T3, 0, false, out string boundary);

			Assert.Equal(1, targetIdx);
			Assert.Null(boundary);
		}

		[Fact]
		public void CalculateShiftedBarIndex_ShiftForward_DecrementsIndex()
		{
			List<DateTime> times = new List<DateTime> { T3, T2, T1, T0 };
			// Start at T1 (index 2). Shift forward (isForward = true) -> target index 1 (T2)
			int targetIdx = KatTradeCalculator.CalculateShiftedBarIndex(times, T1, 2, true, out string boundary);

			Assert.Equal(1, targetIdx);
			Assert.Null(boundary);
		}

		[Fact]
		public void CalculateShiftedBarIndex_TimestampMatch_HandlesNewBarsArrival()
		{
			// Suppose T4 and T5 arrived after order was placed at T2
			DateTime T4 = new DateTime(2026, 8, 3, 10, 4, 0);
			DateTime T5 = new DateTime(2026, 8, 3, 10, 5, 0);
			List<DateTime> updatedTimes = new List<DateTime> { T5, T4, T3, T2, T1, T0 };

			// Active order timestamp was T2 (now at index 3).
			// Shift backward (isForward = false) -> should target T1 (index 4)
			int targetIdx = KatTradeCalculator.CalculateShiftedBarIndex(updatedTimes, T2, 0, false, out string boundary);

			Assert.Equal(4, targetIdx);
			Assert.Equal(T1, updatedTimes[targetIdx]);
			Assert.Null(boundary);
		}

		[Fact]
		public void CalculateShiftedBarIndex_ReachedNewest_ReturnsNegativeOne()
		{
			List<DateTime> times = new List<DateTime> { T3, T2, T1, T0 };
			// Start at T3 (index 0). Shift forward -> boundary reached
			int targetIdx = KatTradeCalculator.CalculateShiftedBarIndex(times, T3, 0, true, out string boundary);

			Assert.Equal(-1, targetIdx);
			Assert.Equal("REACHED_NEWEST", boundary);
		}

		[Fact]
		public void CalculateShiftedBarIndex_ReachedOldest_ReturnsNegativeOne()
		{
			List<DateTime> times = new List<DateTime> { T3, T2, T1, T0 };
			// Start at T0 (index 3). Shift backward -> boundary reached
			int targetIdx = KatTradeCalculator.CalculateShiftedBarIndex(times, T0, 3, false, out string boundary);

			Assert.Equal(-1, targetIdx);
			Assert.Equal("REACHED_OLDEST", boundary);
		}

		[Fact]
		public void CalculateShiftedBarIndex_FallbackIndex_UsedWhenTimestampNotFound()
		{
			List<DateTime> times = new List<DateTime> { T3, T2, T1, T0 };
			DateTime unknownTime = new DateTime(2026, 8, 3, 9, 0, 0);

			// Fallback to index 1 (T2). Shift backward (isForward = false) -> index 2 (T1)
			int targetIdx = KatTradeCalculator.CalculateShiftedBarIndex(times, unknownTime, 1, false, out string boundary);

			Assert.Equal(2, targetIdx);
			Assert.Null(boundary);
		}

		[Fact]
		public void CalculateShiftedBarIndex_MaxBarsAgoBoundary_MatchesOldestBarTimestamp()
		{
			// Verify that when barsAgo equals currentBars index (oldest bar T0 at index 3), timestamp lookup succeeds
			List<DateTime> times = new List<DateTime> { T3, T2, T1, T0 };
			int targetIdx = KatTradeCalculator.CalculateShiftedBarIndex(times, T0, 3, true, out string boundary);

			// Shifting forward from oldest bar T0 (index 3) should yield index 2 (T1)
			Assert.Equal(2, targetIdx);
			Assert.Equal(T1, times[targetIdx]);
			Assert.Null(boundary);
		}
	}
}
