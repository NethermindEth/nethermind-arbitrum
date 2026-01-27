# Task 06: Test Infrastructure Builder Pattern

> **Status**: TODO
> **Priority**: LOW - Test maintainability improvement

## Metadata
- **Target**: New file `src/Nethermind.Arbitrum.Test/Infrastructure/ArbitrumTestBlockchainBuilder.cs`
- **Affected Files**: `ArbitrumRpcTestBlockchain.cs` (516 lines)
- **Type**: Test Infrastructure
- **Dependencies**: None
- **Estimated Impact**: Better test readability, easier test setup

## Problem Statement

`ArbitrumRpcTestBlockchain` is 516 lines with complex setup:

```csharp
// Current usage - complex, magic methods
ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
    configurer: builder => builder.AddSingleton<ISomething, MockSomething>(),
    chainSpec: FullChainSimulationChainSpecProvider.Create(),
    configureArbitrum: c => c.BlockProcessingTimeout = 10_000
);
```

**Problems:**
1. Factory method with many optional parameters
2. Configuration via lambdas is hard to read
3. Common test setups duplicated across tests
4. Hard to understand what configuration does

## Solution

Implement fluent builder pattern:

### Step 1: Create Builder Class

```csharp
// Infrastructure/ArbitrumTestBlockchainBuilder.cs
public class ArbitrumTestBlockchainBuilder
{
    private ChainSpec? _chainSpec;
    private ArbitrumConfig _arbitrumConfig = new() { BlockProcessingTimeout = 10_000 };
    private readonly List<Action<ContainerBuilder>> _containerConfigurations = new();
    private ulong _arbosVersion = ArbosVersion.Latest;
    private UInt256 _initialL1BaseFee = 1_000_000_000; // 1 gwei default
    private bool _buildBlocksOnMainState = true;

    /// <summary>
    /// Use custom chain spec
    /// </summary>
    public ArbitrumTestBlockchainBuilder WithChainSpec(ChainSpec chainSpec)
    {
        _chainSpec = chainSpec;
        return this;
    }

    /// <summary>
    /// Configure ArbOS version for tests
    /// </summary>
    public ArbitrumTestBlockchainBuilder WithArbosVersion(ulong version)
    {
        _arbosVersion = version;
        return this;
    }

    /// <summary>
    /// Configure block processing timeout
    /// </summary>
    public ArbitrumTestBlockchainBuilder WithBlockProcessingTimeout(int timeoutMs)
    {
        _arbitrumConfig.BlockProcessingTimeout = timeoutMs;
        return this;
    }

    /// <summary>
    /// Configure initial L1 base fee
    /// </summary>
    public ArbitrumTestBlockchainBuilder WithInitialL1BaseFee(UInt256 baseFee)
    {
        _initialL1BaseFee = baseFee;
        return this;
    }

    /// <summary>
    /// Configure whether to build blocks on main state
    /// </summary>
    public ArbitrumTestBlockchainBuilder WithBuildBlocksOnMainState(bool value)
    {
        _buildBlocksOnMainState = value;
        return this;
    }

    /// <summary>
    /// Add DI container configuration
    /// </summary>
    public ArbitrumTestBlockchainBuilder ConfigureContainer(Action<ContainerBuilder> configure)
    {
        _containerConfigurations.Add(configure);
        return this;
    }

    /// <summary>
    /// Add mock service
    /// </summary>
    public ArbitrumTestBlockchainBuilder WithMock<TService, TImplementation>()
        where TImplementation : TService
    {
        return ConfigureContainer(builder =>
            builder.RegisterType<TImplementation>().As<TService>().SingleInstance());
    }

    /// <summary>
    /// Add mock service instance
    /// </summary>
    public ArbitrumTestBlockchainBuilder WithMock<TService>(TService instance)
        where TService : class
    {
        return ConfigureContainer(builder =>
            builder.RegisterInstance(instance).As<TService>());
    }

    /// <summary>
    /// Build the test blockchain
    /// </summary>
    public ArbitrumRpcTestBlockchain Build()
    {
        ChainSpec chainSpec = _chainSpec ?? CreateDefaultChainSpec();
        ConfigureChainSpecForArbosVersion(chainSpec, _arbosVersion);

        ArbitrumConfig config = _arbitrumConfig;

        return ArbitrumRpcTestBlockchain.CreateDefault(
            configurer: CombineConfigurations(),
            chainSpec: chainSpec,
            configureArbitrum: c =>
            {
                c.BlockProcessingTimeout = config.BlockProcessingTimeout;
            }
        );
    }

    private Action<ContainerBuilder>? CombineConfigurations()
    {
        if (_containerConfigurations.Count == 0)
            return null;

        return builder =>
        {
            foreach (Action<ContainerBuilder> config in _containerConfigurations)
                config(builder);
        };
    }

    private static ChainSpec CreateDefaultChainSpec()
    {
        return FullChainSimulationChainSpecProvider.Create();
    }

    private static void ConfigureChainSpecForArbosVersion(ChainSpec chainSpec, ulong version)
    {
        // Configure chain spec parameters for specific ArbOS version
    }
}
```

