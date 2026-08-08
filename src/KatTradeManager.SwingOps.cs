/* KatTradeManager.SwingOps.cs - Swing SL & entry shift (partial class) v1.66 (2026-08-08) */
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		// ponytail: Swing SL shift tracking state
		private List<double> slMoveHistory = new List<double>();
		private int currentSlHistoryIndex = -1;
		private MarketPosition slTrackedPosition = MarketPosition.Flat;
		private double slTrackedEntryPrice = 0;

		// ponytail: EMA Entry shift tracking state
		private int lastEmaOrderPeriod = 0;
		private OrderAction lastEmaOrderAction = OrderAction.Buy;
		private int currentEmaTouchIndex = 0;
		private DateTime lastEmaTouchBarTime = DateTime.MinValue;

		// ponytail: Regular Candle Entry shift tracking state
		private bool hasCandleOrder = false;
		private OrderAction lastCandleOrderAction = OrderAction.Buy;
		private int currentCandleBarsAgo = 0;
		private DateTime lastCandleBarTime = DateTime.MinValue;

		private List<double> GetSwingPoints(MarketPosition position, int maxSwings = 20, int strength = 3)
		{
			List<double> empty = new List<double>();
			int availableBars;
			lock (priceLock)
				availableBars = cachedSwingBars;
			if (availableBars < strength * 2 + 1) return empty;

			int maxBarAgo = Math.Min(availableBars - strength - 1, 500);
			int count = maxBarAgo + strength + 1;
			double[] series = new double[count];
			bool findLows = position == MarketPosition.Long;
			lock (priceLock)
			{
				for (int i = 0; i < count; i++)
					series[i] = findLows ? cachedSwingLows[i] : cachedSwingHighs[i];
			}

			double tickSize = GetEffectiveTickSize();
			return KatTradeCalculator.FindSwingPoints(series, findLows, maxSwings, strength, tickSize);
		}

		private double GetSwingValidationPrice()
		{
			lock (priceLock)
			{
				if (cachedCurrentPrice > 0) return cachedCurrentPrice;
				if (cachedCurrentClose[0] > 0) return cachedCurrentClose[0];
				if (cachedCurrentHigh[0] > 0 && cachedCurrentLow[0] > 0)
					return (cachedCurrentHigh[0] + cachedCurrentLow[0]) / 2.0;
				return 0;
			}
		}

		private void ShiftSlToSwing(bool isRedo)
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
			try
			{
				Position pos = GetInstrumentPosition();
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] Swing SL: No active position to shift SL.");
					ShowHudStatus("SL: no active position", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				if (pos.MarketPosition != slTrackedPosition || Math.Abs(pos.AveragePrice - slTrackedEntryPrice) > 1e-5)
				{
					slMoveHistory.Clear();
					currentSlHistoryIndex = -1;
					slTrackedPosition = pos.MarketPosition;
					slTrackedEntryPrice = pos.AveragePrice;
				}

				var workingStops = GetAccountOrdersSnapshot().Where(o => o.Instrument == Instrument &&
					IsActiveOrderState(o.OrderState) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();
				double livePrice = GetSwingValidationPrice();

				if (slMoveHistory.Count == 0)
				{
					double currentStop = 0;
					if (workingStops.Count > 0)
					{
						currentStop = workingStops[0].StopPrice;
					}
					else
					{
						double tickSize = GetEffectiveTickSize();
						currentStop = pos.MarketPosition == MarketPosition.Long ? pos.AveragePrice - 20 * tickSize : pos.AveragePrice + 20 * tickSize;
					}
					slMoveHistory.Add(currentStop);
					currentSlHistoryIndex = 0;
				}

				double targetPrice = 0;

				if (isRedo)
				{
					if (currentSlHistoryIndex > 0)
					{
						currentSlHistoryIndex--;
						targetPrice = slMoveHistory[currentSlHistoryIndex];
					}
					else
					{
						Print("[KatTradeManager] Swing SL: Already at initial SL position.");
						ShowHudStatus("SL: already at initial position", System.Windows.Media.Brushes.OrangeRed);
						return;
					}
				}
				else
				{
					if (currentSlHistoryIndex < slMoveHistory.Count - 1)
					{
						currentSlHistoryIndex++;
						targetPrice = slMoveHistory[currentSlHistoryIndex];
					}
					else
					{
						List<double> swings = GetSwingPoints(pos.MarketPosition, 20, 3);
						double refPrice = slMoveHistory[currentSlHistoryIndex];
						double tickSize = GetEffectiveTickSize();
						double nextSwing = KatTradeCalculator.FindNextSwingStopPrice(
							swings,
							pos.MarketPosition == MarketPosition.Long ? KatOrderAction.Buy : KatOrderAction.Sell,
							refPrice,
							tickSize);

						// ponytail: no fallback to "any differing swing" — it moved the SL in the WRONG
						// direction (tightened on the loosen button). No swing in the intended direction = stop.
						if (nextSwing > 0)
						{
							// Validate BEFORE recording — an invalid-side swing must never enter history
							if (livePrice > 0 && !KatTradeCalculator.IsStopOnValidSide(pos.MarketPosition == MarketPosition.Long, nextSwing, livePrice))
							{
								Print(string.Format("[KatTradeManager] Swing SL skipped: {0} invalid vs current market {1}.", nextSwing, livePrice));
								ShowHudStatus(string.Format("SL skipped: swing {0} invalid", nextSwing), System.Windows.Media.Brushes.OrangeRed);
								return;
							}
							slMoveHistory.Add(nextSwing);
							currentSlHistoryIndex = slMoveHistory.Count - 1;
							targetPrice = nextSwing;
						}
						else
						{
							Print("[KatTradeManager] Swing SL: No further swing points found on chart.");
							ShowHudStatus("SL: no further swing found", System.Windows.Media.Brushes.OrangeRed);
							return;
						}
					}
				}

				// Historical swing can sit on the wrong side of current market (price already moved past it)
				// -> changing the stop there would be rejected by the broker.
				if (livePrice > 0 && !KatTradeCalculator.IsStopOnValidSide(pos.MarketPosition == MarketPosition.Long, targetPrice, livePrice))
				{
					Print(string.Format("[KatTradeManager] Swing SL skipped: {0} invalid vs current market {1}.", targetPrice, livePrice));
					ShowHudStatus(string.Format("SL skipped: stop {0} invalid", targetPrice), System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				if (TryRejectDisciplineForSlMove(targetPrice, out string slDiscReason))
				{
					Print(string.Format("[KatTradeManager] SL shift REJECTED by Discipline: {0}", slDiscReason));
					ShowHudStatus(slDiscReason, System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				if (workingStops.Count > 0)
				{
					List<Order> changes = new List<Order>();
					foreach (Order stopOrder in workingStops)
					{
						double limitOffset = stopOrder.OrderType == OrderType.StopLimit
							? Math.Abs(stopOrder.LimitPrice - stopOrder.StopPrice)
							: 0;
						stopOrder.StopPriceChanged = targetPrice;
						if (stopOrder.OrderType == OrderType.StopLimit)
						{
							if (limitOffset <= 0)
								limitOffset = GetEffectiveTickSize(0.01);
							if (limitOffset <= 0) limitOffset = 0.01;
							stopOrder.LimitPriceChanged = pos.MarketPosition == MarketPosition.Long
								? targetPrice - limitOffset
								: targetPrice + limitOffset;
						}
						changes.Add(stopOrder);
					}
					QueueAccountOperation(AccountOperationType.Change, changes, "swing stop change");
					Print(string.Format("[KatTradeManager] Shifted Stop Loss to Swing @ {0} (Step {1}/{2})", targetPrice, currentSlHistoryIndex, slMoveHistory.Count - 1));
				}
				else
				{
					OrderAction slAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
					Order slOrder = account.CreateOrder(Instrument, slAction, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, targetPrice, "", "KAT_SL_SWING", NinjaTrader.Core.Globals.MaxDate, null);
					if (slOrder != null)
						QueueAccountOperation(AccountOperationType.Submit, new[] { slOrder }, "swing stop submit");
					Print(string.Format("[KatTradeManager] Submitted Swing Stop Loss @ {0}", targetPrice));
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error shifting Swing SL: {0}", ex.ToString()));
			}
		}

		private void CancelWorkingEntryOrders()
		{
			if (account == null || Instrument == null) return;
			try
			{
				var workingEntries = GetAccountOrdersSnapshot().Where(o => o.Instrument == Instrument
					&& IsActiveOrderState(o.OrderState)
					&& (o.Name == "Entry" || o.Name == "MarketBuy" || o.Name == "MarketSell")).ToArray();
				if (workingEntries.Length > 0)
				{
					QueueAccountOperation(AccountOperationType.Cancel, workingEntries, "entry shift cancellation");
				}
				entryOrder = null;
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error cancelling working entry orders: {0}", ex.ToString()));
			}
		}

		private struct EmaTouchBarInfo
		{
			public int BarsAgo;
			public DateTime Time;
			public double High;
			public double Low;
		}

		private void ShiftEmaEntry(bool isForward)
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
			try
			{
				Position pos = GetInstrumentPosition();
				if (pos != null && pos.MarketPosition != MarketPosition.Flat)
				{
					Print("[KatTradeManager] Shift Entry: Position active — cannot shift entry order while in position.");
					ShowHudStatus("Entry shift: position already active", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				if (lastEmaOrderPeriod != 34 && lastEmaOrderPeriod != 89)
				{
					Print("[KatTradeManager] Shift Entry: No previous EMA 34/89 order placed in this session.");
					ShowHudStatus("Entry: place EMA 34/89 order first", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				int barIdx = GetBarsInProgressIndex();
				if (barIdx < 0 || barIdx >= NUM_SERIES) return;

				List<EmaTouchBarInfo> touchBars;
				lock (priceLock)
				{
					touchBars = lastEmaOrderPeriod == 34 ? ema34TouchLists[barIdx] : ema89TouchLists[barIdx];
				}

				if (touchBars == null || touchBars.Count == 0)
				{
					Print(string.Format("[KatTradeManager] Shift Entry: No touch candles found for EMA {0}", lastEmaOrderPeriod));
					ShowHudStatus(string.Format("Entry: no EMA {0} touch candle", lastEmaOrderPeriod), System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				var barTimes = touchBars.Select(t => t.Time).ToList();
				int targetIndex = KatTradeCalculator.CalculateShiftedBarIndex(barTimes, lastEmaTouchBarTime, currentEmaTouchIndex, isForward, out string boundaryStatus);

				if (targetIndex < 0)
				{
					if (boundaryStatus == "REACHED_NEWEST")
					{
						Print("[KatTradeManager] Shift Entry: Already at newest touch candle.");
						ShowHudStatus("Entry: already at newest touch candle", System.Windows.Media.Brushes.OrangeRed);
					}
					else if (boundaryStatus == "REACHED_OLDEST")
					{
						Print(string.Format("[KatTradeManager] Shift Entry: No older EMA {0} touch candle found (total: {1}).", lastEmaOrderPeriod, touchBars.Count));
						ShowHudStatus("Entry: no older touch candle found", System.Windows.Media.Brushes.OrangeRed);
					}
					return;
				}

				EmaTouchBarInfo targetBar = touchBars[targetIndex];
				KatOrderAction katAction = ToKatAction(lastEmaOrderAction);

				double basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, targetBar.High, targetBar.Low);

				if (basePrice <= 0)
				{
					Print("[KatTradeManager] Shift Entry: Invalid base price calculated.");
					return;
				}

				double currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				CancelWorkingEntryOrders();

				if (!PlaceOrderInternal(lastEmaOrderAction, triggerPrice, orderType, limitPrice, stopPrice, string.Format("shifting EMA {0} order to bar #{1}", lastEmaOrderPeriod, targetBar.BarsAgo), true))
					return;
				currentEmaTouchIndex = targetIndex;
				lastEmaTouchBarTime = targetBar.Time;

				string typeLabel = orderType == OrderType.StopMarket ? "Stop" : (orderType == OrderType.Limit ? "Limit" : orderType.ToString());
				Print(string.Format("[KatTradeManager] Shifted EMA {0} entry: bar #{1} (time {2}, index {3}/{4}), action={5}, type={6}, trig={7}",
					lastEmaOrderPeriod, targetBar.BarsAgo, targetBar.Time != DateTime.MinValue ? targetBar.Time.ToString("HH:mm:ss") : "N/A", targetIndex, touchBars.Count, lastEmaOrderAction, typeLabel, triggerPrice));

				ShowHudStatus(string.Format("Shift Entry EMA{0}: bar #{1} ({2} @ {3})",
					lastEmaOrderPeriod, targetBar.BarsAgo, typeLabel, triggerPrice),
					System.Windows.Media.Brushes.LightGreen);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error shifting EMA entry order: {0}", ex.ToString()));
			}
		}

		private struct CandleBarInfo
		{
			public int BarsAgo;
			public DateTime Time;
			public double High;
			public double Low;
		}

		private void ShiftCandleEntry(bool isForward)
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
			try
			{
				Position pos = GetInstrumentPosition();
				if (pos != null && pos.MarketPosition != MarketPosition.Flat)
				{
					Print("[KatTradeManager] Shift Candle Entry: Position active — cannot shift entry order while in position.");
					ShowHudStatus("Entry shift: position already active", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				if (!hasCandleOrder)
				{
					Print("[KatTradeManager] Shift Candle Entry: No previous Candle order placed in this session.");
					ShowHudStatus("Entry: place Candle order first", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				int barIdx = GetBarsInProgressIndex();
				if (barIdx < 0 || barIdx >= NUM_SERIES) return;

				List<CandleBarInfo> allBars;
				lock (priceLock)
				{
					allBars = candleBarLists[barIdx];
				}

				if (allBars == null || allBars.Count == 0) return;

				var barTimes = allBars.Select(b => b.Time).ToList();
				int targetIndex = KatTradeCalculator.CalculateShiftedBarIndex(barTimes, lastCandleBarTime, currentCandleBarsAgo, isForward, out string boundaryStatus);

				if (targetIndex < 0)
				{
					if (boundaryStatus == "REACHED_NEWEST")
					{
						Print("[KatTradeManager] Shift Candle Entry: Already at current candle.");
						ShowHudStatus("Entry: already at current candle", System.Windows.Media.Brushes.OrangeRed);
					}
					else if (boundaryStatus == "REACHED_OLDEST")
					{
						Print(string.Format("[KatTradeManager] Shift Candle Entry: No older candle found (total: {0}).", allBars.Count));
						ShowHudStatus("Entry: no older candle found", System.Windows.Media.Brushes.OrangeRed);
					}
					return;
				}

				CandleBarInfo targetBar = allBars[targetIndex];
				KatOrderAction katAction = ToKatAction(lastCandleOrderAction);

				double basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, targetBar.High, targetBar.Low);

				if (basePrice <= 0)
				{
					Print("[KatTradeManager] Shift Candle Entry: Invalid base price calculated.");
					return;
				}

				double currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				CancelWorkingEntryOrders();

				if (!PlaceOrderInternal(lastCandleOrderAction, triggerPrice, orderType, limitPrice, stopPrice, string.Format("shifting Candle order to bar #{0}", targetBar.BarsAgo), true))
					return;
				currentCandleBarsAgo = targetBar.BarsAgo;
				lastCandleBarTime = targetBar.Time;

				string typeLabel = orderType == OrderType.StopMarket ? "Stop" : (orderType == OrderType.Limit ? "Limit" : orderType.ToString());
				Print(string.Format("[KatTradeManager] Shifted Candle entry: bar #{0} (time {1}, index {2}/{3}), action={4}, type={5}, trig={6}",
					targetBar.BarsAgo, targetBar.Time != DateTime.MinValue ? targetBar.Time.ToString("HH:mm:ss") : "N/A", targetIndex, allBars.Count, lastCandleOrderAction, typeLabel, triggerPrice));

				ShowHudStatus(string.Format("Shift Entry Candle: bar #{0} ({1} @ {2})",
					targetBar.BarsAgo, typeLabel, triggerPrice),
					System.Windows.Media.Brushes.LightGreen);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error shifting Candle entry order: {0}", ex.ToString()));
			}
		}
	}
}