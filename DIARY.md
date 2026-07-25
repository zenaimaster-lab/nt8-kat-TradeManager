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
- **Component**: `KatTradeManager` (NinjaTrader Indicator)
- **UI Framework**: WPF (`System.Windows.Controls`) on `ChartControl.Parent`
- **Execution Target**: `NinjaTrader.Cbi.Account` (`Sim301` or Active Account)
- **Supported Timeframes**: `Chart TF` (Bars 0), `30s` (Bars 1), `1m` (Bars 2), `2m` (Bars 3)

---

## 📜 Version History & Change Log

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
