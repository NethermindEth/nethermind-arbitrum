// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Consensus.Stateless;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Execution;

/// <summary>
/// Test-only representation of an expected witness for a single block. Persisted as one JSONL
/// file per recording (path from <see cref="RecordingWitnessExpectedFilePath"/>) with one serialized entry per
/// line, ordered by <see cref="Pos"/>. The bootstrap path truncates the JSONL on the first
/// write of each `dotnet test` run and appends thereafter, so a clean run produces a complete,
/// sorted file ready to be consumed by the comparison path on the next run.
///
/// The witness fields (Codes/State/Keys/Headers) are stored separately rather than collapsing
/// them into <see cref="RecordResult.Preimages"/>: this makes diffs/debugging easier when a
/// preimage goes missing because we can tell which category it was supposed to belong to.
/// </summary>
internal sealed class ExpectedWitnessRecording
{
    private static readonly EthereumJsonSerializer Serializer = new();

    public ulong Pos { get; init; }
    public Hash256 BlockHash { get; init; } = null!;
    public ExpectedWitnessFields Witness { get; init; } = new();
    // Inner type is concrete Dictionary<string, byte[]> rather than IReadOnlyDictionary so
    // System.Text.Json can deserialize it; we cast to IReadOnlyDictionary in ToRecordResult.
    public Dictionary<Hash256, Dictionary<string, byte[]>> UserWasms { get; init; } = new();

    public sealed class ExpectedWitnessFields
    {
        public byte[][] Codes { get; init; } = Array.Empty<byte[]>();
        public byte[][] State { get; init; } = Array.Empty<byte[]>();
        public byte[][] Keys { get; init; } = Array.Empty<byte[]>();
        public byte[][] Headers { get; init; } = Array.Empty<byte[]>();
    }

    /// <summary>
    /// Path to the Recordings/Witnesses folder under the test assembly's output directory, populated
    /// at build time by the csproj's <c>Content Include="Recordings/**/*"</c> copy step. Anchoring on
    /// <see cref="AppContext.BaseDirectory"/> rather than a source-tree path keeps the resolution
    /// independent of deterministic-source-path rewriting (which on CI maps <c>[CallerFilePath]</c>
    /// to a non-existent <c>/_/...</c> path).
    ///
    /// Regenerating files writes here too, so the regen workflow is:
    ///   1) delete the stale JSONL(s) under <c>src/Nethermind.Arbitrum.Test/Recordings/Witnesses/...</c>,
    ///   2) run the tests (they write the new expected JSONL into this output dir),
    ///   3) copy the regenerated JSONL(s) back to the source tree and commit.
    ///
    /// Layout under this dir:
    /// - <c>{recording}__expected__witness.jsonl</c> — one file per recording-based test source,
    ///   one JSONL line per block.
    /// - <c>ArbitrumWitnessGenerationTests/{testName}.jsonl</c> — one file per on-the-fly custom
    ///   test in <see cref="ArbitrumWitnessGenerationTests"/>, single line.
    /// </summary>
    private static readonly string s_runtimeWitnessesDir =
        Path.Combine(AppContext.BaseDirectory, "Recordings", "Witnesses");

    private const string CustomTestsSubdir = "ArbitrumWitnessGenerationTests";

    /// <summary>JSONL location holding one expected witness per line for the given recording.</summary>
    public static string RecordingWitnessExpectedFilePath(string recordingFilePath)
    {
        string name = Path.GetFileNameWithoutExtension(recordingFilePath);
        return Path.Combine(s_runtimeWitnessesDir, $"{name}__expected__witness.jsonl");
    }

    /// <summary>
    /// JSONL location holding the single expected witness for a custom (on-the-fly chain) test,
    /// keyed by the test method's name. Stored under the <c>ArbitrumWitnessGenerationTests</c>
    /// subfolder to keep them separate from recording-driven expected files.
    /// </summary>
    public static string CustomTestWitnessExpectedFilePath(string testName)
        => Path.Combine(s_runtimeWitnessesDir, CustomTestsSubdir, $"{testName}.jsonl");

    // Tracks which expected JSONL files have already been truncated within the current test
    // run, so the very first bootstrap write per recording starts the file from scratch and
    // every subsequent write appends to it. Lives for the lifetime of the AppDomain, i.e. one
    // `dotnet test` invocation.
    private static readonly HashSet<string> s_truncatedThisRun = new();
    private static readonly object s_writeLock = new();

