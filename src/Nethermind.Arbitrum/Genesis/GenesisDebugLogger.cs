// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Debug logger for genesis state initialization. Logs state hashes at each step
/// for comparison with Nitro implementation.
/// </summary>
public class GenesisDebugLogger(IWorldState worldState, IReleaseSpec spec) : IDisposable
{
    private const string LogPath = "/tmp/nethermind-genesis.log";
    private readonly StreamWriter? _writer = CreateWriter();

    private static StreamWriter? CreateWriter()
    {
        try { return new StreamWriter(LogPath, append: false); }
        catch { return null; }
    }

    private string GetStateRoot()
    {
        try
        {
            worldState.Commit(spec, isGenesis: true);
            worldState.RecalculateStateRoot();
            return worldState.StateRoot.ToString();
        }
        catch
        {
            return "N/A";
        }
    }

    public void LogStep(string stepName)
    {
        if (_writer == null) return;
        _writer.WriteLine($"STEP: {stepName}");
        _writer.WriteLine($"STATE_HASH: {GetStateRoot()}");
        _writer.WriteLine();
        _writer.Flush();
    }

    public void LogStepWithValue(string stepName, string key, object value)
    {
        if (_writer == null) return;
        _writer.WriteLine($"STEP: {stepName}");
        _writer.WriteLine($"{key}: {value}");
        _writer.WriteLine($"STATE_HASH: {GetStateRoot()}");
        _writer.WriteLine();
        _writer.Flush();
    }

    public void Dispose() => _writer?.Dispose();
}
