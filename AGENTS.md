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

## Version Bump Workflow (MANDATORY) — drift-proof via skill `nt8-deploy-verify`
On every code change, BEFORE closing session. NEVER edit version strings by hand — use `scripts/Bump-Version.ps1` (single source of truth):

1. **Bump version** `pwsh scripts/Bump-Version.ps1 -Description "..."` — auto +0.01 from current (`VERSION` constant), stamps YYYY-MM-DD, syncs ALL 4 locations + runs `Verify-Version.ps1`:
   - `KatTradeManager.cs`: header comment + `VERSION` + `RELEASE_DATE` constants
   - `src/KatTradeManagerUI.cs`: WPF Title Header if version displayed
   - `README.md`: "Current Version" badge
   - `DIARY.md`: new version history entry (manual — add `### [vX.XX] — YYYY-MM-DD` at top)
2. **Verify** `pwsh scripts/Verify-Version.ps1` (warn) / `-Strict` (CI fail) — checks header == VERSION == UI == README == DIARY, aborts deploy on drift.
3. **Checks** `pwsh scripts/Run-AllChecks.ps1` — version guard + xunit + net48 compile gate (0 errors).
4. **Update Graphify**: run `graphify update .`
5. **Deploy NT8 (MANDATORY FULL SYNC)**: `pwsh scripts/Deploy-NT8.ps1` — pre-flight Verify, copy ALL 14 `.cs` files with force overwrite + orphan sweep, atomic nudge, post-deploy VERSION + SHA256 hash verify, waits for `NinjaTrader.Custom.dll` recompile:
   - `KatTradeManager.cs`
   - `src\KatTradeManagerUI.cs` + `src\KatTradeManager.HudDrag.cs`
   - `src\KatTradeManager.OrderOps.cs` + `src\KatTradeManager.Queue.cs` + `src\KatTradeManager.AtmMerge.cs` + `src\KatTradeManager.SwingOps.cs`
   - `src\KatTradeManager.DailyRisk.cs` + `src\KatTradeManager.Discipline.cs` + `src\KatTradeManager.ProfileOps.cs` + `src\KatAtmTemplateService.cs`
   - `src\KatTradeManager.Properties.cs` + `src\KatTradeCalculator.cs` + `src\KatAtmXmlParser.cs`
   - Skill: `nt8-deploy-verify` (see `C:\Users\kieuanhtuan\.agents\skills\nt8-deploy-verify\SKILL.md`) — explains generic apply to any NT8 repo.
6. **Git sync**:
   - `git add .`
   - `git commit -m "vX.XX (YYYY-MM-DD): Description"`
   - `git push origin main`

## Version Tracking
- Code versions: KatTradeManager.cs VERSION constant (header must match constant — `Verify-Version.ps1` enforces)
- Doc versions: README.md, DIARY.md, AGENTS.md
- **Current: v1.97 (2026-08-08)** — next bump via `scripts/Bump-Version.ps1` only (never hand-edit)
