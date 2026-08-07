/* KatTradeManager.AtmMerge.cs - ATM bracket merge & scale-in (partial class) v1.42 (2026-08-08) */
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
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
		private readonly object atmTemplateCacheLock = new object();
		private readonly Dictionary<string, Tuple<bool, DateTime>> atmTemplateCache = new Dictionary<string, Tuple<bool, DateTime>>(StringComparer.OrdinalIgnoreCase);
		private bool HasAtmTemplate(string templateName)
		{
			if (string.IsNullOrEmpty(templateName)) return false;
			lock (atmTemplateCacheLock)
			{
				Tuple<bool, DateTime> cached;
				if (atmTemplateCache.TryGetValue(templateName, out cached) && (DateTime.UtcNow - cached.Item2).TotalSeconds < 5)
					return cached.Item1;
			}
			string path = System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy", templateName + ".xml");
			bool exists = System.IO.File.Exists(path);
			lock (atmTemplateCacheLock) atmTemplateCache[templateName] = Tuple.Create(exists, DateTime.UtcNow);
			return exists;
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

	
	}
}
