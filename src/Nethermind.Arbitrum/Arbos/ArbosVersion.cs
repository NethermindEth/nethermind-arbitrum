// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos;

public static class ArbosVersion
{
    public static ulong Zero = 0;
    public static ulong One = 1;
    public static ulong Two = 2;
    public static ulong Three = 3;
    public static ulong Four = 4;
    public static ulong Five = 5;
    public static ulong Six = 6;
    public static ulong Seven = 7;
    public static ulong Eight = 8;
    public static ulong Nine = 9;
    public static ulong Ten = 10;
    public static ulong Eleven = 11;
    public static ulong Twenty = 20;
    public static ulong Thirty = 30;
    public static ulong Forty = 40;

    // Semantic aliases for important milestones
    public static ulong FixRedeemGas = Eleven;
    public static ulong IntroduceInfraFees = Five;  // Version 5+ introduces infrastructure fees
    public static ulong ChangePosterDestination = Two;  // Version 2+ changes poster fee destination
    public static ulong L1FeesAvailable = Ten;  // Version 10+ tracks L1 fees available
    public static ulong FixZombieAccounts = Thirty;  // Version 30+ fixes zombie account creation
    public static ulong ParentBlockHashSupport = Forty;  // Version 40+ supports parent block hash processing
}
