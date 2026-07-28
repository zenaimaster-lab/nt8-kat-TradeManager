# NT8 Kat TradeManager

**Current Version**: `v0.74` (Released: `2026-07-28`)

An advanced TradeManager Indicator for **NinjaTrader 8 (NT8)** designed for fast execution, candle-based pending stop orders, and dynamic risk management.

## 🚀 Features

- **On-Chart WPF Control Panel**: Interactive buttons with SELL on left / BUY on right and dropdown selectors directly on your NinjaTrader 8 charts.
- **Draggable In-Chart HUD (default)**: Opens at bottom-left inside chart, keeps at least 40px visible while dragging, and preserves user position across watchdog re-attachment. ChartTrader docking remains selectable.
- **Freeze Trail Control (`Freeze Trail: OFF` / `⚡ Freeze Trail: ON`)**: Full-width dark gray HUD button positioned directly above Close/flatten to freeze active trailing Stop Loss in place while preserving working SL and TP orders.
- **Pending Stop-Limit Control (`Stop-Limit: OFF` / `Stop-Limit: ON`)**: Full-width Freeze Trail-style button below Freeze Trail; converts valid candle and fixed-distance pending StopMarket entries to one-tick StopLimit entries when enabled.
- **ATM Bracket Control (`ATM Bracket: MERGE` / `ATM Bracket: SPLIT`)**: Button below Stop-Limit; default MERGE uses the first active ATM stop/target bracket for subsequent scale-in entries and resizes that bracket after fills, while SPLIT starts one ATM bracket set per entry.
- **Swing Stop Loss Shift Controls (`◀ SL` & `SL ▶`)**: Dynamic gray HUD buttons below Section 2 to shift active Stop Loss orders back to historical Swing Lows (Long) or Swing Highs (Short) step-by-step with full Redo functionality.
- **Daily Max Drawdown & Daily Max Profit Controls**: Side-by-side HUD toggle buttons (default ON, darker purple background `#3A136B`) with 6:00 PM NY session reset. Replaces entry blocking and auto-flattens positions/orders on breach with instant HUD toggle reactivity.
- **Configurable Hotkeys**: Assign WPF Hotkeys for all 15 order types directly in Indicator Settings, overriding default NT hotkeys (`e.Handled = true`) with input textbox detection & repeat protection.
- **EMA Place & EMA Angle Filters**: Side-by-side HUD toggle buttons (default OFF, very dark blue background) enforcing 5m multi-EMA position rules only for direct candle/fixed-distance Buy/Sell entry buttons when enabled.
- **EMA 34 & EMA 89 Touch/Cross Orders**: Place Buy/Sell orders based on the most recent candle touching or crossing EMA 34 or EMA 89 lines.


- **Partial Candle Mode Toggle**: Configurable pullback percentage (default 30% from High/Low), automatically determining StopMarket vs Limit order types.
- **BUY / SELL Market Orders**: Instantly execute Market orders with selected ATM Strategy; ATM entries use required `Entry` signal name.
- **Breakeven (BE) & Revert Position Controls**: Move active Stop Loss to Breakeven (+buffer ticks) or instantly reverse open position.
- **Candle-Based Pending Stops**:
  - Place **Buy Stop** / **Sell Stop** at High/Low of **Current Candle** or **Previous Candle**.
  - Multi-timeframe support (30s, 1m, 2m candles).
  - Configurable **Buffer (Ticks)** padding to set orders above/below highs/lows.
- **Automated Risk Management**:
  - Automatically loads and applies your native **NT8 ATM Strategy templates** (selectable directly via a dropdown menu).
  - Submits brackets server-side for maximum reliability and protection against network lag.
- **Visual SL/TP Levels**: Renders dashed horizontal lines on the chart (Red for SL, Green for TP) while pending orders are active.
- **One-Click Close/Flatten & Order Management**: Full-width Close/Flatten button to close active positions and cancel pending orders.
- **Runtime feedback**: Permanent black two-line HUD status slot reports EMA Place/Angle rejection reasons and order submission status; NinjaScript Output logs account order-state transitions.

## 🛠️ Installation in NinjaTrader 8

1. Open **NinjaTrader 8**.
2. Go to **Tools** -> **NinjaScript Editor**.
3. Open or import `KatTradeManager.cs` under `Indicators`.
4. Press **F5** to Compile.
5. Add `KatTradeManager` to any NT8 Chart.

## 📜 License

MIT
