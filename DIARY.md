# Project Diary & Graphify Knowledge Base

## 📊 Graphify System Architecture

```mermaid
graph TD
    A[NinjaTrader 8 Chart] --> B[KatTradeManager Indicator]
    B --> C[WPF UI Panel]
    B --> D[Multi-Timeframe BarsArray 30s / 1m / 2m]
    B --> E[Order Execution Cbi.Account]
    B --> F[Trailing Stop Loss Engine]
```

### Key Entities & Dependencies
- **Component**: `KatTradeManager` (NinjaTrader Indicator partial class)
- **Domain Logic**: `KatTradeCalculator` (price calc, trigger, order type, ATM levels, Renko, 1/2 candle)
- **ATM Parsing**: `KatAtmXmlParser` (XML template parser)
- **UI Framework**: `KatTradeManagerUI` (WPF panel partial class)
- **Execution Target**: `NinjaTrader.Cbi.Account` (`Sim301` or Active Account)
- **Supported Timeframes**: `Chart TF` (Bars 0), `30s` (Bars 1), `1m` (Bars 2), `2m` (Bars 3)
- **Special Modes**: 1/2 Candle toggle, Renko chart detection

---

## 📜 Version History & Change Log
### [v1.12] — 2026-08-04
- **Post-removal re-audit: dead-code purge after v1.07–v1.11 feature cuts**:
  - **ATM XML Quantity**: `AtmTemplateData.Quantity` + `EntryQuantity`/bracket-`Quantity` parsing deleted (no production consumer since the Contracts row was removed in v1.07). Parser now extracts only SL/TP/BE/trail levels. 6 quantity-only tests deleted, quantity asserts stripped from 6 more.
  - **Open/Close orphans**: `EmaTouchBarInfo.Open/Close`, `CandleBarInfo.Open/Close` struct fields, `ema34/89TouchOpen/Close` arrays, `cachedCurrentOpen`/`cachedPrevOpen`/`cachedPrevClose` price arrays, and the `openCache`/`closeCache` params of `UpdateEmaTouchCache` all deleted — no readers remained after `CalculateCandlePrice(action, high, low)` simplification. `cachedCurrentClose` kept (`GetSwingValidationPrice` fallback).
  - **`FindLastEmaTouchBar`** (test-only dead calculator function, documented as such since v0.x) deleted with its 3 tests; production touch scanning uses `IsEmaTouchBar` directly.
  - **Rename**: `KatRenkoAndHalfCandleTests` → `KatRenkoAndOrderTypeTests` (half-candle tests gone).
  - **Verified intact**: `cachedTfIndex`/`DefaultTimeframe` still drive `GetBarsInProgressIndex`; `isRenkoChart` kept as startup diagnostic only; EMA Place validation under `priceLock` intact; MERGE gates correct without freeze; module split unchanged (OrderOps cohesive). No functional regressions found.
  - **Tests**: 170 → **163 passing**. Compile gate 0 errors.
  - **Graphify entity mapping**: `KatAtmXmlParser.ParseXmlDocument` (levels only), `KatTradeManager.UpdateEmaTouchCache` (slimmed), `KatTradeManager.OnBarUpdate` (slimmed caches), `KatTradeCalculator` (FindLastEmaTouchBar removed).

### [v1.11] — 2026-08-04
- **Close/flatten double height + HUD→Chart Trader account sync**:
  - `Close/flatten` button height doubled (33 → 66px) for a bigger flatten target.
  - Selecting an account in the HUD now also selects it in Chart Trader's own account selector (`SyncChartTraderAccount`): the selector is located by scanning Chart Trader's visual-tree ComboBoxes for the account name (layout-resilient), then `SelectedItem` is set so NT8 renders that account's orders on the chart. Sync runs on explicit HUD selection only (not on watchdog rebuilds) and fails soft with an Output log line.
  - **Graphify entity mapping**: `KatTradeManagerUI.SyncChartTraderAccount`, `KatTradeManagerUI.CreateWpfControls` (acc selector handler, btnClose height).

### [v1.10] — 2026-08-04
- **Removed Partial Candle, EMA Angle, and Freeze Trail features; Max DD forced ON per session**:
  - **Partial Candle**: toggle button, `cachedIsPartialCandle`/`cachedPartialPercent`, `DefaultPartialCandlePercent` property, `CalculatePartialCandlePrice`/`CalculateHalfCandlePrice` deleted. `CalculateCandlePrice` simplified to `(action, high, low)` — candle orders always anchor at full High/Low. All 5 call sites updated.
  - **EMA Angle**: toggle button, `cachedIsEmaAngle`, angle series/caches (`emaAngleFilterSeries`, `cachedEmaAngleCurrent/Previous`), 12 `EmaAngle*` indicator properties, `CalculateEmaAngle`/`ValidateEmaAngle`, and the Validation-2 block in `PlaceOrderInternal` deleted. EMA Place filter remains.
  - **Freeze Trail**: entire `src/KatTradeManager.FreezeTrail.cs` partial deleted (ATM detach, KAT_FRZ static exits, quantity reconcile, orphan cleanup), plus HUD button, watchdog hook, `cachedIsFreezeTrail`, freeze-only calculator helpers (`IsPreferredFreezePrice`, `ShouldAdjustFreezeQuantity`, `ShouldCancelFreezeOrphans`, `ShouldSubmitFreezeLeg`, `IsLimitOnValidSide`), MERGE freeze-gates, and `freezeDetachInFlight` queue reset. Deploy script now sweeps the stale file from NT8; CompileCheck csproj updated (7 files).
  - **Max DD**: always starts ON every session — `State.DataLoaded` forces `DailyMaxDDEnabled = true` before caching; the in-session toggle still persists but never survives a reload. Max Profit persistence unchanged.
  - **HUD toggle section** now 4 buttons / 2 rows: `Stop-Limit | Ema place`, `Max DD | Max Profit`.
  - **Tests**: 222 → **170 passing** (deleted `KatFreezeTrailTests.cs`, angle/partial/half-candle tests across 4 files; remaining candle-price tests updated to the new signature). Compile gate 0 errors.
  - **Graphify entity mapping**: `KatTradeCalculator.CalculateCandlePrice` (simplified), `KatTradeManager.OrderOps.PlaceOrderInternal` (EMA Place only), `KatTradeManagerUI.CreateWpfControls` (4-button toggle card), `KatTradeManager.OnStateChange` (Max DD force-ON), `Deploy-NT8.ps1` (stale sweep).

### [v1.09] — 2026-08-04
- **HUD layout reorganization (execution vs toggles)**:
  - All ON/OFF toggle buttons (Partial Candle, Ema place/angle, Max DD, Max Profit, Freeze Trail, Stop-Limit) moved into one dedicated toggle section at the bottom of the HUD.
  - Freeze Trail + Stop-Limit now share one row (side-by-side half-width buttons) instead of two full-width rows.
  - Execution section (single card) order top→bottom: BUY/SELL market row, Entry-candle shift row, BUY/SELL current + previous rows, BE | Revert row, Close/flatten. Market buttons moved above the entry-candle shift row; BE/Revert/Close moved directly below BUY/SELL previous.
  - README updated (Freeze Trail / Stop-Limit bullet placement wording).
  - **Graphify entity mapping**: `KatTradeManagerUI.CreateWpfControls` (section 3 execution card, section 4 toggle card, `freezeStopGrid`).

### [v1.08] — 2026-08-04
- **HUD: full-width account dropdown + ATM Bracket permanently MERGE**:
  - Account selector is now a full-width row (same layout as the ATM dropdown); the `Acc:` label and its 2-column `paramGrid` wrapper were removed along with the now-orphaned `AddGridRow` helper.
  - Removed the `ATM Bracket: MERGE/SPLIT` toggle button and the `cachedIsAtmMerge` flag. Bracket merging is now unconditional: `SubmitOrder` scale-in path, `ScheduleAtmBracketMerge`, `MergeAtmBrackets`, `ProcessAtmScaleInUpdate`, and the account-order-update diagnostics all run as if MERGE were always ON.
  - README updated (ATM Bracket bullet rewritten as always-on).
  - **Graphify entity mapping**: `KatTradeManagerUI.CreateWpfControls` (acc selector full-width, merge button removed), `KatTradeManager.SubmitOrder`, `KatTradeManager.ScheduleAtmBracketMerge`, `KatTradeManager.MergeAtmBrackets`, `KatTradeManager.ProcessAtmScaleInUpdate`, `KatTradeManager.OnAccountOrderUpdateCore`.

### [v1.07] — 2026-08-04
- **HUD slim-down: removed Contracts row, single-line status, removed Buy/Sell distance feature**:
  - Removed the `Contracts:` input row from the HUD and its ATM quantity sync (`LoadAtmTemplateSettings` no longer reads ATM `<EntryQuantity>` into the HUD). Order quantity now comes solely from the `Default Quantity` indicator property; removed orphaned `txtQuantity`, `cachedQuantity`, and `atmQuantity` fields.
  - HUD status slot reduced from a reserved 2-line (32px, wrapping) area to a single 16px line with `TextTrimming.CharacterEllipsis`.
  - Removed the fixed-distance feature entirely: `BUY +distance` / `SELL -distance` HUD buttons, `HotkeyBuyDist`/`HotkeySellDist` hotkeys, `DefaultDistanceTicks` property, `PlaceFixedDistanceOrder`, `cachedDistanceTicks`, and `KatTradeCalculator.CalculateFixedDistanceTriggerPrice` (+7 orphaned unit tests across 4 test files).
  - README updated (hotkey count 15→13, Stop-Limit/EMA filter route wording, status slot description).
  - **Graphify entity mapping**: `KatTradeManagerUI.CreateWpfControls` (Contracts row/distance buttons removed), `KatTradeManagerUI.SyncCachedValues`, `KatTradeManager.LoadAtmTemplateSettings` (qty sync removed), `KatTradeManager.OrderOps.PlaceOrderInternal` (sole candle/EMA entry path).

### [v1.06] — 2026-08-03
- **ATM MERGE defer log once per episode instead of per account event**:
  - Deferring flat cleanup while our ATM entry is still working (until filled/cancelled) is correct behavior; but the defer branch printed on every account order event (~2/sec), flooding the NinjaScript Output.
  - `MergeAtmBrackets` now logs the defer line once per episode (`atmDeferLoggedStartup` keyed by `atmStartupOrder` reference); flag reset in `ClearAtmStartup` and `ResetAtmScaleInTracking`.
  - Verified: compile gate 0 errors, 222/222 unit tests passing.
  - **Graphify entity mapping**: `KatTradeManager.OrderOps.MergeAtmBrackets`, `KatTradeManager.OrderOps.ClearAtmStartup`.

### [v1.05] — 2026-08-03
- **Removed all UI-thread series reads (root cause of dead Buy/Sell buttons) + stopped ATM MERGE log spam**:
  - Runtime evidence: `System.ArgumentOutOfRangeException: 'barsAgo' needed to be between 0 and 6001 but was 41` thrown by `Times[barIdx][barsAgo]` inside `PlaceEmaOrder` — NT8 series indexers are only safe on the data thread (v0.11 lesson; regressed when v1.00–v1.03 added shift-state timestamp lookups + UI-thread fallback scans). The exception aborted the handlers BEFORE `PlaceOrderInternal`, so no order was ever submitted.
  - `KatTradeManager.cs` (data thread, `OnBarUpdate`): added `cachedCurrentBarTime`/`cachedPrevBarTime`, `ema34TouchTime`/`ema89TouchTime`, and per-series snapshot lists `ema34TouchLists`/`ema89TouchLists`/`candleBarLists` (rebuilt under `priceLock`, reference-swapped so UI thread reads immutable snapshots).
  - `KatTradeManager.OrderOps.cs`: `PlaceOrder`, `PlaceEmaOrder`, `ShiftEmaEntry`, `ShiftCandleEntry` now read only cached snapshots; deleted the UI-thread `Highs/Lows/Opens/Closes/Times/EMA` fallback scans.
  - `MergeAtmBrackets`: skip the defer branch when no ATM episode ever happened (`atmLastLifecycleActivityUtc == DateTime.MinValue`) — previously every account order event printed "ATM MERGE flat cleanup deferred" forever.
  - Verified: compile gate 0 errors, 222/222 unit tests passing.
  - **Graphify entity mapping**: `KatTradeManager.UpdateEmaTouchCache`, `KatTradeManager.OnBarUpdate`, `KatTradeManager.OrderOps.PlaceEmaOrder`, `KatTradeManager.OrderOps.MergeAtmBrackets`.

### [v1.04] — 2026-08-03
- **Compile-Error Hotfix: removed nonexistent `OrderState.PendingSubmit` so NT8 can actually load the v1.03 order-flow fixes**:
  - Root cause of "Buy/Sell previous & last 34/89 place no orders": v1.03 referenced `OrderState.PendingSubmit`, a member that does not exist in NinjaTrader's `Cbi.OrderState` enum. NT8's NinjaScript compiler rejected the whole source, silently kept the last good `NinjaTrader.Custom.dll` (v1.02), so none of the v1.02/v1.03 order-path fixes (previous-candle price fallback, dynamic EMA touch scan, submit-queue eligibility, HUD diagnostics) ever ran.
  - Removed `|| order.OrderState == OrderState.PendingSubmit` from `IsAccountOperationEligible` in `src/KatTradeManager.OrderOps.cs` (state unreachable in NT8 anyway).
  - Verified with local compile gate (0 errors) and 222/222 unit tests passing; redeployed all sources to `Indicators\KAT`.
  - **Graphify entity mapping**: `KatTradeManager.OrderOps.IsAccountOperationEligible`.

### [v1.03] — 2026-08-03
- **Order Submit Queue Eligibility & Dynamic EMA Touch Scan Restoration**:
  - Expanded `IsAccountOperationEligible(AccountOperationType.Submit, order)` in `KatTradeManager.OrderOps.cs` to allow non-terminal active states (`Initialized`, `Submitted`, `Accepted`, `AcceptedByRisk`, `Working`, `PendingSubmit`, `TriggerPending`). Prevents queued ATM Strategy orders from being silently skipped/dequeued when NinjaTrader updates order state prior to pump dispatch.
  - Added dynamic historical bar fallback scan for `PlaceEmaOrder` (`BUY/SELL last 34`, `BUY/SELL last 89`) when cached touch index is `-1`, ensuring orders are placed even if `OnBarUpdate` cache is unpopulated.
  - Added visual HUD status notifications (`ShowHudStatus`) on order placement and failure.
  - Added unit test `FindLastEmaTouchBar_ScansAndFindsTouchCandle` in `KatTradeCalculatorTests.cs` (222/222 unit tests passing).
  - **Graphify entity mapping**: `KatTradeManager.OrderOps`, `KatTradeManager.IsAccountOperationEligible`, `KatTradeCalculator.FindLastEmaTouchBar`, `KatTradeManager.Tests.KatTradeCalculatorTests`.

