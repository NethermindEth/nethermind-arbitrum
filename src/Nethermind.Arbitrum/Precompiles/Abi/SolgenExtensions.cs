// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Abi;

namespace Nethermind.Arbitrum.Precompiles.Abi;

/// <summary>
/// Converts pre-parsed descriptors from the Nethermind.Arbitrum.Precompiles package
/// into Nethermind.Abi types consumed by PrecompileAbiEncoder and EventsEncoder.
/// </summary>
public static class SolgenExtensions
{
    public static ArbitrumFunctionDescription ToArbitrumFunctionDescription(this Solgen.FunctionDescriptor descriptor) =>
        new(descriptor.ToAbiFunctionDescription());

    public static AbiFunctionDescription ToAbiFunctionDescription(this Solgen.FunctionDescriptor descriptor) => new()
    {
        Name = descriptor.Name,
        StateMutability = ParseStateMutability(descriptor.StateMutability),
        Inputs = ToAbiParameters(descriptor.Inputs),
        Outputs = ToAbiParameters(descriptor.Outputs),
    };

    public static AbiEventDescription ToAbiEventDescription(this Solgen.EventDescriptor descriptor) => new()
    {
        Name = descriptor.Name,
        Anonymous = descriptor.IsAnonymous,
        Inputs = ToAbiEventParameters(descriptor.Inputs),
    };

    public static AbiErrorDescription ToAbiErrorDescription(this Solgen.ErrorDescriptor descriptor) => new()
    {
        Name = descriptor.Name,
        Inputs = ToAbiParameters(descriptor.Inputs),
    };

    public static AbiType ToAbiType(this Solgen.AbiType type) => type switch
    {
        Solgen.AbiUIntType u => new AbiUInt(u.Size),
        Solgen.AbiIntType i => new AbiInt(i.Size),
        Solgen.AbiBytesType b => new AbiBytes(b.Size),
        Solgen.AbiDynamicBytesType => AbiType.DynamicBytes,
        Solgen.AbiStringType => AbiType.String,
        Solgen.AbiBoolType => AbiType.Bool,
        Solgen.AbiAddressType => AbiType.Address,
        Solgen.AbiArrayType a => new AbiArray(a.ElementType.ToAbiType()),
        Solgen.AbiFixedArrayType fa => new AbiFixedLengthArray(fa.ElementType.ToAbiType(), fa.Length),
        Solgen.AbiTupleType t => new AbiTuple([.. t.Elements.Select(ToAbiType)]),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported package AbiType: {type.GetType().Name}"),
    };

    private static AbiParameter[] ToAbiParameters(IReadOnlyList<Solgen.Parameter> parameters)
    {
        AbiParameter[] result = new AbiParameter[parameters.Count];
        for (int i = 0; i < parameters.Count; i++)
            result[i] = new AbiParameter { Name = parameters[i].Name, Type = parameters[i].Type.ToAbiType() };
        return result;
    }

    private static AbiEventParameter[] ToAbiEventParameters(IReadOnlyList<Solgen.EventParameter> parameters)
    {
        AbiEventParameter[] result = new AbiEventParameter[parameters.Count];
        for (int i = 0; i < parameters.Count; i++)
            result[i] = new AbiEventParameter
            {
                Name = parameters[i].Name,
                Type = parameters[i].Type.ToAbiType(),
                Indexed = parameters[i].IsIndexed,
            };
        return result;
    }

    private static StateMutability ParseStateMutability(string value) => value switch
    {
        "pure" => StateMutability.Pure,
        "view" => StateMutability.View,
        "nonpayable" => StateMutability.NonPayable,
        "payable" => StateMutability.Payable,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown state mutability: '{value}'"),
    };
}
