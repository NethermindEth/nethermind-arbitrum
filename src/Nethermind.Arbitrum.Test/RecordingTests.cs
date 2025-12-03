// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Reflection;
using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test;

public class RecordingTests
{
    [TestCase("./Recordings/1__arbos32_basefee92.jsonl", 18, "0x131320467d82b8bfd1fc6504ed4e13802b7e427b1c3d1ff3c367737d4fc18fa9")]
    [TestCase("./Recordings/2__stylus.jsonl", 18, "0x13acf142e2463eaf5049f9fe1b64f0bf5d8c6ea7ebfd950335582e7a63746ced")]
    [TestCase("./Recordings/2__stylus.jsonl", 19, "0xe869b42547c1c017efb9043524612975f2404412f10878a8d1f273ba11c3df83")] // Solidity Counter
    [TestCase("./Recordings/2__stylus.jsonl", 20, "0x2e8e1d1e4e868a5657e36383d586df9eaee84814eea51fe1b1709d976c65e820")] // Solidity Call
    [TestCase("./Recordings/2__stylus.jsonl", 21, "0x9e8f2027cb191241cbefd09607ae756edd13ff8f32058e76c0d6ddb100994e13")] // Stylus Counter
    [TestCase("./Recordings/2__stylus.jsonl", 22, "0xd05a92ed33d378b973ebbee06959e3b60cfeee82b2a82831ef155df733a27f2c")] // Stylus Counter Activate
    [TestCase("./Recordings/2__stylus.jsonl", 23, "0x7bfd6c04e32d3425c67b8a24944fb67285f7b88738136e485c4789b5d21ba2fe")] // Stylus Call
    [TestCase("./Recordings/2__stylus.jsonl", 24, "0x38c6c22cdd575cc25c039727aa23be05d466ff941235ab6b87acd247f97ff7d9")] // Stylus Call Activate
    [TestCase("./Recordings/2__stylus.jsonl", 25, "0x52344dd73d634837d7b0a4675365fe1b340a3a224b16fce1dcc1823ef3716742")]
    [TestCase("./Recordings/3__stylus.jsonl", 27, "0x3161c61e1363ad106f22da57372e585a07dbc095e22898b0f7464c743006ba6a")]
    [TestCase("./Recordings/3__stylus.jsonl", 29, "0x7a8ab0594d7efc5045b95f756669e233347ab3f2b1dab12c4249f2467bb24ffa")]
    [TestCase("./Recordings/3__stylus.jsonl", 36, "0x0b0f4cb5e19828edebab00b8c7799dedbfb48f8d39b6a3dc507dd4673da28540")]
    [TestCase("./Recordings/3__stylus_cost.jsonl", 24, "0xde1712903062b8a40980605b95d1bba2d5b9f31b1b726e7353bfd58a8138ac0f")]
    [TestCase("./Recordings/4__stylus_nested.jsonl", 28, "0xa05031784dd78cb9bac930d13de0c7d57302f3f5377257be665ce61852328b1f")]
    [TestCase("./Recordings/5__stylus.jsonl", 42, "0x57e78933f559555cfde85192a497175c598213ddb0c8a2cf545381dd25ca1d34")]
    [TestCase("./Recordings/5__stylus.jsonl", 47, "0xc8736abd347f2207fe3ecae3029f905c2ab1256dde129338fe722f95d092fba6")]
    [TestCase("./Recordings/6__stylus_contract_address.jsonl", 39, "0x6237d495257439864863d34241a84e8f1117ba758bb86be1dfc2877261d4b43e")]
    [TestCase("./Recordings/6__stylus_contract_address.jsonl", 44, "0x5342b7c09b725b945fb0ffd746cff5b4f03719a3a0aed63aed961d045b44920e")]
    [TestCase("./Recordings/6__stylus_contract_address.jsonl", 46, "0x358b215bebc0a68547cdab61df329d1a73faeec6f6fb467016a851aef4b04a73")] // MaxCallDepth Cases
    [TestCase("./Recordings/6__stylus_contract_address.jsonl", 50, "0xadf0d2b498a139464fd740f64a2182d5696bd8513f925f6ce1ba80ff65f689fb")] // MaxCallDepth Cases
    [TestCase("./Recordings/7__stylus_accesslist.jsonl", 37, "0xf63694044a83cfac5cf0129101c254d23d23a7e7f00c8207cc69c8f1a565205c")] // MaxCallDepth Cases
    public void Recording_Always_ProducesCorrectBlockHash(string recordingFilePath, byte numberToDigest, string blockHash)
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.BlockProcessingTimeout = 30_000)
            .WithRecording(new FullChainSimulationRecordingFile(recordingFilePath), numberToDigest)
            .Build();

        chain.BlockTree.HeadHash.Should().Be(new Hash256(blockHash));
    }

    [Test]
    public void Recording_Always_CoveredWithTest()
    {
        HashSet<string> recordingFiles = Directory.GetFiles("./Recordings", "*.jsonl")
            .Select(p => p.Replace('\\', '/')).ToHashSet();

        IEnumerable<string> recordingInTests = typeof(RecordingTests)
            .GetMethod(nameof(Recording_Always_ProducesCorrectBlockHash))!
            .GetCustomAttributes<TestCaseAttribute>()
            .Select(attribute => (string)attribute.Arguments[0]!);

        foreach (string recordingInTest in recordingInTests)
            recordingFiles.Remove(recordingInTest);

        recordingFiles.Should().BeEmpty($"all recordings must be covered by {nameof(Recording_Always_ProducesCorrectBlockHash)} test");
    }
}
