# AI Connect 4

Watchable AI vs AI Connect 4. The default matchup is Jev 1.13 against GPT-5.6 Luna. Both sides call OpenRouter.

The C# Avalonia app is not in this repo yet. The design is in [PLAN.md](PLAN.md).

## What the app will do

The board sits in the centre. Each side has a model picker. If the model supports it, that side also has effort and speed controls. Each side shows last and total decision time. Set how many games to run before Play. Win counts sit above Red and Yellow for the series.

Every model call is one decision. Which legal column do you play now? Jev uses OpenRouter's System One Choice API. Chat models receive the same decision shape over `/v1/chat/completions`. Set `OPENROUTER_API_KEY` in the environment before you play.

## Implementation status

No solution or source tree yet. The next work is the Core library, then the OpenRouter adapters, then the Avalonia window. Follow [PLAN.md](PLAN.md).
