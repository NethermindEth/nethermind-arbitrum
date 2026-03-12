// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Sequencer;

public interface IArbitrumSequencerEngine
{
    Task<ResultWrapper<StartSequencingResult>> StartSequencingAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp);
    ResultWrapper<EmptyResponse> EndSequencing(string? error);
    Task<ResultWrapper<EmptyResponse>> AppendLastSequencedBlockAsync();
    Task<ResultWrapper<SequencedMsg?>> ResequenceReorgedMessageAsync(MessageWithMetadata? msg);
    ResultWrapper<EmptyResponse> Pause();
    ResultWrapper<EmptyResponse> Activate();
    ResultWrapper<EmptyResponse> ForwardTo(string url);
    ResultWrapper<EmptyResponse> EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx);
    ResultWrapper<ulong> NextDelayedMessageNumber();
}
