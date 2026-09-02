namespace CommandoWar.Sim

/// The project-owned deterministic pseudo-random source
/// (docs/04_SIMULATION_SPEC.md section 5, docs/09_TEST_STRATEGY.md section 3).
///
/// One algorithm is implemented: SplitMix64. It is chosen because it is small,
/// widely published, uses only fixed-width 64-bit integer arithmetic, has a
/// single-word serialisable state, and has publicly verifiable output vectors.
///
/// Reference: Steele, Lea, Flood, "Fast Splittable Pseudorandom Number
/// Generators", OOPSLA 2014; public-domain reference implementation at
/// <https://prng.di.unimi.it/splitmix64.c>.
///
/// This generator is NOT a cryptographic primitive and no security property is
/// claimed. It exists to make authoritative outcomes reproducible.

/// Identifies the generator algorithm that produced a `RandomState`. The
/// integer values are part of the canonical encoding and must never be
/// renumbered.
type RandomAlgorithm =
    | SplitMix64 = 1

/// Fully serialisable explicit random state. Every field is a value type so
/// the record round-trips through the canonical encoding without loss.
[<Struct>]
type RandomState =
    { /// The algorithm that owns this state.
      Algorithm: RandomAlgorithm
      /// The algorithm version. Bumped only when the output sequence changes.
      AlgorithmVersion: int
      /// The full generator state. For SplitMix64 this is the 64-bit additive
      /// counter, advanced by the gamma constant on every draw.
      Word: uint64
      /// Number of draws taken from this stream since it was created. Carried
      /// for replay diagnostics (docs/09_TEST_STRATEGY.md section 2.4); the
      /// generator itself does not read it.
      Draws: uint64 }

/// Project contract for a deterministic random source. All authoritative
/// randomness must enter through an implementation of this interface. A future
/// algorithm swap replaces the implementation without touching callers.
type IDeterministicRandom =
    /// Stable algorithm name, recorded in golden-vector tests and diagnostics.
    abstract member Name: string
    /// Algorithm version. Changes when the output sequence changes.
    abstract member Version: int
    /// Expands a caller-supplied 64-bit seed into an initial state.
    abstract member Create: seed: uint64 -> RandomState
    /// Produces the next 64-bit output and the advanced state.
    abstract member Next: state: RandomState -> struct (uint64 * RandomState)

[<RequireQualifiedAccess>]
module SplitMix64 =

    /// Algorithm identity. Named in golden-vector tests.
    [<Literal>]
    let Name = "SplitMix64"

    /// Output-sequence version. Bump only if the arithmetic below changes.
    [<Literal>]
    let Version = 1

    /// The SplitMix64 additive constant (the "golden gamma"): the odd 64-bit
    /// integer closest to 2^64 / phi.
    [<Literal>]
    let private Gamma = 0x9E3779B97F4A7C15UL

    [<Literal>]
    let private MixA = 0xBF58476D1CE4E5B9UL

    [<Literal>]
    let private MixB = 0x94D049BB133111EBUL

    /// Seed expansion: the 64-bit seed initialises the additive counter
    /// directly. No pre-mixing is applied, so the first output is the mix of
    /// `seed + Gamma`.
    let create (seed: uint64) : RandomState =
        { Algorithm = RandomAlgorithm.SplitMix64
          AlgorithmVersion = Version
          Word = seed
          Draws = 0UL }

    /// Advances the state and returns the next 64-bit output. All arithmetic
    /// is wrapping (unchecked) 64-bit, which is the defined behaviour of the
    /// algorithm.
    let next (state: RandomState) : struct (uint64 * RandomState) =
        if state.Algorithm <> RandomAlgorithm.SplitMix64 then
            invalidArg (nameof state) $"expected SplitMix64 state, got {state.Algorithm}"

        let counter = state.Word + Gamma
        let mutable z = counter
        z <- (z ^^^ (z >>> 30)) * MixA
        z <- (z ^^^ (z >>> 27)) * MixB
        z <- z ^^^ (z >>> 31)

        struct (z,
                { state with
                    Word = counter
                    Draws = state.Draws + 1UL })

    /// The project-contract view of this generator.
    let generator: IDeterministicRandom =
        { new IDeterministicRandom with
            member _.Name = Name
            member _.Version = Version
            member _.Create seed = create seed
            member _.Next state = next state }

[<RequireQualifiedAccess>]
module RandomStream =

    /// The default stream seed used by headless construction helpers that do
    /// not take an explicit seed. Deterministic tests should always pass their
    /// own seed rather than rely on this.
    [<Literal>]
    let DefaultSeed = 0UL

    /// Creates a SplitMix64 stream from a seed.
    let create (seed: uint64) : RandomState = SplitMix64.create seed

    /// The next 64-bit output and advanced state.
    let next (state: RandomState) : struct (uint64 * RandomState) = SplitMix64.next state
