# Graph Report - zenaimaster-lab-fluffy-winner  (2026-08-05)

## Corpus Check
- 28 files · ~39,953 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 416 nodes · 662 edges · 28 communities (10 shown, 18 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 44 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `951e88d9`
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

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 88 edges
2. `KatTradeManager` - 52 edges
3. `KatTradeManager` - 29 edges
4. `KatCalculatorGapTests` - 28 edges
5. `KatTradeCalculator` - 25 edges
6. `KatOrderLifecycleTests` - 24 edges
7. `KatAccountFilterSwingSessionTests` - 20 edges
8. `KatTradeCalculatorTests` - 13 edges
9. `AtmTemplateNameConverter` - 11 edges
10. `KatDailyRiskTests` - 11 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `DispatcherTimer`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  KatTradeManager.cs → tests/KatTradeManager.Tests/KatEntryShiftTests.cs
- `KatTradeManager` --references--> `object`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `MarketPosition`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  src/KatTradeManager.OrderOps.cs → tests/KatTradeManager.Tests/KatEntryShiftTests.cs

## Communities (28 total, 18 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.1
Nodes (3): OrderAction, Queue, KatTradeManager

### Community 1 - "Community 1"
Cohesion: 0.08
Nodes (11): Brush, Button, Canvas, IInputElement, Point, SolidColorBrush, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT (+3 more)

### Community 2 - "Community 2"
Cohesion: 0.07
Nodes (26): Account, AccountOperationType, Action, AtmLevels, bool, Border, ComboBox, DispatcherTimer (+18 more)

### Community 13 - "Community 13"
Cohesion: 0.18
Nodes (3): DateTime, KatEntryShiftTests, KatTradeManager.Tests

### Community 15 - "Community 15"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **43 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `Border`, `StackPanel` (+38 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **18 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 0` to `Community 1`, `Community 2`, `Community 3`, `Community 13`?**
  _High betweenness centrality (0.151) - this node is a cross-community bridge._
- **Why does `string` connect `Community 2` to `Community 0`, `Community 1`, `Community 5`?**
  _High betweenness centrality (0.101) - this node is a cross-community bridge._
- **Why does `KatTradeManager` connect `Community 1` to `Community 2`, `Community 3`?**
  _High betweenness centrality (0.087) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _43 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.07 - nodes in this community are weakly interconnected._