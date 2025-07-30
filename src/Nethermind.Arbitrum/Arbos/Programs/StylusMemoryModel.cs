// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Arbos.Programs;

public readonly ref struct StylusMemoryModel(ushort freePages, ushort pageGas)
{
    private static readonly uint[] MemoryExponents =
    [
        1, 1, 1, 1, 1, 1, 2, 2, 2, 3, 3, 4, 5, 5, 6, 7, 8, 9, 11, 12, 14, 17, 19, 22, 25, 29, 33, 38,
        43, 50, 57, 65, 75, 85, 98, 112, 128, 147, 168, 193, 221, 253, 289, 331, 379, 434, 497, 569,
        651, 745, 853, 976, 1117, 1279, 1463, 1675, 1917, 2194, 2511, 2874, 3290, 3765, 4309, 4932,
        5645, 6461, 7395, 8464, 9687, 11087, 12689, 14523, 16621, 19024, 21773, 24919, 28521, 32642,
        37359, 42758, 48938, 56010, 64104, 73368, 83971, 96106, 109994, 125890, 144082, 164904, 188735,
        216010, 247226, 282953, 323844, 370643, 424206, 485509, 555672, 635973, 727880, 833067, 953456,
        1091243, 1248941, 1429429, 1636000, 1872423, 2143012, 2452704, 2807151, 3212820, 3677113,
        4208502, 4816684, 5512756, 6309419, 7221210, 8264766, 9459129, 10826093, 12390601, 14181199,
        16230562, 18576084, 21260563, 24332984, 27849408, 31873999
    ];

    public ulong GetGasCost(ushort newPages, ushort pagesOpenNow, ushort pagesOpenEver)
    {
        ushort newOpen = Math.Utils.SaturateAdd(newPages, pagesOpenNow);
        ushort newEver = Math.Utils.SaturateAdd(newOpen, pagesOpenEver);

        // Free until expansion beyond the first few
        if (newEver < freePages)
            return 0;

        ushort adding = Math.Utils.SaturateSub(
            Math.Utils.SaturateSub(newOpen, freePages),
            Math.Utils.SaturateSub(pagesOpenNow, freePages));
        ulong linear = Math.Utils.SaturateMul((ulong)adding, pageGas);
        ulong expand = Exp(newEver) - Exp(pagesOpenEver);

        return Math.Utils.SaturateAdd(linear, expand);
    }

    private ulong Exp(ushort pages)
    {
        return pages < MemoryExponents.Length
            ? MemoryExponents[pages]
            : ulong.MaxValue;
    }
}
