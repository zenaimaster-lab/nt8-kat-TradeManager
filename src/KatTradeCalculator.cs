using System;
using System.Collections.Generic;
using System.Linq;

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

		public static bool ShouldDrawExpectedLines(bool orderSubmitted, KatOrderType orderType)
		{
			return orderSubmitted && orderType != KatOrderType.Market;
		}

		public static bool ShouldDeferAtmFlatCleanup(bool atmEntryStartupPending, bool positionConfirmed)
		{
			return atmEntryStartupPending && !positionConfirmed;
		}

		/// <summary>
		/// Prevents ATM protective-order cleanup while NT8 may still be publishing an entry or
		/// scale-out across its order and position snapshots.
		/// </summary>
		public static bool ShouldDeferAtmFlatCleanup(
			bool atmEntryStartupPending,
			bool positionConfirmed,
			bool positionWasConfirmedThisEpisode,
			double millisecondsSinceLastAtmActivity,
			double graceMilliseconds)
		{
			if (positionConfirmed) return false;
			if (!positionWasConfirmedThisEpisode && atmEntryStartupPending) return true;
			if (double.IsNaN(millisecondsSinceLastAtmActivity)
				|| double.IsInfinity(millisecondsSinceLastAtmActivity)
				|| millisecondsSinceLastAtmActivity < 0)
				return true;

			return millisecondsSinceLastAtmActivity < Math.Max(0, graceMilliseconds);
		}

		/// <summary>Close/flatten has work to do only if the account has working orders or an open position.</summary>
		public static bool ShouldFlattenAccount(bool hasWorkingOrders, bool hasOpenPosition)
		{
			return hasWorkingOrders || hasOpenPosition;
		}

		/// <summary>Returns total positive quantity for ATM bracket consolidation.</summary>
		public static int CalculateMergedOrderQuantity(IEnumerable<int> quantities)
		{
			if (quantities == null) return 0;
			long total = 0;
			foreach (int quantity in quantities)
			{
				if (quantity > 0) total += quantity;
				if (total >= int.MaxValue) return int.MaxValue;
			}
			return (int)total;
		}

		public static double ClampHudCoordinate(double proposed, double panelExtent, double chartExtent, double minVisible)
		{
			if (minVisible < 0) minVisible = 0;
			double min = -Math.Max(0, panelExtent - minVisible);
			double max = Math.Max(0, chartExtent - minVisible);
			return Math.Max(min, Math.Min(proposed, max));
		}

		/// <summary>Start anchor (barsAgo) for short order lines. Never exceeds currentBar, never negative.</summary>
		public static int GetLineStartBar(int currentBar, int maxBarsAgo)
		{
			if (currentBar <= 0 || maxBarsAgo <= 0) return 0;
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

		public static void CalculateStopLimitPrices(KatOrderAction action, double triggerPrice, double tickSize, out double limitPrice, out double stopPrice)
		{
			if (tickSize <= 0) tickSize = 0.01;
			stopPrice = triggerPrice;
			limitPrice = action == KatOrderAction.Buy
				? triggerPrice + tickSize
				: triggerPrice - tickSize;
		}

		/// <summary>
		/// Restores a protective StopLimit's limit price when its stop is moved back to a frozen/target price.
		/// Preserves the order's existing absolute stop-to-limit offset (falling back to one tick, then 0.01)
		/// and keeps the protective direction: a Long exit (sell stop) places the limit BELOW the stop so it
		/// can still fill in a falling market; a Short exit (buy stop) places it ABOVE.
		/// </summary>
		public static double CalculateFrozenStopLimitPrice(bool isLongPosition, double newStopPrice, double existingStopPrice, double existingLimitPrice, double tickSize)
		{
			double offset = Math.Abs(existingLimitPrice - existingStopPrice);
			if (double.IsNaN(offset) || double.IsInfinity(offset) || offset <= 0)
				offset = tickSize > 0 ? tickSize : 0.01;
			return isLongPosition ? newStopPrice - offset : newStopPrice + offset;
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

		/// <summary>
		/// A protective stop for a LONG position must sit BELOW current market; for SHORT, ABOVE.
		/// Placing it on the wrong side gets rejected by the broker (rejections still count toward order rate).
		/// </summary>
		public static bool IsStopOnValidSide(bool isLongPosition, double stopPrice, double currentPrice)
		{
			if (stopPrice <= 0 || currentPrice <= 0) return false;
			return isLongPosition ? stopPrice < currentPrice : stopPrice > currentPrice;
		}

		/// <summary>
		/// Checks account name against comma/semicolon-separated filter.
		/// Tokens prefixed with '!' are excludes; plain tokens are includes.
		/// Empty filter = allow all. Excludes win over includes.
		/// </summary>
		public static bool IsAccountAllowed(string accName, string accountFilter)
		{
			if (string.IsNullOrWhiteSpace(accName)) return false;
			if (string.IsNullOrWhiteSpace(accountFilter)) return true;
			string[] tokens = accountFilter.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim()).ToArray();
			if (tokens.Length == 0) return true;

			var excludes = tokens.Where(t => t.StartsWith("!") && t.Length > 1).Select(t => t.Substring(1).Trim()).ToList();
			var includes = tokens.Where(t => !t.StartsWith("!") && t.Length > 0).ToList();

			if (excludes.Any(ex => accName.IndexOf(ex, StringComparison.OrdinalIgnoreCase) >= 0))
				return false;

			if (includes.Count > 0)
				return includes.Any(inc => accName.IndexOf(inc, StringComparison.OrdinalIgnoreCase) >= 0);

			return true;
		}

		/// <summary>
		/// Finds swing points in a price series indexed by barsAgo (0 = current bar).
		/// findLows=true returns swing lows (for Long SL), false returns swing highs (for Short SL).
		/// Results ordered most-recent-first, deduplicated within one tick.
		/// </summary>
		public static List<double> FindSwingPoints(double[] seriesBarsAgo, bool findLows, int maxSwings, int strength, double tickSize)
		{
			List<double> swings = new List<double>();
			if (seriesBarsAgo == null || strength < 1 || maxSwings < 1) return swings;
			int barCount = seriesBarsAgo.Length;
			if (barCount < strength * 2 + 1) return swings;
			if (tickSize <= 0) tickSize = 0.25;

			int maxBarAgo = Math.Min(barCount - strength - 1, 500);

			for (int barsAgo = strength; barsAgo <= maxBarAgo; barsAgo++)
			{
				double candidate = seriesBarsAgo[barsAgo];
				bool isSwing = true;
				for (int k = 1; k <= strength; k++)
				{
					if (findLows)
					{
						if (seriesBarsAgo[barsAgo - k] < candidate || seriesBarsAgo[barsAgo + k] < candidate)
						{
							isSwing = false;
							break;
						}
					}
					else
					{
						if (seriesBarsAgo[barsAgo - k] > candidate || seriesBarsAgo[barsAgo + k] > candidate)
						{
							isSwing = false;
							break;
						}
					}
				}

				if (isSwing && !swings.Any(s => Math.Abs(s - candidate) < tickSize))
					swings.Add(candidate);

				if (swings.Count >= maxSwings) break;
			}

			return swings;
		}

		public static double FindNextSwingStopPrice(IEnumerable<double> swingPrices, KatOrderAction action, double referencePrice, double tickSize)
		{
			if (swingPrices == null) return 0;
			double threshold = Math.Max(0, tickSize) * 0.5;
			foreach (double swingPrice in swingPrices)
			{
				if (action == KatOrderAction.Buy && swingPrice < referencePrice - threshold)
					return swingPrice;
				if (action == KatOrderAction.Sell && swingPrice > referencePrice + threshold)
					return swingPrice;
			}
			return 0;
		}

		/// <summary>
		/// Calculates UTC timestamp corresponding to 6:00 PM NY time (Eastern Time) of active trading session.
		/// </summary>
		public static DateTime GetNySessionStartUtc(DateTime nowUtc)
		{
			// ponytail: converts UTC to NY Time (EST/EDT) to determine 18:00 session start
			TimeZoneInfo nyZone;
			try
			{
				nyZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
			}
			catch
			{
				nyZone = TimeZoneInfo.Local; // ponytail: fallback if EST zone ID unavailable
			}

			DateTime nowNy = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, nyZone);
			DateTime sessionStartNy;
			if (nowNy.TimeOfDay >= new TimeSpan(18, 0, 0))
			{
				sessionStartNy = nowNy.Date.AddHours(18);
			}
			else
			{
				sessionStartNy = nowNy.Date.AddDays(-1).AddHours(18);
			}

			return TimeZoneInfo.ConvertTimeToUtc(sessionStartNy, nyZone);
		}
	}
}

