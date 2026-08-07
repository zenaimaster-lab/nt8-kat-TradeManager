/* KatTradeManager.Queue.cs - Account operation FIFO queue (partial class) v1.40 (2026-08-08) */
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
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
				try { PumpAccountOperationQueue(); }
				finally { System.Threading.Interlocked.Exchange(ref accountOperationPumpScheduled, 0); }
			};
			try
			{
				if (ChartControl != null && ChartControl.Dispatcher != null)
					ChartControl.Dispatcher.BeginInvoke(pump);
				else
					pump();
			}
			catch { System.Threading.Interlocked.Exchange(ref accountOperationPumpScheduled, 0); }
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
				catch (Exception ex) { Print(string.Format("[KatTradeManager] Account operation continuation failed: {0}", ex.Message)); }
			}
			ScheduleAccountOperationPump();
		}

		private bool IsAccountOperationSettled(AccountOperationRequest request)
		{
			if (request == null || !request.CallReturned || request.Orders == null || request.Orders.Length == 0)
				return false;
			if (request.ExecuteOverride != null)
				return true;
			foreach (Order order in request.Orders)
			{
				if (order == null) continue;
				if (request.Type == AccountOperationType.Cancel && !IsTerminalOrderState(order.OrderState))
					return false;
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
			Order[] requested = (orders ?? Enumerable.Empty<Order>()).Where(order => order != null).Distinct().ToArray();
			if (requested.Length == 0) { completion?.Invoke(); return; }
			AccountOperationRequest request = new AccountOperationRequest
			{
				Type = type,
				Orders = requested,
				Reason = reason
			};
			if (completion != null) request.Completions.Add(completion);
			request.ExecuteOverride = executeOverride;
			lock (accountOperationLock)
			{
				AccountOperationRequest overlap = FindOverlappingOperationLocked(requested);
				if (overlap != null)
				{
					if (overlap.Type == type)
					{
						Order[] remaining = requested.Where(order => !overlap.Orders.Any(existing => SameOrder(existing, order))).ToArray();
						if (remaining.Length == 0) { if (completion != null) overlap.Completions.Add(completion); }
						else overlap.Completions.Add(() => QueueAccountOperation(type, remaining, reason, completion, executeOverride));
					}
					else overlap.Completions.Add(() => QueueAccountOperation(type, requested, reason, completion, executeOverride));
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
				dispatchOrders = request.Orders.Where(order => IsAccountOperationEligible(request.Type, order)).ToArray();
				if (dispatchOrders.Length == 0)
				{
					bool waitingForPlatform = request.Orders.Any(order => order != null && IsAccountOperationPending(order.OrderState));
					if (waitingForPlatform)
					{
						if (queueHeadStallSinceUtc == DateTime.MinValue) { queueHeadStallSinceUtc = DateTime.UtcNow; return; }
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
				foreach (Action completion in request.Completions) completion?.Invoke();
				ScheduleAccountOperationPump();
				return;
			}
			LogAccountOperation("dispatch", request);
			try
			{
				if (request.ExecuteOverride != null) request.ExecuteOverride();
				else if (request.Type == AccountOperationType.Submit) account.Submit(dispatchOrders);
				else if (request.Type == AccountOperationType.Change) account.Change(dispatchOrders);
				else account.Cancel(dispatchOrders);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Account operation failed: type={0} reason={1} error={2}", request.Type, request.Reason, ex));
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
			lock (flattenCloseLock) flattenCloseOrders.Clear();
		}
	}
}