### [v1.02] — 2026-08-03
- **Entry Shift Timestamp Boundary Fix & Previous Candle Order Fallback**:
  - Corrected `CurrentBars` index boundary checks from `<` to `<=` across all 4 timestamp lookup sites in `KatTradeManager.OrderOps.cs` (`PlaceOrder`, `PlaceEmaOrder`, `ShiftEmaEntry`, `ShiftCandleEntry`). Fixed bug where `barsAgo == CurrentBars` returned `DateTime.MinValue`.
  - Added fallback previous candle price lookup (`Highs[barIdx][1]`, `Lows[barIdx][1]`, `Opens[barIdx][1]`, `Closes[barIdx][1]`) inside `lock (priceLock)` in `PlaceOrder` when `cachedPrevHigh` is unpopulated.
  - Fixed swapped button background colors for `BUY previous`/`current` and `SELL previous`/`current` in `KatTradeManagerUI.cs`.
  - Added HUD visual status alerts (`ShowHudStatus`) when `PlaceOrder` aborts due to missing price data or filter rejections.
  - Added unit test `CalculateShiftedBarIndex_MaxBarsAgoBoundary_MatchesOldestBarTimestamp` in `KatEntryShiftTests.cs` (221/221 unit tests passing).
  - **Graphify entity mapping**: `KatTradeManager.OrderOps`, `KatTradeManagerUI`, `KatTradeCalculator.CalculateShiftedBarIndex`, `KatTradeManager.Tests.KatEntryShiftTests`.

