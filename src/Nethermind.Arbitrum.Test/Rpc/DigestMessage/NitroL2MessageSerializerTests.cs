// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Rpc.DigestMessage;

public class NitroL2MessageSerializerTests
{
    [TestCase("./Recordings/1__arbos32_basefee92.jsonl")]
    public void Recordings_Always_SerializableBackAndForth(string recordingFilePath)
    {
        string[] messages = File.ReadAllLines(recordingFilePath);
        EthereumJsonSerializer serializer = new();

        // Read init message
        DigestInitMessage initMessage = serializer.Deserialize<DigestInitMessage>(messages[0]);
        Utf8JsonReader jsonReader = new(initMessage.SerializedChainConfig!);
        ChainConfig chainConfig = serializer.Deserialize<ChainConfig>(ref jsonReader);

        foreach (string message in messages.Skip(1)) // Skip init message
        {
            DigestMessageParameters parameters = serializer.Deserialize<DigestMessageParameters>(message);
            if (parameters.Message.Message.Header.Kind == ArbitrumL1MessageKind.BatchPostingReport)
                continue;

            IReadOnlyList<Transaction> transactions = NitroL2MessageParser.ParseTransactions(parameters.Message.Message, chainConfig.ChainId, 40, new ILogger());
            byte[] serialized = NitroL2MessageSerializer.SerializeTransactions(transactions, parameters.Message.Message.Header);

            parameters.Message.Message.L2Msg.Should().BeEquivalentTo(serialized, o => o.WithStrictOrdering());
        }
    }
}
