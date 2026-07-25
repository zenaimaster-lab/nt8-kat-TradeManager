# AGENTS.md — nt8-kat-TradeManager

## Caveman Mode — ULTRA
- Respond terse like smart caveman. All technical substance, no fluff.
- Drop: articles (a/an/the), filler (just/really/basically), pleasantries, hedging.
- Fragments OK. Short synonyms. Technical terms exact. Code unchanged.
- Preserve user's language. Reply in same language.
- Code/commits/PRs written normal (not caveman).

## Pony Tail — Full
- Laziest solution that works. YAGNI first.
- Stdlib before custom code. Native before dependency.
- One line before fifty. No speculative abstractions.
- Mark intentional shortcuts with `ponytail:` comment naming the ceiling + upgrade path.
- Deletion over addition. Boring over clever. Fewest files.

## Karpathy Guidelines
- Think before coding: state assumptions, ask if uncertain, present multiple interpretations.
- Simplicity first: minimum code, nothing speculative, no features beyond asked.
- Surgical changes: touch only what must change, match existing style, remove YOUR orphans.
- Goal-driven: define success criteria before starting, loop until verified.

## Graphify Best Practices
- Read graphify-out/GRAPH_REPORT.md before answering codebase questions.
- Navigate graphify-out/wiki/ for module context instead of raw files.
- Run `graphify update .` at END of session or after significant milestone.
- AST-only updates between major passes (zero token cost).
- Respect .gitignore — excludes node_modules, venv, __pycache__, logs/, .git.

## Auto GitHub Connection
- Remote: https://github.com/zenaimaster-lab/nt8-kat-TradeManager.git (origin/main).
- All changes commit + push to origin main.
- Use `gh` for PRs/issues if needed.

## Version Bump Workflow (MANDATORY)
On every code change, BEFORE closing session:

1. **Bump version** +0.01 from current (baseline v0.01).
2. **Stamp date** in format YYYY-MM-DD.
3. **Update all locations**:
   - `KatTradeManager.cs`: header comment + `VERSION` + `RELEASE_DATE` constants
   - `src/KatTradeManagerUI.cs`: WPF Title Header if version displayed
   - `README.md`: "Current Version" badge
   - `DIARY.md`: new version history entry
4. **Update Graphify**: run `graphify update .`
5. **Update Diary**: add entry with timestamp, changes summary, Graphify entity mapping.
6. **Deploy NT8 (MANDATORY FULL SYNC)**: copy ALL source `.cs` files (`KatTradeManager.cs` AND `src/*.cs`) to `C:\Users\kieuanhtuan\Documents\NinjaTrader 8\bin\Custom\Indicators\` with force overwrite:
   - `KatTradeManager.cs`
   - `src\KatTradeManagerUI.cs`
   - `src\KatTradeManager.OrderOps.cs`
   - `src\KatTradeManager.Properties.cs`
   - `src\KatTradeCalculator.cs`
   - `src\KatAtmXmlParser.cs`
7. **Git sync**:
   - `git add .`
   - `git commit -m "vX.XX (YYYY-MM-DD): Description"`
   - `git push origin main`

## Version Tracking
- Code versions: KatTradeManager.cs VERSION constant
- Doc versions: README.md, DIARY.md
- Current: v0.60 (2026-07-26)
