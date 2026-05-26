// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.State;

namespace Nethermind.Arbitrum.Modules;

// Reimplemented to match Nitro's FeeHistory (apibackend.go:238-355):
// uses L2 compute rate vs SpeedLimit instead of block gas ratio, always zero rewards
public class ArbitrumFeeHistoryOracle(
    IBlockTree blockTree,
    IReceiptStorage receiptStorage,
    IStateReader stateReader,
    ArbitrumChainSpecEngineParameters chainSpecParams) : IFeeHistoryOracle
{
    private const int MaxFeeHistory = 1024;

    // Precomputed Keccak-derived storage key for ArbOS L2PricingState.SpeedLimitPerSecond (offset 0)
    private static readonly UInt256 SpeedLimitStorageIndex = ComputeSpeedLimitStorageIndex();

    public ResultWrapper<FeeHistoryResults> GetFeeHistory(int blockCount, BlockParameter newestBlock, double[]? rewardPercentiles)
    {
        if (newestBlock.Type == BlockParameterType.BlockHash)
            return ResultWrapper<FeeHistoryResults>.Fail("newestBlock: Is not correct block number", ErrorCodes.InvalidParams);

        if (blockCount > MaxFeeHistory)
            blockCount = MaxFeeHistory;

        if (blockCount < 1)
            return ResultWrapper<FeeHistoryResults>.Fail($"blockCount: Value {blockCount} is less than 1", ErrorCodes.InvalidParams);

        long genesisBlockNum = (long)(chainSpecParams.GenesisBlockNum ?? 0);

        BlockHeader? newestHeader = blockTree.FindHeader(newestBlock);
        if (newestHeader is null)
            return ResultWrapper<FeeHistoryResults>.Fail("newestBlock: Block is not available", ErrorCodes.ResourceUnavailable);

        long newestBlockNumber = newestHeader.Number;
        long latestBlockNumber = blockTree.Head?.Number ?? 0;

        if (newestBlockNumber > latestBlockNumber)
            newestBlockNumber = latestBlockNumber;

        if (newestBlockNumber < genesisBlockNum)
            newestBlockNumber = genesisBlockNum;

        if (blockCount > newestBlockNumber - genesisBlockNum)
            blockCount = (int)(newestBlockNumber - genesisBlockNum) + 1;

        long oldestBlock = newestBlockNumber + 1 - blockCount;

        ArrayPoolList<ArrayPoolList<UInt256>>? rewards = null;
        if (rewardPercentiles is { Length: > 0 })
        {
            rewards = new ArrayPoolList<ArrayPoolList<UInt256>>(blockCount, blockCount);
            for (int i = 0; i < blockCount; i++)
                rewards[i] = new ArrayPoolList<UInt256>(rewardPercentiles.Length, Enumerable.Repeat(UInt256.Zero, rewardPercentiles.Length));
        }

        Result<ulong> speedLimitResult = ReadSpeedLimitPerSecond(newestHeader);
        if (speedLimitResult.IsError)
            return ResultWrapper<FeeHistoryResults>.Fail($"Failed to read speed limit: {speedLimitResult.Error}", ErrorCodes.InternalError);

        ulong speedLimit = speedLimitResult.Data;

        ArrayPoolList<UInt256> baseFees = new(blockCount + 1, blockCount + 1);
        ArrayPoolList<double> gasUsedRatios = new(blockCount, blockCount);
        ArrayPoolList<UInt256> baseFeePerBlobGas = new(blockCount + 1, blockCount + 1);
        ArrayPoolList<double> blobGasUsedRatios = new(blockCount, blockCount);

        long baseFeeLookup = newestBlockNumber + 1;
        if (newestBlockNumber == latestBlockNumber)
            baseFeeLookup = newestBlockNumber;

        ulong prevTimestamp = 0;
        ulong timeSinceLastTimeChange = 0;
        ulong currentTimestampGasUsed = 0;

        if (oldestBlock > genesisBlockNum)
        {
            BlockHeader? prevHeader = blockTree.FindHeader(oldestBlock - 1);
            if (prevHeader is not null)
                prevTimestamp = prevHeader.Timestamp;
        }

        for (long blockNum = oldestBlock; blockNum <= baseFeeLookup; blockNum++)
        {
            int i = (int)(blockNum - oldestBlock);
            BlockHeader? header = blockTree.FindHeader(blockNum);
            if (header is null)
                break;

            baseFees[i] = header.BaseFeePerGas;

            if (blockNum > newestBlockNumber)
                break;

            if (header.Timestamp > prevTimestamp)
            {
                timeSinceLastTimeChange = header.Timestamp - prevTimestamp;
                currentTimestampGasUsed = 0;
            }

            if (header.Hash is not null)
            {
                TxReceipt[] receipts = receiptStorage.Get(header.Hash);
                foreach (TxReceipt receipt in receipts)
                {
                    ulong gasUsedForL1 = receipt is ArbitrumTxReceipt arbReceipt ? arbReceipt.GasUsedForL1 : 0;
                    ulong gasUsed = (ulong)receipt.GasUsed;
                    if (gasUsed > gasUsedForL1)
                        currentTimestampGasUsed += gasUsed - gasUsedForL1;
                }
            }

            prevTimestamp = header.Timestamp;

            double fullnessAnalogue = (timeSinceLastTimeChange > 0 && speedLimit > 0)
                ? System.Math.Min((double)currentTimestampGasUsed / speedLimit / timeSinceLastTimeChange / 2.0, 1.0)
                : 1.0;

            gasUsedRatios[i] = fullnessAnalogue;
        }

        if (newestBlockNumber == latestBlockNumber)
        {
            baseFees[blockCount] = baseFees[blockCount - 1];
        }

        return ResultWrapper<FeeHistoryResults>.Success(
            new FeeHistoryResults(oldestBlock, baseFees, gasUsedRatios, baseFeePerBlobGas, blobGasUsedRatios, rewards));
    }

    private static UInt256 ComputeSpeedLimitStorageIndex()
    {
        // Mirrors ArbosStorage key derivation: OpenSubStorage([1]) then MapAddress(offset=0)
        byte[] subspaceKey = Keccak.Compute(ArbosSubspaceIDs.L2PricingSubspace).BytesToArray();

        // MapAddress input: subspaceKey(32) ++ logicalKey[0..31] (31 zero bytes for offset 0)
        byte[] keccakInput = new byte[subspaceKey.Length + 31];
        subspaceKey.CopyTo(keccakInput, 0);

        byte[] hash = Keccak.Compute(keccakInput).BytesToArray();

        // Final key: hash[0..31] ++ logicalKey[31] (0x00)
        byte[] mappedKey = new byte[32];
        Array.Copy(hash, mappedKey, 31);
        return new UInt256(mappedKey, isBigEndian: true);
    }

    private Result<ulong> ReadSpeedLimitPerSecond(BlockHeader header)
    {
        ReadOnlySpan<byte> value = stateReader.GetStorage(header, ArbosAddresses.ArbosSystemAccount, SpeedLimitStorageIndex);
        return !value.IsEmpty
            ? Result<ulong>.Success((ulong)new UInt256(value, isBigEndian: true))
            : Result<ulong>.Fail("ArbOS is not initialized.");
    }
}
