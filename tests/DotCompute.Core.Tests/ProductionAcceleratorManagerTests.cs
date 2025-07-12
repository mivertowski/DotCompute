// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Abstractions;
using DotCompute.Backends.CPU.Accelerators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Tests;

/// <summary>
/// Production tests for AcceleratorManager functionality using real implementations.
/// </summary>
public class ProductionAcceleratorManagerTests : IAsyncLifetime
{
    private readonly ProductionAcceleratorManager _manager;
    private readonly ILogger<ProductionAcceleratorManager> _logger;

    public ProductionAcceleratorManagerTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ProductionAcceleratorManager>();
        _manager = new ProductionAcceleratorManager(_logger);
    }

    [Fact]
    public async Task InitializeAsync_ShouldDiscoverCpuAccelerator()
    {
        // Act
        await _manager.InitializeAsync();

        // Assert
        _manager.Count.Should().BeGreaterThan(0);
        _manager.Default.Should().NotBeNull();
        _manager.Default.Info.Type.Should().Be(AcceleratorType.Cpu);
    }

    [Fact]
    public async Task GetDefaultAcceleratorAsync_ShouldReturnCpuAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var defaultAccelerator = _manager.Default;

        // Assert
        defaultAccelerator.Should().NotBeNull();
        defaultAccelerator.Info.Type.Should().Be(AcceleratorType.Cpu);
        defaultAccelerator.Info.Name.Should().NotBeNullOrEmpty();
        defaultAccelerator.Info.MaxComputeUnits.Should().BeGreaterThan(0);
        defaultAccelerator.Memory.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAcceleratorById_WithValidId_ShouldReturnAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();
        var expectedId = _manager.Default.Info.Id;

        // Act
        var accelerator = _manager.GetAcceleratorById(expectedId);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator!.Info.Id.Should().Be(expectedId);
    }

    [Fact]
    public async Task GetAcceleratorById_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var accelerator = _manager.GetAcceleratorById("non-existent-id");

        // Assert
        accelerator.Should().BeNull();
    }

    [Fact]
    public async Task GetAcceleratorsByType_ShouldReturnCpuAccelerators()
    {
        // Arrange
        await _manager.InitializeAsync();

        // Act
        var cpuAccelerators = _manager.GetAcceleratorsByType(AcceleratorType.Cpu);

        // Assert
        cpuAccelerators.Should().NotBeEmpty();
        cpuAccelerators.Should().AllSatisfy(acc => 
            acc.Info.Type.Should().Be(AcceleratorType.Cpu));
    }

    [Fact]
    public async Task SelectBest_WithCpuPreference_ShouldReturnCpuAccelerator()
    {
        // Arrange
        await _manager.InitializeAsync();
        var criteria = new AcceleratorSelectionCriteria
        {
            PreferredType = AcceleratorType.Cpu,
            MinimumMemory = 1024 * 1024 // 1MB
        };

        // Act
        var selectedAccelerator = _manager.SelectBest(criteria);

        // Assert
        selectedAccelerator.Should().NotBeNull();
        selectedAccelerator!.Info.Type.Should().Be(AcceleratorType.Cpu);
        selectedAccelerator.Info.GlobalMemorySize.Should().BeGreaterOrEqualTo(1024 * 1024);
    }

    [Fact]
    public async Task SelectBest_WithImpossibleRequirements_ShouldReturnNull()
    {
        // Arrange
        await _manager.InitializeAsync();
        var criteria = new AcceleratorSelectionCriteria
        {
            MinimumMemory = long.MaxValue, // Impossible requirement
            PreferredType = AcceleratorType.Cuda // Not available in test
        };

        // Act
        var selectedAccelerator = _manager.SelectBest(criteria);

        // Assert
        selectedAccelerator.Should().BeNull();
    }

    [Fact]
    public async Task CreateContext_ShouldCreateValidContext()
    {
        // Arrange
        await _manager.InitializeAsync();
        var accelerator = _manager.Default;

        // Act
        var context = _manager.CreateContext(accelerator);

        // Assert
        context.Should().NotBeNull();
        context.Accelerator.Should().BeSameAs(accelerator);
    }

    [Fact]
    public async Task RefreshAsync_ShouldRediscoverAccelerators()
    {
        // Arrange
        await _manager.InitializeAsync();
        var initialCount = _manager.Count;

        // Act
        await _manager.RefreshAsync();

        // Assert
        _manager.Count.Should().Be(initialCount); // Should remain the same for CPU
        _manager.Default.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterProvider_ShouldAddNewProvider()
    {
        // Arrange
        await _manager.InitializeAsync();
        var customProvider = new TestAcceleratorProvider();

        // Act
        _manager.RegisterProvider(customProvider);
        await _manager.RefreshAsync();

        // Assert
        _manager.AvailableAccelerators.Should().Contain(acc => 
            acc.Info.Name == "TestAccelerator");
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        await _manager.InitializeAsync();
        const int taskCount = 10;
        var tasks = new Task[taskCount];

        // Act
        for (int i = 0; i < taskCount; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var accelerator = _manager.Default;
                var context = _manager.CreateContext(accelerator);
                await accelerator.SynchronizeAsync();
                
                // Simulate some work
                await Task.Delay(10);
                
                return accelerator.Info.Name;
            });
        }

        // Assert - Should not throw
        var results = await Task.WhenAll(tasks.Cast<Task<string>>());
        results.Should().AllSatisfy(name => name.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task AcceleratorMemoryManager_ShouldWorkCorrectly()
    {
        // Arrange
        await _manager.InitializeAsync();
        var accelerator = _manager.Default;
        var memoryManager = accelerator.Memory;

        // Act
        var buffer = await memoryManager.AllocateAsync(1024);
        
        try
        {
            // Test basic operations
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            await buffer.CopyFromHostAsync<byte>(testData);
            
            var resultData = new byte[5];
            await buffer.CopyToHostAsync<byte>(resultData);

            // Assert
            resultData.Should().BeEquivalentTo(testData);
            buffer.SizeInBytes.Should().Be(1024);
        }
        finally
        {
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task AcceleratorCapabilities_ShouldBePopulated()
    {
        // Arrange
        await _manager.InitializeAsync();
        var accelerator = _manager.Default;

        // Act & Assert
        var info = accelerator.Info;
        info.Capabilities.Should().NotBeNull();
        info.Capabilities.Should().ContainKey("SimdWidth");
        info.Capabilities.Should().ContainKey("ThreadCount");
        info.Capabilities.Should().ContainKey("SimdInstructionSets");
        
        var simdWidth = info.Capabilities["SimdWidth"];
        simdWidth.Should().BeOfType<int>();
        ((int)simdWidth).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task KernelCompilation_ShouldWork()
    {
        // Arrange
        await _manager.InitializeAsync();
        var accelerator = _manager.Default;
        
        var kernelDef = new KernelDefinition
        {
            Name = "TestKernel",
            Source = "test kernel source",
            EntryPoint = "main"
        };

        // Act
        var compiledKernel = await accelerator.CompileKernelAsync(kernelDef);

        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Definition.Should().Be(kernelDef);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _manager.DisposeAsync();
    }
}

/// <summary>
/// Production implementation of IAcceleratorManager for testing.
/// </summary>
internal class ProductionAcceleratorManager : IAcceleratorManager
{
    private readonly ILogger<ProductionAcceleratorManager> _logger;
    private readonly List<IAccelerator> _accelerators = new();
    private readonly List<IAcceleratorProvider> _providers = new();
    private bool _isInitialized = false;

    public ProductionAcceleratorManager(ILogger<ProductionAcceleratorManager> logger)
    {
        _logger = logger;
        
        // Register default CPU provider
        _providers.Add(new CpuAcceleratorProvider());
    }

    public IAccelerator Default => _accelerators.FirstOrDefault() ?? 
        throw new InvalidOperationException("No accelerators available. Call InitializeAsync first.");

    public IReadOnlyList<IAccelerator> AvailableAccelerators => _accelerators.AsReadOnly();

    public int Count => _accelerators.Count;

    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        _accelerators.Clear();
        
        foreach (var provider in _providers)
        {
            try
            {
                var accelerators = await provider.DiscoverAsync(cancellationToken);
                _accelerators.AddRange(accelerators);
                _logger.LogInformation("Discovered {Count} accelerators from provider {Provider}", 
                    accelerators.Count(), provider.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to discover accelerators from provider {Provider}", 
                    provider.Name);
            }
        }

        if (_accelerators.Count == 0)
        {
            throw new InvalidOperationException("No accelerators discovered");
        }

        _isInitialized = true;
        _logger.LogInformation("Initialized with {Count} accelerators", _accelerators.Count);
    }

    public IAccelerator GetAccelerator(int index)
    {
        if (index < 0 || index >= _accelerators.Count)
            throw new IndexOutOfRangeException($"Index {index} is out of range [0, {_accelerators.Count})");
        
        return _accelerators[index];
    }

    public IAccelerator? GetAcceleratorById(string id)
    {
        return _accelerators.FirstOrDefault(acc => acc.Info.Id == id);
    }

    public IEnumerable<IAccelerator> GetAcceleratorsByType(AcceleratorType type)
    {
        return _accelerators.Where(acc => acc.Info.Type == type);
    }

    public IAccelerator? SelectBest(AcceleratorSelectionCriteria criteria)
    {
        var candidates = _accelerators.AsEnumerable();

        if (criteria.PreferredType.HasValue)
        {
            candidates = candidates.Where(acc => acc.Info.Type == criteria.PreferredType.Value);
        }

        if (criteria.MinimumMemory.HasValue)
        {
            candidates = candidates.Where(acc => acc.Info.GlobalMemorySize >= criteria.MinimumMemory.Value);
        }

        if (criteria.RequiredFeatures.HasValue)
        {
            // Feature checking would be implemented based on specific requirements
        }

        if (criteria.CustomScorer != null)
        {
            return candidates.OrderByDescending(criteria.CustomScorer).FirstOrDefault();
        }

        // Default scoring: prefer dedicated devices, then by memory size
        return candidates
            .OrderByDescending(acc => acc.Info.GlobalMemorySize)
            .FirstOrDefault();
    }

    public AcceleratorContext CreateContext(IAccelerator accelerator)
    {
        return new AcceleratorContext(accelerator);
    }

    public void RegisterProvider(IAcceleratorProvider provider)
    {
        _providers.Add(provider);
    }

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        _isInitialized = false;
        await InitializeAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var accelerator in _accelerators)
        {
            await accelerator.DisposeAsync();
        }
        _accelerators.Clear();
    }
}

