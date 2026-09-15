module CommandoWar.Sim.Tests.FixtureTests

open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Regression guard for the shared framework-spike fixture
// (src/CommandoWar.Headless/Fixture.fs, content/fixtures/SPIKE-FIXTURE.md).
// Both TASK-004 (Godot) and TASK-005 (Mibo) reproduce these hashes from their
// own hosts; if CommandoWar.Sim changes the numbers here without an intentional
// format-version bump, that is a determinism regression and both spikes'
// evidence silently rots.

[<Fact>]
let ``fixture parameters are the pinned values`` () =
    Assert.Equal(32, Fixture.bounds.Width)
    Assert.Equal(32, Fixture.bounds.Height)
    Assert.Equal(20260902UL, Fixture.Seed)
    Assert.Equal(40L, Fixture.TickCount)
    Assert.Equal(3, AgentId.value Fixture.MovedAgent)
    Assert.Equal({ X = 20; Y = 14 }, Fixture.MoveTarget)

[<Fact>]
let ``fixture initial state hash is pinned`` () =
    let h = Hashing.hash (Fixture.initialState ())
    Assert.Equal(7, h.Format)
    Assert.Equal(0xBE2636723F99F53AUL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x0BBDD2A32CE88786UL; 0x35DBCDE0E4F48FEEUL; 0x36D0EDD8D163AE8AUL; 0x2689F8DB132F53CAUL
           0x15A4C3EC76FC219EUL; 0xFFC8F6491D1D8E46UL; 0x66DFD1A019AC6EFAUL; 0x365DFF65492963AAUL
           0x8C185EFAD1344916UL; 0xAA830EE8778BD20EUL; 0xC0CEC2EAC1973DCAUL; 0x960935410A8BA52AUL
           0x22369B4F34CD293EUL; 0xB376D53638C4DF96UL; 0x73368EBA4339A59AUL; 0xD1524188E851716AUL
           0xEAA1A03F24B02EC6UL; 0xDBB808A4C79AB94EUL; 0x1B2F2F31627FA0AAUL; 0x30FD7764A9B3B2EAUL
           0xECD2A66735B33274UL; 0x7D3F3761E988C522UL; 0x8FAB4343DD35C2D8UL; 0x06E21303595009F2UL
           0x0C535972100B914CUL; 0x2FCD7DAF3AD3914AUL; 0x33FAE4823B812F88UL; 0xB9BC48842C5A710AUL
           0x77003C009E690B94UL; 0x0A73CEE90B7F1F12UL; 0x140F6468D58DFCDFUL; 0xB91918166FD008C5UL
           0xAAE9267DF334E9DEUL; 0xB2ECCF6F33E16413UL; 0x2FDC38406561C69CUL; 0x5135C3C589C03AF9UL
           0xB10463F6772828D2UL; 0x1C0F1E3638316DC7UL; 0x901E021E648FDF20UL; 0x56395A49904D017DUL |]

    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(40, outcome.TickHashes.Length)
        let actual = outcome.TickHashes |> Array.map (fun cp -> cp.Hash.Value)
        Assert.Equal<uint64[]>(expected, actual)

[<Fact>]
let ``canonical command-log file parses to the fixture command`` () =
    let text = "version 1\n# comment\n\n1 3 move 20 14\n"

    match CommandLogFile.parse "test" text with
    | Error e -> Assert.Fail(CommandLogFile.describeError e)
    | Ok cmds ->
        Assert.Single(cmds) |> ignore
        let c = cmds.[0]
        Assert.Equal(1L, c.Tick)
        Assert.Equal(0, c.Sequence)
        Assert.Equal(3, AgentId.value c.Command.Agent)
        Assert.Equal(MoveTo { X = 20; Y = 14 }, c.Command.Intent)

[<Fact>]
let ``command-log parser rejects an unknown verb with the line number`` () =
    match CommandLogFile.parse "test" "1 3 warp 4 4\n" with
    | Ok _ -> Assert.Fail("expected a parse error")
    | Error(CommandLogFile.UnknownDirective(line, verb)) ->
        Assert.Equal(1, line)
        Assert.Equal("warp", verb)
    | Error other -> Assert.Fail($"wrong error: {other}")
