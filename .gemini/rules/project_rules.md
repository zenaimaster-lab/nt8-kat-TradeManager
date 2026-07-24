# Project-Specific Rules: nt8-kat-TradeManager

Whenever making changes in this repository, follow these rules:

1. **Version Bumping & Date**:
   - Increment version by `+0.01` starting from `0.01`.
   - Include current date `YYYY-MM-DD`.
   - Update version string in `KatTradeManager.cs` UI header, `README.md`, and `DIARY.md`.

2. **Graphify & Diary**:
   - Log changes in `DIARY.md` using graphify system style (Entities: Components, Data Flow, Features, Versions).
   - Follow Karpathy coding guidelines (surgical diffs, clear contracts, zero bloat).

3. **NinjaTrader 8 Auto-Deployment**:
   - Copy `KatTradeManager.cs` to `C:\Users\kieuanhtuan\Documents\NinjaTrader 8\bin\Custom\Indicators\KatTradeManager.cs`.

4. **GitHub Auto Push**:
   - Run `git add .`, `git commit -m "..."`, and `git push origin main`.
