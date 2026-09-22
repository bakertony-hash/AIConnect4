# Model pickers

Model pickers let the user choose Red and Yellow profiles (curated catalog plus Custom…), and when the profile supports it, Effort and Speed, while the arena is Ready. After Play, settings lock until New Series.

## Sub-features

- `picker-defaults` shows Red default Jev 1.13 and Yellow default Luna while Ready.
- `picker-custom` reveals the Custom model id text box when Custom… is selected.
- `picker-lock` disables model / effort / speed / Games to play after Play until New Series.

## How to get to it (user POV)

- On the Red or Yellow column, open the Model combo and pick a curated name or Custom….
- If Custom…, type a model id in the Custom model id box.
- Adjust Effort / Speed when those combos are enabled for the selected chat profile.
- Press Play to lock settings; New Series to unlock.

## Driving it with control-c4

Preconditions:

- Ready window (prefer `-WithoutApiKey` so Play cannot start accidentally during chrome checks).
- Doctor ok.

- **Confirm labels.** Run `assert-text -Contains "Model"`, `assert-text -Contains "Effort"`, `assert-text -Contains "Speed"`, `assert-text -Contains "Red"`, `assert-text -Contains "Yellow"`.
- **Capture picker tree.** Run `snapshot -Path artifacts/<runId>/pickers.uia.txt`. In the tree, locate ComboBox nodes near the Model / Effort / Speed labels. Curated selection text may appear as child Text (for example Jev / Luna display names).
- **Lock observation (optional, needs API key).** Invoke Play briefly, snapshot again, and confirm settings combos report `enabled=False` (or Games to play disabled). Then New Series and confirm they re-enable.
- **Proof.** Feature id `model-pickers`. Snapshot + screenshot showing both side columns with Model labels visible.

## Gotchas

- ComboBoxes often have empty UIA Names in Avalonia; drive by snapshot structure and visible selection Text, not by Name alone. Prefer expanding this map with AutomationProperties if agents need clickable Names later.
- Effort / Speed enablement depends on the selected chat profile capabilities — disabled is a valid state for Jev.
- Custom… without a model id blocks Play with a status message when a key is present; capture that text if testing the validation path.
