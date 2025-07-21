using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Tracing;

[TestFixture]
public class ArbNullTxTracerTest
{
    private const string ErrorMessage = "Null tracer should never receive any calls.";
    private readonly IArbitrumTxTracer _tracer = ArbNullTxTracer.Instance;

    private readonly Address _dummyAddress = Address.Zero;
    private readonly UInt256 _dummyUint256 = new(1);
    private readonly ValueHash256 _dummyValueHash = new(new byte[32]);
    private readonly GasConsumed _dummyGas = new(100, 100);
    private readonly byte[] _dummyBytes = [];
    private readonly LogEntry[] _dummyLogs = [];
    private readonly Hash256 _dummyHash = Hash256.Zero;
    private readonly StorageCell _dummyStorageCell = new(Address.Zero, UInt256.Zero);

    private void AssertThrows(TestDelegate code)
    {
        var ex = Assert.Throws<InvalidOperationException>(code);
        Assert.That(ex?.Message, Is.EqualTo(ErrorMessage));
    }

    [Test]
    public void CaptureArbitrumTransfer_Always_Throws()
    {
        AssertThrows(() => _tracer.CaptureArbitrumTransfer(_dummyAddress, _dummyAddress, _dummyUint256, true, BalanceChangeReason.BalanceChangeUnspecified));
    }

    [Test]
    public void CaptureArbitrumStorageGet_Always_Throws()
    {
        AssertThrows(() => _tracer.CaptureArbitrumStorageGet(_dummyUint256, 0, true));
    }

    [Test]
    public void CaptureArbitrumStorageSet_Always_Throws()
    {
        AssertThrows(() => _tracer.CaptureArbitrumStorageSet(_dummyUint256, _dummyValueHash, 0, true));
    }

    [Test]
    public void CaptureStylusHostio_Always_Throws()
    {
        AssertThrows(() => _tracer.CaptureStylusHostio("test", ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, 0, 0));
    }

    // =================================================================================
    // ITxTracer / TxTracer Overridden Methods
    // =================================================================================

    [Test]
    public void MarkAsSuccess_Always_Throws()
    {
        AssertThrows(() => _tracer.MarkAsSuccess(_dummyAddress, _dummyGas, _dummyBytes, _dummyLogs));
    }

    [Test]
    public void MarkAsFailed_Always_Throws()
    {
        AssertThrows(() => _tracer.MarkAsFailed(_dummyAddress, _dummyGas, _dummyBytes, "error"));
    }

    [Test]
    public void StartOperation_Always_Throws()
    {
        ExecutionEnvironment env = default;
        AssertThrows(() => _tracer.StartOperation(0, Instruction.STOP, 100, in env));
    }

    [Test]
    public void ReportOperationError_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportOperationError(EvmExceptionType.None));
    }

    [Test]
    public void ReportOperationRemainingGas_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportOperationRemainingGas(100));
    }

    [Test]
    public void SetOperationMemorySize_Always_Throws()
    {
        AssertThrows(() => _tracer.SetOperationMemorySize(128));
    }

    [Test]
    public void ReportMemoryChange_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportMemoryChange((long)0, ReadOnlySpan<byte>.Empty));
    }

    [Test]
    public void ReportStorageChange_WithSpans_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportStorageChange(ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty));
    }

    [Test]
    public void ReportStorageChange_WithCell_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportStorageChange(in _dummyStorageCell, _dummyBytes, _dummyBytes));
    }

    [Test]
    public void SetOperationStack_Always_Throws()
    {
        AssertThrows(() => _tracer.SetOperationStack(new TraceStack()));
    }

    [Test]
    public void ReportStackPush_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportStackPush(ReadOnlySpan<byte>.Empty));
    }

    [Test]
    public void SetOperationMemory_Always_Throws()
    {
        AssertThrows(() => _tracer.SetOperationMemory(new TraceMemory()));
    }

    [Test]
    public void SetOperationStorage_Always_Throws()
    {
        AssertThrows(() => _tracer.SetOperationStorage(_dummyAddress, _dummyUint256, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty));
    }

    [Test]
    public void LoadOperationStorage_Always_Throws()
    {
        AssertThrows(() => _tracer.LoadOperationStorage(_dummyAddress, _dummyUint256, ReadOnlySpan<byte>.Empty));
    }

    [Test]
    public void ReportSelfDestruct_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportSelfDestruct(_dummyAddress, _dummyUint256, _dummyAddress));
    }

    [Test]
    public void ReportBalanceChange_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportBalanceChange(_dummyAddress, _dummyUint256, _dummyUint256));
    }

    [Test]
    public void ReportCodeChange_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportCodeChange(_dummyAddress, _dummyBytes, _dummyBytes));
    }

    [Test]
    public void ReportNonceChange_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportNonceChange(_dummyAddress, _dummyUint256, _dummyUint256));
    }

    [Test]
    public void ReportAccountRead_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportAccountRead(_dummyAddress));
    }

    [Test]
    public void ReportStorageRead_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportStorageRead(in _dummyStorageCell));
    }

    [Test]
    public void ReportAction_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportAction(100, _dummyUint256, _dummyAddress, _dummyAddress, ReadOnlyMemory<byte>.Empty, ExecutionType.CALL));
    }

    [Test]
    public void ReportActionEnd_WithOutput_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportActionEnd(100, ReadOnlyMemory<byte>.Empty));
    }

    [Test]
    public void ReportActionError_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportActionError(EvmExceptionType.None));
    }

    [Test]
    public void ReportActionEnd_WithDeployment_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportActionEnd(100, _dummyAddress, ReadOnlyMemory<byte>.Empty));
    }

    [Test]
    public void ReportBlockHash_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportBlockHash(_dummyHash));
    }

    [Test]
    public void ReportByteCode_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportByteCode(ReadOnlyMemory<byte>.Empty));
    }

    [Test]
    public void ReportGasUpdateForVmTrace_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportGasUpdateForVmTrace(100, 200));
    }

    [Test]
    public void ReportRefund_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportRefund(50));
    }

    [Test]
    public void ReportExtraGasPressure_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportExtraGasPressure(10));
    }

    [Test]
    public void ReportAccess_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportAccess(Array.Empty<Address>(), Array.Empty<StorageCell>()));
    }

    [Test]
    public void ReportFees_Always_Throws()
    {
        AssertThrows(() => _tracer.ReportFees(_dummyUint256, _dummyUint256));
    }
}
