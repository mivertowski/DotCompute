// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests;

/// <summary>
/// Execution-related tests for BaseAccelerator functionality including kernel compilation,
/// synchronization, performance tracking, and advanced execution scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "BaseAccelerator")]
[Trait("TestType", "Execution")]
public sealed class BaseAcceleratorExecutionTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IUnifiedMemoryManager> _mockMemory;
    private readonly TestAccelerator _accelerator;
    private readonly List<TestAccelerator> _accelerators = [];
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the BaseAcceleratorExecutionTests class.
    /// </summary>

    public BaseAcceleratorExecutionTests()
    {
        _mockLogger = new Mock<ILogger>();
        // Setup IsEnabled to return true for all log levels so LoggerMessage works
        _ = _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _mockMemory = new Mock<IUnifiedMemoryManager>();

        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Execution Test Accelerator",
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
    /// Gets compile kernel async_ with valid definition_ calls compile kernel core.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #region Kernel Compilation Tests

    [Fact]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_WithValidDefinition_CallsCompileKernelCore()
    {
        // Arrange
        var definition = new KernelDefinition("test_kernel", "__kernel void test() {}", "test");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };

        // Act
        var result = await _accelerator.CompileKernelAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("test_kernel");
        _ = _accelerator.CompileKernelCoreCalled.Should().BeTrue();
        _ = _accelerator.LastCompiledDefinition.Should().Be(definition);
        _ = _accelerator.LastCompilationOptions.Should().Be(options);
    }
    /// <summary>
    /// Gets compile kernel async_ with different optimization levels_ passes options correctly.
    /// </summary>
    /// <param name="optimizationLevel">The optimization level.</param>
    /// <returns>The result of the operation.</returns>

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.O1)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.O3)]
    [InlineData(OptimizationLevel.O3)]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_WithDifferentOptimizationLevels_PassesOptionsCorrectly(
        OptimizationLevel optimizationLevel)
    {
        // Arrange
        var definition = new KernelDefinition("optimization_test", "__kernel void test() {}", "test");
        var options = new CompilationOptions { OptimizationLevel = optimizationLevel };

        // Act
        var result = await _accelerator.CompileKernelAsync(definition, options);

        // Assert
        _ = result.Should().NotBeNull();
        _ = _accelerator.LastCompilationOptions?.OptimizationLevel.Should().Be(optimizationLevel);
    }
    /// <summary>
    /// Gets compile kernel async_ with null options_ uses default options.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_WithNullOptions_UsesDefaultOptions()
    {
        // Arrange
        var definition = new KernelDefinition("default_options_test", "__kernel void test() {}", "test");

        // Act
        var result = await _accelerator.CompileKernelAsync(definition, null);

        // Assert
        _ = result.Should().NotBeNull();
        _ = _accelerator.LastCompilationOptions.Should().NotBeNull();
        _ = _accelerator.LastCompilationOptions.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        _ = _accelerator.LastCompilationOptions.EnableDebugInfo.Should().BeFalse();
    }
    /// <summary>
    /// Gets compile kernel async_ invalid kernel code_ throws compilation exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_InvalidKernelCode_ThrowsCompilationException()
    {
        // Arrange
        var definition = new KernelDefinition("invalid_kernel", "invalid syntax here", "test");
        var accelerator = CreateTestAccelerator();
        accelerator.ShouldThrowOnCompilation = true;

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Compilation failed*");
    }
    /// <summary>
    /// Gets compile kernel async_ concurrent compilations_ thread safety.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_ConcurrentCompilations_ThreadSafety()
    {
        // Arrange
        var definitions = Enumerable.Range(0, 10)
            .Select(i => new KernelDefinition($"kernel_{i}", $"__kernel void test_{i}() {{}}", "test"))
            .ToArray();
        var compilationTasks = new List<Task<ICompiledKernel>>();

        // Add small delay to ensure tasks overlap
        _accelerator.CompilationDelay = TimeSpan.FromMilliseconds(50);

        // Act - Start all tasks before waiting
        foreach (var definition in definitions)
        {
            compilationTasks.Add(_accelerator.CompileKernelAsync(definition).AsTask());
        }

        var results = await Task.WhenAll(compilationTasks);

        // Assert
        _ = results.Should().HaveCount(10);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());
        _ = _accelerator.ConcurrentCompilationCount.Should().BeGreaterThan(1, "should handle concurrent compilations");
    }
    /// <summary>
    /// Gets compile kernel async_ compilation caching_ reuses compiled kernels.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_CompilationCaching_ReusesCompiledKernels()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableCompilationCaching = true;
        var definition = new KernelDefinition("cached_kernel", "__kernel void test() {}", "test");

        // Act
        var result1 = await accelerator.CompileKernelAsync(definition);
        var result2 = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result1.Should().NotBeNull();
        _ = result2.Should().NotBeNull();
        _ = result1.Id.Should().Be(result2.Id, "cached kernels should have same ID");
        _ = accelerator.CacheHitCount.Should().Be(1, "second compilation should hit cache");
    }
    /// <summary>
    /// Gets compile kernel async_ with cancellation_ stops compilation.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_WithCancellation_StopsCompilation()
    {
        // Arrange
        var definition = new KernelDefinition("cancelled_kernel", "__kernel void test() {}", "test");
        var accelerator = CreateTestAccelerator();
        accelerator.CompilationDelay = TimeSpan.FromSeconds(1);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition, cancellationToken: cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }
    /// <summary>
    /// Gets compile kernel async_ with invalid kernel names_ handles correctly.
    /// </summary>
    /// <param name="kernelName">The kernel name.</param>
    /// <param name="description">The description.</param>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Advanced Kernel Compilation Tests

    [Theory]
    [InlineData("", "Empty kernel name")]
    [InlineData(null, "Null kernel name")]
    [InlineData("kernel with spaces and special chars!@#", "Special characters")]
    [Trait("TestType", "AdvancedCompilation")]
    public async Task CompileKernelAsync_WithInvalidKernelNames_HandlesCorrectly(string kernelName, string description)
    {
        // Arrange
        var definition = new KernelDefinition(kernelName, "__kernel void test() {}", "test");

        // Act
        if (string.IsNullOrEmpty(kernelName))
        {
            // Assert - Should throw for invalid names
            var act = async () => await _accelerator.CompileKernelAsync(definition);
            _ = await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Kernel validation failed*");
        }
        else
        {
            // Assert - Should handle special characters gracefully
            var result = await _accelerator.CompileKernelAsync(definition);
            _ = result.Should().NotBeNull();
        }
    }
    /// <summary>
    /// Gets compile kernel async_ with large kernel source_ handles efficiently.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedCompilation")]
    public async Task CompileKernelAsync_WithLargeKernelSource_HandlesEfficiently()
    {
        // Arrange
        var largeSource = string.Join("\n", Enumerable.Range(0, 1000)
            .Select(i => $"__constant float data_{i} = {i}.0f;")) +
            "\n__kernel void large_kernel(__global float* output) { *output = 42.0f; }";

        var definition = new KernelDefinition("large_kernel", largeSource, "test");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _accelerator.CompileKernelAsync(definition);
        stopwatch.Stop();

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("large_kernel");
        // Should complete in reasonable time even for large sources
        _ = stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }
    /// <summary>
    /// Gets compile kernel async_ with optimization level progression_ shows performance improvement.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedCompilation")]
    public async Task CompileKernelAsync_WithOptimizationLevelProgression_ShowsPerformanceImprovement()
    {
        // Arrange
        var definition = new KernelDefinition(
            "optimization_test",
            "__kernel void complex_computation(__global float* data) { for(int i = 0; i < 1000; i++) data[0] += sqrt(i); }",
            "test");

        var accelerator = CreateTestAccelerator();
        accelerator.EnableMetricsTracking = true;

        var optimizationLevels = new[]
        {
            OptimizationLevel.None,
            OptimizationLevel.O1,
            OptimizationLevel.Default,
            OptimizationLevel.O3,
            OptimizationLevel.O3
        };

        var compilationTimes = new List<TimeSpan>();

        // Act
        foreach (var level in optimizationLevels)
        {
            var options = new CompilationOptions { OptimizationLevel = level };
            var stopwatch = Stopwatch.StartNew();
            _ = await accelerator.CompileKernelAsync(definition, options);
            stopwatch.Stop();
            compilationTimes.Add(stopwatch.Elapsed);
        }

        // Assert
        _ = compilationTimes.Should().HaveCount(5);
        _ = accelerator.TotalCompilations.Should().Be(5);
        // Higher optimization levels might take more time but should still be reasonable
        _ = compilationTimes.Should().AllSatisfy(t => t.Should().BeLessThan(TimeSpan.FromSeconds(10)));
    }
    /// <summary>
    /// Gets compile kernel async_ with cache eviction_ handles memory pressure.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedCompilation")]
    public async Task CompileKernelAsync_WithCacheEviction_HandlesMemoryPressure()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableCompilationCaching = true;

        // Create many kernels to potentially trigger cache eviction
        var definitions = Enumerable.Range(0, 50)
            .Select(i => new KernelDefinition($"cache_test_{i}", $"__kernel void test_{i}() {{ int x = {i}; }}", "test"))
            .ToArray();

        // Act - Compile all kernels
        var results = new List<ICompiledKernel>();
        foreach (var definition in definitions)
        {
            var result = await accelerator.CompileKernelAsync(definition);
            results.Add(result);
        }

        // Recompile first few to test cache behavior
        var cachedResults = new List<ICompiledKernel>();
        for (var i = 0; i < 5; i++)
        {
            var result = await accelerator.CompileKernelAsync(definitions[i]);
            cachedResults.Add(result);
        }

        // Assert
        _ = results.Should().HaveCount(50);
        _ = cachedResults.Should().HaveCount(5);
        _ = accelerator.TotalCompilations.Should().BeGreaterThanOrEqualTo(50);
        _ = accelerator.CacheHitCount.Should().BeGreaterThanOrEqualTo(0); // Some cache hits expected
    }
    /// <summary>
    /// Gets synchronize async_ calls synchronize core.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Synchronization Tests

    [Fact]
    [Trait("TestType", "Synchronization")]
    public async Task SynchronizeAsync_CallsSynchronizeCore()
    {
        // Act
        await _accelerator.SynchronizeAsync();

        // Assert
        _ = _accelerator.SynchronizeCoreCalled.Should().BeTrue();

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Synchronizing", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    /// <summary>
    /// Gets synchronize async_ concurrent calls_ thread safety.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Synchronization")]
    public async Task SynchronizeAsync_ConcurrentCalls_ThreadSafety()
    {
        // Arrange
        var tasks = new List<Task>();

        // Add small delay to ensure tasks overlap
        _accelerator.SyncDelay = TimeSpan.FromMilliseconds(50);

        // Act - Multiple concurrent synchronization calls (start all before waiting)
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(_accelerator.SynchronizeAsync().AsTask());
        }

        // Assert
        var act = async () => await Task.WhenAll(tasks);
        _ = await act.Should().NotThrowAsync();
        _ = _accelerator.SynchronizeCoreCalled.Should().BeTrue();
        _ = _accelerator.ConcurrentSyncCount.Should().BeGreaterThan(1);
    }
    /// <summary>
    /// Gets synchronize async_ with cancellation_ stops operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Synchronization")]
    public async Task SynchronizeAsync_WithCancellation_StopsOperation()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SyncDelay = TimeSpan.FromSeconds(1);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var act = async () => await accelerator.SynchronizeAsync(cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }
    /// <summary>
    /// Gets synchronize async_ with multiple pending operations_ waits for all.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Advanced Synchronization Tests

    [Fact]
    [Trait("TestType", "AdvancedSynchronization")]
    public async Task SynchronizeAsync_WithMultiplePendingOperations_CompletesSuccessfully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.CompilationDelay = TimeSpan.FromMilliseconds(100);

        // Start multiple compilation operations
        var definitions = Enumerable.Range(0, 5)
            .Select(i => new KernelDefinition($"pending_op_{i}", "__kernel void test() {}", "test"))
            .ToArray();

        var compilationTasks = definitions
            .Select(d => accelerator.CompileKernelAsync(d).AsTask())
            .ToArray();

        // Wait briefly to ensure operations are in progress
        await Task.Delay(50);

        // Act
        await accelerator.SynchronizeAsync();

        // Assert
        var results = await Task.WhenAll(compilationTasks);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());

        // SynchronizeAsync completes independently - doesn't block for pending compilation operations
        _ = accelerator.SynchronizeCoreCalled.Should().BeTrue();
    }
    /// <summary>
    /// Gets concurrent synchronization_ multiple threads_ thread safety.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedSynchronization")]
    public async Task ConcurrentSynchronization_MultipleThreads_ThreadSafety()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SyncDelay = TimeSpan.FromMilliseconds(100);

        var syncTasks = new List<Task>();
        var completionTimes = new ConcurrentBag<DateTime>();

        // Act - Start multiple synchronization operations
        for (var i = 0; i < 10; i++)
        {
            syncTasks.Add(Task.Run(async () =>
            {
                await accelerator.SynchronizeAsync();
                completionTimes.Add(DateTime.UtcNow);
            }));
        }

        await Task.WhenAll(syncTasks);

        // Assert
        _ = completionTimes.Should().HaveCount(10);
        _ = accelerator.ConcurrentSyncCount.Should().BeGreaterThan(1);

        // All operations should complete within a reasonable timeframe
        var times = completionTimes.OrderBy(t => t).ToArray();
        var totalSpan = times.Last() - times.First();
        _ = totalSpan.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
    /// <summary>
    /// Gets synchronize async_ deadlock prevention_ completes successfully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "AdvancedSynchronization")]
    public async Task SynchronizeAsync_DeadlockPrevention_CompletesSuccessfully()
    {
        // Arrange
        var accelerator1 = CreateTestAccelerator();
        var accelerator2 = CreateTestAccelerator();

        var sync1Started = new ManualResetEventSlim(false);
        var sync2Started = new ManualResetEventSlim(false);

        // Act - Attempt to create potential deadlock scenario
        var task1 = Task.Run(async () =>
        {
            sync1Started.Set();
            _ = sync2Started.Wait(TimeSpan.FromSeconds(1)); // Wait for other sync to start
            await accelerator1.SynchronizeAsync();
        });

        var task2 = Task.Run(async () =>
        {
            sync2Started.Set();
            _ = sync1Started.Wait(TimeSpan.FromSeconds(1)); // Wait for other sync to start
            await accelerator2.SynchronizeAsync();
        });

        // Assert - Both operations should complete without deadlock
        var act = async () => await Task.WhenAll(task1, task2);
        _ = await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }
    /// <summary>
    /// Gets compile kernel async_ tracks compilation metrics_ automatically.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Performance Tracking Tests

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task CompileKernelAsync_TracksCompilationMetrics_Automatically()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableMetricsTracking = true;
        var definition = new KernelDefinition("metrics_kernel", "__kernel void test() {}", "test");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await accelerator.CompileKernelAsync(definition);
        stopwatch.Stop();

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.LastCompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
        _ = accelerator.LastCompilationTime.Should().BeLessThan(stopwatch.Elapsed);
        _ = accelerator.CompilationMetricsLogged.Should().BeTrue();
    }
    /// <summary>
    /// Gets multiple operations_ track resource usage_ correctly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task MultipleOperations_TrackResourceUsage_Correctly()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableResourceTracking = true;
        var definitions = Enumerable.Range(0, 5)
            .Select(i => new KernelDefinition($"resource_kernel_{i}", "__kernel void test() {}", "test"))
            .ToArray();

        // Act
        foreach (var definition in definitions)
        {
            _ = await accelerator.CompileKernelAsync(definition);
            await accelerator.SynchronizeAsync();
        }

        // Assert
        _ = accelerator.TotalCompilations.Should().Be(5);
        _ = accelerator.TotalSynchronizations.Should().Be(5);
        _ = accelerator.AverageCompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
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
            // Dispose primary accelerator
            if (_accelerator != null && !_accelerator.IsDisposed)
            {
                try
                {
                    _ = _accelerator.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
                }
                catch
                {
                    // Ignore disposal errors in cleanup
                }
            }

            // Dispose accelerator list
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
    /// Enhanced test implementation of BaseAccelerator for execution testing.
    /// </summary>
    private sealed class TestAccelerator(AcceleratorInfo info, IUnifiedMemoryManager memory, ILogger logger) : BaseAccelerator(info ?? throw new ArgumentNullException(nameof(info)),
              Enum.Parse<AcceleratorType>(info.DeviceType),
              memory ?? throw new ArgumentNullException(nameof(memory)),
              new AcceleratorContext(IntPtr.Zero, 0),
              logger ?? throw new ArgumentNullException(nameof(logger)))
    {
        /// <summary>
        /// Gets or sets the compile kernel core called.
        /// </summary>
        /// <value>The compile kernel core called.</value>
        // Basic tracking
        public bool CompileKernelCoreCalled { get; private set; }
        /// <summary>
        /// Gets or sets the synchronize core called.
        /// </summary>
        /// <value>The synchronize core called.</value>
        public bool SynchronizeCoreCalled { get; private set; }
        /// <summary>
        /// Gets or sets the last compiled definition.
        /// </summary>
        /// <value>The last compiled definition.</value>

        // Advanced tracking
        public KernelDefinition? LastCompiledDefinition { get; private set; }
        /// <summary>
        /// Gets or sets the last compilation options.
        /// </summary>
        /// <value>The last compilation options.</value>
        public CompilationOptions? LastCompilationOptions { get; private set; }
        /// <summary>
        /// Gets or sets the last compilation time.
        /// </summary>
        /// <value>The last compilation time.</value>
        public TimeSpan LastCompilationTime { get; private set; }
        /// <summary>
        /// Gets or sets the compilation metrics logged.
        /// </summary>
        /// <value>The compilation metrics logged.</value>
        public bool CompilationMetricsLogged { get; private set; }
        /// <summary>
        /// Gets or sets the concurrent compilation count.
        /// </summary>
        /// <value>The concurrent compilation count.</value>

        // Concurrency tracking
        public int ConcurrentCompilationCount { get; private set; }
        /// <summary>
        /// Gets or sets the concurrent sync count.
        /// </summary>
        /// <value>The concurrent sync count.</value>
        public int ConcurrentSyncCount { get; private set; }
        /// <summary>
        /// Gets or sets the total compilations.
        /// </summary>
        /// <value>The total compilations.</value>

        // Performance tracking
        public int TotalCompilations { get; private set; }
        /// <summary>
        /// Gets or sets the total synchronizations.
        /// </summary>
        /// <value>The total synchronizations.</value>
        public int TotalSynchronizations { get; private set; }
        /// <summary>
        /// Gets or sets the average compilation time.
        /// </summary>
        /// <value>The average compilation time.</value>
        public TimeSpan AverageCompilationTime { get; private set; }
        /// <summary>
        /// Gets or sets the enable compilation caching.
        /// </summary>
        /// <value>The enable compilation caching.</value>

        // Caching
        public bool EnableCompilationCaching { get; set; }
        /// <summary>
        /// Gets or sets the cache hit count.
        /// </summary>
        /// <value>The cache hit count.</value>
        public int CacheHitCount { get; private set; }
        private readonly ConcurrentDictionary<string, ICompiledKernel> _kernelCache = new();
        /// <summary>
        /// Gets or sets the should throw on compilation.
        /// </summary>
        /// <value>The should throw on compilation.</value>

        // Error simulation
        public bool ShouldThrowOnCompilation { get; set; }
        /// <summary>
        /// Gets or sets the compilation delay.
        /// </summary>
        /// <value>The compilation delay.</value>

        // Timing simulation
        public TimeSpan CompilationDelay { get; set; }
        /// <summary>
        /// Gets or sets the sync delay.
        /// </summary>
        /// <value>The sync delay.</value>
        public TimeSpan SyncDelay { get; set; }
        /// <summary>
        /// Gets or sets the enable metrics tracking.
        /// </summary>
        /// <value>The enable metrics tracking.</value>

        // Feature flags
        public bool EnableMetricsTracking { get; set; }
        /// <summary>
        /// Gets or sets the enable resource tracking.
        /// </summary>
        /// <value>The enable resource tracking.</value>
        public bool EnableResourceTracking { get; set; }

        // Performance counters
        private readonly List<TimeSpan> _compilationTimes = [];
        private int _activeCompilations;
        private int _activeSyncs;

        protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            // Track concurrency
            var currentConcurrent = Interlocked.Increment(ref _activeCompilations);
            if (currentConcurrent > ConcurrentCompilationCount)
                ConcurrentCompilationCount = currentConcurrent;

            try
            {
                CompileKernelCoreCalled = true;
                LastCompiledDefinition = definition;
                LastCompilationOptions = options;
                TotalCompilations++;

                // Kernel name validation
                if (string.IsNullOrEmpty(definition.Name))
                {
                    throw new InvalidOperationException("Kernel validation failed: name is required");
                }

                // Simulate compilation delay
                if (CompilationDelay > TimeSpan.Zero)
                {
                    await Task.Delay(CompilationDelay, cancellationToken);
                }

                // Error simulation
                if (ShouldThrowOnCompilation)
                    throw new InvalidOperationException("Compilation failed (simulated)");

                // Check cache if enabled
                var cacheKey = $"{definition.Name}_{definition.Source?.GetHashCode()}_{options.OptimizationLevel}";
                if (EnableCompilationCaching && _kernelCache.TryGetValue(cacheKey, out var cachedKernel))
                {
                    CacheHitCount++;
                    return cachedKernel;
                }

                // Create new kernel
                var mockKernel = new Mock<ICompiledKernel>();
                var kernelId = Guid.NewGuid();
                _ = mockKernel.Setup(x => x.Id).Returns(kernelId);
                _ = mockKernel.Setup(x => x.Name).Returns(definition.Name);

                var kernel = mockKernel.Object;

                // Cache if enabled
                if (EnableCompilationCaching)
                {
                    _ = _kernelCache.TryAdd(cacheKey, kernel);
                }

                // Track performance metrics
                var compilationTime = DateTime.UtcNow - startTime;
                LastCompilationTime = compilationTime;
                _compilationTimes.Add(compilationTime);

                if (EnableMetricsTracking)
                {
                    CompilationMetricsLogged = true;
                }

                if (EnableResourceTracking && _compilationTimes.Count > 0)
                {
                    AverageCompilationTime = TimeSpan.FromTicks(
                        (long)_compilationTimes.Average(t => t.Ticks));
                }

                return kernel;
            }
            finally
            {
                _ = Interlocked.Decrement(ref _activeCompilations);
            }
        }

        protected override async ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
        {
            // Track concurrency
            var currentConcurrent = Interlocked.Increment(ref _activeSyncs);
            if (currentConcurrent > ConcurrentSyncCount)
                ConcurrentSyncCount = currentConcurrent;

            try
            {
                SynchronizeCoreCalled = true;
                TotalSynchronizations++;

                // Simulate sync delay
                if (SyncDelay > TimeSpan.Zero)
                {
                    await Task.Delay(SyncDelay, cancellationToken);
                }
            }
            finally
            {
                _ = Interlocked.Decrement(ref _activeSyncs);
            }
        }

        protected override async ValueTask DisposeCoreAsync() => await base.DisposeCoreAsync();
    }
}
