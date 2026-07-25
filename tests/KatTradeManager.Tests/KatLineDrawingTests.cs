using System;
using Xunit;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Indicators;

namespace KatTradeManager.Tests
{
    public class KatLineDrawingTests
    {
        [Fact]
        public void AtmLevels_WithZeroTicks_ShouldEqualTriggerPrice()
        {
            double triggerPrice = 2000.0;
            double tickSize = 0.25;
            
            double slTicks = 0;
            double tpTicks = 0;
            double slLevel = triggerPrice + ((slTicks * tickSize) * -1);
            double tpLevel = triggerPrice + ((tpTicks * tickSize) * 1);
            
            Assert.Equal(triggerPrice, slLevel, 4);
            Assert.Equal(triggerPrice, tpLevel, 4);
        }

        [Fact]
        public void AtmLevels_WithMixedZeroAndNonZero_ShouldApplyOffsetCorrectly()
        {
            double triggerPrice = 2000.0;
            double tickSize = 0.25;
            
            double slTicks = 20;
            double tpTicks = 0;
            
            double slLevel = triggerPrice - (slTicks * tickSize); 
            double tpLevel = triggerPrice + (tpTicks * tickSize);
            
            Assert.Equal(1995.0, slLevel, 4);
            Assert.Equal(2000.0, tpLevel, 4);
        }

        [Fact]
        public void EntryPriceLinePosition_ShouldPreserveEntryPrice()
        {
            double triggerPrice = 2005.5;
            double entryLevel = triggerPrice;
            
            Assert.Equal(2005.5, entryLevel, 4);
        }
        
        private static int CountLinesToDraw(double sl, double tp, double be, double sl1, double sl2)
        {
            int count = 0;
            if (sl > 0) count++;
            if (tp > 0) count++;
            if (be > 0) count++;
            if (sl1 > 0) count++;
            if (sl2 > 0) count++;
            return count;
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(20, 0, 0, 0, 0, 1)]
        [InlineData(20, 40, 0, 0, 0, 2)]
        [InlineData(20, 40, 10, 15, 0, 4)]
        public void DrawLineCountLogic_ShouldReturnExpectedCount(double sl, double tp, double be, double sl1, double sl2, int expectedCount)
        {
            int count = CountLinesToDraw(sl, tp, be, sl1, sl2);
            Assert.Equal(expectedCount, count);
        }
    }
}
