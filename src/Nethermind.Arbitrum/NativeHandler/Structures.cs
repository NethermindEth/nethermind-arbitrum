using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.NativeHandler;

[StructLayout(LayoutKind.Sequential)]
public struct GoSliceData
{
    public IntPtr ptr; // pointer to data
    public UIntPtr len; // length of data
}

[StructLayout(LayoutKind.Sequential)]
public struct RustBytes
{
    public IntPtr ptr;
    public UIntPtr len;
}

[StructLayout(LayoutKind.Sequential)]
public struct PricingParams
{
    public uint ink_price;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusConfig
{
    public ushort version;
    public uint max_depth;
    public PricingParams pricing;
}

[StructLayout(LayoutKind.Sequential)]
public struct Bytes32
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] bytes;
}

[StructLayout(LayoutKind.Sequential)]
public struct Bytes20
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public byte[] bytes;
}

[StructLayout(LayoutKind.Sequential)]
public struct EvmData
{
    public ulong arbos_version;
    public Bytes32 block_basefee;
    public ulong chainid;
    public Bytes20 block_coinbase;
    public ulong block_gas_limit;
    public ulong block_number;
    public ulong block_timestamp;
    public Bytes20 contract_address;
    public Bytes32 module_hash;
    public Bytes20 msg_sender;
    public Bytes32 msg_value;
    public Bytes32 tx_gas_price;
    public Bytes20 tx_origin;
    public uint reentrant;
    [MarshalAs(UnmanagedType.I1)] public bool cached;
    [MarshalAs(UnmanagedType.I1)] public bool tracing;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusData
{
    public ushort init_cost;
    public ushort cached_init_cost;
    public ushort footprint;
    public uint asm_estimate;
}