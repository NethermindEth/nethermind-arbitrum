// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbitrumCodeInfoRepository(ICodeInfoRepository codeInfoRepository) : ICodeInfoRepository
{
    private readonly Dictionary<Address, ICodeInfo> _arbitrumPrecompiles = InitializePrecompiledContracts();

    private static Dictionary<Address, ICodeInfo> InitializePrecompiledContracts()
    {
        return new Dictionary<Address, ICodeInfo>
        {
            [ArbInfoParser.Address] = new PrecompileInfo(ArbInfoParser.Instance),
            [ArbRetryableTxParser.Address] = new PrecompileInfo(ArbRetryableTxParser.Instance),
            [ArbOwnerParser.Address] = new PrecompileInfo(ArbOwnerParser.Instance),
            [ArbSysParser.Address] = new PrecompileInfo(ArbSysParser.Instance),
            [ArbAddressTableParser.Address] = new PrecompileInfo(ArbAddressTableParser.Instance),
            [ArbWasmParser.Address] = new PrecompileInfo(ArbWasmParser.Instance),
            [ArbGasInfoParser.Address] = new PrecompileInfo(ArbGasInfoParser.Instance),
            [ArbAggregatorParser.Address] = new PrecompileInfo(ArbAggregatorParser.Instance),
            [ArbActsParser.Address] = new PrecompileInfo(ArbActsParser.Instance)
        };
    }

    public ICodeInfo GetCachedCodeInfo(Address codeSource, bool followDelegation, IReleaseSpec vmSpec, out Address? delegationAddress)
    {
        delegationAddress = null;

        if (_arbitrumPrecompiles.TryGetValue(codeSource, out ICodeInfo arbResult))
            return arbResult;

        return codeInfoRepository.GetCachedCodeInfo(codeSource, followDelegation, vmSpec, out delegationAddress);
    }

    public ValueHash256 GetExecutableCodeHash(Address address, IReleaseSpec spec) =>
        codeInfoRepository.GetExecutableCodeHash(address, spec);

    public void InsertCode(ReadOnlyMemory<byte> code, Address codeOwner, IReleaseSpec spec) =>
        codeInfoRepository.InsertCode(code, codeOwner, spec);

    public void SetDelegation(Address codeSource, Address authority, IReleaseSpec spec) =>
        codeInfoRepository.SetDelegation(codeSource, authority, spec);

    public bool TryGetDelegation(Address address, IReleaseSpec vmSpec, [NotNullWhen(true)] out Address? delegatedAddress) =>
        codeInfoRepository.TryGetDelegation(address, vmSpec, out delegatedAddress);
}
