# Graph Report - nt8-kat-TradeManager  (2026-08-03)

## Corpus Check
- 33 files · ~43,184 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 523 nodes · 888 edges · 32 communities (15 shown, 17 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 95 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `696523f8`
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
1. `KatTradeManager` - 94 edges
2. `KatTradeManager` - 52 edges
3. `KatTradeManager` - 49 edges
4. `KatCalculatorGapTests` - 44 edges
5. `KatTradeCalculator` - 36 edges
6. `KatOrderLifecycleTests` - 28 edges
7. `KatAccountFilterSwingSessionTests` - 20 edges
8. `KatFreezeTrailTests` - 17 edges
9. `KatTradeCalculatorTests` - 15 edges
10. `KatRenkoAndHalfCandleTests` - 14 edges

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
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs

## Communities (32 total, 17 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.05
Nodes (25): Account, AtmLevels, Border, Brush, Button, Canvas, ComboBox, DispatcherTimer (+17 more)

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (3): OrderAction, Queue, KatTradeManager

### Community 4 - "Community 4"
Cohesion: 0.07
Nodes (18): AccountOperationType, Action, bool, int, List, AtmScaleInState, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT (+10 more)

### Community 8 - "Community 8"
Cohesion: 0.18
Nodes (7): DateTime, double, OrderType, FreezeExitCapture, KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT

### Community 10 - "Community 10"
Cohesion: 0.24
Nodes (3): KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT

### Community 17 - "Community 17"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **52 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `Border`, `StackPanel` (+47 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **17 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 1` to `Community 0`, `Community 8`, `Community 10`, `Community 4`?**
  _High betweenness centrality (0.113) - this node is a cross-community bridge._
- **Why does `string` connect `Community 4` to `Community 0`, `Community 8`, `Community 3`, `Community 1`?**
  _High betweenness centrality (0.096) - this node is a cross-community bridge._
- **Why does `KatTradeCalculator` connect `Community 3` to `Community 4`?**
  _High betweenness centrality (0.065) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _52 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.04 - nodes in this community are weakly interconnected._