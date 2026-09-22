# Win counts

Win counts sit above the Red and Yellow columns and mirror the series score. They stay at zero until a game finishes with a winner, then tick for that side. Draws are shown separately in the centre as Draws: N.

## Sub-features

- `wins-zero-ready` shows `Wins: 0` on both sides at Ready.
- `wins-tick` increments the winner's label after a finished game.
- `wins-reset` returns both labels to `Wins: 0` after New Series.

## How to get to it (user POV)

- Look above Red and Yellow at any time.
- Complete at least one finished game in a series to see a non-zero win (requires Play with API key).
- Choose New Series to clear the series display.

## Driving it with control-c4

Preconditions:

- Doctor ok on an owned window.
- For zero-only proof, Ready state is enough.
- For tick proof, complete `play-series` until a game finishes or accept abort evidence if models fail.

- **Zero at Ready.** Run `assert-text -Contains "Wins: 0"` (matches both sides). Screenshot the window so both headers are visible.
- **Tick after finish.** After a finished game, run `find-text -Contains "Wins: "` and capture the UIA snapshot. At least one side should no longer be only zero if a win occurred; draws alone leave wins at zero and advance `Draws:`.
- **Reset.** After `invoke -Name "New Series"`, assert `Wins: 0` again.
- **Proof.** Feature id `win-counts`. Screenshot must show the labels above the side columns, not a cropped board-only view.

## Gotchas

- Score mirrors `SeriesScore` and updates on Finished games, not on every disc drop.
- Two Text nodes both say `Wins: 0` at Ready — asserting once is enough for zero, but screenshots should show both columns.
- Do not prove wins by reading ViewModel fields; only UIA Text / screenshots count.
