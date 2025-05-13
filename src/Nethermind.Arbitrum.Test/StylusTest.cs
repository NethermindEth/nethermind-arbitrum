using System.Runtime.InteropServices;
using System.Text;
using Nethermind.Arbitrum.Exceptions;
using Nethermind.Arbitrum.NativeHandler;
using Nethermind.Core.Crypto;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test;

public static class TestStylus
{

    private static byte[] TestCompileLoad()
    {
        var filePath = "host.bin";
        var localTarget = Utils.LocalTarget();
        filePath = localTarget switch
        {
            Utils.TargetArm64 => $"arm64.bin",
            Utils.TargetAmd64 => $"amd64.bin",
            _ => filePath
        };

        byte[] localAsm = File.ReadAllBytes(filePath);
        byte[] calldata = [8, 123, 123, 123, 123, 3];

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

        ulong gas = 0xfffffffffffffff;
        return Stylus.Call(localAsm, calldata, config, handler, evmData, true, 0, ref gas);
    }

    [Test]
    public static void TestCompileArchWithEnv()
    {
        TestCompileArch();
        TestCompileLoad();
    }
    
    
    [TestCase("wasm/bad-export.wat")]
    [TestCase("wasm/bad-export2.wat")]
    [TestCase("wasm/bad-export3.wat")]
    [TestCase("wasm/bad-import.wat")]
    public static void TestCompileFailOnBadModules(string module)
    {
        var localTarget = Utils.LocalTarget();
        var isNativeArm64 = localTarget == Utils.TargetArm64;
        var isNativeAmd64 = localTarget == Utils.TargetAmd64;

        
        Stylus.SetCompilationTarget(Utils.TargetArm64, Utils.Arm64TargetString, isNativeArm64);
        Stylus.SetCompilationTarget(Utils.TargetAmd64, Utils.Amd64TargetString, isNativeAmd64);
        
        byte[] wat = File.ReadAllBytes(module);
        byte[] wasm = Stylus.CompileWatToWasm(wat);
        
        // Compile invalid target to check error handling
        Assert.Throws<StylusCompilationFailedException>(() => Stylus.Compile(wasm, 1, true, "fail"));

        // Compile native
        Assert.Throws<StylusCompilationFailedException>(() =>  Stylus.Compile(wasm, 1, true, ""));
    }

    [Test]
    public static void TestCompileArch()
    {
        var localTarget = Utils.LocalTarget();
        var isNativeArm64 = localTarget == Utils.TargetArm64;
        var isNativeAmd64 = localTarget == Utils.TargetAmd64;

        
        Stylus.SetCompilationTarget(Utils.TargetArm64, Utils.Arm64TargetString, isNativeArm64);
        Stylus.SetCompilationTarget(Utils.TargetAmd64, Utils.Amd64TargetString, isNativeAmd64);
        
        byte[] wat = File.ReadAllBytes($"wasm/keccak.wasm.wat");
        byte[] wasm = Stylus.CompileWatToWasm(wat);
        
        // Compile invalid target to check error handling
        Assert.Throws<StylusCompilationFailedException>(() => Stylus.Compile(wasm, 1, true, "fail"));

        // Compile native
        byte[] compiledWasm = Stylus.Compile(wasm, 1, true, "");
        File.WriteAllBytes($"host.bin", compiledWasm);
        
        // Compile for arm64
        compiledWasm = Stylus.Compile(wasm, 1, true, Utils.TargetArm64);
        File.WriteAllBytes($"arm64.bin", compiledWasm);
        
        // Compile for amd64
        compiledWasm = Stylus.Compile(wasm, 1, true, Utils.TargetAmd64);
        File.WriteAllBytes($"amd64.bin", compiledWasm);
    }
    
    [Test]
    public static void TestStylusCall()
    {
        TestCompileArch();
        
        byte[] wasm = File.ReadAllBytes($"host.bin");
        
        // Prepare calldata
        const string preimage = "°º¤ø,¸,ø¤°º¤ø,¸,ø¤°º¤ø,¸ nyan nyan ~=[,,_,,]:3 nyan nyan";
        var hash = Keccak.Compute(preimage);

        var args = new List<byte> { 0x01 };
        args.AddRange(Encoding.UTF8.GetBytes(preimage));
        var callDataBytes = args.ToArray();

        var config = new StylusConfig
        {
            version = 0,
            max_depth = 10000,
            pricing = new PricingParams { ink_price = 10000 }
        };

        var apiImpl = new TestNativeImpl();
        var id = EvmApiRegistry.Register(apiImpl);

        NativeRequestHandler handler = RegisterHandler.CreateHandler(id);

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

        ulong gas = 1_000_000;
        uint arbosTag = 0;

        
        byte[] resultData = Stylus.Call(wasm, callDataBytes, config, handler, evmData, true, arbosTag, ref gas);

        Assert.That(gas, Is.EqualTo(999581));
        Assert.That(resultData, Is.EquivalentTo(hash.BytesToArray()));
    }
}
