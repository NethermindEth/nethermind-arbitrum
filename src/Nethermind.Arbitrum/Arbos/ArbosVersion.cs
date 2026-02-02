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
    public const ulong Twenty = 20;
    public const ulong Thirty = 30;
    public const ulong ThirtyOne = 31;
    public const ulong ThirtyTwo = 32;
    public const ulong Forty = 40;
    public const ulong FortyOne = 41;
    public const ulong FortyTwo = 42;
    public const ulong FortyThree = 43;
    public const ulong FortyFour = 44;
    public const ulong FortyFive = 45;
    public const ulong FortySix = 46;
    public const ulong FortySeven = 47;
    public const ulong FortyEight = 48;
    public const ulong FortyNine = 49;
    public const ulong Fifty = 50;
    public const ulong FiftyOne = 51;

    // Semantic aliases for important milestones
    public const ulong FixRedeemGas = Eleven;
    public const ulong IntroduceInfraFees = Five;  // Version 5+ introduces infrastructure fees
    public const ulong ChangePosterDestination = Two;  // Version 2+ changes poster fee destination
    public const ulong L1FeesAvailable = Ten;  // Version 10+ tracks L1 fees available
    public const ulong FixZombieAccounts = Thirty;  // Version 30+ fixes zombie account creation
    public const ulong Stylus = Thirty; // Version 30+ introduces Stylus
    public const ulong StylusFixes = ThirtyOne; // Version 31+ includes fixes for Stylus
    public const ulong StylusChargingFixes = ThirtyTwo; // Version 32+ includes charging fixes for Stylus
    public const ulong ParentBlockHashSupport = Forty;  // Version 40+ supports parent block hash processing
    public const ulong NativeTokenManagement = FortyOne; // Version 41+ adds native token management precompile methods
    public const ulong Dia = Fifty; // Version 50+ (Dia) caps Stylus stack depth and adds per-tx gas limit
    public const ulong MultiConstraintPricing = Fifty; // Version 50+ introduces multi-constraint gas pricing
}
