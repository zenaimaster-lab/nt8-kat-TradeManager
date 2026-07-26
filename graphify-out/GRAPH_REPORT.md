# Graph Report - nt8-kat-TradeManager  (2026-07-26)

## Corpus Check
- 26 files · ~26,255 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 318 nodes · 445 edges · 26 communities (12 shown, 14 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 53 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7d53836b`
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

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 46 edges
2. `KatCalculatorGapTests` - 40 edges
3. `KatTradeManager` - 29 edges
4. `KatTradeManager` - 23 edges
5. `KatOrderLifecycleTests` - 20 edges
6. `KatTradeCalculator` - 19 edges
7. `KatAccountFilterSwingSessionTests` - 18 edges
8. `KatRenkoAndHalfCandleTests` - 14 edges
9. `KatTradeCalculatorTests` - 13 edges
10. `StressAndEdgeCaseTests` - 12 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `double`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `DateTime`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `int`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `List`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs
- `KatTradeManager` --references--> `MarketPosition`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.OrderOps.cs

## Communities (26 total, 14 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.11
Nodes (7): DateTime, double, int, List, MarketPosition, KatTradeManager, NinjaTrader.NinjaScript.Indicators

### Community 2 - "Community 2"
Cohesion: 0.09
Nodes (15): Account, AtmLevels, bool, Border, ComboBox, DispatcherTimer, EMA, Grid (+7 more)

### Community 4 - "Community 4"
Cohesion: 0.12
Nodes (3): KatTradeCalculator, NinjaTrader.NinjaScript.Indicators, string

### Community 5 - "Community 5"
Cohesion: 0.15
Nodes (3): KatTradeManager, NinjaTrader.NinjaScript.Indicators, Window

### Community 13 - "Community 13"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **32 isolated node(s):** `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid`, `Border`, `StackPanel` (+27 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 2` to `Community 0`, `Community 4`?**
  _High betweenness centrality (0.066) - this node is a cross-community bridge._
- **Why does `string` connect `Community 4` to `Community 0`, `Community 2`?**
  _High betweenness centrality (0.040) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid` to the rest of the system?**
  _32 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 3` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._