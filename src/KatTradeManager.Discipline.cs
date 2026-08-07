/* KatTradeManager.Discipline.cs - Discipline protects (partial class) v1.30 (2026-08-07) */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		#region Discipline State
		private readonly object disciplineLock = new object();
		private readonly Dictionary<string, DisciplineState> disciplineStates = new Dictionary<string, DisciplineState>(StringComparer.OrdinalIgnoreCase);

		private sealed class DisciplineState
		{
			public int InitialQty;
			public double EntryPrice;
			public double InitialSl;
			public MarketPosition Position = MarketPosition.Flat;
			public bool HasEpisode;
			public double RealizedBeforeEpisode;
			public int ConsecutiveLosses;
			public DateTime LockUntilUtc = DateTime.MinValue;
			// global account-wide loss tracking
			public double LastGlobalRealized;
			public int LastGlobalPosCount;
			public int LastTradesCount = -1;
			public bool HasGlobalBaseline;
		}

		private string GetDisciplineAccountKey()
		{
			try
			{
				if (account != null && !string.IsNullOrEmpty(account.Name)) return account.Name;
			}
			catch {}
			if (!string.IsNullOrEmpty(AccountName)) return AccountName;
			return "SIM";
		}

		private DisciplineState GetDisciplineState(string key)
		{
			lock (disciplineLock)
			{
				if (!disciplineStates.TryGetValue(key, out var st))
				{
					st = new DisciplineState();
					disciplineStates[key] = st;
				}
				return st;
			}
		}

		private DisciplineState GetCurrentDisciplineState()
		{
			return GetDisciplineState(GetDisciplineAccountKey());
		}

		private double GetRealizedPnLForDiscipline()
		{
			if (account == null) return 0;
			try { return account.Get(AccountItem.GrossRealizedProfitLoss, Currency.UsDollar); }
			catch { return 0; }
		}

		private double CaptureCurrentStopPrice(MarketPosition pos)
		{
			if (account == null || Instrument == null || pos == MarketPosition.Flat) return 0;
			try
			{
				var stops = GetAccountOrdersSnapshot().Where(o => o.Instrument == Instrument
					&& IsActiveOrderState(o.OrderState)
					&& (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit)
					&& (pos == MarketPosition.Long ? (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort) : (o.OrderAction == OrderAction.Buy || o.OrderAction == OrderAction.BuyToCover))).ToList();
				if (stops.Count == 0) return 0;
				// prefer the first/longest? return first
				return stops[0].StopPrice;
			}
			catch { return 0; }
		}

		private void ClearDisciplineEpisode(DisciplineState st)
		{
			if (st == null) return;
			st.InitialQty = 0;
			st.EntryPrice = 0;
			st.InitialSl = 0;
			st.HasEpisode = false;
			st.Position = MarketPosition.Flat;
		}

		private void ResetDisciplineForAccount(string key)
		{
			lock (disciplineLock)
			{
				disciplineStates.Remove(key);
			}
		}
		#endregion

		private double GetTradeProfit(object trade)
		{
			if (trade == null) return 0;
			try
			{
				var t = trade.GetType();
				// try common profit property names
				string[] names = new[] { "ProfitCurrency", "Profit", "RealizedProfitLoss", "CurrencyProfit", "PnL", "GrossProfit" };
				foreach (string n in names)
				{
					var pi = t.GetProperty(n);
					if (pi != null)
					{
						object v = pi.GetValue(trade);
						if (v is double d) return d;
						if (v is float f) return f;
						if (v is decimal dec) return (double)dec;
						if (v is int ii) return ii;
						try { return Convert.ToDouble(v); } catch {}
					}
				}
			}
			catch {}
			return 0;
		}

		#region Discipline Episode Update
		private void UpdateDisciplineFromPosition()
		{
			Position pos = null;
			try { pos = GetInstrumentPosition(); } catch {}
			string key = GetDisciplineAccountKey();
			DisciplineState st = GetDisciplineState(key);
			bool isFlat = pos == null || pos.MarketPosition == MarketPosition.Flat;

			// snapshot global account state before lock (avoids nested lock order)
			double curGlobalRealized = 0;
			int curGlobalPosCount = 0;
			int curTradesCount = -1;
			object tradesObj = null;
			try { curGlobalRealized = GetRealizedPnLForDiscipline(); } catch {}
			try { curGlobalPosCount = GetAccountPositionsSnapshot().Count(p => p.MarketPosition != MarketPosition.Flat); } catch {}
			// snapshot Trades for per-trade loss counting (reflection-safe, no compile-time Trade dependency)
			try
			{
				if (account != null)
				{
					var pi = account.GetType().GetProperty("Trades");
					if (pi != null)
					{
						tradesObj = pi.GetValue(account);
						if (tradesObj is System.Collections.IList list) { lock (tradesObj) curTradesCount = list.Count; }
						else if (tradesObj != null)
						{
							lock (tradesObj)
							{
								var cntPi = tradesObj.GetType().GetProperty("Count");
								if (cntPi != null) curTradesCount = (int)cntPi.GetValue(tradesObj);
							}
						}
					}
				}
			}
			catch { curTradesCount = -1; tradesObj = null; }

			double capturedSl = 0;
			MarketPosition capturedMp = isFlat ? MarketPosition.Flat : pos.MarketPosition;
			if (!isFlat)
			{
				try { capturedSl = CaptureCurrentStopPrice(capturedMp); } catch {}
			}
			bool shouldCancelSizing = false;
			int sizingInitialForLog = 0;

			lock (disciplineLock)
			{
				// per-instrument episode for Sizing / SL-pull / Loss-DCA / TP-early
				if (!isFlat)
				{
					MarketPosition mp = pos.MarketPosition;
					if (!st.HasEpisode)
					{
						st.HasEpisode = true;
						st.Position = mp;
						st.EntryPrice = pos.AveragePrice;
						if (atmQuantity > 0) st.InitialQty = atmQuantity;
						else if (pos.Quantity > 0) st.InitialQty = pos.Quantity;
						else if (DefaultQuantity > 0) st.InitialQty = DefaultQuantity;
						st.RealizedBeforeEpisode = curGlobalRealized;
						if (capturedSl > 0) st.InitialSl = capturedSl;
						if (cachedSizingProtect)
						{
							shouldCancelSizing = true;
							sizingInitialForLog = st.InitialQty;
						}
					}
					else
					{
						st.Position = mp;
						if (st.EntryPrice <= 0) st.EntryPrice = pos.AveragePrice;
						if (st.InitialSl <= 0 && capturedSl > 0) st.InitialSl = capturedSl;
						if (st.InitialQty <= 0 && pos.Quantity > 0) st.InitialQty = pos.Quantity;
					}
				}
				else
				{
					if (st.HasEpisode)
					{
						// episode ended — keep realized for global detection, just clear episode
						ClearDisciplineEpisode(st);
					}
				}

				// account-wide LossTimes tracking — per-trade via Trades collection (precise), fallback to realized delta
				bool useTrades = curTradesCount >= 0 && st.LastTradesCount >= 0;
				if (!st.HasGlobalBaseline)
				{
					st.LastGlobalRealized = curGlobalRealized;
					st.LastGlobalPosCount = curGlobalPosCount;
					if (curTradesCount >= 0) st.LastTradesCount = curTradesCount;
					st.HasGlobalBaseline = true;
				}
				else if (useTrades && curTradesCount > st.LastTradesCount && tradesObj is System.Collections.IList list)
				{
					lock (tradesObj)
					{
						for (int i = st.LastTradesCount; i < curTradesCount; i++)
						{
							object tr = null;
							try { tr = list[i]; } catch { try { var gi = tradesObj.GetType().GetMethod("get_Item"); if (gi != null) tr = gi.Invoke(tradesObj, new object[] { i }); } catch {} }
						double profit = GetTradeProfit(tr);
						if (Math.Abs(profit) < 0.01) continue; // ignore breakeven/commission noise
						if (profit < 0)
						{
							st.ConsecutiveLosses++;
							if (cachedLossTimesProtect && KatTradeCalculator.ShouldTriggerLossLock(st.ConsecutiveLosses, cachedLossTimesMaxLosses))
							{
								st.LockUntilUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, cachedLossTimesLockMinutes));
								Print(string.Format("[KatTradeManager] LossTimes lock (trade): {0} losses -> locked until {1:HH:mm:ss} UTC ({2}m) profit={3}", st.ConsecutiveLosses, st.LockUntilUtc, cachedLossTimesLockMinutes, profit));
							}
						}
						else
						{
							st.ConsecutiveLosses = 0;
						}
					}
					st.LastTradesCount = curTradesCount;
					st.LastGlobalRealized = curGlobalRealized;
					st.LastGlobalPosCount = curGlobalPosCount;
					}
				}
				else
				{
					// fallback: realized delta + pos count (when Trades not available or no new trades)
					if (curTradesCount >= 0) st.LastTradesCount = curTradesCount;
					if (curGlobalPosCount < st.LastGlobalPosCount)
					{
						double delta = curGlobalRealized - st.LastGlobalRealized;
						if (Math.Abs(delta) >= 0.05)
						{
							if (delta < 0)
							{
								st.ConsecutiveLosses++;
								if (cachedLossTimesProtect && KatTradeCalculator.ShouldTriggerLossLock(st.ConsecutiveLosses, cachedLossTimesMaxLosses))
								{
									st.LockUntilUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, cachedLossTimesLockMinutes));
									Print(string.Format("[KatTradeManager] LossTimes lock: {0} losses -> locked until {1:HH:mm:ss} UTC ({2}m)", st.ConsecutiveLosses, st.LockUntilUtc, cachedLossTimesLockMinutes));
								}
							}
							else
							{
								st.ConsecutiveLosses = 0;
							}
						}
					}
					st.LastGlobalRealized = curGlobalRealized;
					st.LastGlobalPosCount = curGlobalPosCount;
				}
			}
			if (shouldCancelSizing)
			{
				try
				{
					bool isLongSizing = capturedMp == MarketPosition.Long;
					var workingEntries = GetAccountOrdersSnapshot().Where(o => o.Instrument == Instrument
						&& IsActiveOrderState(o.OrderState)
						&& (o.Name == "Entry" || o.Name == "MarketBuy" || o.Name == "MarketSell")
						&& ((isLongSizing && o.OrderAction == OrderAction.Buy) || (!isLongSizing && (o.OrderAction == OrderAction.Sell || o.OrderAction == OrderAction.SellShort)))).ToArray();
					if (workingEntries.Length > 0)
					{
						QueueAccountOperation(AccountOperationType.Cancel, workingEntries, "sizing protect: cancel pending adds after fill");
						Print(string.Format("[KatTradeManager] Sizing protect: cancelled {0} pending entry orders after fill (max {1})", workingEntries.Length, sizingInitialForLog));
					}
				}
				catch {}
			}
		}

		private void EvaluateDisciplineLockVisual()
		{
			DisciplineState st = GetCurrentDisciplineState();
			bool locked = false;
			DateTime until = DateTime.MinValue;
			int losses = 0;
			lock (disciplineLock)
			{
				locked = cachedLossTimesProtect && KatTradeCalculator.IsLossTimesLockActive(st.LockUntilUtc, DateTime.UtcNow);
				until = st.LockUntilUtc;
				losses = st.ConsecutiveLosses;
			}
			if (locked)
			{
				var remaining = until - DateTime.UtcNow;
				int mins = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
				int secs = Math.Max(0, (int)remaining.TotalSeconds % 60);
				string msg = string.Format("LossTimes LOCKED {0}m{1:D2}s left ({2} losses) — trading paused", mins, secs, losses);
				ShowHudStatus(msg, Brushes.OrangeRed, true);
			}
		}
		#endregion

		#region Discipline Gates
		private bool IsLossTimesLocked(out string reason)
		{
			reason = string.Empty;
			if (!cachedLossTimesProtect) return false;
			DisciplineState st = GetCurrentDisciplineState();
			DateTime until;
			int losses;
			lock (disciplineLock) { until = st.LockUntilUtc; losses = st.ConsecutiveLosses; }
			if (KatTradeCalculator.IsLossTimesLockActive(until, DateTime.UtcNow))
			{
				var rem = until - DateTime.UtcNow;
				int mins = Math.Max(0, (int)Math.Ceiling(rem.TotalMinutes));
				reason = string.Format("LossTimes locked {0}m left ({1} losses)", mins, losses);
				return true;
			}
			// expired but still within object -> clear visual? No need
			return false;
		}

		private bool IsTimingLocked(out string reason)
		{
			reason = string.Empty;
			if (!cachedTimingProtect) return false;
			var windows = new List<KatTradeCalculator.KatTradingWindow>(3);
			windows.Add(new KatTradeCalculator.KatTradingWindow { Enabled = cachedTw1Enabled, StartHour = cachedTw1StartHour, StartMinute = cachedTw1StartMinute, EndHour = cachedTw1EndHour, EndMinute = cachedTw1EndMinute });
			windows.Add(new KatTradeCalculator.KatTradingWindow { Enabled = cachedTw2Enabled, StartHour = cachedTw2StartHour, StartMinute = cachedTw2StartMinute, EndHour = cachedTw2EndHour, EndMinute = cachedTw2EndMinute });
			windows.Add(new KatTradeCalculator.KatTradingWindow { Enabled = cachedTw3Enabled, StartHour = cachedTw3StartHour, StartMinute = cachedTw3StartMinute, EndHour = cachedTw3EndHour, EndMinute = cachedTw3EndMinute });
			bool anyEnabled = windows.Any(w => w.Enabled);
			if (!anyEnabled)
			{
				reason = "No Trading Window enabled — trading blocked";
				return true;
			}

			DateTime ny = KatTradeCalculator.GetNyTime(DateTime.UtcNow);
			TimeSpan tod = ny.TimeOfDay;
			bool inside = KatTradeCalculator.IsWithinTradingWindows(tod, windows);
			if (!inside)
			{
				reason = string.Format("Outside Trading Window (NY {0:hh\\:mm})", tod);
				return true;
			}
			return false;
		}

		private bool IsSizingBlockedForEntry(OrderAction action, int orderQty, out string reason)
		{
			reason = string.Empty;
			if (!cachedSizingProtect) return false;
			DisciplineState st = GetCurrentDisciplineState();
			Position pos = GetInstrumentPosition();
			bool hasPos = pos != null && pos.MarketPosition != MarketPosition.Flat;
			if (!hasPos) return false;
			bool isLong = pos.MarketPosition == MarketPosition.Long;
			int posQty = pos.Quantity;
			int initQty;
			lock (disciplineLock) { initQty = st.InitialQty; }
			// fallback if initial not captured yet -> use atmQuantity
			if (initQty <= 0) initQty = atmQuantity > 0 ? atmQuantity : DefaultQuantity;
			KatOrderAction katAct = action == OrderAction.Buy ? KatOrderAction.Buy : KatOrderAction.Sell;
			// Need to map BuyToCover/SellShort correctly
			if (action == OrderAction.BuyToCover) katAct = KatOrderAction.Buy;
			if (action == OrderAction.SellShort) katAct = KatOrderAction.Sell;
			if (KatTradeCalculator.IsSizingBlocked(hasPos, isLong, katAct, posQty, initQty, orderQty))
			{
				reason = string.Format("Sizing protect: max {0} lots (position {1})", initQty, posQty);
				return true;
			}
			return false;
		}

		private bool IsLossDcaBlockedForEntry(OrderAction action, out string reason)
		{
			reason = string.Empty;
			if (!cachedLossDcaProtect) return false;
			DisciplineState st = GetCurrentDisciplineState();
			Position pos = GetInstrumentPosition();
			if (pos == null || pos.MarketPosition == MarketPosition.Flat) return false;
			bool isLong = pos.MarketPosition == MarketPosition.Long;
			KatOrderAction katAct = action == OrderAction.Buy ? KatOrderAction.Buy : KatOrderAction.Sell;
			if (action == OrderAction.BuyToCover) katAct = KatOrderAction.Buy;
			if (action == OrderAction.SellShort) katAct = KatOrderAction.Sell;
			// only block scale-ins
			if (!KatTradeCalculator.IsScaleIn(isLong, katAct)) return false;
			double entryPx;
			lock (disciplineLock) { entryPx = st.EntryPrice; }
			if (entryPx <= 0) entryPx = pos.AveragePrice;
			double curPrice = 0;
			lock (priceLock) { curPrice = cachedCurrentPrice; }
			if (curPrice <= 0 && Instrument != null && Instrument.MarketData != null && Instrument.MarketData.Last != null)
				curPrice = Instrument.MarketData.Last.Price;
			double tick = cachedTickSize > 0 ? cachedTickSize : (Instrument != null ? Instrument.MasterInstrument.TickSize : 0.25);
			if (KatTradeCalculator.IsLossDcaBlocked(isLong, entryPx, curPrice, tick))
			{
				reason = string.Format("Loss-DCA blocked: price {0} vs entry {1} (against)", curPrice, entryPx);
				return true;
			}
			return false;
		}

		private bool IsTpEarlyBlockedForEntry(OrderAction action, out string reason)
		{
			reason = string.Empty;
			if (!cachedTpEarlyProtect) return false;
			Position pos = GetInstrumentPosition();
			if (pos == null || pos.MarketPosition == MarketPosition.Flat) return false;
			bool isLong = pos.MarketPosition == MarketPosition.Long;
			KatOrderAction katAct = action == OrderAction.Buy ? KatOrderAction.Buy : KatOrderAction.Sell;
			if (action == OrderAction.BuyToCover) katAct = KatOrderAction.Buy;
			if (action == OrderAction.SellShort) katAct = KatOrderAction.Sell;
			if (KatTradeCalculator.IsScaleOut(isLong, katAct))
			{
				reason = "TP-early protect: scale-out blocked (must run to TP)";
				return true;
			}
			return false;
		}

		private bool TryRejectDisciplineForEntry(OrderAction action, int orderQty, double triggerPrice, out string reason)
		{
			reason = string.Empty;
			// LossTimes has priority - most user-visible
			if (IsLossTimesLocked(out string r1)) { reason = r1; return true; }
			if (IsTimingLocked(out string r2)) { reason = r2; return true; }
			// Sizing blocks any add
			if (IsSizingBlockedForEntry(action, orderQty, out string r3)) { reason = r3; return true; }
			if (IsLossDcaBlockedForEntry(action, out string r4)) { reason = r4; return true; }
			// TP early scale-out is also entry gate for market scale-out
			if (IsTpEarlyBlockedForEntry(action, out string r5)) { reason = r5; return true; }
			return false;
		}

		private bool TryRejectDisciplineForClose(out string reason)
		{
			reason = string.Empty;
			if (!cachedTpEarlyProtect) return false;
			// Daily Risk emergency flatten must bypass TP-early (safety over discipline)
			if (IsDailyRiskBreached(out string _riskReason)) return false;
			Position pos = GetInstrumentPosition();
			if (pos == null || pos.MarketPosition == MarketPosition.Flat) return false;
			// any close while in position is blocked by TP-early
			reason = "TP-early protect: Close/flatten blocked (must run to TP)";
			return true;
		}

		private bool TryRejectDisciplineForSlMove(double newSl, out string reason)
		{
			reason = string.Empty;
			if (!cachedSlPullProtect) return false;
			DisciplineState st = GetCurrentDisciplineState();
			Position pos = GetInstrumentPosition();
			if (pos == null || pos.MarketPosition == MarketPosition.Flat) return false;
			double initSl;
			lock (disciplineLock) { initSl = st.InitialSl; }
			if (initSl <= 0) return false; // no baseline yet
			bool isLong = pos.MarketPosition == MarketPosition.Long;
			double tick = cachedTickSize > 0 ? cachedTickSize : (Instrument != null ? Instrument.MasterInstrument.TickSize : 0.25);
			if (KatTradeCalculator.IsSlPullBlocked(isLong, initSl, newSl, tick))
			{
				reason = string.Format("SL-pull protect: {0} beyond initial {1}", newSl, initSl);
				return true;
			}
			return false;
		}
		#endregion
	}
}
