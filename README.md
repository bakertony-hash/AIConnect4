# AI Connect 4

Watchable AI vs AI Connect 4 on Windows. The default matchup is Jev 1.13 (Red) against GPT-5.6 Luna (Yellow). Both sides call OpenRouter.

Design notes live in [PLAN.md](PLAN.md).

## Run

```bash
dotnet build AIConnect4.sln
dotnet run --project src/AIConnect4.App
dotnet test AIConnect4.sln
```

Set `OPENROUTER_API_KEY` in the environment before Play. A missing key leaves the window in Ready and disables Play.

## What you see

Three columns. Win counts sit above Red and Yellow for the whole series. The centre holds games-to-play, the 7×6 board, and Play / Pause / Resume / New Series.

Every model call is one decision. Which legal column do you play now? Jev uses OpenRouter's System One Choice API. Chat models receive the same decision shape over `/v1/chat/completions`. An illegal, empty, or unparseable answer retries once, then aborts the game and stops the series. No silent column fallback.
