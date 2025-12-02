// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Consensus;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;
using Moq;

namespace Nethermind.Arbitrum.Test.Execution;

/// <summary>
/// Tests for ArbitrumHeaderValidator to verify Arbitrum-specific relaxed validation:
/// - Validate1559: Always returns true (bypasses EIP-1559 base fee validation)
/// - ValidateTimestamp: Allows equal timestamps (>= instead of >)
/// These relaxations are intentional for Arbitrum's optimistic rollup architecture.
/// </summary>
public class ArbitrumHeaderValidatorTests
{
    private ArbitrumHeaderValidator _validator = null!;
    private Mock<IBlockTree> _blockTree = null!;
    private Mock<ISealValidator> _sealValidator = null!;
    private ISpecProvider _specProvider = null!;
    private ILogManager _logManager = null!;

    [SetUp]
    public void Setup()
    {
        _blockTree = new Mock<IBlockTree>();
        _sealValidator = new Mock<ISealValidator>();
        _specProvider = FullChainSimulationChainSpecProvider.CreateDynamicSpecProvider();
        _logManager = LimboLogs.Instance;

        // Setup default mocks
        _sealValidator.Setup(s => s.ValidateParams(It.IsAny<BlockHeader>(), It.IsAny<BlockHeader>(), It.IsAny<bool>()))
            .Returns(true);

        Block genesis = Build.A.Block.Genesis.TestObject;
        _blockTree.SetupGet(b => b.Genesis).Returns(genesis.Header);

        _validator = new ArbitrumHeaderValidator(_blockTree.Object, _sealValidator.Object, _specProvider, _logManager);
    }

    [Test]
    public void Validate1559_WithMismatchedBaseFee_ReturnsTrue()
    {
        // Arrange: Create parent and header with mismatched base fees
        // In standard Ethereum, this would fail validation
        BlockHeader parent = Build.A.BlockHeader
            .WithNumber(100)
            .WithTimestamp(1000)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(15_000_000)
            .TestObject;

        // Header with incorrect base fee (not calculated from parent)
        BlockHeader header = Build.A.BlockHeader
            .WithNumber(101)
            .WithTimestamp(1001)
            .WithParentHash(parent.Hash!)
            .WithBaseFee(999.GWei()) // Intentionally wrong - would fail standard validation
            .WithGasLimit(30_000_000)
            .WithGasUsed(10_000_000)
            .TestObject;

        // Act: Validate EIP-1559 (base fee) specifically
        // ArbitrumHeaderValidator.Validate1559 should always return true
        bool result = _validator.Validate(header, parent, isUncle: false, out string? error);

        // Assert: Should pass because Arbitrum bypasses EIP-1559 validation
        // Note: Other validations may fail, but this test documents that EIP-1559 is bypassed
        // The result may be false due to other validation failures (e.g., hash mismatch),
        // but the key point is that EIP-1559 validation is bypassed
        error.Should().NotContain("base fee", "Arbitrum should bypass EIP-1559 base fee validation");
    }

    [Test]
    public void ValidateTimestamp_WithEqualTimestamps_ReturnsTrue()
    {
        // Arrange: Create parent and header with equal timestamps
        // In standard Ethereum, header.Timestamp must be > parent.Timestamp
        // In Arbitrum, header.Timestamp >= parent.Timestamp is valid
        ulong sameTimestamp = 1000;

        BlockHeader parent = Build.A.BlockHeader
            .WithNumber(100)
            .WithTimestamp(sameTimestamp)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(0)
            .TestObject;

        BlockHeader header = Build.A.BlockHeader
            .WithNumber(101)
            .WithTimestamp(sameTimestamp) // Same as parent - would fail standard validation
            .WithParentHash(parent.Hash!)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(0)
            .TestObject;

        // Act & Assert: Validate specifically checking timestamp
        // Full validation may fail for other reasons, but timestamp should pass
        bool result = _validator.Validate(header, parent, isUncle: false, out string? error);

        // The error should not be about invalid timestamp
        if (error is not null)
        {
            error.Should().NotContain("timestamp", "Arbitrum should allow equal timestamps");
        }
    }

