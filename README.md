# NT8 Kat TradeManager

**Current Version**: `v0.31` (Released: `2026-07-25`)

An advanced TradeManager Indicator for **NinjaTrader 8 (NT8)** designed for fast execution, candle-based pending stop orders, and dynamic risk management.

## 🚀 Features

- **On-Chart WPF Control Panel**: Interactive buttons and dropdown selectors directly on your NinjaTrader 8 charts.
- **EMA 34 & EMA 89 Touch/Cross Orders**: Place Buy/Sell orders based on the most recent candle touching or crossing EMA 34 or EMA 89 lines.
- **1/2 Candle Mode Toggle**: Half-candle midpoint order pricing, automatically converting orders to StopMarket or Limit as appropriate.
- **Candle-Based Pending Stops**:

  - Place **Buy Stop** / **Sell Stop** at High/Low of **Current Candle** or **Previous Candle**.
  - Multi-timeframe support (30s, 1m, 2m candles).
  - Configurable **Buffer (Ticks)** padding to set orders above/below highs/lows.
- **Automated Risk Management**:
  - Automatically loads and applies your native **NT8 ATM Strategy templates** (selectable directly via a dropdown menu).
  - Submits brackets server-side for maximum reliability and protection against network lag.
- **Visual SL/TP Levels**: Renders dashed horizontal lines on the chart (Red for SL, Green for TP) while pending orders are active.
- **One-Click Order Cancellation & Position Management**: Quick buttons to Close All Positions or Cancel Pending Orders, instantly cleaning up visual objects.

## 🛠️ Installation in NinjaTrader 8

1. Open **NinjaTrader 8**.
2. Go to **Tools** -> **NinjaScript Editor**.
3. Open or import `KatTradeManager.cs` under `Indicators`.
4. Press **F5** to Compile.
5. Add `KatTradeManager` to any NT8 Chart.

## 📜 License

MIT
