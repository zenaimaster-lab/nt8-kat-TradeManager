 /* KatTradeManager.CloseOps.cs - Close/flatten/revert (partial class) v1.98 (2026-08-08) */
// ponytail: extracted from KatTradeManager.OrderOps.cs 416-1048 — Close/Flatten/Revert + IsClose helpers. OrderOps 940→~540L.
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		private const string CloseOrderName = "KAT_CLOSE";

		private int pendingRevertAction; // 0 = none, 1 = Buy, 2 = Sell
		private int pendingRevertQuantity;
		private int pendingRevertSubmitInFlight;

		
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

private void SchedulePendingRevertRetry()
		{
			if (ChartControl != null && ChartControl.Dispatcher != null)
				ChartControl.Dispatcher.BeginInvoke(new Action(TrySubmitPendingRevert));
			else
				TrySubmitPendingRevert();
		}

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

				// ponytail: close market = priority lane — bypass FIFO (same head-of-line as entry market)
				try
				{
					account.Submit(new[] { closeOrder });
					Print(string.Format("[KatTradeManager] Close submit IMMEDIATE: action={0} qty={1} state={2}",
						action, pos.Quantity, closeOrder.OrderState));
				}
				catch (Exception ex2)
				{
					System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					Print(string.Format("[KatTradeManager] Close submit failed: {0}", ex2));
					return;
				}
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

				// ponytail: flatten market = priority lane — bypass FIFO
				try
				{
					account.Submit(closeOrders.ToArray());
					Print(string.Format("[KatTradeManager] Flatten-all close submit IMMEDIATE: positions={0}", closeOrders.Count));
				}
				catch (Exception ex2)
				{
					ClearFlattenCloseTracking();
					System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
					Print(string.Format("[KatTradeManager] Flatten submit failed: {0}", ex2));
					return;
				}
			}
			catch (Exception ex)
			{
				ClearFlattenCloseTracking();
				System.Threading.Interlocked.Exchange(ref closeOperationQueued, 0);
				Print(string.Format("[KatTradeManager] Error queuing flatten-all: {0}", ex));
			}
		}

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

	}
}