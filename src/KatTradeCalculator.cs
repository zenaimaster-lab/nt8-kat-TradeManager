using System;

namespace NinjaTrader.NinjaScript.Indicators
{
	public enum KatOrderAction
	{
		Buy,
		Sell
	}

	public enum KatOrderType
	{
		Market,
		Limit,
		StopMarket
	}

	public static class KatTradeCalculator
	{
		public static double CalculateTriggerPrice(KatOrderAction action, double basePrice, int bufferTicks, double tickSize)
		{
			if (bufferTicks < 0) bufferTicks = 0;
			if (tickSize <= 0) return basePrice;

			if (action == KatOrderAction.Buy)
			{
				return basePrice + (bufferTicks * tickSize);
			}
			else
			{
				return basePrice - (bufferTicks * tickSize);
			}
		}

		public static double CalculateFixedDistanceTriggerPrice(KatOrderAction action, double currentPrice, int distanceTicks, double tickSize)
		{
			if (tickSize <= 0) return currentPrice;

			if (action == KatOrderAction.Buy)
			{
				return currentPrice + (distanceTicks * tickSize);
			}
			else
			{
				return currentPrice - (distanceTicks * tickSize);
			}
		}

		public static KatOrderType DetermineOrderType(KatOrderAction action, double triggerPrice, double currentPrice, out double limitPrice, out double stopPrice)
		{
			if (action == KatOrderAction.Buy)
			{
				if (triggerPrice > currentPrice)
				{
					stopPrice = triggerPrice;
					limitPrice = 0;
					return KatOrderType.StopMarket;
				}
				else
				{
					limitPrice = triggerPrice;
					stopPrice = 0;
					return KatOrderType.Limit;
				}
			}
			else // Sell
			{
				if (triggerPrice < currentPrice)
				{
					stopPrice = triggerPrice;
					limitPrice = 0;
					return KatOrderType.StopMarket;
				}
				else
				{
					limitPrice = triggerPrice;
					stopPrice = 0;
					return KatOrderType.Limit;
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

		public static AtmLevels CalculateAtmLevels(KatOrderAction action, double triggerPrice, int stopLossTicks, int targetTicks, int beTriggerTicks, int sl1TriggerTicks, int sl2TriggerTicks, double tickSize)
		{
			AtmLevels levels = new AtmLevels();
			if (tickSize <= 0) tickSize = 0.25;

			if (action == KatOrderAction.Buy)
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
