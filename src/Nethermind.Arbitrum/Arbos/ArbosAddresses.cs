using Nethermind.Core;

namespace Nethermind.Arbitrum.Arbos;

public class ArbosAddresses
{
    public static readonly Address ArbosAddress = new("0x00000000000000000000000000000000000a4b05");
    public static readonly Address ArbosSystemAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

    public static readonly Address ArbSysAddress = new("0x0000000000000000000000000000000000000064");
    public static readonly Address ArbInfoAddress = new("0x0000000000000000000000000000000000000065");
    public static readonly Address ArbAddressTableAddress = new("0x0000000000000000000000000000000000000066");
    public static readonly Address ArbBLSAddress = new("0x0000000000000000000000000000000000000067");
    public static readonly Address ArbFunctionTableAddress = new("0x0000000000000000000000000000000000000068");
    public static readonly Address ArbosTestAddress = new("0x0000000000000000000000000000000000000069");
    public static readonly Address ArbGasInfoAddress = new("0x000000000000000000000000000000000000006c");
    public static readonly Address ArbOwnerPublicAddress = new("0x000000000000000000000000000000000000006b");
    public static readonly Address ArbAggregatorAddress = new("0x000000000000000000000000000000000000006d");
    public static readonly Address ArbRetryableTxAddress = new("0x000000000000000000000000000000000000006e");
    public static readonly Address ArbStatisticsAddress = new("0x000000000000000000000000000000000000006f");
    public static readonly Address ArbOwnerAddress = new("0x0000000000000000000000000000000000000070");
    public static readonly Address ArbWasmAddress = new("0x0000000000000000000000000000000000000071");
    public static readonly Address ArbWasmCacheAddress = new("0x0000000000000000000000000000000000000072");
    public static readonly Address ArbDebugAddress = new("0x00000000000000000000000000000000000000ff");

    // Virtual contacts
    public static readonly Address NodeInterfaceAddress = new("0x00000000000000000000000000000000000000c8");
    public static readonly Address NodeInterfaceDebugAddress = new("0x00000000000000000000000000000000000000c9");

    public static readonly Address L1PricerFundsPoolAddress = new("0xA4B00000000000000000000000000000000000f6");
    public static readonly Address BatchPosterAddress = new("0xA4B000000000000000000073657175656e636572");
    public static readonly Address BatchPosterPayToAddress = BatchPosterAddress;
}
