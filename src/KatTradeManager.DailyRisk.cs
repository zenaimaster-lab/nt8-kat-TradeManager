/* KatTradeManager.DailyRisk.cs - Daily Max DD / Max Profit protection (partial class) v0.92 (2026-07-31) */

using System;
using System.Linq;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		#region Daily Risk Protection Logic
		private double CalculateDailyPnL()
		{
			if (account == null) return 0;

			DateTime currentSessionStartUtc = KatTradeCalculator.GetNySessionStartUtc(DateTime.UtcNow);
			double currentRealizedPnL = 0;
			bool realizedReadOk;
			try
			{
				currentRealizedPnL = account.Get(AccountItem.GrossRealizedProfitLoss, Currency.UsDollar);
				realizedReadOk = true;
			}
			catch
			{
				realizedReadOk = false;
			}

			// Failed reads must not capture a zero baseline — the next successful read would
			// report the entire account realized PnL as today's, a phantom breach.
			if (KatTradeCalculator.ShouldCaptureSessionBaseline(isSessionStartCaptured, currentSessionStartUtc, lastSessionStartUtc, realizedReadOk))
			{
				lastSessionStartUtc = currentSessionStartUtc;
				sessionStartRealizedPnL = currentRealizedPnL;
				isSessionStartCaptured = true;
			}

			double dailyRealized = realizedReadOk ? currentRealizedPnL - sessionStartRealizedPnL : 0;

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

			return KatTradeCalculator.EvaluateDailyRiskBreach(
				cachedIsDailyMaxDD, cachedDailyMaxDD,
				cachedIsDailyMaxProfit, cachedDailyMaxProfit,
				dailyPnL, out breachReason);
		}

		private void EvaluateDailyRiskLimits()
		{
			if (account == null || isTerminated) return;

			if (IsDailyRiskBreached(out string breachReason))
			{
				Position pos = GetInstrumentPosition();
				bool hasOpenPos = (pos != null && pos.MarketPosition != MarketPosition.Flat);
				bool hasWorkingOrders = GetAccountOrdersSnapshot().Any(o => (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted) && o.Instrument == Instrument);

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
