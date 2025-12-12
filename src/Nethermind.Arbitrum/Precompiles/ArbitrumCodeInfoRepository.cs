// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbitrumCodeInfoRepository(ICodeInfoRepository codeInfoRepository, IArbosVersionProvider arbosVersionProvider) : ICodeInfoRepository
{
    private readonly Dictionary<Address, ICodeInfo> _arbitrumPrecompiles = InitializePrecompiledContracts();

    private static Dictionary<Address, ICodeInfo> InitializePrecompiledContracts()
    {
        return new Dictionary<Address, ICodeInfo>
        {
            [ArbInfoParser.Address] = new PrecompileInfo(ArbInfoParser.Instance),
            [ArbRetryableTxParser.Address] = new PrecompileInfo(ArbRetryableTxParser.Instance),
            [ArbOwnerParser.Address] = new PrecompileInfo(ArbOwnerParser.Instance),
            [ArbOwnerPublicParser.Address] = new PrecompileInfo(ArbOwnerPublicParser.Instance),
            [ArbSysParser.Address] = new PrecompileInfo(ArbSysParser.Instance),
            [ArbAddressTableParser.Address] = new PrecompileInfo(ArbAddressTableParser.Instance),
            [ArbWasmParser.Address] = new PrecompileInfo(ArbWasmParser.Instance),
            [ArbGasInfoParser.Address] = new PrecompileInfo(ArbGasInfoParser.Instance),
            [ArbAggregatorParser.Address] = new PrecompileInfo(ArbAggregatorParser.Instance),
            [ArbActsParser.Address] = new PrecompileInfo(ArbActsParser.Instance),
            [ArbFunctionTableParser.Address] = new PrecompileInfo(ArbFunctionTableParser.Instance),
            [ArbTestParser.Address] = new PrecompileInfo(ArbTestParser.Instance),
            [ArbStatisticsParser.Address] = new PrecompileInfo(ArbStatisticsParser.Instance),
            [ArbDebugParser.Address] = new PrecompileInfo(ArbDebugParser.Instance),
            [ArbWasmCacheParser.Address] = new PrecompileInfo(ArbWasmCacheParser.Instance),
            [ArbBlsParser.Address] = new PrecompileInfo(ArbBlsParser.Instance),
            [ArbNativeTokenManagerParser.Address] = new PrecompileInfo(ArbNativeTokenManagerParser.Instance)
        };
    }

    public ICodeInfo GetCachedCodeInfo(Address codeSource, bool followDelegation, IReleaseSpec vmSpec, out Address? delegationAddress)
    {
        // Check spec FIRST to respect version-based precompile activation
        // This ensures inactive precompiles are treated as regular accounts for gas charging
        if (!vmSpec.IsPrecompile(codeSource))
        {
            // Not a precompile according to spec - do regular code lookup
            ICodeInfo result = codeInfoRepository.GetCachedCodeInfo(codeSource, followDelegation, vmSpec, out delegationAddress);

            // EIP-7702 precompile delegation fix (ArbOS 50+)
            // When following delegation to a precompile, return empty code instead of precompile code (0xFE)
            // Only apply when actually executing (followDelegation=true)
            if (followDelegation &&
                arbosVersionProvider.Get() >= ArbosVersion.Fifty &&
                delegationAddress is not null &&
                vmSpec.IsPrecompile(delegationAddress))
            {
                return CodeInfo.Empty;
            }

            return result;
        }

        // It's a precompile according to spec
        // Check if it's an Arbitrum precompile we handle
        delegationAddress = null;
        return _arbitrumPrecompiles.TryGetValue(codeSource, out ICodeInfo? arbResult)
            ? arbResult
            :
            // Must be Ethereum precompile - delegate to base repository
            codeInfoRepository.GetCachedCodeInfo(codeSource, followDelegation, vmSpec, out delegationAddress);
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
