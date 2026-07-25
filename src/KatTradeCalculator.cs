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

			double price = action == KatOrderAction.Buy
				? basePrice + (bufferTicks * tickSize)
				: basePrice - (bufferTicks * tickSize);

			double rounded = Math.Round(price / tickSize) * tickSize;
			return Math.Round(rounded, 8);
		}

		public static double CalculateBreakevenPrice(KatOrderAction action, double entryPrice, int bufferTicks, double tickSize)
		{
			return CalculateTriggerPrice(action, entryPrice, bufferTicks, tickSize);
		}

		public static double CalculateFixedDistanceTriggerPrice(KatOrderAction action, double currentPrice, int distanceTicks, double tickSize)
		{
			if (tickSize <= 0) return currentPrice;
			if (distanceTicks < 0) distanceTicks = Math.Abs(distanceTicks);

			double price = action == KatOrderAction.Buy
				? currentPrice + (distanceTicks * tickSize)
				: currentPrice - (distanceTicks * tickSize);

			double rounded = Math.Round(price / tickSize) * tickSize;
			return Math.Round(rounded, 8);
		}


		public static double CalculateHalfCandlePrice(double high, double low, double tickSize)
		{
			return CalculatePartialCandlePrice(KatOrderAction.Buy, high, low, 50.0, tickSize);
		}

		public static double CalculatePartialCandlePrice(KatOrderAction action, double high, double low, double pullbackPercent, double tickSize)
		{
			if (pullbackPercent <= 0) pullbackPercent = 30.0;
			double range = high - low;
			double pct = pullbackPercent / 100.0;
			double rawPrice = action == KatOrderAction.Buy
				? high - (range * pct)
				: low + (range * pct);

			if (tickSize <= 0) return rawPrice;
			double rounded = Math.Round(rawPrice / tickSize) * tickSize;
			return Math.Round(rounded, 8);
		}


		public static double CalculateCandlePrice(KatOrderAction action, bool isPartialCandle, double high, double low, double open, double close, bool isRenko, double tickSize)
		{
			return CalculateCandlePrice(action, isPartialCandle, 30.0, high, low, open, close, isRenko, tickSize);
		}

		public static double CalculateCandlePrice(KatOrderAction action, bool isPartialCandle, double pullbackPercent, double high, double low, double open, double close, bool isRenko, double tickSize)
		{
			if (isPartialCandle)
			{
				return CalculatePartialCandlePrice(action, high, low, pullbackPercent, tickSize);
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

		/// <summary>
		/// Calculates EMA slope angle in degrees relative to tick size.
		/// Positive for upward slope, negative for downward slope.
		/// </summary>
		public static double CalculateEmaAngle(double emaCurrent, double emaPrev, double tickSize)
		{
			// ponytail: Math.Atan of tick change per bar converted to degrees
			if (tickSize <= 0) tickSize = 0.25;
			double deltaTicks = (emaCurrent - emaPrev) / tickSize;
			double radians = Math.Atan(deltaTicks);
			return Math.Round(radians * (180.0 / Math.PI), 2);
		}

		/// <summary>
		/// Validates if entry price is strictly above (Buy) or below (Sell) all enabled EMA values.
		/// </summary>
		public static bool ValidateEmaPlace(KatOrderAction action, double entryPrice, double[] emaValues, out string errorReason)
		{
			errorReason = null;
			if (emaValues == null || emaValues.Length == 0) return true;

			for (int i = 0; i < emaValues.Length; i++)
			{
				double ema = emaValues[i];
				if (ema <= 0) continue;

				if (action == KatOrderAction.Buy && entryPrice <= ema)
				{
					errorReason = string.Format("Entry price {0} is not above EMA ({1})", entryPrice, ema);
					return false;
				}
				if (action == KatOrderAction.Sell && entryPrice >= ema)
				{
					errorReason = string.Format("Entry price {0} is not below EMA ({1})", entryPrice, ema);
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Validates if current EMA slope angles satisfy minimum degree thresholds for all enabled EMAs.
		/// </summary>
		public static bool ValidateEmaAngle(KatOrderAction action, double[] currentEmas, double[] prevEmas, double[] minAngles, double tickSize, out string errorReason)
		{
			errorReason = null;
			if (currentEmas == null || prevEmas == null || minAngles == null) return true;

			int count = Math.Min(currentEmas.Length, Math.Min(prevEmas.Length, minAngles.Length));
			for (int i = 0; i < count; i++)
			{
				if (minAngles[i] <= 0) continue;

				double curr = currentEmas[i];
				double prev = prevEmas[i];
				if (curr <= 0 || prev <= 0) continue;

				double angle = action == KatOrderAction.Buy
					? CalculateEmaAngle(curr, prev, tickSize)
					: CalculateEmaAngle(prev, curr, tickSize);

				if (angle < minAngles[i])
				{
					errorReason = string.Format("EMA slope angle {0:F1}° < required {1}°", angle, minAngles[i]);
					return false;
				}
			}
			return true;
		}
	}
}
