// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;

public static class ArbWasmErrors // Based on ArbWasm solidity interface errors
{
    public const string ProgramNotWasm = "ProgramNotWasm";
    public const string ProgramNotActivated = "ProgramNotActivated";
    public static string ProgramNeedsUpgrade(ushort programVersion, ushort stylusVersion) => $"ProgramNeedsUpgrade({programVersion}, {stylusVersion})";
    public static string ProgramExpired(ulong ageInSeconds) => $"ProgramExpired({ageInSeconds})";
    public const string ProgramUpToDate = "ProgramUpToDate";
    public static string ProgramKeepaliveTooSoon(ulong ageInSeconds) => $"ProgramKeepaliveTooSoon({ageInSeconds})";
    public static string ProgramInsufficientValue(UInt256 have, UInt256 want) => $"ProgramInsufficientValue({have}, {want})";
}
