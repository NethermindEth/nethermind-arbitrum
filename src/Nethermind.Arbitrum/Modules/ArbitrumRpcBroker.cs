using System.Threading.Channels;
using Nethermind.Arbitrum.Execution.Transactions;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumRpcBroker : IDisposable
{
    private readonly Channel<MessageEnvelope> _channel = Channel.CreateUnbounded<MessageEnvelope>(new()
    {
        SingleWriter = true,
        SingleReader = true
    });
    private bool _disposed;

    public async Task<IArbitrumProcessingResult> SendAsync(IArbitrumTransactionData[] transactions, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        TaskCompletionSource<IArbitrumProcessingResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MessageEnvelope envelope = new(transactions, tcs);

        await _channel.Writer.WriteAsync(envelope, cancellationToken);

        await using (cancellationToken.Register(() => envelope.Result.TrySetCanceled()))
        {
            return await envelope.Result.Task;
        }
    }

    public async Task<MessageContext> WaitForMessageAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var envelope = await _channel.Reader.ReadAsync(cancellationToken);
        return new MessageContext(envelope);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _channel.Writer.TryComplete();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ArbitrumRpcBroker));
        }
    }
}

public interface IArbitrumProcessingResult;

public record ArbitrumNoopResult : IArbitrumProcessingResult;

public record MessageEnvelope(IArbitrumTransactionData[] Transactions, TaskCompletionSource<IArbitrumProcessingResult> Result);

public class MessageContext : IDisposable
{
    private readonly MessageEnvelope _envelope;
    private bool _responded;

    internal MessageContext(MessageEnvelope envelope)
    {
        _envelope = envelope;
    }

    public IReadOnlyList<IArbitrumTransactionData> Request => _envelope.Transactions;

    public void RespondAsync(IArbitrumProcessingResult result)
    {
        if (_responded)
        {
            throw new InvalidOperationException("Response already sent");
        }

        _responded = true;
        _envelope.Result.TrySetResult(result);
    }

    public void RespondWithError(Exception exception)
    {
        if (_responded)
        {
            throw new InvalidOperationException("Response already sent");
        }

        _responded = true;
        _envelope.Result.TrySetException(exception);
    }

    public void Dispose()
    {
        if (!_responded)
        {
            _envelope.Result.TrySetResult(new ArbitrumNoopResult());
        }
    }
}
