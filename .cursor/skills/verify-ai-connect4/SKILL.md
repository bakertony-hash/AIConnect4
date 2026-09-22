---
name: verify-ai-connect4
description: >-
  Drive the AI Connect 4 Avalonia desktop app with Windows UI Automation
  (control-c4.ps1): launch a tracked instance, doctor it, exercise Ready /
  series / model-picker journeys, capture UIA snapshots and screenshots, then
  clean up. Use when proving user-facing arena behavior on Windows, or
  extending the feature map.
---

# Verify AI Connect 4

Agent-facing control skill for the primary surface: the **Avalonia desktop app**
(`AIConnect4.App.exe`). Unit tests under `tests/` (including `ScriptedMoveSource`)
are not a substitute — they do not open the real window.

Secondary surfaces: none for verification. There is no CLI arena, no web UI, and
no in-app dry-run that swaps OpenRouter for `ScriptedMoveSource`.

## Interview facts

| Concern | Observed |
| --- | --- |
| Surface | .NET 8 Avalonia 11 WinExe, three-column arena (Red / board / Yellow), MVVM via `ArenaViewModel` |
| Run | `dotnet build AIConnect4.sln` then `dotnet run --project src/AIConnect4.App`, or the Debug exe under `src\AIConnect4.App\bin\Debug\net8.0\AIConnect4.App.exe`. Env: `OPENROUTER_API_KEY` (optional at launch; required for Play). |
| Drive | `.cursor/skills/verify-ai-connect4/helpers/control-c4.ps1` (Windows UI Automation). No FlaUI/Playwright harness in-repo. |
| Observe | Window title `AI Connect 4`, button Names (`Play`, `Pause`, `Resume`, `New Series`), status Text (`Ready` / `Ready (API key missing)` / Running…), win-count Text (`Wins: 0`), UIA tree dumps, window screenshots |
| Isolate | Multiple processes can run. Drive only the PID recorded by `launch`. No shared AppData session store. Prefer one verification instance at a time so screenshots are unambiguous. |

## Launch

From the repo root (PowerShell):

```powershell
$ctrl = ".\.cursor\skills\verify-ai-connect4\helpers\control-c4.ps1"
$runId = [guid]::NewGuid().ToString('N').Substring(0, 12)
& $ctrl launch -RunId $runId -Build
```

Missing-key Ready path (clears `OPENROUTER_API_KEY` for the child process only):

```powershell
& $ctrl launch -RunId $runId -Build -WithoutApiKey
```

Ready when `launch` returns JSON with `title` equal to `AI Connect 4` and `doctor` returns `"status": "ok"`.

Teardown: `& $ctrl cleanup`. Evidence under `.cursor/skills/verify-ai-connect4/artifacts\<runId>\` is kept.

## Doctor

```powershell
& $ctrl doctor
```

Checks: recorded PID alive, process name `AIConnect4.App`, title exactly `AI Connect 4`, exe path present. Also reports whether a Ready status Text node was found and whether the launch recorded an API key.

## Drive

```powershell
$ctrl = ".\.cursor\skills\verify-ai-connect4\helpers\control-c4.ps1"
& $ctrl doctor
& $ctrl get-title
& $ctrl wait-name -Name "Ready"
& $ctrl assert-text -Contains "Wins: 0"
& $ctrl assert-text -Contains "Games to play"
& $ctrl get-enabled -Name "Play"
& $ctrl invoke -Name "Play"          # only when Play is enabled (API key present)
& $ctrl wait-name -Name "Running" -TimeoutSec 60
& $ctrl invoke -Name "Pause"
& $ctrl invoke -Name "Resume"
& $ctrl invoke -Name "New Series"
& $ctrl snapshot -Path ".\.cursor\skills\verify-ai-connect4\artifacts\$runId\tree.uia.txt"
& $ctrl screenshot -Path ".\.cursor\skills\verify-ai-connect4\artifacts\$runId\main.png"
```

Stable handles observed in this app (Avalonia exposes `Button.Content` and `TextBlock` text as UIA `Name`):

- Main window title: `AI Connect 4`
- Buttons: `Play`, `Pause`, `Resume`, `New Series`
- Status (exact): `Ready` when `OPENROUTER_API_KEY` is set; `Ready (API key missing)` otherwise
- Win labels: Text nodes containing `Wins: ` (two — Red and Yellow)
- Side titles: `Red`, `Yellow`
- Labels: `Model`, `Effort`, `Speed`, `Games to play`
- ComboBoxes (model / effort / speed) often have empty Names; identify by sibling label Text and tree position in a UIA snapshot

Feature recipes live in `features/`. Read `features/README.md` before choosing an entry point.

## Evidence

Proof root: `.cursor/skills/verify-ai-connect4/artifacts/<runId>/`

Standards:

1. Exercise the real UI path (buttons, visible status). Do not call ViewModel setters or inject `ScriptedMoveSource` as a UI proof.
2. Capture **action + resulting state**: UIA snapshot and screenshot showing title `AI Connect 4`, Ready (or Running/Paused), and both win counts.
3. Side effects for a live series: board cells change and win counts tick only when games finish. A Ready-only proof is valid when the API key is missing or when the mapped feature is Ready state.
4. Live OpenRouter traffic happens only after Play with a real key. Prefer `-WithoutApiKey` for chrome/Ready proofs so the run does not spend tokens. When you do Play, treat network failures as product signals (status/abort text), not harness bugs.
5. Write `proof.txt` with `runId`, feature id, and entry point. Do not paste `OPENROUTER_API_KEY` into artifacts.

## Cleanup

```powershell
& $ctrl cleanup
```

- Stops only the PID in `%TEMP%\c4-verify\<runId>\run.json`
- Deletes that run's temp directory under `%TEMP%\c4-verify\`
- **Does not** delete `.cursor/skills/verify-ai-connect4/artifacts\<runId>\`

## Helpers

- `helpers/control-c4.ps1` — `launch` (`-Build`, `-WithoutApiKey`), `doctor`, `get-title`, `info`, `wait-name`, `invoke`, `get-enabled`, `find-text`, `assert-text`, `snapshot`, `screenshot`, `cleanup` / `stop`

## Maintenance

Keep the feature map honest as the arena changes with `/maintain-verification-skill`.
