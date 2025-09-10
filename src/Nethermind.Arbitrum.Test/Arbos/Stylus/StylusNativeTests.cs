// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Core.Crypto;
using StylusNative = Nethermind.Arbitrum.Arbos.Stylus.StylusNative;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus;

[NonParallelizable]
public class StylusNativeTests
{
    private static readonly byte[] ValidWatBytes = "(module)"u8.ToArray();

    [TestCase("module", "expected `(`")]
    [TestCase("binary\0file\0\that\0is\0not\0wat", "unexpected character")]
    public void WatToWasm_HasInvalidWat_Fails(string wat, string errorPrefix)
    {
        StylusNativeResult<byte[]> expected = StylusNativeResult<byte[]>.Failure(UserOutcomeKind.Failure, errorPrefix);
        StylusNativeResult<byte[]> actual = StylusNative.WatToWasm(Encoding.UTF8.GetBytes(wat));

        actual.Should().BeEquivalentTo(expected, o => o.ForErrorResult());
    }

    [Test]
    public void WatToWasm_ValidWat_Succeeds()
    {
        StylusNativeResult<byte[]> expected = StylusNativeResult<byte[]>.Success([0x00, 0x61, 0x73, 0x6d, 0x01, 0x00, 0x00, 0x00]);
        StylusNativeResult<byte[]> actual = StylusNative.WatToWasm(ValidWatBytes);

        actual.Should().BeEquivalentTo(expected);
    }

