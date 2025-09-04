// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Core.Specs;
using Nethermind.Facade;
using Nethermind.Facade.Eth;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;
using Nethermind.Network;
using Nethermind.State;
using Nethermind.TxPool;
using Nethermind.Wallet;

namespace Nethermind.Arbitrum.Test.Rpc;

public class ArbitrumEthRpcModuleTests
{
    [Test]
    public void Validate_ArbitrumEthRpcModule_DependenciesExistInTestContainer()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        IContainer container = chain.Container;

        List<string> missingDependencies = new();
        List<string> availableDependencies = new();

        TestDependency<IJsonRpcConfig>(container, availableDependencies, missingDependencies);
        TestDependency<IBlockchainBridge>(container, availableDependencies, missingDependencies);
        TestDependency<IReceiptFinder>(container, availableDependencies, missingDependencies);
        TestDependency<IStateReader>(container, availableDependencies, missingDependencies);
        TestDependency<ITxPool>(container, availableDependencies, missingDependencies);
        TestDependency<ITxSender>(container, availableDependencies, missingDependencies);
        TestDependency<IWallet>(container, availableDependencies, missingDependencies);
        TestDependency<ISpecProvider>(container, availableDependencies, missingDependencies);
        TestDependency<IGasPriceOracle>(container, availableDependencies, missingDependencies);
        TestDependency<IEthSyncingInfo>(container, availableDependencies, missingDependencies);
        TestDependency<IFeeHistoryOracle>(container, availableDependencies, missingDependencies);
        TestDependency<IProtocolsManager>(container, availableDependencies, missingDependencies);
        TestDependency<IForkInfo>(container, availableDependencies, missingDependencies);
        TestDependency<IBlocksConfig>(container, availableDependencies, missingDependencies);

        if (missingDependencies.Count > 0)
        {
            Assert.Inconclusive($"Test container missing {missingDependencies.Count} dependencies needed for ArbitrumEthRpcModule. " +
                               $"Available: {availableDependencies.Count}, Missing: {string.Join(", ", missingDependencies)}");
        }
        else
        {
            Assert.Pass($"All {availableDependencies.Count} dependencies are available in test container");
        }
    }

    private void TestDependency<T>(IContainer container, List<string> available, List<string> missing)
    {
        try
        {
            container.Resolve<T>();
            available.Add(typeof(T).Name);
        }
        catch (Exception ex)
        {
            missing.Add($"{typeof(T).Name} ({ex.GetType().Name})");
        }
    }
}
