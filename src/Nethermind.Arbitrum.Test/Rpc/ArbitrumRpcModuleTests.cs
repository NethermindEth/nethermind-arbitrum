using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Rpc;

public class ArbitrumRpcModuleTests
{
    [Test]
    public void DigestInitMessage_IsNotInitialized_ProducesGenesisBlock()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        DigestInitMessage initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);

        ResultWrapper<MessageResult> result = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

        result.Data.Should().BeEquivalentTo(new MessageResult
        {
            BlockHash = new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"),
            SendRoot = Hash256.Zero
        });
    }

    [Test]
    public void DigestInitMessage_AlreadyInitialized_Throws()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        DigestInitMessage initMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);

        // Produce genesis block
        _ = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

        // Call again to ensure it throws
        chain.Invoking(c => c.ArbitrumRpcModule.DigestInitMessage(initMessage))
            .Should()
            .Throw<InvalidOperationException>();
    }

    [Test]
    public void DigestInitMessage_InvalidInitialL1BaseFee_ReturnsInvalidParamsError()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        DigestInitMessage initMessage = new(UInt256.Zero, FullChainSimulationInitMessage.SerializedChainConfig);

        ResultWrapper<MessageResult> result = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidParams);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("?")]
    public void DigestInitMessage_InvalidSerializedChainConfig_ReturnsInvalidParamsError(string? invalidSerializedChainConfig)
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        DigestInitMessage initMessage = new(UInt256.One, invalidSerializedChainConfig);

        ResultWrapper<MessageResult> result = chain.ArbitrumRpcModule.DigestInitMessage(initMessage);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.ErrorCode.Should().Be(ErrorCodes.InvalidParams);
    }
}
