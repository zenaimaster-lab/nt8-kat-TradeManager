/* KatTradeManager.OrderOps.cs - Order execution, position management & daily risk logic (partial class) v0.83 (2026-07-28) */

using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators
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
		private int closeOperationQueued;
		private Order atmStartupOrder;

		private static bool IsAccountOperationPending(OrderState state)
		{
			return state == OrderState.Submitted
				|| state == OrderState.ChangePending
				|| state == OrderState.ChangeSubmitted
				|| state == OrderState.CancelPending
				|| state == OrderState.CancelSubmitted;
		}

		private static bool IsAccountOperationTerminal(OrderState state)
		{
			return state == OrderState.Filled
				|| state == OrderState.Cancelled
				|| state == OrderState.Rejected;
		}

		private static bool IsAccountOperationEligible(AccountOperationType type, Order order)
		{
			if (order == null || IsAccountOperationTerminal(order.OrderState))
				return false;

			switch (type)
			{
				case AccountOperationType.Submit:
					return order.OrderState == OrderState.Initialized;
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
				if (request.Type == AccountOperationType.Submit
					&& (order.OrderState == OrderState.Initialized || order.OrderState == OrderState.Submitted))
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
			lock (accountOperationLock)
				request = activeAccountOperation;
			if (request != null && IsAccountOperationSettled(request))
				CompleteAccountOperation(request);
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
						if (completion != null)
							overlap.Completions.Add(completion);
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
						return;
					accountOperationQueue.Dequeue();
				}
				else
				{
					accountOperationQueue.Dequeue();
					request.Orders = dispatchOrders;
					request.CallReturned = false;
					activeAccountOperation = request;
				}
			}

			if (dispatchOrders.Length == 0)
			{
				LogAccountOperation("skipped", request);
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
			}
			System.Threading.Interlocked.Exchange(ref accountOperationPumpScheduled, 0);
			System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
		}

		private void TrackAtmStartup(Order order)
		{
			if (order == null) return;
			lock (atmScaleInLock)
				atmStartupOrder = order;
		}

		private void ClearAtmStartup(Order expected = null)
		{
			lock (atmScaleInLock)
			{
				if (expected == null || SameOrder(atmStartupOrder, expected))
					atmStartupOrder = null;
			}
		}

		private bool IsAtmStartupPending()
		{
			Order startup;
			lock (atmScaleInLock)
				startup = atmStartupOrder;
			if (startup == null) return false;
			if (IsTerminalOrderState(startup.OrderState))
			{
				ClearAtmStartup(startup);
				return false;
			}
			return true;
		}

		private void ProcessAtmStartupUpdate(Order observed)
		{
			if (observed == null || !IsTerminalOrderState(observed.OrderState)) return;
			ClearAtmStartup(observed);
		}
		private static KatOrderAction ToKatAction(OrderAction action) => action == OrderAction.Buy ? KatOrderAction.Buy : KatOrderAction.Sell;
		private static OrderType ToNtOrderType(KatOrderType type) => type == KatOrderType.StopMarket ? OrderType.StopMarket : OrderType.Limit;
		private bool HasAtmTemplate(string templateName)
		{
			if (string.IsNullOrEmpty(templateName)) return false;
			string path = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy", templateName + ".xml");
			return System.IO.File.Exists(path);
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
					if (cachedIsAtmMerge && TryPrepareAtmScaleIn(order))
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
			if (!cachedIsAtmMerge || account == null || Instrument == null) return;
			if (System.Threading.Interlocked.CompareExchange(ref atmMergeScheduled, 1, 0) != 0) return;

			Action merge = () =>
			{
				try
				{
					MergeAtmBrackets();
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
			if (!cachedIsAtmMerge || account == null || Instrument == null) return;

			try
			{
				Position position = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
				List<Order> candidates = account.Orders.Where(IsAtmBracketCandidate).ToList();
				bool positionConfirmed = position != null && position.MarketPosition != MarketPosition.Flat;
				if (positionConfirmed)
					ClearAtmStartup();

				if (!positionConfirmed)
				{
					if (KatTradeCalculator.ShouldDeferAtmFlatCleanup(IsAtmStartupPending(), false))
					{
						Print("[KatTradeManager] ATM MERGE flat cleanup deferred: first ATM entry startup pending.");
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
				// ponytail: keep existing anchor prices; ceiling = synthetic ATM recalculation if NT8 exposes ownership transfer.

				Order stopAnchor;
				Order targetAnchor;
				lock (atmScaleInLock)
				{
					stopAnchor = atmMergeStopAnchor != null && stops.Contains(atmMergeStopAnchor)
						? atmMergeStopAnchor
						: stops.FirstOrDefault();
					targetAnchor = atmMergeTargetAnchor != null && targets.Contains(atmMergeTargetAnchor)
						? atmMergeTargetAnchor
						: targets.FirstOrDefault();
					atmMergePosition = position.MarketPosition;
					atmMergeStopAnchor = stopAnchor;
					atmMergeTargetAnchor = targetAnchor;
					atmMergeStopQuantity = position.Quantity;
					atmMergeTargetQuantity = position.Quantity;
				}

				List<Order> changes = new List<Order>();
				if (stopAnchor != null && stopAnchor.Quantity != position.Quantity)
				{
					stopAnchor.QuantityChanged = position.Quantity;
					changes.Add(stopAnchor);
				}
				if (targetAnchor != null && targetAnchor.Quantity != position.Quantity)
				{
					targetAnchor.QuantityChanged = position.Quantity;
					changes.Add(targetAnchor);
				}
				if (changes.Count > 0)
					QueueAccountOperation(AccountOperationType.Change, changes, "ATM MERGE canonical quantity");

				Order[] duplicates = stops
					.Where(o => o != stopAnchor)
					.Concat(targets.Where(o => o != targetAnchor))
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

			Position position = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
			if (position == null || position.MarketPosition == MarketPosition.Flat) return false;

			bool sameDirection = (position.MarketPosition == MarketPosition.Long && entry.OrderAction == OrderAction.Buy)
				|| (position.MarketPosition == MarketPosition.Short && entry.OrderAction == OrderAction.Sell);
			if (!sameDirection) return false;

			List<Order> brackets = account.Orders
				.Where(o => IsAtmMergeOrder(o, position.MarketPosition))
				.ToList();
			if (brackets.Count == 0) return false;

			Order stop = brackets.FirstOrDefault(o => o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit);
			Order target = brackets.FirstOrDefault(o => o.OrderType == OrderType.Limit);
			if (stop == null && target == null) return false;

			lock (atmScaleInLock)
			{
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
			if (!cachedIsAtmMerge || observed == null || observed.Name != "Entry") return;

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
					double open  = isCurrentCandle ? cachedCurrentOpen[barIdx]  : cachedPrevOpen[barIdx];
					double close = isCurrentCandle ? cachedCurrentClose[barIdx] : cachedPrevClose[barIdx];

					basePrice = KatTradeCalculator.CalculateCandlePrice(katAction, cachedIsPartialCandle, cachedPartialPercent, high, low, open, close, barIdx == 0 && isRenkoChart, cachedTickSize);
					currentPx = cachedCurrentPrice > 0 ? cachedCurrentPrice : basePrice;
				}

				if (basePrice <= 0)
				{
					Print(string.Format("[KatTradeManager] PlaceOrder aborted: basePrice={0} (no bar data cached yet — wait for live ticks)", basePrice));
					return;
				}

				double triggerPrice = KatTradeCalculator.CalculateTriggerPrice(katAction, basePrice, cachedBufferTicks, cachedTickSize);
				KatOrderType katOrderType = KatTradeCalculator.DetermineOrderType(katAction, triggerPrice, currentPx, cachedTickSize, out double limitPrice, out double stopPrice);
				OrderType orderType = ToNtOrderType(katOrderType);

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing order", true);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing order: {0}", ex.ToString()));
			}
		}

		private void PlaceFixedDistanceOrder(OrderAction action)
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}

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

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, "placing fixed distance order", true);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing fixed distance order: {0}", ex.ToString()));
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
						basePrice = KatTradeCalculator.CalculateCandlePrice(
							katAction,
							cachedIsPartialCandle,
							cachedPartialPercent,
							ema34TouchHigh[barIdx],
							ema34TouchLow[barIdx],
							ema34TouchOpen[barIdx],
							ema34TouchClose[barIdx],
							barIdx == 0 && isRenkoChart,
							cachedTickSize);
					}
					else if (emaPeriod == 89)
					{
						foundBarsAgo = ema89TouchBarsAgo[barIdx];
						basePrice = KatTradeCalculator.CalculateCandlePrice(
							katAction,
							cachedIsPartialCandle,
							cachedPartialPercent,
							ema89TouchHigh[barIdx],
							ema89TouchLow[barIdx],
							ema89TouchOpen[barIdx],
							ema89TouchClose[barIdx],
							barIdx == 0 && isRenkoChart,
							cachedTickSize);
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

				PlaceOrderInternal(action, triggerPrice, orderType, limitPrice, stopPrice, string.Format("placing EMA {0} order", emaPeriod), false);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Error placing EMA {0} order: {1}", emaPeriod, ex.ToString()));
			}
		}


		private void PlaceOrderInternal(OrderAction action, double triggerPrice, OrderType orderType, double limitPrice, double stopPrice, string errorContext, bool applyEmaFilters)
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}

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
					if (applyEmaFilters && cachedIsEmaPlace)
					{
						double[] emaVals = new double[3];
						int valCount = 0;
						if (EmaPlace1Enabled) emaVals[valCount++] = cachedEmaPlaceValues[0];
						if (EmaPlace2Enabled) emaVals[valCount++] = cachedEmaPlaceValues[1];
						if (EmaPlace3Enabled) emaVals[valCount++] = cachedEmaPlaceValues[2];

						if (valCount > 0 && !KatTradeCalculator.ValidateEmaPlace(katAction, checkPrice, emaVals.Take(valCount).ToArray(), out string errPlace))
						{
							Print(string.Format("[KatTradeManager] Order REJECTED by EMA Place: {0}", errPlace));
							ShowHudStatus(string.Format("EMA Place blocked: {0}", errPlace), System.Windows.Media.Brushes.OrangeRed);
							return;
						}
					}

					// Validation 2: EMA Angle Check
					if (applyEmaFilters && cachedIsEmaAngle)
					{
						double[] currEmas = new double[3];
						double[] prevEmas = new double[3];
						double[] minAngles = new double[3];
						int angleCount = 0;

						if (EmaAngle1Enabled && cachedEmaAngleCurrent[0] > 0 && cachedEmaAnglePrevious[0] > 0)
						{
							currEmas[angleCount] = cachedEmaAngleCurrent[0];
							prevEmas[angleCount] = cachedEmaAnglePrevious[0];
							minAngles[angleCount++] = EmaAngle1MinAngle;
						}
						if (EmaAngle2Enabled && cachedEmaAngleCurrent[1] > 0 && cachedEmaAnglePrevious[1] > 0)
						{
							currEmas[angleCount] = cachedEmaAngleCurrent[1];
							prevEmas[angleCount] = cachedEmaAnglePrevious[1];
							minAngles[angleCount++] = EmaAngle2MinAngle;
						}
						if (EmaAngle3Enabled && cachedEmaAngleCurrent[2] > 0 && cachedEmaAnglePrevious[2] > 0)
						{
							currEmas[angleCount] = cachedEmaAngleCurrent[2];
							prevEmas[angleCount] = cachedEmaAnglePrevious[2];
							minAngles[angleCount++] = EmaAngle3MinAngle;
						}

						if (angleCount > 0 && !KatTradeCalculator.ValidateEmaAngle(katAction, currEmas.Take(angleCount).ToArray(), prevEmas.Take(angleCount).ToArray(), minAngles.Take(angleCount).ToArray(), cachedTickSize, out string errAngle))
						{
							Print(string.Format("[KatTradeManager] Order REJECTED by EMA Angle: {0}", errAngle));
							ShowHudStatus(string.Format("EMA Angle blocked: {0}", errAngle), System.Windows.Media.Brushes.OrangeRed);
							return;
						}
					}
				}

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

				int qty = cachedQuantity > 0 ? cachedQuantity : DefaultQuantity;
				string entryName = "Entry";

				entryOrder = account.CreateOrder(Instrument, action, orderType, OrderEntry.Manual, TimeInForce.Gtc, qty, limitPrice, stopPrice, "", entryName, NinjaTrader.Core.Globals.MaxDate, null);
				if (entryOrder != null)
				{
					if (!SubmitOrder(entryOrder))
					{
						entryOrder = null;
						return;
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
			if (System.Threading.Volatile.Read(ref closeOperationQueued) != 0)
				return true;
			try
			{
				return account.Orders.Any(o => o.Instrument == Instrument && o.Name == CloseOrderName
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
			Order observed = e != null ? e.Order : null;
			if (observed == null)
				return;
			ProcessAtmStartupUpdate(observed);
			TryCompleteActiveAccountOperation();
			if (Instrument == null || observed.Instrument != Instrument)
				return;

			if (cachedIsAtmMerge
				&& (observed.OrderType == OrderType.StopMarket
					|| observed.OrderType == OrderType.StopLimit
					|| observed.OrderType == OrderType.Limit))
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
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				SchedulePendingRevertRetry();
			}
			ProcessAtmScaleInUpdate(observed);
			ScheduleAtmBracketMerge();
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
				var workingOrders = account.Orders.Where(o => o.Name != CloseOrderName
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
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
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
				if (System.Threading.Volatile.Read(ref closeOperationQueued) != 0 || IsCloseInFlight())
				{
					Print("[KatTradeManager] Close already in flight — duplicate close ignored");
					return;
				}

				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
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
				return false;
			}

			if (IsEntryDebounced())
			{
				Print("[KatTradeManager] Duplicate market order ignored (anti-spam debounce).");
				return false;
			}

			try

			{
				int qty = quantityOverride > 0
					? quantityOverride
					: (cachedQuantity > 0 ? cachedQuantity : DefaultQuantity);
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
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
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

				var workingStops = account.Orders.Where(o => o.Instrument == Instrument &&
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

				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
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
			if (requestedAction == 0 || account == null || Instrument == null || IsCloseInFlight()) return;
			if (requestedQuantity <= 0)
			{
				System.Threading.Interlocked.Exchange(ref pendingRevertAction, 0);
				System.Threading.Interlocked.Exchange(ref pendingRevertQuantity, 0);
				return;
			}

			Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
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
						|| account == null || Instrument == null || IsCloseInFlight())
						return;

					Position current = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
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

		private DateTime lastFreezeEnforceTime = DateTime.MinValue;

		private void FreezeCurrentStopLoss()
		{
			if (account == null || Instrument == null)
			{
				if (account == null) Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
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
					IsActiveOrderState(o.OrderState) &&
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
					IsActiveOrderState(o.OrderState) &&
					(o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) &&
					(pos.MarketPosition == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();

				if (workingStops.Count == 0) return;

				if (frozenStopPrice <= 0)
				{
					frozenStopPrice = workingStops[0].StopPrice;
					Print(string.Format("[KatTradeManager] Freeze Trail captured SL price @ {0}", frozenStopPrice));
					return;
				}

				List<Order> changes = new List<Order>();
				foreach (Order stopOrder in workingStops)
				{
					if (Math.Abs(stopOrder.StopPrice - frozenStopPrice) > 0.000001)
					{
						stopOrder.StopPriceChanged = frozenStopPrice;
						// StopLimit must move its limit with the stop, else the trailed limit is left behind
						// and can invert the stop/limit relationship -> rejected or unsafe protective order.
						if (stopOrder.OrderType == OrderType.StopLimit)
						{
							double tickSize = cachedTickSize > 0 ? cachedTickSize : Instrument.MasterInstrument.TickSize;
							stopOrder.LimitPriceChanged = KatTradeCalculator.CalculateFrozenStopLimitPrice(
								pos.MarketPosition == MarketPosition.Long, frozenStopPrice, stopOrder.StopPrice, stopOrder.LimitPrice, tickSize);
						}
						changes.Add(stopOrder);
					}
				}
				if (changes.Count > 0)
				{
					lastFreezeEnforceTime = DateTime.Now;
					QueueAccountOperation(AccountOperationType.Change, changes, "freeze trail stop restoration");
					Print(string.Format("[KatTradeManager] Trailing movement overridden — {0} SL order(s) restored to frozen price {1}", changes.Count, frozenStopPrice));
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
				Position pos = account.Positions.FirstOrDefault(p => p.Instrument == Instrument);
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

				var workingStops = account.Orders.Where(o => o.Instrument == Instrument &&
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
