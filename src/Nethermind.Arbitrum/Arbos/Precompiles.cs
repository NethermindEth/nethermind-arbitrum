using System.Collections.Frozen;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Arbos;

public static class Precompiles
{
    public static readonly byte[] InvalidCode = [(byte)Instruction.INVALID];
    public static readonly Hash256 InvalidCodeHash = Keccak.Compute(InvalidCode);

    public static readonly IReadOnlyDictionary<Address, ulong> PrecompileMinArbOSVersions = new Dictionary<Address, ulong>
    {
        [ArbosAddresses.ArbosAddress] = 0,
        [ArbosAddresses.ArbSysAddress] = 0,
        [ArbosAddresses.ArbInfoAddress] = 0,
        [ArbosAddresses.ArbAddressTableAddress] = 0,
        [ArbosAddresses.ArbBLSAddress] = 0,
        [ArbosAddresses.ArbFunctionTableAddress] = 0,
        [ArbosAddresses.ArbosTestAddress] = 0,
        [ArbosAddresses.ArbGasInfoAddress] = 0,
        [ArbosAddresses.ArbOwnerPublicAddress] = 0,
        [ArbosAddresses.ArbAggregatorAddress] = 0,
        [ArbosAddresses.ArbRetryableTxAddress] = 0,
        [ArbosAddresses.ArbStatisticsAddress] = 0,
        [ArbosAddresses.ArbOwnerAddress] = 0,
        [ArbosAddresses.ArbWasmAddress] = 30,
        [ArbosAddresses.ArbWasmCacheAddress] = 30,
        [ArbosAddresses.ArbDebugAddress] = 0,
    }.ToFrozenDictionary();
}
