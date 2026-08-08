# Graph Report - nt8-kat-TradeManager  (2026-08-08)

## Corpus Check
- 54 files · ~77,230 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 921 nodes · 1904 edges · 55 communities (26 shown, 29 thin omitted)
- Extraction: 79% EXTRACTED · 21% INFERRED · 0% AMBIGUOUS · INFERRED: 395 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `b7322204`
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
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
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
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 115 edges
2. `KatTradeManager` - 97 edges
3. `KatTradeManager` - 52 edges
4. `KatTradeCalculator` - 49 edges
5. `KatCalculatorGapTests` - 45 edges
6. `KatTradeManager` - 31 edges
7. `KatOrderLifecycleTests` - 30 edges
8. `KatTradeManager` - 26 edges
9. `KatTradeManager` - 25 edges
10. `KatTradeManager` - 23 edges

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

## Communities (55 total, 29 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.05
Nodes (13): Border, Button, Canvas, IInputElement, Point, Run, SolidColorBrush, KatTradeManager (+5 more)

### Community 1 - "Community 1"
Cohesion: 0.08
Nodes (5): Queue, KatTradeManager, KatTradeManager, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 2 - "Community 2"
Cohesion: 0.09
Nodes (7): ConcurrentDictionary<Type, Func<object, double>>, PropertyInfo>, KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 3 - "Community 3"
Cohesion: 0.07
Nodes (9): ControlTemplate, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager (+1 more)

### Community 7 - "Community 7"
Cohesion: 0.13
Nodes (3): Dictionary, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 8 - "Community 8"
Cohesion: 0.12
Nodes (9): Account, AtmLevels, ComboBox, DispatcherTimer, Grid, Indicator, KatTradeManager, StackPanel (+1 more)

### Community 14 - "Community 14"
Cohesion: 0.15
Nodes (15): bool, DateTime, double, int, MarketPosition, OrderAction, OrderType, KatAtmBracketOrder (+7 more)

### Community 15 - "Community 15"
Cohesion: 0.14
Nodes (6): Brush, AtmTemplateNameConverter, KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, TypeConverter

### Community 25 - "Community 25"
Cohesion: 0.17
Nodes (10): AccountOperationType, Action, List, AtmScaleInState, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, Order, AccountOperationRequest (+2 more)

### Community 30 - "Community 30"
Cohesion: 0.28
Nodes (4): HashSet, object, KatAtmTemplateService, NinjaTrader.NinjaScript.Indicators

### Community 31 - "Community 31"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **67 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `AtmLevels`, `NinjaTrader.NinjaScript.Indicators` (+62 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **29 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 0` to `Community 2`, `Community 3`, `Community 40`, `Community 8`, `Community 14`, `Community 15`, `Community 25`, `Community 30`?**
  _High betweenness centrality (0.089) - this node is a cross-community bridge._
- **Why does `KatTradeManager` connect `Community 1` to `Community 0`, `Community 2`, `Community 7`, `Community 8`, `Community 14`, `Community 25`, `Community 30`?**
  _High betweenness centrality (0.079) - this node is a cross-community bridge._
- **Why does `string` connect `Community 14` to `Community 0`, `Community 1`, `Community 2`, `Community 3`, `Community 4`, `Community 8`, `Community 15`, `Community 25`?**
  _High betweenness centrality (0.069) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _67 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._