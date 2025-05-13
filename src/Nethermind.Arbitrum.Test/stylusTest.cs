using System.Runtime.InteropServices;
using System.Text;
using Nethermind.Arbitrum.NativeHandler;
using Nethermind.Core.Extensions;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test;

public static class TestStylus
{
    
    private const string NitroLocation = "/Users/tanishqjasoria/GolandProjects/nitro";
    private const string TargetLocation = "/Users/tanishqjasoria/GolandProjects/nitro/target";

    private static bool TestCompileLoad()
    {
        string filePath = $"{TargetLocation}/testdata/host.bin";
        string localTarget = Utils.LocalTarget();
        if (localTarget == Utils.TargetArm64) filePath = $"{TargetLocation}/testdata/arm64.bin";
        if (localTarget == Utils.TargetAmd64) filePath = $"{TargetLocation}/testdata/amd64.bin";

        Console.WriteLine($"starting load test. FilePath: {filePath} GOARCH/GOOS: {RuntimeInformation.ProcessArchitecture}/{Environment.OSVersion}");

        byte[] localAsm = File.ReadAllBytes(filePath);
        GoSliceData asmSlice = CreateSlice(localAsm);
        GoSliceData calldata = CreateSlice(new byte[] { 8, 123, 123, 123, 123, 3 });

        StylusConfig config = new StylusConfig
        {
            version = 1,
            max_depth = 10000,
            pricing = new PricingParams { ink_price = 1 }
        };

        var apiImpl = new TestNativeImpl();
        var id = EvmApiRegistry.Register(apiImpl);

        NativeRequestHandler handler = RegisterHandler.CreateHandler(id);

        EvmData evmData = new EvmData(); // Simplified for now

        RustBytes output = new RustBytes();
        ulong gas = 0xfffffffffffffff;

        Console.WriteLine("launching program..");
        int status = Rust.stylus_call(
            asmSlice,
            calldata,
            config,
            handler,
            evmData,
            true,
            ref output,
            ref gas,
            0);

        byte[] result = ReadBytes(output);
        Console.WriteLine($"returned: {status}, output: {Encoding.UTF8.GetString(result)}");

        return status == 0;
    }

    [Test]
    public static void TestCompileArchWithEnv()
    {
        string compileEnv = Environment.GetEnvironmentVariable("TEST_COMPILE") ?? string.Empty;
        if (string.IsNullOrEmpty(compileEnv))
        {
            Console.WriteLine("use TEST_COMPILE=[STORE|LOAD] to allow store/load in compile test");
        }

        bool store = true;
        TestCompileArch();
      
        if (store || compileEnv.Contains("LOAD"))
        {
            if (!TestCompileLoad())
            {
                throw new Exception("testCompileLoad failed (1)");
            }
            // ResetNativeTarget(); (optional)
            if (!TestCompileLoad())
            {
                throw new Exception("testCompileLoad failed (2)");
            }
        }
    }

    [Test]
    public static void TestCompileArch()
    {
        
        string localTarget = Utils.LocalTarget();
        bool nativeArm64 = localTarget == Utils.TargetArm64;
        bool nativeAmd64 = localTarget == Utils.TargetAmd64;

        string arm64TargetString = "arm64-linux-unknown+neon";
        string amd64TargetString = "x86_64-linux-unknown+sse4.2+lzcnt+bmi";

        RustBytes output = new RustBytes();

        Console.WriteLine($"starting test.. native arm? {nativeArm64} amd? {nativeAmd64} GOARCH/GOOS: {RuntimeInformation.ProcessArchitecture}/{Environment.OSVersion}");

        int status = Rust.stylus_target_set(
            CreateSlice(Utils.TargetArm64),
            CreateSlice(arm64TargetString),
            ref output,
            nativeArm64);
        if (status != 0)
        {
            Console.WriteLine($"failed setting compilation target arm: {ReadBytes(output).ToHexString()}");
            throw new Exception();
        }

        status = Rust.stylus_target_set(
            CreateSlice(Utils.TargetAmd64),
            CreateSlice(amd64TargetString),
            ref output,
            nativeAmd64);
        if (status != 0)
        {
            Console.WriteLine($"failed setting compilation target amd: {ReadBytes(output).ToHexString()}");
            throw new Exception();
        }

        byte[] wat = File.ReadAllBytes($"{NitroLocation}/arbitrator/stylus/tests/keccak/target/wasm32-unknown-unknown/release/keccak.wasm.wat");
        RustBytes wasmOutput = new RustBytes();
        int watStatus = Rust.wat_to_wasm(CreateSlice(wat), ref wasmOutput);
        if (watStatus != 0)
        {
            Console.WriteLine("Failed to compile WAT to WASM");
            throw new Exception();
        }

        byte[] wasm = ReadBytes(wasmOutput);

 

        Console.WriteLine("Storing compiled files to ../../target/testdata/");
        Directory.CreateDirectory($"{NitroLocation}/target/testdata");
        

        // Compile invalid target to check error handling
        status = Rust.stylus_compile(CreateSlice(wasm), 1, true, CreateSlice("booga"), ref output);
        if (status == 0)
        {
            Console.WriteLine($"Unexpected success for invalid arch: {ReadBytes(output).ToHexString()}");
            throw new Exception();
        }

        // Compile native
        status = Rust.stylus_compile(CreateSlice(wasm), 1, true, CreateSlice(""), ref output);
        if (status != 0)
        {
            Console.WriteLine($"Failed compiling native: {ReadBytes(output).ToHexString()}");
            throw new Exception();
        }
        File.WriteAllBytes($"{NitroLocation}/target/testdata/host.bin", ReadBytes(output));
    

        // Compile for arm64
        status = Rust.stylus_compile(CreateSlice(wasm), 1, true, CreateSlice(Utils.TargetArm64), ref output);
        if (status != 0)
        {
            Console.WriteLine($"Failed compiling arm64: {ReadBytes(output).ToHexString()}");
            throw new Exception();
        }
        File.WriteAllBytes($"{NitroLocation}/target/testdata/arm64.bin", ReadBytes(output));
        

        // Compile for amd64
        status = Rust.stylus_compile(CreateSlice(wasm), 1, true, CreateSlice(Utils.TargetAmd64), ref output);
        if (status != 0)
        {
            Console.WriteLine($"Failed compiling amd64: {ReadBytes(output).ToHexString()}");
            throw new Exception();
        }

        File.WriteAllBytes($"{NitroLocation}/target/testdata/amd64.bin", ReadBytes(output));
    }
    
