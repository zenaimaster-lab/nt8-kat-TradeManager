# NT8 Kat TradeManager

**Current Version**: `v0.01` (Released: `2026-07-24`)

An advanced TradeManager Indicator for **NinjaTrader 8 (NT8)** designed for fast execution, candle-based pending stop orders, and dynamic risk management.

## 🚀 Features

- **On-Chart WPF Control Panel**: Interactive buttons directly on your NinjaTrader 8 charts.
- **Candle-Based Pending Stops**:
  - Place **Buy Stop** / **Sell Stop** at High/Low of **Current Candle** or **Previous Candle**.
  - Multi-timeframe support (30s, 1m, 2m candles).
- **Automated Risk Management**:
  - Auto Stop Loss (SL) & Take Profit (TP) in ticks/points.
  - Dynamic **Trailing Stop Loss** engine.
- **One-Click Order Cancellation & Position Management**: Quick buttons to Close All Positions or Cancel Pending Orders.

## 🛠️ Installation in NinjaTrader 8

1. Open **NinjaTrader 8**.
2. Go to **Tools** -> **NinjaScript Editor**.
3. Open or import `KatTradeManager.cs` under `Indicators`.
4. Press **F5** to Compile.
5. Add `KatTradeManager` to any NT8 Chart.

## 📜 License

MIT
