# Repository Development & Release Rules

This file is supplemented by `AGENTS.md` which configures agent behavior modes (Caveman, Pony Tail, Karpathy, Graphify).

Whenever any changes are made to this repository, the following workflow MUST be strictly executed:

## 1. Version Bumping & Date Stamping
- Incremental version bump of **+0.01** (Starting baseline: `v0.01`).
- Embed version & current execution date (e.g., `v0.27 - 2026-07-25`) in:
  - `KatTradeManager.cs` (Header comments, `VERSION` constant, `RELEASE_DATE` constant)
  - `src/KatTradeManagerUI.cs` (WPF Title Header if version displayed)
  - `README.md` (Current Version badge)
  - `DIARY.md` (new version history entry)

## 2. Graphify & Project Diary Integration
- Run `graphify update .` at end of session.
- Maintain `DIARY.md` with:
  - Version history entry with timestamp.
  - Graphify entity mapping (Components, Dependencies, Actions, Data Flow).
- Apply Karpathy Guidelines: surgical minimal diffs, zero unnecessary abstractions, clear success criteria.

## 3. NinjaTrader 8 Deployment
- Copy/deploy `KatTradeManager.cs` directly to NinjaTrader 8 custom indicators directory:
  `C:\Users\kieuanhtuan\Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`

## 4. Git & GitHub Synchronization
- Stage all modified files (`git add .`).
- Commit with version & bump message (`git commit -m "vX.XX (YYYY-MM-DD): Description"`).
- Push directly to `origin main` on GitHub.
