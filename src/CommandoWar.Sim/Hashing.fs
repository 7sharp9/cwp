namespace CommandoWar.Sim

/// A canonical authoritative-state hash (docs/04_SIMULATION_SPEC.md section 17).
[<Struct>]
type StateHash =
    { /// The canonical-format version the hash was computed over. A hash is
      /// only comparable to another hash with the same format version.
      Format: int
      /// The 64-bit digest of the canonical encoding.
      Value: uint64 }

/// Project contract for turning authoritative world state into a comparable
/// hash. Isolated behind this interface so the digest algorithm or canonical
/// format can be replaced without changing simulation behaviour.
type IStateHasher =
    /// The canonical-format version this hasher encodes.
    abstract member CanonicalFormatVersion: int
    /// The digest algorithm name, for diagnostics and test evidence.
    abstract member Algorithm: string
    /// Hashes authoritative world state.
    abstract member Hash: world: WorldState -> StateHash

/// The canonical state hash.
///
/// Digest: FNV-1a, 64-bit, computed over `Canonical.encode`. FNV-1a is chosen
/// for being tiny, dependency-free, and defined purely in wrapping 64-bit
/// integer arithmetic. It is NOT collision-resistant and no cryptographic or
/// tamper-evidence property is claimed; it exists to detect replay divergence.
///
/// Reference: Fowler / Noll / Vo hash, FNV-1a variant
/// (<https://datatracker.ietf.org/doc/html/draft-eastlake-fnv>).
[<RequireQualifiedAccess>]
module Hashing =

    /// Digest identity, named in tests.
    [<Literal>]
    let Algorithm = "FNV-1a-64"

    [<Literal>]
    let private Offset = 0xCBF29CE484222325UL

    [<Literal>]
    let private Prime = 0x00000100000001B3UL

    /// FNV-1a over a byte span. All arithmetic is wrapping 64-bit.
    let private fnv1a (bytes: byte[]) : uint64 =
        let mutable h = Offset
        for b in bytes do
            h <- (h ^^^ uint64 b) * Prime
        h

    /// The 64-bit FNV-1a digest of an arbitrary byte array. Exposed so tests
    /// can recompute a hash independently of `hash`.
    let digest (bytes: byte[]) : uint64 = fnv1a bytes

    /// Hashes authoritative world state via its canonical encoding.
    let hash (world: WorldState) : StateHash =
        { Format = Canonical.FormatVersion
          Value = fnv1a (Canonical.encode world) }

    /// The project-contract view of the canonical hasher.
    let canonicalHasher: IStateHasher =
        { new IStateHasher with
            member _.CanonicalFormatVersion = Canonical.FormatVersion
            member _.Algorithm = Algorithm
            member _.Hash world = hash world }