/// <summary>
/// CPU accelerator provider for production testing.
/// </summary>
internal class CpuAcceleratorProvider : IAcceleratorProvider
{
    public string Name => "CPU";
    public AcceleratorType[] SupportedTypes => new[] { AcceleratorType.Cpu };

    public async ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<CpuAccelerator>();
        
        var options = Options.Create(new CpuAcceleratorOptions());
        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions());
        
        var accelerator = new CpuAccelerator(options, threadPoolOptions, logger);
        
        return new[] { accelerator };
    }

    public async ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        var accelerators = await DiscoverAsync(cancellationToken);
        return accelerators.FirstOrDefault(acc => acc.Info.Id == info.Id) ??
            throw new ArgumentException($"No accelerator found with ID {info.Id}");
    }
}

/// <summary>
/// Test accelerator provider for testing provider registration.
/// </summary>
internal class TestAcceleratorProvider : IAcceleratorProvider
{
    public string Name => "Test";
    public AcceleratorType[] SupportedTypes => new[] { AcceleratorType.Cpu };

    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var accelerator = new TestAccelerator();
        return ValueTask.FromResult<IEnumerable<IAccelerator>>(new[] { accelerator });
    }

    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IAccelerator>(new TestAccelerator());
    }
}

/// <summary>
/// Simple test accelerator for provider testing.
/// </summary>
internal class TestAccelerator : IAccelerator
{
    public AcceleratorInfo Info { get; } = new AcceleratorInfo
    {
        Id = "test-accelerator",
        Name = "TestAccelerator",
        Type = AcceleratorType.Cpu,
        Vendor = "Test",
        DriverVersion = "1.0.0",
        MaxComputeUnits = 1,
        MaxWorkGroupSize = 1,
        MaxMemoryAllocation = 1024,
        GlobalMemorySize = 1024 * 1024,
        LocalMemorySize = 1024,
        SupportsDoublePrecision = false,
        Capabilities = new Dictionary<string, object>()
    };

    public IMemoryManager Memory { get; } = new CpuMemoryManager();

    public ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<ICompiledKernel>(new TestCompiledKernel(definition));
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Simple test compiled kernel.
/// </summary>
internal class TestCompiledKernel : ICompiledKernel
{
    public TestCompiledKernel(KernelDefinition definition)
    {
        Definition = definition;
    }

    public KernelDefinition Definition { get; }

    public ValueTask<object> ExecuteAsync(object[] parameters, WorkGroupSize workGroupSize, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<object>("test result");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}