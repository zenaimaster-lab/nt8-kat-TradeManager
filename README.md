# NT8 Kat TradeManager

**Current Version**: `v0.64` (Released: `2026-07-26`)

An advanced TradeManager Indicator for **NinjaTrader 8 (NT8)** designed for fast execution, candle-based pending stop orders, and dynamic risk management.

## 🚀 Features

- **On-Chart WPF Control Panel**: Interactive buttons with SELL on left / BUY on right and dropdown selectors directly on your NinjaTrader 8 charts.
- **Bottom-Anchored Floating ChartTrader HUD**: Anchored to bottom row of ChartTrader Grid with high Z-Index (`99999`). Docks at very bottom during normal height, floating upward over native controls when chart window height is reduced.
- **Freeze Trail Control (`Freeze Trail: OFF` / `⚡ Freeze Trail: ON`)**: Full-width dark gray HUD button positioned directly above Close/flatten to freeze active trailing Stop Loss in place while preserving working SL and TP orders.
- **Swing Stop Loss Shift Controls (`◀ SL` & `SL ▶`)**: Dynamic gray HUD buttons below Section 2 to shift active Stop Loss orders back to historical Swing Lows (Long) or Swing Highs (Short) step-by-step with full Redo functionality.
- **Daily Max Drawdown & Daily Max Profit Controls**: Side-by-side HUD toggle buttons (default ON, darker purple background `#3A136B`) with 6:00 PM NY session reset. Replaces entry blocking and auto-flattens positions/orders on breach with instant HUD toggle reactivity.
- **Configurable Hotkeys**: Assign WPF Hotkeys for all 15 order types directly in Indicator Settings, overriding default NT hotkeys (`e.Handled = true`) with input textbox detection & repeat protection.
- **EMA Place & EMA Angle Filters**: Side-by-side HUD toggle buttons (default ON, very dark blue background) enforcing 5m multi-EMA position rules and slope angle degree thresholds before order placement.
- **EMA 34 & EMA 89 Touch/Cross Orders**: Place Buy/Sell orders based on the most recent candle touching or crossing EMA 34 or EMA 89 lines.


- **Partial Candle Mode Toggle**: Configurable pullback percentage (default 30% from High/Low), automatically determining StopMarket vs Limit order types.
- **BUY / SELL Market Orders**: Instantly execute Market orders with selected ATM Strategy.
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

## 🛠️ Installation in NinjaTrader 8

1. Open **NinjaTrader 8**.
2. Go to **Tools** -> **NinjaScript Editor**.
3. Open or import `KatTradeManager.cs` under `Indicators`.
4. Press **F5** to Compile.
5. Add `KatTradeManager` to any NT8 Chart.

## 📜 License

MIT
