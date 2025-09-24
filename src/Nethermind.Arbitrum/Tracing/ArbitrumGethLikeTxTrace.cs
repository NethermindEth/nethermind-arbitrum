// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json.Serialization;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumGethLikeTxTrace : IDisposable
{
    private readonly IDisposable? _disposable;

    public ArbitrumGethLikeTxTrace(
        GethLikeTxTrace trace,
        List<ArbitrumTransfer> beforeEvmTransfers,
        List<ArbitrumTransfer> afterEvmTransfers,
        IDisposable? disposable = null)
    {
        Gas = trace.Gas;
        Failed = trace.Failed;
        ReturnValue = trace.ReturnValue;
        TxHash = trace.TxHash;
        foreach (Dictionary<string, string> newDict in trace.StoragesByDepth.Select(dict => new Dictionary<string, string>(dict)))
        {
            StoragesByDepth.Push(newDict);
        }

        Entries = new List<GethTxTraceEntry>(trace.Entries);
        BeforeEvmTransfers = beforeEvmTransfers;
        AfterEvmTransfers = afterEvmTransfers;
        _disposable = disposable;
    }

    public ArbitrumGethLikeTxTrace() { }

    public List<ArbitrumTransfer> BeforeEvmTransfers { get; set; }

    public List<ArbitrumTransfer> AfterEvmTransfers { get; set; }

    public Stack<Dictionary<string, string>> StoragesByDepth { get; } = new();

    public long Gas { get; set; }

    public bool Failed { get; set; }

    public byte[] ReturnValue { get; set; } = [];

    public Hash256? TxHash { get; set; }

    public List<GethTxTraceEntry> Entries { get; set; } = new();

    public void Dispose()
    {
        _disposable?.Dispose();
    }
}
