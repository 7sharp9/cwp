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
    Assert.Equal(3, h.Format)
    Assert.Equal(0x50BFA007EDFC42FEUL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x49B5D8933477075DUL; 0x9B7976EEAD0CF14BUL; 0x2AC8DFDF044E6F49UL; 0xD4BB26B9DAD3F94BUL
           0x3219F84813D2CD9DUL; 0x83DD96A38C68B78BUL; 0x5947FF1393C23D51UL; 0x75E1D96B4C135DABUL
           0x06EA316E0847421DUL; 0x38C44D4994B0B95BUL; 0x1E022A3B5CB6E109UL; 0x4BBEBC1FD441DABBUL
           0xAC647FAEF4D6F13DUL; 0x0348425CCC72CB7BUL; 0xF329C5C5106FC021UL; 0x47D35A76EAEFF03BUL
           0x53E2903512F26ADDUL; 0x5350B7222CE2AFABUL; 0x50A59D79B0B0CF49UL; 0x46CAE398829007EBUL
           0xC43F76166D58364FUL; 0xE4630408EBD6CAA7UL; 0xB6A7315607E265BBUL; 0xE4C7CBD07E91F453UL
           0x85211392EFD8A68FUL; 0x9A44CD31131334D7UL; 0x63BD517A2A364BD3UL; 0x9A088554D5FA94BBUL
           0xBFE9F82658F4CDAFUL; 0x3C9EAC6F1AE744B7UL; 0xF670BB2CC2DDC64EUL; 0x45E777B5589C6EF7UL
           0xB56D5AFC5752237CUL; 0xAEF6C5B115E9BD45UL; 0xA2C4B26CFB1CEABAUL; 0x33F933880D9A5843UL
           0x7599080132EE3238UL; 0x6F2272B5F185CC01UL; 0x7B1341849D9FED86UL; 0xD9D6EC3DDC1D602FUL |]

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
