// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public interface IExpressLaneService
{
    Task SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber);
}

public class DisabledExpressLaneService : IExpressLaneService
{
    public Task SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber)
    {
        return Task.CompletedTask;
    }
}
