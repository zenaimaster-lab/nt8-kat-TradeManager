/* KatTradeManager.ProfileOps.cs - Trading Profile helpers (partial class) v1.64 (2026-08-08) */
// ponytail: extracted from KatTradeManagerUI.cs 500-620 — 14 getters + 2 predicates reused by UI + discipline.
// Switch-based (20 props x8) is intentional minimal; table-driven would add indirection without saving lines here.
// Ceiling: adding TradingWindows(15) + EmaPlace(9) per-profile => ~192 props — upgrade to dictionary/config object when requested.

using System;

namespace NinjaTrader.NinjaScript.Indicators.KAT
{
	public partial class KatTradeManager
	{
		// ponytail: profile bundle covers account/ATM/qty/TF/buffer/StopLimit/EmaProtect/DailyRisk/Discipline (20 props x8). TradingWindows (15) + EmaPlace (9) stay global by design.
		private string GetTradingProfileName(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Name; case 1: return TradingProfile2Name; case 2: return TradingProfile3Name; case 3: return TradingProfile4Name; case 4: return TradingProfile5Name; case 5: return TradingProfile6Name; case 6: return TradingProfile7Name; default: return TradingProfile8Name; }
		}
		private string GetTradingProfileAccount(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Account; case 1: return TradingProfile2Account; case 2: return TradingProfile3Account; case 3: return TradingProfile4Account; case 4: return TradingProfile5Account; case 5: return TradingProfile6Account; case 6: return TradingProfile7Account; default: return TradingProfile8Account; }
		}
		private string GetTradingProfileAtm(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Atm; case 1: return TradingProfile2Atm; case 2: return TradingProfile3Atm; case 3: return TradingProfile4Atm; case 4: return TradingProfile5Atm; case 5: return TradingProfile6Atm; case 6: return TradingProfile7Atm; default: return TradingProfile8Atm; }
		}
		private int GetTradingProfileQuantity(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Quantity; case 1: return TradingProfile2Quantity; case 2: return TradingProfile3Quantity; case 3: return TradingProfile4Quantity; case 4: return TradingProfile5Quantity; case 5: return TradingProfile6Quantity; case 6: return TradingProfile7Quantity; default: return TradingProfile8Quantity; }
		}
		private KatTimeframe GetTradingProfileTimeframe(int idx)
		{
			switch (idx) { case 0: return TradingProfile1Timeframe; case 1: return TradingProfile2Timeframe; case 2: return TradingProfile3Timeframe; case 3: return TradingProfile4Timeframe; case 4: return TradingProfile5Timeframe; case 5: return TradingProfile6Timeframe; case 6: return TradingProfile7Timeframe; default: return TradingProfile8Timeframe; }
		}
		private int GetTradingProfileBufferTicks(int idx)
		{
			switch (idx) { case 0: return TradingProfile1BufferTicks; case 1: return TradingProfile2BufferTicks; case 2: return TradingProfile3BufferTicks; case 3: return TradingProfile4BufferTicks; case 4: return TradingProfile5BufferTicks; case 5: return TradingProfile6BufferTicks; case 6: return TradingProfile7BufferTicks; default: return TradingProfile8BufferTicks; }
		}
		private bool GetTradingProfileStopLimit(int idx)
		{
			switch (idx) { case 0: return TradingProfile1StopLimitEnabled; case 1: return TradingProfile2StopLimitEnabled; case 2: return TradingProfile3StopLimitEnabled; case 3: return TradingProfile4StopLimitEnabled; case 4: return TradingProfile5StopLimitEnabled; case 5: return TradingProfile6StopLimitEnabled; case 6: return TradingProfile7StopLimitEnabled; default: return TradingProfile8StopLimitEnabled; }
		}
		private bool GetTradingProfileEmaProtect(int idx)
		{
			switch (idx) { case 0: return TradingProfile1EmaProtectEnabled; case 1: return TradingProfile2EmaProtectEnabled; case 2: return TradingProfile3EmaProtectEnabled; case 3: return TradingProfile4EmaProtectEnabled; case 4: return TradingProfile5EmaProtectEnabled; case 5: return TradingProfile6EmaProtectEnabled; case 6: return TradingProfile7EmaProtectEnabled; default: return TradingProfile8EmaProtectEnabled; }
		}
		private bool GetTradingProfileDailyMaxDDEnabled(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxDDEnabled; case 1: return TradingProfile2DailyMaxDDEnabled; case 2: return TradingProfile3DailyMaxDDEnabled; case 3: return TradingProfile4DailyMaxDDEnabled; case 4: return TradingProfile5DailyMaxDDEnabled; case 5: return TradingProfile6DailyMaxDDEnabled; case 6: return TradingProfile7DailyMaxDDEnabled; default: return TradingProfile8DailyMaxDDEnabled; }
		}
		private double GetTradingProfileDailyMaxDD(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxDD; case 1: return TradingProfile2DailyMaxDD; case 2: return TradingProfile3DailyMaxDD; case 3: return TradingProfile4DailyMaxDD; case 4: return TradingProfile5DailyMaxDD; case 5: return TradingProfile6DailyMaxDD; case 6: return TradingProfile7DailyMaxDD; default: return TradingProfile8DailyMaxDD; }
		}
		private bool GetTradingProfileDailyMaxProfitEnabled(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxProfitEnabled; case 1: return TradingProfile2DailyMaxProfitEnabled; case 2: return TradingProfile3DailyMaxProfitEnabled; case 3: return TradingProfile4DailyMaxProfitEnabled; case 4: return TradingProfile5DailyMaxProfitEnabled; case 5: return TradingProfile6DailyMaxProfitEnabled; case 6: return TradingProfile7DailyMaxProfitEnabled; default: return TradingProfile8DailyMaxProfitEnabled; }
		}
		private double GetTradingProfileDailyMaxProfit(int idx)
		{
			switch (idx) { case 0: return TradingProfile1DailyMaxProfit; case 1: return TradingProfile2DailyMaxProfit; case 2: return TradingProfile3DailyMaxProfit; case 3: return TradingProfile4DailyMaxProfit; case 4: return TradingProfile5DailyMaxProfit; case 5: return TradingProfile6DailyMaxProfit; case 6: return TradingProfile7DailyMaxProfit; default: return TradingProfile8DailyMaxProfit; }
		}
		private bool GetTradingProfileSizing(int idx)
		{
			switch (idx) { case 0: return TradingProfile1SizingProtect; case 1: return TradingProfile2SizingProtect; case 2: return TradingProfile3SizingProtect; case 3: return TradingProfile4SizingProtect; case 4: return TradingProfile5SizingProtect; case 5: return TradingProfile6SizingProtect; case 6: return TradingProfile7SizingProtect; default: return TradingProfile8SizingProtect; }
		}
		private bool GetTradingProfileSlPull(int idx)
		{
			switch (idx) { case 0: return TradingProfile1SlPullProtect; case 1: return TradingProfile2SlPullProtect; case 2: return TradingProfile3SlPullProtect; case 3: return TradingProfile4SlPullProtect; case 4: return TradingProfile5SlPullProtect; case 5: return TradingProfile6SlPullProtect; case 6: return TradingProfile7SlPullProtect; default: return TradingProfile8SlPullProtect; }
		}
		private bool GetTradingProfileLossDca(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossDcaProtect; case 1: return TradingProfile2LossDcaProtect; case 2: return TradingProfile3LossDcaProtect; case 3: return TradingProfile4LossDcaProtect; case 4: return TradingProfile5LossDcaProtect; case 5: return TradingProfile6LossDcaProtect; case 6: return TradingProfile7LossDcaProtect; default: return TradingProfile8LossDcaProtect; }
		}
		private bool GetTradingProfileTpEarly(int idx)
		{
			switch (idx) { case 0: return TradingProfile1TpEarlyProtect; case 1: return TradingProfile2TpEarlyProtect; case 2: return TradingProfile3TpEarlyProtect; case 3: return TradingProfile4TpEarlyProtect; case 4: return TradingProfile5TpEarlyProtect; case 5: return TradingProfile6TpEarlyProtect; case 6: return TradingProfile7TpEarlyProtect; default: return TradingProfile8TpEarlyProtect; }
		}
		private bool GetTradingProfileLossTimes(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossTimesProtect; case 1: return TradingProfile2LossTimesProtect; case 2: return TradingProfile3LossTimesProtect; case 3: return TradingProfile4LossTimesProtect; case 4: return TradingProfile5LossTimesProtect; case 5: return TradingProfile6LossTimesProtect; case 6: return TradingProfile7LossTimesProtect; default: return TradingProfile8LossTimesProtect; }
		}
		private bool GetTradingProfileTiming(int idx)
		{
			switch (idx) { case 0: return TradingProfile1TimingProtect; case 1: return TradingProfile2TimingProtect; case 2: return TradingProfile3TimingProtect; case 3: return TradingProfile4TimingProtect; case 4: return TradingProfile5TimingProtect; case 5: return TradingProfile6TimingProtect; case 6: return TradingProfile7TimingProtect; default: return TradingProfile8TimingProtect; }
		}
		private int GetTradingProfileLossTimesMaxLosses(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossTimesMaxLosses; case 1: return TradingProfile2LossTimesMaxLosses; case 2: return TradingProfile3LossTimesMaxLosses; case 3: return TradingProfile4LossTimesMaxLosses; case 4: return TradingProfile5LossTimesMaxLosses; case 5: return TradingProfile6LossTimesMaxLosses; case 6: return TradingProfile7LossTimesMaxLosses; default: return TradingProfile8LossTimesMaxLosses; }
		}
		private int GetTradingProfileLossTimesLockMinutes(int idx)
		{
			switch (idx) { case 0: return TradingProfile1LossTimesLockMinutes; case 1: return TradingProfile2LossTimesLockMinutes; case 2: return TradingProfile3LossTimesLockMinutes; case 3: return TradingProfile4LossTimesLockMinutes; case 4: return TradingProfile5LossTimesLockMinutes; case 5: return TradingProfile6LossTimesLockMinutes; case 6: return TradingProfile7LossTimesLockMinutes; default: return TradingProfile8LossTimesLockMinutes; }
		}

		private bool IsTradingProfileConfigured(int idx)
		{
			string acc = GetTradingProfileAccount(idx);
			string atm = GetTradingProfileAtm(idx);
			return !string.IsNullOrWhiteSpace(acc) || !string.IsNullOrWhiteSpace(atm);
		}

		private bool IsTradingProfileActive(int idx)
		{
			if (!IsTradingProfileConfigured(idx)) return false;
			if (!string.Equals(AccountName ?? string.Empty, GetTradingProfileAccount(idx) ?? string.Empty, StringComparison.OrdinalIgnoreCase)) return false;
			string liveAtm = IsNoAtmSelection(DefaultAtmTemplate) ? string.Empty : (DefaultAtmTemplate ?? string.Empty);
			string profAtm = IsNoAtmSelection(GetTradingProfileAtm(idx)) ? string.Empty : (GetTradingProfileAtm(idx) ?? string.Empty);
			if (!string.Equals(liveAtm, profAtm, StringComparison.OrdinalIgnoreCase)) return false;
			int profQty = Math.Max(1, Math.Min(100, GetTradingProfileQuantity(idx)));
			if (DefaultQuantity != profQty) return false;
			if (DefaultTimeframe != GetTradingProfileTimeframe(idx)) return false;
			int profBuf = Math.Max(0, Math.Min(100, GetTradingProfileBufferTicks(idx)));
			if (DefaultBufferTicks != profBuf) return false;
			if (cachedIsStopLimit != GetTradingProfileStopLimit(idx)) return false;
			if (cachedIsEmaPlace != GetTradingProfileEmaProtect(idx)) return false;
			if (DailyMaxDDEnabled != GetTradingProfileDailyMaxDDEnabled(idx)) return false;
			if (Math.Abs(DailyMaxDD - GetTradingProfileDailyMaxDD(idx)) > 0.0001) return false;
			if (DailyMaxProfitEnabled != GetTradingProfileDailyMaxProfitEnabled(idx)) return false;
			if (Math.Abs(DailyMaxProfit - GetTradingProfileDailyMaxProfit(idx)) > 0.0001) return false;
			if (SizingProtectEnabled != GetTradingProfileSizing(idx)) return false;
			if (SlPullProtectEnabled != GetTradingProfileSlPull(idx)) return false;
			if (LossDcaProtectEnabled != GetTradingProfileLossDca(idx)) return false;
			if (TpEarlyProtectEnabled != GetTradingProfileTpEarly(idx)) return false;
			if (LossTimesProtectEnabled != GetTradingProfileLossTimes(idx)) return false;
			if (TimingWindowsProtectEnabled != GetTradingProfileTiming(idx)) return false;
			int profLossMax = Math.Max(1, Math.Min(20, GetTradingProfileLossTimesMaxLosses(idx)));
			if (LossTimesMaxLosses != profLossMax) return false;
			int profLock = Math.Max(1, Math.Min(1440, GetTradingProfileLossTimesLockMinutes(idx)));
			if (LossTimesLockMinutes != profLock) return false;
			return true;
		}
	}
}
