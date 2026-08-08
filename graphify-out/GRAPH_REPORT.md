# Graph Report - nt8-kat-TradeManager  (2026-08-08)

## Corpus Check
- 51 files · ~76,867 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 872 nodes · 1697 edges · 54 communities (25 shown, 29 thin omitted)
- Extraction: 84% EXTRACTED · 16% INFERRED · 0% AMBIGUOUS · INFERRED: 278 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `08f670ec`
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
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
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
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]

## God Nodes (most connected - your core abstractions)
1. `KatTradeManager` - 115 edges
2. `KatTradeManager` - 97 edges
3. `KatTradeManager` - 50 edges
4. `KatTradeCalculator` - 49 edges
5. `KatCalculatorGapTests` - 45 edges
6. `KatTradeManager` - 31 edges
7. `KatOrderLifecycleTests` - 30 edges
8. `KatTradeManager` - 25 edges
9. `KatTradeManager` - 23 edges
10. `KatAuditGapTests` - 23 edges

## Surprising Connections (you probably didn't know these)
- `KatTradeManager` --references--> `string`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `Border`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManager.AccountInfo.cs
- `KatTradeManager` --references--> `StackPanel`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `ComboBox`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs
- `KatTradeManager` --references--> `DispatcherTimer`  [EXTRACTED]
  KatTradeManager.cs → src/KatTradeManagerUI.cs

## Communities (54 total, 29 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.07
Nodes (3): Queue, KatTradeManager, KatTradeManager

### Community 1 - "Community 1"
Cohesion: 0.06
Nodes (24): Account, AccountOperationType, Action, AtmLevels, ComboBox, DispatcherTimer, EMA, Grid (+16 more)

### Community 3 - "Community 3"
Cohesion: 0.06
Nodes (23): bool, Brush, DateTime, double, int, KatEntryShiftTests, KatTradeManager.Tests, MarketPosition (+15 more)

### Community 5 - "Community 5"
Cohesion: 0.08
Nodes (14): Border, Button, Canvas, ControlTemplate, IInputElement, Point, Run, SolidColorBrush (+6 more)

### Community 7 - "Community 7"
Cohesion: 0.12
Nodes (3): Dictionary, KatTradeManager, NinjaTrader.NinjaScript.Indicators.KAT

### Community 15 - "Community 15"
Cohesion: 0.2
Nodes (3): ConcurrentDictionary<Type, Func<object, double>>, PropertyInfo>, KatTradeManager

### Community 22 - "Community 22"
Cohesion: 0.16
Nodes (4): AtmTemplateNameConverter, NinjaTrader.NinjaScript.Indicators, NinjaTrader.NinjaScript.Indicators.KAT, TypeConverter

### Community 32 - "Community 32"
Cohesion: 0.38
Nodes (3): AtmTemplateData, KatAtmXmlParser, NinjaTrader.NinjaScript.Indicators

## Knowledge Gaps
- **65 isolated node(s):** `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid`, `AtmLevels`, `NinjaTrader.NinjaScript.Indicators` (+60 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **29 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `KatTradeManager` connect `Community 5` to `Community 0`, `Community 1`, `Community 3`, `Community 40`, `Community 8`, `Community 14`, `Community 17`?**
  _High betweenness centrality (0.090) - this node is a cross-community bridge._
- **Why does `KatTradeManager` connect `Community 0` to `Community 1`, `Community 3`, `Community 7`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **Why does `string` connect `Community 3` to `Community 0`, `Community 1`, `Community 2`, `Community 5`, `Community 15`?**
  _High betweenness centrality (0.064) - this node is a cross-community bridge._
- **What connects `NinjaTrader.NinjaScript.Indicators.KAT`, `Account`, `Grid` to the rest of the system?**
  _65 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.07 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._