// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class ArbitrumRpcTestBlockchainExtensions
{
    extension(ArbitrumRpcTestBlockchain chain)
    {
        public Block AppendBlock(Action<ArbitrumRpcTestBlockchain> modifyState)
        {
            ArgumentNullException.ThrowIfNull(chain.BlockTree.Head);

            Block parentBlock = chain.BlockTree.Head;

            Hash256 stateRoot;
            using (chain.MainWorldState.BeginScope(parentBlock.Header))
            {
                modifyState(chain);

                chain.MainWorldState.Commit(chain.SpecProvider.GenesisSpec);
                chain.MainWorldState.CommitTree(parentBlock.Number + 1);

                stateRoot = chain.MainWorldState.StateRoot;
            }

            BlockHeader header = parentBlock.Header.Clone();
            header.ParentHash = chain.BlockTree.HeadHash;
            header.StateRoot = stateRoot;
            header.Number++;
            header.Hash = header.CalculateHash();
            header.TotalDifficulty = (header.TotalDifficulty ?? 0) + 1;

            Block block = Build.A.Block
                .WithHeader(header)
                .WithTransactions() // Leave block empty as we only care about state changes
                .TestObject;

            chain.BlockTree.SuggestBlock(block, BlockTreeSuggestOptions.ForceSetAsMain);
            chain.BlockTree.UpdateMainChain([block], true, true);

            chain.AdvanceBlockNumber();

            return block;
        }

        public ResultWrapper<MessageResult> PrefundAccount(Address recipient, UInt256 amount)
        {
            Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
            return chain.Digest(new TestEthDeposit(requestId, chain.InitialL1BaseFee, recipient, recipient, amount)).GetAwaiter().GetResult();
        }

        public ResultWrapper<MessageResult> DeployStylusContract(Address sender, string watFilePath, out byte[] wasmCode, out Address contractAddress)
        {
            byte[] wat = File.ReadAllBytes(watFilePath);
            StylusNativeResult<byte[]> wasmResult = StylusNative.WatToWasm(wat);
            wasmResult.IsSuccess.Should().BeTrue($"{watFilePath}: WAT to WASM conversion should succeed");

            return chain.DeployStylusContract(sender, wasmResult.Value!, out wasmCode, out contractAddress);
        }

        public ResultWrapper<MessageResult> DeployStylusContract(Address sender, byte[] wasm, out byte[] wasmCode, out Address contractAddress)
        {
            // Compress and add Stylus prefix
            byte[] compressed = BrotliCompression.Compress(wasm, 1).ToArray();
            wasmCode = [.. StylusCode.NewStylusPrefix(dictionary: (byte)BrotliCompression.Dictionary.EmptyDictionary), .. compressed];

            // Create init code that returns the WASM bytecode as runtime code
            byte[] stylusInitCode = Prepare.EvmCode.ForInitOf(wasmCode).Done;

            // Get sender nonce and calculate expected contract address
            UInt256 senderNonce = chain.WorldStateAccessor.GetNonce(sender);
            contractAddress = ContractAddress.From(sender, senderNonce);

            // Create contract deployment transaction
            Transaction deployTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(null)
                .WithData(stylusInitCode)
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(100_000_000)
                .WithValue(0)
                .WithNonce(senderNonce)
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;

            // Deploy via Digest
            return chain.Digest(new TestL2Transactions(chain.InitialL1BaseFee, sender, deployTx)).GetAwaiter().GetResult();
        }

        public ResultWrapper<MessageResult> ActivateStylusContract(Address sender, Address contact)
        {
            AbiFunctionDescription function = ArbWasmParser.ActivateProgramDescription.AbiFunctionDescription;
            byte[] calldata = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, function.GetCallInfo().Signature, contact);

            Transaction activateTx = Build.A.Transaction
                .WithType(TxType.EIP1559)
                .WithTo(ArbosAddresses.ArbWasmAddress)
                .WithData(calldata)
                .WithValue(100.Ether())
                .WithMaxFeePerGas(10.GWei())
                .WithGasLimit(5_000_000)
                .WithNonce(chain.WorldStateAccessor.GetNonce(sender))
                .SignedAndResolved(FullChainSimulationAccounts.Owner)
                .TestObject;

            // Execute activation via Digest
            return chain.Digest(new TestL2Transactions(chain.InitialL1BaseFee, sender, activateTx)).GetAwaiter().GetResult();
        }
    }
}
