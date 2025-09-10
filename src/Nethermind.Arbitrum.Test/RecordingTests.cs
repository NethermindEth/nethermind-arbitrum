// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Reflection;
using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test;

public class RecordingTests
{
    [TestCase("./Recordings/1__arbos32_basefee92.jsonl", "0x131320467d82b8bfd1fc6504ed4e13802b7e427b1c3d1ff3c367737d4fc18fa9")]
    public void Recording_Always_ProducesCorrectBlockHash(string recordingFilePath, string blockHash)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.BlockProcessingTimeout = 30_000)
            .WithRecording(new FullChainSimulationRecordingFile(recordingFilePath))
            .Build();

        chain.BlockTree.HeadHash.Should().Be(new Hash256(blockHash));
    }

    [Test]
    public void Recording_Always_CoveredWithTest()
    {
        HashSet<string> recordingFiles = Directory.GetFiles("./Recordings", "*.jsonl").ToHashSet();

        IEnumerable<string> recordingInTests = typeof(RecordingTests)
            .GetMethod(nameof(Recording_Always_ProducesCorrectBlockHash))!
            .GetCustomAttributes<TestCaseAttribute>()
            .Select(attribute => (string)attribute.Arguments[0]!);

        foreach (string recordingInTest in recordingInTests)
            recordingFiles.Remove(recordingInTest);

        recordingFiles.Should().BeEmpty($"all recordings must be covered by {nameof(Recording_Always_ProducesCorrectBlockHash)} test");
    }
}
