// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Programs;

public static class StylusProgramsRebuildExtensions
{
    public static bool IsProgramActive(this StylusPrograms programs, in ValueHash256 codeHash, ulong currentTimestamp, StylusParams stylusParams)
    {
        StylusPrograms.Program program = programs.GetProgram(in codeHash, currentTimestamp);

        // Not activated
        if (program.Version == 0)
            return false;

        // Check if expired
        if (program.AgeSeconds > ArbitrumTime.DaysToSeconds(stylusParams.ExpiryDays))
            return false;

        return true;
    }

    public static ValueHash256? GetModuleHashForRebuild(this StylusPrograms programs, in ValueHash256 codeHash)
    {
        ValueHash256 moduleHash = programs.ModuleHashesStorage.Get(codeHash);

        // Check if it's zero (not found)
        return moduleHash == Hash256.Zero ? null : moduleHash;
    }

    public static (ushort version, uint activatedAtHours, ulong ageSeconds, bool cached) GetProgramInternalData(
        this StylusPrograms programs, in ValueHash256 codeHash, ulong timestamp)
    {
        StylusPrograms.Program program = programs.GetProgram(in codeHash, timestamp);
        return (program.Version, program.ActivatedAtHours, program.AgeSeconds, program.Cached);
    }
}
public class ProgramData
{
    public ushort Version { get; set; }
    public ulong ActivatedAt { get; set; }  // Hours since Arbitrum genesis
    public ulong ExpiresAt { get; set; }    // Hours since Arbitrum genesis (0 = no expiry based on ExpiryDays)

    public ProgramData(ushort version, ulong activatedAt, ulong expiresAt)
    {
        Version = version;
        ActivatedAt = activatedAt;
        ExpiresAt = expiresAt;
    }
}

