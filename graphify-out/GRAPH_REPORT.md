# Graph Report - nt8-kat-TradeManager  (2026-07-25)

## Corpus Check
- 21 files · ~18,600 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 212 nodes · 264 edges · 21 communities (9 shown, 12 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 19 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `0460b0c3`
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

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 36 edges
2. `KatTradeManager` - 21 edges
3. `KatOrderLifecycleTests` - 20 edges
4. `KatTradeCalculator` - 16 edges
5. `KatRenkoAndHalfCandleTests` - 14 edges
6. `KatTradeCalculatorTests` - 13 edges
7. `StressAndEdgeCaseTests` - 12 edges
8. `KatEmaPlaceAndAngleTests` - 11 edges
9. `KatEmaTouchTests` - 6 edges
10. `KatLineDrawingTests` - 6 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `string`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeCalculator.cs
- `KatTradeManager` --references--> `bool`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs

## Communities (21 total, 12 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.11
Nodes (17): Account, AtmLevels, bool, Border, ComboBox, DispatcherTimer, double, EMA (+9 more)

### Community 3 - "Community 3"
Cohesion: 0.15
Nodes (3): KatTradeCalculator, NinjaTrader.NinjaScript.Indicators, string

### Community 10 - "Community 10"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **28 isolated node(s):** `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid`, `Border`, `StackPanel` (+23 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **12 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 0` to `Community 1`, `Community 3`?**
  _High betweenness centrality (0.093) - this node is a cross-community bridge._
- **Why does `string` connect `Community 3` to `Community 0`?**
  _High betweenness centrality (0.045) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid` to the rest of the system?**
  _28 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.11 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.14 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 4` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._