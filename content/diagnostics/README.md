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
| `demo.ascii.txt` | The terrain-demo scenario (`src/CommandoWar.Headless/DemoScenario.fs`, 12 x 8) at tick 0: a diagonal elevation ridge, an impassable 2 x 2 block, a movement-cost patch, an opaque wall, and three directional cover edges. Exercises every layer of the ASCII renderer. |
| `demo.svg` | SVG of the terrain-demo scenario at tick 0: hatched impassable cells, dark-bordered opaque cells, elevation shading, cover triangles, agents. |
| `demo.html` | Self-contained scrubber: one SVG per tick for the 21-frame demo run (ticks 0..20), with a slider and Prev / Next. Inline CSS and JS, no external references. |

## Regeneration

From the repository root, after `dotnet build CommandoWar.slnx -c Release`:

```sh
R="dotnet run --project src/CommandoWar.Headless -c Release --"

$R render fixture --tick 0  --format ascii --out content/diagnostics/fixture-tick-000.ascii.txt
$R render fixture --tick 40 --format ascii --out content/diagnostics/fixture-tick-040.ascii.txt
$R render fixture --tick 0  --format svg   --out content/diagnostics/fixture-tick-000.svg
$R render fixture --tick 40 --format svg   --out content/diagnostics/fixture-tick-040.svg
$R render demo --format ascii --out content/diagnostics/demo.ascii.txt
$R render demo --format svg   --out content/diagnostics/demo.svg
$R render demo --format html  --out content/diagnostics/demo.html
```

`render` also accepts a command-log path in place of `fixture` / `demo` (replayed
against the shared fixture initial state), an optional `--layer <name>` to render
a single terrain layer (`elevation`, `passability`, `movement-cost`, `opacity`),
and `--tick N` to pick the frame for the `ascii` / `svg` formats (`html` always
embeds every tick).
