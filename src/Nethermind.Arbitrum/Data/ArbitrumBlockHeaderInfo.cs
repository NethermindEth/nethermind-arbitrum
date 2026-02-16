// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Data
{
    /// <summary>
    /// Represents the Arbitrum-specific information stored in block headers.
    /// This matches the structure used in Nitro's HeaderExtraInformation.
    /// </summary>
    public class ArbitrumBlockHeaderInfo
    {
        /// <summary>
        /// The root hash of the send merkle tree for this block
        /// </summary>
        public required Hash256 SendRoot { get; set; }

        /// <summary>
        /// The version of ArbOS used in this block
        /// </summary>
        public ulong ArbOSFormatVersion { get; set; }

        /// <summary>
        /// The corresponding L1 block number for this L2 block
        /// </summary>
        public ulong L1BlockNumber { get; set; }

        /// <summary>
        /// The total number of sends processed up to this block
        /// </summary>
        public ulong SendCount { get; set; }

        /// <summary>
        /// Returns an empty ArbitrumBlockHeaderInfo with all fields set to zero
        /// </summary>
        public static ArbitrumBlockHeaderInfo Empty =>
            new()
            {
                SendRoot = Hash256.Zero,
                ArbOSFormatVersion = 0,
                L1BlockNumber = 0,
                SendCount = 0
            };

        /// <summary>
        /// Deserializes Arbitrum-specific information from a block header's ExtraData and MixHash fields.
        /// The data is split as follows:
        /// ExtraData (32 bytes):
        /// - 32 bytes for SendRoot
        /// MixHash (24 bytes):
        /// - 8 bytes for SendCount
        /// - 8 bytes for L1BlockNumber
        /// - 8 bytes for ArbOSFormatVersion
        /// </summary>
        /// <param name="header">The block header containing the Arbitrum-specific information</param>
        /// <param name="logger">Logger for diagnostic information</param>
        /// <returns>Deserialized ArbitrumBlockHeaderInfo, or Empty if data is invalid</returns>
        public static ArbitrumBlockHeaderInfo Deserialize(BlockHeader header, ILogger logger)
        {
            try
            {
                if (header == null)
                {
                    if (logger.IsWarn)
                        logger.Warn("Block header is null");
                    return Empty;
                }

                if (header.BaseFeePerGas.IsZero || header.ExtraData.Length != 32 || header.Difficulty != 1)
                {
                    // imported blocks have no base fee
                    // The genesis block doesn't have an ArbOS encoded extra field
                    return Empty;
                }

                if (header.MixHash == null)
                {
                    if (logger.IsWarn)
                        logger.Warn("Block header MixHash is null");
                    return Empty;
                }

                ReadOnlySpan<byte> mixHashBytes = header.MixHash.Bytes;
                ArbitrumBlockHeaderInfo info = new()
                {
                    SendRoot = new Hash256(header.ExtraData),
                    SendCount = ArbitrumBinaryReader.ReadULongOrFail(ref mixHashBytes),
                    L1BlockNumber = ArbitrumBinaryReader.ReadULongOrFail(ref mixHashBytes),
                    ArbOSFormatVersion = ArbitrumBinaryReader.ReadULongOrFail(ref mixHashBytes)
                };

                if (logger.IsTrace)
                    logger.Trace($"Deserialized block header info: SendRoot={info.SendRoot}, ArbOSVersion={info.ArbOSFormatVersion}, L1Block={info.L1BlockNumber}, SendCount={info.SendCount}");
                return info;
            }
            catch (Exception ex)
            {
                if (logger.IsError)
                    logger.Error($"Failed to deserialize block header info: {ex.Message}", ex);
                return Empty;
            }
        }

        public static void UpdateHeader(BlockHeader header, ArbitrumBlockHeaderInfo info)
        {
            Span<byte> mixHash = stackalloc byte[32];
            info.SendCount.ToBigEndianByteArray().CopyTo(mixHash);
            info.L1BlockNumber.ToBigEndianByteArray().CopyTo(mixHash[8..]);
            info.ArbOSFormatVersion.ToBigEndianByteArray().CopyTo(mixHash[16..]);

            header.ExtraData = info.SendRoot.BytesToArray();
            header.MixHash = new Hash256(mixHash);
        }
    }
}
