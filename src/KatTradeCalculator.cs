using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators
{
	public static class KatTradeCalculator
	{
		public static double CalculateTriggerPrice(OrderAction action, double basePrice, int bufferTicks, double tickSize)
		{
			if (action == OrderAction.Buy)
			{
				return basePrice + (bufferTicks * tickSize);
			}
			else
			{
				return basePrice - (bufferTicks * tickSize);
			}
		}

		public static double CalculateFixedDistanceTriggerPrice(OrderAction action, double currentPrice, int distanceTicks, double tickSize)
		{
			if (action == OrderAction.Buy)
			{
				return currentPrice + (distanceTicks * tickSize);
			}
			else
			{
				return currentPrice - (distanceTicks * tickSize);
			}
		}

		public static OrderType DetermineOrderType(OrderAction action, double triggerPrice, double currentPrice, out double limitPrice, out double stopPrice)
		{
			if (action == OrderAction.Buy)
			{
				if (triggerPrice > currentPrice)
				{
					stopPrice = triggerPrice;
					limitPrice = 0;
					return OrderType.StopMarket;
				}
				else
				{
					limitPrice = triggerPrice;
					stopPrice = 0;
					return OrderType.Limit;
				}
			}
			else // Sell
			{
				if (triggerPrice < currentPrice)
				{
					stopPrice = triggerPrice;
					limitPrice = 0;
					return OrderType.StopMarket;
				}
				else
				{
					limitPrice = triggerPrice;
					stopPrice = 0;
					return OrderType.Limit;
				}
			}
		}

		public struct AtmLevels
		{
			public double SlPrice;
			public double TpPrice;
			public double BePrice;
			public double Sl1Price;
			public double Sl2Price;
		}

		public static AtmLevels CalculateAtmLevels(OrderAction action, double triggerPrice, int stopLossTicks, int targetTicks, int beTriggerTicks, int sl1TriggerTicks, int sl2TriggerTicks, double tickSize)
		{
			AtmLevels levels = new AtmLevels();
			if (action == OrderAction.Buy)
			{
				levels.SlPrice  = triggerPrice - (stopLossTicks * tickSize);
				levels.TpPrice  = triggerPrice + (targetTicks * tickSize);
				levels.BePrice  = triggerPrice + (beTriggerTicks * tickSize);
				levels.Sl1Price = triggerPrice + (sl1TriggerTicks * tickSize);
				levels.Sl2Price = triggerPrice + (sl2TriggerTicks * tickSize);
			}
			else
			{
				levels.SlPrice  = triggerPrice + (stopLossTicks * tickSize);
				levels.TpPrice  = triggerPrice - (targetTicks * tickSize);
				levels.BePrice  = triggerPrice - (beTriggerTicks * tickSize);
				levels.Sl1Price = triggerPrice - (sl1TriggerTicks * tickSize);
				levels.Sl2Price = triggerPrice - (sl2TriggerTicks * tickSize);
			}
			return levels;
		}
	}
}
