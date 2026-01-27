# Task 03: RPC CQRS-Style Handler Extraction

> **Status**: TODO
> **Priority**: MEDIUM - Improves testability and separation of concerns

## Metadata
- **Target**: New directory `src/Nethermind.Arbitrum/Modules/Handlers/`
- **Affected Files**: `ArbitrumRpcModule.cs` (589 lines)
- **Type**: Architecture Refactoring
- **Dependencies**: None (can run parallel to Task 01/02)
- **Estimated Impact**: Better testability, reduced class complexity

## Problem Statement

`ArbitrumRpcModule` is 589 lines handling multiple responsibilities:

1. Block production (DigestMessage, Reorg)
2. Block queries (MessageIndexToBlockNumber, HeadMessageIndex)
3. Finality management (SetFinalityData, Synced)
4. Maintenance (TriggerMaintenance, ShouldTriggerMaintenance)
5. Sync status (SetConsensusSyncData, FullSyncProgressMap)

**Problems:**
- Hard to test individual operations in isolation
- Growing class with many dependencies (12 constructor parameters)
- Mixed concerns: commands (mutations) vs queries (reads)
- Difficult to add cross-cutting concerns (logging, metrics, validation)

## Solution

Extract CQRS-style handlers for each operation group:

```
src/Nethermind.Arbitrum/Modules/
├── ArbitrumRpcModule.cs          # Thin facade, delegates to handlers
├── IArbitrumRpcModule.cs         # Interface unchanged (backward compat)
└── Handlers/
    ├── IBlockProductionHandler.cs
    ├── BlockProductionHandler.cs    # DigestMessage, Reorg
    ├── IBlockQueryHandler.cs
    ├── BlockQueryHandler.cs         # MessageIndexToBlockNumber, etc.
    ├── IFinalityHandler.cs
    ├── FinalityHandler.cs           # SetFinalityData, Synced
    ├── IMaintenanceHandler.cs
    └── MaintenanceHandler.cs        # TriggerMaintenance, etc.
```

## Implementation

### Step 1: Define Handler Interfaces

```csharp
// Handlers/IBlockProductionHandler.cs
public interface IBlockProductionHandler
{
    Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters);
    Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters);
}

// Handlers/IBlockQueryHandler.cs
public interface IBlockQueryHandler
{
    Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(ulong messageIndex);
    Task<ResultWrapper<ulong>> HeadMessageIndex();
    Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex);
    Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber);
    Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndex(ulong messageIndex);
}

// Handlers/IFinalityHandler.cs
public interface IFinalityHandler
{
    ResultWrapper<string> SetFinalityData(SetFinalityDataParams? parameters);
    ResultWrapper<string> MarkFeedStart(ulong to);
    ResultWrapper<string> SetConsensusSyncData(SetConsensusSyncDataParams? parameters);
    ResultWrapper<bool> Synced();
    ResultWrapper<Dictionary<string, object>> FullSyncProgressMap();
}

// Handlers/IMaintenanceHandler.cs
public interface IMaintenanceHandler
{
    Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatus();
    Task<ResultWrapper<bool>> ShouldTriggerMaintenance();
    Task<ResultWrapper<string>> TriggerMaintenance();
}
```

### Step 2: Extract BlockProductionHandler

```csharp
// Handlers/BlockProductionHandler.cs
public class BlockProductionHandler : IBlockProductionHandler
{
    private readonly SemaphoreSlim _createBlocksSemaphore = new(1, 1);
    private readonly IBlockTree _blockTree;
    private readonly IManualBlockProductionTrigger _trigger;
    private readonly IArbitrumSpecHelper _specHelper;
    private readonly IBlocksConfig _blocksConfig;
    private readonly ILogger _logger;

    public BlockProductionHandler(
        IBlockTree blockTree,
        IManualBlockProductionTrigger trigger,
        IArbitrumSpecHelper specHelper,
        IBlocksConfig blocksConfig,
        ILogManager logManager)
    {
        _blockTree = blockTree;
        _trigger = trigger;
        _specHelper = specHelper;
        _blocksConfig = blocksConfig;
        _logger = logManager.GetClassLogger<BlockProductionHandler>();
    }

    public async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
    {
        // Move existing DigestMessage logic here
        // Current: ArbitrumRpcModule.cs:73-105
    }

    public async Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters)
    {
        // Move existing Reorg logic here
        // Current: ArbitrumRpcModule.cs:107-185
    }
}
```

