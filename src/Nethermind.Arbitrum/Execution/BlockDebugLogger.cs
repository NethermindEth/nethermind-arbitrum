// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Specs;
using Nethermind.State;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Debug logger for block production. Logs state hashes at each step
/// for comparison with Nitro implementation.
/// </summary>
public class BlockDebugLogger : IDisposable
{
    private const string LogPath = "/tmp/nethermind-block.log";
    private static BlockDebugLogger? _instance;
    private readonly StreamWriter? _writer;
    private IWorldState? _worldState;
    private IReleaseSpec? _spec;

    private BlockDebugLogger()
    {
        try { _writer = new StreamWriter(LogPath, append: true); }
        catch { _writer = null; }
    }

    public static BlockDebugLogger GetOrCreate(IWorldState worldState, IReleaseSpec spec)
    {
        _instance ??= new BlockDebugLogger();
        _instance._worldState = worldState;
        _instance._spec = spec;
        return _instance;
    }

    private string GetStateRoot()
    {
        if (_worldState == null || _spec == null) return "N/A";
        try
        {
            _worldState.Commit(_spec, isGenesis: false);
            _worldState.RecalculateStateRoot();
            return _worldState.StateRoot.ToString();
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

    public void LogValue(string key, object? value)
    {
        if (_writer == null) return;
        _writer.WriteLine($"{key}: {value}");
        _writer.Flush();
    }

    /// <summary>
    /// Static method for logging from places without access to IWorldState/IReleaseSpec.
    /// Uses the singleton instance if available.
    /// </summary>
    public static void LogValueStatic(string key, object? value)
    {
        _instance?.LogValue(key, value);
    }

    public void Dispose() => _writer?.Dispose();
}
