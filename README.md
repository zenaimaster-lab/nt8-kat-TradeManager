# NT8 Kat TradeManager

**Current Version**: `v1.23` (Released: `2026-08-06`)

An advanced TradeManager Indicator for **NinjaTrader 8 (NT8)** designed for fast execution, candle-based pending stop orders, and dynamic risk management.

## 🚀 Features

- **On-Chart WPF Control Panel**: Interactive buttons with SELL on left / BUY on right and dropdown selectors directly on your NinjaTrader 8 charts.
- **Configurable HUD (default InChart / optional ChartTrader)**: `HUD Left Inset (px)` defaults to 10px for fresh placement; `HUD Drag Enabled` defaults ON and can lock HUD in place. Background drag works in both modes, keeps at least 40px visible, preserves user position across watchdog re-attachment, and leaves buttons/text controls clickable.
- **ATM `None` selection**: First item of the HUD ATM dropdown submits plain orders without any ATM strategy, matching NT8 Chart Trader's own None mode; with None selected the HUD no longer merges, resizes, or cancels protective orders it does not own.
- **ATM Quick Set buttons (A–F)**: Row of 6 one-click buttons directly below the ATM dropdown; each instantly selects its assigned ATM template (the dropdown updates to match). Exactly one shows amber (ON) — the one owning the currently selected ATM — the rest stay gray; None turns all OFF. Button labels (max 3 chars, default A–F) and assigned ATMs (dropdown lists) are configured in Indicator Settings under "ATM Quick Sets".
- **ATM entry quantity sync**: BUY/SELL buttons and hotkeys use selected ATM template's `EntryQuantity` (or summed bracket quantities); ATM `None` falls back to `Default Quantity`.
- **Pending Stop-Limit Control (`Stop-Limit: OFF` / `Stop-Limit: ON`)**: Toggle button paired side-by-side with EMA Place in the bottom ON/OFF toggles section; converts valid candle and EMA-touch pending StopMarket entries to one-tick StopLimit entries when enabled.
- **ATM Bracket MERGE (always on)**: Every trade automatically reconciles all scale-in and scale-out activity to one canonical SL plus one TP at live position quantity; the former MERGE/SPLIT toggle was removed so bracket merging can never be disabled. Merge is OCO-safe: quantities consolidate only within one complete stop+target OCO pair, so reconciliation can never trigger broker OCO cascade cancels or cancel/recreate storms.
- **Serialized account operations**: Submit, Change, and Cancel requests run through one state-aware FIFO gate; Cancel releases the gate only after terminal confirmation, and Close/flatten cancels working orders before submitting its close order.
- **ATM startup lifecycle hardening**: ATM API calls release queue ownership after return, while MERGE defers flat cleanup until first-entry startup resolves so initial SL/TP brackets are not cancelled prematurely.
- **Account-wide Close/flatten**: cancels all active account orders, then submits one market close per open position across every instrument; Revert and daily-risk remain instrument-scoped.
- **Swing Stop Loss Shift Controls (`◀ SL` & `SL ▶`)**: Dynamic gray HUD buttons below Section 2 to shift active Stop Loss orders back to historical Swing Lows (Long) or Swing Highs (Short) step-by-step with full Redo functionality.
- **Daily Max Drawdown & Daily Max Profit Controls**: Side-by-side HUD toggle buttons (darker purple background `#3A136B`) with 6:00 PM NY session reset. Max DD always starts ON every session regardless of the previous toggle; Max Profit keeps its persisted state. Replaces entry blocking and auto-flattens positions/orders on breach with instant HUD toggle reactivity.
- **Daily Risk Quick Set buttons (1–6)**: One row below Max DD / Max Profit; each button writes its configured Max DD and Max Profit pair without changing either ON/OFF toggle. Labels and six value pairs are configurable under "Daily Risk Quick Sets"; selected pair uses darker purple `#240748`.
- **Configurable Hotkeys**: Assign WPF Hotkeys for all 13 order types directly in Indicator Settings, overriding default NT hotkeys (`e.Handled = true`) with repeat protection.
- **EMA Place Filter**: HUD toggle button (default OFF, very dark blue background, paired with Stop-Limit) enforcing 5m multi-EMA position rules only for direct candle Buy/Sell entry buttons when enabled.
- **EMA 34 & EMA 89 Touch/Cross Orders**: Place Buy/Sell orders based on the most recent candle touching or crossing EMA 34 or EMA 89 lines.


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
- **Runtime feedback**: Permanent single-line HUD status slot with transparent background reports EMA Place rejection reasons and order submission status; NinjaScript Output logs account order-state transitions.

## 🛠️ Installation in NinjaTrader 8

1. Open **NinjaTrader 8**.
2. Go to **Tools** -> **NinjaScript Editor**.
3. Open or import `KatTradeManager.cs` under `Indicators`.
4. Press **F5** to Compile.
5. Add `KatTradeManager` to any NT8 Chart.

## 📜 License

MIT