    private static GoSliceData CreateSlice(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return new GoSliceData { ptr = ptr, len = (UIntPtr)bytes.Length };
    }

    private static GoSliceData CreateSlice(byte[] bytes)
    {
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return new GoSliceData { ptr = ptr, len = (UIntPtr)bytes.Length };
    }

    private static byte[] ReadBytes(RustBytes output)
    {
        byte[] buffer = new byte[(int)output.len];
        if (buffer.Length != 0) Marshal.Copy(output.ptr, buffer, 0, buffer.Length);
        return buffer;
    }
    
    [Test]
    public static void TestStylusCall()
    {
        // Load .wasm file (compiled using Rust FFI or Wat2Wasm)
        byte[] wasm = File.ReadAllBytes("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum.Test/wasm/keccak.wasm");
        IntPtr wasmPtr = Marshal.AllocHGlobal(wasm.Length);
        Marshal.Copy(wasm, 0, wasmPtr, wasm.Length);

        var module = new GoSliceData
        {
            ptr = wasmPtr,
            len = (UIntPtr)wasm.Length
        };

        // Prepare calldata (e.g., empty or test input)
        byte[] calldataBytes = Array.Empty<byte>();
        IntPtr calldataPtr = Marshal.AllocHGlobal(calldataBytes.Length);
        Marshal.Copy(calldataBytes, 0, calldataPtr, calldataBytes.Length);

        var calldata = new GoSliceData
        {
            ptr = calldataPtr,
            len = (UIntPtr)calldataBytes.Length
        };

        var config = new StylusConfig
        {
            version = 1,
            max_depth = 10000,
            pricing = new PricingParams { ink_price = 10000 }
        };

        var handler = RegisterHandler.CreateHandler((UIntPtr)1);

        var evmData = new EvmData
        {
            arbos_version = 40,
            block_basefee = new Bytes32 { bytes = new byte[32] },
            chainid = 42161,
            block_coinbase = new Bytes20 { bytes = new byte[20] },
            block_gas_limit = 30_000_000,
            block_number = 999,
            block_timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            contract_address = new Bytes20 { bytes = new byte[20] },
            module_hash = new Bytes32 { bytes = new byte[32] },
            msg_sender = new Bytes20 { bytes = new byte[20] },
            msg_value = new Bytes32 { bytes = new byte[32] },
            tx_gas_price = new Bytes32 { bytes = new byte[32] },
            tx_origin = new Bytes20 { bytes = new byte[20] },
            reentrant = 0,
            cached = false,
            tracing = false
        };

        RustBytes output = new RustBytes();
        ulong gas = 1_000_000;
        uint arbosTag = 0;

        Console.WriteLine("Calling stylus_call...");
        int result = Rust.stylus_call(
            module, calldata, config, handler, evmData, debug: true,
            ref output, ref gas, arbosTag);

        Console.WriteLine($"stylus_call returned {result}, gas left: {gas}");

        // Read result data from output.ptr
        byte[] resultData = new byte[(int)output.len];
        Marshal.Copy(output.ptr, resultData, 0, resultData.Length);
        string resultStr = System.Text.Encoding.UTF8.GetString(resultData);

        Console.WriteLine($"Program output: {resultStr}");

        // Free unmanaged memory
        Marshal.FreeHGlobal(wasmPtr);
        Marshal.FreeHGlobal(calldataPtr);
    }
}
