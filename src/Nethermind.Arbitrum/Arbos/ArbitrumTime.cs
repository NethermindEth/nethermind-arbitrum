// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Arbos;

public static class ArbitrumTime
{
    public const ulong StartTime = 1421388000; // The day it all began, Friday, January 16, 2015 6:00:00 AM GMT

    /// <summary>
    /// Computes program age in seconds from the hours passed since Arbitrum began.
    /// </summary>
    public static ulong HoursToAgeSeconds(ulong timestamp, uint hoursSinceArbitrum)
    {
        ulong seconds = Math.Utils.SaturateMul(hoursSinceArbitrum, 3600ul);
        ulong activatedAtSeconds = Math.Utils.SaturateAdd(StartTime, seconds);
        return Math.Utils.SaturateSub(timestamp, activatedAtSeconds);
    }

    /// <summary>
    /// Hours since Arbitrum began, rounded down.
    /// </summary>
    public static uint HoursSinceArbitrum(ulong timestamp)
    {
        ulong secondsSinceStart = Math.Utils.SaturateSub(timestamp, StartTime);
        uint hoursSinceArbitrum = (uint)(secondsSinceStart / 3600ul); // 3600 seconds in an hour
        return System.Math.Min(hoursSinceArbitrum, Math.Utils.MaxUint24);
    }

    public static ulong DaysToSeconds(ulong days)
    {
        return days * 86400; // 24 * 60 * 60
    }
}
