// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.RegularExpressions;
using FluentAssertions;
using Nethermind.Arbitrum.Processing;

namespace Nethermind.Arbitrum.Test.Processing;

[TestFixture]
public class ProcessingStatsFormatterTests
{
    private static readonly Regex AnsiRegex = new("\u001b\\[[0-9;]*m", RegexOptions.Compiled);

    [Test]
    public void Format_MultiBlockChunk_MatchesExampleOutput()
    {
        ProcessingStatsReport report = new(
            BlockNumber: 452963255,
            ChunkBlocks: 128,
            ChunkMGas: 83.17,
            ChunkTx: 617,
            ChunkMs: 316.8,
            RunMs: -1,
            GasPrices: (Min: 0.020f, EstMedian: 0.020f, Ave: 0.020f, Max: 0.024f),
            ChunkCalls: 0,
            ChunkEmptyCalls: 0,
            ChunkSload: 0,
            ChunkSstore: 0,
            ChunkCreates: 0,
            ChunkSelfDestructs: 0,
            ChunkOpCodes: 11652987,
            MGasPerSecond: 262.54,
            Txps: 1947.5,
            Bps: 404.03,
            GasLimit: 32_000_000,
            StylusCallsDelta: 0,
            StylusMs: 0,
            CachedContractsUsed: 0,
            ContractsAnalysed: 0,
            RecoveryQueueSize: 0,
            ProcessingQueueSize: 0);

        string expected =
            """
            Processed  452963128..452963255 |    316.8 ms | elapsed     -1 ms | ⛽ Gas gwei: 0.020 .. 0.020 (0.020) .. 0.024
             Blocks x128         83.17 MGas |     617 txs | stylus          0 | calls           0 | sload      0 | sstore       0 | create    0
             Throughput       262.54 MGas/s | 1,947.5 tps |   404.03 blocks/s | code cache      0 | new        0 | ops 11,652,987 | destruct  0
            """;

        Format(in report).Should().Be(expected);
    }

    [Test]
    public void Format_Teragas_MatchesExampleOutput()
    {
        ProcessingStatsReport report = new(
            BlockNumber: 452963255,
            ChunkBlocks: 128,
            ChunkMGas: 83.17,
            ChunkTx: 617,
            ChunkMs: 316.8,
            RunMs: -1,
            GasPrices: (Min: 0.020f, EstMedian: 0.020f, Ave: 0.020f, Max: 0.024f),
            ChunkCalls: 0,
            ChunkEmptyCalls: 0,
            ChunkSload: 0,
            ChunkSstore: 0,
            ChunkCreates: 0,
            ChunkSelfDestructs: 0,
            ChunkOpCodes: 0,
            MGasPerSecond: 2000.54,
            Txps: 1947.5,
            Bps: 404.03,
            GasLimit: 32_000_000,
            StylusCallsDelta: 0,
            StylusMs: 0,
            CachedContractsUsed: 0,
            ContractsAnalysed: 0,
            RecoveryQueueSize: 0,
            ProcessingQueueSize: 0);

        string expected =
            """
            Processed  452963128..452963255 |    316.8 ms | elapsed     -1 ms | ⛽ Gas gwei: 0.020 .. 0.020 (0.020) .. 0.024
             Blocks x128         83.17 MGas |     617 txs | stylus          0 | calls           0 | sload      0 | sstore       0 | create    0
             Throughput    🔥2000.54 MGas/s | 1,947.5 tps |   404.03 blocks/s | code cache      0 | new        0 | ops          0 | destruct  0
            """;

        Format(in report).Should().Be(expected);
    }

    [Test]
    public void Format_SingleBlockChunk_UsesSingularBlockLabel()
    {
        ProcessingStatsReport report = new(
            BlockNumber: 100,
            ChunkBlocks: 1,
            ChunkMGas: 5.50,
            ChunkTx: 50,
            ChunkMs: 2500.0,
            RunMs: 1000,
            GasPrices: (Min: 0.1f, EstMedian: 0.1f, Ave: 0.1f, Max: 0.2f),
            ChunkCalls: 100,
            ChunkEmptyCalls: 5,
            ChunkSload: 200,
            ChunkSstore: 50,
            ChunkCreates: 10,
            ChunkSelfDestructs: 0,
            ChunkOpCodes: 5000,
            MGasPerSecond: 22.0,
            Txps: 200.0,
            Bps: 4.0,
            GasLimit: 32_000_000,
            StylusCallsDelta: 0,
            StylusMs: 0,
            CachedContractsUsed: 100,
            ContractsAnalysed: 5,
            RecoveryQueueSize: 0,
            ProcessingQueueSize: 0);

        string expected =
            """
            Processed                   100 |  2,500.0 ms | elapsed  1,000 ms | ⛽ Gas gwei: 0.100 .. 0.100 (0.100) .. 0.200
             Block                5.50 MGas |      50 txs | stylus          0 | calls         100 | sload    200 | sstore      50 | create   10
             Throughput        22.00 MGas/s |   200.0 tps |     4.00 blocks/s | code cache    100 | new        5 | ops      5,000 | destruct  0
            """;

        Format(in report).Should().Be(expected);
    }

