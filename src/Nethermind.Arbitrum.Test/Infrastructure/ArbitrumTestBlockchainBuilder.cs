// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.JsonRpc;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class ArbitrumTestBlockchainBuilder
{
    private readonly List<Action<ArbitrumRpcTestBlockchain>> _configurations = new();
    private ChainSpec _chainSpec = FullChainSimulationChainSpecProvider.Create();
    private Action<ArbitrumConfig>? _configureArbitrum;

    public ArbitrumTestBlockchainBuilder WithChainSpec(ChainSpec chainSpec)
    {
        _chainSpec = chainSpec;
        return this;
    }

    public ArbitrumTestBlockchainBuilder WithArbitrumConfig(Action<ArbitrumConfig> configure)
    {
        _configureArbitrum = configure;
        return this;
    }

    public ArbitrumTestBlockchainBuilder WithGenesisBlock(ulong initialBaseFee = 92, ulong arbosVersion = 32)
    {
        DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(initialBaseFee, arbosVersion);
        return WithGenesisBlock(digestInitMessage);
    }

    public ArbitrumTestBlockchainBuilder WithGenesisBlock(DigestInitMessage message)
    {
        if (_configurations.Count != 0)
            throw new InvalidOperationException("Genesis block must be configured before any other configurations");

        _configurations.Add(chain =>
        {
            ThrowOnFailure(chain.ArbitrumRpcModule.DigestInitMessage(message), 0);
        });

        return this;
    }

    public ArbitrumTestBlockchainBuilder WithRecording<T>(ushort numberToDigest = ushort.MaxValue)
        where T : IFullChainSimulationRecording, new()
    {
        return WithRecording(new T(), numberToDigest);
    }

    public ArbitrumTestBlockchainBuilder WithRecording(IFullChainSimulationRecording recording, ushort numberToDigest = ushort.MaxValue)
    {
        if (recording.HasDigestInitMessage)
        {
            if (_configurations.Count != 0)
                throw new InvalidOperationException("Genesis block configuration is being used along the digest init message, only one of them can be used at a time");

            DigestInitMessage digestInitMessage = recording.GetDigestInitMessage();
            WithGenesisBlock(digestInitMessage);
        }

        _configurations.Add(chain =>
        {
            foreach (DigestMessageParameters digestMessage in recording.GetDigestMessages().Take(numberToDigest))
                ThrowOnFailure(chain.ArbitrumRpcModule.DigestMessage(digestMessage).GetAwaiter().GetResult(), digestMessage.Index);
        });

        return this;
    }

    public ArbitrumRpcTestBlockchain Build()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(chainSpec: _chainSpec, configureArbitrum: _configureArbitrum);

        foreach (Action<ArbitrumRpcTestBlockchain> configuration in _configurations)
            configuration(chain);

        return chain;
    }

    private static void ThrowOnFailure<T>(ResultWrapper<T> result, ulong number)
    {
        if (result.Result != Result.Success)
            throw new InvalidOperationException($"Failed to execute RPC method, number {number}, code {result.ErrorCode}: {result.Result.Error}");
    }
}

public interface IFullChainSimulationRecording
{
    bool HasDigestInitMessage { get; }

    DigestInitMessage GetDigestInitMessage();
    IEnumerable<DigestMessageParameters> GetDigestMessages();
}

public class FullChainSimulationRecordingFile : IFullChainSimulationRecording
{
    private readonly DigestInitMessage _digestInitMessage;
    private readonly List<DigestMessageParameters> _digestMessages = new();
    private readonly EthereumJsonSerializer _serializer = new();

    public FullChainSimulationRecordingFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File {filePath} not found.");

        string[] messages = File.ReadAllLines(filePath);
        if (messages.Length == 0)
            throw new InvalidOperationException($"File {filePath} is empty.");

        _digestInitMessage = _serializer.Deserialize<DigestInitMessage>(messages[0]);
        _digestMessages = messages.Skip(1).Select(m => _serializer.Deserialize<DigestMessageParameters>(m)).ToList();
    }

    public bool HasDigestInitMessage => true;

    public DigestInitMessage GetDigestInitMessage() => _digestInitMessage;

    public IEnumerable<DigestMessageParameters> GetDigestMessages() => _digestMessages;
}
