# Graph Report - nt8-kat-TradeManager  (2026-08-07)

## Corpus Check
- 39 files · ~63,366 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 738 nodes · 1434 edges · 41 communities (17 shown, 24 thin omitted)
- Extraction: 86% EXTRACTED · 14% INFERRED · 0% AMBIGUOUS · INFERRED: 203 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `6f1d7d3c`
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

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 103 edges
2. `KatTradeManager` - 97 edges
3. `KatTradeManager` - 49 edges
4. `KatTradeCalculator` - 48 edges
5. `KatCalculatorGapTests` - 45 edges
6. `KatTradeManager` - 31 edges
7. `KatOrderLifecycleTests` - 30 edges
8. `KatAuditGapTests` - 23 edges
9. `KatTradeManager` - 22 edges
10. `KatTradeCalculatorTests` - 22 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `ComboBox`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DispatcherTimer`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  src/KatTradeManager.OrderOps.cs → tests/KatTradeManager.Tests/KatEntryShiftTests.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  src/KatTradeManagerUI.cs → tests/KatTradeManager.Tests/KatEntryShiftTests.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  src/KatTradeManager.FreezeTrail.cs → tests/KatTradeManager.Tests/KatEntryShiftTests.cs

## Communities (41 total, 24 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.06
Nodes (9): Button, Canvas, IInputElement, Point, SolidColorBrush, KatTradeManager, TextBlock, UIElement (+1 more)

### Community 1 - "Community 1"
Cohesion: 0.05
Nodes (37): Account, AccountOperationType, Action, AtmLevels, bool, Border, ComboBox, DateTime (+29 more)

### Community 2 - "Community 2"
Cohesion: 0.09
Nodes (4): OrderAction, Queue, KatTradeManager, KatTradeManager

### Community 3 - "Community 3"
Cohesion: 0.1
Nodes (4): KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager

### Community 7 - "Community 7"
Cohesion: 0.12
Nodes (3): Dictionary, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 12 - "Community 12"
Cohesion: 0.14
Nodes (6): Brush, AtmTemplateNameConverter, KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, TypeConverter

### Community 24 - "Community 24"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **58 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `Border`, `StackPanel` (+53 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **24 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 2` to `Community 0`, `Community 1`, `Community 3`, `Community 7`?**
  _High betweenness centrality (0.105) - this node is a cross-community bridge._
- **Why does `KatTradeManager` connect `Community 0` to `Community 1`, `Community 3`, `Community 12`, `Community 30`?**
  _High betweenness centrality (0.092) - this node is a cross-community bridge._
- **Why does `string` connect `Community 1` to `Community 0`, `Community 2`, `Community 4`, `Community 12`?**
  _High betweenness centrality (0.075) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _58 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._