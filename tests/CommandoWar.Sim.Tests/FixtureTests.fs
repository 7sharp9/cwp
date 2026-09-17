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
    Assert.Equal(9, h.Format)
    Assert.Equal(0xA2726329BB740614UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0xC4AA7ADBCA44BFE0UL; 0x2B656E5920408440UL; 0x71B98F53920498BCUL; 0x844F8624FB11D45CUL
           0x53F80F747AD80FD0UL; 0x2084307FF67C7300UL; 0x4DF2267AB6DD5F44UL; 0xE7CFC1528F5F9B44UL
           0x2496618603E04DC0UL; 0xACF09439572F6F60UL; 0x10651CD9E7E3F59CUL; 0x0ACDC7BB01DC06BCUL
           0x702A0F1FE8C9C910UL; 0x7B26B1C53D7F3BE0UL; 0x680698AED304E1B4UL; 0x01E43386AB871DB4UL
           0x21F0E4FA6CA98080UL; 0x6B6956339410F0A0UL; 0x33C5C5289AEC06DCUL; 0x9D6AC9647EF61E7CUL
           0x7C6B2023942FFA92UL; 0xDAA7E09AE2C9C21CUL; 0x560E8F9E090D9FCEUL; 0xEC7C9085D85DE62CUL
           0xE61EFC106A90BEA2UL; 0x87BFB9E5490636BCUL; 0x77FEA3AA5FF924C6UL; 0x0B43FDF62408189CUL
           0x3226C994C0913712UL; 0x1CA554DBC81452BCUL; 0x4F3EC0681DAB75D9UL; 0x1B782242D0412EAFUL
           0x4D46746BE6572948UL; 0x5D3C111796889F01UL; 0xEBA69D54FF136F3AUL; 0x2661BF9E7A4EAD1BUL
           0x3EBCD25CE036A124UL; 0xAA56D8D708F22B0DUL; 0x1ADA1B8F5F232EA6UL; 0xC9694E97A7210117UL |]

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
