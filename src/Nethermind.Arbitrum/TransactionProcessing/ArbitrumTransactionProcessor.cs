
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.TransactionProcessing;

// A TxProcessor is created and freed for every L2 transaction.
// It tracks state for ArbOS, allowing it infuence in Geth's tx processing.
// Public fields are accessible in precompiles.
public class ArbitrumTransactionProcessor : TransactionProcessorBase
{
    private Message? _message;

    private ArbosState? _arbosState;

    // set once in GasChargingHook to track L1 calldata costs
    public Int256.Int256? PosterFee { get; set; }

    private ulong _posterGas;

    // amount of gas temporarily held to prevent compute from exceeding the gas limit
    private ulong computeHoldGas;

    // whether this tx was submitted through the delayed inbox
    private bool _delayedInbox;

    // maybe we can/should reuse  Nethermind.Evm.ExecutionEnvironment here
    public Contract[] Contracts { get; set; } = [];

    // # of distinct context spans for each program
    public Dictionary<Address, uint> Programs { get; set; } = new();

    // set once in StartTxHook
    public byte? TopTxType;

    public Hash256? CurrentRetryable;

    public Address? CurrentRefundTo;

    // Caches for the latest L1 block number and hash,
	// for the NUMBER and BLOCKHASH opcodes.
    private readonly ulong _cachedL1BlockNumber;
    private readonly Dictionary<ulong, Hash256> _cachedL1BlockHashes = new();

    public ArbitrumTransactionProcessor(
        ISpecProvider? specProvider,
        IWorldState? worldState,
        IVirtualMachine? virtualMachine,
        ICodeInfoRepository? codeInfoRepository,
        ILogManager? logManager)
        : base(specProvider, worldState, virtualMachine, new ArbitrumCodeInfoRepository(codeInfoRepository), logManager)
    {
        // Context context = new(state.From, (ulong)state.GasAvailable, (ulong)state.GasAvailable, TxTracer, false);
        // context.ArbosState = ArbosState.OpenArbosState(WorldState, context, Logger);
    }
}

public class Contract{}


public class Message
{
    public MessageRunMode RunMode { get; set; }

    public Transaction? Transaction { get; set; }

    // When SkipAccountChecks is true, the message nonce is not checked against the
    // account nonce in state. It also disables checking that the sender is an EOA.
    // This field will be set to true for operations like RPC eth_call.
    public bool SkipAccountChecks { get; set; }
}

public enum MessageRunMode : byte
{
    MessageCommitMode,
    MessageGasEstimationMode,
    MessageEthcallMode,
    MessageReplayMode
}
