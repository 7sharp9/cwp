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
    Assert.Equal(2, h.Format)
    Assert.Equal(0xE13D7540912C7E25UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x5D8C8970F39771B2UL; 0x986FAB7690854608UL; 0x78C52861050C9DC2UL; 0xD40695B8FAFB561CUL
           0xBDF65E0D29AE9A42UL; 0xB0256E0D8E8C2E90UL; 0xAB9AF8816810B182UL; 0xF2F8B000576FE69CUL
           0x919F42D04CF1B922UL; 0xE2A8F9194D7A34C8UL; 0x7EED1056CB502CD2UL; 0xEF595793C31AF83CUL
           0x329F3AB2B2CBBEB2UL; 0xC767132B1E2EBDE0UL; 0x27D271EBF3B18B72UL; 0x3B2FA9658EB10ADCUL
           0xE60587050481C8B2UL; 0x0F62C2D1BBA02B88UL; 0x27C80E5B13FB6D22UL; 0xEED0A7B655AC8D1CUL
           0x82A85615AAE4D800UL; 0x95D3F98DB887CB64UL; 0xDF031A9CEAE912E0UL; 0xB5810624FE66B394UL
           0xC2CF53891C27B820UL; 0x73EBD725AB4924BCUL; 0xA03BA02B75A0DAD0UL; 0xE2FE27F5CD67C07CUL
           0x9CE2A92D40948A50UL; 0xE683753245907FD4UL; 0x92FF4C99EC571279UL; 0x0744CA8C28A3C39CUL
           0x9E14C1BE3A6A6367UL; 0xCB8897ABEC49C97AUL; 0xB22633B283235655UL; 0xB20CE024173DD318UL
           0xADCFC43BA52AD3F3UL; 0xA2AE579826CB3E86UL; 0x5CEE494A71BD65D1UL; 0xAFA35198CC6BD8D4UL |]

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
