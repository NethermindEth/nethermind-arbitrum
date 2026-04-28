// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;

namespace Nethermind.Arbitrum.Precompiles;

public enum ArbosActsMethod
{
    StartBlock,
    BatchPostingReport,
    BatchPostingReportV2
}

/// <summary>
/// ABI helpers for internal ArbOS system transactions (startBlock, batchPostingReport, batchPostingReportV2).
/// Unrelated to user-facing precompile ABIs, which are sourced from the Nethermind.Arbitrum.Precompiles package.
/// </summary>
public static class ArbosActsCodec
{
    private static readonly FrozenDictionary<ArbosActsMethod, AbiFunctionDescription> Functions = new Dictionary<ArbosActsMethod, AbiFunctionDescription>
    {
        [ArbosActsMethod.StartBlock] = Solgen.ArbosActs.Functions.All[Solgen.ArbosActs.Methods.StartBlock].ToAbiFunctionDescription(),
        [ArbosActsMethod.BatchPostingReport] = Solgen.ArbosActs.Functions.All[Solgen.ArbosActs.Methods.BatchPostingReport].ToAbiFunctionDescription(),
        [ArbosActsMethod.BatchPostingReportV2] = Solgen.ArbosActs.Functions.All[Solgen.ArbosActs.Methods.BatchPostingReportV2].ToAbiFunctionDescription(),
    }.ToFrozenDictionary();

    public static byte[] StartBlockMethodId { get; } = PackBigEndian(Solgen.ArbosActs.Methods.StartBlock);
    public static byte[] BatchPostingReportMethodId { get; } = PackBigEndian(Solgen.ArbosActs.Methods.BatchPostingReport);
    public static byte[] BatchPostingReportV2MethodId { get; } = PackBigEndian(Solgen.ArbosActs.Methods.BatchPostingReportV2);

    public static byte[] PackInput(ArbosActsMethod method, params object[] arguments)
    {
        if (!Functions.TryGetValue(method, out AbiFunctionDescription? fn))
            throw new ArgumentException($"Unknown ArbosActs method '{method}'", nameof(method));

        return AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, fn.GetCallInfo().Signature, arguments);
    }

    public static Dictionary<string, object> UnpackInput(ArbosActsMethod method, byte[] rawData)
    {
        if (rawData.Length <= 4)
            throw new ArgumentException($"Input data too short for '{method}': got {rawData.Length} bytes, expected > 4");

        if (!Functions.TryGetValue(method, out AbiFunctionDescription? fn))
            throw new ArgumentException($"Unknown ArbosActs method '{method}'", nameof(method));

        AbiSignature signature = fn.GetCallInfo().Signature;
        object[] arguments = AbiEncoder.Instance.Decode(AbiEncodingStyle.None, signature, rawData[4..]);

        Dictionary<string, object> result = [];
        for (int i = 0; i < fn.Inputs.Length; i++)
            result[fn.Inputs[i].Name] = arguments[i];

        return result;
    }

    private static byte[] PackBigEndian(uint methodId)
    {
        byte[] buffer = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, methodId);
        return buffer;
    }
}
