// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Sequencer;

public interface IArbitrumSequencerEngine
{
    Task<ResultWrapper<StartSequencingResult>> StartSequencingAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp);
    Task<ResultWrapper<EmptyResponse>> EndSequencingAsync(string? error);
    Task<ResultWrapper<EmptyResponse>> AppendLastSequencedBlockAsync();
    Task<ResultWrapper<SequencedMsg?>> ResequenceReorgedMessageAsync(MessageWithMetadata? msg);
    ResultWrapper<EmptyResponse> Pause();
    ResultWrapper<EmptyResponse> Activate();
    ResultWrapper<EmptyResponse> ForwardTo(string url);
    ResultWrapper<EmptyResponse> EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx);
    ResultWrapper<ulong> NextDelayedMessageNumber();
}

public sealed class DisabledArbitrumSequencerEngine : IArbitrumSequencerEngine
{
    private const string Error = "Sequencer not enabled";

    public Task<ResultWrapper<StartSequencingResult>> StartSequencingAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
        => Task.FromResult(ResultWrapper<StartSequencingResult>.Fail(Error));

    public Task<ResultWrapper<EmptyResponse>> EndSequencingAsync(string? error)
        => ResultWrapper<EmptyResponse>.Fail(Error);

    public Task<ResultWrapper<EmptyResponse>> AppendLastSequencedBlockAsync()
        => Task.FromResult(ResultWrapper<EmptyResponse>.Fail(Error));

    public Task<ResultWrapper<SequencedMsg?>> ResequenceReorgedMessageAsync(MessageWithMetadata? msg)
        => Task.FromResult(ResultWrapper<SequencedMsg?>.Fail(Error));

    public ResultWrapper<EmptyResponse> Pause()
        => ResultWrapper<EmptyResponse>.Fail(Error);

    public ResultWrapper<EmptyResponse> Activate()
        => ResultWrapper<EmptyResponse>.Fail(Error);

    public ResultWrapper<EmptyResponse> ForwardTo(string url)
        => ResultWrapper<EmptyResponse>.Fail(Error);

    public ResultWrapper<EmptyResponse> EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx)
        => ResultWrapper<EmptyResponse>.Fail(Error);

    public ResultWrapper<ulong> NextDelayedMessageNumber()
        => ResultWrapper<ulong>.Fail(Error);
}
