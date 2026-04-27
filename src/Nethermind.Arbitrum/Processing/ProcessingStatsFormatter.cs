// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Processing;

internal static class ProcessingStatsFormatter
{
    private const string ResetColor = "\u001b[37m";
    private const string WhiteText = "\u001b[97m";
    private const string YellowText = "\u001b[93m";
    private const string OrangeText = "\u001b[38;5;208m";
    private const string RedText = "\u001b[38;5;196m";
    private const string GreenText = "\u001b[92m";
    private const string DarkGreenText = "\u001b[32m";
    private const string DarkCyanText = "\u001b[36m";
    private const string BlueText = "\u001b[94m";
    private const string MagentaText = "\u001b[95m";

    private const double StylusSignificanceThreshold = 0.2;

    public static void Format(scoped in ProcessingStatsReport report, List<string> lines)
    {
        string gasPrice = report.GasPrices is { } g
            ? $"⛽ Gas gwei: {g.Min:N3} .. {WhiteText}{System.Math.Max(g.Min, g.EstMedian):N3}{ResetColor} ({g.Ave:N3}) .. {g.Max:N3}"
            : "";

        if (report.ChunkBlocks > 1)
            lines.Add($"Processed  {report.BlockNumber - report.ChunkBlocks + 1,9}..{report.BlockNumber,9} | {report.ChunkMs,8:N1} ms | elapsed {report.RunMs,6:N0} ms | {gasPrice}");
        else
        {
            string chunkColor = report.ChunkMs switch
            {
                < 200 => GreenText,
                < 300 => DarkGreenText,
                < 500 => WhiteText,
                < 1000 => YellowText,
                < 2000 => OrangeText,
                _ => RedText
            };
            lines.Add($"Processed             {report.BlockNumber,9} | {chunkColor}{report.ChunkMs,8:N1}{ResetColor} ms | elapsed {report.RunMs,6:N0} ms | {gasPrice}");
        }

        string mgasPerSecondColor = (report.MGasPerSecond / (report.GasLimit / 1_000_000.0)) switch
        {
            > 3 => GreenText,
            > 2.5f => DarkGreenText,
            > 2 => WhiteText,
            > 1.5f => ResetColor,
            > 1 => YellowText,
            > 0.5f => OrangeText,
            _ => RedText
        };
        string sstoreColor = report.ChunkBlocks > 1 ? "" : report.ChunkSstore switch
        {
            > 3500 => RedText,
            > 2500 => OrangeText,
            > 2000 => YellowText,
            > 1500 => WhiteText,
            > 900 when report.ChunkCalls > 900 => WhiteText,
            _ => ""
        };
        string callsColor = report.ChunkBlocks > 1 ? "" : report.ChunkCalls switch
        {
            > 3500 => RedText,
            > 2500 => OrangeText,
            > 2000 => YellowText,
            > 1500 => WhiteText,
            > 900 when report.ChunkSstore > 900 => WhiteText,
            _ => ""
        };
        string createsColor = report.ChunkBlocks > 1 ? "" : report.ChunkCreates switch
        {
            > 300 => RedText,
            > 200 => OrangeText,
            > 150 => YellowText,
            > 75 => WhiteText,
            _ => ""
        };
        string mgasColor = report.ChunkBlocks != 1 ? "" : (report.ChunkMGas / (report.GasLimit / 16_000_000.0)) switch
        {
            > 15 => RedText,
            > 14 => OrangeText,
            > 13 => YellowText,
            > 10 => DarkGreenText,
            > 7 => GreenText,
            > 6 => DarkGreenText,
            > 5 => WhiteText,
            > 4 => ResetColor,
            > 3 => DarkCyanText,
            _ => BlueText
        };

        bool isSignificant = report.StylusCallsDelta > 10 && (report.ChunkCalls == 0 || report.StylusCallsDelta > report.ChunkCalls * StylusSignificanceThreshold);
        string splash = isSignificant ? " ✨" : "   ";
        string stylusSection = $" | {MagentaText}stylus {GetGap(7, report.StylusCallsDelta)}{splash}{report.StylusCallsDelta:N0}{ResetColor}";

        lines.Add($" Block{(report.ChunkBlocks > 1 ? $"s x{report.ChunkBlocks,-3:N0} " : "       ")} {mgasColor}{report.ChunkMGas,12:F2}{ResetColor} MGas | {report.ChunkTx,7:N0} txs{stylusSection} | calls {callsColor}{report.ChunkCalls,11:N0}{ResetColor} | sload {report.ChunkSload,6 :N0} | sstore {sstoreColor}{report.ChunkSstore,7:N0}{ResetColor} | create {createsColor}{report.ChunkCreates,4:N0}{ResetColor}");

        if (report.RecoveryQueueSize > 0 || report.ProcessingQueueSize > 0)
            lines.Add($" Throughput    {(report.MGasPerSecond > 1000 ? "🔥" : "  ")}{mgasPerSecondColor}{report.MGasPerSecond,7:F2}{ResetColor} MGas/s | {report.Txps,7:N1} tps " +
                      $"|   {report.Bps,6:F2} blocks/s | recover {report.RecoveryQueueSize,9:N0} | process {report.ProcessingQueueSize,4:N0} | ops {report.ChunkOpCodes,10:N0} | destruct {report.ChunkSelfDestructs,2:N0}");
        else
            lines.Add($" Throughput    {(report.MGasPerSecond > 1000 ? "🔥" : "  ")}{mgasPerSecondColor}{report.MGasPerSecond,7:F2}{ResetColor} MGas/s | {report.Txps,7:N1} tps " +
                      $"|   {report.Bps,6:F2} blocks/s | code cache {report.CachedContractsUsed,6:N0} | new {report.ContractsAnalysed,8:N0} | ops {report.ChunkOpCodes,10:N0} | destruct {report.ChunkSelfDestructs,2:N0}");
    }

    private static string GetGap(int gapMaxLength, long n)
    {
        int digits = n == 0 ? 1 : (int)System.Math.Floor(System.Math.Log10(System.Math.Abs(n))) + 1;
        int length = digits + (digits - 1) / 3 + (n < 0 ? 1 : 0);
        return new string(' ', gapMaxLength - length);
    }
}

internal readonly record struct ProcessingStatsReport(
    long BlockNumber,
    long ChunkBlocks,
    double ChunkMGas,
    long ChunkTx,
    double ChunkMs,
    double RunMs,
    (float Min, float EstMedian, float Ave, float Max)? GasPrices,
    long ChunkCalls,
    long ChunkEmptyCalls,
    long ChunkSload,
    long ChunkSstore,
    long ChunkCreates,
    long ChunkSelfDestructs,
    long ChunkOpCodes,
    double MGasPerSecond,
    double Txps,
    double Bps,
    long GasLimit,
    long StylusCallsDelta,
    double StylusMs,
    long CachedContractsUsed,
    long ContractsAnalysed,
    long RecoveryQueueSize,
    long ProcessingQueueSize);
