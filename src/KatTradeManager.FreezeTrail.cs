/* KatTradeManager.FreezeTrail.cs - Freeze Trail: ATM detach / HUD takeover (partial class) v0.91 (2026-07-31) */

using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class KatTradeManager
	{
		#region Freeze Trail (ATM detach / HUD takeover)
		// Freeze Trail does NOT fight the ATM trail by re-pushing stop prices — that lost the race every time,
		// clobbered the independent ATMs of later entries, and reverted BE / Swing SL / chart drags. Instead it
		// ABANDONS the ATM: every ATM-owned protective exit on this instrument is cancelled and replaced with one
		// static KAT_FRZ bracket the HUD owns. Nothing trails afterwards and no code writes those prices again.
		private const string FreezeOrderPrefix = "KAT_FRZ";
		private const string FreezeStopOrderName = "KAT_FRZ_SL";
		private const string FreezeTargetOrderName = "KAT_FRZ_TP";
		private int freezeDetachInFlight;
		private DateTime freezeFlatSinceUtc = DateTime.MinValue;

		private sealed class FreezeExitCapture
		{
			public OrderType Type;
			public double StopPrice;
			public double LimitPrice;
		}

		private static bool IsFreezeProtectionOrder(Order order)
		{
			return order != null
				&& !string.IsNullOrEmpty(order.Name)
				&& order.Name.StartsWith(FreezeOrderPrefix, StringComparison.OrdinalIgnoreCase);
		}

		private void FreezeCurrentStopLoss()
		{
			if (account == null)
			{
				Print("[KatTradeManager] No account — watchdog auto-recovering. Retry in a moment.");
				return;
			}
			// Nothing to detach while flat: the watchdog takes over every ATM bracket that appears later,
			// which is what makes freeze cover ALL orders, including 2nd+ entries with their own ATM.
			ProcessFreezeTrail();
		}

		private void ProcessFreezeTrail()
		{
			if (account == null || Instrument == null) return;
			try
			{
				Position pos;
				var positions = account.Positions;
				lock (positions)
					pos = positions.FirstOrDefault(p => p.Instrument == Instrument);

				if (pos == null || pos.MarketPosition == MarketPosition.Flat)
				{
					if (freezeFlatSinceUtc == DateTime.MinValue)
						freezeFlatSinceUtc = DateTime.UtcNow;
					CancelFreezeOrphans();
					return;
				}

				freezeFlatSinceUtc = DateTime.MinValue;
				if (!cachedIsFreezeTrail) return;

				DetachAtmProtection(pos.MarketPosition);
				ReconcileFreezeQuantity(pos.MarketPosition, pos.Quantity);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Freeze Trail processing failed: {0}", ex.Message));
			}
		}

		// Cancels every ATM protective exit for the live position and re-submits one static bracket instead.
		private void DetachAtmProtection(MarketPosition position)
		{
			if (System.Threading.Volatile.Read(ref freezeDetachInFlight) != 0) return;

			List<Order> atmExits;
			var orders = account.Orders;
			lock (orders)
				atmExits = orders.Where(o => IsAtmMergeOrder(o, position)).ToList();
			if (atmExits.Count == 0) return;

			// Value snapshot: the Order objects keep trailing until the cancel settles.
			List<FreezeExitCapture> captures = atmExits
				.Select(o => new FreezeExitCapture { Type = o.OrderType, StopPrice = o.StopPrice, LimitPrice = o.LimitPrice })
				.ToList();

			if (System.Threading.Interlocked.CompareExchange(ref freezeDetachInFlight, 1, 0) != 0) return;
			// ponytail: cancel-then-submit leaves a sub-second unprotected window; submitting first would
			// double the protective quantity, which exits twice on a gap. Ceiling = broker-side replace.
			QueueAccountOperation(
				AccountOperationType.Cancel,
				atmExits,
				"freeze detach ATM protection",
				completion: () => SubmitFreezeProtection(captures, position));
			Print(string.Format("[KatTradeManager] Freeze detach queued: atmExits={0} position={1}", atmExits.Count, position));
		}

		private void SubmitFreezeProtection(List<FreezeExitCapture> captures, MarketPosition position)
		{
			try
			{
				if (account == null || Instrument == null || captures == null || captures.Count == 0) return;

				Position pos;
				var positions = account.Positions;
				lock (positions)
					pos = positions.FirstOrDefault(p => p.Instrument == Instrument);
				if (pos == null || pos.MarketPosition != position || pos.Quantity <= 0)
				{
					Print("[KatTradeManager] Freeze detach: position gone or reversed — static protection skipped.");
					return;
				}

				bool isLong = position == MarketPosition.Long;
				FreezeExitCapture stop = null;
				FreezeExitCapture target = null;
				foreach (FreezeExitCapture capture in captures)
				{
					if (capture.Type == OrderType.StopMarket || capture.Type == OrderType.StopLimit)
					{
						if (stop == null || KatTradeCalculator.IsPreferredFreezePrice(isLong, capture.StopPrice, stop.StopPrice))
							stop = capture;
					}
					else if (capture.Type == OrderType.Limit
						&& (target == null || KatTradeCalculator.IsPreferredFreezePrice(isLong, capture.LimitPrice, target.LimitPrice)))
					{
						target = capture;
					}
				}

				OrderAction exitAction = isLong ? OrderAction.Sell : OrderAction.BuyToCover;

				// Frozen legs already working already protect this position. Submitting again on every
				// re-detach stacked duplicate SL/TP pairs on the chart, each under its own OCO, so two
				// stops could both fill and flip the position. Dedupe per leg instead.
				Order existingStop = null;
				Order existingTarget = null;
				var orders = account.Orders;
				lock (orders)
				{
					foreach (Order o in orders)
					{
						if (o.Instrument != Instrument
							|| !IsFreezeProtectionOrder(o)
							|| !IsActiveOrderState(o.OrderState)
							|| !IsAtmExitAction(o.OrderAction, position))
							continue;
						if ((o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit) && existingStop == null)
							existingStop = o;
						else if (o.OrderType == OrderType.Limit && existingTarget == null)
							existingTarget = o;
					}
				}

				double livePrice;
				lock (priceLock)
					livePrice = cachedCurrentPrice;

				bool hasCapturedStop = stop != null && stop.StopPrice > 0;
				bool hasCapturedTarget = target != null && target.LimitPrice > 0;
				// A captured price the market has already passed would be broker-rejected (platform error popups).
				bool stopValid = hasCapturedStop
					&& (livePrice <= 0 || KatTradeCalculator.IsStopOnValidSide(isLong, stop.StopPrice, livePrice));
				bool targetValid = hasCapturedTarget
					&& (livePrice <= 0 || KatTradeCalculator.IsLimitOnValidSide(isLong, target.LimitPrice, livePrice));

				bool wantStop = KatTradeCalculator.ShouldSubmitFreezeLeg(existingStop != null, hasCapturedStop, stopValid);
				bool wantTarget = KatTradeCalculator.ShouldSubmitFreezeLeg(existingTarget != null, hasCapturedTarget, targetValid);

				if (hasCapturedStop && !stopValid)
					Print(string.Format("[KatTradeManager] Freeze: captured stop {0} already passed by market {1} — skipped (would be rejected).", stop.StopPrice, livePrice));
				if (hasCapturedTarget && !targetValid)
					Print(string.Format("[KatTradeManager] Freeze: captured target {0} already passed by market {1} — skipped (would be rejected).", target.LimitPrice, livePrice));

				if (!wantStop && !wantTarget)
				{
					if (existingStop != null || existingTarget != null)
						Print("[KatTradeManager] Freeze: static protection already in place — duplicate submit skipped.");
					else
					{
						Print("[KatTradeManager] Freeze detach: no usable captured price — position has NO protective order.");
						ShowHudStatus("Freeze: ATM detached, no SL captured — set SL manually", System.Windows.Media.Brushes.OrangeRed);
					}
					return;
				}

				// Link the pair under one OCO: reuse the surviving leg's OCO so the new leg cancels its sibling.
				bool paired = (wantStop || existingStop != null) && (wantTarget || existingTarget != null);
				string oco = string.Empty;
				if (paired)
				{
					if (existingStop != null && !string.IsNullOrEmpty(existingStop.Oco))
						oco = existingStop.Oco;
					else if (existingTarget != null && !string.IsNullOrEmpty(existingTarget.Oco))
						oco = existingTarget.Oco;
					else
						oco = FreezeOrderPrefix + DateTime.UtcNow.Ticks.ToString();
				}

				int qty = pos.Quantity;
				List<Order> submits = new List<Order>();
				if (wantStop)
				{
					// StopLimit keeps its captured limit price, so the protective stop/limit relationship survives.
					double stopLimitPrice = stop.Type == OrderType.StopLimit ? stop.LimitPrice : 0;
					Order frozenStop = account.CreateOrder(Instrument, exitAction, stop.Type, OrderEntry.Manual, TimeInForce.Gtc,
						qty, stopLimitPrice, stop.StopPrice, oco, FreezeStopOrderName, NinjaTrader.Core.Globals.MaxDate, null);
					if (frozenStop != null) submits.Add(frozenStop);
				}
				if (wantTarget)
				{
					Order frozenTarget = account.CreateOrder(Instrument, exitAction, OrderType.Limit, OrderEntry.Manual, TimeInForce.Gtc,
						qty, target.LimitPrice, 0, oco, FreezeTargetOrderName, NinjaTrader.Core.Globals.MaxDate, null);
					if (frozenTarget != null) submits.Add(frozenTarget);
				}

				if (submits.Count == 0)
				{
					Print("[KatTradeManager] Freeze detach: order creation failed — position may have NO protective order.");
					ShowHudStatus("Freeze: static order creation failed — check position manually", System.Windows.Media.Brushes.OrangeRed);
					return;
				}

				QueueAccountOperation(AccountOperationType.Submit, submits, "freeze static protection");
				Print(string.Format("[KatTradeManager] Freeze static protection queued: qty={0} newStop={1} newTarget={2} keptStop={3} keptTarget={4} oco={5}",
					qty,
					wantStop ? stop.StopPrice.ToString() : "no",
					wantTarget ? target.LimitPrice.ToString() : "no",
					existingStop != null ? existingStop.StopPrice.ToString() : "none",
					existingTarget != null ? existingTarget.LimitPrice.ToString() : "none",
					oco));
				ShowHudStatus(string.Format("Freeze ON: static SL {0}{1}",
					wantStop ? stop.StopPrice.ToString() : (existingStop != null ? existingStop.StopPrice.ToString() : "none"),
					wantTarget ? " / TP " + target.LimitPrice.ToString() : (existingTarget != null ? " / TP " + existingTarget.LimitPrice.ToString() : string.Empty)),
					System.Windows.Media.Brushes.Orange);
			}
			catch (Exception ex)
			{
				Print(string.Format("[KatTradeManager] Freeze static protection failed: {0}", ex.ToString()));
			}
			finally
			{
				System.Threading.Interlocked.Exchange(ref freezeDetachInFlight, 0);
			}
		}

		// Scale-in/scale-out only: quantity, never price. Manual SL moves must stick.
		// Also sweeps stacked duplicate legs (left by pre-v0.90 versions): two live frozen stops would
		// both fill and flip the position. Keeps the single best leg per kind, cancels the rest.
		private void ReconcileFreezeQuantity(MarketPosition position, int positionQuantity)
		{
			if (positionQuantity <= 0) return;

			List<Order> freezeExits;
			var orders = account.Orders;
			lock (orders)
				freezeExits = orders.Where(o => o.Instrument == Instrument
					&& IsFreezeProtectionOrder(o)
					&& IsAtmExitAction(o.OrderAction, position)
					&& IsAccountOperationEligible(AccountOperationType.Change, o)).ToList();

			bool isLong = position == MarketPosition.Long;
			Order bestStop = null;
			Order bestTarget = null;
			List<Order> staleDuplicates = new List<Order>();
			List<Order> changes = new List<Order>();
			foreach (Order exit in freezeExits)
			{
				bool isStopLeg = exit.OrderType == OrderType.StopMarket || exit.OrderType == OrderType.StopLimit;
				if (isStopLeg)
				{
					if (bestStop == null || KatTradeCalculator.IsPreferredFreezePrice(isLong, exit.StopPrice, bestStop.StopPrice))
					{
						if (bestStop != null) staleDuplicates.Add(bestStop);
						bestStop = exit;
					}
					else
					{
						staleDuplicates.Add(exit);
					}
				}
				else if (exit.OrderType == OrderType.Limit)
				{
					if (bestTarget == null || KatTradeCalculator.IsPreferredFreezePrice(isLong, exit.LimitPrice, bestTarget.LimitPrice))
					{
						if (bestTarget != null) staleDuplicates.Add(bestTarget);
						bestTarget = exit;
					}
					else
					{
						staleDuplicates.Add(exit);
					}
				}
			}
			if (staleDuplicates.Count > 0)
			{
				QueueAccountOperation(AccountOperationType.Cancel, staleDuplicates, "freeze duplicate sweep");
				Print(string.Format("[KatTradeManager] Freeze duplicate sweep: cancelled={0} keptStop={1} keptTarget={2}",
					staleDuplicates.Count,
					bestStop != null ? bestStop.StopPrice.ToString() : "none",
					bestTarget != null ? bestTarget.LimitPrice.ToString() : "none"));
			}

			foreach (Order exit in freezeExits)
			{
				if (staleDuplicates.Contains(exit)) continue;
				if (!KatTradeCalculator.ShouldAdjustFreezeQuantity(exit.Quantity, positionQuantity)) continue;
				exit.QuantityChanged = positionQuantity;
				changes.Add(exit);
			}
			if (changes.Count == 0) return;

			QueueAccountOperation(AccountOperationType.Change, changes, "freeze quantity reconcile");
			Print(string.Format("[KatTradeManager] Freeze quantity reconciled: orders={0} positionQty={1}", changes.Count, positionQuantity));
		}

		private void CancelFreezeOrphans()
		{
			double flatAge = freezeFlatSinceUtc == DateTime.MinValue
				? -1
				: (DateTime.UtcNow - freezeFlatSinceUtc).TotalMilliseconds;
			if (!KatTradeCalculator.ShouldCancelFreezeOrphans(true, flatAge, AtmLifecycleGraceMilliseconds)) return;

			List<Order> orphans;
			var orders = account.Orders;
			lock (orders)
				orphans = orders.Where(o => o.Instrument == Instrument
					&& IsFreezeProtectionOrder(o)
					&& IsAccountOperationEligible(AccountOperationType.Cancel, o)).ToList();
			if (orphans.Count == 0) return;

			QueueAccountOperation(AccountOperationType.Cancel, orphans, "freeze flat cleanup");
			Print(string.Format("[KatTradeManager] Freeze flat cleanup: cancelled={0} flatAgeMs={1:F0}", orphans.Count, flatAge));
		}
		#endregion
	}
}
