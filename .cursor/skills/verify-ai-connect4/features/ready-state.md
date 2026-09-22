# Ready state

Ready state is the idle arena after launch: both sides show Wins: 0, Games to play is editable, model pickers are available, and status reads Ready (or Ready with an API-key hint). Play is enabled only when `OPENROUTER_API_KEY` is present.

## Sub-features

- `ready-chrome` shows the three-column layout with Red, Yellow, board, and control buttons.
- `ready-wins-zero` shows `Wins: 0` above both sides before any finished game.
- `ready-play-gate` enables Play when the API key is set and disables it when missing.

## How to get to it (user POV)

- Start the app (`dotnet run --project src/AIConnect4.App` or the verification helper launch).
- Leave settings untouched; do not press Play yet.
- Optionally unset `OPENROUTER_API_KEY` before start to see the missing-key Ready path.

## Driving it with control-c4

Preconditions:

- No prior owned PID, or previous run cleaned up.
- For the missing-key path, launch with `-WithoutApiKey`.
- `control-c4 doctor` returns `"status": "ok"`.

- **Launch.** Run `control-c4.ps1 launch -RunId <id> -Build` (add `-WithoutApiKey` for the keyless path). JSON `title` is `AI Connect 4`.
- **Doctor.** Run `control-c4.ps1 doctor`. Status is `ok`; `statusText` is `Ready` or `Ready (API key missing)`.
- **Assert Ready chrome.** Run `control-c4.ps1 wait-name -Name "Ready"`, `assert-text -Contains "Wins: 0"`, `assert-text -Contains "Games to play"`, `assert-text -Contains "Red"`, `assert-text -Contains "Yellow"`.
- **Assert Play gate.** Run `control-c4.ps1 get-enabled -Name "Play"`. With a key, `enabled` is `true`. With `-WithoutApiKey`, `enabled` is `false`.
- **Proof.** Run `snapshot` and `screenshot` into `artifacts/<runId>/`. Write `proof.txt` with feature id `ready-state`. Artifacts show title, Ready text, and both win zeros.

## Gotchas

- Avalonia may expose Ready as a Text Name exactly `Ready` or `Ready (API key missing)` — assert the path you launched.
- ComboBoxes often have blank Names; do not require them for Ready chrome proof.
- A disabled Play button still appears in the UIA tree; check `get-enabled`, not mere presence.
- Win labels must be single `TextBlock` bindings (`Wins: {0}`). Multi-`Run` TextBlocks expose empty UIA Names.
- `ScriptedMoveSource` lives only in tests. It is not a Ready substitute and is not a UI dry-run.
