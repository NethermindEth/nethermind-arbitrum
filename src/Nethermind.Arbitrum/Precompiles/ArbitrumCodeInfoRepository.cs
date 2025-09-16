using System.Diagnostics.CodeAnalysis;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Precompiles;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbitrumCodeInfoRepository(ICodeInfoRepository codeInfoRepository) : IOverridableCodeInfoRepository
{
    private readonly Dictionary<Address, ICodeInfo> _codeOverwrites = InitializePrecompiledContracts();

    private static Dictionary<Address, ICodeInfo> InitializePrecompiledContracts()
    {
        return new Dictionary<Address, ICodeInfo>
        {
            [ArbInfoParser.Address] = new PrecompileInfo(ArbInfoParser.Instance),
            [ArbRetryableTxParser.Address] = new PrecompileInfo(ArbRetryableTxParser.Instance),
            [ArbOwnerParser.Address] = new PrecompileInfo(new OwnerWrapper<ArbOwnerParser>(ArbOwnerParser.Instance, ArbOwner.OwnerActsEvent)),
            [ArbSysParser.Address] = new PrecompileInfo(ArbSysParser.Instance),
            [ArbAddressTableParser.Address] = new PrecompileInfo(ArbAddressTableParser.Instance),
            [ArbWasmParser.Address] = new PrecompileInfo(ArbWasmParser.Instance),
            [ArbGasInfoParser.Address] = new PrecompileInfo(ArbGasInfoParser.Instance),
            [ArbAggregatorParser.Address] = new PrecompileInfo(ArbAggregatorParser.Instance),
        };
    }

    public bool IsPrecompile(Address address, IReleaseSpec spec) => spec.IsPrecompile(address);
    public ICodeInfo GetCachedCodeInfo(Address codeSource, bool followDelegation, IReleaseSpec vmSpec, out Address? delegationAddress)
    {
        delegationAddress = null;
        return _codeOverwrites.TryGetValue(codeSource, out ICodeInfo result)
            ? result
            : codeInfoRepository.GetCachedCodeInfo(codeSource, followDelegation, vmSpec, out delegationAddress);
    }

    public ValueHash256 GetExecutableCodeHash(Address address, IReleaseSpec spec) =>
        codeInfoRepository.GetExecutableCodeHash(address, spec);
    public void InsertCode(ReadOnlyMemory<byte> code, Address codeOwner, IReleaseSpec spec) =>
        codeInfoRepository.InsertCode(code, codeOwner, spec);
    public void SetDelegation(Address codeSource, Address authority, IReleaseSpec spec) =>
        codeInfoRepository.SetDelegation(codeSource, authority, spec);
    public bool TryGetDelegation(Address address, IReleaseSpec vmSpec, [NotNullWhen(true)] out Address? delegatedAddress) =>
        codeInfoRepository.TryGetDelegation(address, vmSpec, out delegatedAddress);
    public void SetDelegation(IWorldState state, Address codeSource, Address authority, IReleaseSpec spec) =>
        codeInfoRepository.SetDelegation(codeSource, authority, spec);

    public bool TryGetDelegation(IReadOnlyStateProvider worldState, Address address, IReleaseSpec vmSpec, [NotNullWhen(true)] out Address? delegatedAddress) =>
        codeInfoRepository.TryGetDelegation(address, vmSpec, out delegatedAddress);

    public void SetCodeOverwrite(IReleaseSpec vmSpec, Address key, ICodeInfo value, Address? redirectAddress = null)
    {
        if (redirectAddress is not null)
        {
            _codeOverwrites[redirectAddress] = this.GetCachedCodeInfo(key, vmSpec);
        }

        _codeOverwrites[key] = value;
    }

    public void ResetOverrides() => _codeOverwrites.Clear();
}
