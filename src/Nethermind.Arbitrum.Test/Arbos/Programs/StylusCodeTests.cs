// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Test.Arbos.Programs;

[TestFixture]
public class StylusCodeTests
{
    private const byte FragmentVersion = 0x01;
    private const byte RootVersion = 0x02;

    [Test]
    public void IsStylusFragment_OnFragmentBytecode_ReturnsTrue()
    {
        StylusCode.IsStylusProgramFragment(FragmentWithPayload(10)).Should().BeTrue();
    }

    [TestCaseSource(nameof(NonFragmentBytecodes))]
    public void IsStylusFragment_OnNonFragmentBytecode_ReturnsFalse(byte[] code)
    {
        StylusCode.IsStylusProgramFragment(code).Should().BeFalse();
    }

    [Test]
    public void IsStylusFragment_OnBareDiscriminantWithoutPayload_ReturnsFalse()
    {
        StylusCode.IsStylusProgramFragment([0xEF, 0xF0, FragmentVersion]).Should().BeFalse();
    }

    [Test]
    public void IsStylusRoot_OnRootBytecode_ReturnsTrue()
    {
        byte[] code = Concat(RootHeader(1, 0x1234), new byte[Address.Size]);

        StylusCode.IsStylusProgramRoot(code).Should().BeTrue();
    }

    [Test]
    public void IsStylusRoot_OnBareDiscriminant_ReturnsFalse()
    {
        StylusCode.IsStylusProgramRoot([0xEF, 0xF0, RootVersion]).Should().BeFalse();
    }

    [TestCaseSource(nameof(StylusComponentBytecodes))]
    public void IsStylusComponentPrefix_OnAnyStylusComponentAtV60_ReturnsTrue(byte[] code)
    {
        StylusCode.IsStylusComponentPrefix(code, ArbosVersion.Sixty).Should().BeTrue();
    }

    [Test]
    public void IsStylusComponentPrefix_OnPlainBytecodeAtV60_ReturnsFalse()
    {
        StylusCode.IsStylusComponentPrefix([0x60, 0x60, 0x60, 0x60, 0x60], ArbosVersion.Sixty).Should().BeFalse();
    }

    [Test]
    public void IsStylusComponentPrefix_OnFragmentAtV50_ReturnsFalse()
    {
        StylusCode.IsStylusComponentPrefix(FragmentWithPayload(8), ArbosVersion.Fifty).Should().BeFalse();
    }

    [TestCaseSource(nameof(DeployableStylusBytecodes))]
    public void IsStylusDeployableProgramPrefix_OnClassicOrRootAtV60_ReturnsTrue(byte[] code)
    {
        StylusCode.IsStylusDeployableProgramPrefix(code, ArbosVersion.Sixty).Should().BeTrue();
    }

    [Test]
    public void IsStylusDeployableProgramPrefix_OnFragmentAtV60_ReturnsFalse()
    {
        StylusCode.IsStylusDeployableProgramPrefix(FragmentWithPayload(8), ArbosVersion.Sixty).Should().BeFalse();
    }

    [Test]
    public void IsStylusDeployableProgramPrefix_OnRootAtV50_ReturnsFalse()
    {
        byte[] root = Concat(RootHeader(1, 100), new byte[Address.Size]);
        StylusCode.IsStylusDeployableProgramPrefix(root, ArbosVersion.Fifty).Should().BeFalse();
    }

    [Test]
    public void IsStylusDeployableProgramPrefix_OnClassicPreStylus_ReturnsFalse()
    {
        StylusCode.IsStylusDeployableProgramPrefix(ClassicWithDict(1), ArbosVersion.Twenty).Should().BeFalse();
    }

    [Test]
    public void StripStylusFragmentPrefix_OnFragmentBytecode_ReturnsCompressedPayload()
    {
        byte[] payload = new byte[100];
        for (int i = 0; i < payload.Length; i++)
            payload[i] = (byte)(i * 7);
        byte[] fragment = StylusCode.NewStylusFragmentPrefix(payload);

        StylusOperationResult<ReadOnlySpan<byte>> result = StylusCode.StripStylusFragmentPrefix(fragment);

        result.IsSuccess.Should().BeTrue();
        result.Value.SequenceEqual(payload).Should().BeTrue();
    }

