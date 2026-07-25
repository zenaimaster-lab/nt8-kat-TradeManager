# Graphify Knowledge Graph — nt8-kat-TradeManager

## God Nodes (Top-Level Components)

| Node | Type | Description |
|------|------|-------------|
| KatTradeManager | Component | NT8 Indicator, main entry point (partial: KatTradeManager.cs + KatTradeManagerUI.cs) |
| KatTradeCalculator | Component | Static domain logic: price calc, trigger, order type, ATM levels |
| KatAtmXmlParser | Component | ATM XML template parser |
| KatTradeManagerUI | Component | WPF UI panel (partial class) |

## Community Structure

### Community 1: Trading Engine
- KatTradeManager (entry point)
- KatTradeCalculator (pure logic)
- Order execution pipeline
- Price caching & OnBarUpdate

### Community 2: UI Layer
- KatTradeManagerUI (WPF panel)
- ChartControl integration
- User input controls

### Community 3: ATM Strategy
- KatAtmXmlParser
- ATM template file I/O
- Expected line drawing

## Key Dependencies

| From | To | Edge Type | Confidence |
|------|-----|-----------|------------|
| KatTradeManager | KatTradeCalculator | USES | EXTRACTED |
| KatTradeManager | KatAtmXmlParser | USES | EXTRACTED |
| KatTradeManager | KatTradeManagerUI | EXTENDS | EXTRACTED |
| KatTradeManager | NinjaTrader.Cbi.Account | EXECUTES_ON | EXTRACTED |
| KatTradeManager | NinjaTrader.Gui.Chart | RENDERS_ON | EXTRACTED |

## Data Flow
1. User input (UI) → cached values → OnBarUpdate (data thread)
2. OnBarUpdate → price caches (thread-safe, lock)
3. PlaceOrder → CalculateCandlePrice → CalculateTriggerPrice → DetermineOrderType → PlaceOrderInternal
4. PlaceOrderInternal → pending draw flags → OnBarUpdate → DrawExpectedLines
5. LoadAtmTemplateSettings → KatAtmXmlParser.ParseFile → ATM params

## Current Version
v0.27 (2026-07-25)
