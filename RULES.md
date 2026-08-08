# Repository Development & Release Rules

This file is supplemented by `AGENTS.md` which configures agent behavior modes (Caveman, Pony Tail, Karpathy, Graphify) and skill `nt8-deploy-verify`.

Whenever any changes are made to this repository, the following workflow MUST be strictly executed:

## 1. Version Bumping & Date Stamping — SINGLE SOURCE OF TRUTH
- NEVER edit `Version:` / `VERSION` / `RELEASE_DATE` by hand — drift-proof guard will block deploy (v1.57 bug).
- ALWAYS use `pwsh scripts/Bump-Version.ps1 -Description "..."` — auto +0.01 from `KatTradeManager.cs:VERSION`, stamps YYYY-MM-DD, syncs ALL locations + runs `Verify-Version.ps1`:
  - `KatTradeManager.cs` (Header `Version: X.Y (date)`, `VERSION`, `RELEASE_DATE`)
  - `src/KatTradeManagerUI.cs` (WPF Title Header `vX.Y (date)`)
  - `README.md` (Current Version badge + date)
  - `DIARY.md` (new `### [vX.Y] — date` entry — manual, required for `-Strict`)
- Verify: `pwsh scripts/Verify-Version.ps1` (warn) / `-Strict` (CI fail) — checks header == VERSION == UI == README == DIARY.
- Skill: `nt8-deploy-verify` (`C:\Users\kieuanhtuan\.agents\skills\nt8-deploy-verify\SKILL.md`) explains generic apply to any NT8 repo.

## 2. Graphify & Project Diary Integration
- Run `graphify update .` at end of session.
- Maintain `DIARY.md` with:
  - Version history entry with timestamp.
  - Graphify entity mapping (Components, Dependencies, Actions, Data Flow).
- Apply Karpathy Guidelines: surgical minimal diffs, zero unnecessary abstractions, clear success criteria.

## 3. NinjaTrader 8 Deployment (MANDATORY FULL SYNC HARD RULE — VERIFIED)
- MUST use `pwsh scripts/Deploy-NT8.ps1` — it enforces:
  - **Pre-flight**: `Verify-Version.ps1` aborts deploy if any version drift (header vs VERSION vs UI vs README).
  - **Copy**: ALL 11 `.cs` files to `Documents\NinjaTrader 8\bin\Custom\Indicators\KAT\` with force overwrite + stale/orphan sweep.
  - **Post-deploy**: VERSION + RELEASE_DATE match repo + SHA256 hash match for every file; atomic timestamp nudge to force single consistent NT8 recompile.
  - **Recompile wait**: polls `NinjaTrader.Custom.dll` LastWriteTime > deployTime (60s timeout).
- Never copy files manually — manual copy bypasses verification and reproduces v1.57 bug.
- Compile gate: `dotnet build tools/CompileCheck` must succeed (net48 + NT8 assemblies, mirrors NT8's internal Roslyn compile) — also run via `pwsh scripts/Run-AllChecks.ps1` (version guard + xunit + compile gate).

## 4. Git & GitHub Synchronization
- Stage all modified files (`git add .`).
- Commit with version & bump message (`git commit -m "vX.XX (YYYY-MM-DD): Description"`).
- Push directly to `origin main` on GitHub.
- CI runs `Verify-Version.ps1 -Strict` — push fails if version drift shipped.
