# Graph Report - nt8-kat-TradeManager  (2026-07-25)

## Corpus Check
- 20 files · ~14,362 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 183 nodes · 212 edges · 20 communities (10 shown, 10 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 11 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e6c86ea3`
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

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 34 edges
2. `KatOrderLifecycleTests` - 20 edges
3. `KatRenkoAndHalfCandleTests` - 14 edges
4. `KatTradeCalculator` - 13 edges
5. `KatTradeCalculatorTests` - 13 edges
6. `StressAndEdgeCaseTests` - 12 edges
7. `KatTradeManager` - 9 edges
8. `KatEmaTouchTests` - 6 edges
9. `KatLineDrawingTests` - 6 edges
10. `KatLineTagAndStartBarTests` - 5 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `string`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeCalculator.cs

## Communities (20 total, 10 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.12
Nodes (16): Account, AtmLevels, bool, Border, ComboBox, DispatcherTimer, double, Grid (+8 more)

### Community 2 - "Community 2"
Cohesion: 0.17
Nodes (3): EMA, KatTradeManager, NinjaTrader.NinjaScript.Indicators

### Community 4 - "Community 4"
Cohesion: 0.18
Nodes (3): KatTradeCalculator, NinjaTrader.NinjaScript.Indicators, string

### Community 9 - "Community 9"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **28 isolated node(s):** `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid`, `Border`, `StackPanel` (+23 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **10 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 0` to `Community 2`, `Community 4`?**
  _High betweenness centrality (0.086) - this node is a cross-community bridge._
- **Why does `string` connect `Community 4` to `Community 0`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators`, `Account`, `Grid` to the rest of the system?**
  _28 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 3` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._
- **Should `Community 5` be split into smaller, more focused modules?**
  _Cohesion score 0.13 - nodes in this community are weakly interconnected._