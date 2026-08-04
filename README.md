# NT8 Kat TradeManager

**Current Version**: `v0.98` (Released: `2026-08-03`)

An advanced TradeManager Indicator for **NinjaTrader 8 (NT8)** designed for fast execution, candle-based pending stop orders, and dynamic risk management.

## 🚀 Features

- **On-Chart WPF Control Panel**: Interactive buttons with SELL on left / BUY on right and dropdown selectors directly on your NinjaTrader 8 charts.
- **Configurable HUD (default InChart / optional ChartTrader)**: `HUD Left Inset (px)` defaults to 10px for fresh placement; `HUD Drag Enabled` defaults ON and can lock HUD in place. Background drag works in both modes, keeps at least 40px visible, preserves user position across watchdog re-attachment, and leaves buttons/text controls clickable.
- **Freeze Trail Control (`Freeze Trail: OFF` / `⚡ Freeze Trail: ON`)**: Full-width dark gray HUD button directly above Close/flatten. ON abandons the ATM instead of racing it: every ATM protective exit on the instrument — including brackets of 2nd+ entries and Chart Trader ATMs — is cancelled and replaced by one static `KAT_FRZ_SL` (plus OCO `KAT_FRZ_TP` when the ATM had a target) at the tightest captured stop and farthest captured target, sized to live position quantity. Nothing trails afterwards and no code re-pushes prices, so BE, Swing SL, and chart drags of the Stop Loss stay where you put them. Quantity follows scale-in/scale-out; static exits are cancelled once the position stays flat. OFF stops taking over new brackets and keeps existing static exits.
- **ATM `None` selection**: First item of the HUD ATM dropdown submits plain orders without any ATM strategy, matching NT8 Chart Trader's own None mode; with None selected the HUD no longer merges, resizes, or cancels protective orders it does not own.
- **ATM Quick Set buttons (A–F)**: Row of 6 one-click buttons directly below the ATM dropdown; each instantly selects its assigned ATM template (the dropdown updates to match). Exactly one shows amber (ON) — the one owning the currently selected ATM — the rest stay gray; None turns all OFF. Button labels (max 3 chars, default A–F) and assigned ATMs (dropdown lists) are configured in Indicator Settings under "ATM Quick Sets".
- **Pending Stop-Limit Control (`Stop-Limit: OFF` / `Stop-Limit: ON`)**: Full-width Freeze Trail-style button below Freeze Trail; converts valid candle and fixed-distance pending StopMarket entries to one-tick StopLimit entries when enabled.
- **ATM Bracket Control (`ATM Bracket: MERGE` / `ATM Bracket: SPLIT`)**: Button below Stop-Limit; MERGE reconciles all scale-in and scale-out activity to one canonical SL plus one TP at live position quantity, while SPLIT preserves independent bracket sets per entry.
- **Serialized account operations**: Submit, Change, and Cancel requests run through one state-aware FIFO gate; Close/flatten cancels working orders before submitting its close order.
- **ATM startup lifecycle hardening**: ATM API calls release queue ownership after return, while MERGE defers flat cleanup until first-entry startup resolves so initial SL/TP brackets are not cancelled prematurely.
- **Account-wide Close/flatten**: cancels all active account orders, then submits one market close per open position across every instrument; Revert and daily-risk remain instrument-scoped.
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
- **Runtime feedback**: Permanent two-line HUD status slot with transparent background reports EMA Place/Angle rejection reasons and order submission status; NinjaScript Output logs account order-state transitions.

## 🛠️ Installation in NinjaTrader 8

1. Open **NinjaTrader 8**.
2. Go to **Tools** -> **NinjaScript Editor**.
3. Open or import `KatTradeManager.cs` under `Indicators`.
4. Press **F5** to Compile.
5. Add `KatTradeManager` to any NT8 Chart.

## 📜 License

MIT
