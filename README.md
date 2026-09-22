# AI Connect 4

Watchable AI vs AI Connect 4. The default matchup is Jev 1.13 against GPT-5.6 Luna. Both sides call OpenRouter.

The C# Avalonia app is not in this repo yet. The design is in [PLAN.md](PLAN.md).

## What the app will do

The board sits in the centre. Each side has a model picker. If the model supports it, that side also has effort and speed controls. Each side shows last and total decision time. The finished game shows combined think time.

Jev uses OpenRouter's System One API. Chat models use `/v1/chat/completions`. Set `OPENROUTER_API_KEY` in the environment before you play.

## Implementation status

No solution or source tree yet. The next work is the Core library, then the OpenRouter adapters, then the Avalonia window. Follow [PLAN.md](PLAN.md).
