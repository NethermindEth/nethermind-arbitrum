// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Nethermind.Core;
using Nethermind.Core.Specs;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbitrumPrecompileChecker : IPrecompileChecker
{
    public bool IsPrecompile(Address address, IReleaseSpec spec)
    {
        Span<uint> data = MemoryMarshal.Cast<byte, uint>(address.Bytes.AsSpan());

        return (data[4] & 0x0000ffff) == 0
               && data[3] == 0 && data[2] == 0 && data[1] == 0 && data[0] == 0
               && (data[4] >>> 24) switch
               {
                   0x64 => true, // ArbSys
                   _ => false
               };
    }
}
