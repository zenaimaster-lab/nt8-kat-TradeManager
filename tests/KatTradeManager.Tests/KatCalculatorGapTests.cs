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

		#region ParseFile — real file roundtrip
		[Fact]
		public void ParseFile_ValidTempFile_ParsesSameAsParseXml()
		{
			string xml = "<AtmStrategy><EntryQuantity>3</EntryQuantity><Brackets><Bracket>"
				+ "<StopLoss>20</StopLoss><Target>40</Target><Quantity>3</Quantity>"
				+ "<StopStrategy><AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>"
				+ "<AutoTrailSteps><AutoTrailStep><ProfitTrigger>15</ProfitTrigger></AutoTrailStep>"
				+ "<AutoTrailStep><ProfitTrigger>25</ProfitTrigger></AutoTrailStep></AutoTrailSteps>"
				+ "</StopStrategy></Bracket></Brackets></AtmStrategy>";
			string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kat_atm_test_" + System.Guid.NewGuid().ToString("N") + ".xml");
			try
			{
				System.IO.File.WriteAllText(path, xml);
				AtmTemplateData fromFile = KatAtmXmlParser.ParseFile(path);
				AtmTemplateData fromText = KatAtmXmlParser.ParseXml(xml);

				Assert.Equal(fromText.StopLoss, fromFile.StopLoss);
				Assert.Equal(fromText.Target, fromFile.Target);
				Assert.Equal(fromText.BETrigger, fromFile.BETrigger);
				Assert.Equal(fromText.SL1Trigger, fromFile.SL1Trigger);
				Assert.Equal(fromText.SL2Trigger, fromFile.SL2Trigger);
				Assert.Equal(fromText.Quantity, fromFile.Quantity);
				Assert.Equal(20, fromFile.StopLoss);
				Assert.Equal(3, fromFile.Quantity);
			}
			finally
			{
				if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
			}
		}

		[Fact]
		public void ParseFile_DirectoryPath_ReturnsDefaultSafely()
		{
			AtmTemplateData data = KatAtmXmlParser.ParseFile(System.IO.Path.GetTempPath());
			Assert.Equal(0, data.StopLoss);
			Assert.Equal(0, data.Quantity); // 0 = unspecified -> caller keeps user's quantity
		}
		#endregion

		#region FindSwingPoints — degenerate series
		[Fact]
		public void FindSwingPoints_FlatSeries_ReturnsSingleDeduplicatedSwing()
		{
			// Every bar qualifies as a "swing" on a flat series; dedup within 1 tick collapses to one
			double[] flat = new double[50];
			for (int i = 0; i < flat.Length; i++) flat[i] = 100.0;

			var lows = KatTradeCalculator.FindSwingPoints(flat, true, 20, 3, 0.25);
			Assert.Single(lows);
			Assert.Equal(100.0, lows[0]);
		}

		[Fact]
		public void FindSwingPoints_StrengthOne_FindsImmediateTurningPoint()
		{
			// barsAgo-indexed: [0]=5, [1]=3, [2]=5 -> swing low at 3 (barsAgo=1)
			double[] series = new double[] { 5.0, 3.0, 5.0 };
			var lows = KatTradeCalculator.FindSwingPoints(series, true, 20, 1, 0.25);
			Assert.Single(lows);
			Assert.Equal(3.0, lows[0]);

			var highs = KatTradeCalculator.FindSwingPoints(new double[] { 3.0, 5.0, 3.0 }, false, 20, 1, 0.25);
			Assert.Single(highs);
			Assert.Equal(5.0, highs[0]);
		}
		#endregion

		#region GetLineStartBar — zero bar
		[Fact]
		public void GetLineStartBar_ZeroCurrentBar_ReturnsZero()
		{
			Assert.Equal(0, KatTradeCalculator.GetLineStartBar(0, 20));
		}

		[Fact]
		public void GetLineStartBar_NegativeMaxBarsAgo_ClampedToZero()
		{
			// Doc contract: never negative — a bad maxBarsAgo must not produce a future-bar anchor
			Assert.Equal(0, KatTradeCalculator.GetLineStartBar(10, -5));
			Assert.Equal(0, KatTradeCalculator.GetLineStartBar(10, 0));
		}
		#endregion

		#region CalculateAtmLevels — negative ticks invert side
		[Fact]
		public void AtmLevels_NegativeTicks_InvertsOffsetSide()
		{
			// Negative SL ticks on a Buy puts the SL line ABOVE entry — formula applies sign verbatim
			var levels = KatTradeCalculator.CalculateAtmLevels(KatOrderAction.Buy, 100.0, -10, -20, 0, 0, 0, 0.25);
			Assert.Equal(102.5, levels.SlPrice);
			Assert.Equal(95.0, levels.TpPrice);
		}
		#endregion

		#region IsEmaTouchBar — exact boundary touch
		[Fact]
		public void IsEmaTouchBar_ExactBoundary_TouchCounts()
		{
			// high == ema exactly -> touch; low == ema exactly -> touch
			Assert.True(KatTradeCalculator.IsEmaTouchBar(100.0, 90.0, 100.0));
			Assert.True(KatTradeCalculator.IsEmaTouchBar(100.0, 90.0, 90.0));
			// fully above / fully below -> no touch
			Assert.False(KatTradeCalculator.IsEmaTouchBar(101.0, 100.25, 100.0));
			Assert.False(KatTradeCalculator.IsEmaTouchBar(99.75, 99.0, 100.0));
		}
		#endregion

		#region ValidateEmaPlace — strict equality rejected
		[Fact]
		public void ValidateEmaPlace_EntryExactlyAtEma_Invalid()
		{
			// Strictly above/below required: entry == ema fails both directions
			Assert.False(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Buy, 100.0, new[] { 100.0 }, out _));
			Assert.False(KatTradeCalculator.ValidateEmaPlace(KatOrderAction.Sell, 100.0, new[] { 100.0 }, out _));
		}
		#endregion

		#region CalculateTriggerPrice — tick rounding
		[Fact]
		public void CalculateTriggerPrice_MisalignedBase_RoundsToTick()
		{
			// 100.03 + 2*0.25 = 100.53 -> rounds to nearest tick 100.50
			double price = KatTradeCalculator.CalculateTriggerPrice(KatOrderAction.Buy, 100.03, 2, 0.25);
			Assert.Equal(100.50, price);
		}
		#endregion

		#region AtmXmlParser — whitespace-padded numbers
		[Fact]
		public void ParseXml_WhitespacePaddedNumbers_ParsedCorrectly()
		{
			string xml = "<AtmStrategy><EntryQuantity> 2 </EntryQuantity><Brackets><Bracket>"
				+ "<StopLoss> 20 </StopLoss><Target>\n40\n</Target></Bracket></Brackets></AtmStrategy>";
			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);
			Assert.Equal(20, data.StopLoss);
			Assert.Equal(40, data.Target);
			Assert.Equal(2, data.Quantity);
		}
		#endregion

		#region IsAccountAllowed — spaced exclude token
		[Fact]
		public void IsAccountAllowed_SpacedBangToken_StillExcludes()
		{
			// "! BX" -> exclude "BX" after inner trim
			Assert.False(KatTradeCalculator.IsAccountAllowed("BX123", "! BX"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("BX123", "  !BX  , Sim"));
		}
		#endregion

		#region Doji & degenerate candles
		[Fact]
		public void IsEmaTouchBar_NaNEma_ReturnsFalse()
		{
			// NaN comparisons are always false -> no phantom touch from an uninitialized EMA
			Assert.False(KatTradeCalculator.IsEmaTouchBar(200.0, 100.0, double.NaN));
		}
		#endregion

		#region FindSwingPoints — 500-bar scan cap
		[Fact]
		public void FindSwingPoints_SwingBeyond500BarCap_NotReturned()
		{
			// Descending ramp (no accidental swings), one dip inside the cap, one beyond it
			double[] series = new double[600];
			for (int i = 0; i < series.Length; i++) series[i] = 600.0 - i;
			series[100] = 90.0;  // swing low inside cap (barsAgo 100)
			series[550] = 40.0;  // swing low beyond maxBarAgo = min(600-3-1, 500) = 500 -> excluded

			var lows = KatTradeCalculator.FindSwingPoints(series, true, 20, 3, 0.25);
			Assert.Single(lows);
			Assert.Equal(90.0, lows[0]);
		}
		#endregion

		#region AtmXmlParser — quantity fallback chain
		[Fact]
		public void ParseXml_ZeroEntryQuantity_FallsBackToBracketSum()
		{
			// EntryQuantity of 0 is invalid -> bracket quantities take over
			string xml = "<AtmStrategy><EntryQuantity>0</EntryQuantity><Brackets>"
				+ "<Bracket><Quantity>2</Quantity><StopLoss>10</StopLoss></Bracket>"
				+ "<Bracket><Quantity>3</Quantity></Bracket></Brackets></AtmStrategy>";
			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);
			Assert.Equal(5, data.Quantity);
		}

		[Fact]
		public void ParseXml_NoBracketsNode_LevelsZeroQuantityDefault()
		{
			string xml = "<AtmStrategy><EntryQuantity>4</EntryQuantity></AtmStrategy>";
			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);
			Assert.Equal(0, data.StopLoss);
			Assert.Equal(0, data.Target);
			Assert.Equal(4, data.Quantity);
		}
		#endregion

		#region DetermineOrderType — zero tickSize skips rounding
		[Fact]
		public void DetermineOrderType_ZeroTickSize_UsesUnroundedPrices()
		{
			var type = KatTradeCalculator.DetermineOrderType(KatOrderAction.Buy, 100.03, 100.0, 0.0, out double limit, out double stop);
			Assert.Equal(KatOrderType.StopMarket, type);
			Assert.Equal(100.03, stop);
			Assert.Equal(0.0, limit);
		}
		#endregion

		#region IsAccountAllowed — mixed separators
		[Fact]
		public void IsAccountAllowed_MixedCommaSemicolon_AllParsed()
		{
			Assert.True(KatTradeCalculator.IsAccountAllowed("Sim101", "Playback,Sim;Other"));
			Assert.False(KatTradeCalculator.IsAccountAllowed("BxAcct", "Sim;!bx,Other"));
		}
		#endregion

		#region AtmXmlParser — unspecified quantity stays zero
		[Fact]
		public void ParseXml_NoQuantityNodes_QuantityStaysZero()
		{
			// Valid template with levels but no quantity info -> 0 (unspecified), not 1
			string xml = "<AtmStrategy><Brackets><Bracket><StopLoss>12</StopLoss></Bracket></Brackets></AtmStrategy>";
			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);
			Assert.Equal(12, data.StopLoss);
			Assert.Equal(0, data.Quantity);
		}
		#endregion

		#region IsStopOnValidSide
		[Fact]
		public void IsStopOnValidSide_LongPosition_StopMustBeBelowMarket()
		{
			Assert.True(KatTradeCalculator.IsStopOnValidSide(true, 99.0, 100.0));
			Assert.False(KatTradeCalculator.IsStopOnValidSide(true, 101.0, 100.0)); // underwater BE case
			Assert.False(KatTradeCalculator.IsStopOnValidSide(true, 100.0, 100.0)); // exactly at market
		}

		[Fact]
		public void IsStopOnValidSide_ShortPosition_StopMustBeAboveMarket()
		{
			Assert.True(KatTradeCalculator.IsStopOnValidSide(false, 101.0, 100.0));
			Assert.False(KatTradeCalculator.IsStopOnValidSide(false, 99.0, 100.0));
			Assert.False(KatTradeCalculator.IsStopOnValidSide(false, 100.0, 100.0));
		}

		[Fact]
		public void IsStopOnValidSide_ZeroOrNegativePrices_Invalid()
		{
			Assert.False(KatTradeCalculator.IsStopOnValidSide(true, 0.0, 100.0));
			Assert.False(KatTradeCalculator.IsStopOnValidSide(true, 99.0, 0.0));
			Assert.False(KatTradeCalculator.IsStopOnValidSide(false, -5.0, 100.0));
		}
		#endregion
	}
}
