// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos;

public static class ArbosVersion
{
    public const ulong Zero = 0;
    public const ulong One = 1;
    public const ulong Two = 2;
    public const ulong Three = 3;
    public const ulong Four = 4;
    public const ulong Five = 5;
    public const ulong Six = 6;
    public const ulong Seven = 7;
    public const ulong Eight = 8;
    public const ulong Nine = 9;
    public const ulong Ten = 10;
    public const ulong Eleven = 11;
    public const ulong Thirty = 30;
    public const ulong ThirtyOne = 31;
    public const ulong Forty = 40;

    // Semantic aliases for important milestones
    public static ulong FixRedeemGas = Eleven;
    public static ulong IntroduceInfraFees = Five;  // Version 5+ introduces infrastructure fees
    public static ulong ChangePosterDestination = Two;  // Version 2+ changes poster fee destination
    public static ulong L1FeesAvailable = Ten;  // Version 10+ tracks L1 fees available
    public static ulong FixZombieAccounts = Thirty;  // Version 30+ fixes zombie account creation
    public static ulong StylusFixes = ThirtyOne; // Version 31+ includes fixes for Stylus
    public static ulong ParentBlockHashSupport = Forty;  // Version 40+ supports parent block hash processing
}