### [v1.01] — 2026-08-03
- **Entry Shift Domain Modularization & Comprehensive Testing**:
  - Extracted pure calculation logic `CalculateShiftedBarIndex` into [`KatTradeCalculator.cs`](file:///c:/Users/kieuanhtuan/Documents/all.%20Coding/nt8-kat-TradeManager/src/KatTradeCalculator.cs).
  - Added dedicated unit test suite [`KatEntryShiftTests.cs`](file:///c:/Users/kieuanhtuan/Documents/all.%20Coding/nt8-kat-TradeManager/tests/KatTradeManager.Tests/KatEntryShiftTests.cs) covering forward/backward index shifting, timestamp matching across live bar arrivals, boundary condition handling (`REACHED_NEWEST`, `REACHED_OLDEST`), and fallback index handling (220/220 unit tests passing).
  - **Graphify entity mapping**: `KatTradeCalculator.CalculateShiftedBarIndex`, `KatTradeManager.Tests.KatEntryShiftTests`.

### [v1.00] — 2026-08-03
- **Entry Shift Controls Re-Audit & Polishing (`v1.00` Milestone)**:
  - Guarded historical series index checks with `CurrentBars[barIdx]` across all time-based timestamp lookups.
  - Verified 100% thread-safe `priceLock` isolation and zero bar-drift behavior across both EMA 89/34 and Candle shift modes.
  - **Graphify entity mapping**: `KatTradeManager.OrderOps` (`ShiftEmaEntry`, `ShiftCandleEntry`).

### [v0.99] — 2026-08-03
- **Entry Candle Shift Buttons (`◀ Entry candle` & `Entry candle ▶`)**:
  - Added WPF candle entry shift control panel directly above Buy/Sell current/previous buttons in Section 3, styled identically to SL moving buttons (dark background `#141414`, height 33, font size 12).
  - Records session state for active Candle entry order: `hasCandleOrder`, `lastCandleOrderAction` (Buy/Sell), and `lastCandleBarTime`.
  - Moving back (`◀ Entry candle`) shifts entry price to older candles infinitely back in chart history; moving forward (`Entry candle ▶`) shifts entry price to newer candles towards current time (stopping at current candle `barsAgo = 0`).
  - Thread-safe series scanning under `priceLock` and timestamp matching (`lastCandleBarTime`) to prevent bar drift as new candles form.
  - Automatic Stop-to-Limit conversion via `DetermineOrderType` if price has run past target entry price.
  - **Graphify entity mapping**: `KatTradeManagerUI` (`candleShiftGrid`, `btnCandleBack`, `btnCandleRedo`), `KatTradeManager.OrderOps` (`ShiftCandleEntry`, `CandleBarInfo`, `hasCandleOrder`, `lastCandleOrderAction`, `currentCandleBarsAgo`, `lastCandleBarTime`).

### [v0.98] — 2026-08-03
- **Entry 89/34 Shift Buttons Audit & Refactoring (`◀ Entry 89/34` & `Entry 89/34 ▶`)**:
  - Thread-safe historical series scanning: wrapped EMA touch bar scan inside `lock (priceLock)` to prevent data thread race conditions during bar updates.
  - Bar Timestamp Matching (`lastEmaTouchBarTime`): tracks exact candle timestamp of active entry order, preventing index drift when new bars arrive on chart.
  - Enhanced HUD feedback: shows target candle `bar #`, `orderType` (Stop vs Limit), and exact `triggerPrice`.
  - **Graphify entity mapping**: `KatTradeManager.OrderOps` (`ShiftEmaEntry`, `EmaTouchBarInfo`, `lastEmaTouchBarTime`).

### [v0.97] — 2026-08-03
- **Entry 89/34 Shift Buttons (`◀ Entry 89/34` & `Entry 89/34 ▶`)**:
  - Added WPF entry shift control panel directly above `SELL last 34` / `BUY last 34` buttons in Section 2, styled identically to SL moving buttons (dark background `#141414`, height 33, font size 12).
  - Records session state for active EMA entry order: `lastEmaOrderPeriod` (34 or 89) and `lastEmaOrderAction` (Buy or Sell).
  - Moving back (`◀ Entry 89/34`) shifts entry price to older EMA touch candles in chart history; moving forward (`Entry 89/34 ▶`) shifts entry price to newer EMA touch candles towards current time.
  - Automatic Stop-to-Limit conversion: evaluates target entry price against current market price (`DetermineOrderType`), automatically converting StopMarket to Limit order when price has passed the entry.
  - Cancels active working entry order before placing the shifted order.
  - **Graphify entity mapping**: `KatTradeManagerUI` (`entryShiftGrid`, `btnEntryBack`, `btnEntryRedo`), `KatTradeManager.OrderOps` (`ShiftEmaEntry`, `CancelWorkingEntryOrders`, `lastEmaOrderPeriod`, `lastEmaOrderAction`, `currentEmaTouchIndex`).

### [v0.95] — 2026-07-31
- **ATM Quick Set buttons (A–F) distribution fix**:
  - Replaced asymmetric left margin grid column distribution with an 11-column Grid layout using 5 explicit 2px fixed column spacers and 0-margin buttons.
  - Guarantees 100% uniform 2px gaps between all 6 buttons without floating-point layout rounding drift between button 3 and 4.
  - **Graphify entity mapping**: `KatTradeManagerUI` (`atmSetGrid`).

### [v0.94] — 2026-07-31
- **ATM Quick Set buttons (A–F)**:
  - Row of 6 one-click buttons directly below the HUD ATM dropdown; each instantly selects its assigned ATM template (equivalent to picking it from the dropdown — the dropdown updates to match).
  - Exactly one button shows amber ON state — the one whose assigned ATM equals the current selection; the rest render the standard OFF gray; ATM `None` turns all OFF. Manual dropdown changes re-sync the buttons through `ApplyAtmSelection`.
  - 12 new persisted settings in group "ATM Quick Sets": per-set button label (text, normalized to max 3 chars with letter fallback via `KatTradeCalculator.NormalizeAtmSetName`) and per-set ATM template (standard-values dropdown via `AtmTemplateNameConverter`). Defaults: labels A–F, no ATM assigned (click shows HUD status hint).
  - Unassigned or deleted-template clicks surface a HUD status warning instead of silently doing nothing.
- **Validation**: 214/214 tests passing (+4 quick-set name normalization tests); CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `KatTradeManagerUI.ApplyAtmSetSelection`, `KatTradeManagerUI.UpdateAtmSetButtons`, `KatTradeManagerUI.GetAtmSetTemplate`, `KatTradeManagerUI.GetAtmSetName`, `KatTradeManager.AtmSet1Name`–`AtmSet6Name`, `KatTradeManager.AtmSet1Atm`–`AtmSet6Atm`, `KatTradeCalculator.NormalizeAtmSetName`, `KatAtmQuickSetTests`.
### [v0.93] — 2026-07-31
- **Idle-time "Index was outside the bounds of the array" dialog fix**:
  - NT8 trace evidence (`trace.20260731`, 03:25:42): `System.IndexOutOfRangeException` attributed to `ScheduleAtmBracketMerge` from `OnPanelWatchdogTick` via `DispatcherTimer.FireTick` — escaping every inner try/catch, so it was thrown inside a guard-clause NT8 property getter (`Instrument` indexes Bars internally) during overnight session maintenance (hourly HdsClient reconnects / token renewals). Release-build line numbers in the trace were misattributed; the boundary was the real hole.
  - `OnPanelWatchdogTick` now wraps its whole body in a boundary catch — one bad tick logs and retries 500 ms later instead of popping an unhandled-exception dialog or killing the timer.
  - `OnAccountOrderUpdate` (broker event thread) got the same boundary catch, core logic extracted to `OnAccountOrderUpdateCore`.
  - All other event entry points (`OnBarUpdate`, queue pump, merge, button handlers) already had catches — these two were the only unprotected boundaries.
- **Validation**: 210/210 tests passing; CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings). Handlers are NT8-runtime-bound (DispatcherTimer/OrderEventArgs/Instrument) and cannot be instantiated in the xunit sandbox; verified via full suite + compile gate + structural boundary check.
- **Graphify entity mapping**: `KatTradeManagerUI.OnPanelWatchdogTick`, `KatTradeManager.OnAccountOrderUpdate`, `KatTradeManager.OnAccountOrderUpdateCore`, `KatTradeManager.ScheduleAtmBracketMerge`.
### [v0.92] — 2026-07-31
- **Re-audit rounds 3+4 — FIFO stall ceiling, PnL baseline poisoning, cross-account revert, freeze OFF mid-detach**:
  - **Account-operation FIFO stall (round 3)**: a broker state stuck pending (e.g. `ChangePending`/`CancelPending` hang) pinned the serialized queue forever — every later submit/change/cancel, including Close/flatten, starved behind it with no escape. Added a 10 s ceiling: the active operation is timeout-released and a stalled queue head is timeout-skipped (both logged), so the queue always drains.
  - **Daily PnL baseline poisoning (round 3)**: a failed `account.Get(GrossRealizedProfitLoss)` read fell into the catch with `currentRealizedPnL = 0`, and that zero was captured as the session baseline — the next successful read then reported the entire account realized PnL as today's, a phantom breach (or phantom recovery). Baseline capture now requires a successful read (`KatTradeCalculator.ShouldCaptureSessionBaseline`); failed reads contribute zero daily realized instead of corrupting state.
  - **Cross-account revert leak (round 3)**: a queued revert intent (`pendingRevertAction`) survived account switches and could fire a market order on the NEW account. `SwitchAccount` now clears pending revert action/quantity.
  - **Freeze OFF mid-detach (round 4)**: toggling Freeze OFF while the detach cancel was still in flight no longer submits the static KAT_FRZ bracket — the user asked for ATM behavior again, so `SubmitFreezeProtection` guards on `cachedIsFreezeTrail`.
- **Validation**: 210/210 tests passing (+3 session-baseline gate tests); CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `KatTradeManager.TryCompleteActiveAccountOperation`, `KatTradeManager.PumpAccountOperationQueue`, `KatTradeManager.SwitchAccount`, `KatTradeManager.CalculateDailyPnL`, `KatTradeManager.SubmitFreezeProtection`, `KatTradeCalculator.ShouldCaptureSessionBaseline`, `KatDailyRiskTests`.
### [v0.91] — 2026-07-31
- **Re-audit round 2 — account collection race hardening**:
  - 15 sites enumerated `Account.Orders` / `Account.Positions` without a lock (Close, Flatten, CancelAll, BE, Swing SL, Revert, scale-in prep, daily risk); NT8 broker-thread mutations could throw "Collection was modified" mid-enumeration and surface as random error spam or silently skipped logic. v0.88 only hardened MERGE.
  - All reads now go through locked snapshots: `GetInstrumentPosition()`, `GetAccountOrdersSnapshot()`, `GetAccountPositionsSnapshot()`. Freeze/MERGE paths keep their existing explicit locks.
- **Per-account daily-risk baseline centralization (bug #1 variant)**:
  - `SwitchAccount()` is now the single account-change point — resets the session PnL baseline and the flatten guard, then re-subscribes order events.
  - Watchdog auto-recovery, saved-account restore, and first-allowed defaulting previously assigned `account` directly without resetting the baseline, so a previous account's realized PnL could phantom-breach (or blind) daily risk on the new account. Only the HUD SelectionChanged handler reset it before.
- **Dead/duplicate code removal**: deleted write-only `cachedDailyPnL`; collapsed duplicate `IsAccountOperationTerminal` into `IsTerminalOrderState`.
- **Validation**: 207/207 tests passing; CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `KatTradeManager.GetInstrumentPosition`, `KatTradeManager.GetAccountOrdersSnapshot`, `KatTradeManager.GetAccountPositionsSnapshot`, `KatTradeManager.SwitchAccount`, `KatTradeManager.IsDailyRiskBreached`, `KatTradeManagerUI.OnPanelWatchdogTick`, `KatTradeManagerUI.CreateWpfControls`.
### [v0.90] — 2026-07-31
- **Daily-risk toggles persist (bug: OFF silently re-enabled)**:
  - HUD Max DD / Max Profit toggles used to flip only the volatile cached flags; a script refresh/reload re-read the persisted properties (default ON) and could EMERGENCY FLATTEN on the next breach, especially after account switches or refreshes.
  - Toggles now write through to `DailyMaxDDEnabled` / `DailyMaxProfitEnabled`, matching the AccountName / DefaultAtmTemplate persistence pattern.
  - Breach gate extracted to pure `KatTradeCalculator.EvaluateDailyRiskBreach`; OFF means never breached, zero/negative limit means disabled (legacy semantics preserved).
- **Freeze Trail duplicate SL/TP stack fix (bug: chart littered with overlapping KAT_FRZ pairs)**:
  - Root cause: the ATM strategy stays alive after detach and keeps re-creating trailing stops; every 500 ms watchdog re-detach submitted a NEW KAT_FRZ pair without checking the existing one — each pair under its own OCO, so two stops could both fill and flip the position. Pairs vanished on close/fill via the flat-orphan cleanup, matching the reported symptom.
  - `SubmitFreezeProtection` now dedupes per leg against active frozen exits, only submits missing legs, and links mixed old/new pairs under the surviving leg's OCO.
  - `ReconcileFreezeQuantity` sweeps legacy stacked duplicates: keeps the single best stop/target leg, cancels the rest.
- **Broker-reject spam fix (bug: bursts of platform error notifications)**:
  - Captured freeze stop/target prices are validated against the live market side before submit (`IsStopOnValidSide` / new `IsLimitOnValidSide`); prices the market already passed are skipped instead of submitted into guaranteed broker rejections.
- **Module split**: Freeze Trail and Daily Risk regions moved out of `KatTradeManager.OrderOps.cs` (2372 → 1966 lines) into new partials `src/KatTradeManager.FreezeTrail.cs` and `src/KatTradeManager.DailyRisk.cs`; CompileCheck and deploy list updated.
- **Tools**: added `scripts/Deploy-NT8.ps1` (deploy + live-recompile verification) and `scripts/Run-AllChecks.ps1` (xunit + net48 compile gate one-shot).
- **Validation**: 207/207 tests passing (+11 new: breach gate matrix, freeze leg dedupe, limit-side validation); CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `KatTradeCalculator.EvaluateDailyRiskBreach`, `KatTradeCalculator.IsLimitOnValidSide`, `KatTradeCalculator.ShouldSubmitFreezeLeg`, `KatTradeManager.SubmitFreezeProtection`, `KatTradeManager.ReconcileFreezeQuantity`, `KatTradeManager.IsDailyRiskBreached`, `KatTradeManagerUI.CreateWpfControls` (toggle persistence), `KatDailyRiskTests`, `KatFreezeTrailTests`.
### [v0.89] — 2026-07-30
- **Freeze Trail v2 — ATM detach / HUD takeover** (replaces price-lock enforcement):
  - Freeze ON now cancels every ATM-owned protective exit of the instrument and submits one static `KAT_FRZ_SL` (+ OCO `KAT_FRZ_TP` when a target existed) at the tightest captured stop / farthest captured target, sized to live position quantity.
  - Watchdog keeps detaching newly appearing ATM brackets, so freeze covers 2nd+ entries with independent ATMs and Chart Trader ATMs.
  - Removed all stop-price re-pushing (`frozenStopPrice`, `lastFreezeEnforceTime`, `CheckFreezeTrailEnforcement`, `KatTradeCalculator.CalculateFrozenStopLimitPrice`): BE, Swing SL, and chart SL drags are no longer reverted.
  - Quantity-only reconciliation for scale-in/scale-out; static exits are cancelled after the position stays flat past the ATM lifecycle grace window.
  - MERGE reconciliation is gated off while freeze is ON to avoid two owners of the same orders.
- **ATM `None` support**:
  - HUD ATM dropdown gains `None` as first item (also the fallback when the saved template is missing), clearing `cachedAtmTemplate` so entries submit natively without an ATM.
  - ATM MERGE scheduling/reconciliation now requires an active HUD ATM template, so None-mode Chart Trader orders are never merged, resized, or cancelled by the HUD.
- **Validation**: 196/196 tests passing; CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `KatTradeManager.ProcessFreezeTrail`, `KatTradeManager.DetachAtmProtection`, `KatTradeManager.SubmitFreezeProtection`, `KatTradeManager.ReconcileFreezeQuantity`, `KatTradeManager.CancelFreezeOrphans`, `KatTradeManager.IsHudAtmActive`, `KatTradeManagerUI.ApplyAtmSelection`, `KatTradeCalculator.IsPreferredFreezePrice`, `KatTradeCalculator.ShouldAdjustFreezeQuantity`, `KatTradeCalculator.ShouldCancelFreezeOrphans`, `KatFreezeTrailTests`.
### [v0.88] — 2026-07-30
- **ATM merge collection-race hardening**:
  - Locks `Account.Positions` and `Account.Orders` while taking the merge snapshot, preventing NT8 broker-thread mutations from corrupting LINQ enumeration.
  - Adds an outer dispatcher callback guard so an unexpected collection exception cannot escape the HUD watchdog as an unhandled UI exception.
- **Validation**: 194/194 tests passing; CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `KatTradeManager.ScheduleAtmBracketMerge`, `KatTradeManager.MergeAtmBrackets`.
### [v0.87] — 2026-07-30
- **Indicator settings and HUD lifecycle**:
  - Account Name now uses NinjaTrader `AccountNameConverter`, exposing connected accounts as standard property-grid choices while preserving serializable string settings.
  - Default ATM Template now scans sorted `templates\AtmStrategy\*.xml` names through a standard-values converter/editor.
  - Runtime account and ATM selectors honor saved settings and write user selections back to persisted properties.
  - Show Control Panel visibility gate now runs before account operations, risk checks, hotkeys, drag handlers, or HUD creation; hidden HUD teardown is idempotent.
- **Validation**: 194/194 tests passing; CompileCheck: 0 errors (existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `AtmTemplateNameConverter`, `KatTradeManagerUI.OnPanelWatchdogTick`, `KatTradeManagerUI.CreateWpfControls`, `KatTradeManagerUI.RemoveWpfControls`, `KatTradeManager.SelectAccount`.
### [v0.86] — 2026-07-30
- **ATM protective bracket lifecycle hardening** (2026-07-30 08:46 UTC):
  - MERGE no longer cancels SL/TP during short NT8 gaps where `Entry` is terminal but `Account.Positions` still reports Flat.
  - Tracks ATM Entry, scale-in, and protective-order callbacks; defers flat cleanup for 3 seconds after recent lifecycle activity.
  - Preserves first-entry startup protection through terminal-entry callbacks and records confirmed-position episodes across scale-out.
  - Added regression coverage for terminal-entry propagation, transient scale-out Flat snapshots, and stale-flat cleanup.
- **Validation**: 194/194 tests passing; CompileCheck: 0 errors (134 existing NT8 reference-conflict/obsolete warnings).
- **Graphify entity mapping**: `KatTradeManager.IsAtmStartupPending`, `KatTradeManager.ProcessAtmStartupUpdate`, `KatTradeManager.MergeAtmBrackets`, `KatTradeManager.OnAccountOrderUpdate`, `KatTradeCalculator.ShouldDeferAtmFlatCleanup`, `KatOrderLifecycleTests`.
### [v0.85] — 2026-07-29
- **Buy/Sell HUD ordering and visual sizing**:
  - Buy/Sell `current` buttons now appear above corresponding `previous` buttons.
  - Current buttons inherit previous buttons' former colors; previous buttons inherit current buttons' former colors.
  - Current buttons now match previous buttons at `48px` height and `12px` font size.
- **Validation**: 191/191 tests passing; CompileCheck: 0 errors (existing NT8 reference-conflict warnings).
- **Graphify entity mapping**: `KatTradeManagerUI.CreateWpfControls`, `KatTradeManagerUI.CreateButton`, `KatTradeManager.PlaceOrder`.
### [v0.84] — 2026-07-28
- **Account-wide Close/flatten**:
  - Close/flatten button and hotkey now clear the entire selected account, not only chart instrument position.
  - Cancels all active orders first, including pending/working entry and ATM orders, then submits one market close per non-flat account position across every instrument.
  - Clicking Close while account is flat but has pending orders now still performs cancellation.
- **Multi-position safety**:
  - Tracks every generated `KAT_CLOSE` until all close orders reach terminal state; first filled position cannot unlock duplicate flatten clicks early.
  - Pending Revert intent is cleared and Revert retries are blocked while any account-wide close remains active.
- **Regression coverage**: Added account flatten work/no-op predicate tests. Suite: 191/191 passing; CompileCheck: 0 errors (134 existing warnings).
- **Graphify entity mapping**: `KatTradeManager.FlattenAllPositions`, `KatTradeManager.SubmitQueuedFlattenAll`, `KatTradeManager.CancelAllOrders`, `KatTradeManager.IsAccountCloseInFlight`, `KatTradeManagerUI.CreateWpfControls`, `KatTradeManagerUI.OnChartPreviewKeyDown`, `KatTradeCalculator.ShouldFlattenAccount`.
### [v0.83] — 2026-07-28
- **Close/flatten queue recovery**:
  - ATM `StartAtmStrategy` requests now release serialized queue ownership after the API call returns instead of waiting for ATM-managed entry states that can remain `Initialized`/`Submitted`.
  - This prevents first-entry ATM lifecycle state from blocking later cancellation and `KAT_CLOSE` submission.
- **First-entry ATM bracket protection**:
  - MERGE flat cleanup now defers while tracked first ATM entry startup remains non-terminal.
  - Startup tracking clears on terminal entry updates, confirmed non-flat position, account detach, or submit failure.
  - Initial ATM SL/TP orders remain intact during position-confirmation timing; stale flat cleanup remains active after startup resolves.
- **Regression coverage**: Added pure startup/flat-cleanup gate tests. Suite: 190/190 passing; CompileCheck: 0 errors (133 existing warnings).
- **Graphify entity mapping**: `KatTradeManager.IsAccountOperationSettled`, `KatTradeManager.TrackAtmStartup`, `KatTradeManager.IsAtmStartupPending`, `KatTradeManager.MergeAtmBrackets`, `KatTradeManager.OnAccountOrderUpdate`, `KatTradeCalculator.ShouldDeferAtmFlatCleanup`.
### [v0.82] — 2026-07-28
- **Serialized account-operation gate**:
  - Added FIFO `Submit` / `Change` / `Cancel` queue with one active account mutation at a time.
  - Dispatcher-safe pump retries pending platform states and releases operations after state settlement.
  - Overlapping order/OCO requests coalesce or defer instead of mutating the same order concurrently.
  - Added operation diagnostics with type, reason, order ID, OCO, and quantity.
- **Close/flatten sequencing**:
  - Close now queues cancellation first, then creates/submits fresh close order only after cancellation settles.
  - Duplicate Close/Revert attempts remain blocked while cancellation or close submission is queued.
- **Mutation path coverage**:
  - ATM MERGE, scale-in resize, BE, Freeze Trail, Swing SL, native/ATM entries, manual SL submits, and daily-risk flatten now use gate.
- **Validation**: 188/188 tests passing; CompileCheck succeeded with 0 errors (132 existing warnings).
- **Graphify entity mapping**: `KatTradeManager.QueueAccountOperation`, `KatTradeManager.PumpAccountOperationQueue`, `KatTradeManager.CompleteAccountOperation`, `KatTradeManager.SubmitQueuedClose`, `KatTradeManager.OnAccountOrderUpdate`, `KatTradeManagerUI.OnPanelWatchdogTick`.
### [v0.81] — 2026-07-28
- **Freeze Trail StopLimit synchronization**:
  - Added `KatTradeCalculator.CalculateFrozenStopLimitPrice` to preserve existing Stop-to-Limit offset when restoring a frozen protective StopLimit.
  - Long protective exits restore Limit below Stop; Short protective exits restore Limit above Stop.
  - Invalid/zero offset falls back to instrument tick size, then `0.01`.
  - `CheckFreezeTrailEnforcement` now sets both `StopPriceChanged` and `LimitPriceChanged` before one `Account.Change` call.
- **Freeze Trail regression coverage**:
  - Added Long/Short direction, multi-tick offset, zero-offset tick fallback, and invalid-tick fallback tests.
- **Validation**: 188/188 tests passing; CompileCheck succeeded with 0 errors (131 existing warnings).
- **Graphify entity mapping**: `KatTradeCalculator.CalculateFrozenStopLimitPrice`, `KatTradeManager.CheckFreezeTrailEnforcement`, `KatFreezeTrailTests`.
### [v0.80] — 2026-07-28
- **Configurable HUD layout**:
  - Added persisted `HUD Left Inset (px)` setting, default 10px, applied only when no dragged position exists.
  - Added persisted `HUD Drag Enabled` setting, default ON; fixed mode uses arrow cursor, blocks capture, and releases active capture when disabled.
- **HUD drag runtime fix**:
  - Routed preview handlers now attach to both `panelBorder` and its actual InChart/ChartTrader host, covering visual-tree routes that bypass the Border while preserving interactive controls.
  - Handler lifetime is explicitly detached during watchdog recreation/termination to prevent stale host subscriptions.
  - ChartTrader and InChart fresh placement both honor configured left inset; dragged coordinates remain authoritative.
- **Validation**: 183/183 tests passing; CompileCheck succeeded with 0 errors (131 existing warnings).
- **Graphify entity mapping**: `KatTradeManager.HudLeftInset`, `KatTradeManager.HudDragEnabled`, `KatTradeManagerUI.SyncCachedValues`, `KatTradeManagerUI.AttachHudDragHandlers`, `KatTradeManagerUI.DetachHudDragHandlers`, `KatTradeManagerUI.CreateWpfControls`.
### [v0.79] — 2026-07-28
- **Revert quantity fix**:
  - Revert now captures live position quantity before close and carries that quantity through asynchronous close-fill retry.
  - Reversed market entry no longer falls back to HUD Contracts value, so a 4-contract position reverts to 4 contracts instead of 1.
- **ATM MERGE stale-bracket cleanup**:
  - Reconciliation now scans all ATM-looking protective orders on the instrument, not only orders matching current position exit direction.
  - Opposite-side stale ATM SL/TP sets, such as old `Sell` brackets left after reversal while current position is Short, are cancelled.
  - Current-side canonical SL/TP quantity merge remains unchanged; manual `KAT_*` exits remain excluded.
- **Runtime diagnostics**: Revert logs captured close/entry quantity; MERGE logs `staleOpposite` removals.
- **Validation**: 183/183 tests passing; CompileCheck succeeded with 0 errors.
- **Graphify entity mapping**: `KatTradeManager.RevertPosition`, `KatTradeManager.TrySubmitPendingRevert`, `KatTradeManager.PlaceMarketOrder`, `KatTradeManager.IsAtmBracketCandidate`, `KatTradeManager.MergeAtmBrackets`.
### [v0.78] — 2026-07-28
- **HUD drag runtime hardening**:
  - Default InChart left inset is now 50px.
  - Drag source traversal now handles visual, logical, and `ContentElement`/`Run` parents, with runtime capture/mode/parent diagnostics.
  - ChartTrader restores persisted dragged coordinates after watchdog re-attachment instead of resetting to its default docked alignment.
- **ATM MERGE scale-out reconciliation hardening**:
  - Protective-order detection now uses ATM bracket names, `FromEntrySignal`, and known anchor OCO identity while excluding all `KAT_*` manual exits.
  - Includes transient ATM states such as `AcceptedByRisk`, `TriggerPending`, `ChangePending`, `ChangeSubmitted`, `PartFilled`, and `Suspended`.
  - Runtime diagnostics print order name, ID, OCO, entry signal, action, type, state, quantity, fill, stop, and limit values for direct scale-out verification.
  - `PartFilled` remains active for bracket resizing and is no longer treated as terminal for tracked scale-in/revert orders.
- **Validation**: 183/183 tests passing; CompileCheck succeeded with 0 errors.
- **Graphify entity mapping**: `KatTradeManagerUI.GetHudParent`, `KatTradeManagerUI.OnHudPreviewMouseLeftButtonDown`, `KatTradeManagerUI.CreateWpfControls`, `KatTradeManager.IsAtmMergeOrder`, `KatTradeManager.IsKnownAtmBracket`, `KatTradeManager.OnAccountOrderUpdate`.
### [v0.77] — 2026-07-28
- **HUD drag root-cause fix**:
  - ChartTrader mode previously set `Cursor = Arrow` and attached no drag handlers; InChart mode captured the Canvas, making routed WPF move/up events fragile.
  - Both modes now attach preview handlers to `panelBorder` and capture `panelBorder` subtree directly.
  - Hit testing walks visual/logical parents, including `ContentElement`/`Run`, while interactive controls remain excluded so buttons keep normal clicks.
  - Drag capture is released before watchdog teardown/recreation.
- **HUD default inset**:
  - InChart HUD now starts 80px from left edge instead of 10px, reducing overlap with other indicators' left-side S/R labels.
- **Validation**: 183/183 tests passing; CompileCheck succeeded with 0 errors.
- **Graphify entity mapping**: `KatTradeManagerUI.IsHudDragSource`, `KatTradeManagerUI.OnHudPreviewMouseLeftButtonDown`, `KatTradeManagerUI.OnHudPreviewMouseMove`, `KatTradeManagerUI.CreateWpfControls`, `KatTradeManagerUI.RemoveWpfControls`.
### [v0.76] — 2026-07-28
- **ATM MERGE scale-in/scale-out reconciliation**:
  - Reconciles every 500 ms and after account order updates while MERGE is enabled.
  - Uses live `Position.Quantity` as single source of truth for canonical SL and TP quantities.
  - Keeps one existing stop anchor plus one target anchor; cancels duplicate ATM brackets even when their prices differ.
  - Flat-position cleanup cancels remaining ATM brackets; MERGE OFF leaves independent brackets untouched and restores reconciliation when re-enabled.
- **Validation**: 183/183 tests passing; CompileCheck succeeded with 0 errors.
- **Graphify entity mapping**: `KatTradeManager.ScheduleAtmBracketMerge`, `KatTradeManager.MergeAtmBrackets`, `KatTradeManager.OnAccountOrderUpdate`, `KatTradeManagerUI.OnPanelWatchdogTick`, `KatTradeManagerUI.CreateWpfControls`.
### [v0.75] — 2026-07-28
- **HUD status visual cleanup**:
  - Removed black fill from status slot; status text now renders with transparent background.
  - Preserved fixed two-line slot, timeout clearing, and HUD height stability.
- **BE / Swing SL runtime fix**:
  - `Account.Change()` now receives `StopPriceChanged` and `LimitPriceChanged`, matching NT8 Cbi order-change contract.
  - Added HUD feedback for no-position, invalid-stop, and no-swing no-op paths.
- **Validation**: 183/183 tests passing; CompileCheck succeeded with 0 errors.
- **Graphify entity mapping**: `KatTradeManagerUI.CreateWpfControls` status `TextBlock` background, `KatTradeManager.SetBreakeven`, `KatTradeManager.ShiftSlToSwing`.
### [v0.74] — 2026-07-28
- **Permanent HUD status slot**:
  - Added fixed-height black two-line status region at HUD top.
  - Status timeout now clears text and resets color without collapsing or changing HUD height.
  - Watchdog recreation preserves same fixed slot contract.
- **ATM MERGE active-bracket scale-in**:
  - First entry still starts selected ATM template through `StartAtmStrategy`.
  - Subsequent same-direction MERGE entries submit through `Account.Submit` instead of creating another ATM instance.
  - Incremental `Order.Filled` quantities resize first active ATM stop/target anchors through `Account.Change`.
  - SPLIT retains independent ATM-per-entry behavior; legacy duplicate-bracket cancellation removed.
- **Validation**: 183/183 tests passing; CompileCheck succeeded with 0 errors.
- **Graphify entity mapping**: `KatTradeManagerUI.CreateWpfControls`, `KatTradeManagerUI.ShowHudStatus`, `KatTradeManager.SubmitOrder`, `KatTradeManager.TryPrepareAtmScaleIn`, `KatTradeManager.ProcessAtmScaleInUpdate`, `KatTradeManager.ResizeAtmBracketForFill`.
### [v0.73] — 2026-07-28
- **HUD drag reliability fix**:
  - Replaced Border-only mouse capture with routed handlers registered using `handledEventsToo`.
  - Captures InChart overlay Canvas subtree, so nested card/control routing cannot lose drag move/up events.
  - Preserves button, TextBox, ComboBox, Selector, and Thumb clicks by rejecting interactive visual sources.
  - Keeps 40px visibility clamp and persisted HUD coordinates.
  - **Tests**: 183/183 passing. Compile gate: succeeded with 0 errors (existing NT8 reference-conflict/obsolete warnings only).
  - **Graphify entity mapping**: `KatTradeManagerUI.OnHudPreviewMouseLeftButtonDown`, `KatTradeManagerUI.OnHudPreviewMouseMove`, `KatTradeManagerUI.OnHudPreviewMouseLeftButtonUp`, `KatTradeManagerUI.StopHudDrag`, `KatTradeCalculator.ClampHudCoordinate`.
### [v0.72] — 2026-07-28
- **ATM bracket merge/split toggle**:
  - Added default-on `ATM Bracket: MERGE` button directly below Stop-Limit; `SPLIT` preserves existing separate-bracket behavior.
  - Because `StartAtmStrategy(template, order)` creates a new ATM instance instead of attaching to active Chart Trader ATM, merge mode consolidates same-price named ATM stop/target orders after account updates by increasing anchor quantity and cancelling duplicates.
  - BE (`KAT_SL_BE`), swing (`KAT_SL_SWING`), and other manual exits stay excluded by ATM bracket-name filtering.
  - Added overflow-safe `KatTradeCalculator.CalculateMergedOrderQuantity`.
  - **Tests**: 183/183 passing. Compile gate: succeeded with 0 errors (existing NT8 reference-conflict/obsolete warnings only).
  - **Graphify entity mapping**: `KatTradeManager.MergeAtmBrackets`, `KatTradeManager.ScheduleAtmBracketMerge`, `KatTradeManager.IsAtmMergeOrder`, `KatTradeCalculator.CalculateMergedOrderQuantity`, `KatTradeManagerUI.CreateWpfControls`.
### [v0.71] — 2026-07-28
- **Swing Stop Loss back/forward fix**:
  - `ShiftSlToSwing` now sees all active stop states, not only `Working`/`Accepted`; submitted/change-pending stops can be modified instead of silently falling through.
  - Added chart-price fallback from cached close/high/low when live price cache is empty, preventing valid swing targets from being rejected as zero-price stops.
  - StopLimit protective orders now move both stop and limit prices together, preserving one-tick direction offset.
  - Centralized previous swing H/L selection for Long/Short and preserved history-based back/forward behavior.
  - **Tests**: 181/181 passing. Compile gate: succeeded (existing NT8 reference-conflict warnings only).
  - **Graphify entity mapping**: `KatTradeManager.ShiftSlToSwing`, `KatTradeManager.GetSwingValidationPrice`, `KatTradeCalculator.FindNextSwingStopPrice`, `KatTradeManager.GetSwingPoints`.
### [v0.70] — 2026-07-28
- **Runtime BE, EMA scope, Stop-Limit, and HUD drag fixes**:
  - Hardened BE action against missing live-price cache, transient active stop states, invalid stop side, and null stop creation; successful moves/submissions now show HUD feedback.
  - EMA Place and EMA Angle checks now run only on direct candle/fixed-distance Buy/Sell entry routes; EMA touch, market, Revert, BE, and Close paths bypass them. Both HUD filter toggles default OFF.
  - Added Freeze Trail-style `Stop-Limit: OFF/ON` button directly below Freeze Trail. When enabled, valid pending StopMarket entries use StopLimit with a one-tick protective limit offset.
  - Replaced fixed-bottom InChart margin drag with Canvas absolute coordinates, bounded movement, and watchdog position persistence.
  - **Tests**: 179/179 passing. Compile gate: succeeded (existing NT8 reference-conflict warnings only).
  - **Graphify entity mapping**: `KatTradeManager.SetBreakeven`, `KatTradeManager.PlaceOrderInternal`, `KatTradeCalculator.CalculateStopLimitPrices`, `KatTradeManagerUI.CreateWpfControls`, `KatTradeManagerUI.RemoveWpfControls`.
### [v0.69] — 2026-07-28
- **Runtime order/HUD fix round**:
  - Fixed ATM market BUY/SELL orders stuck at `Initialized`: ATM-backed market entries now use NinjaTrader-required order name `Entry`; native submit remains fallback when template file is missing.
  - Added account `OrderUpdate` diagnostics for tracked entries/close orders, including state transitions and close-submit details.
  - Revert now retries opposite market entry from watchdog and close-order terminal events, preserves pending action until submit succeeds, and guards against duplicate flip submissions. Short close uses `BuyToCover`.
  - Hardened InChart drag routing with preview move/up events, mouse capture, lost-capture cleanup, and interactive-child filtering.
  - Added dispatcher-safe, auto-clearing HUD status for EMA Place/Angle rejection reasons and successful market submission.
  - **Tests**: 177/177 passing. Compile gate: succeeded (existing NT8 reference-conflict warnings only).
  - **Graphify entity mapping**: `KatTradeManager.SubmitOrder`, `KatTradeManager.PlaceMarketOrder`, `KatTradeManager.OnAccountOrderUpdate`, `KatTradeManager.TrySubmitPendingRevert`, `KatTradeManager.ClosePosition`, `KatTradeManagerUI.CreateWpfControls`, `KatTradeManagerUI.ShowHudStatus`.
### [v0.68] — 2026-07-28
- **Full click-path reaudit and HUD interaction fix**:
  - HUD now defaults to `InChart`, first renders bottom-left, supports bounded drag, and preserves user position when watchdog reattaches the panel.
  - Preview drag handler ignores Button/TextBox/ComboBox descendants, so controls no longer lose `MouseUp`/`Click` events.
  - Pending candle/fixed-distance/EMA entries enqueue chart lines only after the exact order submission succeeds; market orders never create misleading pending-entry lines.
  - Revert now queues opposite market entry until the close order fills, preventing close/reverse race and position over-flip.
  - EMA touch, EMA filter, live-price, and Swing reads used by HUD actions now come from data-thread snapshots instead of WPF-thread NinjaScript series access.
  - Added pure regression coverage for line eligibility and HUD drag clamping.
  - **Tests**: 177/177 passing. Compile gate: succeeded (existing NT8 reference-conflict warnings only).
  - **Graphify entity mapping**: `KatTradeManager.OnBarUpdate`, `KatTradeManager.UpdateEmaTouchCache`, `KatTradeManagerUI.CreateWpfControls`, `KatTradeManager.SubmitOrder`, `KatTradeManager.TrySubmitPendingRevert`, `KatTradeCalculator.ShouldDrawExpectedLines`, `KatTradeCalculator.ClampHudCoordinate`.

### [v0.66] — 2026-07-28
- **CRITICAL FIX: "No button works — no order created"**:
  - **Root cause**: `DataLoaded` selected `account` only if `Account.All.Count > 0` at the moment the chart opened. If the chart was opened BEFORE NT8 finished connecting accounts (common on startup), `Account.All` was empty → `account` stayed **null forever** — no retry existed. Every button click hit `if (account == null || Instrument == null) return;` and returned **silently** (no Print, no order, no error). The user saw a panel with buttons but nothing happened.
  - **Fix 1 (root cause)**: New `SelectAccount()` helper (DRY) extracted from DataLoaded. The 500 ms UI watchdog now auto-recovers: `if (account == null) account = SelectAccount()`. As soon as NT8 connects accounts after chart open, the watchdog assigns one within 500 ms — buttons work immediately. Printed: `Account auto-recovered by watchdog: <name>`.
  - **Fix 2**: accSelector fallback (`SelectedIndex = 0`) now assigns `account = allowedAccs[0]` directly, instead of only setting the visual selection (the SelectionChanged handler wasn't attached yet at that point).
  - **Fix 3 (diagnostic)**: All 10 order-method guards now Print `No account — watchdog auto-recovering. Retry in a moment.` instead of returning silently. The user sees EXACTLY why buttons don't fire instead of guessing.
  - **Fix 4**: DataLoaded now prints `WARNING: Account.All empty at load` or `WARNING: No account selected` when the initial selection fails, so the NinjaScript Output window (Ctrl+Alt+Shift+O) shows the full status chain.
  - **Tests**: 170/170 passing (fix is NT8-runtime state, not pure logic). Compile gate: **succeeded**.
  - **Graphify entity mapping**: `KatTradeManager.SelectAccount` (new helper), `KatTradeManager.OnPanelWatchdogTick` (auto-recovery), `KatTradeManagerUI.CreateWpfControls` (accSelector fallback fix).

### [v0.65] — 2026-07-26
- **Final Audit Round 10: Contract Clamp, Deploy-Sync Verification, Scope Documentation**:
  - **Bug fix (contract)**: `GetLineStartBar(currentBar, maxBarsAgo)` violated its own "never negative" contract for negative `maxBarsAgo` (returned it verbatim → future-bar anchor). Now clamps to 0. +1 test.
  - **Verification (new)**: First-ever hash-level deploy sync check (repo vs `Indicators\`). Found only cosmetic EOL-tail differences (LF vs CRLF, NT8 compiles both); full sync re-established after deploy.
  - **Docs**: `CancelAllOrders` account-wide scope (no `Instrument` filter) is now explicitly commented as intentional — matches "Close/flatten" and account-level daily-risk semantics; every other order query in the class is Instrument-scoped. Behavior unchanged.
  - **Audit conclusion**: no further functional defects found across all 6 source files; auto paths bounded (Interlocked latch, 3 s freeze rate-limit), user paths guarded (debounce, in-flight close, side validation).
  - **Tests**: 169 → **170 tests, all passing**. Compile gate: **succeeded**.
  - **Graphify entity mapping**: `KatTradeCalculator.GetLineStartBar` (negative clamp), `KatTradeManager.CancelAllOrders` (scope documented).

### [v0.64] — 2026-07-26
- **Audit Round 9: Broker-Rejection Guards, NRE Race Fix**:
  - **Bug fix (broker rejections = order-rate cost)**: `SetBreakeven` on an underwater position created/moved the stop to the wrong side of market (Long: sell stop ABOVE price) → broker rejection. Now guarded by new pure helper `KatTradeCalculator.IsStopOnValidSide` (Long: stop must be below market; Short: above) — prints a skip reason instead of spending an order-rate slot on a guaranteed rejection.
  - **Bug fix**: `ShiftSlToSwing` could target a historical swing that is already on the wrong side of current market (price moved past it) → `account.Change` rejection. Guarded twice: at swing selection (invalid swings never enter `slMoveHistory`) and as a final net before applying. Also repaired the round-6 indentation damage in that block.
  - **Bug fix (robustness)**: `OnBarUpdate` read `entryOrder.OrderState` right after a null-check — the UI thread (`CancelAllOrders`) can null the volatile field in between → NullReferenceException caught by the broad catch (log spam each occurrence). Now uses a local copy.
  - **Tests**: +3 facts (`IsStopOnValidSide` long/short/zero-price cases). Suite: 166 → **169 tests, all passing**. Compile gate: **succeeded**.
  - **Graphify entity mapping**: `KatTradeCalculator.IsStopOnValidSide` (new pure helper), `KatTradeManager.SetBreakeven` (underwater guard), `KatTradeManager.ShiftSlToSwing` (selection + net guards), `KatTradeManager.OnBarUpdate` (local-copy fix).

### [v0.63] — 2026-07-26
- **Audit Round 8: Anti-Order-Spam Hardening (kick-out protection)**:
  - **Bug fix (critical)**: `ClosePosition` could cancel its OWN just-submitted close order — `account.Submit(close)` immediately followed by `CancelAllOrders()`, and a market order can already be `Accepted` in `account.Orders` at that point → close silently cancelled, position left open while the user (or the emergency-flatten latch) believed it was closed. `CancelAllOrders` now always excludes orders named `KAT_CLOSE`.
  - **Bug fix (critical)**: Double-clicking **Close/flatten** submitted two market close orders → position FLIPPED (Long 3 → Short 3). New `IsCloseInFlight()` guard (any working/accepted `KAT_CLOSE` on the instrument) makes the second click a no-op. `ClosePosition` restructured: cancel orders first (excluding the close), then skip if a close is already in flight, then submit.
  - **Bug fix**: Double-clicking **Revert** while the close was still in flight fired a second close + an extra reverse market order → over-reversal. `RevertPosition` now aborts up-front when a close is in flight.
  - **Hardening**: 500 ms anti-spam debounce on both entry paths (`PlaceOrderInternal`, `PlaceMarketOrder`) — mouse-jitter double-clicks and hotkey bounces can no longer duplicate entries. All hotkeys route through the same methods, so they are covered too.
  - **Spam-safety matrix (verified)**: auto paths are bounded — watchdog risk-eval is latched by `Interlocked` (max 1 flatten per breach episode, both threads), Freeze-Trail enforcement is rate-limited to 1 change batch per 3 s, line drawing submits nothing; user paths are now guarded — entries debounced, close/revert in-flight-guarded, BE/Swing-SL are single `account.Change` batches per deliberate click with natural stop conditions.
  - **Tests**: 166/166 passing (no pure-logic change this round; guards are NT8-runtime side). Compile gate: **succeeded**.
  - **Graphify entity mapping**: `KatTradeManager.IsCloseInFlight` (new), `KatTradeManager.CancelAllOrders` (KAT_CLOSE exclusion), `KatTradeManager.ClosePosition` (restructured), `KatTradeManager.RevertPosition` (in-flight guard), `KatTradeManager.IsEntryDebounced` (new).

### [v0.62] — 2026-07-26
- **Audit Round 7: Swing-SL Direction Fix, ATM Quantity Contract Fix**:
  - **Bug fix (trading logic)**: `ShiftSlToSwing`'s fallback (`swings.FirstOrDefault(differing)`) fired when no swing existed in the intended direction and moved the stop loss the WRONG way — for a Long it grabbed a HIGHER swing low (tightening the SL) when the user pressed the loosen button (◀ SL), and vice-versa for Short. Fallback removed: when no further swing exists in the intended direction, the indicator now prints "No further swing points found on chart." and leaves the stop untouched.
  - **Bug fix (UX/data contract)**: `AtmTemplateData.Quantity` defaulted to 1, so an ATM template with no quantity info (or a file deleted between listing and loading) stomped the user's Contracts box to "1" via `LoadAtmTemplateSettings`. Default is now 0 = "unspecified"; the existing `atmQuantity > 0` guard preserves the user's quantity. Updated 9 existing test assertions + comment to the new contract.
  - **Tests**: +1 (`ParseXml_NoQuantityNodes_QuantityStaysZero`). Suite: 165 → **166 tests, all passing**. Compile gate: **succeeded**.
  - **Graphify entity mapping**: `KatTradeManager.ShiftSlToSwing` (fallback removed), `AtmTemplateData.Quantity` (0 = unspecified contract), `KatTradeManager.LoadAtmTemplateSettings` (stomp prevented).

### [v0.61] — 2026-07-26
- **Audit Round 6: Account-Switch State Reset, XML Fallback & Angle Tests**:
  - **Bug fix (trading impact)**: Switching accounts via the HUD dropdown did not reset per-account state. The OLD account's gross realized PnL stayed as the daily-PnL session baseline → the new account showed phantom daily PnL (e.g. old account +$200 captured, new account at $0 → phantom −$200) causing false emergency flattens or missed breach detection. The stale `frozenStopPrice` from the old account could also yank the new account's stops to an outdated price. The account-change handler now resets: `isSessionStartCaptured = false`, `dailyRiskFlattened = 0` (Interlocked), `frozenStopPrice = 0`.
  - **Noted, not changed**: `cachedDailyPnL` is write-only (kept for future HUD display); Freeze Trail captures `workingStops[0]` — multi-bracket stops get unified to the first stop's price (documented limitation).
  - **Tests**: `KatCalculatorGapTests.cs` round 5 (+5): ATM XML `EntryQuantity=0` bracket-sum fallback, XML without `Brackets` node, `CalculateEmaAngle` exact 2-tick slope (63.43°), `DetermineOrderType` zero-tickSize unrounded path, `IsAccountAllowed` mixed `,;` separators. Suite: 160 → **165 tests, all passing**. Compile gate: **succeeded**.
  - **Graphify entity mapping**: `KatTradeManagerUI` accSelector handler (state reset), `KatTradeManager.CalculateDailyPnL` (baseline contract), `KatAtmXmlParser.ParseXmlDocument` (fallback coverage).

### [v0.60] — 2026-07-26
- **Audit Round 5: Freeze-Trail Stale Price Fix, Vertical Drag Fix, Boundary Tests**:
  - **Bug fix (trading impact)**: Freeze Trail could yank the stop loss to a STALE price — `FreezeCurrentStopLoss` left `frozenStopPrice` untouched when toggled ON with no position / no working stops ("waiting" branch). A value from a previous freeze episode survived, and the next appearance of a working stop got force-changed to the outdated price (e.g. froze at 100, toggled off, toggled on while flat, new stop trails to 120 → enforcement slammed it back to 100). Now `frozenStopPrice` is reset to 0 at the start of every freeze activation, so enforcement always re-captures the CURRENT stop.
  - **Bug fix (UX)**: Vertical dragging silently did nothing in ChartTrader-fallback mode — the panel uses `VerticalAlignment.Bottom` there, where `Margin.Top` is ignored, but the drag handler only ever adjusted `Margin.Top`. Mouse-down now normalizes the panel to `Left`/`Top` alignment with an absolute margin (via `TranslatePoint`) before the drag begins; horizontal-only drag is fixed in both fallback and InChart modes. Round-1 clamping still applies.
  - **Tests**: `KatCalculatorGapTests.cs` round 4 (+5): doji-candle partial price (high==low), NaN EMA touch guard, flat-EMA angle failing a positive threshold, `FindSwingPoints` 500-bar scan cap (swing beyond cap excluded), partial price with unknown tick size (unrounded). Suite: 155 → **160 tests, all passing**. Compile gate: **succeeded**.
  - **Graphify entity mapping**: `KatTradeManager.FreezeCurrentStopLoss` (stale-reset), `KatTradeManager.CreateWpfControls` (drag alignment normalize), `KatTradeCalculator.FindSwingPoints` (cap coverage), `KatTradeCalculator.CalculatePartialCandlePrice` (doji/no-tick coverage).

### [v0.59] — 2026-07-25
- **Audit Round 4: Orphaned-Order Fix, Volatile Termination Flag, Boundary Tests**:
  - **Runtime verification**: `NinjaTrader.Custom.dll` recompiled at 21:58 (after v0.58 deploy at 21:04) — NT8's file watcher auto-compiled v0.58 cleanly. Compile gate + live NT8 compile both green.
  - **Bug fix**: Orphaned entry order when the selected ATM template is missing on disk (stale `DefaultAtmTemplate`, emptied templates dir) — `AtmStrategy.StartAtmStrategy` fails silently, leaving a created-but-never-submitted order whose non-terminal state pins the expected-lines on the chart forever. New `SubmitOrder(order)` helper checks `File.Exists` on the template first and falls back to plain `account.Submit` with a printed warning. Applied at both submit sites (`PlaceOrderInternal`, `PlaceMarketOrder`).
  - **Hardening**: `isTerminated` is read on the data thread (`OnBarUpdate`), the UI watchdog and the hotkey handler but was a plain bool — now `volatile` for cross-thread visibility.
  - **Tests**: `KatCalculatorGapTests.cs` round 3 (+7): `IsEmaTouchBar` exact boundary touch, `ValidateEmaPlace` strict-equality rejection, `CalculateTriggerPrice` misaligned-base tick rounding, `CalculateFixedDistanceTriggerPrice` zero distance, `CalculateEmaAngle` negative-tickSize fallback, ATM XML whitespace-padded numbers, `IsAccountAllowed` spaced `"! BX"` exclude token. Suite: 148 → **155 tests, all passing**.
  - **Graphify entity mapping**: `KatTradeManager.SubmitOrder` (new helper), `KatTradeManager.isTerminated` (volatile), `KatTradeManager.PlaceOrderInternal`/`PlaceMarketOrder` (SubmitOrder call sites).

### [v0.58] — 2026-07-25
- **Audit Round 3: Local Compile Gate, ChartTrader Migration Fix, Test Gap Round 2**:
  - **New tooling**: `tools/CompileCheck/CompileCheck.csproj` — full local compile of ALL 6 indicator source files against .NET Framework 4.8 reference assemblies (NuGet `Microsoft.NETFramework.ReferenceAssemblies.net48`, no admin needed) + NinjaTrader.Core/Gui + compiled `NinjaTrader.Custom.dll` (provides built-in `EMA`). Mirrors NT8's internal Roslyn compile: `dotnet build tools/CompileCheck` = green gate before deploy. Build: **succeeded, zero errors, zero warnings**.
  - **Bug fix**: Panel stuck in chartGrid fallback — when `PanelLocation = ChartTrader` and the ChartTrader panel reappeared (user re-opened ChartTrader after it was hidden), `IsPanelAttached` kept accepting the chartGrid fallback location so the HUD never migrated back into ChartTrader. Now, when the ChartTrader panel is available, attachment is only accepted from that panel — watchdog re-docks the HUD automatically.
  - **Style**: `CancelAllOrders` batched-cancel block re-indented to try-body depth.
  - **Docs**: `RULES.md` deploy list was stale (missing `OrderOps`/`Properties`) — updated to the full 6-file list; compile-gate command documented.
  - **Noted, not changed**: `KatTradeCalculator.FindLastEmaTouchBar` + `CalculateHalfCandlePrice` are test-only (pre-existing dead code in production); `PlaceMarketOrder` intentionally bypasses EMA Place/Angle filters (market = manual override, daily-risk still enforced).
  - **Tests**: `KatCalculatorGapTests.cs` round 2 (+7): `ParseFile` real temp-file roundtrip & directory-path guard, `FindSwingPoints` flat-series dedup & strength-1 turning points, `CalculatePartialCandlePrice` 100% boundary, `GetLineStartBar(0)`, `CalculateAtmLevels` negative-tick sign behavior. Suite: 141 → **148 tests, all passing**.
  - **Graphify entity mapping**: `KatTradeManager.IsPanelAttached` (migration fix), `tools.CompileCheck` (new build gate), `KatAtmXmlParser.ParseFile` (roundtrip coverage), `KatTradeCalculator.FindSwingPoints` (degenerate-series coverage).

### [v0.57] — 2026-07-25
- **Full Audit Round 2: Critical Race Fix, Hotkey Leak Fix, Drag Clamp, Module Split & Test Gaps**:
  - **Bug fix (CRITICAL)**: Emergency flatten double-fire race — `EvaluateDailyRiskLimits` runs on BOTH the data thread (`OnBarUpdate`) and the UI thread (500ms watchdog). The `dailyRiskFlattened` bool check-then-set could be passed by both threads simultaneously, submitting `ClosePosition` twice and FLIPPING the position. Replaced with `Interlocked.CompareExchange`/`Exchange` on an int flag — exactly one flatten per breach episode, guaranteed.
  - **Bug fix**: Hotkey handler window leak — the window-level `PreviewKeyDown` handler was detached via a *fresh* `Window.GetWindow(ChartControl)` lookup; if the chart had been dragged to a different window, the old window kept the handler (keys in the detached window still fired trades). The window is now cached at attach time (`hotkeyWindow`), detach uses that exact reference, and attach detects window changes and re-attaches.
  - **Bug fix**: InChart/fallback panel drag had no bounds clamping — the panel could be dragged fully off-chart and lost. Drag now clamps the margin so at least 40px of the panel always stays reachable inside the chart grid.
  - **Improvement**: ATM template dropdown list is now sorted alphabetically (deterministic default selection instead of filesystem order).
  - **Improvement**: `CancelAllOrders` now submits one batched `account.Cancel(Order[])` call instead of per-order cancels.
  - **Refactor**: Extracted all `[NinjaScriptProperty]` definitions (~230 lines) into new partial class `src/KatTradeManager.Properties.cs`. Main file down to ~555 lines (lifecycle, price caching, drawing). Deploy list in `AGENTS.md` updated.
  - **Known limitation (documented)**: Daily PnL baseline is captured at indicator load — PnL accumulated earlier in the session before load is not included. Full fix requires historical trade query; deferred.
  - **Tests**: New `KatCalculatorGapTests.cs` — 14 tests covering `GetNySessionStartUtc` summer EDT offsets, `ValidateEmaPlace`/`ValidateEmaAngle` null/zero/mismatched-length guards, `CalculateEmaAngle` tick fallback & exact 45°, `IsAccountAllowed` semicolon separators, `CalculatePartialCandlePrice` percent edges. Suite: 127 → 141 tests, all passing.
  - **Verification**: Full indicator source compiled against NinjaTrader.Core/Gui assemblies (harness) — zero errors in all touched files; brace/region balance verified on all 6 source files.
  - **Graphify entity mapping**: `KatTradeManager.Properties` (new file), `KatTradeManager.EvaluateDailyRiskLimits` (Interlocked guard), `KatTradeManager.AttachHotkeyHandler`/`DetachHotkeyHandler` (window cache), `KatTradeManager.CreateWpfControls` (drag clamp, sorted ATM list), `KatTradeManager.CancelAllOrders` (batch cancel), `KatCalculatorGapTests` (new).

### [v0.56] — 2026-07-25
- **Full Codebase Audit, Bug Fixes, Module Split & Test Expansion**:
  - **Bug fix**: `PlaceEmaOrder` off-by-one loop bound (`barsAgo <= maxBars` → `< maxBars`) — eliminated out-of-range series access on the last scan iteration.
  - **Bug fix**: `ClosePosition` and `CancelAllOrders` wrapped in try/catch with null-check on `CreateOrder` — exceptions can no longer escape into the 500ms UI watchdog or button handlers (chart crash risk removed).
  - **Bug fix**: Emergency flatten spam — `EvaluateDailyRiskLimits` now flattens only once per breach episode (`dailyRiskFlattened` latch, resets when PnL recovers) instead of re-submitting close orders every 500ms while a breach persists.
  - **Hardening**: `SetBreakeven`/`ShiftSlToSwing` null-check created SL orders before `Submit`.
  - **Refactor**: Extracted order execution, position management, swing SL and daily risk logic (~650 lines) into new partial class `src/KatTradeManager.OrderOps.cs`. Main file now 799 lines (lifecycle + properties + drawing).
  - **Refactor**: Moved pure logic into `KatTradeCalculator` for testability: `IsAccountAllowed(accName, filter)` and `FindSwingPoints(series, findLows, maxSwings, strength, tickSize)`. Indicator methods are now thin delegates.
  - **Tests**: New `KatAccountFilterSwingSessionTests.cs` — 16 tests covering account filter tokens, swing point detection and `GetNySessionStartUtc` boundaries (EDT/EST). Suite: 111 → 127 tests, all passing.
  - **Graphify entity mapping**: `KatTradeManager.OrderOps` (new file), `KatTradeCalculator.IsAccountAllowed` (new), `KatTradeCalculator.FindSwingPoints` (new), `KatTradeManager.GetSwingPoints` (now delegates).

### [v0.55] — 2026-07-25
- **Clean NinjaScript Freeze Trail Engine**:
  - Removed all non-standard `AtmStrategyId` property and `StopAtmStrategy` method calls.
  - Relies exclusively on standard NinjaTrader 8 `account.Orders` inspection, `frozenStopPrice` lock, and `account.Change(new[] { stopOrder })` watchdog enforcement to override trailing shifts.
  - Zero custom API dependency — completely clean NinjaScript compilation.

### [v0.54] — 2026-07-25
- **Fix NinjaScript Compilation Errors**:
  - Replaced invalid `AtmStrategy.GetAtmStrategyUniqueId` with native `order.AtmStrategyId` property on `NinjaTrader.Cbi.Order`.
  - Replaced non-existent `AtmStrategy.ChangeStopLoss` static method with `AtmStrategy.StopAtmStrategy(atmId)` and standard `account.Change(new[] { stopOrder })`.
  - Restored clean compilation in NinjaTrader 8.

### [v0.53] — 2026-07-25
- **Safe Native ATM Stop Engine (`Freeze Trail`)**:
  - Refactored `FreezeCurrentStopLoss()` to invoke `NinjaTrader.NinjaScript.AtmStrategy.StopAtmStrategy(atmId)` when `Freeze Trail` is activated.
  - Automatically stops NinjaTrader's internal trailing engine at the source without sending high-frequency order modification requests.
  - Preserves working Stop Loss and Target orders as static manual/OCO orders sitting on the broker server.
  - Added a 3-second rate-limit guard (`lastFreezeEnforceTime`) in `CheckFreezeTrailEnforcement()` to completely eliminate API order spamming and rate-limit disconnection risks.

### [v0.52] — 2026-07-25
- **Freeze Trail Control (`Freeze Trail: OFF` / `⚡ Freeze Trail: ON`)**:
  - Added full-width dark gray HUD button (`#232834` / `Color.FromRgb(35, 40, 52)`) positioned directly above the `Close/flatten` button in Section 4 with height matching `BUY current` / `SELL current` buttons (Height: 24, FontSize: 10).
  - Toggling ON activates `cachedIsFreezeTrail` and captures current working Stop Loss price (`frozenStopPrice`).
  - Added `CheckFreezeTrailEnforcement()` running on every 500ms watchdog tick to override NinjaTrader ATM trailing engine movements and restore SL back to frozen price until toggled OFF or position is flat.

### [v0.51] — 2026-07-25
- **Bottom Alignment Fix for Floating ChartTrader HUD**:
  - Assigned `panelBorder` to the last row (`Grid.SetRow(panelBorder, lastRow)`) of ChartTrader's Grid.
  - Ensures HUD panel starts attached at the very bottom of ChartTrader at normal window height, and floats upward over native buttons when window height is reduced.

### [v0.50] — 2026-07-25
- **Floating ChartTrader HUD Overlay**:
  - Re-anchored `panelBorder` to the top-level outer container `Grid` of `ChartTraderControl` with `Panel.SetZIndex(panelBorder, 99999)` and `ClipToBounds = false`.
  - When chart window height is reduced, HUD panel floats on top of ChartTrader's native controls, gradually covering buttons from bottom to top so HUD is always 100% visible and prioritized.
- **Crisp Arrow Button Styling**:
  - Replaced `<-- SL` and `SL -->` labels with clean arrow symbols `◀ SL` and `SL ▶`.

### [v0.49] — 2026-07-25
- **Swing Stop Loss Shift Controls (`<-- SL` & `SL -->`)**:
  - Added new HUD control grid directly under `SELL last 89` in Section 2 with gray background buttons (`#2D3241`) matching Close/Flatten styling.
  - Implemented `GetSwingPoints` method to calculate Swing Lows (for Long positions) and Swing Highs (for Short positions) on the primary chart timeframe.
  - Implemented `ShiftSlToSwing(bool isRedo)` with step history tracking:
    - `<-- SL`: Moves active Stop Loss order to the nearest past Swing Low/High, stepping back to older swing points on repeated clicks.
    - `SL -->`: Redos / steps SL forward back towards the original SL level step-by-step.
    - Resets tracking history automatically on position flat/flip or new position entry.

### [v0.48] — 2026-07-25
- **Daily Max Drawdown & Daily Max Profit Protection**:
  - Implemented automated daily risk control in `KatTradeManager.cs` to reject order entries and trigger emergency position/order flattening when Daily Max Drawdown or Daily Max Profit limits are breached.
  - Session PnL baseline calculation (`CalculateDailyPnL`) computes net realized PnL from closed trades (`account.Trades`) exited since **6:00 PM NY time** (Eastern Time) plus real-time unrealized PnL (`account.Get(AccountItem.UnrealizedProfitLoss)`).
  - Added 2 side-by-side HUD toggle buttons (`Max DD: ON/OFF` and `Max Profit: ON/OFF`) directly under the EMA filter buttons, styled in darker purple brush (`#3A136B`).
  - Toggling HUD buttons provides instant reactivity (`EvaluateDailyRiskLimits`), immediately checking and enforcing or releasing protection bounds without requiring indicator restart.
  - Updated Close/flatten button background color (`closeBg`) to very dark gray `Color.FromRgb(20, 20, 20)` (`#141414`).

### [v0.47] — 2026-07-25

- **Fixed ChartTrader Squeezed Layout Bug**:
  - Replaced deep depth-first visual search with shallowest visual tree depth algorithm (`GetVisualDepth`) and direct `ContentControl`/`ScrollViewer` extraction, preventing HUD from being attached to nested 2-column sub-grids inside Market buttons.
  - Added dynamic Grid row creation (`RowDefinitions.Add(RowDefinition)`) and `Grid.SetColumnSpan` spanning 100% width across all columns so HUD is placed at the very bottom of ChartTrader without column squeezing.

### [v0.46] — 2026-07-25
- **Enhanced ChartTrader Docking Placement & Bottom-Left Fallback**:
  - Refined `FindChartTraderPanel` visual tree search to target the main vertical `StackPanel` containing all order controls, properly embedding HUD at the very bottom of the right-side ChartTrader panel.
  - Updated fallback behavior when ChartTrader menu is disabled: HUD automatically moves to bottom-left corner (`HorizontalAlignment.Left`, `VerticalAlignment.Bottom`) so it does not block right-side candles/price scale, with full mouse drag support enabled.

### [v0.45] — 2026-07-25
- **Added HUD Panel Location Setting (`PanelLocation`)**:
  - Added `HUD Location` (`PanelLocation`) enum property to Indicator Settings with options: `ChartTrader` (right-side panel, default) and `InChart` (floating overlay inside chart area).
  - Implemented WPF Visual Tree detection (`GetChartTraderControl`, `FindChartTraderPanel`) to automatically attach the HUD panel to the bottom of NinjaTrader 8's native ChartTrader right-side column, freeing up 100% of chart candle view area.
  - Implemented automatic fallback to `InChart` overlay if ChartTrader is disabled or hidden by user.

### [v0.44] — 2026-07-25
- **Added Account Filter Setting (`AccountFilter`)**:
  - Added configurable `AccountFilter` property in Indicator settings (comma-separated keywords, e.g. `79424, Sim101` or `!BX, !LTE`).
  - Added `IsAccountAllowed` filtering logic supporting inclusion keywords and `!` exclusion patterns to filter out breached/inactive prop accounts from HUD dropdown selector.

### [v0.43] — 2026-07-25
- **Swapped SELL / BUY Column Layout**:
  - Moved SELL column buttons (`SELL last 34`, `SELL last 89`, `SELL previous`, `SELL current`, `SELL -distance`, `SELL market`) to the left (Column 0).
  - Moved BUY column buttons (`BUY last 34`, `BUY last 89`, `BUY previous`, `BUY current`, `BUY +distance`, `BUY market`) to the right (Column 2).
- **Added Indicator Settings Hotkeys with WPF PreviewKeyDown Overrides**:
  - Exposed 15 configurable `System.Windows.Input.Key` properties in NinjaTrader Indicator Settings under `GroupName="Hotkeys"`.
  - Added master `Enable Hotkeys` toggle (`HotkeyEnabled`).
  - Implemented WPF `PreviewKeyDown` tunneling event listener on `ChartControl` & `ChartWindow` setting `e.Handled = true` to override default NinjaTrader hotkeys.
  - Added safety checks: ignores key repeats (`e.IsRepeat`) to prevent order spam, and skips execution when user is typing in HUD input textboxes (`Keyboard.FocusedElement is TextBox`).

### [v0.42] — 2026-07-25
- **Enhanced EMA Filter Settings Organization & Configurable Timeframes**:
  - Renamed EMA 1, 2, 3 parameters to `1st`, `2nd`, `3rd` EMA (e.g. `1st EMA Place`, `2nd EMA Place`, `3rd EMA Place`, `1st EMA Angle`, `2nd EMA Angle`, `3rd EMA Angle`).
  - Added per-EMA Timeframe selection property (`KatEmaTimeframe`) for each EMA slot in both Place and Angle filters, defaulting to `5m` while allowing independent per-EMA TF selection (Chart TF, 30s, 1m, 2m, 3m, 5m, 15m, 30m, 60m).
  - Split EMA Place Filter and EMA Angle Filter into two distinct parameter sections in NinjaTrader settings window (`GroupName="EMA Place Filter"` and `GroupName="EMA Angle Filter"`).
  - Updated multi-timeframe series loading to support 9 series (`NUM_SERIES = 9`).

### [v0.41] — 2026-07-25

- **Fixed CS0136 Variable Scope Shadowing Error**:
  - Resolved compiler error in `KatTradeManager.cs` where `katAction` variable was re-declared inside `PlaceOrderInternal`'s inner `lock (priceLock)` scope.
  - Re-deployed clean `.cs` files to NinjaTrader 8.

### [v0.40] — 2026-07-25

- **Added EMA Place & EMA Angle HUD Buttons and 5m Multi-EMA Validation Engine**:
  - Placed 2 new toggle buttons (`EMA Place` and `EMA Angle`) side-by-side on 1 row directly below `Partial Candle` button on the HUD.
  - Default state: ON for both, with very dark blue background `#0C234B` when ON and dark slate `#2D3241` when OFF.
  - Added 5m DataSeries (`BarsArray[4]`) in `State.Configure` and initialized 5m EMA series in `State.DataLoaded`.
  - Added configurable indicator parameters under `"EMA Filters (5m)"`:
    - EMA Place: EMA 1 (9 default, ON), EMA 2 (34 default, ON), EMA 33 (89 default, ON).
    - EMA Angle: EMA 1 (9, min angle >= 35°), EMA 2 (34, min angle >= 30°), EMA 3 (89, min angle >= 15°).
  - Implemented `KatTradeCalculator.CalculateEmaAngle`, `ValidateEmaPlace`, and `ValidateEmaAngle`.
  - Integrated pre-order validation into `PlaceOrderInternal` to reject orders if EMA Place or EMA Angle requirements fail.
  - Added 10 new unit tests covering EMA Place & Angle math and validation logic in `KatEmaPlaceAndAngleTests.cs` (Total: 111 tests passing).
  - Graphify Entity Mapping: `KatTradeManager` -> `KatTradeCalculator` -> `KatTradeManagerUI` (5m EMA validation pipeline).

### [v0.39] — 2026-07-25

- **Fixed Compilation Error CS0128**:
  - Removed duplicate `sec1Panel` StackPanel variable declaration in `KatTradeManagerUI.CreateWpfControls()`.

### [v0.38] — 2026-07-25
- **Refined Typography & HUD Header Alignment**:
  - Changed button text font weight from `Bold` to `Normal` for a cleaner, modern look.
  - Aligned HUD Title (`⚡ KAT TradeManager v0.38`) to `Left`.
  - Formatted button labels following `BUY`/`SELL` uppercase prefix with lower-case descriptors (e.g. `BUY last 34`, `SELL last 34`, `BUY previous`, `BUY current`, `BUY +distance`, `BUY market`, `Close/flatten`).

### [v0.37] — 2026-07-25
- **Darkened Section 4 Button Palette Below Distance Order Colors**:
  - `BUY Market`: adjusted to deep stealth green `#0C3019` (darker than BUY Distance `#10381E`).
  - `SELL Market`: adjusted to deep stealth red `#370F12` (darker than SELL Distance `#4B1418`).
  - `BE`: adjusted to deep slate teal `#0E303E`.
  - `Revert`: adjusted to deep burnt amber `#4B2A0A`.
  - `Close/Flatten`: adjusted to deep dark maroon `#3C0E12`.

### [v0.36] — 2026-07-25
- **HUD Section Card Architecture & Button Spacing Refinement**:
  - Wrapped all 4 HUD sections in distinct solid black section card containers (`CreateSectionCard` helper) with background `#0A0C12`, subtle border `#232A38`, 5px corner radius, and 6px padding.
  - Standardized internal button spacing: set uniform 4px vertical and horizontal gaps between all buttons within the same section.

### [v0.35] — 2026-07-25
- **Added Visual Section Spacing Gaps in HUD Panel**:
  - Section 1 (ATM dropdown & above): added 10px bottom margin after ATM selector.
  - Section 2 (BUY/SELL Last EMA 34 & 89): added 10px bottom margin after EMA 89 grid.
  - Section 3 (Partial Candle & BUY/SELL Distance): added 10px bottom margin after order grid.
  - Section 4 (BUY/SELL Market, BE, Revert, Close/Flatten): grouped at bottom of HUD panel.

### [v0.34] — 2026-07-25
- **Fixed NinjaTrader 8 Order Modification Compilation Error**:
  - Fixed `CS1501: No overload for method 'Change' takes 4 arguments` error in `KatTradeManager.SetBreakeven()`.
  - Updated `stopOrder.StopPrice = bePrice;` before submitting `account.Change(new[] { stopOrder })`, adhering strictly to NinjaTrader 8's `Account.Change(IEnumerable<Order>)` API signature.

### [v0.33] — 2026-07-25
- **Redesigned Bottom HUD Management Controls**:
  - Removed old `Cancel` button.
  - Added **BUY Market** (Emerald Green) & **SELL Market** (Ruby Red) buttons above management controls (Height 48, Font 12).
  - Added **BE** (Breakeven) (Slate Teal) & **Revert** (Amber Gold) position management buttons (Height 33, Font 12).
  - Updated **Close/Flatten** button: full-width layout, enlarged height (33px, 1.5x) and font size (15pt, 1.5x bold) in Deep Crimson Red.
  - Added `CalculateBreakevenPrice()` helper to `KatTradeCalculator` and unit test coverage in `KatTradeCalculatorTests`.
- **Graphify Entity Mapping**:
  - `KatTradeCalculator.CalculateBreakevenPrice` -> Pure calculation of Breakeven price (+/- buffer ticks).
  - `KatTradeManager.SetBreakeven` -> Adjusts active Stop Loss orders or submits new Breakeven Stop order.
  - `KatTradeManager.RevertPosition` -> Closes current position and submits market order in opposite direction.
  - `KatTradeManager.PlaceMarketOrder` -> Submits immediate Market entry order with configured ATM strategy template.

### [v0.32] — 2026-07-25
- **Partial Candle Mode Refactor with Configurable Pullback %**:
  - Renamed `1/2 Candle` toggle button to `Partial Candle`.
  - Display button text dynamically reflects configured percentage (e.g. `⚡ Partial 30%: ON` when active).
  - Added `DefaultPartialCandlePercent` NinjaScript Indicator setting (Range 1-99%, default: `30%`).
  - Updated price calculation in `KatTradeCalculator.CalculatePartialCandlePrice`:
    - Buy: `High - (High - Low) * (pullbackPercent / 100.0)`
    - Sell: `Low + (High - Low) * (pullbackPercent / 100.0)`
  - Backward compatible: preserved 50% midpoint overload for existing callers.
- **Tests**: Expanded test suite to 97 tests (all passing in 111ms).
- **Graphify**: AST-only update.

### [v0.31] — 2026-07-25

- **EMA 34 & EMA 89 Buy/Sell Last Candle Feature**:
  - Added 2 button rows (`BUY Last 34` / `SELL Last 34` and `BUY Last 89` / `SELL Last 89`) in WPF control panel placed above `1/2 Candle ON/OFF` button.
  - Button height (48px) and font size (12pt) match `BUY Previous` / `SELL Previous` button sizes.
  - Scanning logic scans historical bars backward to find the most recent candle touching or crossing EMA 34 / 89 line (`High >= EMA && Low <= EMA`).
  - Supports 1/2 Candle mode toggle: calculates midpoint trigger price when 1/2 Candle mode is active, automatically determining StopMarket vs Limit order types.
  - Multi-timeframe aware: scans EMA 34/89 on the active selected timeframe (`Chart TF`, `30s`, `1m`, `2m`).
- **Tests**: Added `KatEmaTouchTests.cs` (91 tests passing cleanly in 106ms).
- **Graphify**: AST-only update.

### [v0.30] — 2026-07-25

- **HUD UI Refactor & Parameter Streamlining**:
  - Removed Buffer, Distance, and TF input controls from HUD panel to reduce clutter and vertical size.
  - Added `KatTimeframe` enum property (`DefaultTimeframe`) to NinjaScript Indicator properties (default: Chart TF). Buffer (2 ticks) and Distance (320 ticks) remain configurable in Indicator settings.
  - Subdued KAT TradeManager header title color (`Color.FromRgb(70, 130, 160)`) to eliminate glaring contrast and distraction.
  - Expanded ATM dropdown selector to fullwidth across panel, removing "ATM:" label to maximize template name visibility.
- **Tests**: 82 tests passing cleanly.
- **Graphify**: AST-only update.

### [v0.29] — 2026-07-25
- **Audit & Line Draw/Remove Fixes**:
  - Fixed `CancelAllOrders` double-removal race: removed redundant UI-thread `RemoveExpectedLines()` dispatch that contradicted the pending-remove pattern. Single removal path now: `pendingRemoveLines` → `OnBarUpdate` (data thread). Eliminates cross-thread `RemoveDrawObject` race.
  - Fixed `DrawExpectedLines` startBar anchor: `Math.Max(1, CurrentBar)` produced invalid barsAgo=1 on bar 0. Extracted testable `KatTradeCalculator.GetLineStartBar(currentBar, max)` — never negative, never exceeds currentBar.
  - Extracted `KatTradeCalculator.LineTags[]` single source for all 6 draw-object tags (entry/SL/TP/BE/SL1/SL2). Draw and Remove now share the same list — removal can never drift from drawing when lines are added.
- **Verified Correct (no change needed)**:
  - Renko auto-detect (`BarsPeriodType.Renko` + name fallback) and brick H/L pricing.
  - Stop→Limit conversion: tick-rounded comparison, equality → Limit, both directions.
  - 1/2 Candle ON/OFF toggle → tick-rounded midpoint pricing.
- **Tests**: added `KatLineTagAndStartBarTests.cs` (9 tests): tag uniqueness/completeness, startBar clamp edges. Total: 82 (all passing).
- **Graphify**: AST-only update.

### [v0.28] — 2026-07-25
- **Bug Fixes & Logic Improvements**:
  - Fixed `KatTradeCalculator.CalculateAtmLevels`: early return on invalid trigger price (zero/negative) to prevent meaningless level calculations.
  - Fixed `KatTradeCalculator.CalculateFixedDistanceTriggerPrice`: negative distance ticks now clamped to absolute value, preventing inverted orders.
  - Simplified Renko candle price logic: removed redundant `Math.Max/Min(open/close)` branch since Renko bricks have no wicks and standard high/low logic produces identical results. Added test proving identity.
  - Fixed `KatTradeManager.OnBarUpdate` line removal: only removes lines on terminal order states (Filled/Cancelled/Rejected), no longer removes on transient states like PendingChange/PendingSubmit.
  - Fixed `KatTradeManager` pending flags race condition: `pendingRemoveLines` no longer clears `pendingDrawRequest`, so Cancel + New Order in same cycle correctly draws new lines.
  - Fixed `PlaceFixedDistanceOrder` fallback price: uses `cachedCurrentClose` instead of `cachedCurrentHigh` for more accurate current price estimation.
- **Test Suite Expansion**:
  - Added `KatOrderLifecycleTests.cs` (25 tests): ATM levels edge cases, half-candle with Renko, negative buffer/distance clamping, multicurrency tick size Stop/Limit boundary testing, price-only (StopMarket vs Limit) output validation across 0.01/0.05/0.10/0.25/0.50/1.0 tick sizes.
  - Updated `StressAndEdgeCaseTests.cs`: adjusted to match new negative distance clamping behavior.
  - Total test count: 73 (all passing).
- **Graphify**: AST-only update (no semantic extraction).

### [v0.27] — 2026-07-25
- **Agent Configuration Infrastructure**:
  - Created `AGENTS.md` with Caveman Ultra mode, Pony Tail (full) rules, Karpathy guidelines, Graphify best practices, auto GitHub connection, and mandatory version bump workflow.
  - Updated `RULES.md` to reference AGENTS.md and standardize version locations (VERSION constant + RELEASE_DATE constant).
  - Created `graphify-out/GRAPH_REPORT.md` with god nodes, community structure, and key dependency edges.
  - Added `.gitignore` entries for agent metadata and graphify-out.
- **Renko Chart & 1/2 Candle Trading Support**:
  - Added `cachedIsHalfCandle` toggle and `isRenkoChart` detection in `KatTradeManager.cs`.
  - Added `CalculateHalfCandlePrice()` and `CalculateCandlePrice()` methods to `KatTradeCalculator.cs` with Renko-aware high/low/close logic.
  - Added `btnHalfCandle` WPF toggle button in UI panel (lightblue = ON, darkgray = OFF).
  - Extended price caching to include `Open[]` and `Close[]` for full candle data.
- **Tick-Size Rounding for Order Type Determination**:
  - Added overload `DetermineOrderType(..., double tickSize, ...)` that rounds trigger/current price to nearest tick before comparison.
  - Prevents floating-point precision issues causing wrong order type (Stop vs Limit).
- **WPF Panel Visual Refinements**:
  - Made panel border `Transparent` with `BorderThickness = 0` (removed DodgerBlue border).
  - Fixed null-check in `CreateButton` event handler attachment.
  - Removed redundant `: Indicator` base class specifier in partial class.
- **Graphify Knowledge Graph**:
  - Initialized graph structure: god nodes (KatTradeManager, KatTradeCalculator, KatAtmXmlParser, KatTradeManagerUI) and community groupings.
- **Test Suite Expansion**:
  - Added `KatRenkoAndHalfCandleTests.cs` (15 tests covering half-candle midpoint, Renko box price, standard high/low, tick-rounded order type determination).
- **Graphify & Diary**:
  - Created `graphify-out/GRAPH_REPORT.md` with entity mapping.
  - Updated DIARY.md with this version history entry.

### [v0.24] - 2026-07-25
- **Short Line Drawing & Removal Fixes**:
  - `DrawExpectedLines()` now calls `RemoveExpectedLines()` FIRST before drawing new line objects. This guarantees old tags (e.g. `KAT_BE_LINE`, `KAT_SL1_LINE`, `KAT_SL2_LINE`) from previous orders are completely wiped when switching ATM templates or placing consecutive orders.
  - Added bar index protection for chart rendering: `startBarsAgo` is now dynamically bounded by `Math.Min(20, Math.Max(1, CurrentBar))`, preventing out-of-bounds errors on charts with fewer than 20 total bars.
  - Added immediate UI thread line clearing dispatch in `CancelAllOrders()` so chart lines erase instantly off-market or when idle without waiting for incoming ticks.
- **Pure Domain Decoupling for .NET SDK Unit Testing**:
  - Decoupled `KatTradeCalculator` from `NinjaTrader.Cbi` types (`OrderAction`, `OrderType`) by introducing domain enums `KatOrderAction` and `KatOrderType`.
  - Resolved AgileDotNet obfuscator `WindowsImpersonationContext` / `mscorlib` type load failure during .NET 8 unit testing.
  - Configured `KatTradeManager.Tests.csproj` with `<PlatformTarget>x64</PlatformTarget>`, `<TargetFramework>net8.0-windows</TargetFramework>`, and `<UseWPF>true</UseWPF>`.
  - Added `TestAssemblyInitializer.cs` to hook `AssemblyLoadContext.Default.Resolving` and `AppDomain.CurrentDomain.AssemblyResolve`.
- **Test Suite Expansion**:
  - Created `KatAtmXmlParserEdgeCaseTests.cs` to test multi-bracket ATM XML files, quantity summation, and zero-trigger handling.
  - Updated all 34 test cases to run natively under .NET SDK with 100% pass rate in 66 ms.
- **NinjaTrader 8 Deployment & Sync**:
  - Deployed updated codebase to `C:\Users\kieuanhtuan\Documents\NinjaTrader 8\bin\Custom\Indicators\`.

### [v0.23] - 2026-07-25
- **P0 Fix: Lines Not Drawing (Root Cause)**:
  - `Draw.Line()` was called from the WPF UI thread (button click handler), but NinjaTrader's Draw API only works on the NinjaScript data thread (`OnBarUpdate()`). All draw calls silently failed.
  - Implemented pending-draw pattern: `PlaceOrderInternal()` stores draw request in thread-safe fields (`pendingDrawRequest`, `pendingLevels`, `pendingEntryPrice`), `OnBarUpdate()` picks up the request and executes `DrawExpectedLines()` on the correct thread.
  - Same pattern for removal: `CancelAllOrders()` sets `pendingRemoveLines` flag, `OnBarUpdate()` calls `RemoveExpectedLines()` on data thread.
- **P0 Fix: Thread-Safe entryOrder Access**:
  - Made `entryOrder` volatile. Added terminal state detection (Filled/Cancelled/Rejected) in `OnBarUpdate()` to clear stale order references.
- **P1 Fix: PlaceFixedDistanceOrder Order Type**:
  - Replaced hardcoded `OrderType.StopMarket` with `KatTradeCalculator.DetermineOrderType()` call, consistent with `PlaceOrder()`. Fixed incorrect order type when trigger price is on wrong side of market.
- **New: Entry Price Line**:
  - Added gold `KAT_ENTRY_LINE` drawn at the trigger/entry price when placing orders.
- **Modular Split: WPF UI Extraction**:
  - Extracted ~300 lines of WPF UI code to `src/KatTradeManagerUI.cs` as `partial class`. Main file reduced to ~520 lines focused on trading logic.
- **New Tests**:
  - Added `KatLineDrawingTests.cs` (ATM levels with zero ticks, mixed params, draw count logic).
  - Added `FixedDistanceOrder_ShouldUseDetermineOrderType` test to `StressAndEdgeCaseTests.cs`.

### [v0.22] - 2026-07-24
- **R1 Bug Fixes & Dead Code Removal**:
  - Synchronized and bumped version string to 0.22 across `KatTradeManager.cs` (header comment & VERSION constant), `README.md`, and `DIARY.md`.
  - Removed unused `DefaultStopLossTicks` and `DefaultTakeProfitTicks` properties, `[NinjaScriptProperty]` attributes, defaults in `OnStateChange()`, and parameters in generated code overloads.
- **R2 Code Duplication Elimination**:
  - Extracted shared order execution, ATM strategy launch, expected level calculation, line drawing, and exception handling from `PlaceOrder()` and `PlaceFixedDistanceOrder()` into a private helper `PlaceOrderInternal()`.
- **R3 Thread Safety**:
  - Added `private readonly object priceLock = new object();`.
  - Synchronized all writes to `cachedCurrentHigh[]`, `cachedCurrentLow[]`, `cachedPrevHigh[]`, `cachedPrevLow[]`, and `cachedCurrentPrice` in `OnBarUpdate()` inside `lock (priceLock)`.
  - Synchronized all reads of these cached price fields in `PlaceOrder()`, `PlaceFixedDistanceOrder()`, `SyncCachedValues()`, etc., inside `lock (priceLock)`.
- **R4 Modular Refactoring & Pure Static Logic Extraction**:
  - Organized `KatTradeManager.cs` into clear `#region` blocks (Metadata & Variables, Indicator Lifecycle, WPF UI Construction & Handlers, Price Caching & OnBarUpdate, Order Execution & Trading Operations, ATM XML Template Parsing, Chart Visuals & Line Drawing, NinjaScript Properties, NinjaScript Generated Code).
  - Extracted pure domain logic static helper classes `src/KatTradeCalculator.cs` and `src/KatAtmXmlParser.cs`.
- **R5 Unit Testing Suite**:
  - Created test project `tests/KatTradeManager.Tests/KatTradeManager.Tests.csproj` with test files `KatTradeCalculatorTests.cs` and `KatAtmXmlParserTests.cs` (xUnit test suite).
  - Verified trigger price calculations, order type selection logic (StopMarket vs Limit), ATM level calculations, and ATM XML parsing.
- **R6 Versioning, Deployment & Sync**:
  - Deployed updated `KatTradeManager.cs`, `KatTradeCalculator.cs`, and `KatAtmXmlParser.cs` to `C:\Users\kieuanhtuan\Documents\NinjaTrader 8\bin\Custom\Indicators\`.

### [v0.21] - 2026-07-24
- **Fixed Input Field Keyboard Isolation & CS0111 Duplicate Region Error**:
  - Fixed CS0111/CS0102 compilation error caused by NinjaTrader's compiler appending a duplicate `#region NinjaScript generated code` block onto existing file signatures.
  - Removed artificial `ChartControl` key event re-raising from input textboxes (`Contracts`, `Buffer`, `Dist (Ticks)`).
  - Users can now click input fields and type values (e.g., `5`, `20`, `320`) directly without triggering NinjaTrader's chart symbol shortcut popup.
  - Added `Enter` key handling to save input parameter values instantly and return focus to the chart.

### [v0.20] - 2026-07-24
- **Added Fixed-Distance Pending Stop Buttons & Input Parameter**:
  - Added `Dist (Ticks):` input field (`DefaultDistanceTicks = 320`, corresponding to 80 points on NQ/MNQ) with key-event redirect to `ChartControl`.
  - Added `BUY +Distance` and `SELL -Distance` order execution buttons positioned directly under the `BUY Current` and `SELL Current` buttons.
  - Applied extra-deep dark desaturated button background colors (`RGB 16, 56, 30` for Buy and `RGB 75, 20, 24` for Sell) to maintain visual hierarchy.

### [v0.19] - 2026-07-24
- **Visual Polish & Ergonomics**:
  - Increased font size for `BUY Previous` and `SELL Previous` buttons to 12pt (`FontWeight.Bold`) for clearer focus and readability.
  - Replaced high-saturation bright colors with a sleek, desaturated, dark-mode friendly color palette (`Color.FromRgb`) to minimize eye fatigue during trading sessions.

### [v0.18] - 2026-07-24
- **Refined Button Layout & Keyboard Event Forwarding**:
  - Simplified button labels: removed dot emojis (`🟢`/`🔴`) and the word `Candle`, resulting in clean labels (`BUY Previous`, `BUY Current`, `SELL Previous`, `SELL Current`).
  - Doubled the height of `BUY Previous` and `SELL Previous` buttons to 48px for faster and easier clicking.
  - Enhanced key event forwarding on `txtQuantity` and `txtBuffer` by re-raising `Keyboard.KeyDownEvent` directly on `ChartControl` so NinjaTrader 8's native chart shortcut typing overlay (symbol/ticker search, interval changes) opens instantly.

### [v0.17] - 2026-07-24
- **Refined UI & Smart Order Execution Engine**:
  - Fixed CS0677 compilation error by changing `private volatile double cachedCurrentPrice` to `private double cachedCurrentPrice` (C# does not allow `volatile` modifier on 64-bit `double` type).
  - Updated order button labels: removed "STOP", renamed `Prev High`/`Prev Low` -> `Previous Candle` and `Curr High`/`Curr Low` -> `Current Candle`.
  - Reorganized buttons into a 2-column layout (Buy on left, Sell on right).
  - Implemented dynamic Stop vs Limit order auto-switching: orders default to `Pending Stop`, but automatically convert to `Limit` if current market price has crossed past the trigger position.
  - Added keyboard focus redirect (`PreviewKeyDown`) on `txtQuantity` and `txtBuffer` to pass key events to `ChartControl` and trigger NinjaTrader's native chart typing overlay.
  - Fixed ATM contract quantity synchronization to parse `<EntryQuantity>` and sum `<Quantity>` across `<Bracket>` XML elements instead of reading static `<DefaultQuantity>`.

### [v0.16] - 2026-07-24
- **Fixed CS1061 Compilation Error in AddGridRow**:
  - Changed `AddGridRow` parameter type from base `UIElement` to `FrameworkElement` to enable property access for `VerticalAlignment`, `HorizontalAlignment`, and `Height`.

### [v0.15] - 2026-07-24
- **Refined WPF Panel Layout & Auto-Synced Contracts Quantity**:
  - Replaced stacked panels with a 2-column WPF `Grid` (`paramGrid`) for perfect vertical/horizontal alignment of labels and input controls.
  - Set `FontSize = 10` for order buttons and reduced vertical heights to eliminate visual strain.
  - Automatically populates the `Contracts` input box from the `<DefaultQuantity>` tag of the selected ATM Template XML.
  - Converted `Cancel` and `Close` management buttons to a star-stretched Grid layout (`mgrGrid`) to align seamlessly with left/right panel margins.

### [v0.14] - 2026-07-24
- **Fixed double-to-int line width compilation error**:
  - Changed the visual line widths of BE, SL1, and SL2 target lines from double `1.5` to int `1` in `Draw.Line()`.

### [v0.13] - 2026-07-24
- **Implemented XML ATM Template Parsing & Automatic Chart Brackets Drawing**:
  - Automatically loads and parses settings from the selected ATM Template XML file.
  - Extracts parameters: SL, TP, Break-even (BE), and Trailing Steps (SL1, SL2).
  - Removed manual `SL (Ticks)` and `TP (Ticks)` textbox inputs from the WPF panel UI.
  - Automatically draws all 5 expected target/trailing trigger lines on the chart.
  - Added a clean auto-wipe system to instantly erase all lines when cancelling orders or closing positions.

### [v0.12] - 2026-07-24
- **Fixed compilation errors and integrated ATM strategy selection dropdown**:
  - Corrected `Draw.Line` parameter overload mismatch by passing `false` as the third parameter (`isAutoScale`).
  - Swapped the manual TextBox ATM template string input with a ComboBox dropdown (`ATM:`) populated automatically from the NinjaTrader saved ATM files directory (`Documents\NinjaTrader 8\templates\AtmStrategy`).

### [v0.11] - 2026-07-24
- **Fixed Button Press Errors (barsAgo & NullReferenceExceptions) & Synced Version**:
  - Replaced direct UI-thread `Highs`/`Lows` calls with thread-safe volatile caches updated via `OnBarUpdate` on the data thread.
  - Added verification that `basePrice > 0` before submitting stop orders to prevent default-price execution.
  - Implemented null reference protection on the returned `entryOrder` from `account.CreateOrder` before calling `account.Submit`.
  - Fixed `NullReferenceException` inside `CreateOrder` by replacing `DateTime.MaxValue` with `NinjaTrader.Core.Globals.MaxDate` (since SQL-equivalent date conversion of 9999 overflows/crashes in NinjaTrader).
  - Overwrote the running NinjaTrader 8 `KatTradeManager.cs` indicator file to resolve the version display mismatch (running v0.10 vs codebase v0.11).

### [v0.07] - 2026-07-24
- **Added `Show Control Panel` (`IsPanelVisible`) Property (Default: True)**:
  - Exposed `Show Control Panel` checkbox parameter in Indicator Settings dialog to easily toggle panel visibility on/off.
  - Fixed cross-instance deletion bug where instance A's destructor was deleting instance B's panel on Dispatcher execution.
  - Redeployed `KatTradeManager.cs` to `Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`.

### [v0.06] - 2026-07-24
- **Fixed 1-Second Disappearing Bug via Persistent `chartGrid` Container**:
  - Identified root cause: NinjaTrader's `ChartTrader` runs a 1-second internal UI refresh loop for PnL and position displays, which wipes out manually injected controls inside `ChartTrader`'s private children.
  - Attached `panelBorder` directly to `ChartControl.Parent` (`chartGrid`) with `SetZIndex = 9999` and `Grid.SetColumnSpan = 3`.
  - Added full mouse Drag-and-Drop capability (`MouseLeftButtonDown`, `MouseMove`, `MouseLeftButtonUp`) so users can move the control panel anywhere on the chart canvas.
  - Redeployed `KatTradeManager.cs` to `Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`.

### [v0.05] - 2026-07-24
- **Fixed WPF Panel Flashing & Disappearing on Re-adding Indicator**:
  - Target Vertical `StackPanel` instead of arbitrary first `StackPanel` (which picked horizontal sub-rows in ChartTrader).
  - Added Tag `KatTradeManagerPanel` and implemented `RemoveExistingPanels()` to clean up duplicate panels across instances.
  - Delayed control binding using `DispatcherPriority.Loaded` and added automatic re-attachment check on `State.Historical` & `State.Realtime`.
  - Redeployed `KatTradeManager.cs` to `Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`.

### [v0.04] - 2026-07-24
- **Fixed CS1061 `SetZIndex` Namespace Collision**:
  - Replaced ambiguous `Panel.SetZIndex(...)` with fully qualified `System.Windows.Controls.Panel.SetZIndex(...)` to prevent collision with NinjaScript's `Panel` integer property.
  - Redeployed clean `KatTradeManager.cs` to `Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`.

### [v0.03] - 2026-07-24
- **ChartTrader Integration & UI Placement**:
  - Embedded WPF panel directly into ChartTrader right-side panel below position display.
  - Added visual tree searching (`GetChartTrader()`, `FindVisualChild<T>()`) to locate ChartTrader container.
  - Added fallback docking to bottom-right of chart with `Panel.SetZIndex = 9999` so controls are never hidden.
  - Redeployed `KatTradeManager.cs` to `Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`.

### [v0.02] - 2026-07-24
- **NinjaTrader 8 API Fixes**:
  - Fixed `CS0118`: Removed indicator-incompatible `OrderFillResolution` assignment.
  - Fixed `CS0117`: Changed `Account.AllAccounts` to valid NT8 `Account.All`.
  - Fixed `CS1501`: Updated `Account.CreateOrder` 12-argument overload signature including `DateTime.MaxValue`.
  - Fixed `CS1061`: Corrected `order.State` to `order.OrderState`.
  - Fixed `CS1501`: Updated `Account.Change` overload to pass array of orders after mutating `StopPrice`.
  - Redeployed clean `KatTradeManager.cs` to `Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`.

### [v0.01] - 2026-07-24
- **Initial Release & Infrastructure**:
  - Created initial repository layout and `.gitignore`.
  - Added `RULES.md` & `.gemini/rules/project_rules.md` for automated versioning and release workflows.
  - Implemented `KatTradeManager.cs` with WPF control panel overlay.
  - Added pending stop placement at High/Low of Previous and Current candles across 30s, 1m, 2m timeframes.
  - Implemented Trailing Stop Loss engine and quick position management actions.
  - Deployed directly to NinjaTrader 8 (`C:\Users\kieuanhtuan\Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`).

### [v0.96] - 2026-08-01
- **KAT folder grouping**: moved indicator under `KAT` group in NT8 Add Indicator dialog.
  - Changed namespace of 6 partial files (main, UI, OrderOps, FreezeTrail, DailyRisk, Properties) from `NinjaTrader.NinjaScript.Indicators` to `NinjaTrader.NinjaScript.Indicators.KAT` (NT8 groups indicators by namespace, mirroring the folder chosen at creation).
  - Deploy script now copies sources into `bin\Custom\Indicators\KAT\` and removes stale flat-root copies (NT8 compiles recursively — duplicate class otherwise).
  - Pure files (`KatTradeCalculator`, `KatAtmXmlParser`) stay in parent namespace — parent-namespace lookup keeps main class + tests working unchanged.
- **Graphify entity mapping**: `KatTradeManager` (namespace `...Indicators.KAT`), `Deploy-NT8.ps1`.
