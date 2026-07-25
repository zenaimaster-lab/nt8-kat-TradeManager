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
		/// <summary>All draw-object tags for order lines. Draw and Remove MUST use this single list.</summary>
		public static readonly string[] LineTags = new[]
		{
			"KAT_ENTRY_LINE", "KAT_SL_LINE", "KAT_TP_LINE", "KAT_BE_LINE", "KAT_SL1_LINE", "KAT_SL2_LINE"
		};

		/// <summary>Start anchor (barsAgo) for short order lines. Never exceeds currentBar, never negative.</summary>
		public static int GetLineStartBar(int currentBar, int maxBarsAgo)
		{
			if (currentBar <= 0) return 0;
			return currentBar < maxBarsAgo ? currentBar : maxBarsAgo;
		}

		/// <summary>Checks if a candle touches or crosses an EMA line (High >= EMA && Low <= EMA).</summary>
		public static bool IsEmaTouchBar(double high, double low, double ema)
		{
			return high >= ema && low <= ema;
		}

		/// <summary>Scans bars backward (0..count-1) and returns the barsAgo index of the first candle touching/crossing EMA.</summary>
		public static int FindLastEmaTouchBar(double[] highs, double[] lows, double[] emas, int count)
		{
			if (highs == null || lows == null || emas == null) return -1;
			int limit = Math.Min(count, Math.Min(highs.Length, Math.Min(lows.Length, emas.Length)));
			for (int barsAgo = 0; barsAgo < limit; barsAgo++)
			{
				if (IsEmaTouchBar(highs[barsAgo], lows[barsAgo], emas[barsAgo]))
					return barsAgo;
			}
			return -1;
		}


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
			if (distanceTicks < 0) distanceTicks = Math.Abs(distanceTicks);

			if (action == KatOrderAction.Buy)
			{
				return currentPrice + (distanceTicks * tickSize);
			}
			else
			{
				return currentPrice - (distanceTicks * tickSize);
			}
		}

		public static double CalculateHalfCandlePrice(double high, double low, double tickSize)
		{
			double mid = (high + low) / 2.0;
			if (tickSize <= 0) return mid;
			return Math.Round(mid / tickSize) * tickSize;
		}

		public static double CalculateCandlePrice(KatOrderAction action, bool isHalfCandle, double high, double low, double open, double close, bool isRenko, double tickSize)
		{
			if (isHalfCandle)
			{
				return CalculateHalfCandlePrice(high, low, tickSize);
			}

			// Renko bricks have no wicks: high == max(open,close), low == min(open,close)
			// Standard logic (Buy=high, Sell=low) works identically for Renko
			return action == KatOrderAction.Buy ? high : low;
		}

		public static KatOrderType DetermineOrderType(KatOrderAction action, double triggerPrice, double currentPrice, out double limitPrice, out double stopPrice)
		{
			return DetermineOrderType(action, triggerPrice, currentPrice, 0.0, out limitPrice, out stopPrice);
		}

		public static KatOrderType DetermineOrderType(KatOrderAction action, double triggerPrice, double currentPrice, double tickSize, out double limitPrice, out double stopPrice)
		{
			double trig = triggerPrice;
			double curr = currentPrice;
			if (tickSize > 0)
			{
				trig = Math.Round(triggerPrice / tickSize) * tickSize;
				curr = Math.Round(currentPrice / tickSize) * tickSize;
			}

			if (action == KatOrderAction.Buy)
			{
				if (trig > curr)
				{
					stopPrice = trig;
					limitPrice = 0;
					return KatOrderType.StopMarket;
				}
				else
				{
					limitPrice = trig;
					stopPrice = 0;
					return KatOrderType.Limit;
				}
			}
			else // Sell
			{
				if (trig < curr)
				{
					stopPrice = trig;
					limitPrice = 0;
					return KatOrderType.StopMarket;
				}
				else
				{
					limitPrice = trig;
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
			if (triggerPrice <= 0) return levels;

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
