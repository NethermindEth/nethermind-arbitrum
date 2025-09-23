using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Execution.Receipts;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Tracks L1 price data posted by nitro
/// </summary>
public class CachedL1PriceData(ILogManager logManager)
{
    public ulong StartOfL1PriceDataCache { get; private set; }
    public ulong EndOfL1PriceDataCache { get; private set; }
    public List<L1PriceDataOfMsg> MsgToL1PriceData { get; private set; } = [];

    private readonly ILogger _logger = logManager.GetClassLogger<CachedL1PriceData>();
    private readonly Lock _lock = new();

    public void MarkFeedStart(ulong to)
    {
        lock (_lock)
        {
            if (to < StartOfL1PriceDataCache)
            {
                if (_logger.IsDebug)
                    _logger.Debug("Trying to trim older L1 price data cache which doesn't exist anymore");
            }
            else if (to >= EndOfL1PriceDataCache)
            {
                StartOfL1PriceDataCache = 0;
                EndOfL1PriceDataCache = 0;
                MsgToL1PriceData.Clear();
            }
            else
            {
                ulong newStart = to - StartOfL1PriceDataCache + 1;
                MsgToL1PriceData = MsgToL1PriceData[(int)newStart..];
                StartOfL1PriceDataCache = to + 1;
            }
        }
    }

    public void CacheL1PriceDataOfMsg(
        ulong msgIndex, TxReceipt[] txReceipts,
        Block block, bool blockBuiltUsingDelayedMessage)
    {
        ulong gasUsedForL1 = 0;
        ulong callDataUnits = 0;
        if (!blockBuiltUsingDelayedMessage)
        {
            // CachedL1PriceData tracks L1 price data for messages posted by Nitro,
            // so delayed messages should not update cummulative values kept on it.

            // First transaction in every block is an Arbitrum internal transaction,
            // so we skip it here.
            for (int i = 1; i < txReceipts.Length; i++)
            {
                gasUsedForL1 += (txReceipts[i] as ArbitrumTxReceipt)?.GasUsedForL1 ?? 0;
            }
            foreach (Transaction tx in block.Transactions)
            {
                (_, ulong txCachedL1Units) = tx.GetRawCachedCalldataUnits();
                callDataUnits += txCachedL1Units;
            }
        }

        ulong l1GasCharged = gasUsedForL1 * block.BaseFeePerGas.ToUInt64(null);

        void ResetCache()
        {
            StartOfL1PriceDataCache = msgIndex;
            EndOfL1PriceDataCache = msgIndex;
            MsgToL1PriceData = [new L1PriceDataOfMsg(callDataUnits, callDataUnits, l1GasCharged, l1GasCharged)];
        }

        lock (_lock)
        {
            int size = MsgToL1PriceData.Count;
            if (size == 0 ||
                StartOfL1PriceDataCache == 0 ||
                EndOfL1PriceDataCache == 0 ||
                (ulong)size != EndOfL1PriceDataCache - StartOfL1PriceDataCache + 1)
            {
                ResetCache();
                return;
            }

            if (msgIndex != EndOfL1PriceDataCache + 1)
            {
                if (msgIndex > EndOfL1PriceDataCache + 1)
                {
                    if (_logger.IsTrace)
                        _logger.Trace("Message position higher then current end of l1 price data cache, resetting cache to this message");
                    ResetCache();
                }
                else if (msgIndex < StartOfL1PriceDataCache)
                {
                    if (_logger.IsTrace)
                        _logger.Trace("Message position lower than start of l1 price data cache, ignoring");
                }
                else
                {
                    if (_logger.IsTrace)
                        _logger.Trace("Message position already seen in l1 price data cache, ignoring");
                }
            }
            else
            {
                ulong cummulativeUnitsSoFar = MsgToL1PriceData[size - 1].CummulativeCallDataUnits;
                ulong cummulativeL1GasChargedSoFar = MsgToL1PriceData[size - 1].CummulativeL1GasCharged;
                MsgToL1PriceData.Add(new(
                    CallDataUnits: callDataUnits,
                    CummulativeCallDataUnits: cummulativeUnitsSoFar + callDataUnits,
                    L1GasCharged: l1GasCharged,
                    CummulativeL1GasCharged: cummulativeL1GasChargedSoFar + l1GasCharged));
                EndOfL1PriceDataCache = msgIndex;
            }
        }
    }

    public record L1PriceDataOfMsg(
        ulong CallDataUnits,
        ulong CummulativeCallDataUnits,
        ulong L1GasCharged,
        ulong CummulativeL1GasCharged
    );
}
