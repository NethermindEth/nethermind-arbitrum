// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;
using Nethermind.Core.Extensions;
using Nethermind.Db.Rocks.Config;

namespace Nethermind.Arbitrum.Stylus;

public class ArbitrumDbConfigFactory(IRocksDbConfigFactory factory, IDbConfig dbConfig, IConfigProvider configProvider) : IRocksDbConfigFactory
{
    public IRocksDbConfig GetForDatabase(string databaseName, string? columnName)
    {
        if (!string.Equals(databaseName, WasmDb.DbName, StringComparison.InvariantCultureIgnoreCase))
            return factory.GetForDatabase(databaseName, columnName);

        // IDbConfig.RocksDbOptions contains base options for all DBs, IWasmDbConfig contains config specific to WASM DB.
        IWasmDbConfig wasmConfig = configProvider.GetConfig<IWasmDbConfig>();
        IRocksDbConfig wasmTableConfig = new WasmMergedDbConfig(dbConfig, wasmConfig);

        return wasmTableConfig;
    }
}

public interface IWasmDbConfig : IRocksDbConfig, IConfig;

public class WasmDbConfig : IWasmDbConfig
{
    public string AdditionalRocksDbOptions { get; set; } = string.Empty;
    public double CompressibilityHint { get; set; } = 1.0;
    public bool EnableDbStatistics { get; set; } = false;
    public bool EnableFileWarmer { get; set; } = false;
    public bool FlushOnExit { get; set; } = true;
    public int? MaxOpenFiles { get; set; }
    public ulong? ReadAheadSize { get; set; } = (ulong)256.KiB();
    // Default config options based on Code DB.
    public string RocksDbOptions { get; set; } =
        "write_buffer_size=16000000;" +
        "block_based_table_factory.block_cache=16000000;" +
        "optimize_filters_for_hits=false;" +
        "prefix_extractor=capped:8;" +
        "block_based_table_factory.index_type=kHashSearch;" +
        "block_based_table_factory.block_size=4096;" +
        "memtable=prefix_hash:1000000;" +
        // Bloom crash with kHashSearch index
        "block_based_table_factory.filter_policy=null;" +
        "allow_concurrent_memtable_write=false;";
    public ulong? RowCacheSize { get; set; } = (ulong)16.MiB();
    public uint StatsDumpPeriodSec { get; set; } = 600;
    public bool? VerifyChecksum { get; set; } = true;
    public bool WriteAheadLogSync { get; set; } = false;
    public ulong? WriteBufferNumber { get; set; }
    public ulong? WriteBufferSize { get; set; }
}

public class WasmMergedDbConfig(IDbConfig baseConfig, IWasmDbConfig wasmConfig) : IRocksDbConfig
{
    public string AdditionalRocksDbOptions { get; } = baseConfig.AdditionalRocksDbOptions + wasmConfig.AdditionalRocksDbOptions;
    public double CompressibilityHint { get; } = wasmConfig.CompressibilityHint;
    public bool EnableDbStatistics { get; } = wasmConfig.EnableDbStatistics;
    public bool EnableFileWarmer { get; } = wasmConfig.EnableFileWarmer;
    public bool FlushOnExit { get; } = wasmConfig.FlushOnExit;
    public int? MaxOpenFiles { get; } = wasmConfig.MaxOpenFiles;
    public ulong? ReadAheadSize { get; } = wasmConfig.ReadAheadSize;
    public string RocksDbOptions { get; } = baseConfig.RocksDbOptions + wasmConfig.RocksDbOptions;
    public ulong? RowCacheSize { get; } = wasmConfig.RowCacheSize;
    public uint StatsDumpPeriodSec { get; } = wasmConfig.StatsDumpPeriodSec;
    public bool? VerifyChecksum { get; } = wasmConfig.VerifyChecksum;
    public bool WriteAheadLogSync { get; } = wasmConfig.WriteAheadLogSync;
    public ulong? WriteBufferNumber { get; } = wasmConfig.WriteBufferNumber;
    public ulong? WriteBufferSize { get; } = wasmConfig.WriteBufferSize;
}
