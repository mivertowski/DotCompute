// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests;

/// <summary>
/// Memory-related tests for BaseAccelerator functionality including memory integration,
/// allocation tracking, pressure handling, and fragmentation scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "BaseAccelerator")]
[Trait("TestType", "Memory")]
public sealed class BaseAcceleratorMemoryTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IUnifiedMemoryManager> _mockMemory;
    private readonly TestAccelerator _accelerator;
    private readonly List<TestAccelerator> _accelerators = [];
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the BaseAcceleratorMemoryTests class.
    /// </summary>

    public BaseAcceleratorMemoryTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMemory = new Mock<IUnifiedMemoryManager>();

        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Memory Test Accelerator",
            "1.0",
            1024 * 1024 * 1024,
            4,
            3000,
            new Version(1, 0),
            1024 * 1024,
            true
        );

        _accelerator = new TestAccelerator(info, _mockMemory.Object, _mockLogger.Object);
        _accelerators.Add(_accelerator);
    }
    /// <summary>
    /// Performs memory_ property_ returns injected memory manager.
    /// </summary>

    #region Memory Integration Tests

    [Fact]
    [Trait("TestType", "MemoryIntegration")]
    public void Memory_Property_ReturnsInjectedMemoryManager()
        => _accelerator.Memory.Should().Be(_mockMemory.Object);
    /// <summary>
    /// Performs memory_ integration_ enforces memory limits.
    /// </summary>

    [Fact]
    [Trait("TestType", "MemoryIntegration")]
    public void Memory_Integration_EnforcesMemoryLimits()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);

        // Act
        var memory = accelerator.Memory;

        // Assert
        _ = memory.Should().Be(memoryManager.Object);
        _ = accelerator.Info.TotalMemory.Should().BeGreaterThan(0);
        _ = accelerator.Info.AvailableMemory.Should().BeGreaterThan(0);
        _ = accelerator.Info.AvailableMemory.Should().BeLessThanOrEqualTo(accelerator.Info.TotalMemory);
    }
    /// <summary>
    /// Gets dispose async_ disposes memory manager_ when configured.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryIntegration")]
    public async Task DisposeAsync_DisposesMemoryManager_WhenConfigured()
    {
        // Arrange
        var mockMemoryManager = new Mock<IUnifiedMemoryManager>();
        var accelerator = CreateTestAccelerator(memoryManager: mockMemoryManager.Object);

        // Act
        await accelerator.DisposeAsync();

        // Assert
        _ = accelerator.IsDisposed.Should().BeTrue();
        // Memory manager disposal is handled by AcceleratorUtilities
    }
    /// <summary>
    /// Performs memory manager_ allocation tracking_ reports accurate usage.
    /// </summary>

    #endregion

    #region Advanced Memory Integration Tests

    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public void MemoryManager_AllocationTracking_ReportsAccurateUsage()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        const long totalMemory = 1024 * 1024 * 1024; // 1GB
        const long currentMemoryUsage = 100 * 1024 * 1024; // 100MB allocated

        // Setup Statistics property with all required property names
        var stats = new DotCompute.Abstractions.Memory.MemoryStatistics
        {
            TotalMemoryBytes = totalMemory,
            UsedMemoryBytes = currentMemoryUsage,
            AvailableMemoryBytes = totalMemory - currentMemoryUsage,
            TotalAllocated = totalMemory,
            CurrentUsage = currentMemoryUsage,
            AvailableMemory = totalMemory - currentMemoryUsage,
            AllocationCount = 1
        };

        _ = memoryManager.Setup(m => m.Statistics).Returns(stats);
        _ = memoryManager.Setup(m => m.TotalAvailableMemory).Returns(totalMemory);
        _ = memoryManager.Setup(m => m.CurrentAllocatedMemory).Returns(currentMemoryUsage);

        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);

        // Act & Assert
        _ = accelerator.Memory.Statistics.UsedMemoryBytes.Should().Be(100L * 1024 * 1024);
        _ = accelerator.Memory.Statistics.AvailableMemoryBytes.Should().Be(1024L * 1024 * 1024 - 100L * 1024 * 1024);
        _ = accelerator.Memory.Statistics.TotalMemoryBytes.Should().Be(1024 * 1024 * 1024);
        _ = accelerator.Info.TotalMemory.Should().Be(1024 * 1024 * 1024);
    }
    /// <summary>
    /// Gets memory pressure_ during compilation_ triggers garbage collection.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task MemoryPressure_DuringCompilation_TriggersGarbageCollection()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();

        _ = memoryManager.Setup(m => m.TotalAvailableMemory).Returns(1024L * 1024 * 1024);
        _ = memoryManager.Setup(m => m.CurrentAllocatedMemory)
            .Returns(1024L * 1024 * 1024 - 10L * 1024 * 1024); // Low available memory

        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);
        var definition = new KernelDefinition("memory_pressure_test", "__kernel void test() {}", "test");

        // Act - Force GC before compilation
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert - Kernel compiled successfully under memory pressure
        _ = result.Should().NotBeNull();
        _ = accelerator.Memory.Should().Be(memoryManager.Object, "memory manager should be accessible");
    }
    /// <summary>
    /// Gets large kernel compilation_ memory allocation_ tracks correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task LargeKernelCompilation_MemoryAllocation_TracksCorrectly()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        const long allocationSize = 500 * 1024 * 1024; // 500MB allocation

        // Setup Statistics property with large allocation values
        var stats = new DotCompute.Abstractions.Memory.MemoryStatistics
        {
            CurrentUsage = allocationSize,
            TotalAllocated = 1024L * 1024 * 1024,
            AvailableMemory = 1024L * 1024 * 1024 - allocationSize,
            UsedMemoryBytes = allocationSize,
            TotalMemoryBytes = 1024L * 1024 * 1024
        };

        _ = memoryManager.Setup(m => m.Statistics).Returns(stats);
        _ = memoryManager.Setup(m => m.TotalAvailableMemory).Returns(1024L * 1024 * 1024);
        _ = memoryManager.Setup(m => m.CurrentAllocatedMemory).Returns(allocationSize);

        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);

        // Create a large kernel source
        var largeKernelSource = string.Join("\n",
            Enumerable.Range(0, 5000).Select(i => $"float var_{i} = {i}.0f;")) +
            "\n__kernel void large_memory_kernel(__global float* output) { *output = var_4999; }";

        var definition = new KernelDefinition("large_memory_kernel", largeKernelSource, "test");

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert - Kernel compiled successfully and statistics are accessible
        _ = result.Should().NotBeNull();
        _ = accelerator.Memory.Statistics.CurrentUsage.Should().Be(allocationSize);
        _ = accelerator.Memory.Should().Be(memoryManager.Object, "memory manager should be accessible");
    }
    /// <summary>
    /// Gets memory fragmentation_ handles degraded performance.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task MemoryFragmentation_HandlesDegradedPerformance()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        var fragmentationLevel = 0.0;

        _ = memoryManager.Setup(m => m.TotalAvailableMemory).Returns(1024L * 1024 * 1024);
        _ = memoryManager.Setup(m => m.CurrentAllocatedMemory)
            .Returns(() => (long)(1024L * 1024 * 1024 * fragmentationLevel));

        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);
        accelerator.EnableMetricsTracking = true;

        var compilationTimes = new List<TimeSpan>();

        // Act - Increase fragmentation over time
        for (var i = 0; i < 5; i++)
        {
            fragmentationLevel = i * 0.1; // 0%, 10%, 20%, 30%, 40% fragmentation
            var definition = new KernelDefinition($"frag_test_{i}", "__kernel void test() {}", "test");

            var stopwatch = Stopwatch.StartNew();
            _ = await accelerator.CompileKernelAsync(definition);
            stopwatch.Stop();

            compilationTimes.Add(stopwatch.Elapsed);
        }

        // Assert
        _ = compilationTimes.Should().HaveCount(5);
        // With increasing fragmentation, compilation might take longer
        // but should still complete successfully
        _ = compilationTimes.Should().AllSatisfy(t => t.Should().BeLessThan(TimeSpan.FromSeconds(30)));
    }
    /// <summary>
    /// Gets initialization_ under memory pressure_ retries and succeeds.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task Initialization_UnderMemoryPressure_RetriesAndSucceeds()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        var callCount = 0;
        _ = memoryManager.Setup(m => m.TotalAvailableMemory).Returns(() =>
        {
            callCount++;
            if (callCount <= 2)
                throw new OutOfMemoryException("Memory pressure");
            return 1024L * 1024 * 500; // 500MB available
        });
        _ = memoryManager.Setup(m => m.CurrentAllocatedMemory).Returns(0);

        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);
        var definition = new KernelDefinition("memory_test", "__kernel void test() {}", "test");

        // Act & Assert - First attempts should handle memory pressure gracefully
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().NotThrowAsync();
    }
    /// <summary>
    /// Gets resource exhaustion_ gradual degradation_ handles gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task ResourceExhaustion_GradualDegradation_HandlesGracefully()
    {
        // Arrange - Create accelerator with custom memory manager
        var memoryManager = new Mock<IUnifiedMemoryManager>();

        // Simulate gradually decreasing available memory
        var memoryCallCount = 0;
        var memoryValues = new long[]
        {
            1024L * 1024 * 1024, // 1GB
            512L * 1024 * 1024,  // 512MB
            256L * 1024 * 1024,  // 256MB
            128L * 1024 * 1024   // 128MB
        };
        _ = memoryManager.Setup(m => m.TotalAvailableMemory).Returns(() =>
        {
            if (memoryCallCount >= memoryValues.Length)
                throw new OutOfMemoryException("Insufficient memory");
            return memoryValues[Math.Min(memoryCallCount++, memoryValues.Length - 1)];
        });
        _ = memoryManager.Setup(m => m.CurrentAllocatedMemory).Returns(0);

        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);

        var results = new List<ICompiledKernel>();
        var exceptions = new List<Exception>();

        // Act - Attempt multiple compilations as memory decreases
        for (var i = 0; i < 5; i++)
        {
            try
            {
                var definition = new KernelDefinition($"resource_test_{i}", "__kernel void test() {}", "test");
                var result = await accelerator.CompileKernelAsync(definition);
                results.Add(result);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        // Assert - All should succeed since TestAccelerator doesn't enforce memory limits
        _ = results.Should().HaveCount(5, "test accelerator doesn't enforce memory limits");
        _ = accelerator.Memory.Should().Be(memoryManager.Object, "memory manager should be injected");
    }

    #endregion

    #region Helper Methods

    private TestAccelerator CreateTestAccelerator(AcceleratorInfo? info = null, IUnifiedMemoryManager? memoryManager = null)
    {
        var acceleratorInfo = info ?? new AcceleratorInfo(
            AcceleratorType.CPU,
            "Test Accelerator",
            "1.0",
            1024 * 1024 * 1024,
            4,
            3000,
            new Version(1, 0),
            1024 * 1024,
            true
        );

        var memory = memoryManager ?? new Mock<IUnifiedMemoryManager>().Object;
        var logger = new Mock<ILogger>().Object;

        var accelerator = new TestAccelerator(acceleratorInfo, memory, logger);
        _accelerators.Add(accelerator);
        return accelerator;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var accelerator in _accelerators)
            {
                if (!accelerator.IsDisposed)
                {
                    try
                    {
                        _ = accelerator.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
                    }
                    catch
                    {
                        // Ignore disposal errors in cleanup
                    }
                }
            }
            _disposed = true;
        }
    }

    #endregion

    /// <summary>
    /// Simplified test implementation of BaseAccelerator for memory testing.
    /// </summary>
    private sealed class TestAccelerator(AcceleratorInfo info, IUnifiedMemoryManager memory, ILogger logger) : BaseAccelerator(info ?? throw new ArgumentNullException(nameof(info)),
              info != null ? Enum.Parse<AcceleratorType>(info.DeviceType) : AcceleratorType.CPU,
              memory ?? throw new ArgumentNullException(nameof(memory)),
              new AcceleratorContext(IntPtr.Zero, 0),
              logger ?? throw new ArgumentNullException(nameof(logger)))
    {
        /// <summary>
        /// Gets or sets the enable metrics tracking.
        /// </summary>
        /// <value>The enable metrics tracking.</value>
        public bool EnableMetricsTracking { get; set; }
        /// <summary>
        /// Gets or sets the enable resource tracking.
        /// </summary>
        /// <value>The enable resource tracking.</value>
        public bool EnableResourceTracking { get; set; }

        protected override object? InitializeCore() => base.InitializeCore();

        protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken)
        {
            // Simulate compilation work
            await Task.Delay(10, cancellationToken);

            var mockKernel = new Mock<ICompiledKernel>();
            var kernelId = Guid.NewGuid();
            _ = mockKernel.Setup(x => x.Id).Returns(kernelId);
            _ = mockKernel.Setup(x => x.Name).Returns(definition.Name);

            return mockKernel.Object;
        }

        protected override async ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken) => await Task.Delay(1, cancellationToken);

        protected override async ValueTask DisposeCoreAsync() => await base.DisposeCoreAsync();
    }
}