using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Nethermind.Arbitrum.NativeHandler;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Test.Stylus;

public class TestNativeImpl : INativeApi
{
    public ConcurrentDictionary<Bytes20, byte[]> Contracts { get; }
    public ConcurrentDictionary<byte[], byte[]> Storage { get; }
    public Bytes20 Program { get; }
    public ConcurrentBag<byte> WriteResult { get; }
    public ConcurrentDictionary<Bytes20, StylusConfig> Configs { get; }
    public EvmData EvmData { get; }

    private (ushort open, ushort ever) pages;
    private readonly object pagesLock = new();
    
    private GCHandle _selfHandle;
    private readonly List<GCHandle> handles;

    public TestNativeImpl(
        Bytes20 program,
        EvmData evmData)
    {
        Contracts = new ConcurrentDictionary<Bytes20, byte[]>();
        Storage = new ConcurrentDictionary<byte[], byte[]>(Bytes.EqualityComparer);
        WriteResult = new ConcurrentBag<byte>();
        Configs = new ConcurrentDictionary<Bytes20, StylusConfig>();
        pages = (0, 0);

        Program = program;
        EvmData = evmData;
        _selfHandle = GCHandle.Alloc(this);
        handles = new();
    }

    public void SetPageState(ushort open, ushort ever)
    {
        lock (pagesLock)
        {
            pages = (open, ever);
        }
    }

    public (ushort open, ushort ever) GetPageState()
    {
        lock (pagesLock)
        {
            return pages;
        }
    }
    public (byte[] result, byte[] rawData, ulong gasCost) Handle(RequestType requestType, byte[] input)
    {
        switch (requestType)
        {
            case RequestType.GetBytes32:
                var key1 = input[..];
                return  (Storage[key1], [], 2100);
            case RequestType.SetTrieSlots:
                var gasLeft2 = BinaryPrimitives.ReadInt64BigEndian(input[..8]);
                var key2 = input[8..40];
                var value = input[40..];
                Storage[key2] = value;
                break;
            case RequestType.GetTransientBytes32:
                break;
            case RequestType.SetTransientBytes32:
                break;
            case RequestType.ContractCall:
                break;
            case RequestType.DelegateCall:
                break;
            case RequestType.StaticCall:
                break;
            case RequestType.Create1:
                break;
            case RequestType.Create2:
                break;
            case RequestType.EmitLog:
                break;
            case RequestType.AccountBalance:
                break;
            case RequestType.AccountCode:
                break;
            case RequestType.AccountCodeHash:
                break;
            case RequestType.AddPages:
                break;
            case RequestType.CaptureHostIo:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null);
        }
        return ([(byte)ApiStatus.Success], [], 0);
    }

    public GoSliceData AllocateGoSlice(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return new GoSliceData { Ptr = IntPtr.Zero, Len = UIntPtr.Zero };
        }

        var pinnedData = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        handles.Add(pinnedData);
        return new GoSliceData
        {
            Ptr = pinnedData.AddrOfPinnedObject(),
            Len = (UIntPtr)bytes.Length
        };
    }
    
    public void Dispose()
    {
        _selfHandle.Free();
        foreach (var handle in handles)
        {
            handle.Free();
        }
    }
}