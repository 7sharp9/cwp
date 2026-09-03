## 2026-09-03 - TASK-004 interactive addendum - Godot editor GUI still not drivable in-session

**Owner:** Dave with coding-agent assistance
**Source revision:** `aa48abc` (Add Mibo framework spike and evidence)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; Godot
`4.7.2.stable.mono.official.ed1daf0bf` at
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\`; terminal
session, no interactive display
**Status change:** none (TASK-004 stays `done`)

### Why this entry exists

TASK-006 requires the `docs/06` section 12 content-edit exercise to be run
**interactively in the Godot editor** (visual TileMap edit, inspector marker
add, sim-property change, scene hot-reload / F5 / debugger, invalid-edit
failure) so that Godot's core claimed advantage - editor iteration speed - is
measured rather than asserted. The TASK-004 session recorded this as not done
(headless session, understating Godot).

### What was attempted and what happened

- `Godot_..._console.exe --editor --headless --quit --path src/CommandoWar.Client.Godot`
  -> exit 0. The editor imports the spike project, loads global class names,
  verifies GDExtensions, initialises plugins, loads the editor layout, and quits
  cleanly. **New evidence:** the spike project opens in the 4.7.2 editor with no
  import or script error.
- Driving the editor GUI itself (painting a TileMap cell, dragging a
  `Marker2D` in the inspector, editing an exported property, triggering scene
  hot-reload, pressing F5, stepping in the debugger, watching a live remote
  scene tree) requires a human at an interactive display. It cannot be done
  from this terminal session, exactly as in the TASK-004 session.
- `--export-release "Windows Desktop"` now fails earlier than in TASK-004:
  `This project doesn't have an export_presets.cfg file at its root` (the
  TASK-004 preset was transient and never committed). The underlying packaging
  blocker is unchanged: `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\`
  exists but is empty, so no self-contained export can complete regardless of
  the preset.

### Decision on the evidence gap

Per Dave's instruction for this session: do not block indefinitely on the
editor-GUI measurement; accept the decision on the evidence in hand with the
authoring-tool comparison recorded as a known limitation, since the direction of
the conclusion is not in doubt. The ADR-0001 evidence-table cells for Godot
content authoring are therefore **left as "editor hot-reload / inspector
drag-edit NOT measured this session (no GUI)"**; they are not upgraded to a
measured claim. The ADR-0001 scored table docks Godot's content-authoring driver
0.5 for this, and adds a review trigger (B-025 must record a real editor
edit->visible-result measurement).

### Documents updated

- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (scored driver table notes; review
  trigger 1)
- `docs/12_PROGRESS_LEDGER.md` (this entry)