    [Test]
    public void StripStylusFragmentPrefix_OnClassicProgram_Fails()
    {
        StylusOperationResult<ReadOnlySpan<byte>> result = StylusCode.StripStylusFragmentPrefix(ClassicWithDict(1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.InvalidByteCode);
        result.Error.Value.Message.Should().Be("Specified bytecode is not a Stylus program fragment");
    }

    [Test]
    public void StripStylusPrefix_OnNonStylusBytecode_FailsWithLowercaseNitroMessage()
    {
        StylusOperationResult<StylusBytes> result = StylusCode.StripStylusPrefix([0x60, 0x60, 0x60, 0x60]);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.InvalidByteCode);
        result.Error.Value.Message.Should().Be("Specified bytecode is not a Stylus program");
    }

    [Test]
    public void Parse_RootWithOneFragment_ReturnsAllFields()
    {
        Address addr = RandomAddress(7);
        byte[] root = StylusCode.NewStylusRootPrefix(dictionary: 1, decompressedLength: 0x1234, fragments: new[] { addr });

        StylusOperationResult<StylusRoot> result = StylusRoot.Parse(root);

        result.IsSuccess.Should().BeTrue();
        result.Value.DictionaryType.Should().Be(1);
        result.Value.DecompressedLength.Should().Be(0x1234u);
        result.Value.Fragments.Should().HaveCount(1);
        result.Value.Fragments[0].Should().Be(addr);
    }

    [Test]
    public void Parse_RootWithTwoFragments_ReturnsBothAddresses()
    {
        Address addr1 = RandomAddress(1);
        Address addr2 = RandomAddress(2);
        byte[] root = StylusCode.NewStylusRootPrefix(dictionary: 0, decompressedLength: 4096, fragments: new[] { addr1, addr2 });

        StylusOperationResult<StylusRoot> result = StylusRoot.Parse(root);

        result.IsSuccess.Should().BeTrue();
        result.Value.Fragments.Should().Equal(addr1, addr2);
    }

