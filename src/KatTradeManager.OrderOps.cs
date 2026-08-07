/* KatTradeManager.OrderOps.cs - Order execution & position management (partial class) v0.93 (2026-07-31) */

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

		private static bool IsAccountOperationPending(OrderState state)
		{
			return state == OrderState.Submitted
				|| state == OrderState.ChangePending
				|| state == OrderState.ChangeSubmitted
				|| state == OrderState.CancelPending
				|| state == OrderState.CancelSubmitted;
		}

		private static bool IsAccountOperationEligible(AccountOperationType type, Order order)
		{
			if (order == null || IsTerminalOrderState(order.OrderState))
				return false;

			switch (type)
			{
				case AccountOperationType.Submit:
					return order.OrderState == OrderState.Initialized
						|| order.OrderState == OrderState.Submitted
						|| order.OrderState == OrderState.Accepted
						|| order.OrderState == OrderState.AcceptedByRisk
						|| order.OrderState == OrderState.Working
						|| order.OrderState == OrderState.TriggerPending;
				case AccountOperationType.Change:
					return order.OrderState == OrderState.Accepted
						|| order.OrderState == OrderState.AcceptedByRisk
						|| order.OrderState == OrderState.Working
						|| order.OrderState == OrderState.TriggerPending
						|| order.OrderState == OrderState.PartFilled
						|| order.OrderState == OrderState.Suspended;
				case AccountOperationType.Cancel:
					return order.OrderState == OrderState.Initialized
						|| order.OrderState == OrderState.Submitted
						|| order.OrderState == OrderState.Accepted
						|| order.OrderState == OrderState.AcceptedByRisk
						|| order.OrderState == OrderState.Working
						|| order.OrderState == OrderState.TriggerPending
						|| order.OrderState == OrderState.PartFilled
						|| order.OrderState == OrderState.Suspended;
				default:
					return false;
			}
		}

		private static bool SameOrder(Order left, Order right)
		{
			if (ReferenceEquals(left, right)) return true;
			if (left == null || right == null) return false;
			return !string.IsNullOrEmpty(left.OrderId)
				&& string.Equals(left.OrderId, right.OrderId, StringComparison.Ordinal);
		}

		private static bool OperationsOverlap(AccountOperationRequest request, Order[] orders)
		{
			return request != null
				&& request.Orders != null
				&& orders != null
				&& request.Orders.Any(existing => orders.Any(order => SameOrder(existing, order)));
		}

		private AccountOperationRequest FindOverlappingOperationLocked(Order[] orders)
		{
			if (OperationsOverlap(activeAccountOperation, orders))
				return activeAccountOperation;
			return accountOperationQueue.FirstOrDefault(request => OperationsOverlap(request, orders));
		}

		private void LogAccountOperation(string eventName, AccountOperationRequest request, Order[] orders = null)
		{
			Order[] observed = orders ?? (request != null ? request.Orders : null);
			string ids = observed == null
				? string.Empty
				: string.Join(",", observed.Where(order => order != null).Select(order => string.Format(
					"{0}/{1}/{2}",
					order.OrderId ?? string.Empty,
					order.Oco ?? string.Empty,
					order.Quantity)));
			Print(string.Format(
				"[KatTradeManager] Account operation {0}: type={1} reason={2} orders={3}",
				eventName,
				request != null ? request.Type.ToString() : string.Empty,
				request != null ? request.Reason : string.Empty,
				ids));
		}

		private void ScheduleAccountOperationPump()
		{
			if (account == null) return;
			if (System.Threading.Interlocked.CompareExchange(ref accountOperationPumpScheduled, 1, 0) != 0)
				return;

			Action pump = () =>
			{
				try
				{
					PumpAccountOperationQueue();
				}
				finally
				{
					System.Threading.Interlocked.Exchange(ref accountOperationPumpScheduled, 0);
				}
			};

			try
			{
				if (ChartControl != null && ChartControl.Dispatcher != null)
					ChartControl.Dispatcher.BeginInvoke(pump);
				else
					pump();
			}
			catch
			{
				System.Threading.Interlocked.Exchange(ref accountOperationPumpScheduled, 0);
			}
		}

		private void CompleteAccountOperation(AccountOperationRequest request)
		{
			List<Action> completions = null;
			lock (accountOperationLock)
			{
				if (!ReferenceEquals(activeAccountOperation, request))
					return;
				activeAccountOperation = null;
				activeAccountOperationSinceUtc = DateTime.MinValue;
				completions = request.Completions.ToList();
			}

			LogAccountOperation("released", request);
			foreach (Action completion in completions)
			{
				try { completion?.Invoke(); }
				catch (Exception ex)
				{
					Print(string.Format("[KatTradeManager] Account operation continuation failed: {0}", ex.Message));
				}
			}
			ScheduleAccountOperationPump();
		}

		private bool IsAccountOperationSettled(AccountOperationRequest request)
		{
			if (request == null || !request.CallReturned || request.Orders == null || request.Orders.Length == 0)
				return false;
			// ponytail: StartAtmStrategy owns subsequent order-state transitions; queue ceiling = serialize
			// the API call, not wait for ATM-managed entry lifecycle events that may never target this order.
			if (request.ExecuteOverride != null)
				return true;

			foreach (Order order in request.Orders)
			{
				if (order == null) continue;
				if (request.Type == AccountOperationType.Cancel && !IsTerminalOrderState(order.OrderState))
					return false;
				if (request.Type == AccountOperationType.Submit
					&& (order.OrderState == OrderState.Initialized
						|| order.OrderState == OrderState.Submitted))
					return false;
				if ((request.Type == AccountOperationType.Change || request.Type == AccountOperationType.Cancel)
					&& IsAccountOperationPending(order.OrderState))
					return false;
			}
			return true;
		}

		private void TryCompleteActiveAccountOperation()
		{
			AccountOperationRequest request;
			DateTime activeSince;
			lock (accountOperationLock)
			{
				request = activeAccountOperation;
				activeSince = activeAccountOperationSinceUtc;
			}
			if (request == null) return;
			if (IsAccountOperationSettled(request))
			{
				CompleteAccountOperation(request);
				return;
			}
			// Broker never settled the call (pending state hang) — release the FIFO so later
			// operations, including Close/flatten, are not starved behind the stuck request.
			if (activeSince != DateTime.MinValue
				&& (DateTime.UtcNow - activeSince).TotalMilliseconds > AccountOperationSettleTimeoutMs)
			{
				LogAccountOperation("timeout-release", request);
				CompleteAccountOperation(request);
			}
		}

		private void QueueAccountOperation(
			AccountOperationType type,
			IEnumerable<Order> orders,
			string reason,
			Action completion = null,
			Action executeOverride = null)
		{
			Order[] requested = (orders ?? Enumerable.Empty<Order>())
				.Where(order => order != null)
				.Distinct()
				.ToArray();
			if (requested.Length == 0)
			{
				completion?.Invoke();
				return;
			}

			AccountOperationRequest request = new AccountOperationRequest
			{
				Type = type,
				Orders = requested,
				Reason = reason
			};
			if (completion != null)
				request.Completions.Add(completion);
			request.ExecuteOverride = executeOverride;

			lock (accountOperationLock)
			{
				AccountOperationRequest overlap = FindOverlappingOperationLocked(requested);
				if (overlap != null)
				{
					if (overlap.Type == type)
					{
						Order[] remaining = requested
							.Where(order => !overlap.Orders.Any(existing => SameOrder(existing, order)))
							.ToArray();
						if (remaining.Length == 0)
						{
							if (completion != null)
								overlap.Completions.Add(completion);
						}
						else
							overlap.Completions.Add(() => QueueAccountOperation(type, remaining, reason, completion, executeOverride));
					}
					else
						overlap.Completions.Add(() => QueueAccountOperation(type, requested, reason, completion, executeOverride));
					LogAccountOperation("coalesced", request, requested);
					return;
				}
				accountOperationQueue.Enqueue(request);
			}

			LogAccountOperation("queued", request);
			ScheduleAccountOperationPump();
		}

		private void PumpAccountOperationQueue()
		{
			TryCompleteActiveAccountOperation();

			AccountOperationRequest request;
			Order[] dispatchOrders;
			bool stalledHead = false;
			lock (accountOperationLock)
			{
				if (activeAccountOperation != null || accountOperationQueue.Count == 0)
					return;

				request = accountOperationQueue.Peek();
				dispatchOrders = request.Orders
					.Where(order => IsAccountOperationEligible(request.Type, order))
					.ToArray();

				if (dispatchOrders.Length == 0)
				{
					bool waitingForPlatform = request.Orders.Any(order => order != null && IsAccountOperationPending(order.OrderState));
					if (waitingForPlatform)
					{
						// Head orders stuck pending: wait, but not forever — a hung broker state must not
						// starve every operation queued behind it.
						if (queueHeadStallSinceUtc == DateTime.MinValue)
						{
							queueHeadStallSinceUtc = DateTime.UtcNow;
							return;
						}
						if ((DateTime.UtcNow - queueHeadStallSinceUtc).TotalMilliseconds <= AccountOperationSettleTimeoutMs)
							return;
						stalledHead = true;
					}
					accountOperationQueue.Dequeue();
					queueHeadStallSinceUtc = DateTime.MinValue;
				}
				else
				{
					accountOperationQueue.Dequeue();
					queueHeadStallSinceUtc = DateTime.MinValue;
					request.Orders = dispatchOrders;
					request.CallReturned = false;
					activeAccountOperation = request;
					activeAccountOperationSinceUtc = DateTime.UtcNow;
				}
			}

			if (dispatchOrders.Length == 0)
			{
				LogAccountOperation(stalledHead ? "timeout-skip" : "skipped", request);
				foreach (Action completion in request.Completions)
					completion?.Invoke();
				ScheduleAccountOperationPump();
				return;
			}

			LogAccountOperation("dispatch", request);
			try
			{
				if (request.ExecuteOverride != null)
					request.ExecuteOverride();
				else if (request.Type == AccountOperationType.Submit)
					account.Submit(dispatchOrders);
				else if (request.Type == AccountOperationType.Change)
					account.Change(dispatchOrders);
				else
					account.Cancel(dispatchOrders);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Account operation failed: type={0} reason={1} error={2}",
					request.Type, request.Reason, ex));
				CompleteAccountOperation(request);
				return;
			}

			request.CallReturned = true;
			TryCompleteActiveAccountOperation();
		}

		private void ResetAccountOperationQueue()
		{
			lock (accountOperationLock)
			{
				accountOperationQueue.Clear();
				activeAccountOperation = null;
				activeAccountOperationSinceUtc = DateTime.MinValue;
				queueHeadStallSinceUtc = DateTime.MinValue;
			}
			System.Threading.Interlocked.Exchange(ref accountOperationPumpScheduled, 0);
			System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
			ClearFlattenCloseTracking();
		}

		private void ClearFlattenCloseTracking()
		{
			lock (flattenCloseLock)
				flattenCloseOrders.Clear();
		}

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

		private void TrackAtmStartup(Order order)
		{
			if (order == null) return;
			lock (atmScaleInLock)
			{
				atmStartupOrder = order;
				atmLastLifecycleActivityUtc = DateTime.UtcNow;
				atmPositionWasConfirmedThisEpisode = false;
			}
		}

		private void ClearAtmStartup(Order expected = null)
		{
			lock (atmScaleInLock)
			{
				if (expected == null || SameOrder(atmStartupOrder, expected))
					atmStartupOrder = null;
				atmDeferLoggedStartup = null;
			}
		}

		private bool IsAtmStartupPending()
		{
			Order startup;
			DateTime lastActivity;
			lock (atmScaleInLock)
			{
				startup = atmStartupOrder;
				lastActivity = atmLastLifecycleActivityUtc;
			}
			if (startup == null) return false;
			if (!IsTerminalOrderState(startup.OrderState))
			{
				// Ceiling: an entry stuck non-terminal (silent ATM start failure) must not defer
				// flat cleanup forever — leftover SL/TP would survive Close/flatten otherwise.
				if (lastActivity == DateTime.MinValue) return true;
				return (DateTime.UtcNow - lastActivity).TotalMilliseconds < AccountOperationSettleTimeoutMs;
			}

			if (lastActivity == DateTime.MinValue) return true;
			return (DateTime.UtcNow - lastActivity).TotalMilliseconds < AtmLifecycleGraceMilliseconds;
		}

		private void ProcessAtmStartupUpdate(Order observed)
		{
			if (observed == null) return;
			lock (atmScaleInLock)
			{
				if (SameOrder(atmStartupOrder, observed))
					atmLastLifecycleActivityUtc = DateTime.UtcNow;
			}
		}
		private static KatOrderAction ToKatAction(OrderAction action) => action == OrderAction.Buy ? KatOrderAction.Buy : KatOrderAction.Sell;
		private static OrderType ToNtOrderType(KatOrderType type) => type == KatOrderType.StopMarket ? OrderType.StopMarket : OrderType.Limit;
		private bool HasAtmTemplate(string templateName)
		{
			if (string.IsNullOrEmpty(templateName)) return false;
			string path = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy", templateName + ".xml");
			return System.IO.File.Exists(path);
		}

		// HUD ATM selector set to "None" clears the template, which means the HUD owns no ATM brackets and
		// must not merge, resize or cancel Chart Trader orders. Deliberately no disk check: this runs on
		// every watchdog tick and order update.
		private bool IsHudAtmActive()
		{
			return !string.IsNullOrEmpty(cachedAtmTemplate);
		}
		private static bool IsManualExitOrder(Order order)
		{
			return order != null
				&& !string.IsNullOrEmpty(order.Name)
				&& order.Name.StartsWith("KAT_", StringComparison.OrdinalIgnoreCase);
		}

		// Submits via ATM template when it exists on disk; falls back to plain submit otherwise.
		// StartAtmStrategy requires the entry order name "Entry"; callers must preserve that contract.
		private bool SubmitOrder(Order order)
		{
			if (account == null || order == null) return false;
			string tpl = cachedAtmTemplate;
			try
			{
				if (HasAtmTemplate(tpl))
				{
					if (TryPrepareAtmScaleIn(order))
					{
						TrackAtmScaleIn(order);
						QueueAccountOperation(AccountOperationType.Submit, new[] { order }, "ATM MERGE scale-in submit");
						Print(string.Format("[KatTradeManager] ATM MERGE scale-in submitted: name={0} type={1} state={2}",
							order.Name, order.OrderType, order.OrderState));
						return true;
					}
					TrackAtmStartup(order);
					QueueAccountOperation(
						AccountOperationType.Submit,
						new[] { order },
						"ATM start",
						executeOverride: () => NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(tpl, order));
					Print(string.Format("[KatTradeManager] ATM start requested: template={0} name={1} state={2}",
						tpl, order.Name, order.OrderState));
					return true;
				}
				if (!string.IsNullOrEmpty(tpl))
					Print(string.Format("[KatTradeManager] ATM template '{0}' not found — submitting order WITHOUT ATM strategy", tpl));

				QueueAccountOperation(AccountOperationType.Submit, new[] { order }, "native submit");
				Print(string.Format("[KatTradeManager] Native submit requested: name={0} type={1} state={2}",
					order.Name, order.OrderType, order.OrderState));
				return true;
			}
			catch (Exception ex)
			{
				UntrackAtmScaleIn(order);
				ClearAtmStartup(order);
				Print(string.Format("[KatTradeManager] Order submit failed: {0}", ex.ToString()));
				return false;
			}
		}

		private void UntrackAtmScaleIn(Order order)
		{
			if (order == null) return;
			lock (atmScaleInLock)
			{
				AtmScaleInState state = atmScaleInStates.FirstOrDefault(s => s.Order == order);
				if (state != null)
					atmScaleInStates.Remove(state);
			}
		}

		private static bool IsMergeCandidateState(OrderState state)
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
				|| state == OrderState.Suspended;
		}

		private static bool HasAtmBracketName(Order order)
		{
			if (order == null || string.IsNullOrEmpty(order.Name)) return false;
			string name = order.Name;
			return name.IndexOf("stop", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0
				|| name.IndexOf("profit", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool HasAtmEntrySignal(Order order)
		{
			return order != null
				&& !string.IsNullOrEmpty(order.FromEntrySignal)
				&& order.FromEntrySignal.IndexOf("entry", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private bool IsAtmLifecycleOrder(Order order)
		{
			return order != null
				&& (order.Name == "Entry"
					|| HasAtmBracketName(order)
					|| HasAtmEntrySignal(order)
					|| IsKnownAtmBracket(order));
		}

		private void MarkAtmLifecycleActivity()
		{
			lock (atmScaleInLock)
				atmLastLifecycleActivityUtc = DateTime.UtcNow;
		}

		private bool IsKnownAtmBracket(Order order)
		{
			if (order == null) return false;
			lock (atmScaleInLock)
			{
				if (ReferenceEquals(order, atmMergeStopAnchor) || ReferenceEquals(order, atmMergeTargetAnchor))
					return true;
				if (atmMergeStopAnchor != null && !string.IsNullOrEmpty(atmMergeStopAnchor.Oco)
					&& string.Equals(atmMergeStopAnchor.Oco, order.Oco, StringComparison.Ordinal))
					return true;
				if (atmMergeTargetAnchor != null && !string.IsNullOrEmpty(atmMergeTargetAnchor.Oco)
					&& string.Equals(atmMergeTargetAnchor.Oco, order.Oco, StringComparison.Ordinal))
					return true;
				return false;
			}
		}

		private static bool IsAtmExitAction(OrderAction action, MarketPosition position)
		{
			return position == MarketPosition.Long
				? action == OrderAction.Sell || action == OrderAction.SellShort
				: action == OrderAction.Buy || action == OrderAction.BuyToCover;
		}


		private bool IsAtmBracketCandidate(Order order)
		{
			if (order == null || Instrument == null || order.Instrument != Instrument) return false;
			if (IsManualExitOrder(order) || !IsMergeCandidateState(order.OrderState)) return false;
			if (order.OrderType != OrderType.StopMarket
				&& order.OrderType != OrderType.StopLimit
				&& order.OrderType != OrderType.Limit)
				return false;
			return HasAtmBracketName(order) || HasAtmEntrySignal(order) || IsKnownAtmBracket(order);
		}

		private bool IsAtmMergeOrder(Order order, MarketPosition position)
		{
			return IsAtmBracketCandidate(order) && IsAtmExitAction(order.OrderAction, position);
		}

		private void ScheduleAtmBracketMerge()
		{
			if (!IsHudAtmActive() || account == null || Instrument == null) return;
			if (System.Threading.Interlocked.CompareExchange(ref atmMergeScheduled, 1, 0) != 0) return;

			Action merge = () =>
			{
				try
				{
					MergeAtmBrackets();
				}
				catch (Exception ex)
				{
					Print(string.Format("[KatTradeManager] ATM MERGE dispatcher callback failed: {0}", ex.Message));
				}
				finally
				{
					System.Threading.Interlocked.Exchange(ref atmMergeScheduled, 0);
				}
			};

			try
			{
				if (ChartControl != null && ChartControl.Dispatcher != null)
					ChartControl.Dispatcher.BeginInvoke(merge);
				else
					merge();
			}
			catch
			{
				System.Threading.Interlocked.Exchange(ref atmMergeScheduled, 0);
			}
		}

		private void MergeAtmBrackets()
		{
			if (!IsHudAtmActive() || account == null || Instrument == null) return;

			try
			{
				Position position;
				var positions = account.Positions;
				lock (positions)
					position = positions.FirstOrDefault(p => p.Instrument == Instrument);

				List<Order> candidates;
				var orders = account.Orders;
				lock (orders)
					candidates = orders.Where(IsAtmBracketCandidate).ToList();
				bool positionConfirmed = position != null && position.MarketPosition != MarketPosition.Flat;
				if (positionConfirmed)
				{
					lock (atmScaleInLock)
					{
						if (!atmPositionWasConfirmedThisEpisode)
							atmLastLifecycleActivityUtc = DateTime.UtcNow;
						atmPositionWasConfirmedThisEpisode = true;
					}
					ClearAtmStartup();
				}

				if (!positionConfirmed)
				{
					bool startupPending = IsAtmStartupPending();
					bool wasPositionConfirmed;
					DateTime lastActivity;
					lock (atmScaleInLock)
					{
						wasPositionConfirmed = atmPositionWasConfirmedThisEpisode;
						lastActivity = atmLastLifecycleActivityUtc;
					}

					double activityAge = lastActivity == DateTime.MinValue
						? -1
						: (DateTime.UtcNow - lastActivity).TotalMilliseconds;
					// No ATM episode ever happened (no startup, no position, no activity): nothing to
					// defer — skipping avoids an endless "deferred" log spam on every account event.
					if (lastActivity != DateTime.MinValue
						&& KatTradeCalculator.ShouldDeferAtmFlatCleanup(
						startupPending,
						false,
						wasPositionConfirmed,
						activityAge,
						AtmLifecycleGraceMilliseconds))
					{
						// Deferring while our entry works is normal — log once per episode, not per account event.
						bool logNow;
						lock (atmScaleInLock)
						{
							logNow = !ReferenceEquals(atmDeferLoggedStartup, atmStartupOrder);
							if (logNow) atmDeferLoggedStartup = atmStartupOrder;
						}
						if (logNow)
							Print(string.Format(
								"[KatTradeManager] ATM MERGE flat cleanup deferred: startupPending={0} wasPositionConfirmed={1} activityAgeMs={2:F0}.",
								startupPending,
								wasPositionConfirmed,
								activityAge));
						return;
					}
					if (candidates.Count > 0)
					{
						QueueAccountOperation(AccountOperationType.Cancel, candidates, "ATM MERGE flat cleanup");
						Print(string.Format("[KatTradeManager] ATM MERGE flat cleanup: removed={0}", candidates.Count));
					}
					ResetAtmScaleInTracking();
					return;
				}

				List<Order> brackets = candidates
					.Where(o => IsAtmExitAction(o.OrderAction, position.MarketPosition))
					.ToList();
				List<Order> staleOppositeBrackets = candidates
					.Where(o => !IsAtmExitAction(o.OrderAction, position.MarketPosition))
					.ToList();
				if (staleOppositeBrackets.Count > 0)
					QueueAccountOperation(AccountOperationType.Cancel, staleOppositeBrackets, "ATM MERGE stale opposite cleanup");
				List<Order> stops = brackets
					.Where(o => o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit)
					.ToList();
				List<Order> targets = brackets
					.Where(o => o.OrderType == OrderType.Limit)
					.ToList();

				// Only merge within SAME OCO pair to avoid broker-side cascade cancellations.
				List<Order> bracketOrders = stops.Concat(targets).ToList();
				if (bracketOrders.Any(o => IsAccountOperationPending(o.OrderState)))
					return; // anti-churn: never stack new mutations on in-flight broker operations
				List<KatTradeCalculator.KatAtmBracketOrder> plannerOrders = new List<KatTradeCalculator.KatAtmBracketOrder>(bracketOrders.Count);
				foreach (Order o in bracketOrders)
				{
					plannerOrders.Add(new KatTradeCalculator.KatAtmBracketOrder
					{
						Oco = o.Oco ?? string.Empty,
						IsStop = o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit,
						Quantity = o.Quantity,
						Price = o.OrderType == OrderType.Limit ? o.LimitPrice : o.StopPrice,
					});
				}
				var mergePlan = KatTradeCalculator.PlanAtmBracketMerge(plannerOrders, position.Quantity);

				Order stopAnchor = mergePlan.KeepStopIndex >= 0 ? bracketOrders[mergePlan.KeepStopIndex] : null;
				Order targetAnchor = mergePlan.KeepTargetIndex >= 0 ? bracketOrders[mergePlan.KeepTargetIndex] : null;
				lock (atmScaleInLock)
				{
					atmMergePosition = position.MarketPosition;
					atmMergeStopAnchor = stopAnchor;
					atmMergeTargetAnchor = targetAnchor;
					atmMergeStopQuantity = mergePlan.DesiredStopQuantity;
					atmMergeTargetQuantity = mergePlan.DesiredTargetQuantity;
				}

				List<Order> changes = new List<Order>();
				foreach (int idx in mergePlan.ChangeIndices)
				{
					Order changeOrder = bracketOrders[idx];
					changeOrder.QuantityChanged = plannerOrders[idx].IsStop
						? mergePlan.DesiredStopQuantity
						: mergePlan.DesiredTargetQuantity;
					changes.Add(changeOrder);
				}
				if (changes.Count > 0)
					QueueAccountOperation(AccountOperationType.Change, changes, "ATM MERGE canonical quantity");

				Order[] duplicates = mergePlan.CancelIndices
					.Select(i => bracketOrders[i])
					.ToArray();
				if (duplicates.Length > 0)
					QueueAccountOperation(AccountOperationType.Cancel, duplicates, "ATM MERGE duplicate cleanup");
				int removedCount = duplicates.Length + staleOppositeBrackets.Count;
				if (changes.Count > 0 || removedCount > 0)
				{
					Print(string.Format(
						"[KatTradeManager] ATM MERGE reconciled: positionQty={0} stop={1} target={2} changed={3} removed={4} staleOpposite={5}",
						position.Quantity,
						stopAnchor != null ? stopAnchor.OrderType.ToString() : "none",
						targetAnchor != null ? targetAnchor.OrderType.ToString() : "none",
						changes.Count,
						removedCount,
						staleOppositeBrackets.Count));
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] ATM MERGE reconciliation failed: {0}", ex.Message));
			}
		}

		private bool TryPrepareAtmScaleIn(Order entry)
		{
			if (entry == null || account == null || Instrument == null) return false;

			Position position = GetInstrumentPosition();
			if (position == null || position.MarketPosition == MarketPosition.Flat) return false;

			bool sameDirection = (position.MarketPosition == MarketPosition.Long && entry.OrderAction == OrderAction.Buy)
				|| (position.MarketPosition == MarketPosition.Short && entry.OrderAction == OrderAction.Sell);
			if (!sameDirection) return false;

			List<Order> brackets = GetAccountOrdersSnapshot()
				.Where(o => IsAtmMergeOrder(o, position.MarketPosition))
				.ToList();
			if (brackets.Count == 0) return false;

			Order stop = brackets.FirstOrDefault(o => o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit);
			Order target = brackets.FirstOrDefault(o => o.OrderType == OrderType.Limit);
			if (stop == null && target == null) return false;

			lock (atmScaleInLock)
			{
				if (!atmPositionWasConfirmedThisEpisode)
					atmLastLifecycleActivityUtc = DateTime.UtcNow;
				atmPositionWasConfirmedThisEpisode = true;
				if (atmMergePosition != position.MarketPosition)
				{
					atmScaleInStates.Clear();
					atmMergePosition = position.MarketPosition;
				}
				if (atmMergeStopAnchor != stop)
					atmMergeStopQuantity = stop != null ? stop.Quantity : 0;
				else if (stop != null)
					atmMergeStopQuantity = Math.Max(atmMergeStopQuantity, stop.Quantity);
				if (atmMergeTargetAnchor != target)
					atmMergeTargetQuantity = target != null ? target.Quantity : 0;
				else if (target != null)
					atmMergeTargetQuantity = Math.Max(atmMergeTargetQuantity, target.Quantity);
				atmMergeStopAnchor = stop;
				atmMergeTargetAnchor = target;
			}
			return true;
		}

		private void TrackAtmScaleIn(Order order)
		{
			if (order == null) return;
			lock (atmScaleInLock)
			{
				atmLastLifecycleActivityUtc = DateTime.UtcNow;
				atmScaleInStates.Add(new AtmScaleInState
				{
					Order = order,
					AppliedFilled = 0
				});
			}
		}

		private void ResetAtmScaleInTracking()
		{
			lock (atmScaleInLock)
			{
				atmScaleInStates.Clear();
				atmMergeStopAnchor = null;
				atmMergeTargetAnchor = null;
				atmMergeStopQuantity = 0;
				atmMergeTargetQuantity = 0;
				atmMergePosition = MarketPosition.Flat;
				atmStartupOrder = null;
				atmLastLifecycleActivityUtc = DateTime.MinValue;
				atmPositionWasConfirmedThisEpisode = false;
				atmDeferLoggedStartup = null;
			}
		}

		private AtmScaleInState FindAtmScaleInState(Order observed)
		{
			if (observed == null) return null;
			lock (atmScaleInLock)
			{
				return atmScaleInStates.FirstOrDefault(s => s.Order == observed
					|| (!string.IsNullOrEmpty(s.Order.OrderId)
						&& s.Order.OrderId == observed.OrderId));
			}
		}

		private void ResizeAtmBracketForFill(int fillDelta)
		{
			if (fillDelta <= 0 || account == null) return;

			List<Order> changes = new List<Order>();
			lock (atmScaleInLock)
			{
				if (atmMergeStopAnchor != null && IsActiveOrderState(atmMergeStopAnchor.OrderState))
				{
					atmMergeStopQuantity += fillDelta;
					atmMergeStopAnchor.QuantityChanged = atmMergeStopQuantity;
					changes.Add(atmMergeStopAnchor);
				}
				if (atmMergeTargetAnchor != null && IsActiveOrderState(atmMergeTargetAnchor.OrderState))
				{
					atmMergeTargetQuantity += fillDelta;
					atmMergeTargetAnchor.QuantityChanged = atmMergeTargetQuantity;
					changes.Add(atmMergeTargetAnchor);
				}
			}
			if (changes.Count == 0) return;

			QueueAccountOperation(AccountOperationType.Change, changes, "ATM MERGE scale-in resize");
			Print(string.Format("[KatTradeManager] ATM MERGE bracket resized: fillDelta={0} stop={1} target={2}",
				fillDelta,
				changes.Any(o => o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit),
				changes.Any(o => o.OrderType == OrderType.Limit)));
		}

		private void ProcessAtmScaleInUpdate(Order observed)
		{
			if (observed == null || observed.Name != "Entry") return;
			lock (atmScaleInLock)
				atmLastLifecycleActivityUtc = DateTime.UtcNow;

			AtmScaleInState state = FindAtmScaleInState(observed);
			if (state == null) return;

			int filled = Math.Max(0, observed.Filled);
			int delta;
			lock (atmScaleInLock)
			{
				delta = filled - state.AppliedFilled;
				if (delta > 0)
					state.AppliedFilled = filled;
			}
			if (delta > 0)
				ResizeAtmBracketForFill(delta);

			if (IsTerminalOrderState(observed.OrderState))
			{
				lock (atmScaleInLock)
					atmScaleInStates.Remove(state);
			}
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

				OrderAction oppositeAction = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.Buy;
				int revertQuantity = pos.Quantity;
				System.Threading.Interlocked.Exchange(ref pendingRevertAction, oppositeAction == OrderAction.Buy ? 1 : 2);
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

			OrderAction action = requestedAction == 1 ? OrderAction.Buy : OrderAction.Sell;
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
