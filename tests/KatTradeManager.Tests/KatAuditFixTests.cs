using System;
using System.Collections.Generic;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatAuditFixTests
	{
		// ResolveTickSize — P1-1 GetEffectiveTickSize pure helper
		[Theory]
		[InlineData(0.25, 0.01, 0.25)] // cached zero -> instrument
		[InlineData(0.5, 0.01, 0.5)]  // cached wins
		[InlineData(0, 0, 0.25)]      // both zero -> fallback
		[InlineData(0, 0, 0.01)]      // fallback 0.01
		public void ResolveTickSize_PicksCorrect(double cached, double instrument, double fallback)
		{
			double result = KatTradeCalculator.ResolveTickSize(cached, instrument, fallback);
			Assert.Equal(cached > 0 ? cached : (instrument > 0 ? instrument : fallback), result);
		}

		[Fact]
		public void ResolveTickSize_DefaultFallback_Is025()
		{
			Assert.Equal(0.25, KatTradeCalculator.ResolveTickSize(0, 0));
			Assert.Equal(0.01, KatTradeCalculator.ResolveTickSize(0, 0, 0.01));
		}

		// Oco empty group ceiling — keep merged per existing test expectation
		[Fact]
		public void PlanAtmBracketMerge_EmptyOco_MergedAsOneGroup()
		{
			var orders = new[]
			{
				new KatTradeCalculator.KatAtmBracketOrder { Oco = null, IsStop = true, Quantity = 2, Price = 100 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "", IsStop = false, Quantity = 2, Price = 110 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = null, IsStop = true, Quantity = 1, Price = 100 },
				new KatTradeCalculator.KatAtmBracketOrder { Oco = "", IsStop = false, Quantity = 1, Price = 110 },
			};
			var plan = KatTradeCalculator.PlanAtmBracketMerge(orders, 4);
			Assert.False(plan.IsNoop);
			Assert.Equal(2, plan.CancelIndices.Length); // 2 duplicates cancelled
		}

		// Profile name normalization — truncation to 8 chars, fallback
		[Theory]
		[InlineData("  P1  ", "P1")]
		[InlineData("VeryLongProfileName", "VeryLong")] // 8 chars max
		[InlineData("", "P1")]
		[InlineData(null, "P1")]
		[InlineData("   ", "P1")]
		public void NormalizeProfileName_TrimsAndTruncates(string input, string expected)
		{
			Assert.Equal(expected, KatTradeCalculator.NormalizeProfileName(input, "P1"));
		}

		[Theory]
		[InlineData("A", "A")]
		[InlineData("ABCD", "ABC")] // AtmSet 3 chars max
		[InlineData("", "A")]
		public void NormalizeAtmSetName_TrimsTo3(string input, string expected)
		{
			Assert.Equal(expected, KatTradeCalculator.NormalizeAtmSetName(input, "A"));
		}

		// ClampHudCoordinate — keeps panel visible
		[Fact]
		public void ClampHudCoordinate_KeepsMinVisible()
		{
			double clamped = KatTradeCalculator.ClampHudCoordinate(-1000, 250, 800, 40);
			Assert.True(clamped >= -(250 - 40) - 0.01);
			clamped = KatTradeCalculator.ClampHudCoordinate(1000, 250, 800, 40);
			Assert.True(clamped <= (800 - 40) + 0.01);
		}

		// ShouldDefer — NaN and negative defer
		[Fact]
		public void ShouldDefer_NaN_Defer()
		{
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(false, false, false, double.NaN, 3000));
			Assert.True(KatTradeCalculator.ShouldDeferAtmFlatCleanup(false, false, false, -1, 3000));
		}

		// Trading windows — overnight
		[Fact]
		public void IsWithinTradingWindows_Overnight()
		{
			var windows = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 22, StartMinute = 0, EndHour = 2, EndMinute = 0 }
			};
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(23, 0, 0), windows));
			Assert.True(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(1, 0, 0), windows));
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(3, 0, 0), windows));
		}

		[Fact]
		public void IsWithinTradingWindows_ZeroLength_Disabled()
		{
			var windows = new List<KatTradeCalculator.KatTradingWindow>
			{
				new KatTradeCalculator.KatTradingWindow { Enabled = true, StartHour = 9, StartMinute = 0, EndHour = 9, EndMinute = 0 }
			};
			Assert.False(KatTradeCalculator.IsWithinTradingWindows(new TimeSpan(9, 0, 0), windows));
		}

		// Market priority — calculator IsScaleIn helper used by market gate
		[Fact]
		public void IsScaleIn_Long_BuyIsScaleIn()
		{
			Assert.True(KatTradeCalculator.IsScaleIn(true, KatOrderAction.Buy));
			Assert.False(KatTradeCalculator.IsScaleIn(true, KatOrderAction.Sell));
			Assert.True(KatTradeCalculator.IsScaleIn(false, KatOrderAction.Sell));
		}

		// Daily risk — breach off means never
		[Fact]
		public void EvaluateDailyRisk_OffNeverBreach()
		{
			bool breached = KatTradeCalculator.EvaluateDailyRiskBreach(false, 500, false, 1000, -10000, out string reason);
			Assert.False(breached);
			Assert.Equal(string.Empty, reason);
		}

		[Fact]
		public void AccountFilter_EmptyAllowsAll()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", ""));
			Assert.True(KatTradeCalculator.IsAccountAllowed("MySim101", "Sim101"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("Sim101", "!Sim101"));
		}
	}
}