    [Test]
    public void Parse_TruncatedRootHeader_Fails()
    {
        byte[] code = [0xEF, 0xF0, RootVersion, 0x01, 0x00, 0x00, 0x00];

        StylusOperationResult<StylusRoot> result = StylusRoot.Parse(code);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.InvalidByteCode);
        result.Error.Value.Message.Should().Be($"Stylus program root too short: need at least 8 bytes, got {code.Length}");
    }

    [Test]
    public void Parse_NonAlignedAddressData_Fails()
    {
        byte[] code = Concat(RootHeader(1, 100), new byte[Address.Size + 1]);

        StylusOperationResult<StylusRoot> result = StylusRoot.Parse(code);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.InvalidByteCode);
        result.Error.Value.Message.Should().Be($"Stylus program root address data has invalid length: {Address.Size + 1} (must be multiple of {Address.Size})");
    }

    [Test]
    public void Parse_WithBadDictionaryAndBadAddressLength_FailsOnAddressLengthOnly()
    {
        // Documents the parse order: the address-length check fires before the dict byte is even
        // read, mirroring Nitro's NewStylusRoot (statedb_arbitrum.go:127-153). A future revert that
        // moved dict validation back into Parse would change which error fires first on this input
        // and break consensus-relevant gas-burn parity on the bad-dict path.
        byte[] code = Concat(RootHeader(dict: 0xFF, decompressedLen: 100), new byte[Address.Size + 1]);

        StylusOperationResult<StylusRoot> result = StylusRoot.Parse(code);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.InvalidByteCode);
        result.Error.Value.Message.Should().Be(
            $"Stylus program root address data has invalid length: {Address.Size + 1} (must be multiple of {Address.Size})");
    }

    [Test]
    public void Parse_UnknownDictionaryByte_ReturnsSuccessWithRawByte()
    {
        // Nitro parity (go-ethereum/core/state/statedb_arbitrum.go:127-153): Parse stores the raw
        // dictionary byte without validating against the supported set. Validation is deferred to
        // the post-fragment-read site in StylusPrograms so an invalid-dict root still pays
        // fragment-read gas before erroring — see ResolveStylusCompressionDict.
        byte[] code = Concat(RootHeader(0xFF, 100), new byte[Address.Size]);

        StylusOperationResult<StylusRoot> result = StylusRoot.Parse(code);

        result.IsSuccess.Should().BeTrue();
        result.Value.DictionaryType.Should().Be(0xFF);
    }

    [Test]
    public void Parse_NonRootBytecode_Fails()
    {
        StylusOperationResult<StylusRoot> result = StylusRoot.Parse(ClassicWithDict(1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Value.OperationResultType.Should().Be(StylusOperationResultType.InvalidByteCode);
        result.Error.Value.Message.Should().Be("Specified bytecode is not a Stylus program root");
    }

    [Test]
    public void NewStylusRootPrefix_RoundTrippedThroughParse_RecoversFields()
    {
        Address[] expected = [RandomAddress(11), RandomAddress(22)];
        const byte dict = 1;
        const uint decompressedLength = 42;

        byte[] root = StylusCode.NewStylusRootPrefix(dict, decompressedLength, expected);
        StylusOperationResult<StylusRoot> parsed = StylusRoot.Parse(root);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Value.DictionaryType.Should().Be(dict);
        parsed.Value.DecompressedLength.Should().Be(decompressedLength);
        parsed.Value.Fragments.Should().Equal(expected);
    }

    [Test]
    public void NewStylusFragmentPrefix_OnAnyPayload_StartsWithDiscriminant()
    {
        byte[] fragment = StylusCode.NewStylusFragmentPrefix([0xAA, 0xBB]);

        fragment.AsSpan(0, 3).ToArray().Should().Equal(new byte[] { 0xEF, 0xF0, FragmentVersion });
    }

    [Test]
    public void NewStylusFragmentPrefix_OnAnyPayload_AppendsPayload()
    {
        byte[] payload = [0x10, 0x20, 0x30, 0x40];

        byte[] fragment = StylusCode.NewStylusFragmentPrefix(payload);

        fragment.AsSpan(3).ToArray().Should().Equal(payload);
    }

    [Test]
    public void IsStylusProgram_OnClassicBytecode_ReturnsTrue()
    {
        StylusCode.IsStylusProgramClassic(ClassicWithDict(1)).Should().BeTrue();
    }

    [Test]
    public void IsStylusProgram_OnFragmentBytecode_ReturnsFalse()
    {
        StylusCode.IsStylusProgramClassic(FragmentWithPayload(8)).Should().BeFalse();
    }

    [Test]
    public void IsStylusProgram_OnRootBytecode_ReturnsFalse()
    {
        byte[] root = Concat(RootHeader(1, 100), new byte[Address.Size]);

        StylusCode.IsStylusProgramClassic(root).Should().BeFalse();
    }

    private static IEnumerable<TestCaseData> NonFragmentBytecodes()
    {
        yield return new TestCaseData(ClassicWithDict(1)).SetName("classic");
        yield return new TestCaseData(Concat(RootHeader(1, 100), new byte[Address.Size])).SetName("root");
        yield return new TestCaseData(new byte[] { 0x60, 0x60, 0x60, 0x60 }).SetName("plain bytecode");
        yield return new TestCaseData(Array.Empty<byte>()).SetName("empty");
    }

    private static IEnumerable<TestCaseData> StylusComponentBytecodes()
    {
        yield return new TestCaseData(ClassicWithDict(1)).SetName("classic");
        yield return new TestCaseData(Concat(RootHeader(1, 100), new byte[Address.Size])).SetName("root");
        yield return new TestCaseData(FragmentWithPayload(8)).SetName("fragment");
    }

    private static IEnumerable<TestCaseData> DeployableStylusBytecodes()
    {
        yield return new TestCaseData(ClassicWithDict(1)).SetName("classic");
        yield return new TestCaseData(Concat(RootHeader(1, 100), new byte[Address.Size])).SetName("root");
    }

    private static byte[] ClassicWithDict(byte dict, int payloadLen = 4)
    {
        return Concat([0xEF, 0xF0, 0x00, dict], new byte[payloadLen]);
    }

    private static byte[] FragmentWithPayload(int payloadLen)
    {
        return Concat([0xEF, 0xF0, FragmentVersion], new byte[payloadLen]);
    }

    private static byte[] RootHeader(byte dict, uint decompressedLen)
    {
        byte[] header = new byte[8];
        header[0] = 0xEF;
        header[1] = 0xF0;
        header[2] = RootVersion;
        header[3] = dict;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), decompressedLen);
        return header;
    }

    private static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        byte[] result = new byte[a.Length + b.Length];
        a.CopyTo(result);
        b.CopyTo(result.AsSpan(a.Length));
        return result;
    }

    private static Address RandomAddress(int seed)
    {
        byte[] bytes = new byte[Address.Size];
        for (int i = 0; i < Address.Size; i++)
            bytes[i] = (byte)(seed * 31 + i);
        return new Address(bytes);
    }
}
