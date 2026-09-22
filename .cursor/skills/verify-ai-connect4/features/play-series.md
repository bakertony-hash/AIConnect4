# Play series

Play series starts a configured number of games between the Red and Yellow models, shows Running status with turn text, and supports Pause, Resume, and New Series reset. Moves come from OpenRouter (Jev System One Choice or chat completions) — one Decision per call.

## Sub-features

- `series-play` starts the series and shows Running status.
- `series-pause-resume` pauses and resumes without resetting score.
- `series-new` cancels or finishes cleanup via New Series and returns to Ready.

## How to get to it (user POV)

- Set `OPENROUTER_API_KEY` in the environment.
- Optionally change Games to play and model pickers while Ready.
- Choose Play. Optionally Pause, Resume, then New Series when done.

## Driving it with control-c4

Preconditions:

- Launch **without** `-WithoutApiKey` and with a real `OPENROUTER_API_KEY` in the parent environment.
- `get-enabled -Name "Play"` reports `"enabled": true`.
- Prefer Games to play = 1 for a short smoke.

- **Confirm Ready.** Run `wait-name -Name "Ready"` and `get-enabled -Name "Play"`.
- **Start series.** Run `invoke -Name "Play"`. Within ~60s, `wait-name -Name "Running"` succeeds (or status shows an abort/fault string if the network fails — capture that as evidence).
- **Pause / Resume.** Run `invoke -Name "Pause"`, then `wait-name -Name "Paused"`, then `invoke -Name "Resume"` and `wait-name -Name "Running"`.
- **Reset.** Run `invoke -Name "New Series"`, then `wait-name -Name "Ready"`. Settings unlock again; win counts return to zero if the series had not finished scoring in a way that persists — after New Series, Wins reset to 0.
- **Proof.** Snapshot and screenshot while Running (or after abort). Record feature id `play-series` in `proof.txt`. Do not paste the API key.

## Gotchas

- Without an API key, Play stays disabled. Mark this entry point `verified-unreachable` for that launch and use Ready state instead.
- Live calls cost tokens and may abort on illegal or empty model answers; capture status Text rather than retrying indefinitely.
- There is no product dry-run path that substitutes `ScriptedMoveSource` into the Avalonia host.
- Watch pacing inserts ~400 ms between moves; do not treat brief idle as a hang.
