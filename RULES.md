# Repository Development & Release Rules

Whenever any changes are made to this repository, the following workflow MUST be strictly executed:

## 1. Version Bumping & Date Stamping
- Incremental version bump of **+0.01** (Starting baseline: `v0.01`).
- Embed version & current execution date (e.g., `v0.01 - 2026-07-24`) in:
  - `KatTradeManager.cs` (Header comments and WPF UI Title Header)
  - `README.md`
  - `DIARY.md`

## 2. Graphify & Project Diary Integration
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
