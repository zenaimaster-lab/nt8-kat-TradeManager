using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
	public class KatAtmXmlParserTests
	{
		[Fact]
		public void ParseXml_ValidAtmXml_ParsesAllFieldsCorrectly()
		{
			// Arrange
			string xml = @"<?xml version=""1.0"" encoding=""utf-16""?>
<AtmStrategy xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <EntryQuantity>2</EntryQuantity>
  <Brackets>
    <Bracket>
      <Quantity>2</Quantity>
      <StopLoss>20</StopLoss>
      <Target>40</Target>
      <StopStrategy>
        <AutoBreakEvenProfitTrigger>10</AutoBreakEvenProfitTrigger>
        <AutoTrailSteps>
          <AutoTrailStep>
            <ProfitTrigger>15</ProfitTrigger>
          </AutoTrailStep>
          <AutoTrailStep>
            <ProfitTrigger>25</ProfitTrigger>
          </AutoTrailStep>
        </AutoTrailSteps>
      </StopStrategy>
    </Bracket>
  </Brackets>
</AtmStrategy>";

			// Act
			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);

			// Assert
			Assert.Equal(2, data.Quantity);
			Assert.Equal(20, data.StopLoss);
			Assert.Equal(40, data.Target);
			Assert.Equal(10, data.BETrigger);
			Assert.Equal(15, data.SL1Trigger);
			Assert.Equal(25, data.SL2Trigger);
		}

		[Fact]
		public void ParseXml_MultipleBrackets_SumsQuantities()
		{
			// Arrange
			string xml = @"<?xml version=""1.0"" encoding=""utf-16""?>
<AtmStrategy>
  <Brackets>
    <Bracket>
      <Quantity>3</Quantity>
      <StopLoss>20</StopLoss>
      <Target>40</Target>
    </Bracket>
    <Bracket>
      <Quantity>2</Quantity>
      <StopLoss>20</StopLoss>
      <Target>80</Target>
    </Bracket>
  </Brackets>
</AtmStrategy>";

			// Act
			AtmTemplateData data = KatAtmXmlParser.ParseXml(xml);

			// Assert
			Assert.Equal(5, data.Quantity);
			Assert.Equal(20, data.StopLoss);
			Assert.Equal(40, data.Target);
		}

		[Fact]
		public void ParseXml_EmptyOrInvalidXml_ReturnsDefaultsWithoutThrowing()
		{
			// Arrange
			string emptyXml = "";
			string invalidXml = "<invalid><unclosed></invalid>";

			// Act
			AtmTemplateData emptyData = KatAtmXmlParser.ParseXml(emptyXml);
			AtmTemplateData invalidData = KatAtmXmlParser.ParseXml(invalidXml);

			// Assert
			Assert.Equal(0, emptyData.Quantity);
			Assert.Equal(0, emptyData.StopLoss);
			Assert.Equal(0, emptyData.Target);
			Assert.Equal(0, emptyData.BETrigger);

			Assert.Equal(0, invalidData.Quantity);
			Assert.Equal(0, invalidData.StopLoss);
		}
	}
}
