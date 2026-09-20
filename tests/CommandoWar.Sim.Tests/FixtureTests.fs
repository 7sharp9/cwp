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
    Assert.Equal(14, h.Format)
    Assert.Equal(0x672815D313E0AE51UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0xD355A8FE9C651965UL; 0xB5D1029C4EA8F319UL; 0x896F7B9FA74BFF3DUL; 0xFAB7308909D7B911UL
           0xDA0058EEAE335F8DUL; 0xE6DAC708C3C6FE19UL; 0x0AC818DA82020365UL; 0xDEEF3A08898BD701UL
           0x8A5D0F8E375260D5UL; 0xAAFD1A41DB9A23F9UL; 0x7FC11AA806B3493DUL; 0xD4EF636F2695D061UL
           0x572BC05982ED116DUL; 0x2F77D3B209A875D9UL; 0xB60DD62C872B68B5UL; 0xEBB2DC1C863D8091UL
           0x11D9D095A2DE3EE5UL; 0xB5A33865E5B2BDF9UL; 0x1727FCE22514731DUL; 0xC23A4F95CE757E71UL
           0x2536293F3D7433BFUL; 0x506B8B72BAC9CDFDUL; 0x25B9C269298E4197UL; 0x4960B2B85B11BFE9UL
           0xF8E45FB61F47D207UL; 0x92DB691EC159A1DDUL; 0x75443077F6BAA52FUL; 0x6374EB7E01143FE1UL
           0x874DB43C615FF4DFUL; 0xDB4F015E7E3B5A7DUL; 0x171B8349205C6650UL; 0x9F5E66E7D25BD636UL
           0xE5A4DF413B6FB8DDUL; 0x3F75CD16932AA8C4UL; 0x3D7830F82E9AB58BUL; 0xE2A0199D42419ABAUL
           0x900E0F2C60498401UL; 0x6443ADCA3031EC88UL; 0xB99D446DD800037FUL; 0x27FC9F2AA2CA441EUL |]

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
        Assert.Equal(Order(MoveTo { X = 20; Y = 14 }, Replace), c.Command.Body)

[<Fact>]
let ``command-log parser rejects an unknown verb with the line number`` () =
    match CommandLogFile.parse "test" "1 3 warp 4 4\n" with
    | Ok _ -> Assert.Fail("expected a parse error")
    | Error(CommandLogFile.UnknownDirective(line, verb)) ->
        Assert.Equal(1, line)
        Assert.Equal("warp", verb)
    | Error other -> Assert.Fail($"wrong error: {other}")
