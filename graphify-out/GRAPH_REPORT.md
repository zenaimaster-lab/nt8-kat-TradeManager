# Graph Report - nt8-kat-TradeManager  (2026-08-07)

## Corpus Check
- 33 files · ~48,928 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 603 nodes · 1062 edges · 38 communities (18 shown, 20 thin omitted)
- Extraction: 87% EXTRACTED · 13% INFERRED · 0% AMBIGUOUS · INFERRED: 134 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `a4b9f7c3`
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
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 96 edges
2. `KatTradeManager` - 61 edges
3. `KatTradeManager` - 49 edges
4. `KatTradeCalculator` - 47 edges
5. `KatCalculatorGapTests` - 45 edges
6. `KatOrderLifecycleTests` - 30 edges
7. `KatTradeCalculatorTests` - 22 edges
8. `KatTradeManager` - 21 edges
9. `KatAccountFilterSwingSessionTests` - 20 edges
10. `KatFreezeTrailTests` - 17 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `string`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DispatcherTimer`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `bool`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `double`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  KatTradeManager.cs → tests/KatTradeManager.Tests/KatEntryShiftTests.cs

## Communities (38 total, 20 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.09
Nodes (4): OrderAction, Queue, KatTradeManager, KatTradeManager

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (6): Dictionary, KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 4 - "Community 4"
Cohesion: 0.05
Nodes (29): AccountOperationType, Action, bool, DateTime, double, int, KatEntryShiftTests, KatTradeManager.Tests (+21 more)

### Community 5 - "Community 5"
Cohesion: 0.09
Nodes (10): Brush, Button, Canvas, IInputElement, Point, SolidColorBrush, KatTradeManager, TextBlock (+2 more)

### Community 7 - "Community 7"
Cohesion: 0.12
Nodes (12): Account, AtmLevels, Border, ComboBox, DispatcherTimer, EMA, Grid, Indicator (+4 more)

### Community 22 - "Community 22"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **56 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `Border`, `StackPanel` (+51 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **20 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 0` to `Community 1`, `Community 19`, `Community 4`, `Community 7`?**
  _High betweenness centrality (0.116) - this node is a cross-community bridge._
- **Why does `string` connect `Community 4` to `Community 0`, `Community 3`, `Community 5`, `Community 7`?**
  _High betweenness centrality (0.103) - this node is a cross-community bridge._
- **Why does `KatTradeCalculator` connect `Community 3` to `Community 4`?**
  _High betweenness centrality (0.075) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _56 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.04 - nodes in this community are weakly interconnected._