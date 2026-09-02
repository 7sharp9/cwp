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
    Assert.Equal(1, h.Format)
    Assert.Equal(0xF2F3DF0D820AD9ACUL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0xC848D905A9CAD13FUL; 0x19234AC6465E2F05UL; 0x90B080305E382443UL; 0xE008C6578B0117F5UL
           0xF8119436FD242507UL; 0x09ECD0A79A9D2E55UL; 0xBB613E8ED1CA1D43UL; 0xE8B6D914A2926D85UL
           0x2CA1EF76113BC17FUL; 0x8455F41604132775UL; 0xB12F3560E7A16D83UL; 0x981418895730F065UL
           0x2B70655288477397UL; 0x04343D056D0BAC45UL; 0xD8029633060B5D63UL; 0xE276C8464FB10855UL
           0x8FDADCC37477541FUL; 0x22D0B640903DE885UL; 0x9C70657E45A3E2A3UL; 0x2FE3DACE9E2B5595UL
           0xB577EFF6B20A97B5UL; 0xA11CD0A9CFA11981UL; 0xD3D5AE64EB7CEE39UL; 0x08879506597DB88DUL
           0xD0FBA852FE8E758DUL; 0xD01017A5D95737E1UL; 0xCFB1B389277D8239UL; 0x1A1D9BB7613B6D65UL
           0xA76081C2C6A32DE5UL; 0xA3E0E517D26FED91UL; 0x25315447F9D0E230UL; 0xA9905D83030ECF85UL
           0xE84D22152E175E0AUL; 0x329E1078823EA257UL; 0x7911F8633F9416ECUL; 0xF6D282ED1457EA01UL
           0xF744976403CDB056UL; 0x656789B91AB43663UL; 0xC6541DCD50DD3168UL; 0x838D3AE7DBFB735DUL |]

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
