using System.Buffers.Binary;
using System.Text;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public enum TracingScenario : byte
{
    TracingBeforeEvm,
    TracingDuringEvm,
    TracingAfterEvm
}

public class TracingInfo : IDisposable
{
    private static readonly byte[] MockReturnStack = CreateStackBytes([UInt256.Zero, UInt256.Zero]);
    private static readonly byte[] MockReturnPop = CreateStackBytes([UInt256.One]);
    private ExecutionEnvironment? _env;
    private readonly StorageCache _storageCache = new();
    private bool _firstOpcodeInHostio = true;
    private bool _disposed;

    public TracingInfo(IArbitrumTxTracer tracer, TracingScenario scenario, ExecutionEnvironment? env)
    {
        Tracer = tracer;
        Scenario = scenario;
        if (scenario == TracingScenario.TracingDuringEvm && env == null)
            throw new ArgumentException("ExecutionEnvironment needs to be set to TracingDuringEvm");
        _env = env;
    }

    public IArbitrumTxTracer Tracer { get; }
    public TracingScenario Scenario { get; }

    /// <summary>
    /// Disposes the ExecutionEnvironment held by this TracingInfo, returning it to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _env?.Dispose();
        _env = null;
    }

    public void RecordStorageGet(ValueHash256 key)
    {
        if (!Tracer.IsTracingStorage)
            return;

        if (Scenario == TracingScenario.TracingDuringEvm)
        {
            byte[] stack = CreateStackBytes([new UInt256(key.Bytes)]);
            TraceInstruction(new TraceMemory(), new TraceStack(stack), Instruction.SLOAD);
        }
        else
        {
            if (_env != null)
                Tracer.CaptureArbitrumStorageGet(new UInt256(key.Bytes), _env.CallDepth,
                    Scenario == TracingScenario.TracingBeforeEvm);
        }
    }

    public void RecordStorageSet(ValueHash256 key, ValueHash256 value)
    {
        if (!Tracer.IsTracingStorage)
            return;

        if (Scenario == TracingScenario.TracingDuringEvm)
        {
            byte[] stack = CreateStackBytes(new[] { new UInt256(key.Bytes), new UInt256(value.Bytes) });
            TraceInstruction(new TraceMemory(), new TraceStack(stack), Instruction.SSTORE);
        }
        else
        {
            if (_env != null)
                Tracer.CaptureArbitrumStorageSet(new UInt256(key.Bytes), value, _env.CallDepth,
                    Scenario == TracingScenario.TracingBeforeEvm);
        }
    }

    public void MockCall(Address from, Address to, UInt256 amount, long gas, byte[] input)
    {
        // if (!Tracer.IsTracingActions) return;
        TraceMemory memoryCall = new((ulong)input.Length, input);
        Span<UInt256> callArgs = stackalloc UInt256[7];
        callArgs[0] = (UInt256)gas;
        callArgs[1] = new UInt256(to.Bytes);
        callArgs[2] = amount;
        callArgs[3] = 0; // memory offset
        callArgs[4] = (UInt256)input.Length; // memory length
        callArgs[5] = 0; // return offset
        callArgs[6] = 0; // return size

        TraceStack stackCall = new(CreateStackBytes(callArgs));
        TraceInstruction(memoryCall, stackCall, Instruction.CALL);

        Tracer.ReportAction(gas, amount, from, to, input, ExecutionType.CALL);

        TraceStack stackReturn = new(MockReturnStack);
        TraceInstruction(new TraceMemory(), stackReturn, Instruction.RETURN);

        Tracer.ReportActionEnd(gas, Array.Empty<byte>());

        TraceStack stackPop = new(MockReturnPop);
        TraceInstruction(new TraceMemory(), stackPop, Instruction.POP);
    }

    public void CaptureEvmTraceForHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk,
        ulong endInk)
    {
        _firstOpcodeInHostio = true;

        void Capture(Instruction op, ReadOnlyMemory<byte> memory, params UInt256[] stackValues)
        {
            const ulong inkToGas = 10000;
            ulong gas = endInk / inkToGas;
            ulong cost = 0;
            if (_firstOpcodeInHostio)
            {
                cost = startInk > endInk ? (startInk - endInk) / inkToGas : 0;
                _firstOpcodeInHostio = false;
            }

            CaptureState(op, gas, cost, memory, stackValues);
        }

        switch (name)
        {
            case "read_args":
                Capture(Instruction.CALLDATACOPY, outs.ToArray(), UInt256.Zero, UInt256.Zero, new UInt256((ulong)outs.Length));
                break;

            case "storage_load_bytes32":
                if (args.Length < 32 || outs.Length < 32)
                    return;
                ReadOnlySpan<byte> key = args[..32];
                ReadOnlySpan<byte> value = outs[..32];
                if (_storageCache.Load(new Hash256(key), new Hash256(value)))
                {
                    Capture(Instruction.SLOAD, Array.Empty<byte>(), new UInt256(key));
                    Capture(Instruction.POP, Array.Empty<byte>(), new UInt256(value));
                }

                break;

            case "storage_cache_bytes32":
                if (args.Length < 64)
                    return;
                _storageCache.Store(new Hash256(args[..32]), new Hash256(args.Slice(32, 32)));
                break;

            case "storage_flush_cache":
                if (args.Length < 1)
                    return;
                foreach (StorageStore store in _storageCache.Flush())
                    Capture(Instruction.SSTORE, Array.Empty<byte>(), new UInt256(store.Key.Bytes),
                        new UInt256(store.Value.Bytes));
                if (args[0] != 0)
                    _storageCache.Clear();
                break;

            case "transient_load_bytes32":
                if (args.Length < 32 || outs.Length < 32)
                    return;
                Capture(Instruction.TLOAD, null, new UInt256(args[..32]));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "transient_store_bytes32":
                if (args.Length < 64)
                    return;
                Capture(Instruction.TSTORE, null, new UInt256(args[..32]), new UInt256(args.Slice(32, 64)));
                break;

            case "create1":
                if (args.Length < 32 || outs.Length < 20)
                    return;
                UInt256 createValue = new(args[..32]);
                byte[] createCode = args[32..].ToArray();
                UInt256 createAddress = new(outs[..20]);
                Capture(Instruction.CREATE, createCode, createValue, UInt256.Zero,
                    new UInt256((ulong)createCode.Length));
                Capture(Instruction.POP, null, createAddress);
                break;

            case "create2":
                if (args.Length < 64 || outs.Length < 20)
                    return;
                UInt256 create2Value = new(args[..32]);
                UInt256 create2Salt = new(args.Slice(32, 32));
                byte[] create2Code = args[64..].ToArray();
                UInt256 create2Address = new(outs[..20]);
                Capture(Instruction.CREATE2, create2Code, create2Value, UInt256.Zero,
                    new UInt256((ulong)create2Code.Length), create2Salt);
                Capture(Instruction.POP, null, create2Address);
                break;

            case "read_return_data":
                if (args.Length < 8)
                    return;
                Capture(Instruction.RETURNDATACOPY, outs.ToArray(), UInt256.Zero, new UInt256(args[..4]),
                    new UInt256(args.Slice(4, 4)));
                break;

            case "return_data_size":
                if (outs.Length < 4)
                    return;
                Capture(Instruction.RETURNDATASIZE, null);
                Capture(Instruction.POP, null, new UInt256(outs[..4]));
                break;

            case "emit_log":
                if (args.Length < 4)
                    return;
                uint numTopics = BinaryPrimitives.ReadUInt32BigEndian(args[..4]);

                int dataOffset = 4 + (int)numTopics * 32;
                if (args.Length < dataOffset)
                    return;

                byte[] logData = args[dataOffset..].ToArray();
                List<UInt256> stack = [UInt256.Zero, new((ulong)logData.Length)];

                for (int i = 0; i < numTopics; i++)
                    stack.Add(new UInt256(args.Slice(4 + i * 32, 32)));

                // Assuming Instruction enum has LOG0, LOG1, etc. defined contiguously.
                Instruction logOp = (Instruction)((byte)Instruction.LOG0 + numTopics);
                Capture(logOp, logData, stack.ToArray());
                break;

            case "account_balance":
                if (args.Length < 20 || outs.Length < 32)
                    return;
                Capture(Instruction.BALANCE, Array.Empty<byte>(), new UInt256(args[..20]));
                Capture(Instruction.POP, Array.Empty<byte>(), new UInt256(outs[..32]));
                break;

            case "account_code":
                if (args.Length < 28)
                    return;
                Capture(Instruction.EXTCODECOPY, null, new UInt256(args[..20]), UInt256.Zero,
                    new UInt256(args.Slice(20, 4)), new UInt256(args.Slice(24, 4)));
                break;

            case "account_code_size":
                if (args.Length < 20 || outs.Length < 4)
                    return;
                Capture(Instruction.EXTCODESIZE, null, new UInt256(args[..20]));
                Capture(Instruction.POP, null, new UInt256(outs[..4]));
                break;

            case "account_codehash":
                if (args.Length < 20 || outs.Length < 32)
                    return;
                Capture(Instruction.EXTCODEHASH, null, new UInt256(args[..20]));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "block_basefee":
                if (outs.Length < 32)
                    return;
                Capture(Instruction.BASEFEE, null);
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "block_coinbase":
                if (outs.Length < 20)
                    return;
                Capture(Instruction.COINBASE, null);
                Capture(Instruction.POP, null, new UInt256(outs[..20]));
                break;

            case "block_gas_limit":
                if (outs.Length < 8)
                    return;
                Capture(Instruction.GASLIMIT, null);
                Capture(Instruction.POP, null, new UInt256(outs[..8]));
                break;

            case "block_number":
                if (outs.Length < 8)
                    return;
                Capture(Instruction.NUMBER, null);
                Capture(Instruction.POP, null, new UInt256(outs[..8]));
                break;

            case "block_timestamp":
                if (outs.Length < 8)
                    return;
                Capture(Instruction.TIMESTAMP, null);
                Capture(Instruction.POP, null, new UInt256(outs[..8]));
                break;

            case "chainid":
                if (outs.Length < 8)
                    return;
                Capture(Instruction.CHAINID, null);
                Capture(Instruction.POP, null, new UInt256(outs[..8]));
                break;

            case "contract_address":
                if (outs.Length < 20)
                    return;
                Capture(Instruction.ADDRESS, null);
                Capture(Instruction.POP, null, new UInt256(outs[..20]));
                break;

            case "evm_gas_left":
            case "evm_ink_left":
                if (outs.Length < 8)
                    return;
                Capture(Instruction.GAS, null);
                Capture(Instruction.POP, null, new UInt256(outs[..8]));
                break;

            case "math_div":
                if (args.Length < 64 || outs.Length < 32)
                    return;
                Capture(Instruction.DIV, null, new UInt256(args[..32]), new UInt256(args.Slice(32, 32)));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "math_mod":
                if (args.Length < 64 || outs.Length < 32)
                    return;
                Capture(Instruction.MOD, null, new UInt256(args[..32]), new UInt256(args.Slice(32, 32)));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "math_pow":
                if (args.Length < 64 || outs.Length < 32)
                    return;
                Capture(Instruction.EXP, null, new UInt256(args[..32]), new UInt256(args.Slice(32, 32)));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "math_add_mod":
                if (args.Length < 96 || outs.Length < 32)
                    return;
                Capture(Instruction.ADDMOD, null, new UInt256(args[..32]), new UInt256(args.Slice(32, 32)),
                    new UInt256(args.Slice(64, 32)));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "math_mul_mod":
                if (args.Length < 96 || outs.Length < 32)
                    return;
                Capture(Instruction.MULMOD, null, new UInt256(args[..32]), new UInt256(args.Slice(32, 32)),
                    new UInt256(args.Slice(64, 32)));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "msg_sender":
                if (outs.Length < 20)
                    return;
                Capture(Instruction.CALLER, null);
                Capture(Instruction.POP, null, new UInt256(outs[..20]));
                break;

            case "msg_value":
                if (outs.Length < 32)
                    return;
                Capture(Instruction.CALLVALUE, null);
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "native_keccak256":
                if (outs.Length < 32)
                    return;
                byte[] keccakData = args.ToArray();
                Capture(Instruction.KECCAK256, keccakData, UInt256.Zero, new UInt256((ulong)keccakData.Length));
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "tx_gas_price":
                if (outs.Length < 32)
                    return;
                Capture(Instruction.GASPRICE, null);
                Capture(Instruction.POP, null, new UInt256(outs[..32]));
                break;

            case "tx_ink_price":
                if (outs.Length < 4)
                    return;
                Capture(Instruction.GASPRICE, null); // Assumed to be equivalent for tracing
                Capture(Instruction.POP, null, new UInt256(outs[..4]));
                break;

            case "tx_origin":
                if (outs.Length < 20)
                    return;
                Capture(Instruction.ORIGIN, null);
                Capture(Instruction.POP, null, new UInt256(outs[..20]));
                break;

            // Cases handled elsewhere or with no EVM equivalent
            case "call_contract":
            case "delegate_call_contract":
            case "static_call_contract":
            case "write_result":
            case "exit_early":
            case "user_entrypoint":
            case "user_returned":
            case "msg_reentrant":
            case "pay_for_memory_grow":
            case "console_log_text":
            case "console_log":
                break;
        }
    }

    public void CaptureStylusCall(Instruction opCode, Address contract, UInt256 value, byte[] input, ulong gas,
        ulong startGas, ulong baseCost)
    {
        List<UInt256> stack = new()
        {
            new(gas),
            new(contract.Bytes)
        };
        if (opCode == Instruction.CALL)
            stack.Add(value);
        stack.Add(UInt256.Zero); // memory offset
        stack.Add(new UInt256((ulong)input.Length)); // memory length
        stack.Add(UInt256.Zero); // return offset
        stack.Add(UInt256.Zero); // return size

        CaptureState(opCode, startGas, baseCost + gas, input, stack.ToArray());
    }

    public void CaptureStylusExit(byte status, byte[]? data, Exception? err, ulong gas)
    {
        Instruction opCode;
        if (status == 0)
        {
            if (data?.Length == 0)
            {
                CaptureState(Instruction.STOP, gas, 0, null, ReadOnlySpan<UInt256>.Empty);
                return;
            }

            opCode = Instruction.RETURN;
        }
        else
        {
            opCode = Instruction.REVERT;
            if (data == null && err != null)
                data = Encoding.UTF8.GetBytes(err.Message);
        }

        ReadOnlySpan<UInt256> stack = [UInt256.Zero, new((ulong)(data?.Length ?? 0))];
        CaptureState(opCode, gas, 0, data, stack);
    }

    private void CaptureState(Instruction op, ulong gas, ulong cost, ReadOnlyMemory<byte> memory,
        ReadOnlySpan<UInt256> stackValues)
    {
        TraceMemory memoryTrace = new((ulong)memory.Length, memory);
        TraceStack stackTrace = new(CreateStackBytes(stackValues));

        if (_env != null)
        {
            Tracer.StartOperation(0, op, (long)gas, _env);
        }

        if (Tracer.IsTracingMemory)
        {
            Tracer.SetOperationMemory(memoryTrace);
            Tracer.SetOperationMemorySize(memoryTrace.Size);
        }

        if (Tracer.IsTracingStack)
            Tracer.SetOperationStack(stackTrace);
        Tracer.ReportOperationRemainingGas((long)(gas - cost));
    }

    private void TraceInstruction(TraceMemory memory, TraceStack stack, Instruction instruction)
    {
        if (_env != null)
        {
            Tracer.StartOperation(0, instruction, 0, _env);
        }
        if (Tracer.IsTracingMemory)
        {
            Tracer.SetOperationMemory(memory);
            Tracer.SetOperationMemorySize(memory.Size);
        }

        if (Tracer.IsTracingStack)
            Tracer.SetOperationStack(stack);
        Tracer.ReportOperationRemainingGas(0);
    }

    private static byte[] CreateStackBytes(ReadOnlySpan<UInt256> args)
    {
        if (args.IsEmpty)
            return [];

        byte[] stackBytes = new byte[args.Length * 32];
        Span<byte> span = stackBytes.AsSpan();

        for (int i = 0; i < args.Length; i++)
            args[args.Length - 1 - i].ToBigEndian(span.Slice(i * 32, 32));

        return stackBytes;
    }
}
