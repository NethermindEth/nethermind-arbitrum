// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Sequencer.Timeboost;

public interface IExpressLaneService
{
    Task<ResultWrapper<EmptyResponse>> SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber);
}

public class DisabledExpressLaneService : IExpressLaneService
{
    public Task<ResultWrapper<EmptyResponse>> SequenceAsync(ExpressLaneSubmission submission, ulong currentBlockNumber) =>
        Task.FromResult(ResultWrapper.EmptySuccess);
}
