# Graph Report - nt8-kat-TradeManager  (2026-08-08)

## Corpus Check
- 50 files · ~75,712 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 850 nodes · 1675 edges · 57 communities (30 shown, 27 thin omitted)
- Extraction: 83% EXTRACTED · 17% INFERRED · 0% AMBIGUOUS · INFERRED: 277 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `0bbfa533`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 43|Community 43]]

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 115 edges
2. `KatTradeManager` - 97 edges
3. `KatTradeManager` - 50 edges
4. `KatTradeCalculator` - 49 edges
5. `KatCalculatorGapTests` - 45 edges
6. `KatTradeManager` - 31 edges
7. `KatOrderLifecycleTests` - 30 edges
8. `KatTradeManager` - 25 edges
9. `KatTradeManager` - 23 edges
10. `KatAuditGapTests` - 23 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `string`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `Border`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.AccountInfo.cs
- `KatTradeManager` --references--> `StackPanel`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `ComboBox`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DispatcherTimer`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs

## Communities (57 total, 27 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.1
Nodes (8): OrderAction, Queue, KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 1 - "Community 1"
Cohesion: 0.08
Nodes (3): KatTradeManager, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 4 - "Community 4"
Cohesion: 0.08
Nodes (16): Border, Button, Canvas, ControlTemplate, IInputElement, Point, Run, SolidColorBrush (+8 more)

### Community 6 - "Community 6"
Cohesion: 0.12
Nodes (3): Dictionary, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 9 - "Community 9"
Cohesion: 0.12
Nodes (10): Account, AtmLevels, ComboBox, DispatcherTimer, EMA, Grid, Indicator, KatTradeManager (+2 more)

### Community 14 - "Community 14"
Cohesion: 0.18
Nodes (4): ConcurrentDictionary<Type, Func<object, double>>, PropertyInfo>, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 18 - "Community 18"
Cohesion: 0.14
Nodes (16): AccountOperationType, Action, bool, Brush, int, List, AtmScaleInState, Order (+8 more)

### Community 22 - "Community 22"
Cohesion: 0.16
Nodes (4): AtmTemplateNameConverter, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, TypeConverter

### Community 30 - "Community 30"
Cohesion: 0.28
Nodes (4): HashSet, object, KatAtmTemplateService, NinjaTrader.NinjaScript.Indicators

### Community 31 - "Community 31"
Cohesion: 0.22
Nodes (8): DateTime, double, MarketPosition, OrderType, DisciplineState, FreezeExitCapture, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT

### Community 35 - "Community 35"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **64 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `AtmLevels`, `NinjaTrader.NinjaScript.Indicators` (+59 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **27 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 4` to `Community 32`, `Community 0`, `Community 7`, `Community 9`, `Community 13`, `Community 15`, `Community 16`, `Community 18`, `Community 30`, `Community 31`?**
  _High betweenness centrality (0.094) - this node is a cross-community bridge._
- **Why does `KatTradeManager` connect `Community 0` to `Community 1`, `Community 6`, `Community 7`, `Community 18`, `Community 30`, `Community 31`?**
  _High betweenness centrality (0.085) - this node is a cross-community bridge._
- **Why does `string` connect `Community 18` to `Community 0`, `Community 1`, `Community 2`, `Community 4`, `Community 9`, `Community 14`?**
  _High betweenness centrality (0.067) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _64 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._