### Step 2: Add Preset Builders

```csharp
// Infrastructure/ArbitrumTestBlockchainBuilder.cs (continued)
public class ArbitrumTestBlockchainBuilder
{
    // ... existing code ...

    /// <summary>
    /// Create builder for basic integration tests
    /// </summary>
    public static ArbitrumTestBlockchainBuilder ForIntegrationTests()
    {
        return new ArbitrumTestBlockchainBuilder()
            .WithBlockProcessingTimeout(10_000)
            .WithBuildBlocksOnMainState(true);
    }

    /// <summary>
    /// Create builder for precompile tests
    /// </summary>
    public static ArbitrumTestBlockchainBuilder ForPrecompileTests()
    {
        return new ArbitrumTestBlockchainBuilder()
            .WithBlockProcessingTimeout(5_000)
            .WithArbosVersion(ArbosVersion.Latest);
    }

    /// <summary>
    /// Create builder for ArbOS version-specific tests
    /// </summary>
    public static ArbitrumTestBlockchainBuilder ForArbosVersion(ulong version)
    {
        return new ArbitrumTestBlockchainBuilder()
            .WithArbosVersion(version);
    }

    /// <summary>
    /// Create builder for performance tests (longer timeout)
    /// </summary>
    public static ArbitrumTestBlockchainBuilder ForPerformanceTests()
    {
        return new ArbitrumTestBlockchainBuilder()
            .WithBlockProcessingTimeout(60_000);
    }
}
```

### Step 3: Usage Examples

```csharp
// BEFORE - Complex, hard to read
[Test]
public async Task MyTest()
{
    ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
        configurer: builder => builder.RegisterInstance(mockService).As<IService>(),
        chainSpec: FullChainSimulationChainSpecProvider.Create(),
        configureArbitrum: c => c.BlockProcessingTimeout = 10_000
    );
    // ...
}

// AFTER - Fluent, readable
[Test]
public async Task MyTest()
{
    ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
        .WithBlockProcessingTimeout(10_000)
        .WithMock(mockService)
        .Build();
    // ...
}

// AFTER - Using presets
[Test]
public async Task PrecompileTest()
{
    ArbitrumRpcTestBlockchain chain = ArbitrumTestBlockchainBuilder
        .ForPrecompileTests()
        .WithArbosVersion(ArbosVersion.Fifty)
        .Build();
    // ...
}
```

## Verification

### Code Quality Metrics

| Metric | Before | After |
|--------|--------|-------|
| Lines per test setup | 5-10 | 2-4 |
| Setup readability | Low | High |
| Duplicated configuration | Many | None (presets) |

### Test to Verify

```csharp
[Test]
public void Builder_DefaultConfiguration_CreatesValidChain()
{
    // Arrange & Act
    ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder().Build();

    // Assert
    Assert.That(chain.ArbitrumRpcModule, Is.Not.Null);
    Assert.That(chain.BlockTree, Is.Not.Null);
}

[Test]
public void Builder_WithArbosVersion_ConfiguresCorrectVersion()
{
    // Arrange & Act
    ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
        .WithArbosVersion(ArbosVersion.Fifty)
        .Build();

    // Assert
    // Verify ArbOS version in chain state
}

[Test]
public void Builder_WithMock_InjectsMockCorrectly()
{
    // Arrange
    Mock<ICustomService> mockService = new();

    // Act
    ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
        .WithMock(mockService.Object)
        .Build();

    // Assert
    // Verify mock was injected
}
```

## Files to Create/Update

| File | Changes |
|------|---------|
| New: `ArbitrumTestBlockchainBuilder.cs` | Create builder class |
| `ArbitrumRpcTestBlockchain.cs` | Keep existing factory, add builder support |
| Existing tests | Optionally migrate to builder pattern |

## Acceptance Criteria

- [ ] `ArbitrumTestBlockchainBuilder` class created
- [ ] Fluent API for common configuration options
- [ ] Preset builders for common scenarios
- [ ] At least 3 tests migrated to builder pattern
- [ ] Builder tests pass
- [ ] Documentation in builder class

## Migration Strategy

1. **Phase 1**: Create builder alongside existing factory
2. **Phase 2**: Migrate new tests to use builder
3. **Phase 3**: Gradually migrate existing tests (optional)

No breaking changes - existing `CreateDefault` method remains.

## Rollback Plan

Builder is additive - existing tests continue to work. If issues:

```csharp
// Keep using original factory
ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
```
