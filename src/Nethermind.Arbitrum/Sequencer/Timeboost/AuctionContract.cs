// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Facade;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public sealed class AuctionContract(
    IBlockTree blockTree,
    IBlockchainBridgeFactory bridgeFactory,
    IArbitrumConfig arbitrumConfig,
    ILogManager logManager) : IAuctionContract
{
    // resolvedRounds() function selector = keccak256("resolvedRounds()")[:4] = 0x0d253fbe
    private static readonly byte[] ResolvedRoundsSelector = [0x0d, 0x25, 0x3f, 0xbe];

    private readonly IBlockchainBridge _bridge = bridgeFactory.CreateBlockchainBridge();
    private readonly ILogger _logger = logManager.GetClassLogger<AuctionContract>();

    public Address Address { get; } = new(arbitrumConfig.TimeboostAuctionContractAddress);

    public ResolvedRound ResolveRounds()
    {
        try
        {
            BlockHeader? head = blockTree.Head?.Header;
            if (head is null)
                return ResolvedRound.Empty;

            Transaction callTx = new()
            {
                To = Address,
                Data = ResolvedRoundsSelector,
                GasLimit = 200_000,
                SenderAddress = Address.Zero,
            };

            CallOutput result = _bridge.Call(head, callTx);
            if (result.Error is not null || result.ExecutionReverted)
                return ResolvedRound.Empty;

            return ParseOutput(result.OutputData);
        }
        catch (Exception ex)
        {
            if (_logger.IsDebug)
                _logger.Warn($"AuctionContract: ResolveRounds() failed: {ex.Message}");

            return ResolvedRound.Empty;
        }
    }

    private static ResolvedRound ParseOutput(ReadOnlySpan<byte> output)
    {
        if (output.Length < 128)
            return ResolvedRound.Empty;

        Address controller = new(output.Slice(12, 20));
        ulong round = BinaryPrimitives.ReadUInt64BigEndian(output.Slice(56, 8));

        if (controller == Address.Zero)
            return ResolvedRound.Empty;

        return new ResolvedRound(controller, round);
    }
}
