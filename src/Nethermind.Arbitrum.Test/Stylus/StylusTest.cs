using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nethermind.Arbitrum.Exceptions;
using Nethermind.Arbitrum.NativeHandler;
using Nethermind.Core.Crypto;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test;

[Parallelizable(ParallelScope.None)]
public static class TestStylus
{
    [Test]
    public static void TestCompileArchWithEnv()
    {
        TestCompileArch("Stylus/wasm/keccak.wasm.wat");
        TestCompileLoad();
    }


    [TestCase("Stylus/wasm/bad-export.wat")]
    [TestCase("Stylus/wasm/bad-export2.wat")]
    [TestCase("Stylus/wasm/bad-export3.wat")]
    [TestCase("Stylus/wasm/bad-import.wat")]
    public static void TestCompileFailOnBadModules(string module)
    {
        var localTarget = Utils.LocalTarget();
        var isNativeArm64 = localTarget == Utils.TargetArm64;
        var isNativeAmd64 = localTarget == Utils.TargetAmd64;


        Stylus.SetCompilationTarget(Utils.TargetArm64, Utils.Arm64TargetString, isNativeArm64);
        Stylus.SetCompilationTarget(Utils.TargetAmd64, Utils.Amd64TargetString, isNativeAmd64);

        var wat = File.ReadAllBytes(module);
        var wasm = Stylus.CompileWatToWasm(wat);

        // Compile invalid target to check error handling
        Assert.Throws<StylusCompilationFailedException>(() => Stylus.Compile(wasm, 1, true, "fail"));

        // Compile native
        Assert.Throws<StylusCompilationFailedException>(() => Stylus.Compile(wasm, 1, true, ""));
    }

    [TestCase("Stylus/wasm/keccak.wasm.wat")]
    [TestCase("Stylus/wasm/storage.wasm.wat")]
    public static void TestCompileArch(string wasmFile)
    {
        var localTarget = Utils.LocalTarget();
        var isNativeArm64 = localTarget == Utils.TargetArm64;
        var isNativeAmd64 = localTarget == Utils.TargetAmd64;


        Stylus.SetCompilationTarget(Utils.TargetArm64, Utils.Arm64TargetString, isNativeArm64);
        Stylus.SetCompilationTarget(Utils.TargetAmd64, Utils.Amd64TargetString, isNativeAmd64);

        var wat = File.ReadAllBytes(wasmFile);
        var wasm = Stylus.CompileWatToWasm(wat);

        // Compile invalid target to check error handling
        Assert.Throws<StylusCompilationFailedException>(() => Stylus.Compile(wasm, 1, true, "fail"));

        // Compile native
        var compiledWasm = Stylus.Compile(wasm, 1, true, "");
        File.WriteAllBytes("host.bin", compiledWasm);

        // Compile for arm64
        compiledWasm = Stylus.Compile(wasm, 1, true, Utils.TargetArm64);
        File.WriteAllBytes("arm64.bin", compiledWasm);

        // Compile for amd64
        compiledWasm = Stylus.Compile(wasm, 1, true, Utils.TargetAmd64);
        File.WriteAllBytes("amd64.bin", compiledWasm);
    }

    private static void Compile(string wasmFile, string outFile)
    {
        var wat = File.ReadAllBytes(wasmFile);
        var wasm = Stylus.CompileWatToWasm(wat);

        // Compile native
        var compiledWasm = Stylus.Compile(wasm, 1, true, "");
        File.WriteAllBytes(outFile, compiledWasm);
    }

