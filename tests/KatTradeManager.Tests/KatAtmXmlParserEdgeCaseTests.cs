using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
    public class KatAtmXmlParserEdgeCaseTests
    {
        [Fact]
        public void ParseXml_MultiBracketXml_SumsQuantitiesAndParsesFirstBracketLevels()
        {
            string xml = @"<AtmStrategy>
  <Brackets>
    <Bracket>
      <Quantity>2</Quantity>
      <StopLoss>20</StopLoss>
      <Target>40</Target>
      <StopStrategy>
        <AutoBreakEvenProfitTrigger>12</AutoBreakEvenProfitTrigger>
        <AutoTrailSteps>
          <AutoTrailStep>
            <ProfitTrigger>16</ProfitTrigger>
          </AutoTrailStep>
          <AutoTrailStep>
            <ProfitTrigger>24</ProfitTrigger>
          </AutoTrailStep>
        </AutoTrailSteps>
      </StopStrategy>
    </Bracket>
    <Bracket>
      <Quantity>3</Quantity>
      <StopLoss>25</StopLoss>
      <Target>60</Target>
    </Bracket>
  </Brackets>
</AtmStrategy>";

            AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);

            Assert.Equal(20, data.StopLoss);
            Assert.Equal(40, data.Target);
            Assert.Equal(12, data.BETrigger);
            Assert.Equal(16, data.SL1Trigger);
            Assert.Equal(24, data.SL2Trigger);
            Assert.Equal(5, data.Quantity);
        }

        [Fact]
        public void ParseXml_ZeroOrNegativeTriggers_DefaultToZero()
        {
            string xml = @"<AtmStrategy>
  <Brackets>
    <Bracket>
      <StopLoss>0</StopLoss>
      <Target>0</Target>
    </Bracket>
  </Brackets>
</AtmStrategy>";

            AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);

            Assert.Equal(0, data.StopLoss);
            Assert.Equal(0, data.Target);
            Assert.Equal(0, data.BETrigger);
            Assert.Equal(0, data.SL1Trigger);
            Assert.Equal(0, data.SL2Trigger);
        }
    }
}
