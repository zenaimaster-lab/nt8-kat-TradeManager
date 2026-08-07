/* KatTradeManager.OrderOps.cs - Order execution & position management (partial class) v1.41 (2026-08-08) */

using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		#region Order Execution & Trading Operations
		private enum AccountOperationType
		{
			Submit,
			Change,
			Cancel
		}

		private sealed class AccountOperationRequest
		{
			public AccountOperationType Type;
			public Order[] Orders;
			public string Reason;
			public readonly List<Action> Completions = new List<Action>();
			public Action ExecuteOverride;
			public volatile bool CallReturned;
		}

		private readonly object accountOperationLock = new object();
		private readonly Queue<AccountOperationRequest> accountOperationQueue = new Queue<AccountOperationRequest>();
		private AccountOperationRequest activeAccountOperation;
		private int accountOperationPumpScheduled;
		private DateTime activeAccountOperationSinceUtc = DateTime.MinValue;
		private DateTime queueHeadStallSinceUtc = DateTime.MinValue;
		// A broker state stuck pending (ChangePending/CancelPending hang) would pin the FIFO forever —
		// every later submit/change/cancel, including Close/flatten, would starve behind it. 10s ceiling.
		private const double AccountOperationSettleTimeoutMs = 10000.0;
		private int closeOperationQueued;
		private Order atmStartupOrder;
		private readonly object flattenCloseLock = new object();
		private readonly List<Order> flattenCloseOrders = new List<Order>();
		private const double AtmLifecycleGraceMilliseconds = 3000.0;
		private DateTime atmLastLifecycleActivityUtc = DateTime.MinValue;
		private bool atmPositionWasConfirmedThisEpisode;
		private Order atmDeferLoggedStartup;

		// ponytail: queue extracted to KatTradeManager.Queue.cs (partial class)

		// NT8 mutates Orders/Positions from the broker thread while the UI thread and watchdog read them;
		// an unlocked LINQ pass throws "Collection was modified" mid-enumeration. Always snapshot under lock.
		private Position GetInstrumentPosition()
		{
			if (account == null || Instrument == null) return null;
			var positions = account.Positions;
			lock (positions)
				return positions.FirstOrDefault(p => p.Instrument == Instrument);
		}

		private List<Order> GetAccountOrdersSnapshot()
		{
			var orders = account.Orders;
			lock (orders)
				return orders.ToList();
		}

		private List<Position> GetAccountPositionsSnapshot()
		{
			var positions = account.Positions;
			lock (positions)
				return positions.ToList();
		}

		// Central account switch point: resets the per-account session baseline so the previous account's
		// realized PnL cannot phantom-breach (or blind) daily risk on the new account.
		private void SwitchAccount(Account next)
		{
			if (ReferenceEquals(account, next)) return;
			account = next;
			isSessionStartCaptured = false;
			System.Threading.Interlocked.Exchange(ref dailyRiskFlattened, 0);
			// A revert queued on the OLD account must never fire a market order on the new one.
			System.Threading.Interlocked.Exchange(ref pendingRevertAction, 0);
			System.Threading.Interlocked.Exchange(ref pendingRevertQuantity, 0);
			EnsureAccountEventSubscription();
		}

		private bool IsAccountCloseInFlight()
		{
			try
			{
				return account != null && GetAccountOrdersSnapshot().Any(o => o.Name == CloseOrderName && IsActiveOrderState(o.OrderState));
			}
			catch
			{
				return false;
			}
		}

			// ponytail: ATM merge extracted to KatTradeManager.AtmMerge.cs (partial class)

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
			Print(string.Format("[KatTradeManager] PlaceOrder click: {0} {1}", action, isCurrentCandle ? "current" : "previous"));
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}

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

					basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, high, low);
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}

				if (basePrice <= 0)
				{
					Print(string.Format("[KatTradeManager] PlaceOrder aborted: basePrice={0} (no bar data cached yet — wait for live ticks)", basePrice));
					ShowHudStatus("Order aborted: no price data yet", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				if (PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing order", true))
				{
					hasCandleOrder = true;
					lastCandleOrderAction = action;
					currentCandleBarsAgo = isCurrentCandle ? 0 : 1;
					lock (priceLock)
					{
						lastCandleBarTime = isCurrentCandle ? cachedCurrentBarTime[barIdx] : cachedPrevBarTime[barIdx];
					}
					ShowHudStatus(string.Format("{0} {1} candle @ {2}", action == OrderAction.Buy ? "BUY" : "SELL", isCurrentCandle ? "curr" : "prev", triggerPrice), System.Windows.Media.Brushes.LightGreen);
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing order: {0}", ex.ToString()));
			}
		}

		private void PlaceEmaOrder(OrderAction action, int emaPeriod)
		{
			Print(string.Format("[KatTradeManager] PlaceEmaOrder click: {0} EMA{1}", action, emaPeriod));
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}

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
					if (emaPeriod == 34)
					{
						foundBarsAgo = ema34TouchBarsAgo[barIdx];
						basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, ema34TouchHigh[barIdx], ema34TouchLow[barIdx]);
					}
					else if (emaPeriod == 89)
					{
						foundBarsAgo = ema89TouchBarsAgo[barIdx];
						basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, ema89TouchHigh[barIdx], ema89TouchLow[barIdx]);
					}
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}


				if (foundBarsAgo < 0 || basePrice <= 0)
				{
					Print(string.Format("[KatTradeManager] No candle found touching/crossing EMA {0} on TF index {1}", emaPeriod, barIdx));
					ShowHudStatus(string.Format("No EMA {0} touch candle found", emaPeriod), System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				if (PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, string.Format("placing EMA {0} order", emaPeriod), true))
				{
					lastEmaOrderPeriod = emaPeriod;
					lastEmaOrderAction = action;
					currentEmaTouchIndex = 0;
					lock (priceLock)
					{
						lastEmaTouchBarTime = emaPeriod == 34 ? ema34TouchTime[barIdx] : ema89TouchTime[barIdx];
					}
					ShowHudStatus(string.Format("{0} EMA{1} @ {2}", action == OrderAction.Buy ? "BUY" : "SELL", emaPeriod, triggerPrice), System.Windows.Media.Brushes.LightGreen);
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing EMA {0} order: {1}", emaPeriod, ex.ToString()));
			}
		}


		
		/// <summary>
		/// When HUD "Ema protect" is ON: BUY entry must sit strictly above every enabled Settings EMA
		/// (period+TF, default EMA9/34/89 on 5m); SELL must sit strictly below. Returns true if rejected.
		/// </summary>
		private bool TryRejectEmaProtect(OrderAction action, double checkPrice)
		{
			if (!cachedIsEmaPlace) return false;

			lock (priceLock)
			{
				// Slot = Settings EMA Place 1/2/3 (Enabled + Period + Timeframe series cache).
				int[] periods = { EmaPlace1Period, EmaPlace2Period, EmaPlace3Period };
				bool[] enabled = { EmaPlace1Enabled, EmaPlace2Enabled, EmaPlace3Enabled };

				double[] emaVals = new double[3];
				int[] emaPeriods = new int[3];
				int valCount = 0;
				for (int i = 0; i < 3; i++)
				{
					if (!enabled[i]) continue;
					double v = cachedEmaPlaceValues[i];
					if (v <= 0)
					{
						string wait = string.Format("EMA{0} data not ready", periods[i]);
						Print(string.Format("[KatTradeManager] Order REJECTED by EMA Protect: {0}", wait));
						ShowHudStatus(string.Format("EMA Protect blocked: {0}", wait), System.Windows.Media.Brushes.OrangeRed);
						return true;
					}
					emaVals[valCount] = v;
					emaPeriods[valCount] = periods[i];
					valCount++;
				}

				if (valCount == 0) return false;

				KatOrderAction katAction = ToKatAction(action);
				for (int i = 0; i < valCount; i++)
				{
					if (KatTradeCalculator.ValidateEmaPlace(katAction, checkPrice, new[] { emaVals[i] }, out string errPlace))
						continue;

					string msg = string.Format("entry {0} vs EMA{1}={2}", checkPrice, emaPeriods[i], emaVals[i]);
					Print(string.Format("[KatTradeManager] Order REJECTED by EMA Protect: {0} ({1})", errPlace, msg));
					ShowHudStatus(string.Format("EMA Protect blocked: {0}", msg), System.Windows.Media.Brushes.OrangeRed);
					return true;
				}
				return false;
			}
		}

		private bool PlaceOrderInternal(OrderAction action, double triggerPrice, OrderType orderType, double limitPrice, double stopPrice, string errorContext, bool applyEmaFilters)
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return false;
			}

			if (IsDailyRiskBreached(out string breachReason))
			{
				Print(string.Format("[KatTradeManager] Order REJECTED by Daily Risk Protection: {0}", breachReason));
				ShowHudStatus(string.Format("Daily Risk blocked: {0}", breachReason), System.Windows.Media.Brushes.OrangeRed);
				return false;
			}

			if (IsEntryDebounced())
			{
				Print("[KatTradeManager] Duplicate entry ignored (anti-spam debounce).");
				return false;
			}

			int discQty = atmQuantity > 0 ? atmQuantity : DefaultQuantity;
			if (TryRejectDisciplineForEntry(action, discQty, triggerPrice, out string discReason))
			{
				Print(string.Format("[KatTradeManager] Order REJECTED by Discipline: {0}", discReason));
				ShowHudStatus(discReason, System.Windows.Media.Brushes.OrangeRed);
				return false;
			}

			try

			{
				KatOrderAction katAction = ToKatAction(action);
				double checkPrice = (orderType == OrderType.Market) ? (cachedCurrentPrice > 0 ? cachedCurrentPrice : triggerPrice) : triggerPrice;

				if (applyEmaFilters && TryRejectEmaProtect(action, checkPrice))
					return false;

				// Re-evaluate Stop vs Limit using the *latest* cachedCurrentPrice right before submit.
				// If price has run past the intended stop (making StopMarket invalid), flip to Limit order.
				// This prevents broker rejections like "sell stop price must be below trade price".
				double liveCurrent = cachedCurrentPrice > 0 ? cachedCurrentPrice : 0;
				if (liveCurrent <= 0) liveCurrent = triggerPrice;

				KatOrderType liveKatType = KatTradeCalculator.DetermineOrderType(
					katAction, triggerPrice, liveCurrent, cachedTickSize, out double liveLimitPrice, out double liveStopPrice);
				OrderType liveOrderType = ToNtOrderType(liveKatType);

				if (cachedIsStopLimit && liveOrderType == OrderType.StopMarket)
				{
					double stopLimitOffset = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
					if (stopLimitOffset <= 0) stopLimitOffset = 0.01;
					KatTradeCalculator.CalculateStopLimitPrices(katAction, triggerPrice, stopLimitOffset, out liveLimitPrice, out liveStopPrice);
					liveOrderType = OrderType.StopLimit;
					Print(string.Format("[KatTradeManager] Pending stop mode: StopLimit stop={0} limit={1}",
						liveStopPrice, liveLimitPrice));
				}

				if (liveOrderType != orderType)
				{
					Print(string.Format("[KatTradeManager] Price moved past intended stop — FLIPPING {0} → {1} (trigger={2}, live={3})",
						orderType, liveOrderType, triggerPrice, liveCurrent));
				}

				orderType = liveOrderType;
				limitPrice = liveLimitPrice;
				stopPrice = liveStopPrice;

				int qty = atmQuantity > 0 ? atmQuantity : DefaultQuantity;
				string entryName = "Entry";

				entryOrder = account.CreateOrder(Instrument, action, orderType, OrderEntry.Manual, TimeInForce.Gtc, qty, limitPrice, stopPrice, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
				if (entryOrder != null)
				{
					if (!SubmitOrder(entryOrder))
					{
						entryOrder = null;
						return false;
					}
					Print(string.Format("[KatTradeManager] Order submitted: {0} {1} @ {2} qty={3} atm={4}",
						action, orderType, triggerPrice, qty, cachedAtmTemplate ?? "(none)"));

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
					pendingDrawOrder = entryOrder;
					pendingDrawRequest = true;
					return true;
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error {0}: {1}", errorContext, ex.ToString()));
			}
			return false;
		}

		private const string CloseOrderName = "KAT_CLOSE";

		// True while our own market close order is still working — double-clicking Close/Revert must not
		// submit a second close (position flip) or cancel the in-flight one.
		private bool IsCloseInFlight()
		{
			if (account == null || Instrument == null) return false;
			if (System.Threading.Volatile.Read(ref closeOperationQueued) != 0)
				return true;
			try
			{
				return GetAccountOrdersSnapshot().Any(o => o.Instrument == Instrument && o.Name == CloseOrderName
					&& IsActiveOrderState(o.OrderState));
			}
			catch { return false; }
		}

		private static bool IsActiveOrderState(OrderState state)
		{
			return state == OrderState.Initialized
				|| state == OrderState.Submitted
				|| state == OrderState.Accepted
				|| state == OrderState.AcceptedByRisk
				|| state == OrderState.Working
				|| state == OrderState.TriggerPending
				|| state == OrderState.ChangePending
				|| state == OrderState.ChangeSubmitted
				|| state == OrderState.PartFilled
				|| state == OrderState.Suspended
				|| state == OrderState.CancelPending
				|| state == OrderState.CancelSubmitted;
		}

		private static bool IsTerminalOrderState(OrderState state)
		{
			return state == OrderState.Filled
				|| state == OrderState.Cancelled
				|| state == OrderState.Rejected;
		}

		private void EnsureAccountEventSubscription()
		{
			if (ReferenceEquals(subscribedAccount, account)) return;
			RemoveAccountEventSubscription();
			subscribedAccount = account;
			if (subscribedAccount != null)
				subscribedAccount.OrderUpdate += OnAccountOrderUpdate;
		}

		private void RemoveAccountEventSubscription()
		{
			if (subscribedAccount != null)
				subscribedAccount.OrderUpdate -= OnAccountOrderUpdate;
			subscribedAccount = null;
			ClearAtmStartup();
			ResetAtmScaleInTracking();
			ResetAccountOperationQueue();
		}

		private void OnAccountOrderUpdate(object sender, OrderEventArgs e)
		{
			// Boundary catch: this runs on the broker event thread — a transient NT8-internal
			// failure must surface as a log line, not an unhandled-exception dialog.
			try
			{
				OnAccountOrderUpdateCore(e);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Account order-update error: {0}", ex.Message));
			}
		}

		private void OnAccountOrderUpdateCore(OrderEventArgs e)
		{
			Order observed = e != null ? e.Order : null;
			if (observed == null)
				return;
			ProcessAtmStartupUpdate(observed);
			TryCompleteActiveAccountOperation();
			bool isOurInstrument = Instrument != null && observed.Instrument == Instrument;
			if (isOurInstrument && IsAtmLifecycleOrder(observed))
				MarkAtmLifecycleActivity();

			if (observed.OrderType == OrderType.StopMarket
				|| observed.OrderType == OrderType.StopLimit
				|| observed.OrderType == OrderType.Limit)
			{
				Print(string.Format(
					"[KatTradeManager] ATM order identity: name={0} id={1} oco={2} from={3} action={4} type={5} state={6} qty={7} filled={8} stop={9} limit={10}",
					observed.Name ?? string.Empty,
					observed.OrderId ?? string.Empty,
					observed.Oco ?? string.Empty,
					observed.FromEntrySignal ?? string.Empty,
					observed.OrderAction,
					observed.OrderType,
					observed.OrderState,
					observed.Quantity,
					observed.Filled,
					observed.StopPrice,
					observed.LimitPrice));
			}

			bool tracked = observed.Name == CloseOrderName
				|| observed.Name == "Entry"
				|| observed.Name == "MarketBuy"
				|| observed.Name == "MarketSell";
			if (tracked)
			{
				Print(string.Format("[KatTradeManager] Account order update: name={0} action={1} type={2} state={3}",
					observed.Name, observed.OrderAction, observed.OrderType, observed.OrderState));
			}

			if (observed.Name == CloseOrderName && IsTerminalOrderState(observed.OrderState))
			{
				bool releaseFlatten = false;
				lock (flattenCloseLock)
				{
					if (flattenCloseOrders.Count == 0)
						releaseFlatten = true;
					else
					{
						flattenCloseOrders.RemoveAll(order => SameOrder(order, observed));
						releaseFlatten = flattenCloseOrders.Count == 0;
					}
				}
				if (releaseFlatten)
					System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				SchedulePendingRevertRetry();
			}
			if (isOurInstrument)
			{
				ProcessAtmScaleInUpdate(observed);
				ScheduleAtmBracketMerge();
			}
			try { UpdateDisciplineFromPosition(); } catch {}
			try { EvaluateDisciplineLockVisual(); } catch {}
		}

		private void SchedulePendingRevertRetry()
		{
			if (ChartControl != null && ChartControl.Dispatcher != null)
				ChartControl.Dispatcher.BeginInvoke(new Action(TrySubmitPendingRevert));
			else
				TrySubmitPendingRevert();
		}

		// ponytail: intentionally ACCOUNT-WIDE (no Instrument filter) — matches "Close/flatten" and
		// account-level daily-risk semantics. Every other order query in this class is Instrument-scoped.
		private void CancelAllOrders(Action afterCancel = null)
		{
			if (account == null) return;
			try
			{
				// Never cancel our own close order — a just-submitted close can already be Accepted here.
				var workingOrders = GetAccountOrdersSnapshot().Where(o => o.Name != CloseOrderName
					&& IsActiveOrderState(o.OrderState)).ToArray();
				QueueAccountOperation(AccountOperationType.Cancel, workingOrders, "close/flatten cancellation", afterCancel);
				entryOrder = null;
				pendingRemoveLines = true; // ponytail: single removal path — OnBarUpdate (data thread) executes it
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error cancelling orders: {0}", ex.ToString()));
			}
		}

		private void SubmitQueuedClose()
		{
			if (account == null || Instrument == null)
			{
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				return;
			}

			try
			{
				Position pos = GetInstrumentPosition();
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					return;
				}

				OrderAction action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
				Order closeOrder = account.CreateOrder(Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, 0, "", CloseOrderName, NinjaTrader.Core.Globals.MaxDate, null);
				if (closeOrder == null)
				{
					System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					Print("[KatTradeManager] Close order creation returned null.");
					return;
				}

				QueueAccountOperation(
					AccountOperationType.Submit,
					new[] { closeOrder },
					"close/flatten submit",
					completion: () =>
					{
						if (IsTerminalOrderState(closeOrder.OrderState))
							System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					});
				Print(string.Format("[KatTradeManager] Close submit queued: action={0} qty={1} state={2}",
					action, pos.Quantity, closeOrder.OrderState));
			}
			catch (Exception ex)
			{
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				Print(string.Format("[KatTradeManager] Error queuing close position: {0}", ex));
			}
		}
		private void ClosePosition()
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
			try
			{
				if (TryRejectDisciplineForClose(out string discCloseReason))
				{
					Print(string.Format("[KatTradeManager] Close REJECTED by Discipline: {0}", discCloseReason));
					ShowHudStatus(discCloseReason, System.Windows.Media.Brushes.OrangeRed);
					return;
				}
				if (System.Threading.Volatile.Read(ref closeOperationQueued) != 0
					|| IsCloseInFlight()
					|| IsAccountCloseInFlight())
				{
					Print("[KatTradeManager] Close already in flight — duplicate close ignored");
					return;
				}

				Position pos = GetInstrumentPosition();
				if (pos != null && pos.MarketPosition != MarketPosition.Flat)
				{
					if (System.Threading.Interlocked.CompareExchange(ref closeOperationQueued, 1, 0) != 0)
						return;
					CancelAllOrders(SubmitQueuedClose);
					Print(string.Format("[KatTradeManager] Close sequence queued: qty={0}", pos.Quantity));
				}
			}
			catch (Exception ex)
			{
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				Print(string.Format("[KatTradeManager] Error closing position: {0}", ex.ToString()));
			}
		}

		// Close/flatten button + hotkey: clear the ENTIRE account — cancel every working order and
		// market-close every open position across all instruments. Revert and daily-risk keep the
		// instrument-scoped ClosePosition path; they must not flatten unrelated positions.
		private void FlattenAllPositions()
		{
			if (account == null)
			{
				Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
			try
			{
				if (TryRejectDisciplineForClose(out string discFlatReason))
				{
					Print(string.Format("[KatTradeManager] Flatten REJECTED by Discipline: {0}", discFlatReason));
					ShowHudStatus(discFlatReason, System.Windows.Media.Brushes.OrangeRed);
					return;
				}
				if (System.Threading.Volatile.Read(ref closeOperationQueued) != 0
					|| IsCloseInFlight()
					|| IsAccountCloseInFlight())
				{
					Print("[KatTradeManager] Flatten already in flight — duplicate ignored");
					return;
				}

				bool hasWorkingOrders = GetAccountOrdersSnapshot().Any(o => IsActiveOrderState(o.OrderState));
				bool hasOpenPosition = GetAccountPositionsSnapshot().Any(p => p.MarketPosition != MarketPosition.Flat);
				if (!KatTradeCalculator.ShouldFlattenAccount(hasWorkingOrders, hasOpenPosition))
				{
					Print("[KatTradeManager] Flatten: account already clear — no orders or positions");
					return;
				}

				if (System.Threading.Interlocked.CompareExchange(ref closeOperationQueued, 1, 0) != 0)
					return;
				System.Threading.Interlocked.Exchange(ref pendingRevertAction, 0);
				System.Threading.Interlocked.Exchange(ref pendingRevertQuantity, 0);
				CancelAllOrders(SubmitQueuedFlattenAll);
				Print("[KatTradeManager] Flatten-all queued: cancel all orders then close all positions");
			}
			catch (Exception ex)
			{
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				Print(string.Format("[KatTradeManager] Error flattening account: {0}", ex.ToString()));
			}
		}

		private void SubmitQueuedFlattenAll()
		{
			if (account == null)
			{
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				return;
			}

			try
			{
				List<Position> openPositions = GetAccountPositionsSnapshot()
					.Where(p => p.MarketPosition != MarketPosition.Flat)
					.ToList();
				if (openPositions.Count == 0)
				{
					ClearFlattenCloseTracking();
					System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					return;
				}

				List<Order> closeOrders = new List<Order>();
				foreach (Position pos in openPositions)
				{
					OrderAction action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
					Order closeOrder = account.CreateOrder(pos.Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, 0, "", CloseOrderName, NinjaTrader.Core.Globals.MaxDate, null);
					if (closeOrder != null)
						closeOrders.Add(closeOrder);
					else
						Print(string.Format("[KatTradeManager] Flatten: close order creation returned null for {0}",
							pos.Instrument != null ? pos.Instrument.FullName : "(null)"));
				}

				if (closeOrders.Count == 0)
				{
					ClearFlattenCloseTracking();
					System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					return;
				}
				lock (flattenCloseLock)
				{
					flattenCloseOrders.Clear();
					flattenCloseOrders.AddRange(closeOrders);
				}

				QueueAccountOperation(
					AccountOperationType.Submit,
					closeOrders,
					"flatten-all close submit",
					completion: () =>
					{
						if (closeOrders.All(o => IsTerminalOrderState(o.OrderState)))
							System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					});
				Print(string.Format("[KatTradeManager] Flatten-all close submit queued: positions={0}", closeOrders.Count));
			}
			catch (Exception ex)
			{
				ClearFlattenCloseTracking();
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				Print(string.Format("[KatTradeManager] Error queuing flatten-all: {0}", ex));
			}
		}

		private bool PlaceMarketOrder(OrderAction action)
		{
			return PlaceMarketOrder(action, 0);
		}

		private bool PlaceMarketOrder(OrderAction action, int quantityOverride)
		{
			Print(string.Format("[KatTradeManager] PlaceMarketOrder click: {0}", action));
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return false;
			}

			if (IsDailyRiskBreached(out string breachReason))
			{
				Print(string.Format("[KatTradeManager] Market Order REJECTED by Daily Risk Protection: {0}", breachReason));
				ShowHudStatus(string.Format("Daily Risk blocked: {0}", breachReason), System.Windows.Media.Brushes.OrangeRed);
				return false;
			}

			if (IsEntryDebounced())
			{
				Print("[KatTradeManager] Duplicate market order ignored (anti-spam debounce).");
				return false;
			}

			double mktCheckPrice = cachedCurrentPrice > 0 ? cachedCurrentPrice : 0;
			if (mktCheckPrice <= 0 && Instrument.MarketData != null && Instrument.MarketData.Last != null)
				mktCheckPrice = Instrument.MarketData.Last.Price;

			Position openPosition = GetInstrumentPosition();
			bool hasFilledPosition = openPosition != null && openPosition.MarketPosition != MarketPosition.Flat;
			if (!hasFilledPosition && TryRejectEmaProtect(action, mktCheckPrice))
				return false;

			int mktQty = quantityOverride > 0 ? quantityOverride : (atmQuantity > 0 ? atmQuantity : DefaultQuantity);
			if (TryRejectDisciplineForEntry(action, mktQty, mktCheckPrice, out string discMktReason))
			{
				Print(string.Format("[KatTradeManager] Market Order REJECTED by Discipline: {0}", discMktReason));
				ShowHudStatus(discMktReason, System.Windows.Media.Brushes.OrangeRed);
				return false;
			}

			try

			{
				int qty = mktQty;
				// NinjaTrader ATM contract: CreateOrder name MUST be "Entry".
				// A custom name leaves StartAtmStrategy stuck at Initialized.
				string entryName = HasAtmTemplate(cachedAtmTemplate) ? "Entry" : (action == OrderAction.Buy ? "MarketBuy" : "MarketSell");

				entryOrder = account.CreateOrder(Instrument, action, OrderType.Market, OrderEntry.Manual, TimeInForce.Gtc, qty, 0, 0, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
				if (entryOrder != null)
				{
					if (SubmitOrder(entryOrder))
					{
						Print(string.Format("[KatTradeManager] Market order submitted: {0} qty={1} atm={2}", action, qty, cachedAtmTemplate ?? "(none)"));
						ShowHudStatus(string.Format("{0} market order submitted", action), System.Windows.Media.Brushes.LightGreen);
						return true;
					}
					else
					{
						entryOrder = null;
					}
				}
				else
					Print(string.Format("[KatTradeManager] Market order creation returned null: action={0} qty={1} instrument={2}",
						action, qty, Instrument.FullName));
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing market order: {0}", ex.ToString()));
			}
			return false;
		}

		private void SetBreakeven()
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
					Print("[KatTradeManager] BE: No active position to set Breakeven.");
					ShowHudStatus("BE: no active position", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				double tickSize = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
				int bufferTicks = cachedBufferTicks >= 0 ? cachedBufferTicks : DefaultBufferTicks;
				KatOrderAction katAction = pos.MarketPosition == MarketPosition.Long ? KatOrderAction.Buy : KatOrderAction.Sell;

				double bePrice = KatTradeCalculator.CalculateBreakevenPrice(katAction, pos.AveragePrice, bufferTicks, tickSize);
				double livePrice;
				lock (priceLock)
					livePrice = cachedCurrentPrice;

				// Underwater position: BE stop would sit on the wrong side of market -> broker rejection
				if (livePrice > 0 && !KatTradeCalculator.IsStopOnValidSide(pos.MarketPosition == MarketPosition.Long, bePrice, livePrice))
				{
					Print(string.Format("[KatTradeManager] BE skipped: stop {0} invalid vs current market {1}.", bePrice, livePrice));
					ShowHudStatus(string.Format("BE skipped: stop {0} invalid", bePrice), System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				if (TryRejectDisciplineForSlMove(bePrice, out string beDiscReason))
				{
					Print(string.Format("[KatTradeManager] BE REJECTED by Discipline: {0}", beDiscReason));
					ShowHudStatus(beDiscReason, System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				var workingStops = GetAccountOrdersSnapshot().Where(o => o.Instrument == Instrument &&
					IsActiveOrderState(o.OrderState) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();

				if (workingStops.Count > 0)
				{
					foreach (Order stopOrder in workingStops)
						stopOrder.StopPriceChanged = bePrice;
					QueueAccountOperation(AccountOperationType.Change, workingStops, "breakeven stop change");
					Print(string.Format("[KatTradeManager] Moved {0} Stop Loss order(s) to Breakeven @ {1} (Buffer: {2} ticks)", workingStops.Count, bePrice, bufferTicks));
					ShowHudStatus(string.Format("BE stop moved @ {0}", bePrice), System.Windows.Media.Brushes.LightGreen);
				}
				else
				{
					OrderAction slAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
					Order slOrder = account.CreateOrder(Instrument, slAction, OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc, pos.Quantity, 0, bePrice, "", "KAT_SL_BE", NinjaTrader.Core.Globals.MaxDate, null);
					if (slOrder != null)
					{
						QueueAccountOperation(AccountOperationType.Submit, new[] { slOrder }, "breakeven stop submit");
						Print(string.Format("[KatTradeManager] Submitted Breakeven Stop Loss @ {0} (Buffer: {1} ticks)", bePrice, bufferTicks));
						ShowHudStatus(string.Format("BE stop submitted @ {0}", bePrice), System.Windows.Media.Brushes.LightGreen);
					}
					else
						Print(string.Format("[KatTradeManager] BE order creation returned null: action={0} qty={1} stop={2}", slAction, pos.Quantity, bePrice));
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error setting Breakeven: {0}", ex.ToString()));
			}
		}

		private int pendingRevertAction; // 0 = none, 1 = Buy, 2 = Sell
		private int pendingRevertQuantity;
		private int pendingRevertSubmitInFlight;

		private void RevertPosition()
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
			try
			{
				if (TryRejectDisciplineForClose(out string discRevertReason))
				{
					Print(string.Format("[KatTradeManager] Revert REJECTED by Discipline: {0}", discRevertReason));
					ShowHudStatus(discRevertReason, System.Windows.Media.Brushes.OrangeRed);
					System.Threading.Interlocked.Exchange(ref pendingRevertAction, 0);
					System.Threading.Interlocked.Exchange(ref pendingRevertQuantity, 0);
					return;
				}
				if (IsCloseInFlight())
				{
					Print("[KatTradeManager] Revert: close already in flight — wait for fill before reverting again.");
					return;
				}

				Position pos = GetInstrumentPosition();
				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					Print("[KatTradeManager] Revert: No active position to revert.");
					return;
				}

				OrderAction oppositeAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
				int revertQuantity = pos.Quantity;
				System.Threading.Interlocked.Exchange(ref pendingRevertAction, oppositeAction == OrderAction.BuyToCover ? 1 : 2);
				System.Threading.Interlocked.Exchange(ref pendingRevertQuantity, revertQuantity);
				ClosePosition();
				Print(string.Format("[KatTradeManager] Revert queued: close qty={0}, then enter {1} qty={0} after close fill.",
					revertQuantity, oppositeAction));
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error reverting position: {0}", ex.ToString()));
			}
		}

		private void TrySubmitPendingRevert()
		{
			int requestedAction = System.Threading.Volatile.Read(ref pendingRevertAction);
			int requestedQuantity = System.Threading.Volatile.Read(ref pendingRevertQuantity);
			if (requestedAction == 0 || account == null || Instrument == null || IsCloseInFlight() || IsAccountCloseInFlight()) return;
			if (requestedQuantity <= 0)
			{
				System.Threading.Interlocked.Exchange(ref pendingRevertAction, 0);
				System.Threading.Interlocked.Exchange(ref pendingRevertQuantity, 0);
				return;
			}

			Position pos = GetInstrumentPosition();
			if (pos != null && pos.MarketPosition != MarketPosition.Flat) return;
			if (System.Threading.Interlocked.CompareExchange(ref pendingRevertSubmitInFlight, 1, 0) != 0)
				return;

			OrderAction action = requestedAction == 1 ? OrderAction.BuyToCover : OrderAction.Sell;
			Action submit = () =>
			{
				try
				{
					if (System.Threading.Volatile.Read(ref pendingRevertAction) != requestedAction
						|| System.Threading.Volatile.Read(ref pendingRevertQuantity) != requestedQuantity
						|| account == null || Instrument == null || IsCloseInFlight() || IsAccountCloseInFlight())
						return;

					Position current = GetInstrumentPosition();
					if (current != null && current.MarketPosition != MarketPosition.Flat)
						return;

					if (PlaceMarketOrder(action, requestedQuantity))
					{
						System.Threading.Interlocked.CompareExchange(ref pendingRevertAction, 0, requestedAction);
						System.Threading.Interlocked.CompareExchange(ref pendingRevertQuantity, 0, requestedQuantity);
					}
				}
				finally
				{
					System.Threading.Interlocked.Exchange(ref pendingRevertSubmitInFlight, 0);
				}
			};

			try
			{
				if (ChartControl != null && ChartControl.Dispatcher != null)
					ChartControl.Dispatcher.BeginInvoke(submit);
				else
					submit();
			}
			catch
			{
				System.Threading.Interlocked.Exchange(ref pendingRevertSubmitInFlight, 0);
			}
		}

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

			double tickSize = cachedTickSize > 0 ? cachedTickSize : (Instrument != null ? Instrument.MasterInstrument.TickSize : 0.25);
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
						double tickSize = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
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
								limitOffset = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
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
		#endregion

		// ponytail: Daily risk protection extracted to src/KatTradeManager.DailyRisk.cs (partial class)
	}
}