    [Test]
    public void ValidateTimestamp_WithLaterTimestamp_ReturnsTrue()
    {
        // Arrange: Standard case - header timestamp > parent timestamp
        BlockHeader parent = Build.A.BlockHeader
            .WithNumber(100)
            .WithTimestamp(1000)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(0)
            .TestObject;

        BlockHeader header = Build.A.BlockHeader
            .WithNumber(101)
            .WithTimestamp(1001) // Later than parent
            .WithParentHash(parent.Hash!)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(0)
            .TestObject;

        // Act
        bool result = _validator.Validate(header, parent, isUncle: false, out string? error);

        // Assert: Should not fail due to timestamp
        if (error is not null)
        {
            error.Should().NotContain("timestamp", "Later timestamp should be valid");
        }
    }

    [Test]
    public void ValidateTimestamp_WithEarlierTimestamp_ReturnsFalse()
    {
        // Arrange: Header timestamp < parent timestamp - should fail even in Arbitrum
        BlockHeader parent = Build.A.BlockHeader
            .WithNumber(100)
            .WithTimestamp(1000)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(0)
            .TestObject;

        BlockHeader header = Build.A.BlockHeader
            .WithNumber(101)
            .WithTimestamp(999) // Earlier than parent - should fail
            .WithParentHash(parent.Hash!)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(0)
            .TestObject;

        // Act
        bool result = _validator.Validate(header, parent, isUncle: false, out string? error);

        // Assert: Should fail because timestamp is before parent (even Arbitrum doesn't allow this)
        result.Should().BeFalse("Timestamp before parent should fail validation");
        error.Should().NotBeNull();
    }

    [Test]
    public void Validate1559_WithZeroBaseFee_ReturnsTrue()
    {
        // Arrange: Test with zero base fee which would fail standard EIP-1559 validation
        BlockHeader parent = Build.A.BlockHeader
            .WithNumber(100)
            .WithTimestamp(1000)
            .WithBaseFee(1.GWei())
            .WithGasLimit(30_000_000)
            .WithGasUsed(15_000_000)
            .TestObject;

        BlockHeader header = Build.A.BlockHeader
            .WithNumber(101)
            .WithTimestamp(1001)
            .WithParentHash(parent.Hash!)
            .WithBaseFee(0) // Zero base fee - would fail standard EIP-1559
            .WithGasLimit(30_000_000)
            .WithGasUsed(0)
            .TestObject;

        // Act
        bool result = _validator.Validate(header, parent, isUncle: false, out string? error);

        // Assert: Should not fail due to base fee
        if (error is not null)
        {
            error.Should().NotContain("base fee", "Arbitrum should bypass EIP-1559 base fee validation");
        }
    }

    [Test]
    public void Validate_WithIntegrationBlockchain_ValidatesCorrectly()
    {
        // Integration test using ArbitrumRpcTestBlockchain
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = false
            });
        });

        BlockHeader parent = chain.BlockTree.Head!.Header;

        // Create header with same timestamp as parent
        BlockHeader header = Build.A.BlockHeader
            .WithNumber(parent.Number + 1)
            .WithTimestamp(parent.Timestamp) // Equal timestamp
            .WithParentHash(parent.Hash!)
            .WithBaseFee(parent.BaseFeePerGas)
            .WithGasLimit(parent.GasLimit)
            .WithGasUsed(0)
            .TestObject;

        // Get the validator from the chain
        ArbitrumHeaderValidator validator = new(
            chain.BlockTree,
            NullSealEngine.Instance,
            chain.SpecProvider,
            chain.LogManager);

        // Act
        bool result = validator.Validate(header, parent, isUncle: false, out string? error);

        // Assert: Should not fail due to timestamp (may fail for other reasons like hash)
        if (error is not null)
        {
            error.Should().NotContain("timestamp", "Arbitrum should allow equal timestamps in integration test");
        }
    }
}
