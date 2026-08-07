# Graph Report - nt8-kat-TradeManager  (2026-08-07)

## Corpus Check
- 33 files · ~54,808 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 630 nodes · 1162 edges · 34 communities (15 shown, 19 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 138 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `76e3839a`
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

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 96 edges
2. `KatTradeManager` - 90 edges
3. `KatTradeManager` - 49 edges
4. `KatTradeCalculator` - 48 edges
5. `KatCalculatorGapTests` - 45 edges
6. `KatOrderLifecycleTests` - 30 edges
7. `KatTradeCalculatorTests` - 22 edges
8. `KatTradeManager` - 21 edges
9. `KatAccountFilterSwingSessionTests` - 20 edges
10. `KatFreezeTrailTests` - 17 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `string`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `ComboBox`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DispatcherTimer`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `bool`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `double`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs

## Communities (34 total, 19 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.06
Nodes (12): Brush, Button, Canvas, IInputElement, Point, SolidColorBrush, KatTradeManager, NinjaTrader.NinjaScript.Indicators (+4 more)

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (4): OrderAction, Queue, KatTradeManager, KatTradeManager

### Community 2 - "Community 2"
Cohesion: 0.09
Nodes (7): Dictionary, object, KatTradeManager, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 5 - "Community 5"
Cohesion: 0.06
Nodes (29): AccountOperationType, Action, bool, DateTime, double, int, List, MarketPosition (+21 more)

### Community 6 - "Community 6"
Cohesion: 0.11
Nodes (11): Account, AtmLevels, Border, ComboBox, DispatcherTimer, EMA, Grid, Indicator (+3 more)

### Community 19 - "Community 19"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **55 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `Border`, `StackPanel` (+50 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **19 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 1` to `Community 0`, `Community 2`, `Community 5`?**
  _High betweenness centrality (0.111) - this node is a cross-community bridge._
- **Why does `string` connect `Community 5` to `Community 0`, `Community 1`, `Community 3`, `Community 6`?**
  _High betweenness centrality (0.102) - this node is a cross-community bridge._
- **Why does `KatTradeManager` connect `Community 0` to `Community 2`, `Community 5`, `Community 6`?**
  _High betweenness centrality (0.100) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _55 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09 - nodes in this community are weakly interconnected._