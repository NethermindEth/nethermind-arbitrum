// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

/// <summary>
/// Helper class for encoding calldata and decoding responses for the evm-data Stylus contract.
/// The evm-data contract returns various EVM environment data.
/// See nitro/arbitrator/stylus/tests/evm-data/src/main.rs for reference.
/// </summary>
public static class EvmDataCallData
{
    private const int AddressSize = 20;
    private const int Hash256Size = 32;
    private const int UInt64Size = 8;
    private const int UInt32Size = 4;

    /// <summary>
    /// Creates calldata for the evm-data contract.
    /// Format: [balance_check_addr:20][eth_precompile_addr:20][arb_test_addr:20][contract_addr:20][burn_call_data:N]
    /// </summary>
    /// <param name="balanceCheckAddress">Address to check balance for</param>
    /// <param name="ethPrecompileAddress">Ethereum precompile address (e.g., 0x01 for ecrecover)</param>
    /// <param name="arbTestAddress">Arbitrum precompile address (0x69 for ArbTest)</param>
    /// <param name="contractAddress">Contract to get code/codehash for</param>
    /// <param name="burnCallData">Optional calldata for ArbTest.burnArbGas call</param>
    public static byte[] CreateCallData(
        Address balanceCheckAddress,
        Address ethPrecompileAddress,
        Address arbTestAddress,
        Address contractAddress,
        byte[]? burnCallData = null)
    {
        burnCallData ??= [];

        int totalSize = AddressSize * 4 + burnCallData.Length;
        byte[] result = new byte[totalSize];
        int offset = 0;

        balanceCheckAddress.Bytes.CopyTo(result.AsSpan(offset, AddressSize));
        offset += AddressSize;

        ethPrecompileAddress.Bytes.CopyTo(result.AsSpan(offset, AddressSize));
        offset += AddressSize;

        arbTestAddress.Bytes.CopyTo(result.AsSpan(offset, AddressSize));
        offset += AddressSize;

        contractAddress.Bytes.CopyTo(result.AsSpan(offset, AddressSize));
        offset += AddressSize;

        if (burnCallData.Length > 0)
            burnCallData.CopyTo(result.AsSpan(offset));

        return result;
    }

    /// <summary>
    /// Creates calldata for ArbTest.burnArbGas(uint256 gasAmount).
    /// Uses the same method ID calculation as ArbitrumPrecompiles.
    /// </summary>
    public static byte[] CreateBurnArbGasCallData(UInt256 gasAmount)
    {
        byte[] callData = new byte[36];
        uint methodId = PrecompileHelper.GetMethodId("burnArbGas(uint256)");
        BinaryPrimitives.WriteUInt32BigEndian(callData.AsSpan(0, 4), methodId);
        gasAmount.ToBigEndian(callData.AsSpan(4, 32));
        return callData;
    }

    /// <summary>
    /// Parses the response from the evm-data contract.
    /// </summary>
    /// <param name="response">Raw response bytes from the contract</param>
    /// <param name="expectedCodeLength">Expected length of contract code (needed to parse variable-length code)</param>
    public static EvmDataResponse ParseResponse(ReadOnlySpan<byte> response, int expectedCodeLength)
    {
        int offset = 0;

        UInt256 blockNumberMinusOne = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        UInt256 chainId = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        UInt256 baseFee = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        UInt256 gasPrice = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        UInt256 gasLimit = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        UInt256 value = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        UInt256 timestamp = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        UInt256 addressBalance = new(response.Slice(offset, Hash256Size), isBigEndian: true);
        offset += Hash256Size;

        Address contractAddress = new(response.Slice(offset + 12, AddressSize).ToArray());
        offset += Hash256Size;

        Address sender = new(response.Slice(offset + 12, AddressSize).ToArray());
        offset += Hash256Size;

        Address origin = new(response.Slice(offset + 12, AddressSize).ToArray());
        offset += Hash256Size;

        Address coinbase = new(response.Slice(offset + 12, AddressSize).ToArray());
        offset += Hash256Size;

        Hash256 contractCodeHash = new(response.Slice(offset, Hash256Size));
        offset += Hash256Size;

        Hash256 arbPrecompileCodeHash = new(response.Slice(offset, Hash256Size));
        offset += Hash256Size;

        Hash256 ethPrecompileCodeHash = new(response.Slice(offset, Hash256Size));
        offset += Hash256Size;

        byte[] contractCode = response.Slice(offset, expectedCodeLength).ToArray();
        offset += expectedCodeLength;

        uint inkPrice = BinaryPrimitives.ReadUInt32BigEndian(response.Slice(offset, UInt32Size));
        offset += UInt32Size;

        ulong gasLeftBefore = BinaryPrimitives.ReadUInt64BigEndian(response.Slice(offset, UInt64Size));
        offset += UInt64Size;

        ulong inkLeftBefore = BinaryPrimitives.ReadUInt64BigEndian(response.Slice(offset, UInt64Size));
        offset += UInt64Size;

        ulong gasLeftAfter = BinaryPrimitives.ReadUInt64BigEndian(response.Slice(offset, UInt64Size));
        offset += UInt64Size;

        ulong inkLeftAfter = BinaryPrimitives.ReadUInt64BigEndian(response.Slice(offset, UInt64Size));

        return new EvmDataResponse
        {
            BlockNumberMinusOne = blockNumberMinusOne,
            ChainId = chainId,
            BaseFee = baseFee,
            GasPrice = gasPrice,
            GasLimit = gasLimit,
            Value = value,
            Timestamp = timestamp,
            AddressBalance = addressBalance,
            ContractAddress = contractAddress,
            Sender = sender,
            Origin = origin,
            Coinbase = coinbase,
            ContractCodeHash = contractCodeHash,
            ArbPrecompileCodeHash = arbPrecompileCodeHash,
            EthPrecompileCodeHash = ethPrecompileCodeHash,
            ContractCode = contractCode,
            InkPrice = inkPrice,
            GasLeftBefore = gasLeftBefore,
            InkLeftBefore = inkLeftBefore,
            GasLeftAfter = gasLeftAfter,
            InkLeftAfter = inkLeftAfter
        };
    }
}

public readonly record struct EvmDataResponse
{
    public required UInt256 BlockNumberMinusOne { get; init; }
    public required UInt256 ChainId { get; init; }
    public required UInt256 BaseFee { get; init; }
    public required UInt256 GasPrice { get; init; }
    public required UInt256 GasLimit { get; init; }
    public required UInt256 Value { get; init; }
    public required UInt256 Timestamp { get; init; }
    public required UInt256 AddressBalance { get; init; }

    public required Address ContractAddress { get; init; }
    public required Address Sender { get; init; }
    public required Address Origin { get; init; }
    public required Address Coinbase { get; init; }

    public required Hash256 ContractCodeHash { get; init; }
    public required Hash256 ArbPrecompileCodeHash { get; init; }
    public required Hash256 EthPrecompileCodeHash { get; init; }

    public required byte[] ContractCode { get; init; }

    public required uint InkPrice { get; init; }
    public required ulong GasLeftBefore { get; init; }
    public required ulong InkLeftBefore { get; init; }
    public required ulong GasLeftAfter { get; init; }
    public required ulong InkLeftAfter { get; init; }
}
