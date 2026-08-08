using System;
using System.Collections.Generic;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatReauditP1Tests
	{
		[Theory]
		[InlineData("Sim101", "Sim101, Apex", true)]
		[InlineData("Apex123", "Sim101, Apex", true)]
		[InlineData("Other", "Sim101, Apex", false)]
		[InlineData("Sim101", "!Sim101", false)]
		[InlineData("Sim101", "!Apex", true)]
		[InlineData("MySim101", "Sim101 ; !MySim101", false)]
		public void IsAccountAllowed_FilterSemantics(string acc, string filter, bool allowed)
		{
			Assert.Equal(allowed, KatTradeCalculator.IsAccountAllowed(acc, filter));
		}

		[Fact]
		public void IsAccountAllowed_ExcludeWinsOverInclude()
		{
			Assert.False(KatTradeCalculator.IsAccountAllowed("Sim101_Apex", "Sim101, !Apex"));
		}

		[Fact]
		public void IsAccountAllowed_CaseInsensitive()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("sim101", "SIM101"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("SIM101", "!sim101"));
		}

		[Fact]
		public void PlanAtmBracketMerge_PicksLargestQtyOco()
		{
			var orders = new List<KatTradeCalculator.KatAtmBracketOrder>
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "A", IsStop = true, Quantity = 1, Price = 99 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "A", IsStop = false, Quantity = 1, Price = 101 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "B", IsStop = true, Quantity = 5, Price = 98 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "B", IsStop = false, Quantity = 5, Price = 102 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 5);
			Assert.False(plan.IsNoop);
			Assert.Equal(5, plan.DesiredStopQuantity);
			Assert.Equal(2, plan.CancelIndices.Length); // A pair cancelled
		}

		[Fact]
		public void PlanAtmBracketMerge_IncompleteOco_IsNoop()
		{
			var orders = new List<KatTradeCalculator.KatAtmBracketOrder>
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "A", IsStop = true, Quantity = 1, Price = 99 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "B", IsStop = false, Quantity = 1, Price = 101 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 1);
			Assert.True(plan.IsNoop);
		}

		[Fact]
		public void PlanAtmBracketMerge_NullQtyOrderIgnored()
		{
			var orders = new List<KatTradeCalculator.KatAtmBracketOrder>
			{
				null,
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "A", IsStop = true, Quantity = 0, Price = 99 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "A", IsStop = false, Quantity = 1, Price = 101 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 1);
			Assert.True(plan.IsNoop); // no complete pair with qty>0 both sides
		}

		[Fact]
		public void CalculateShiftedBarIndex_Boundaries()
		{
			var times = new List<DateTime> { new DateTime(2026, 1, 1, 10, 0, 0), new DateTime(2026, 1, 1, 10, 1, 0), new DateTime(2026, 1, 1, 10, 2, 0) };
			int idx = KatTradeCalculator.CalculateShiftedBarIndex(times, times[0], 0, true, out string s);
			Assert.Equal(-1, idx); Assert.Equal("REACHED_NEWEST", s);
			idx = KatTradeCalculator.CalculateShiftedBarIndex(times, times[2], 2, false, out s);
			Assert.Equal(-1, idx); Assert.Equal("REACHED_OLDEST", s);
			idx = KatTradeCalculator.CalculateShiftedBarIndex(times, times[1], 1, false, out s);
			Assert.Equal(2, idx); Assert.Null(s);
		}

		[Fact]
		public void CalculateShiftedBarIndex_EmptyReturnsEmpty()
		{
			int idx = KatTradeCalculator.CalculateShiftedBarIndex(null, DateTime.Now, 0, false, out string s);
			Assert.Equal(-1, idx); Assert.Equal("EMPTY", s);
		}

		[Fact]
		public void FindSwingPoints_DedupWithinTick()
		{
			double[] series = new double[] { 12, 12, 12, 11, 11, 9, 11, 11, 11, 11, 9, 11, 11, 12, 12, 12, 12, 12, 12, 12 };
			var lows = KatTradeCalculator.FindSwingPoints(series, true, 10, 2, 0.25);
			int count9 = 0; foreach (var v in lows) if (Math.Abs(v - 9) < 0.001) count9++;
			Assert.Equal(1, count9);
		}

		[Fact]
		public void FindNextSwingStopPrice_Threshold()
		{
			var swings = new[] { 99.9, 99.0, 98.0 };
			double next = KatTradeCalculator.FindNextSwingStopPrice(swings, KatOrderAction.Buy, 100.0, 0.25);
			Assert.Equal(99.0, next); // 99.9 within 0.125 skipped
			next = KatTradeCalculator.FindNextSwingStopPrice(swings, KatOrderAction.Buy, 99.91, 0.25);
			Assert.Equal(99.0, next);
			next = KatTradeCalculator.FindNextSwingStopPrice(swings, KatOrderAction.Buy, 99.0, 0.25);
			Assert.Equal(98.0, next);
		}

		[Fact]
		public void ShouldCaptureSessionBaseline_Guard()
		{
			Assert.False(KatTradeCalculator.ShouldCaptureSessionBaseline(true, DateTime.UtcNow, DateTime.UtcNow.AddHours(-1), false));
			Assert.True(KatTradeCalculator.ShouldCaptureSessionBaseline(false, DateTime.UtcNow, DateTime.MinValue, true));
		}
	}
}
