# Golden diagnostic renders

Byte-for-byte reference output of the diagnostic renderers
(`src/CommandoWar.Headless/DiagnosticRender.fs`) over a `DiagnosticFrame`
(`src/CommandoWar.Sim/Diagnostics.fs`).

`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` compares fresh renderer output
against these files. A diff here is either an intentional renderer change (in
which case regenerate the files with the commands below and note it in the
progress ledger) or a regression.

The renderers emit `\n` line endings and no timestamps or generated ids, so the
output is stable across machines and runs. `.gitattributes` pins this directory
to `eol=lf` so the byte comparison holds on Windows too.

## Files

| File | Shows |
|---|---|
| `fixture-tick-000.ascii.txt` | The shared spike fixture (`src/CommandoWar.Headless/Fixture.fs`, 32 x 32, 6 friendly agents) at tick 0: empty terrain, agents at column x = 0, no events. |
| `fixture-tick-040.ascii.txt` | The same fixture at the final tick 40: agent 3 has reached `(20, 14)`, all agents at rest. |
| `fixture-tick-000.svg` | SVG of the fixture at tick 0. |
| `fixture-tick-040.svg` | SVG of the fixture at tick 40, with the `MovementCompleted` event ring and agent 3's (now absent) destination line. |
| `fixture-mid-route.ascii.txt` | The shared fixture at tick 25: agent 3 is at `(20, 8)`, still en route to `(20, 14)`. `Diagnostics.frameOf` emits a `PlannedPath` overlay from the agent's stored `AgentState.Route` (TASK-015); `S` = the planned-from cell `(0, 3)`, `+` = a path cell, `G` = the destination, `@` = agent 3 on the route. |
| `fixture-mid-route.svg` | SVG of the same mid-route frame: the followed path as a solid polyline with a green start disc and an orange goal box. |
| `demo.ascii.txt` | The terrain-demo scenario (`src/CommandoWar.Headless/DemoScenario.fs`, 12 x 8) at tick 0: a diagonal elevation ridge, an impassable 2 x 2 block, a movement-cost patch, an opaque wall, and three directional cover edges. Exercises every layer of the ASCII renderer. |
| `demo.svg` | SVG of the terrain-demo scenario at tick 0: hatched impassable cells, dark-bordered opaque cells, elevation shading, cover triangles, agents. |
| `demo.html` | Self-contained scrubber: one SVG per tick for the 21-frame demo run (ticks 0..20), with a slider and Prev / Next. Inline CSS and JS, no external references. The moving ticks carry the `PlannedPath` overlay `Diagnostics.frameOf` emits for each agent following a route (TASK-015). |
| `los.ascii.txt` | The line-of-sight demo (`src/CommandoWar.Headless/LosDemo.fs`, 12 x 12) at tick 0 with five `--los` rays: a clear ray, a ray blocked by the opaque wall at `(5,4)`, a ray grazing the corner of the lone wall `(9,3)` (visible), a ray blocked by the ridge peak `(3,7)`, and a ray blocked by the `(9,7)`/`(8,8)` solid corner. `*` = traced cell, `x` = blocking cell. |
| `los.svg` | SVG of the same LOS demo frame: each ray a dashed line with traced-cell dots and a red cross on its blocker. |
| `path.ascii.txt` | The pathfinding demo (`src/CommandoWar.Headless/PathDemo.fs`, 16 x 12) at tick 0 with four `--path` routes: a straight clear route, a route detouring around the impassable `x = 5` wall, a route preferring a cheap detour over the `x = 10..11` movement-cost patch, and a no-path route to the walled-off pocket `(14,9)`. `+` = path cell, `S` = start, `G` = goal. |
| `path.svg` | SVG of the same pathfinding demo frame: each route a solid polyline with a green start disc and an orange goal box; the no-path route shows only its markers. |

## Regeneration

From the repository root, after `dotnet build CommandoWar.slnx -c Release`:

```sh
R="dotnet run --project src/CommandoWar.Headless -c Release --"

$R render fixture --tick 0  --format ascii --out content/diagnostics/fixture-tick-000.ascii.txt
$R render fixture --tick 40 --format ascii --out content/diagnostics/fixture-tick-040.ascii.txt
$R render fixture --tick 0  --format svg   --out content/diagnostics/fixture-tick-000.svg
$R render fixture --tick 40 --format svg   --out content/diagnostics/fixture-tick-040.svg
$R render fixture --tick 25 --format ascii --out content/diagnostics/fixture-mid-route.ascii.txt
$R render fixture --tick 25 --format svg   --out content/diagnostics/fixture-mid-route.svg
$R render demo --format ascii --out content/diagnostics/demo.ascii.txt
$R render demo --format svg   --out content/diagnostics/demo.svg
$R render demo --format html  --out content/diagnostics/demo.html

LOS="--los 1,1:10,1 --los 1,4:10,4 --los 7,2:10,5 --los 1,7:6,7 --los 6,5:11,10"
$R render los $LOS --format ascii --out content/diagnostics/los.ascii.txt
$R render los $LOS --format svg   --out content/diagnostics/los.svg

PATHS="--path 1,1:4,1 --path 2,6:9,6 --path 9,3:13,3 --path 1,10:14,9"
$R render path $PATHS --format ascii --out content/diagnostics/path.ascii.txt
$R render path $PATHS --format svg   --out content/diagnostics/path.svg
```

`render` also accepts a command-log path in place of `fixture` / `demo` / `los` /
`path` (replayed against the shared fixture initial state), an optional
`--layer <name>` to render a single terrain layer (`elevation`, `passability`,
`movement-cost`, `opacity`), `--tick N` to pick the frame for the `ascii` / `svg`
formats (`html` always embeds every tick), a repeatable `--los AX,AY:BX,BY` to
attach a line-of-sight ray overlay (computed via `Sight.trace` over the target's
terrain), and a repeatable `--path AX,AY:BX,BY` to attach a planned-path overlay
(computed via `Pathfinding.find` over the target's terrain). `--los` and
`--path` compose.
