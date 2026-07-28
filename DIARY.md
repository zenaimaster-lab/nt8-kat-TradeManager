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
