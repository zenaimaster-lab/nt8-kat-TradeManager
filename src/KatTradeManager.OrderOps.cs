/* KatTradeManager.OrderOps.cs - Order execution, position management & daily risk logic (partial class) v0.64 (2026-07-26) */

using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class KatTradeManager
	{
		#region Order Execution & Trading Operations
		private static KatOrderAction ToKatAction(OrderAction action) => action == OrderAction.Buy ? KatOrderAction.Buy : KatOrderAction.Sell;
		private static OrderType ToNtOrderType(KatOrderType type) => type == KatOrderType.StopMarket ? OrderType.StopMarket : OrderType.Limit;

		// Submits via ATM template when it exists on disk; falls back to plain submit otherwise.
		// StartAtmStrategy with a missing template fails silently -> created order never submitted (orphaned).
		private void SubmitOrder(Order order)
		{
			string tpl = cachedAtmTemplate;
			if (!string.IsNullOrEmpty(tpl))
			{
				string path = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy", tpl + ".xml");
				if (System.IO.File.Exists(path))
				{
					NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(tpl, order);
					return;
				}
				Print(string.Format("[KatTradeManager] ATM template '{0}' not found — submitting order WITHOUT ATM strategy", tpl));
			}
			account.Submit(new[] { order });
		}

		private DateTime lastEntrySubmitTime = DateTime.MinValue;
		private const double EntryDebounceMs = 500;

		// Blocks accidental duplicate entry submissions (mouse-jitter double-click, hotkey bounce).
		// Callers are UI-thread only, so check-then-set needs no lock.
		private bool IsEntryDebounced()
		{
			if ((DateTime.Now - lastEntrySubmitTime).TotalMilliseconds < EntryDebounceMs) return true;
			lastEntrySubmitTime = DateTime.Now;
			return false;
		}

		private int GetBarsInProgressIndex()
		{
			switch (cachedTfIndex)
			{
				case 1: return 1; // 30s
				case 2: return 2; // 1m
				case 3: return 3; // 2m
				default: return 0; // Chart TF
			}
		}

		private void PlaceOrder(OrderAction action, bool isCurrentCandle)
		{
			if (account == null || Instrument == null) return;

			try
			{
				int barIdx = GetBarsInProgressIndex();
				if (barIdx < 0 || barIdx >= NUM_SERIES) return;

				double basePrice = 0;
				double currentPx = 0;
				KatOrderAction katAction = ToKatAction(action);

				lock (priceLock)
				{
					double high  = isCurrentCandle ? cachedCurrentHigh[barIdx]  : cachedPrevHigh[barIdx];
					double low   = isCurrentCandle ? cachedCurrentLow[barIdx]   : cachedPrevLow[barIdx];
					double open  = isCurrentCandle ? cachedCurrentOpen[barIdx]  : cachedPrevOpen[barIdx];
					double close = isCurrentCandle ? cachedCurrentClose[barIdx] : cachedPrevClose[barIdx];

					basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, cachedIsPartialCandle, cachedPartialPercent, high, low, open, close, barIdx == 0 && isRenkoChart, cachedTickSize);
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}

				if (basePrice <= 0) return;

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing order");
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing order: {0}", ex.ToString()));
			}
		}

		private void PlaceFixedDistanceOrder(OrderAction action)
		{
			if (account == null || Instrument == null) return;

			try
			{
				double currentPx = 0;
				lock (priceLock)
				{
					currentPx = cachedCurrentPrice;
					if (currentPx <= 0)
					{
						int barIdx = GetBarsInProgressIndex();
						if (barIdx >= 0 && barIdx < NUM_SERIES)
							currentPx = cachedCurrentClose[barIdx] > 0 ? cachedCurrentClose[barIdx] : 0;
					}
				}

				if (currentPx <= 0) return;

				int distTicks = cachedDistanceTicks > 0 ? cachedDistanceTicks : DefaultDistanceTicks;
				KatOrderAction katAction = ToKatAction(action);
				double triggerPrice = KatTradeCalculator.CalculateFixedDistanceTriggerPrice(katAction, currentPx, distTicks, cachedTickSize);

				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing fixed distance order");
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing fixed distance order: {0}", ex.ToString()));
			}
		}

		private void PlaceEmaOrder(OrderAction action, int emaPeriod)
		{
			if (account == null || Instrument == null) return;

			try
			{
				int barIdx = GetBarsInProgressIndex();
				if (barIdx < 0 || barIdx >= NUM_SERIES) return;

				double basePrice = 0;
				double currentPx = 0;
				KatOrderAction katAction = ToKatAction(action);
				int foundBarsAgo = -1;

				lock (priceLock)
				{
					EMA targetEma = (emaPeriod == 34) ? (ema34Series != null && barIdx < ema34Series.Length ? ema34Series[barIdx] : null)
					                                  : (ema89Series != null && barIdx < ema89Series.Length ? ema89Series[barIdx] : null);

					int maxBars = CurrentBars[barIdx];
					if (targetEma != null)
					{
						for (int barsAgo = 0; barsAgo < maxBars && barsAgo < 500; barsAgo++)
						{
							double h = Highs[barIdx][barsAgo];
							double l = Lows[barIdx][barsAgo];
							double emaVal = targetEma[barsAgo];

							if (KatTradeCalculator.IsEmaTouchBar(h, l, emaVal))
							{
								foundBarsAgo = barsAgo;
								double open  = Opens[barIdx][barsAgo];
								double close = Closes[barIdx][barsAgo];
								basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, cachedIsPartialCandle, cachedPartialPercent, h, l, open, close, barIdx == 0 && isRenkoChart, cachedTickSize);
								break;
							}
						}
					}
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}


				if (foundBarsAgo < 0 || basePrice <= 0)
				{
					Print(string.Format("[KatTradeManager] No candle found touching/crossing EMA {0} on TF index {1}", emaPeriod, barIdx));
					return;
				}

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, string.Format("placing EMA {0} order", emaPeriod));
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing EMA {0} order: {1}", emaPeriod, ex.ToString()));
			}
		}


		private void PlaceOrderInternal(OrderAction action, double triggerPrice, OrderType orderType, double limitPrice, double stopPrice, string errorContext)
		{
			if (account == null || Instrument == null) return;

			if (IsDailyRiskBreached(out string breachReason))
			{
				Print(string.Format("[KatTradeManager] Order REJECTED by Daily Risk Protection: {0}", breachReason));
				return;
			}

			if (IsEntryDebounced())
			{
				Print("[KatTradeManager] Duplicate entry ignored (anti-spam debounce).");
				return;
			}

			try

			{
				KatOrderAction katAction = ToKatAction(action);
				double checkPrice = (orderType == OrderType.Market) ? (cachedCurrentPrice > 0 ? cachedCurrentPrice : triggerPrice) : triggerPrice;

				lock (priceLock)
				{
					// Validation 1: EMA Place Check
					if (cachedIsEmaPlace)
					{
						double[] emaVals = new double[3];
						int valCount = 0;
						if (EmaPlace1Enabled && emaPlaceFilterSeries != null && emaPlaceFilterSeries[0] != null && CurrentBars[emaPlaceFilterBarIdx[0]] >= 0)
							emaVals[valCount++] = emaPlaceFilterSeries[0][0];
						if (EmaPlace2Enabled && emaPlaceFilterSeries != null && emaPlaceFilterSeries[1] != null && CurrentBars[emaPlaceFilterBarIdx[1]] >= 0)
							emaVals[valCount++] = emaPlaceFilterSeries[1][0];
						if (EmaPlace3Enabled && emaPlaceFilterSeries != null && emaPlaceFilterSeries[2] != null && CurrentBars[emaPlaceFilterBarIdx[2]] >= 0)
							emaVals[valCount++] = emaPlaceFilterSeries[2][0];

						if (valCount > 0 && !KatTradeCalculator.ValidateEmaPlace(katAction, checkPrice, emaVals.Take(valCount).ToArray(), out string errPlace))
						{
							Print(string.Format("[KatTradeManager] Order REJECTED by EMA Place: {0}", errPlace));
							return;
						}
					}

					// Validation 2: EMA Angle Check
					if (cachedIsEmaAngle)
					{
						double[] currEmas = new double[3];
						double[] prevEmas = new double[3];
						double[] minAngles = new double[3];
						int angleCount = 0;

						if (EmaAngle1Enabled && emaAngleFilterSeries != null && emaAngleFilterSeries[0] != null && CurrentBars[emaAngleFilterBarIdx[0]] >= 1)
						{
							currEmas[angleCount] = emaAngleFilterSeries[0][0];
							prevEmas[angleCount] = emaAngleFilterSeries[0][1];
							minAngles[angleCount++] = EmaAngle1MinAngle;
						}
						if (EmaAngle2Enabled && emaAngleFilterSeries != null && emaAngleFilterSeries[1] != null && CurrentBars[emaAngleFilterBarIdx[1]] >= 1)
						{
							currEmas[angleCount] = emaAngleFilterSeries[1][0];
							prevEmas[angleCount] = emaAngleFilterSeries[1][1];
							minAngles[angleCount++] = EmaAngle2MinAngle;
						}
						if (EmaAngle3Enabled && emaAngleFilterSeries != null && emaAngleFilterSeries[2] != null && CurrentBars[emaAngleFilterBarIdx[2]] >= 1)
						{
							currEmas[angleCount] = emaAngleFilterSeries[2][0];
							prevEmas[angleCount] = emaAngleFilterSeries[2][1];
							minAngles[angleCount++] = EmaAngle3MinAngle;
						}

						if (angleCount > 0 && !KatTradeCalculator.ValidateEmaAngle(katAction, currEmas.Take(angleCount).ToArray(), prevEmas.Take(angleCount).ToArray(), minAngles.Take(angleCount).ToArray(), cachedTickSize, out string errAngle))
						{
							Print(string.Format("[KatTradeManager] Order REJECTED by EMA Angle: {0}", errAngle));
							return;
						}
					}
				}


				int qty = cachedQuantity > 0 ? cachedQuantity : DefaultQuantity;
				string entryName = "Entry";

				entryOrder = account.CreateOrder(Instrument, action, orderType, OrderEntry.Manual, TimeInForce.Gtc, qty, limitPrice, stopPrice, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
				if (entryOrder != null)
				{
					SubmitOrder(entryOrder);


					// Store pending draw request — OnBarUpdate (data thread) will execute the actual Draw calls
					lock (priceLock)
					{
						pendingLevels = KatTradeCalculator.CalculateAtmLevels(
							katAction, triggerPrice, atmStopLoss, atmTarget, atmBETrigger, atmSL1Trigger, atmSL2Trigger, cachedTickSize);

						pendingEntryPrice = triggerPrice;
						pendingAtmStopLoss = atmStopLoss;
						pendingAtmTarget = atmTarget;
						pendingAtmBETrigger = atmBETrigger;
						pendingAtmSL1Trigger = atmSL1Trigger;
						pendingAtmSL2Trigger = atmSL2Trigger;
					}
					pendingDrawRequest = true;
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error {0}: {1}", errorContext, ex.ToString()));
			}
		}

		private const string CloseOrderName = "KAT_CLOSE";

		// True while our own market close order is still working — double-clicking Close/Revert must not
		// submit a second close (position flip) or cancel the in-flight one.
		private bool IsCloseInFlight()
		{
			if (account == null || Instrument == null) return false;
			try
			{
				return account.Orders.Any(o => o.Instrument == Instrument && o.Name == CloseOrderName
					&& (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted));
			}
			catch { return false; }
		}

		private void CancelAllOrders()
		{
			if (account == null) return;
			try
			{
				// Never cancel our own close order — a just-submitted close can already be Accepted here
				var workingOrders = account.Orders.Where(o => o.Name != CloseOrderName
					&& (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)).ToArray();
				if (workingOrders.Length > 0)
					account.Cancel(workingOrders);
				entryOrder = null;
				pendingRemoveLines = true; // ponytail: single removal path — OnBarUpdate (data thread) executes it
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error cancelling orders: {0}", ex.ToString()));
			}
		}

		private void ClosePosition()
		{
			if (account == null || Instrument == null) return;
			try
			{
				CancelAllOrders();

				if (IsCloseInFlight())
				{
					Print("[KatTradeManager] Close already in flight — duplicate close ignored");
					return;
				}

				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos != null && pos.MarketPosition != MarketPosition.Flat)
				{
					OrderAction action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.Buy;
					Order closeOrder = account.CreateOrder(Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, 0, "", CloseOrderName, NinjaTrader.Core.Globals.MaxDate, null);
					if (closeOrder != null)
						account.Submit(new[] { closeOrder });
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error closing position: {0}", ex.ToString()));
			}
		}

		private void PlaceMarketOrder(OrderAction action)
		{
			if (account == null || Instrument == null) return;

			if (IsDailyRiskBreached(out string breachReason))
			{
				Print(string.Format("[KatTradeManager] Market Order REJECTED by Daily Risk Protection: {0}", breachReason));
				return;
			}

			if (IsEntryDebounced())
			{
				Print("[KatTradeManager] Duplicate market order ignored (anti-spam debounce).");
				return;
			}

			try

			{
				int qty = cachedQuantity > 0 ? cachedQuantity : DefaultQuantity;
				string entryName = action == OrderAction.Buy ? "MarketBuy" : "MarketSell";

				entryOrder = account.CreateOrder(Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, qty, 0, 0, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
				if (entryOrder != null)
				{
					SubmitOrder(entryOrder);
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing market order: {0}", ex.ToString()));
			}
		}

		private void SetBreakeven()
		{
			if (account == null || Instrument == null) return;
			try
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] BE: No active position to set Breakeven.");
					return;
				}

				double tickSize = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
				int bufferTicks = cachedBufferTicks >= 0 ? cachedBufferTicks : DefaultBufferTicks;
				KatOrderAction katAction = pos.MarketPosition == MarketPosition.Long ? KatOrderAction.Buy : KatOrderAction.Sell;

				double bePrice = KatTradeCalculator.CalculateBreakevenPrice(katAction, pos.AveragePrice, bufferTicks, tickSize);

				// Underwater position: BE stop would sit on the wrong side of market -> broker rejection
				if (!KatTradeCalculator.IsStopOnValidSide(pos.MarketPosition == MarketPosition.Long, bePrice, cachedCurrentPrice))
				{
					Print(string.Format("[KatTradeManager] BE skipped: stop {0} invalid vs current market {1} (position underwater or price unavailable).", bePrice, cachedCurrentPrice));
					return;
				}

				var workingStops = account.Orders.Where(o => o.Instrument == Instrument &&
					(o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();

				if (workingStops.Count > 0)
				{
					foreach (Order stopOrder in workingStops)
					{
						stopOrder.StopPrice = bePrice;
						account.Change(new[] { stopOrder });
					}
					Print(string.Format("[KatTradeManager] Moved {0} Stop Loss order(s) to Breakeven @ {1} (Buffer: {2} ticks)", workingStops.Count, bePrice, bufferTicks));
				}
				else
				{
					OrderAction slAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
					Order slOrder = account.CreateOrder(Instrument, slAction, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, bePrice, "", "KAT_SL_BE", NinjaTrader.Core.Globals.MaxDate, null);
					if (slOrder != null)
						account.Submit(new[] { slOrder });
					Print(string.Format("[KatTradeManager] Submitted Breakeven Stop Loss @ {0} (Buffer: {1} ticks)", bePrice, bufferTicks));
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error setting Breakeven: {0}", ex.ToString()));
			}
		}

		private void RevertPosition()
		{
			if (account == null || Instrument == null) return;
			try
			{
				if (IsCloseInFlight())
				{
					Print("[KatTradeManager] Revert: close already in flight — wait for fill before reverting again.");
					return;
				}

				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] Revert: No active position to revert.");
					return;
				}

				OrderAction oppositeAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.Buy;
				ClosePosition();
				PlaceMarketOrder(oppositeAction);
				Print(string.Format("[KatTradeManager] Reverted position to {0}", oppositeAction));
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error reverting position: {0}", ex.ToString()));
			}
		}

		private DateTime lastFreezeEnforceTime = DateTime.MinValue;

		private void FreezeCurrentStopLoss()
		{
			if (account == null || Instrument == null) return;
			frozenStopPrice = 0; // clear stale value from a previous freeze episode — enforcement re-captures fresh
			try
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] Freeze Trail: No active position to freeze.");
					return;
				}

				var workingStops = account.Orders.Where(o => o.Instrument == Instrument &&
					(o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();

				if (workingStops.Count > 0)
				{
					frozenStopPrice = workingStops[0].StopPrice;
					Print(string.Format("[KatTradeManager] Freeze Trail active @ Stop Loss price: {0}", frozenStopPrice));
				}
				else
				{
					Print("[KatTradeManager] Freeze Trail active: Waiting for working Stop Loss order.");
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error freezing Stop Loss: {0}", ex.ToString()));
			}
		}

		private void CheckFreezeTrailEnforcement()
		{
			if (!cachedIsFreezeTrail || account == null || Instrument == null) return;
			try
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					frozenStopPrice = 0;
					return;
				}

				// Rate-limit check: only evaluate enforcement at most once every 3 seconds to avoid API spamming
				if ((DateTime.Now - lastFreezeEnforceTime).TotalMilliseconds < 3000)
					return;

				var workingStops = account.Orders.Where(o => o.Instrument == Instrument &&
					(o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();

				if (workingStops.Count == 0) return;

				if (frozenStopPrice <= 0)
				{
					frozenStopPrice = workingStops[0].StopPrice;
					Print(string.Format("[KatTradeManager] Freeze Trail captured SL price @ {0}", frozenStopPrice));
					return;
				}

				foreach (Order stopOrder in workingStops)
				{
					if (Math.Abs(stopOrder.StopPrice - frozenStopPrice) > 0.000001)
					{
						lastFreezeEnforceTime = DateTime.Now;
						stopOrder.StopPrice = frozenStopPrice;
						account.Change(new[] { stopOrder });
						Print(string.Format("[KatTradeManager] Trailing movement overridden — SL restored to frozen price {0}", frozenStopPrice));
					}
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error enforcing Freeze Trail: {0}", ex.ToString()));
			}
		}

		// ponytail: Swing SL shift tracking state
		private List<double> slMoveHistory = new List<double>();
		private int currentSlHistoryIndex = -1;
		private MarketPosition slTrackedPosition = MarketPosition.Flat;
		private double slTrackedEntryPrice = 0;

		private List<double> GetSwingPoints(MarketPosition position, int maxSwings = 20, int strength = 3)
		{
			List<double> empty = new List<double>();
			if (CurrentBars[0] < strength * 2 + 1) return empty;

			int maxBarAgo = Math.Min(CurrentBars[0] - strength - 1, 500);
			int count = maxBarAgo + strength + 1;
			double[] series = new double[count];
			bool findLows = position == MarketPosition.Long;
			for (int i = 0; i < count; i++)
				series[i] = findLows ? Lows[0][i] : Highs[0][i];

			double tickSize = cachedTickSize > 0 ? cachedTickSize : (Instrument != null ? Instrument.MasterInstrument.TickSize : 0.25);
			return KatTradeCalculator.FindSwingPoints(series, findLows, maxSwings, strength, tickSize);
		}

		private void ShiftSlToSwing(bool isRedo)
		{
			if (account == null || Instrument == null) return;
			try
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] Swing SL: No active position to shift SL.");
					return;
				}

				if (pos.MarketPosition != slTrackedPosition || Math.Abs(pos.AveragePrice - slTrackedEntryPrice) > 1e-5)
				{
					slMoveHistory.Clear();
					currentSlHistoryIndex = -1;
					slTrackedPosition = pos.MarketPosition;
					slTrackedEntryPrice = pos.AveragePrice;
				}

				var workingStops = account.Orders.Where(o => o.Instrument == Instrument &&
					(o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();

				if (slMoveHistory.Count == 0)
				{
					double currentStop = 0;
					if (workingStops.Count > 0)
					{
						currentStop = workingStops[0].StopPrice;
					}
					else
					{
						double tickSize = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
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
						double tickSize = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;

						double nextSwing = 0;
						foreach (double s in swings)
						{
							if (pos.MarketPosition == MarketPosition.Long && s < refPrice - tickSize * 0.5)
							{
								nextSwing = s;
								break;
							}
							else if (pos.MarketPosition == MarketPosition.Short && s > refPrice + tickSize * 0.5)
							{
								nextSwing = s;
								break;
							}
						}

						// ponytail: no fallback to "any differing swing" — it moved the SL in the WRONG
						// direction (tightened on the loosen button). No swing in the intended direction = stop.
						if (nextSwing > 0)
						{
							// Validate BEFORE recording — an invalid-side swing must never enter history
							if (!KatTradeCalculator.IsStopOnValidSide(pos.MarketPosition == MarketPosition.Long, nextSwing, cachedCurrentPrice))
							{
								Print(string.Format("[KatTradeManager] Swing SL skipped: {0} invalid vs current market {1}.", nextSwing, cachedCurrentPrice));
								return;
							}
							slMoveHistory.Add(nextSwing);
							currentSlHistoryIndex = slMoveHistory.Count - 1;
							targetPrice = nextSwing;
						}
						else
						{
							Print("[KatTradeManager] Swing SL: No further swing points found on chart.");
							return;
						}
					}
				}

				// Historical swing can sit on the wrong side of current market (price already moved past it)
				// -> changing the stop there would be rejected by the broker.
				if (!KatTradeCalculator.IsStopOnValidSide(pos.MarketPosition == MarketPosition.Long, targetPrice, cachedCurrentPrice))
				{
					Print(string.Format("[KatTradeManager] Swing SL skipped: {0} invalid vs current market {1}.", targetPrice, cachedCurrentPrice));
					return;
				}

				if (workingStops.Count > 0)
				{
					foreach (Order stopOrder in workingStops)
					{
						stopOrder.StopPrice = targetPrice;
						account.Change(new[] { stopOrder });
					}
					Print(string.Format("[KatTradeManager] Shifted Stop Loss to Swing @ {0} (Step {1}/{2})", targetPrice, currentSlHistoryIndex, slMoveHistory.Count - 1));
				}
				else
				{
					OrderAction slAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
					Order slOrder = account.CreateOrder(Instrument, slAction, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, targetPrice, "", "KAT_SL_SWING", NinjaTrader.Core.Globals.MaxDate, null);
					if (slOrder != null)
						account.Submit(new[] { slOrder });
					Print(string.Format("[KatTradeManager] Submitted Swing Stop Loss @ {0}", targetPrice));
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error shifting Swing SL: {0}", ex.ToString()));
			}
		}
		#endregion

		#region Daily Risk Protection Logic
		private double CalculateDailyPnL()
		{
			if (account == null) return 0;

			DateTime currentSessionStartUtc = KatTradeCalculator.GetNySessionStartUtc(DateTime.UtcNow);
			double currentRealizedPnL = 0;
			try
			{
				currentRealizedPnL = account.Get(AccountItem.GrossRealizedProfitLoss, Currency.UsDollar);
			}
			catch {}

			if (!isSessionStartCaptured || currentSessionStartUtc > lastSessionStartUtc)
			{
				lastSessionStartUtc = currentSessionStartUtc;
				sessionStartRealizedPnL = currentRealizedPnL;
				isSessionStartCaptured = true;
			}

			double dailyRealized = currentRealizedPnL - sessionStartRealizedPnL;

			double dailyUnrealized = 0;
			try
			{
				dailyUnrealized = account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
			}
			catch {}

			return dailyRealized + dailyUnrealized;
		}


		private bool IsDailyRiskBreached(out string breachReason)
		{
			breachReason = string.Empty;
			if (account == null) return false;

			double dailyPnL = CalculateDailyPnL();
			cachedDailyPnL = dailyPnL;

			if (cachedIsDailyMaxDD && cachedDailyMaxDD > 0 && dailyPnL <= -Math.Abs(cachedDailyMaxDD))
			{
				breachReason = string.Format("Daily Max DD breached (Current Daily PnL: ${0:F2} <= Max DD limit: -${1:F2})", dailyPnL, Math.Abs(cachedDailyMaxDD));
				return true;
			}

			if (cachedIsDailyMaxProfit && cachedDailyMaxProfit > 0 && dailyPnL >= cachedDailyMaxProfit)
			{
				breachReason = string.Format("Daily Max Profit reached (Current Daily PnL: ${0:F2} >= Max Profit limit: ${1:F2})", dailyPnL, cachedDailyMaxProfit);
				return true;
			}

			return false;
		}

		private void EvaluateDailyRiskLimits()
		{
			if (account == null || isTerminated) return;

			if (IsDailyRiskBreached(out string breachReason))
			{
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				bool hasOpenPos = (pos != null && pos.MarketPosition != MarketPosition.Flat);
				bool hasWorkingOrders = account.Orders.Any(o => (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) && o.Instrument == Instrument);

				// ponytail: flatten once per breach episode — flag resets when PnL recovers, prevents order spam from 500ms watchdog
				// Interlocked: this method runs on BOTH data thread (OnBarUpdate) and UI thread (watchdog) —
				// check-then-set on a bool raced and could submit ClosePosition twice (position flip).
				if (hasOpenPos || hasWorkingOrders)
				{
					if (System.Threading.Interlocked.CompareExchange(ref dailyRiskFlattened, 1, 0) == 0)
					{
						Print(string.Format("[KatTradeManager] EMERGENCY FLATTEN triggered by Daily Risk Protection: {0}", breachReason));
						ClosePosition();
					}
				}
			}
			else
			{
				System.Threading.Interlocked.Exchange(ref dailyRiskFlattened, 0);
			}
		}
		#endregion
	}
}
