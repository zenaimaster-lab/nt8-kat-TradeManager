using System;
using Xunit;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class StressAndEdgeCaseTests
	{
		#region KatTradeCalculator Edge Cases

		[Fact]
		public void CalculateTriggerPrice_ZeroTickSize_ReturnsBasePrice()
		{
			double buyTrigger = KatTradeCalculator.CalculateTriggerPrice(OrderAction.Buy, 1000.0, 5, 0.0);
			double sellTrigger = KatTradeCalculator.CalculateTriggerPrice(OrderAction.Sell, 1000.0, 5, 0.0);

			Assert.Equal(1000.0, buyTrigger);
			Assert.Equal(1000.0, sellTrigger);
		}

		[Fact]
		public void CalculateTriggerPrice_NegativeBasePrice_CalculatesCorrectly()
		{
			// Commodity futures (e.g. WTI crude oil during negative pricing events)
			double basePrice = -37.0;
			double buyTrigger = KatTradeCalculator.CalculateTriggerPrice(OrderAction.Buy, basePrice, 4, 0.25);
			double sellTrigger = KatTradeCalculator.CalculateTriggerPrice(OrderAction.Sell, basePrice, 4, 0.25);

			Assert.Equal(-36.0, buyTrigger, 4);
			Assert.Equal(-38.0, sellTrigger, 4);
		}

		[Fact]
		public void DetermineOrderType_PriceEquality_SelectsLimitOrder()
		{
			// When trigger price equals current price exactly
			double price = 1500.25;

			OrderType buyType = KatTradeCalculator.DetermineOrderType(OrderAction.Buy, price, price, out double buyLimit, out double buyStop);
			Assert.Equal(OrderType.Limit, buyType);
			Assert.Equal(price, buyLimit);
			Assert.Equal(0.0, buyStop);

			OrderType sellType = KatTradeCalculator.DetermineOrderType(OrderAction.Sell, price, price, out double sellLimit, out double sellStop);
			Assert.Equal(OrderType.Limit, sellType);
			Assert.Equal(price, sellLimit);
			Assert.Equal(0.0, sellStop);
		}

		[Fact]
		public void CalculateAtmLevels_ExtremeBufferValues_DoesNotOverflowOrCrash()
		{
			double trigger = 10000.0;
			int extremeTicks = 1000000;
			double tickSize = 0.25;

			var levels = KatTradeCalculator.CalculateAtmLevels(OrderAction.Buy, trigger, extremeTicks, extremeTicks, extremeTicks, extremeTicks, extremeTicks, tickSize);

			Assert.Equal(10000.0 - 250000.0, levels.SlPrice, 4);
			Assert.Equal(10000.0 + 250000.0, levels.TpPrice, 4);
			Assert.Equal(10000.0 + 250000.0, levels.BePrice, 4);
			Assert.Equal(10000.0 + 250000.0, levels.Sl1Price, 4);
			Assert.Equal(10000.0 + 250000.0, levels.Sl2Price, 4);
		}

		[Fact]
		public void CalculateFixedDistanceTriggerPrice_NegativeDistance_HandlesInvertedDistance()
		{
			double currentPrice = 2000.0;
			double buyTrigger = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(OrderAction.Buy, currentPrice, -10, 0.25);
			double sellTrigger = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(OrderAction.Sell, currentPrice, -10, 0.25);

			Assert.Equal(1997.50, buyTrigger, 4);
			Assert.Equal(2002.50, sellTrigger, 4);
		}

		[Fact]
		public void FixedDistanceOrder_ShouldUseDetermineOrderType_NotHardcodedStopMarket()
		{
			// When price is at 2000 and we do BUY +Distance with -10 ticks (below market),
			// the order should be Limit, not StopMarket
			double currentPrice = 2000.0;
			double triggerPrice = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(OrderAction.Buy, currentPrice, -10, 0.25);
			// triggerPrice = 1997.50, below currentPrice = 2000.0
			OrderType orderType = KatTradeCalculator.DetermineOrderType(OrderAction.Buy, triggerPrice, currentPrice, out double limitPrice, out double stopPrice);
			Assert.Equal(OrderType.Limit, orderType);
			Assert.Equal(1997.50, limitPrice, 4);
			Assert.Equal(0.0, stopPrice, 4);
		}

		#endregion

		#region KatAtmXmlParser Edge Cases

		[Fact]
		public void ParseXml_NullOrWhitespaceInput_ReturnsDefaultsSafely()
		{
			AtmTemplateData dataNull = KatAtmXmlParser.ParseXml(null);
			AtmTemplateData dataEmpty = KatAtmXmlParser.ParseXml("");
			AtmTemplateData dataSpace = KatAtmXmlParser.ParseXml("   \t\r\n  ");

			Assert.NotNull(dataNull);
			Assert.Equal(1, dataNull.Quantity);
			Assert.Equal(0, dataNull.StopLoss);

			Assert.NotNull(dataEmpty);
			Assert.Equal(1, dataEmpty.Quantity);

			Assert.NotNull(dataSpace);
			Assert.Equal(1, dataSpace.Quantity);
		}

		[Fact]
		public void ParseXml_MalformedXmlStrings_HandlesGracefullyWithoutThrowing()
		{
			string truncatedXml = "<AtmStrategy><Brackets><Bracket><StopLoss>20";
			string badTagsXml = "<AtmStrategy><Brackets>Unmatched</Something></AtmStrategy>";
			string randomNoise = "Not XML at all !!! 12345";

			AtmTemplateData d1 = KatAtmXmlParser.ParseXml(truncatedXml);
			AtmTemplateData d2 = KatAtmXmlParser.ParseXml(badTagsXml);
			AtmTemplateData d3 = KatAtmXmlParser.ParseXml(randomNoise);

			Assert.NotNull(d1);
			Assert.Equal(1, d1.Quantity);
			Assert.NotNull(d2);
			Assert.NotNull(d3);
		}

		[Fact]
		public void ParseXml_InvalidNumberFormatsAndOverflows_HandledGracefully()
		{
			string xml = @"<AtmStrategy>
  <EntryQuantity>999999999999999999999999999999</EntryQuantity>
  <Brackets>
    <Bracket>
      <StopLoss>NotANumber</StopLoss>
      <Target>2147483648</Target>
      <Quantity>-5</Quantity>
    </Bracket>
  </Brackets>
</AtmStrategy>";

			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);

			Assert.NotNull(data);
			Assert.Equal(1, data.Quantity); // EntryQuantity overflowed int.TryParse, negative bracket qty ignored
			Assert.Equal(0, data.StopLoss); // Non-number -> 0
			Assert.Equal(0, data.Target);   // Overflow > int.MaxValue -> 0
		}

		[Fact]
		public void ParseFile_NonExistentPathOrNull_ReturnsDefaultSafely()
		{
			AtmTemplateData d1 = KatAtmXmlParser.ParseFile(null);
			AtmTemplateData d2 = KatAtmXmlParser.ParseFile("");
			AtmTemplateData d3 = KatAtmXmlParser.ParseFile(@"C:\NonExistentFolder\NonExistentFile_12345.xml");

			Assert.NotNull(d1);
			Assert.Equal(1, d1.Quantity);
			Assert.NotNull(d2);
			Assert.NotNull(d3);
		}

		[Fact]
		public void ParseXml_XmlWithSurroundingWhitespaceAndNewlines_ParsesSuccessfully()
		{
			string xml = @"
<AtmStrategy>
  <EntryQuantity>
    4 
  </EntryQuantity>
  <Brackets>
    <Bracket>
      <StopLoss> 30 </StopLoss>
      <Target> 60 </Target>
    </Bracket>
  </Brackets>
</AtmStrategy>";

			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);

			Assert.Equal(4, data.Quantity);
			Assert.Equal(30, data.StopLoss);
			Assert.Equal(60, data.Target);
		}

		#endregion
	}
}
