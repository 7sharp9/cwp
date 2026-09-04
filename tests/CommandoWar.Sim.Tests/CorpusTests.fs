module CommandoWar.Sim.Tests.CorpusTests

open System
open System.IO
open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Data-driven determinism guard over the committed replay corpus
// (content/replays/, TASK-016, backlog B-012). The corpus files are copied
// next to the test assembly by the project file (the content/diagnostics/*
// copy-glob precedent). Each entry is replayed from its initial state, checked
// against a second independent run of itself, and compared to the committed
// content/replays/<name>.md table; a mismatch here fails dotnet test without
// an opt-in `cwheadless corpus` run.

let private corpusDir = Path.Combine(AppContext.BaseDirectory, "replays")

/// One `[<MemberData>]` row per corpus entry name.
let entryNames: obj[] seq =
    Corpus.all |> Array.toSeq |> Seq.map (fun e -> [| box e.Name |])

[<Theory>]
[<MemberData(nameof entryNames)>]
let ``corpus entry replays deterministically and matches its committed table`` (name: string) =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = name)

    match Corpus.checkEntry corpusDir entry with
    | Corpus.Passed -> ()
    | other -> Assert.Fail($"corpus entry '{name}' did not match its committed table: {other}")

[<Fact>]
let ``the corpus includes the spike fixture and is not an independent re-pin of it`` () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "spike-fixture")

    // The committed command log parses to exactly the fixture command.
    match Corpus.loadLog corpusDir entry with
    | Error m -> Assert.Fail(m)
    | Ok cmds ->
        Assert.Single(cmds) |> ignore
        Assert.Equal(3, AgentId.value cmds.[0].Command.Agent)
        Assert.Equal(MoveTo { X = 20; Y = 14 }, cmds.[0].Command.Intent)
        Assert.Equal(1L, cmds.[0].Tick)

    // The committed per-tick table is the same 40-value sequence Fixture.run ()
    // produces, so it is not an independent re-pin of SPIKE-FIXTURE.md /
    // FixtureTests.fs.
    let tableText = File.ReadAllText(Path.Combine(corpusDir, "spike-fixture.md"))

    match Corpus.parseTable tableText, Fixture.run () with
    | Error m, _ -> Assert.Fail(m)
    | _, Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok table, Ok outcome ->
        Assert.Equal(0xF2F3DF0D820AD9ACUL, table.InitialHash)
        Assert.Equal(0x838D3AE7DBFB735DUL, table.FinalHash)
        Assert.Equal(33, table.EventCount)

        let fromRun = outcome.TickHashes |> Array.map (fun cp -> cp.Tick, cp.Hash.Value)
        Assert.Equal<(int64 * uint64)[]>(fromRun, table.TickHashes)

[<Fact>]
let ``a perturbed command log is reported as a table mismatch at the first divergent tick`` () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "wall-detour")

    match Corpus.loadLog corpusDir entry with
    | Error m -> Assert.Fail(m)
    | Ok cmds ->
        // Perturb the one recorded destination without touching the file on
        // disk: retarget the move a cell over.
        let perturbed =
            cmds
            |> Array.map (fun c ->
                match c.Command.Intent with
                | MoveTo _ ->
                    { c with
                        Command = Command.moveTo c.Command.Id c.Command.IssueTick c.Command.Agent { X = 9; Y = 3 } })

        match Corpus.run entry cmds, Corpus.run entry perturbed with
        | Ok reference, Ok candidate ->
            match Divergence.compare reference candidate with
            | Diverged(point, _, _) -> Assert.Equal(1L, point.Tick)
            | other -> Assert.Fail($"expected the perturbed log to diverge at tick 1, got {other}")
        | Error e, _
        | _, Error e -> Assert.Fail($"{e}")