    [Test]
    public void Format_RecoveryOrProcessingQueueActive_UsesAlternateThroughputLine()
    {
        ProcessingStatsReport report = new(
            BlockNumber: 200,
            ChunkBlocks: 10,
            ChunkMGas: 10.0,
            ChunkTx: 100,
            ChunkMs: 100.0,
            RunMs: 200,
            GasPrices: (Min: 0.5f, EstMedian: 0.5f, Ave: 0.5f, Max: 0.5f),
            ChunkCalls: 0,
            ChunkEmptyCalls: 0,
            ChunkSload: 0,
            ChunkSstore: 0,
            ChunkCreates: 0,
            ChunkSelfDestructs: 0,
            ChunkOpCodes: 1000,
            MGasPerSecond: 100.0,
            Txps: 1000.0,
            Bps: 100.0,
            GasLimit: 32_000_000,
            StylusCallsDelta: 3,
            StylusMs: 0,
            CachedContractsUsed: 0,
            ContractsAnalysed: 0,
            RecoveryQueueSize: 5,
            ProcessingQueueSize: 3);

        string expected =
            """
            Processed        191..      200 |    100.0 ms | elapsed    200 ms | ⛽ Gas gwei: 0.500 .. 0.500 (0.500) .. 0.500
             Blocks x10          10.00 MGas |     100 txs | stylus          3 | calls           0 | sload      0 | sstore       0 | create    0
             Throughput       100.00 MGas/s | 1,000.0 tps |   100.00 blocks/s | recover         5 | process    3 | ops      1,000 | destruct  0
            """;

        Format(in report).Should().Be(expected);
    }

    [Test]
    public void Format_SignificantStylusActivity_AddsSplashEmoji()
    {
        ProcessingStatsReport report = new(
            BlockNumber: 1000,
            ChunkBlocks: 10,
            ChunkMGas: 5.0,
            ChunkTx: 50,
            ChunkMs: 100.0,
            RunMs: 200,
            GasPrices: (Min: 0.5f, EstMedian: 0.5f, Ave: 0.5f, Max: 0.5f),
            ChunkCalls: 12751,
            ChunkEmptyCalls: 5,
            ChunkSload: 22621,
            ChunkSstore: 5,
            ChunkCreates: 2,
            ChunkSelfDestructs: 0,
            ChunkOpCodes: 5630090,
            MGasPerSecond: 50.0,
            Txps: 500.0,
            Bps: 100.0,
            GasLimit: 32_000_000,
            StylusCallsDelta: 3125,
            StylusMs: 12.3,
            CachedContractsUsed: 19716,
            ContractsAnalysed: 0,
            RecoveryQueueSize: 0,
            ProcessingQueueSize: 0);

        string expected =
            """
            Processed        991..     1000 |    100.0 ms | elapsed    200 ms | ⛽ Gas gwei: 0.500 .. 0.500 (0.500) .. 0.500
             Blocks x10           5.00 MGas |      50 txs | stylus    ✨3,125 | calls      12,751 | sload 22,621 | sstore       5 | create    2
             Throughput        50.00 MGas/s |   500.0 tps |   100.00 blocks/s | code cache 19,716 | new        0 | ops  5,630,090 | destruct  0
            """;

        Format(in report).Should().Be(expected);
    }

    [Test]
    public void Format_NullGasPrices_OmitsGasSegment()
    {
        ProcessingStatsReport report = new(
            BlockNumber: 50,
            ChunkBlocks: 1,
            ChunkMGas: 1.0,
            ChunkTx: 1,
            ChunkMs: 100.0,
            RunMs: 100,
            GasPrices: null,
            ChunkCalls: 0,
            ChunkEmptyCalls: 0,
            ChunkSload: 0,
            ChunkSstore: 0,
            ChunkCreates: 33,
            ChunkSelfDestructs: 20,
            ChunkOpCodes: 0,
            MGasPerSecond: 10.0,
            Txps: 10.0,
            Bps: 10.0,
            GasLimit: 32_000_000,
            StylusCallsDelta: 0,
            StylusMs: 0,
            CachedContractsUsed: 0,
            ContractsAnalysed: 0,
            RecoveryQueueSize: 0,
            ProcessingQueueSize: 0);

        // Trailing space on line 1 (`| `) is significant — the empty gas segment leaves it dangling.
        // Use interpolation in a raw string so editor trim_trailing_whitespace cannot silently break the test.
        string expected =
            $$"""
            Processed                    50 |    100.0 ms | elapsed    100 ms |{{" "}}
             Block                1.00 MGas |       1 txs | stylus          0 | calls           0 | sload      0 | sstore       0 | create   33
             Throughput        10.00 MGas/s |    10.0 tps |    10.00 blocks/s | code cache      0 | new        0 | ops          0 | destruct 20
            """;

        Format(in report).Should().Be(expected);
    }

    private static string Format(in ProcessingStatsReport report)
    {
        List<string> lines = new();
        ProcessingStatsFormatter.Format(in report, lines);
        return string.Join('\n', lines.Select(l => AnsiRegex.Replace(l, "")));
    }
}
