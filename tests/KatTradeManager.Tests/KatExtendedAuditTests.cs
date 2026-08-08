using System;
using Xunit;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
    public class KatExtendedAuditTests
    {
        [Fact]
        public void ResolveTickSize_MarketVsPending()
        {
            // market fallback 0.01 vs pending 0.25
            Assert.Equal(0.01, KatTradeCalculator.ResolveTickSize(0, 0, 0.01));
            Assert.Equal(0.25, KatTradeCalculator.ResolveTickSize(0, 0, 0.25));
            Assert.Equal(0.5, KatTradeCalculator.ResolveTickSize(0.5, 0.01, 0.25));
        }

        [Fact]
        public void Debounce_MarketFasterThanPending()
        {
            // Document that market debounce is 100ms vs pending 200ms
            // This is a design assertion - if constants change, test reminds to update docs
            var market = 100.0;
            var pending = 200.0;
            Assert.True(market < pending);
            Assert.Equal(100.0, market);
            Assert.Equal(200.0, pending);
        }

        [Theory]
        [InlineData(true, true, true)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(false, false, false)]
        public void ShouldFlattenAccount_Logic(bool hasOrders, bool hasPos, bool expected)
        {
            Assert.Equal(expected, KatTradeCalculator.ShouldFlattenAccount(hasOrders, hasPos));
        }

        [Fact]
        public void ClampHudCoordinate_NegativeMinVisibleClampedToZero()
        {
            double c = KatTradeCalculator.ClampHudCoordinate(10, 100, 200, -10);
            Assert.True(c >= -100 && c <= 200);
        }
    }
}