    [TestCase("Arbos/Stylus/Resources/bad-export.wat")]
    [TestCase("Arbos/Stylus/Resources/bad-export2.wat")]
    [TestCase("Arbos/Stylus/Resources/bad-export3.wat")]
    [TestCase("Arbos/Stylus/Resources/bad-import.wat")]
    [TestCase("Arbos/Stylus/Resources/counter-contract.wat")]
    [TestCase("Arbos/Stylus/Resources/keccak.wasm.wat")]
    [TestCase("Arbos/Stylus/Resources/storage.wasm.wat")]
    public void WatToWasm_TestResources_Succeeds(string resource)
    {
        byte[] wat = File.ReadAllBytes(resource);
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);

        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);
    }

    [Test]
    public void SetTarget_UnknownTarget_Fails()
    {
        string randomTarget = Guid.NewGuid().ToString();

        StylusNativeResult<byte[]> expected = StylusNativeResult<byte[]>.Failure(UserOutcomeKind.Failure, "Unrecognized architecture");
        StylusNativeResult<byte[]> actual = StylusNative.SetTarget(randomTarget, randomTarget, false);

        actual.Should().BeEquivalentTo(expected, o => o.ForErrorResult());
    }

    [TestCase(StylusTargets.HostDescriptor)]
    [TestCase(StylusTargets.LinuxX64Descriptor)]
    [TestCase(StylusTargets.LinuxArm64Descriptor)]
    [TestCase(StylusTargets.MacOsX64Descriptor)]
    [TestCase(StylusTargets.MacOsArm64Descriptor)]
    [TestCase(StylusTargets.WindowsGnuX64Descriptor)]
    public void SetTarget_TargetIsKnown_Succeeds(string descriptor)
    {
        string randomName = Guid.NewGuid().ToString(); // Is not relevant for the test

        StylusNativeResult<byte[]> setTargetNativeResult = StylusNative.SetTarget(randomName, descriptor, false);

        setTargetNativeResult.Status.Should().Be(UserOutcomeKind.Success);
    }

    [Test]
    public void Activate_InvalidWasm_Fails()
    {
        ulong gas = 1_000_000;
        StylusNativeResult<ActivateResult> expected = StylusNativeResult<ActivateResult>.Failure(UserOutcomeKind.Failure, "failed to parse wasm");
        StylusNativeResult<ActivateResult> activateNativeResult = StylusNative.Activate("\0c#"u8.ToArray(), 100, 1, 40,
            true, new Bytes32(), ref gas);

        activateNativeResult.Should().BeEquivalentTo(expected, o => o.ForErrorResult());
    }

    [TestCase("Arbos/Stylus/Resources/limits.memory-2.wat", "multiple memories")]
    [TestCase("Arbos/Stylus/Resources/limits.page-limit-17.wat", "memory exceeds limit")]
    public void Activate_FacesLimits_Fails(string resource, string error)
    {
        byte[] wat = File.ReadAllBytes(resource);
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        Bytes32 codeHash = new(KeccakHash.ComputeHashBytes(wasmNativeResult.Value!));

        ulong gas = 1_000_000;
        StylusNativeResult<ActivateResult> activateNativeResult = StylusNative.Activate(wasmNativeResult.Value!, 16, 1, 40, true,
            codeHash, ref gas);

        activateNativeResult.Status.Should().Be(UserOutcomeKind.Failure);
        activateNativeResult.Error.Should().Contain(error);
    }

    [Test]
    public void Activate_ArbosVersionForGasIsZero_DoesntConsumeGas()
    {
        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        Bytes32 codeHash = new(KeccakHash.ComputeHashBytes(wasmNativeResult.Value!));

        ulong expectedGas = 1_000_000;
        ulong actualGas = expectedGas;
        StylusNativeResult<ActivateResult> activateNativeResult = StylusNative.Activate(wasmNativeResult.Value!, 100, 1, 0, true,
            codeHash, ref actualGas);

        activateNativeResult.Status.Should().Be(UserOutcomeKind.Success);
        actualGas.Should().Be(expectedGas);
    }

    [Test]
    public void Activate_ValidContract_BuildsWavmModuleAndProvides()
    {
        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        Bytes32 codeHash = new(KeccakHash.ComputeHashBytes(wasmNativeResult.Value!));
        ulong gas = 1_000_000;

        StylusNativeResult<ActivateResult> activateNativeResult = StylusNative.Activate(wasmNativeResult.Value!, 100, 1, 40, true,
            codeHash, ref gas);

        activateNativeResult.Status.Should().Be(UserOutcomeKind.Success);
        activateNativeResult.Value.ModuleHash.ToArray().Any(b => b != 0).Should().BeTrue();
        activateNativeResult.Value.ActivationInfo.Should().BeEquivalentTo(new StylusData
        {
            AsmEstimate = 6,
            CachedInitCost = 0,
            Footprint = 4,
            InitCost = 3
        });
    }

    [Test]
    public void Compile_TargetIsUnknown_Fails()
    {
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(ValidWatBytes);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> expected = StylusNativeResult<byte[]>.Failure(UserOutcomeKind.Failure, "arch not set");
        StylusNativeResult<byte[]> actual = StylusNative.Compile(wasmNativeResult.Value!, 1, true, "random");

        actual.Should().BeEquivalentTo(expected, o => o.ForErrorResult());
    }

    [Test]
    public void Compile_TargetIsHost_Succeeds()
    {
        StylusNativeResult<byte[]> setNativeResult = StylusNative.SetTarget(StylusTargets.HostTargetName, StylusTargets.HostDescriptor, true);
        setNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> compileNativeResult = StylusNative.Compile(wasmNativeResult.Value!, 1, true, StylusTargets.HostTargetName);

        compileNativeResult.Status.Should().Be(UserOutcomeKind.Success);
    }

    [Test]
    public void Compile_TargetIsSet_Succeeds()
    {
        string targetName = Guid.NewGuid().ToString();
        string targetDescriptor = StylusTargets.GetLocalDescriptor();
        StylusNativeResult<byte[]> setNativeResult = StylusNative.SetTarget(targetName, targetDescriptor, false);
        setNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> compileNativeResult = StylusNative.Compile(wasmNativeResult.Value!, 1, true, targetName);

        compileNativeResult.Status.Should().Be(UserOutcomeKind.Success);
    }

    [TestCase("Arbos/Stylus/Resources/bad-import.wat")]
    [TestCase("Arbos/Stylus/Resources/bad-export.wat")]
    [TestCase("Arbos/Stylus/Resources/bad-export2.wat")]
    [TestCase("Arbos/Stylus/Resources/bad-export3.wat")]
    public void Compile_InvalidWebAssemblyModule_Fails(string module)
    {
        byte[] wat = File.ReadAllBytes(module);
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> expected = StylusNativeResult<byte[]>.Failure(UserOutcomeKind.Failure, "WebAssembly translation error");
        StylusNativeResult<byte[]> actual = StylusNative.Compile(wasmNativeResult.Value!, 1, true, StylusTargets.HostDescriptor);

        actual.Should().BeEquivalentTo(expected, o => o.ForErrorResult());
    }

    [TestCase("Arbos/Stylus/Resources/counter-contract.wat")]
    [TestCase("Arbos/Stylus/Resources/keccak.wasm.wat")]
    [TestCase("Arbos/Stylus/Resources/storage.wasm.wat")]
    public void Compile_ValidWebAssemblyModule_Succeeds(string module)
    {
        byte[] wat = File.ReadAllBytes(module);
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> compileNativeResult = StylusNative.Compile(wasmNativeResult.Value!, 1, true, StylusTargets.HostDescriptor);

        compileNativeResult.Status.Should().Be(UserOutcomeKind.Success);
    }

    [Test]
    public static void Call_CounterContractSetsValue_UpdatesStorageThroughNativeApi()
    {
        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        string targetName = Guid.NewGuid().ToString();
        string targetDescriptor = StylusTargets.GetLocalDescriptor();
        StylusNativeResult<byte[]> setNativeResult = StylusNative.SetTarget(targetName, targetDescriptor, false);
        setNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> asmNativeResult = StylusNative.Compile(wasmNativeResult.Value!, 1, true, targetName);
        asmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusConfig config = GetDefaultStylusConfig();
        EvmData evmData = GetDefaultEvmData(asmNativeResult);
        using TestStylusEvmApi apiApi = new();

        ulong gas = 1_000_000;
        uint arbosTag = 0;

        // Get number (should be 0 initially)
        byte[] getNumberCalldata = CounterContractCallData.GetNumberCalldata();
        StylusNativeResult<byte[]> getNumberResult1 = StylusNative.Call(asmNativeResult.Value!, getNumberCalldata, config, apiApi, evmData, true, arbosTag, ref gas);
        getNumberResult1.Value.Should().BeEquivalentTo(new byte[32]);

        // Set number to 9
        byte[] setNumberCalldata = CounterContractCallData.GetSetNumberCalldata(9);
        StylusNativeResult<byte[]> setNumberNativeResult = StylusNative.Call(asmNativeResult.Value!, setNumberCalldata, config, apiApi, evmData, true, arbosTag, ref gas);
        setNumberNativeResult.Value.Should().BeEmpty();

        // Get number again (should now be 9)
        StylusNativeResult<byte[]> getNumberResult2 = StylusNative.Call(asmNativeResult.Value!, getNumberCalldata, config, apiApi, evmData, true, arbosTag, ref gas);

        byte[] expected = new byte[32];
        expected[^1] = 9; // Last byte should be 9 after setNumber(9)

        getNumberResult2.Value.Should().BeEquivalentTo(expected);
    }

    [Test]
    public static void Call_CounterContractIncrement_EmitsLogsAndUpdatesStorageThroughNativeApi()
    {
        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/counter-contract.wat");
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        string targetName = Guid.NewGuid().ToString();
        string targetDescriptor = StylusTargets.GetLocalDescriptor();
        StylusNativeResult<byte[]> setNativeResult = StylusNative.SetTarget(targetName, targetDescriptor, false);
        setNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> asmNativeResult = StylusNative.Compile(wasmNativeResult.Value!, 1, true, targetName);
        asmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusConfig config = GetDefaultStylusConfig();
        EvmData evmData = GetDefaultEvmData(asmNativeResult);
        using TestStylusEvmApi apiApi = new();

        ulong gas = 1_000_000;
        uint arbosTag = 0;

        // Get number (should be 0 initially)
        byte[] getNumberCalldata = CounterContractCallData.GetNumberCalldata();
        StylusNativeResult<byte[]> getNumberResult1 = StylusNative.Call(asmNativeResult.Value!, getNumberCalldata, config, apiApi, evmData, true, arbosTag, ref gas);
        getNumberResult1.Value.Should().BeEquivalentTo(new byte[32]);

        // Increment number from 0 to 1
        byte[] incrementNumberCalldata = CounterContractCallData.GetIncrementCalldata();
        StylusNativeResult<byte[]> incrementNumberNativeResult = StylusNative.Call(asmNativeResult.Value!, incrementNumberCalldata, config, apiApi, evmData, true, arbosTag, ref gas);
        incrementNumberNativeResult.IsSuccess.Should().BeTrue();

        // Get number again (should now be 1)
        StylusNativeResult<byte[]> getNumberResult2 = StylusNative.Call(asmNativeResult.Value!, getNumberCalldata, config, apiApi, evmData, true, arbosTag, ref gas);

        byte[] expected = new byte[32];
        expected[^1] = 1;

        getNumberResult2.Value.Should().BeEquivalentTo(expected);
    }

    [Test]
    public static void Call_KeccakCalculation_ReturnsValidHash()
    {
        // Keccak contract is a simple implementation that computes the Keccak hash of a given input..
        byte[] wat = File.ReadAllBytes("Arbos/Stylus/Resources/keccak.wasm.wat");
        StylusNativeResult<byte[]> wasmNativeResult = StylusNative.WatToWasm(wat);
        wasmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        string targetName = Guid.NewGuid().ToString();
        string targetDescriptor = StylusTargets.GetLocalDescriptor();
        StylusNativeResult<byte[]> setNativeResult = StylusNative.SetTarget(targetName, targetDescriptor, false);
        setNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        StylusNativeResult<byte[]> asmNativeResult = StylusNative.Compile(wasmNativeResult.Value!, 1, true, targetName);
        asmNativeResult.Status.Should().Be(UserOutcomeKind.Success);

        // Prepare calldata
        const string preimage = "°º¤ø,¸,ø¤°º¤ø,¸,ø¤°º¤ø,¸ nyan nyan ~=[,,_,,]:3 nyan nyan";
        byte[] hash = KeccakHash.ComputeHashBytes(Encoding.UTF8.GetBytes(preimage));

        List<byte> args = [0x01]; // Number of Keccak loops (just the keccak.wasm.wat logic)
        args.AddRange(Encoding.UTF8.GetBytes(preimage));
        byte[] callDataBytes = args.ToArray();

        StylusConfig config = GetDefaultStylusConfig();
        EvmData evmData = GetDefaultEvmData(asmNativeResult);
        using TestStylusEvmApi apiApi = new();

        ulong gas = 1_000_000;
        uint arbosTag = 0;

        StylusNativeResult<byte[]> nativeResultData = StylusNative.Call(asmNativeResult.Value!, callDataBytes, config, apiApi, evmData, true, arbosTag, ref gas);

        nativeResultData.Value.Should().BeEquivalentTo(hash);
    }

    [Test]
    public void Compress_OutputSizeIsTooSmall_Fails()
    {
        byte[] input = RandomNumberGenerator.GetBytes(128);
        byte[] compressed = new byte[10];

        BrotliStatus status = StylusNative.BrotliCompress(input, compressed, 11, BrotliDictionary.Empty, out int compressedSize);

        status.Should().Be(BrotliStatus.Failure);
        compressedSize.Should().Be(compressed.Length);
    }

    [Test]
    public void Decompress_OutputSizeIsTooSmall_Fails()
    {
        byte[] input = RandomNumberGenerator.GetBytes(128);
        int maxSize = StylusNative.GetCompressedBufferSize(input.Length);
        byte[] compressed = new byte[maxSize];
        byte[] decompressed = new byte[10];

        BrotliStatus compressedStatus = StylusNative.BrotliCompress(input, compressed, 11, BrotliDictionary.Empty, out int compressedSize);
        compressedStatus.Should().Be(BrotliStatus.Success);

        BrotliStatus decompressedStatus = StylusNative.BrotliDecompress(compressed[..compressedSize], decompressed,
            BrotliDictionary.Empty, out int decompressedSize);

        decompressedStatus.Should().Be(BrotliStatus.Failure);
        decompressedSize.Should().Be(decompressed.Length);
    }

    [Test]
    public void Decompress_InvalidInput_Fails()
    {
        byte[] input = Enumerable.Repeat(0, 128).Select(i => (byte)i).ToArray();
        int maxSize = StylusNative.GetCompressedBufferSize(input.Length);
        byte[] output = new byte[maxSize];

        BrotliStatus decompressedResult = StylusNative.BrotliDecompress(input, output, BrotliDictionary.Empty, out int decompressedSize);

        decompressedResult.Should().Be(BrotliStatus.Failure);
    }

    [TestCase(BrotliDictionary.Empty)]
    [TestCase(BrotliDictionary.StylusProgram)]
    public void CompressDecompress_Always_DecompressesToOriginal(BrotliDictionary dictionary)
    {
        byte[] input = RandomNumberGenerator.GetBytes(128);
        int maxSize = StylusNative.GetCompressedBufferSize(input.Length);
        byte[] compressed = new byte[maxSize];
        byte[] decompressed = new byte[maxSize];

        BrotliStatus compressedStatus = StylusNative.BrotliCompress(input, compressed, 11, dictionary, out int compressedSize);
        compressedStatus.Should().Be(BrotliStatus.Success);

        BrotliStatus decompressedStatus = StylusNative.BrotliDecompress(compressed[..compressedSize], decompressed, dictionary, out int decompressedSize);
        decompressedStatus.Should().Be(BrotliStatus.Success);

        decompressed[..decompressedSize].Should().BeEquivalentTo(input);
    }

    private static EvmData GetDefaultEvmData(StylusNativeResult<byte[]> asmNativeResult)
    {
        return new()
        {
            ArbosVersion = 40,
            BlockBaseFee = new(),
            ChainId = 42161,
            BlockCoinbase = new(),
            BlockGasLimit = 30_000_000,
            BlockNumber = 999,
            BlockTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ContractAddress = new(),
            ModuleHash = new(KeccakHash.ComputeHashBytes(asmNativeResult.Value!)),
            MsgSender = new(),
            MsgValue = new(),
            TxGasPrice = new(),
            TxOrigin = new(),
            Reentrant = 0,
            Cached = false,
            Tracing = true
        };
    }

    private static StylusConfig GetDefaultStylusConfig()
    {
        return new()
        {
            Version = 0,
            MaxDepth = 10000,
            Pricing = new PricingParams { InkPrice = 10000 }
        };
    }
}
