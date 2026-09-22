# AI Connect 4 feature map

This directory is the maintained source for verifying user-facing arena behavior.
Read the index before driving the app, then use the matching feature file as the recipe.

## Baseline preconditions

- Launch via `helpers/control-c4.ps1 launch` (optionally `-Build`, optionally `-WithoutApiKey`).
- Run `control-c4.ps1 doctor` and require `"status": "ok"` and title `AI Connect 4`.
- Never drive a PID that was not started by this verification run.
- Prefer `-WithoutApiKey` for Ready / chrome proofs. Use a real `OPENROUTER_API_KEY` only for Play-series proofs.

## Driving conventions

- Start every recipe from a freshly launched Ready window unless the file says otherwise.
- Prefer UIA Names from button Content and TextBlock text over coordinates.
- Treat every command as literal. Keep quoted names and flags unchanged.
- Restore Ready via `New Series` after a Play when continuing in the same process, or `cleanup` + relaunch.
- Cleanup removes the process and temp state; it never deletes proof artifacts.

## Proof and skip reporting

- Capture the user action and the resulting state, not only the final screen.
- UI proof includes a UIA snapshot and a screenshot with the window title visible.
- Record the feature ID and entry point in `proof.txt` beside artifacts.
- Report an unreachable path with the unmet precondition (for example, Play requires an API key).
- Do not report a skipped entry point as verified through a different path.

## Feature entry contract

Each feature file starts with an H1 title and one paragraph describing the user-visible behavior. It then uses exactly four H2 sections in this order.

1. `Sub-features`
2. `How to get to it (user POV)`
3. `Driving it with control-c4`
4. `Gotchas`

## Features

- [Ready state](./ready-state.md) covers launch chrome, win counts at zero, and Play enablement with or without an API key.
- [Play series](./play-series.md) covers Play / Pause / Resume / New Series with a live OpenRouter key.
- [Win counts](./win-counts.md) covers series score labels above Red and Yellow.
- [Model pickers](./model-pickers.md) covers Red and Yellow model / effort / speed controls before Play.
