# Graph Report - nt8-kat-TradeManager  (2026-07-31)

## Corpus Check
- 32 files · ~40,937 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 500 nodes · 845 edges · 32 communities (15 shown, 17 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 93 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e2f97977`
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
1. `KatTradeManager` - 91 edges
2. `KatTradeManager` - 49 edges
3. `KatTradeManager` - 46 edges
4. `KatCalculatorGapTests` - 44 edges
5. `KatTradeCalculator` - 35 edges
6. `KatOrderLifecycleTests` - 28 edges
7. `KatAccountFilterSwingSessionTests` - 20 edges
8. `KatFreezeTrailTests` - 17 edges
9. `KatTradeCalculatorTests` - 15 edges
10. `KatRenkoAndHalfCandleTests` - 14 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `DispatcherTimer`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `double`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `object`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `MarketPosition`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs

## Communities (32 total, 17 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.06
Nodes (23): Account, AccountOperationType, Action, AtmLevels, bool, Border, ComboBox, EMA (+15 more)

### Community 1 - "Community 1"
Cohesion: 0.1
Nodes (4): DateTime, Queue, KatTradeManager, KatTradeManager

### Community 4 - "Community 4"
Cohesion: 0.09
Nodes (10): Brush, Canvas, DispatcherTimer, IInputElement, Point, KatTradeManager, NinjaTrader.NinjaScript.Indicators, TextBlock (+2 more)

### Community 11 - "Community 11"
Cohesion: 0.16
Nodes (4): AtmTemplateNameConverter, KatTradeManager, NinjaTrader.NinjaScript.Indicators, TypeConverter

### Community 17 - "Community 17"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

### Community 22 - "Community 22"
Cohesion: 0.4
Nodes (4): double, OrderType, FreezeExitCapture, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **43 isolated node(s):** `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid`, `Border`, `StackPanel` (+38 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **17 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 1` to `Community 0`, `Community 4`, `Community 22`, `Community 6`?**
  _High betweenness centrality (0.101) - this node is a cross-community bridge._
- **Why does `string` connect `Community 0` to `Community 1`, `Community 3`, `Community 4`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **Why does `KatTradeCalculator` connect `Community 3` to `Community 0`?**
  _High betweenness centrality (0.061) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid` to the rest of the system?**
  _43 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.04 - nodes in this community are weakly interconnected._