### Step 3: Refactor ArbitrumRpcModule to Facade

```csharp
// ArbitrumRpcModule.cs - AFTER (thin facade)
public class ArbitrumRpcModule : IArbitrumRpcModule
{
    private readonly IBlockProductionHandler _blockProductionHandler;
    private readonly IBlockQueryHandler _blockQueryHandler;
    private readonly IFinalityHandler _finalityHandler;
    private readonly IMaintenanceHandler _maintenanceHandler;

    public ArbitrumRpcModule(
        IBlockProductionHandler blockProductionHandler,
        IBlockQueryHandler blockQueryHandler,
        IFinalityHandler finalityHandler,
        IMaintenanceHandler maintenanceHandler)
    {
        _blockProductionHandler = blockProductionHandler;
        _blockQueryHandler = blockQueryHandler;
        _finalityHandler = finalityHandler;
        _maintenanceHandler = maintenanceHandler;
    }

    // Delegate to handlers
    public Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        => _blockProductionHandler.DigestMessage(parameters);

    public Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters)
        => _blockProductionHandler.Reorg(parameters);

    public Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(ulong messageIndex)
        => _blockQueryHandler.ResultAtMessageIndex(messageIndex);

    // ... other methods delegate similarly
}
```

### Step 4: Update DI Registration

```csharp
// ArbitrumModule.cs or ArbitrumRpcModuleFactory.cs
builder
    .AddSingleton<IBlockProductionHandler, BlockProductionHandler>()
    .AddSingleton<IBlockQueryHandler, BlockQueryHandler>()
    .AddSingleton<IFinalityHandler, FinalityHandler>()
    .AddSingleton<IMaintenanceHandler, MaintenanceHandler>();
```

## Testing Benefits

### Before (Current)

```csharp
// Test requires full RPC module setup
ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.DigestMessage(params);
```

### After (With Handlers)

```csharp
// Unit test handler in isolation
[Test]
public async Task DigestMessage_ValidMessage_ProducesBlock()
{
    // Arrange - minimal mocks
    Mock<IBlockTree> blockTree = new();
    Mock<IManualBlockProductionTrigger> trigger = new();
    BlockProductionHandler handler = new(blockTree.Object, trigger.Object, ...);

    // Act
    ResultWrapper<MessageResult> result = await handler.DigestMessage(parameters);

    // Assert
    Assert.That(result.Result, Is.EqualTo(Result.Success));
    trigger.Verify(t => t.BuildBlock(It.IsAny<BlockHeader>(), It.IsAny<IPayloadAttributes>()), Times.Once);
}
```

## Verification

### Metrics

| Metric | Before | After |
|--------|--------|-------|
| ArbitrumRpcModule lines | 589 | ~100 |
| Constructor parameters | 12 | 4 |
| Test setup complexity | High (full chain) | Low (mock handlers) |
| Test execution time | ~2-5s | ~10ms |

### Commands to Verify

```bash
# Ensure all tests still pass
dotnet test src/Nethermind.Arbitrum.Test --filter "ArbitrumRpcModule"

# Check line counts
wc -l src/Nethermind.Arbitrum/Modules/ArbitrumRpcModule.cs
# Should be < 150 lines

wc -l src/Nethermind.Arbitrum/Modules/Handlers/*.cs
# Handler total should be ~500 lines (logic moved, not duplicated)
```

## Acceptance Criteria

- [ ] Handler interfaces defined
- [ ] At least `BlockProductionHandler` extracted
- [ ] `ArbitrumRpcModule` reduced to < 150 lines
- [ ] All existing tests pass
- [ ] New handler unit tests added
- [ ] DI registration updated
- [ ] No new warnings

## Rollback Plan

Keep original implementation alongside:

```csharp
public class ArbitrumRpcModule : IArbitrumRpcModule
{
    // If handlers not available, fall back to embedded logic
    private readonly IBlockProductionHandler? _handler;

    public Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        => _handler?.DigestMessage(parameters) ?? DigestMessageInternal(parameters);
}
```

## Future Enhancements

1. **Decorator Pattern**: Add logging/metrics decorators around handlers
2. **Validation Pipeline**: Add FluentValidation before handlers
3. **Caching**: Add caching layer for query handlers
4. **Retry Logic**: Add retry decorator for transient failures
