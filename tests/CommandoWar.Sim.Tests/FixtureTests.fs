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
    Assert.Equal(6, h.Format)
    Assert.Equal(0xF1A703A752C0F6B9UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x150D89093BC9C641UL; 0x3C84741325F8A8C1UL; 0x57E8F85C95140235UL; 0x41D748A18F9B49E5UL
           0x8CA1EBD72D3AAC01UL; 0x37862CBC2AD98B31UL; 0x2111C2150C221B8DUL; 0xEA85FFAAC658199DUL
           0xE80EE4D9CF5FE9E1UL; 0x1F280FA49C98A181UL; 0x368F4741DA362755UL; 0xEB6755F0337A2905UL
           0x8A488DBC994415E1UL; 0xD57146F61D2C68D1UL; 0x282B7249BB53691DUL; 0xAA2DA0EBE95A908DUL
           0x871F8A5EC05789A1UL; 0x67D775B13745CF61UL; 0xF5C24EDA81E10815UL; 0x91D758C4242CE4A5UL
           0x1570C920BE41E3A7UL; 0xAEC7D965387F39E5UL; 0x92B4D29865C54A7BUL; 0x76807D006F01B995UL
           0xD66421CB943DB947UL; 0x71880F0882A9AB15UL; 0xA44DEEC6B77B10E3UL; 0x1E1C0818E3B97CC5UL
           0xD6CBBA0C1F11C1C7UL; 0x46B17BCEA997D385UL; 0xB340A8EFC5EB97A4UL; 0xC4235060A1BFF05EUL
           0x2BBE91A830226A55UL; 0xF6F7A35660F42E3CUL; 0x0CD3C8E1A0801AC3UL; 0x00E6A0A37F11BF92UL
           0x8D8411A745C8CD49UL; 0xFBAD4EA87A0C0F80UL; 0x1FB1BFF52F1268B7UL; 0x507D041E109404B6UL |]

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
