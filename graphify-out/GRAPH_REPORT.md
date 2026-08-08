# Graph Report - nt8-kat-TradeManager  (2026-08-07)

## Corpus Check
- 39 files · ~62,813 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 738 nodes · 1434 edges · 44 communities (19 shown, 25 thin omitted)
- Extraction: 86% EXTRACTED · 14% INFERRED · 0% AMBIGUOUS · INFERRED: 203 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7bcc37c6`
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
- `KatTradeManager` --references--> `Order`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `int`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `string`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `bool`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `double`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs

## Communities (44 total, 25 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.06
Nodes (9): Button, Canvas, IInputElement, Point, SolidColorBrush, KatTradeManager, TextBlock, UIElement (+1 more)

### Community 1 - "Community 1"
Cohesion: 0.08
Nodes (3): OrderAction, Queue, KatTradeManager

### Community 4 - "Community 4"
Cohesion: 0.11
Nodes (11): Account, AtmLevels, Border, ComboBox, DispatcherTimer, EMA, Grid, Indicator (+3 more)

### Community 5 - "Community 5"
Cohesion: 0.08
Nodes (27): AccountOperationType, Action, bool, Brush, DateTime, double, int, List (+19 more)

### Community 7 - "Community 7"
Cohesion: 0.13
Nodes (4): Dictionary, object, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 15 - "Community 15"
Cohesion: 0.16
Nodes (4): AtmTemplateNameConverter, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, TypeConverter

### Community 26 - "Community 26"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

### Community 27 - "Community 27"
Cohesion: 0.38
Nodes (3): KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT

## Knowledge Gaps
- **58 isolated node(s):** `NinjaTrader.NinjaScript.Indicators`, `AtmTemplateData`, `NinjaTrader.NinjaScript.Indicators`, `NinjaTrader.NinjaScript.Indicators.KAT`, `NinjaTrader.NinjaScript.Indicators.KAT` (+53 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 1` to `Community 0`, `Community 17`, `Community 5`, `Community 7`?**
  _High betweenness centrality (0.105) - this node is a cross-community bridge._
- **Why does `KatTradeManager` connect `Community 0` to `Community 33`, `Community 4`, `Community 5`, `Community 7`, `Community 17`?**
  _High betweenness centrality (0.092) - this node is a cross-community bridge._
- **Why does `string` connect `Community 5` to `Community 0`, `Community 1`, `Community 3`, `Community 4`?**
  _High betweenness centrality (0.075) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators`, `AtmTemplateData`, `NinjaTrader.NinjaScript.Indicators` to the rest of the system?**
  _58 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.04 - nodes in this community are weakly interconnected._