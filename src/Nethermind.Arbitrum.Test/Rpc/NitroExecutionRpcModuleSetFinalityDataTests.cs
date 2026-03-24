// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Modules;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Test;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Rpc;

/// <summary>
/// Tests for nitroexecution_setFinalityData that go through the JSON-RPC service layer.
/// These tests verify proper handling of explicit null parameters in JSON-RPC requests.
///
/// IMPORTANT: RpcFinalityData must be a struct (not a class) for correct null handling.
/// These tests provide two levels of protection against accidentally changing struct to class:
/// 1. COMPILE-TIME: The last test uses .Value on Nullable&lt;RpcFinalityData&gt; which only works for structs
/// 2. RUNTIME: The first three tests would fail with TargetParameterCountException if changed to class
/// </summary>
[TestFixture]
public sealed class NitroExecutionRpcModuleSetFinalityDataTests
{
    private const string ExpectedSuccessResponse = """{"jsonrpc":"2.0","result":{},"id":67}""";

    /// <summary>
    /// Tests that nitroexecution_setFinalityData correctly handles explicit null values
    /// when passed through JSON-RPC deserialization.
    ///
    /// This test will FAIL if RpcFinalityData is changed from struct to class because:
    /// - For structs, Nullable&lt;T&gt; is reliably detected as nullable by JSON-RPC binder
    /// - For classes, nullable reference types rely on NullableAttribute which isn't reliably detected
    /// - When not detected as nullable, explicit null values cause TargetParameterCountException
    /// </summary>
    [Test]
    public async Task SetFinalityData_ExplicitNullForThirdParameter_ReturnsSuccess()
    {
        IArbitrumExecutionEngine engine = Substitute.For<IArbitrumExecutionEngine>();
        engine.SetFinalityData(Arg.Any<SetFinalityDataParams>())
            .Returns(ResultWrapper<EmptyResponse>.Success(default));

        INitroExecutionRpcModule module = new NitroExecutionRpcModule(engine);

        RpcFinalityData safeData = new() { MsgIdx = 100, BlockHash = TestItem.KeccakA };
        RpcFinalityData finalizedData = new() { MsgIdx = 100, BlockHash = TestItem.KeccakA };

        string response = await RpcTest.TestSerializedRequest(
            module,
            "nitroexecution_setFinalityData",
            safeData,
            finalizedData,
            null);

        Assert.That(
            JsonElement.DeepEquals(
                JsonDocument.Parse(response).RootElement,
                JsonDocument.Parse(ExpectedSuccessResponse).RootElement),
            response);
    }

    /// <summary>
    /// Tests that nitroexecution_setFinalityData correctly handles all parameters being null.
    /// </summary>
    [Test]
    public async Task SetFinalityData_AllParametersExplicitlyNull_ReturnsSuccess()
    {
        IArbitrumExecutionEngine engine = Substitute.For<IArbitrumExecutionEngine>();
        engine.SetFinalityData(Arg.Any<SetFinalityDataParams>())
            .Returns(ResultWrapper<EmptyResponse>.Success(default));

        INitroExecutionRpcModule module = new NitroExecutionRpcModule(engine);

        string response = await RpcTest.TestSerializedRequest(
            module,
            "nitroexecution_setFinalityData",
            null,
            null,
            null);

        Assert.That(
            JsonElement.DeepEquals(
                JsonDocument.Parse(response).RootElement,
                JsonDocument.Parse(ExpectedSuccessResponse).RootElement),
            response);
    }

    /// <summary>
    /// Tests that nitroexecution_setFinalityData correctly handles mixed null and non-null parameters.
    /// </summary>
    [Test]
    public async Task SetFinalityData_MixedNullAndNonNullParameters_ReturnsSuccess()
    {
        IArbitrumExecutionEngine engine = Substitute.For<IArbitrumExecutionEngine>();
        engine.SetFinalityData(Arg.Any<SetFinalityDataParams>())
            .Returns(ResultWrapper<EmptyResponse>.Success(default));

        INitroExecutionRpcModule module = new NitroExecutionRpcModule(engine);

        RpcFinalityData finalizedData = new() { MsgIdx = 50, BlockHash = TestItem.KeccakB };

        string response = await RpcTest.TestSerializedRequest(
            module,
            "nitroexecution_setFinalityData",
            null,
            finalizedData,
            null);

        Assert.That(
            JsonElement.DeepEquals(
                JsonDocument.Parse(response).RootElement,
                JsonDocument.Parse(ExpectedSuccessResponse).RootElement),
            response);
    }

    /// <summary>
    /// Tests that the parameters are correctly passed to the engine when using non-null values.
    /// This test also provides compile-time protection: .Value only works on Nullable&lt;T&gt; (struct).
    /// </summary>
    [Test]
    public async Task SetFinalityData_AllParametersProvided_PassesCorrectValuesToEngine()
    {
        SetFinalityDataParams? capturedParams = null;
        IArbitrumExecutionEngine engine = Substitute.For<IArbitrumExecutionEngine>();
        engine.SetFinalityData(Arg.Do<SetFinalityDataParams>(p => capturedParams = p))
            .Returns(ResultWrapper<EmptyResponse>.Success(default));

        INitroExecutionRpcModule module = new NitroExecutionRpcModule(engine);

        Hash256 safeHash = TestItem.KeccakA;
        Hash256 finalizedHash = TestItem.KeccakB;
        Hash256 validatedHash = TestItem.KeccakC;

        RpcFinalityData safeData = new() { MsgIdx = 10, BlockHash = safeHash };
        RpcFinalityData finalizedData = new() { MsgIdx = 8, BlockHash = finalizedHash };
        RpcFinalityData validatedData = new() { MsgIdx = 5, BlockHash = validatedHash };

        string response = await RpcTest.TestSerializedRequest(
            module,
            "nitroexecution_setFinalityData",
            safeData,
            finalizedData,
            validatedData);

        Assert.That(
            JsonElement.DeepEquals(
                JsonDocument.Parse(response).RootElement,
                JsonDocument.Parse(ExpectedSuccessResponse).RootElement),
            response);

        capturedParams.Should().NotBeNull();
        capturedParams!.SafeFinalityData.Should().NotBeNull();
        capturedParams.SafeFinalityData!.Value.MsgIdx.Should().Be(10UL);
        capturedParams.SafeFinalityData!.Value.BlockHash.Should().Be(safeHash);

        capturedParams.FinalizedFinalityData.Should().NotBeNull();
        capturedParams.FinalizedFinalityData!.Value.MsgIdx.Should().Be(8UL);
        capturedParams.FinalizedFinalityData!.Value.BlockHash.Should().Be(finalizedHash);

        capturedParams.ValidatedFinalityData.Should().NotBeNull();
        capturedParams.ValidatedFinalityData!.Value.MsgIdx.Should().Be(5UL);
        capturedParams.ValidatedFinalityData!.Value.BlockHash.Should().Be(validatedHash);
    }
}
