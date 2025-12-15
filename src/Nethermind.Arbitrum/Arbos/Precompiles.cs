using System.Collections.Frozen;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Arbos;

public static class Precompiles
{
    public static readonly byte[] InvalidCode = [(byte)Instruction.INVALID];
    public static readonly Hash256 InvalidCodeHash = Keccak.Compute(InvalidCode);

    // EIP-2935 - Serve historical block hashes from state (Arbitrum version)
    // Differs from the original EIP-2935 in two aspects:
    // 1. Buffer size is 393,168 blocks instead of 8191
    // 2. Uses arb_block_num (L2 block number) instead of number (L1 block number)
    // See: https://github.com/OffchainLabs/sys-asm/blob/main/src/execution_hash/main.eas
    // See: https://github.com/OffchainLabs/go-ethereum/blob/57fe4b732d4e640e696da40773f2dacba97e722b/params/protocol_params.go#L221
    public static readonly byte[] HistoryStorageCodeArbitrum =
        Bytes.FromHexString(
            "0x3373fffffffffffffffffffffffffffffffffffffffe1460605760203603605c575f3563a3b1b31d5f5260205f6004601c60645afa15605c575f51600181038211605c57816205ffd0910311605c576205ffd09006545f5260205ff35b5f5ffd5b5f356205ffd0600163a3b1b31d5f5260205f6004601c60645afa15605c575f5103065500");

    public static readonly Hash256 HistoryStorageCodeHash = Keccak.Compute(HistoryStorageCodeArbitrum);

    private static readonly Address P256VerifyAddress = new("0x0000000000000000000000000000000000000100");
    private static readonly Address Bls12381G1AddAddress = new("0x000000000000000000000000000000000000000b");
    private static readonly Address Bls12381G1MulAddress = new("0x000000000000000000000000000000000000000c");
    private static readonly Address Bls12381G1MultiExpAddress = new("0x000000000000000000000000000000000000000d");
    private static readonly Address Bls12381G2AddAddress = new("0x000000000000000000000000000000000000000e");
    private static readonly Address Bls12381G2MulAddress = new("0x000000000000000000000000000000000000000f");
    private static readonly Address Bls12381G2MultiExpAddress = new("0x0000000000000000000000000000000000000010");
    private static readonly Address Bls12381PairingAddress = new("0x0000000000000000000000000000000000000011");
    private static readonly Address Bls12381MapFpToG1Address = new("0x0000000000000000000000000000000000000012");
    private static readonly Address Bls12381MapFp2ToG2Address = new("0x0000000000000000000000000000000000000013");

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
        [ArbosAddresses.ArbDebugAddress] = 0,
        [ArbosAddresses.ArbWasmAddress] = 30,
        [ArbosAddresses.ArbWasmCacheAddress] = 30,
        [ArbosAddresses.ArbNativeTokenManagerAddress] = 41,

        // P256Verify (RIP-7212) - activated in ArbOS 30
        [P256VerifyAddress] = 30,

        // BLS12-381 precompiles (EIP-2537 / Prague-Osaka) - activated in ArbOS 50
        [Bls12381G1AddAddress] = 50,
        [Bls12381G1MulAddress] = 50,
        [Bls12381G1MultiExpAddress] = 50,
        [Bls12381G2AddAddress] = 50,
        [Bls12381G2MulAddress] = 50,
        [Bls12381G2MultiExpAddress] = 50,
        [Bls12381PairingAddress] = 50,
        [Bls12381MapFpToG1Address] = 50,
        [Bls12381MapFp2ToG2Address] = 50,
    }.ToFrozenDictionary();
}