    [Test]
    public static void TestStylusCall()
    {
        Compile("Stylus/wasm/keccak.wasm.wat", "keccak.bin");

        var wasm = File.ReadAllBytes("keccak.bin");

        // Prepare calldata
        const string preimage = "°º¤ø,¸,ø¤°º¤ø,¸,ø¤°º¤ø,¸ nyan nyan ~=[,,_,,]:3 nyan nyan";
        var hash = Keccak.Compute(preimage);

        var args = new List<byte> { 0x01 };
        args.AddRange(Encoding.UTF8.GetBytes(preimage));
        var callDataBytes = args.ToArray();

        var config = new StylusConfig
        {
            Version = 0,
            MaxDepth = 10000,
            Pricing = new PricingParams { InkPrice = 10000 }
        };

        var evmData = new EvmData
        {
            ArbosVersion = 40,
            BlockBasefee = new Bytes32 { Bytes = new byte[32] },
            Chainid = 42161,
            BlockCoinbase = new Bytes20 { Bytes = new byte[20] },
            BlockGasLimit = 30_000_000,
            BlockNumber = 999,
            BlockTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ContractAddress = new Bytes20 { Bytes = new byte[20] },
            ModuleHash = new Bytes32 { Bytes = new byte[32] },
            MsgSender = new Bytes20 { Bytes = new byte[20] },
            MsgValue = new Bytes32 { Bytes = new byte[32] },
            TxGasPrice = new Bytes32 { Bytes = new byte[32] },
            TxOrigin = new Bytes20 { Bytes = new byte[20] },
            Reentrant = 0,
            Cached = false,
            Tracing = false
        };

        var apiImpl = new TestNativeImpl(new Bytes20(), evmData);
        var id = EvmApiRegistry.Register(apiImpl);

        var handler = RegisterHandler.Create(id);

        ulong gas = 1_000_000;
        uint arbosTag = 0;


        var resultData = Stylus.Call(wasm, callDataBytes, config, handler, evmData, true, arbosTag, ref gas);

        Assert.That(resultData, Is.EquivalentTo(hash.BytesToArray()));
    }


    [Test]
    public static void TestStylusCallV2()
    {
        Compile("Stylus/wasm/storage.wasm.wat", "storage.bin");

        var wasm = File.ReadAllBytes("storage.bin");

        var filename = "tests/storage/target/wasm32-unknown-unknown/release/storage.wasm";

        var key = Keccak.Compute(filename);
        var value = Keccak.Compute("value");

        var args = new List<byte> { 0x01 };
        args.AddRange(key.BytesToArray());
        args.AddRange(value.BytesToArray());
        var callDataBytes = args.ToArray();

        var config = new StylusConfig
        {
            Version = 0,
            MaxDepth = 10000,
            Pricing = new PricingParams { InkPrice = 10000 }
        };

        var evmData = new EvmData
        {
            ArbosVersion = 40,
            BlockBasefee = new Bytes32 { Bytes = new byte[32] },
            Chainid = 42161,
            BlockCoinbase = new Bytes20 { Bytes = new byte[20] },
            BlockGasLimit = 30_000_000,
            BlockNumber = 999,
            BlockTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ContractAddress = new Bytes20 { Bytes = new byte[20] },
            ModuleHash = new Bytes32 { Bytes = new byte[32] },
            MsgSender = new Bytes20 { Bytes = new byte[20] },
            MsgValue = new Bytes32 { Bytes = new byte[32] },
            TxGasPrice = new Bytes32 { Bytes = new byte[32] },
            TxOrigin = new Bytes20 { Bytes = new byte[20] },
            Reentrant = 0,
            Cached = false,
            Tracing = false
        };

        var apiImpl = new TestNativeImpl(new Bytes20(), evmData);
        var id = EvmApiRegistry.Register(apiImpl);

        var handler = RegisterHandler.Create(id);

        ulong gas = 1_000_000;
        uint arbosTag = 0;

        Stylus.Call(wasm, callDataBytes, config, handler, evmData, true, arbosTag, ref gas);

        var loadArgs = new List<byte> { 0x00 };
        loadArgs.AddRange(key.BytesToArray());
        var callDataBytesLoad = loadArgs.ToArray();
        var resultData = Stylus.Call(wasm, callDataBytesLoad, config, handler, evmData, true, arbosTag, ref gas);

        Assert.That(resultData, Is.EquivalentTo(value.BytesToArray()));
    }

    private static byte[] TestCompileLoad()
    {
        var filePath = "host.bin";
        var localTarget = Utils.LocalTarget();
        filePath = localTarget switch
        {
            Utils.TargetArm64 => "arm64.bin",
            Utils.TargetAmd64 => "amd64.bin",
            _ => filePath
        };

        var localAsm = File.ReadAllBytes(filePath);
        byte[] calldata = [8, 123, 123, 123, 123, 3];

        var config = new StylusConfig
        {
            Version = 1,
            MaxDepth = 10000,
            Pricing = new PricingParams { InkPrice = 1 }
        };

        var evmData = new EvmData(); // Simplified for now

        var apiImpl = new TestNativeImpl(new Bytes20(), evmData);
        var id = EvmApiRegistry.Register(apiImpl);

        var handler = RegisterHandler.Create(id);

        ulong gas = 0xfffffffffffffff;
        return Stylus.Call(localAsm, calldata, config, handler, evmData, true, 0, ref gas);
    }
}