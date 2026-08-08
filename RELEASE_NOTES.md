# Release Notes — v1.67 (2026-08-08)

> Archive: v0.92 notes retained below. Current release v1.67 — see DIARY.md for full v1.00→v1.67 changelog. This file now tracks only latest verified smoke results; history lives in DIARY.md.

## Current (v1.67 2026-08-08) — smoke

| Layer | Result |
|---|---|
| xunit suite | 238/238 passed |
| CompileCheck (net48 gate) | 0 errors (0 warnings) |
| Verify-Version | v1.67 header == VERSION == UI == README == DIARY |
| Deploy | 14 files KAT\ SHA256 verified |

---

# Archive — v0.92 (2026-07-31)

Audit & hardening series v0.90 → v0.92. Three user-reported bugs fixed, then three more re-audit rounds of latent defects found and fixed. 210/210 unit tests, 0 compile-gate errors, live NT8 compile verified.

## User-reported bugs (v0.90)

### 1. Max DD / net TP OFF still auto-flattened orders
- **Root cause**: HUD toggles flipped only volatile cached flags. A script refresh/reload (or indicator restart) re-read the persisted properties (default ON), silently re-enabled protection, and could EMERGENCY FLATTEN on the next breach — matching reports after account switches and refreshes.
- **Fix**: toggles now write through to `DailyMaxDDEnabled` / `DailyMaxProfitEnabled`. Breach logic extracted to pure, testable `KatTradeCalculator.EvaluateDailyRiskBreach` (OFF = never breach).

### 2. Freeze Trail stacked duplicate SL/TP on chart (vanished on close/fill)
- **Root cause**: the ATM strategy stays alive after detach and keeps re-creating trailing stops; the 500 ms watchdog re-detached and submitted a NEW `KAT_FRZ` pair every cycle without checking the existing one — each pair under its own OCO, so two stops could both fill and flip the position. Flat-orphan cleanup removed the pile on close, matching the "disappears on fill" symptom.
- **Fix**: per-leg dedupe in `SubmitFreezeProtection` (submit only missing legs, reuse surviving OCO), plus a legacy duplicate sweep in `ReconcileFreezeQuantity` (keeps best stop/target, cancels the rest).

### 3. Bursts of platform error notifications
- **Root cause**: freeze submitted captured stop/target prices the market had already passed → guaranteed broker rejections.
- **Fix**: captured prices validated against live market side before submit (`IsStopOnValidSide` / new `IsLimitOnValidSide`); stale captures skipped.

## Re-audit findings (v0.91–v0.92)

- **15 unlocked `Account.Orders`/`Positions` reads** (Close, Flatten, CancelAll, BE, Swing SL, Revert, scale-in prep, daily risk) could throw "Collection was modified" from broker-thread mutations → all reads now locked snapshots. (v0.91)
- **Per-account baseline leak**: watchdog auto-recovery and panel account restore assigned `account` without resetting the session PnL baseline → centralized `SwitchAccount()` resets baseline + flatten guard. (v0.91)
- **Order-operation FIFO stall**: a broker pending-state hang pinned the serialized queue forever — every later op, including Close/flatten, starved. 10 s settle ceiling added (timeout-release / timeout-skip). (v0.92)
- **Daily PnL baseline poisoning**: a failed account read captured a zero baseline → phantom breach on next read. Capture now gated on read success (`ShouldCaptureSessionBaseline`). (v0.92)
- **Cross-account revert leak**: queued revert intent could fire a market order on the newly selected account → cleared on switch. (v0.92)
- **Freeze OFF mid-detach** no longer submits the static bracket. (v0.92)
- Dead/duplicate code removed (`cachedDailyPnL`, `IsAccountOperationTerminal`).

## Structure & tooling

- **Module split**: Freeze Trail and Daily Risk moved out of `KatTradeManager.OrderOps.cs` (2372 → 1966 lines) into `src/KatTradeManager.FreezeTrail.cs` and `src/KatTradeManager.DailyRisk.cs`.
- **New tools**: `scripts/Deploy-NT8.ps1` (deploy + live-recompile verification), `scripts/Run-AllChecks.ps1` (xunit + net48 compile gate one-shot).
- **Tests**: 196 → 210 (+14 regression tests across the audit series).

## Smoke test results (2026-07-31)

| Layer | Result |
|---|---|
| xunit suite | 210/210 passed |
| CompileCheck (net48 gate) | 0 errors |
| Workflow wiring (buttons/watchdog → methods) | 20/20 paths verified |
| Deploy content sync (8 source files) | identical (dest adds NT8-generated region, expected) |
| NT8 live recompile | `NinjaTrader.Custom.dll` rebuilt 22:21 local — v0.92 accepted |

## Upgrade notes

- No settings migration needed. Persisted daily-risk toggles now survive reloads — verify Max DD / Max Profit toggle state once after upgrading.
- If upgrading from ≤ v0.89 with stale `KAT_FRZ_*` orders on a chart: they are swept automatically on the next freeze reconcile, or cancelled via Close/flatten.
