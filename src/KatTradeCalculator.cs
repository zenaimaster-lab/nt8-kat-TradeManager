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

		public sealed class KatAtmBracketOrder
		{
			public string Oco;
			public bool IsStop;
			public int Quantity;
			public double Price;
		}

		public sealed class KatAtmMergePlan
		{
			public int KeepStopIndex = -1;
			public int KeepTargetIndex = -1;
			public int DesiredStopQuantity;
			public int DesiredTargetQuantity;
			public int[] ChangeIndices = new int[0];
			public int[] CancelIndices = new int[0];
			public bool IsNoop => ChangeIndices.Length == 0 && CancelIndices.Length == 0;
		}

		/// <summary>
		/// Chooses one canonical stop/target pair from SAME OCO only; quantity is merged to that pair.
		/// Different OCO brackets are never merged into each other to avoid broker-side OCO cascades.
		/// Index-based so it is immune to null/empty broker OrderId or Oco values.
		/// </summary>
		public static KatAtmMergePlan PlanAtmBracketMerge(IList<KatAtmBracketOrder> orders, int livePositionQuantity)
		{
			KatAtmMergePlan plan = new KatAtmMergePlan();
			if (orders == null || livePositionQuantity <= 0) return plan;

			List<int> valid = new List<int>();
			for (int i = 0; i < orders.Count; i++)
			{
				if (orders[i] != null && orders[i].Quantity > 0) valid.Add(i);
			}
			if (valid.Count == 0) return plan;

			// Group by OCO; only groups with BOTH a stop and a target are complete pairs.
			var groups = valid.GroupBy(i => orders[i].Oco ?? string.Empty).ToList();
			var complete = groups
				.Where(g => g.Any(i => orders[i].IsStop) && g.Any(i => !orders[i].IsStop))
				.ToList();
			if (complete.Count == 0) return plan;
			var chosen = complete.OrderByDescending(g => g.Sum(i => orders[i].Quantity)).First();

			int keepStop = chosen.Where(i => orders[i].IsStop)
				.OrderByDescending(i => orders[i].Quantity).ThenBy(i => orders[i].Price).First();
			int keepTarget = chosen.Where(i => !orders[i].IsStop)
				.OrderByDescending(i => orders[i].Quantity).ThenBy(i => orders[i].Price).First();

			plan.KeepStopIndex = keepStop;
			plan.KeepTargetIndex = keepTarget;
			plan.DesiredStopQuantity = Math.Max(livePositionQuantity, orders[keepStop].Quantity);
			plan.DesiredTargetQuantity = Math.Max(livePositionQuantity, orders[keepTarget].Quantity);

			List<int> changes = new List<int>();
			if (orders[keepStop].Quantity != plan.DesiredStopQuantity) changes.Add(keepStop);
			if (orders[keepTarget].Quantity != plan.DesiredTargetQuantity) changes.Add(keepTarget);
			plan.ChangeIndices = changes.ToArray();

			plan.CancelIndices = valid.Where(i => i != keepStop && i != keepTarget).ToArray();
			return plan;
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

		/// <summary>
		/// Calculates target bar index when shifting entry orders backward or forward.
		/// Uses bar timestamps to accurately track current position even when new bars arrive on chart.
		/// Returns target index in barTimes list, or -1 if boundary reached (status string output).
		/// </summary>
		public static int CalculateShiftedBarIndex(
			System.Collections.Generic.IList<DateTime> barTimes,
			DateTime currentBarTime,
			int fallbackIndex,
			bool isForward,
			out string boundaryStatus)
		{
			boundaryStatus = null;
			if (barTimes == null || barTimes.Count == 0)
			{
				boundaryStatus = "EMPTY";
				return -1;
			}

			int currentIndex = -1;
			if (currentBarTime != DateTime.MinValue)
			{
				for (int i = 0; i < barTimes.Count; i++)
				{
					if (barTimes[i] == currentBarTime)
					{
						currentIndex = i;
						break;
					}
				}
			}

			if (currentIndex == -1)
			{
				currentIndex = fallbackIndex;
			}

			int targetIndex = isForward ? currentIndex - 1 : currentIndex + 1;

			if (targetIndex < 0)
			{
				boundaryStatus = "REACHED_NEWEST";
				return -1;
			}

			if (targetIndex >= barTimes.Count)
			{
				boundaryStatus = "REACHED_OLDEST";
				return -1;
			}

			return targetIndex;
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

		public static void CalculateStopLimitPrices(KatOrderAction action, double triggerPrice, double tickSize, out double limitPrice, out double stopPrice)
		{
			if (tickSize <= 0) tickSize = 0.01;
			stopPrice = triggerPrice;
			limitPrice = action == KatOrderAction.Buy
				? triggerPrice + tickSize
				: triggerPrice - tickSize;
		}

		// Renko bricks have no wicks: high == max(open,close), low == min(open,close),
		// so the standard Buy=high / Sell=low anchor works identically for Renko.
		public static double CalculateCandlePrice(KatOrderAction action, double high, double low)
		{
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
		/// A protective stop for a LONG position must sit BELOW current market; for SHORT, ABOVE.
		/// Placing it on the wrong side gets rejected by the broker (rejections still count toward order rate).
		/// </summary>
		public static bool IsStopOnValidSide(bool isLongPosition, double stopPrice, double currentPrice)
		{
			if (stopPrice <= 0 || currentPrice <= 0) return false;
			return isLongPosition ? stopPrice < currentPrice : stopPrice > currentPrice;
		}

		/// <summary>
		/// Pure daily-risk gate: a limit can only breach while its toggle is ON and the configured
		/// limit is positive. OFF means never breached, regardless of PnL.
		/// </summary>
		public static bool EvaluateDailyRiskBreach(
			bool isMaxDDEnabled,
			double maxDD,
			bool isMaxProfitEnabled,
			double maxProfit,
			double dailyPnL,
			out string breachReason)
		{
			breachReason = string.Empty;

			if (isMaxDDEnabled && maxDD > 0 && dailyPnL <= -Math.Abs(maxDD))
			{
				breachReason = string.Format("Daily Max DD breached (Current Daily PnL: ${0:F2} <= Max DD limit: -${1:F2})", dailyPnL, Math.Abs(maxDD));
				return true;
			}

			if (isMaxProfitEnabled && maxProfit > 0 && dailyPnL >= maxProfit)
			{
				breachReason = string.Format("Daily Max Profit reached (Current Daily PnL: ${0:F2} >= Max Profit limit: ${1:F2})", dailyPnL, maxProfit);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Normalizes an ATM quick-set button label: trimmed, at most 3 characters, falling back to
		/// the default letter when empty/whitespace.
		/// </summary>
		public static string NormalizeAtmSetName(string value, string fallback)
		{
			string trimmed = (value ?? string.Empty).Trim();
			if (trimmed.Length == 0) return fallback;
			return trimmed.Length > 3 ? trimmed.Substring(0, 3) : trimmed;
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
		/// Session baseline capture gate. The baseline (realized PnL at session start) must only be
		/// captured when the account read actually succeeded — capturing 0 after a failed read poisons
		/// the baseline and produces a phantom daily PnL (and a phantom risk breach) on the next read.
		/// </summary>
		public static bool ShouldCaptureSessionBaseline(bool isCaptured, DateTime currentSessionStartUtc, DateTime lastSessionStartUtc, bool readSucceeded)
		{
			if (!readSucceeded) return false;
			return !isCaptured || currentSessionStartUtc > lastSessionStartUtc;
		}

		// ponytail: discipline — trading windows
		public struct KatTradingWindow
		{
			public bool Enabled;
			public int StartHour;
			public int StartMinute;
			public int EndHour;
			public int EndMinute;
			public TimeSpan Start => new TimeSpan(StartHour, StartMinute, 0);
			public TimeSpan End => new TimeSpan(EndHour, EndMinute, 0);
		}

		public static DateTime GetNyTime(DateTime utc)
		{
			TimeZoneInfo nyZone;
			try { nyZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
			catch { nyZone = TimeZoneInfo.Local; }
			return TimeZoneInfo.ConvertTimeFromUtc(utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime(), nyZone);
		}

		public static bool IsWithinTradingWindows(TimeSpan nyTimeOfDay, IList<KatTradingWindow> windows)
		{
			if (windows == null || windows.Count == 0) return false;
			bool anyEnabled = false;
			foreach (var w in windows)
			{
				if (!w.Enabled) continue;
				anyEnabled = true;
				TimeSpan start = w.Start;
				TimeSpan end = w.End;
				if (start == end) continue; // zero-length window = disabled
				bool inside;
				if (start < end) inside = nyTimeOfDay >= start && nyTimeOfDay < end;
				else inside = nyTimeOfDay >= start || nyTimeOfDay < end; // overnight
				if (inside) return true;
			}
			return !anyEnabled ? false : false;
		}

		public static bool IsSizingBlocked(bool hasPosition, bool isLongPosition, KatOrderAction action, int positionQty, int initialQty, int orderQty)
		{
			if (!hasPosition) return false;
			bool isScaleIn = isLongPosition ? action == KatOrderAction.Buy : action == KatOrderAction.Sell;
			if (!isScaleIn) return false;
			// Strict: any same-direction add after fill is blocked when sizing protect ON (max = first fill = ATM qty)
			return true;
		}

		public static bool IsSlPullBlocked(bool isLong, double initialSl, double newSl, double tickSize)
		{
			if (initialSl <= 0 || newSl <= 0) return false;
			double tol = (tickSize > 0 ? tickSize : 0.01) * 0.5;
			if (isLong) return newSl < initialSl - tol;
			return newSl > initialSl + tol;
		}

		public static bool IsLossDcaBlocked(bool isLong, double entryPrice, double curPrice, double tickSize)
		{
			if (entryPrice <= 0 || curPrice <= 0) return false;
			double tol = (tickSize > 0 ? tickSize : 0.01) * 0.5;
			if (isLong) return curPrice < entryPrice - tol;
			return curPrice > entryPrice + tol;
		}

		public static bool IsScaleIn(bool isLong, KatOrderAction action)
		{
			return isLong ? action == KatOrderAction.Buy : action == KatOrderAction.Sell;
		}

		public static bool IsScaleOut(bool isLong, KatOrderAction action)
		{
			return isLong ? action == KatOrderAction.Sell : action == KatOrderAction.Buy;
		}

		public static bool IsLossTimesLockActive(DateTime lockUntilUtc, DateTime nowUtc)
		{
			return lockUntilUtc != DateTime.MinValue && nowUtc < lockUntilUtc;
		}

		public static bool ShouldTriggerLossLock(int consecutiveLosses, int maxLosses)
		{
			return maxLosses > 0 && consecutiveLosses >= maxLosses;
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

