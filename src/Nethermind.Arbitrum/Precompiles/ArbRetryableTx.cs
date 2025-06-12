using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Core;
using Nethermind.Evm;

namespace Nethermind.Arbitrum.Precompiles;

public class ArbRetryableTx
{
    public static Address Address => ArbosAddresses.ArbRetryableTxAddress;

    public static readonly string Metadata =
        "[{\"inputs\":[],\"name\":\"NoTicketWithID\",\"type\":\"error\"},{\"inputs\":[],\"name\":\"NotCallable\",\"type\":\"error\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"Canceled\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"newTimeout\",\"type\":\"uint256\"}],\"name\":\"LifetimeExtended\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"},{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"retryTxHash\",\"type\":\"bytes32\"},{\"indexed\":true,\"internalType\":\"uint64\",\"name\":\"sequenceNum\",\"type\":\"uint64\"},{\"indexed\":false,\"internalType\":\"uint64\",\"name\":\"donatedGas\",\"type\":\"uint64\"},{\"indexed\":false,\"internalType\":\"address\",\"name\":\"gasDonor\",\"type\":\"address\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"maxRefund\",\"type\":\"uint256\"},{\"indexed\":false,\"internalType\":\"uint256\",\"name\":\"submissionFeeRefund\",\"type\":\"uint256\"}],\"name\":\"RedeemScheduled\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"userTxHash\",\"type\":\"bytes32\"}],\"name\":\"Redeemed\",\"type\":\"event\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":true,\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"TicketCreated\",\"type\":\"event\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"cancel\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"getBeneficiary\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getCurrentRedeemer\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getLifetime\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"getTimeout\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"keepalive\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"ticketId\",\"type\":\"bytes32\"}],\"name\":\"redeem\",\"outputs\":[{\"internalType\":\"bytes32\",\"name\":\"\",\"type\":\"bytes32\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"requestId\",\"type\":\"bytes32\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"deposit\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"callvalue\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"gasFeeCap\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"gasLimit\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"maxSubmissionFee\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"feeRefundAddress\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"beneficiary\",\"type\":\"address\"},{\"internalType\":\"address\",\"name\":\"retryTo\",\"type\":\"address\"},{\"internalType\":\"bytes\",\"name\":\"retryData\",\"type\":\"bytes\"}],\"name\":\"submitRetryable\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";

    // Solidity: event TicketCreated(bytes32 indexed ticketId);
    public static readonly AbiEventDescription TicketCreatedEvent;

    static ArbRetryableTx()
    {
        List<AbiEventDescription> allEvents = AbiMetadata.GetAllEventDescriptions(Metadata)!;
        TicketCreatedEvent = allEvents.FirstOrDefault(e => e.Name == "TicketCreated") ?? throw new ArgumentException("TicketCreated event not found");

        // TicketCreatedEvent = new AbiEventDescription
        // {
        //     Name = "TicketCreated",
        //     Inputs =
        //     [
        //         new AbiEventParameter
        //         {
        //             Name = "ticketId",
        //             Type = AbiBytes.Bytes32,
        //             Indexed = true
        //         }
        //     ],
        //     Anonymous = false
        // };
    }

    /********* Events *********/
    public static LogEntry TicketCreated(Context context, ArbVirtualMachine vm, byte[] ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(TicketCreatedEvent, Address, ticketId);
        return EventsEncoder.EmitEvent(context, vm, eventLog);
    }

    /********* Events Cost *********/
    public static ulong TicketCreatedGasCost(byte[] ticketId)
    {
        LogEntry eventLog = EventsEncoder.BuildLogEntryFromEvent(TicketCreatedEvent, Address, ticketId);
        return EventsEncoder.EventCost(eventLog);
    }


    /********* Methods *********/
    public Int256.UInt256 GetBalance(Context context, ArbVirtualMachine vm, Address account)
    {
        context.Burn(GasCostOf.BalanceEip1884);
        return vm.WorldState.GetBalance(account);
    }

    public byte[] GetCode(Context context, ArbVirtualMachine vm, Address account)
    {
        context.Burn(GasCostOf.ColdSLoad);
        byte[] code = vm.WorldState.GetCode(account) ?? [];
        context.Burn(GasCostOf.DataCopy * (ulong)EvmPooledMemory.Div32Ceiling((Int256.UInt256)code.Length));
        return code;
    }
}