    public static IReadOnlyList<ExpectedWitnessRecording> ReadAll(string jsonlFilePath)
    {
        string[] lines = File.ReadAllLines(jsonlFilePath);
        List<ExpectedWitnessRecording> entries = new(lines.Length);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            entries.Add(Serializer.Deserialize<ExpectedWitnessRecording>(line));
        }
        return entries;
    }

    /// <summary>
    /// Method used initially to bootstrap the expected witness files from existing recordings (see Recordings/Witnesses/*).
    /// Writes a new expected witness entry for the given recording. Appends to any existing file
    /// for that recording, but truncates on the first write of each `dotnet test` run to ensure
    /// we start with a clean slate.
    /// </summary>
    public static void WriteExpectedWitnessFileFromRecording(
        string recordingFilePath,
        ulong pos,
        Hash256 blockHash,
        ArbitrumWitness witness,
        IReadOnlyDictionary<Hash256, IReadOnlyDictionary<string, byte[]>> userWasms)
    {
        string path = RecordingWitnessExpectedFilePath(recordingFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string line = Serializer.Serialize(BuildEntry(pos, blockHash, witness, userWasms)) + "\n";

        lock (s_writeLock)
        {
            if (s_truncatedThisRun.Add(path))
                File.WriteAllText(path, line);
            else
                File.AppendAllText(path, line);
        }
    }

    /// <summary>
    /// Method used initially to bootstrap the expected witness files from an existing test (see Recordings/Witnesses/ArbitrumWitnessGenerationTests/*).
    /// Writes the single expected witness entry for a custom test, keyed by the test method name.
    /// Truncates any existing file (custom tests produce exactly one entry, so no append semantics).
    /// </summary>
    public static void WriteExpectedWitnessFileFromTest(
        string testName,
        ulong pos,
        Hash256 blockHash,
        ArbitrumWitness witness,
        IReadOnlyDictionary<Hash256, IReadOnlyDictionary<string, byte[]>> userWasms)
    {
        string path = CustomTestWitnessExpectedFilePath(testName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, Serializer.Serialize(BuildEntry(pos, blockHash, witness, userWasms)) + "\n");
    }

    /// <summary>
    /// Build a <see cref="RecordResult"/> equivalent to what <see cref="RecordResult"/>'s constructor
    /// would produce for these witness fields. The transient <see cref="Witness"/> is disposed straight
    /// away — <see cref="RecordResult"/> only retains the underlying byte[] references in its Preimages
    /// dictionary, so the ArrayPoolList wrappers can be returned to the pool immediately.
    /// </summary>
    public RecordResult ToRecordResult()
    {
        Witness witness = new()
        {
            Codes = new ArrayPoolList<byte[]>(Witness.Codes.Length, Witness.Codes),
            State = new ArrayPoolList<byte[]>(Witness.State.Length, Witness.State),
            Keys = new ArrayPoolList<byte[]>(Witness.Keys.Length, Witness.Keys),
            Headers = new ArrayPoolList<byte[]>(Witness.Headers.Length, Witness.Headers),
        };

        Dictionary<ValueHash256, IReadOnlyDictionary<string, byte[]>> userWasmsByValueHash =
            UserWasms.ToDictionary(
                kvp => new ValueHash256(kvp.Key.Bytes),
                kvp => (IReadOnlyDictionary<string, byte[]>)kvp.Value);

        using ArbitrumWitness arbWitness = new(witness, userWasmsByValueHash);
        return new RecordResult(Pos, BlockHash, arbWitness);
    }

    private static ExpectedWitnessRecording BuildEntry(
        ulong pos,
        Hash256 blockHash,
        ArbitrumWitness witness,
        IReadOnlyDictionary<Hash256, IReadOnlyDictionary<string, byte[]>> userWasms) => new()
        {
            Pos = pos,
            BlockHash = blockHash,
            Witness = new ExpectedWitnessFields
            {
                Codes = witness.Witness.Codes.ToArray(),
                State = witness.Witness.State.ToArray(),
                Keys = witness.Witness.Keys.ToArray(),
                Headers = witness.Witness.Headers.ToArray(),
            },
            UserWasms = userWasms.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToDictionary(inner => inner.Key, inner => inner.Value)),
        };
}
