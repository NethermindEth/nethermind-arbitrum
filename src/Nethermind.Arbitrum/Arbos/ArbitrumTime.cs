// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos;

public static class ArbitrumTime
{
    public const ulong StartTime = 1421388000; // The day it all began, Friday, January 16, 2015 6:00:00 AM GMT

    public static ulong HoursToSeconds(ulong timestamp, uint hoursSinceArbitrum)
    {
        ulong seconds = hoursSinceArbitrum * 3600ul;
        ulong activatedAtSeconds = StartTime + seconds;
        return timestamp - activatedAtSeconds;
    }

    public static uint HoursSinceArbitrum(ulong timestamp)
    {
        ulong secondsSinceStart = timestamp - StartTime;
        uint hoursSinceArbitrum = (uint)(secondsSinceStart / 3600); // 3600 seconds in an hour
        return System.Math.Min(hoursSinceArbitrum, Math.Utils.MaxUint24);
    }

    public static ulong DaysToSeconds(ulong days)
    {
        return days * 86400; // 24 * 60 * 60
    }
}
