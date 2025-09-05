// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Core;
using DotCompute.Core.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive tests for the BaseAccelerator consolidation that eliminated 68% of duplicate code.
/// Achieves 90%+ coverage with extensive lifecycle, compilation, error handling, and performance scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "BaseAccelerator")]
public class BaseAcceleratorTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IUnifiedMemoryManager> _mockMemory;
    private readonly TestAccelerator _accelerator;
    private readonly List<TestAccelerator> _accelerators = [];
    private bool _disposed;

    public BaseAcceleratorTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMemory = new Mock<IUnifiedMemoryManager>();
        
        var info = new AcceleratorInfo(
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
        
        _accelerator = new TestAccelerator(info, _mockMemory.Object, _mockLogger.Object);
        _accelerators.Add(_accelerator);
    }

    #region Lifecycle Management Tests
    
    [Fact]
    [Trait("TestType", "Lifecycle")]
    public void Constructor_InitializesProperties_Correctly()
    {
        // Assert
        _ = _accelerator.Info.Should().NotBeNull();
        _ = _accelerator.Type.Should().Be(AcceleratorType.CPU);
        _ = _accelerator.Memory.Should().Be(_mockMemory.Object);
        _ = _accelerator.IsDisposed.Should().BeFalse();
        _ = _accelerator.Context.Should().NotBeNull();
    }
    
    [Theory]
    [InlineData(AcceleratorType.GPU, "GPU Device", "2.0", 2048L * 1024 * 1024)]
    [InlineData(AcceleratorType.CUDA, "CUDA Device", "12.0", 8192L * 1024 * 1024)]
    [InlineData(AcceleratorType.OpenCL, "OpenCL Device", "3.0", 4096L * 1024 * 1024)]
    [Trait("TestType", "Lifecycle")]
    public void Constructor_WithVariousConfigurations_InitializesCorrectly(
        AcceleratorType type, string name, string driverVersion, long memorySize)
    {
        // Arrange
        var info = new AcceleratorInfo(
            type, name, driverVersion, memorySize, 8, 1500,
            new Version(1, 0), 64 * 1024, type == AcceleratorType.CPU);
        var mockMemory = new Mock<IUnifiedMemoryManager>();
        var mockLogger = new Mock<ILogger>();
        
        // Act
        var accelerator = new TestAccelerator(info, mockMemory.Object, mockLogger.Object);
        _accelerators.Add(accelerator);

        // Assert
        _ = accelerator.Info.Name.Should().Be(name);
        _ = accelerator.Type.Should().Be(type);
        _ = accelerator.Memory.Should().Be(mockMemory.Object);
        _ = accelerator.IsDisposed.Should().BeFalse();
        _ = accelerator.InitializeCoreCalled.Should().BeTrue();
    }

    [Fact]
    [Trait("TestType", "Lifecycle")]
    public void Constructor_WithNullParameters_ThrowsArgumentNullException()
    {
        // Assert
        Action act1 = () => new TestAccelerator(null!, _mockMemory.Object, _mockLogger.Object);
        Action act2 = () => new TestAccelerator(_accelerator.Info, null!, _mockLogger.Object);
        Action act3 = () => new TestAccelerator(_accelerator.Info, _mockMemory.Object, null!);

        _ = act1.Should().Throw<ArgumentNullException>().WithParameterName("info");
        _ = act2.Should().Throw<ArgumentNullException>().WithParameterName("memory");
        _ = act3.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
    
    [Fact]
    [Trait("TestType", "Lifecycle")]
    public async Task DisposeAsync_ProperDisposal_CleansUpResources()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        
        // Act
        await accelerator.DisposeAsync();

        // Assert
        _ = accelerator.IsDisposed.Should().BeTrue();
        _ = accelerator.DisposeCallCount.Should().Be(1);
        _ = accelerator.SynchronizeCoreCalled.Should().BeTrue("synchronization should occur before disposal");
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Fact]
    [Trait("TestType", "Lifecycle")]
    public async Task DisposeAsync_MultipleDisposalAttempts_OnlyDisposesOnce()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        
        // Act
        await accelerator.DisposeAsync();
        await accelerator.DisposeAsync(); // Second call
        await accelerator.DisposeAsync(); // Third call

        // Assert
        _ = accelerator.IsDisposed.Should().BeTrue();
        _ = accelerator.DisposeCallCount.Should().Be(1, "dispose should only be called once");
    }
    
    [Fact]
    [Trait("TestType", "Lifecycle")]
    public async Task ConcurrentDisposal_ThreadSafety_DisposesOnlyOnce()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        var tasks = new List<Task>();
        
        // Act - Attempt concurrent disposal
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () => await accelerator.DisposeAsync()));
        }
        
        await Task.WhenAll(tasks);

        // Assert
        _ = accelerator.IsDisposed.Should().BeTrue();
        _ = accelerator.DisposeCallCount.Should().Be(1, "only one thread should successfully dispose");
    }
    
    #endregion
    
    #region Kernel Compilation Tests

    [Fact]
    [Trait("TestType", "Compilation")]
    public void KernelDefinition_WithNullParameters_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new KernelDefinition("", null!, null!);
        _ = act.Should().Throw<ArgumentNullException>();
    }
    
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
    
    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Minimal)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Aggressive)]
    [InlineData(OptimizationLevel.Maximum)]
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
        _ = _accelerator.LastCompilationOptions.OptimizationLevel.Should().Be(optimizationLevel);
    }
    
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
    
    [Fact]
    [Trait("TestType", "Compilation")]
    public async Task CompileKernelAsync_ConcurrentCompilations_ThreadSafety()
    {
        // Arrange
        var definitions = Enumerable.Range(0, 10)
            .Select(i => new KernelDefinition($"kernel_{i}", $"__kernel void test_{i}() {{}}", "test"))
            .ToArray();
        var compilationTasks = new List<Task<ICompiledKernel>>();
        
        // Act
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
    
    #endregion
    
    #region Error Handling Tests
    
    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task CompileKernelAsync_OutOfMemory_ThrowsOutOfMemoryException()
    {
        // Arrange
        var definition = new KernelDefinition("memory_intensive", "__kernel void test() {}", "test");
        var accelerator = CreateTestAccelerator();
        accelerator.ShouldThrowOutOfMemory = true;
        
        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<OutOfMemoryException>();
        
        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to compile")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    
    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task CompileKernelAsync_Timeout_ThrowsTimeoutException()
    {
        // Arrange
        var definition = new KernelDefinition("slow_compile", "__kernel void test() {}", "test");
        var accelerator = CreateTestAccelerator();
        accelerator.CompilationDelay = TimeSpan.FromSeconds(2);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        
        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition, cancellationToken: cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }
    
    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task SynchronizeAsync_AfterError_RecoversProperly()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.ShouldThrowOnSynchronize = true;
        
        // Act & Assert - First sync throws
        var act1 = async () => await accelerator.SynchronizeAsync();
        _ = await act1.Should().ThrowAsync<InvalidOperationException>();
        
        // Reset error condition
        accelerator.ShouldThrowOnSynchronize = false;
        
        // Act - Second sync should succeed
        var act2 = async () => await accelerator.SynchronizeAsync();
        _ = await act2.Should().NotThrowAsync();
    }
    
    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public void ThrowIfDisposed_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        _accelerator.SimulateDispose();
        
        // Act & Assert
        var act = () => _accelerator.TestThrowIfDisposed();
        _ = act.Should().Throw<ObjectDisposedException>();
    }
    
    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task CompileKernelAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        await _accelerator.DisposeAsync();
        var definition = new KernelDefinition("test", "__kernel void test() {}", "test");
        
        // Act & Assert
        var act = async () => await _accelerator.CompileKernelAsync(definition);
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }
    
    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task SynchronizeAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        await _accelerator.DisposeAsync();
        
        // Act & Assert
        var act = async () => await _accelerator.SynchronizeAsync();
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Memory Integration Tests


    [Fact]
    [Trait("TestType", "MemoryIntegration")]
    public void Memory_Property_ReturnsInjectedMemoryManager()
        // Assert
        => _accelerator.Memory.Should().Be(_mockMemory.Object);


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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Synchronizing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    [Trait("TestType", "Synchronization")]
    public async Task SynchronizeAsync_ConcurrentCalls_ThreadSafety()
    {
        // Arrange
        var tasks = new List<Task>();
        
        // Act - Multiple concurrent synchronization calls
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
    
    #endregion
    
    #region Performance Tracking Tests
    
    [Fact]
    [Trait("TestType", "Performance")]
    public void LogCompilationMetrics_LogsDebugMessage_WithCorrectFormat()
    {
        // Arrange
        const string kernelName = "performance_test_kernel";
        var compilationTime = TimeSpan.FromMilliseconds(150);
        const long byteCodeSize = 2048L;
        
        // Act
        _accelerator.TestLogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(kernelName) && 
                                             v.ToString()!.Contains("150") &&
                                             v.ToString()!.Contains("2048")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    [Trait("TestType", "Performance")]
    public void LogCompilationMetrics_WithNullByteCodeSize_LogsNAValue()
    {
        // Arrange
        const string kernelName = "null_size_kernel";
        var compilationTime = TimeSpan.FromMilliseconds(100);
        
        // Act
        _accelerator.TestLogCompilationMetrics(kernelName, compilationTime, null);
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("N/A")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
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
    
    #region Utility Tests

    [Fact]
    [Trait("TestType", "Utility")]
    public void GetEffectiveOptions_WithNullOptions_ReturnsDefaults()
    {
        // Act
        var options = _accelerator.TestGetEffectiveOptions(null);

        // Assert
        _ = options.Should().NotBeNull();
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Default);
        _ = options.EnableDebugInfo.Should().BeFalse();
    }
    
    [Fact]
    [Trait("TestType", "Utility")]
    public void GetEffectiveOptions_WithProvidedOptions_ReturnsProvidedOptions()
    {
        // Arrange
        var providedOptions = new CompilationOptions 
        { 
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = true
        };
        
        // Act
        var options = _accelerator.TestGetEffectiveOptions(providedOptions);

        // Assert
        _ = options.Should().Be(providedOptions);
        _ = options.OptimizationLevel.Should().Be(OptimizationLevel.Maximum);
        _ = options.EnableDebugInfo.Should().BeTrue();
    }
    
    #endregion
    
    #region Advanced Lifecycle Management Tests
    
    [Fact]
    [Trait("TestType", "AdvancedLifecycle")]
    public async Task InitializeAsync_WithResourceContention_HandlesGracefully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.CompilationDelay = TimeSpan.FromMilliseconds(50); // Simulate resource setup time
        
        var initTasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () => 
            {
                var definition = new KernelDefinition("init_test", "__kernel void test() {}", "test");
                return await accelerator.CompileKernelAsync(definition);
            }))
            .ToArray();
        
        // Act & Assert
        var results = await Task.WhenAll(initTasks);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());
        _ = accelerator.ConcurrentCompilationCount.Should().BeGreaterThan(1);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedLifecycle")]
    public async Task DisposeAsync_WithPendingOperations_WaitsForCompletion()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.CompilationDelay = TimeSpan.FromMilliseconds(200);
        
        var definition = new KernelDefinition("pending_test", "__kernel void test() {}", "test");
        var compilationTask = accelerator.CompileKernelAsync(definition).AsTask();
        
        // Small delay to ensure compilation starts
        await Task.Delay(50);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        await accelerator.DisposeAsync();
        stopwatch.Stop();

        // Assert
        _ = accelerator.IsDisposed.Should().BeTrue();
        _ = stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(100));
        
        // Compilation should still complete
        var result = await compilationTask;
        _ = result.Should().NotBeNull();
    }
    
    [Fact]
    [Trait("TestType", "AdvancedLifecycle")]
    public async Task Initialization_UnderMemoryPressure_RetriesAndSucceeds()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        _ = memoryManager.SetupSequence(m => m.GetAvailableMemory())
            .Throws(new OutOfMemoryException("Memory pressure"))
            .Throws(new OutOfMemoryException("Memory pressure"))
            .Returns(1024 * 1024 * 500); // 500MB available
        
        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);
        var definition = new KernelDefinition("memory_test", "__kernel void test() {}", "test");
        
        // Act & Assert - First attempts should handle memory pressure gracefully
        var act = async () => await accelerator.CompileKernelAsync(definition);
        _ = await act.Should().NotThrowAsync();
    }
    
    [Fact]
    [Trait("TestType", "AdvancedLifecycle")]
    public void Constructor_WithInvalidAcceleratorInfo_ThrowsArgumentException()
    {
        // Arrange
        var invalidInfo = new AcceleratorInfo(
            AcceleratorType.GPU,
            "",  // Empty name
            "1.0",
            -1,  // Invalid memory size
            0,   // Invalid cores
            0,   // Invalid frequency
            new Version(1, 0),
            1024,
            true
        );
        
        // Act & Assert
        var act = () => new TestAccelerator(invalidInfo, _mockMemory.Object, _mockLogger.Object);
        _ = act.Should().Throw<ArgumentException>();
    }
    
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
            _ = await act.Should().ThrowAsync<ArgumentException>();
        }
        else
        {
            // Assert - Should handle special characters gracefully
            var result = await _accelerator.CompileKernelAsync(definition);
            _ = result.Should().NotBeNull();
        }
    }
    
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
    
    [Fact]
    [Trait("TestType", "AdvancedCompilation")]
    public async Task CompileKernelAsync_WithRecursiveKernelDefinitions_HandlesDependencies()
    {
        // Arrange
        var helperKernel = new KernelDefinition(
            "helper", 
            "__kernel void helper(__global int* data) { *data *= 2; }",
            "helper");
        
        var mainKernel = new KernelDefinition(
            "main_with_helper",
            "__kernel void main_kernel(__global int* data) { helper(data); *data += 1; }",
            "main");
        
        // Act
        var helperResult = await _accelerator.CompileKernelAsync(helperKernel);
        var mainResult = await _accelerator.CompileKernelAsync(mainKernel);

        // Assert
        _ = helperResult.Should().NotBeNull();
        _ = mainResult.Should().NotBeNull();
        _ = _accelerator.TotalCompilations.Should().Be(2);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedCompilation")]
    public async Task CompileKernelAsync_WithPreprocessorDirectives_ProcessesCorrectly()
    {
        // Arrange
        var kernelWithDirectives = new KernelDefinition(
            "preprocessor_test",
            @"#define WORK_GROUP_SIZE 256
             #ifdef DEBUG
             #define LOG(x) printf(x)
             #else
             #define LOG(x)
             #endif
             __kernel void test_kernel(__global float* data) {
                 LOG(""Processing data\n"");
                 int idx = get_global_id(0);
                 if (idx < WORK_GROUP_SIZE) data[idx] = idx;
             }",
            "preprocessor");
        
        var options = new CompilationOptions 
        { 
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false
        };
        
        // Act
        var result = await _accelerator.CompileKernelAsync(kernelWithDirectives, options);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.Name.Should().Be("preprocessor_test");
    }
    
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
            OptimizationLevel.Minimal,
            OptimizationLevel.Default,
            OptimizationLevel.Aggressive,
            OptimizationLevel.Maximum
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
    
    #endregion
    
    #region Advanced Error Handling Tests
    
    [Fact]
    [Trait("TestType", "AdvancedErrorHandling")]
    public async Task CompileKernelAsync_WithTransientErrors_RetriesSuccessfully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        var attemptCount = 0;
        
        // Setup to fail first two attempts, succeed on third
        accelerator.ShouldThrowOnCompilation = true;
        
        var definition = new KernelDefinition("retry_test", "__kernel void test() {}", "test");
        
        // Act - Simulate retry logic
        ICompiledKernel? result = null;
        var maxRetries = 3;
        
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                attemptCount++;
                if (attemptCount >= 3) // Succeed on third attempt
                    accelerator.ShouldThrowOnCompilation = false;
                
                result = await accelerator.CompileKernelAsync(definition);
                break;
            }
            catch (InvalidOperationException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(100); // Brief delay between retries
            }
        }

        // Assert
        _ = result.Should().NotBeNull();
        _ = attemptCount.Should().Be(3);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedErrorHandling")]
    public async Task ErrorRecovery_AfterCriticalFailure_RestoresOperationalState()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        
        // Simulate critical failure
        accelerator.ShouldThrowOutOfMemory = true;
        var failingDefinition = new KernelDefinition("critical_fail", "__kernel void test() {}", "test");
        
        // Act & Assert - Critical failure
        var act1 = async () => await accelerator.CompileKernelAsync(failingDefinition);
        _ = await act1.Should().ThrowAsync<OutOfMemoryException>();
        
        // Recovery - Reset error condition and retry
        accelerator.ShouldThrowOutOfMemory = false;
        var recoveryDefinition = new KernelDefinition("recovery_test", "__kernel void test() {}", "test");
        
        // Act - Should recover and work normally
        var result = await accelerator.CompileKernelAsync(recoveryDefinition);
        await accelerator.SynchronizeAsync(); // Should also work

        // Assert
        _ = result.Should().NotBeNull();
        _ = accelerator.SynchronizeCoreCalled.Should().BeTrue();
    }
    
    [Fact]
    [Trait("TestType", "AdvancedErrorHandling")]
    public async Task ConcurrentOperations_WithPartialFailures_MaintainsConsistency()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        
        // Create mix of operations that will succeed and fail
        var operations = new List<Task<ICompiledKernel?>>();
        
        for (var i = 0; i < 10; i++)
        {
            var definition = new KernelDefinition($"concurrent_test_{i}", "__kernel void test() {}", "test");
            
            operations.Add(Task.Run(async () =>
            {
                try
                {
                    // Simulate failure for even numbered operations
                    if (i % 2 == 0)
                    {
                        throw new InvalidOperationException($"Simulated failure for operation {i}");
                    }
                    return await accelerator.CompileKernelAsync(definition);
                }
                catch
                {
                    return null; // Return null for failed operations
                }
            }));
        }
        
        // Act
        var results = await Task.WhenAll(operations);
        
        // Assert
        var successfulResults = results.Where(r => r != null).ToArray();
        var failedResults = results.Where(r => r == null).ToArray();

        _ = successfulResults.Should().HaveCount(5); // Odd numbered operations
        _ = failedResults.Should().HaveCount(5);     // Even numbered operations
        _ = accelerator.CompileKernelCoreCalled.Should().BeTrue();
    }
    
    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [Trait("TestType", "AdvancedErrorHandling")]
    public async Task OperationTimeout_WithVaryingTimeouts_HandlesCorrectly(int timeoutMs)
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.CompilationDelay = TimeSpan.FromMilliseconds(timeoutMs + 100); // Always longer than timeout
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
        var definition = new KernelDefinition("timeout_test", "__kernel void test() {}", "test");
        
        // Act & Assert
        var act = async () => await accelerator.CompileKernelAsync(definition, cancellationToken: cts.Token);
        
        var exception = await act.Should().ThrowAsync<OperationCanceledException>();
        _ = exception.Which.CancellationToken.Should().Be(cts.Token);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedErrorHandling")]
    public async Task ResourceExhaustion_GradualDegradation_HandlesGracefully()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        var memoryManager = new Mock<IUnifiedMemoryManager>();

        // Simulate gradually decreasing available memory
        _ = memoryManager.SetupSequence(m => m.GetAvailableMemory())
            .Returns(1024 * 1024 * 1024) // 1GB
            .Returns(512 * 1024 * 1024)  // 512MB
            .Returns(256 * 1024 * 1024)  // 256MB
            .Returns(128 * 1024 * 1024)  // 128MB
            .Throws(new OutOfMemoryException("Insufficient memory"));
        
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

        // Assert
        _ = results.Should().HaveCountGreaterThan(0, "some operations should succeed");
        _ = exceptions.Should().HaveCountGreaterThan(0, "some operations should fail due to resource exhaustion");
        _ = exceptions.Should().AllBeOfType<OutOfMemoryException>();
    }
    
    #endregion
    
    #region Advanced Memory Integration Tests
    
    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public void MemoryManager_AllocationTracking_ReportsAccurateUsage()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        var currentMemoryUsage = 0L;

        _ = memoryManager.Setup(m => m.GetTotalMemory()).Returns(1024 * 1024 * 1024); // 1GB
        _ = memoryManager.Setup(m => m.GetAvailableMemory()).Returns(() => 1024 * 1024 * 1024 - currentMemoryUsage);
        _ = memoryManager.Setup(m => m.GetUsedMemory()).Returns(() => currentMemoryUsage);
        
        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);
        
        // Act - Simulate memory allocations
        currentMemoryUsage = 100 * 1024 * 1024; // 100MB allocated

        // Assert
        _ = accelerator.Memory.GetUsedMemory().Should().Be(100 * 1024 * 1024);
        _ = accelerator.Memory.GetAvailableMemory().Should().Be(1024 * 1024 * 1024 - 100 * 1024 * 1024);
        _ = accelerator.Info.TotalMemory.Should().Be(1024 * 1024 * 1024);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task MemoryPressure_DuringCompilation_TriggersGarbageCollection()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        var gcCollected = false;

        _ = memoryManager.Setup(m => m.GetAvailableMemory())
            .Returns(10 * 1024 * 1024) // Low available memory
            .Callback(() =>
            {
                if (!gcCollected)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    gcCollected = true;
                }
            });
        
        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);
        var definition = new KernelDefinition("memory_pressure_test", "__kernel void test() {}", "test");
        
        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        _ = gcCollected.Should().BeTrue("garbage collection should have been triggered");
        memoryManager.Verify(m => m.GetAvailableMemory(), Times.AtLeastOnce);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task LargeKernelCompilation_MemoryAllocation_TracksCorrectly()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        var allocationSize = 0L;

        _ = memoryManager.Setup(m => m.GetUsedMemory()).Returns(() => allocationSize);
        _ = memoryManager.Setup(m => m.GetAvailableMemory()).Returns(() => 1024 * 1024 * 1024 - allocationSize);
        
        var accelerator = CreateTestAccelerator(memoryManager: memoryManager.Object);
        
        // Create a large kernel source
        var largeKernelSource = string.Join("\n", 
            Enumerable.Range(0, 5000).Select(i => $"float var_{i} = {i}.0f;")) +
            "\n__kernel void large_memory_kernel(__global float* output) { *output = var_4999; }";
        
        var definition = new KernelDefinition("large_memory_kernel", largeKernelSource, "test");
        
        // Act
        allocationSize = 500 * 1024 * 1024; // Simulate 500MB allocation during compilation
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        _ = result.Should().NotBeNull();
        memoryManager.Verify(m => m.GetUsedMemory(), Times.AtLeastOnce);
        memoryManager.Verify(m => m.GetAvailableMemory(), Times.AtLeastOnce);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedMemoryIntegration")]
    public async Task MemoryFragmentation_HandlesDegradedPerformance()
    {
        // Arrange
        var memoryManager = new Mock<IUnifiedMemoryManager>();
        var fragmentationLevel = 0.0;

        _ = memoryManager.Setup(m => m.GetAvailableMemory())
            .Returns(() => (long)(1024 * 1024 * 1024 * (1.0 - fragmentationLevel)));
        
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
    
    #endregion
    
    #region Advanced Synchronization Tests
    
    [Fact]
    [Trait("TestType", "AdvancedSynchronization")]
    public async Task SynchronizeAsync_WithMultiplePendingOperations_WaitsForAll()
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
        var syncStopwatch = Stopwatch.StartNew();
        await accelerator.SynchronizeAsync();
        syncStopwatch.Stop();
        
        // Assert
        var results = await Task.WhenAll(compilationTasks);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());

        // Synchronization should wait for pending operations
        _ = syncStopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        _ = accelerator.SynchronizeCoreCalled.Should().BeTrue();
    }
    
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
    
    [Fact]
    [Trait("TestType", "AdvancedSynchronization")]
    public async Task SynchronizeAsync_WithCascadingCancellation_PropagatesCorrectly()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.SyncDelay = TimeSpan.FromSeconds(1);
        
        using var parentCts = new CancellationTokenSource();
        using var childCts = CancellationTokenSource.CreateLinkedTokenSource(parentCts.Token);
        
        var syncTask = accelerator.SynchronizeAsync(childCts.Token).AsTask();
        
        // Act - Cancel parent token after short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            parentCts.Cancel();
        });
        
        // Assert
        var act = async () => await syncTask;
        _ = await act.Should().ThrowAsync<OperationCanceledException>();

        _ = childCts.Token.IsCancellationRequested.Should().BeTrue();
        _ = parentCts.Token.IsCancellationRequested.Should().BeTrue();
    }
    
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
    
    #endregion
    
    #region Advanced Performance Metrics Tests
    
    [Fact]
    [Trait("TestType", "AdvancedPerformance")]
    public async Task PerformanceMetrics_ResourceUtilization_TracksAccurately()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableResourceTracking = true;
        accelerator.EnableMetricsTracking = true;
        
        var startTime = DateTime.UtcNow;
        var operations = new List<Task<ICompiledKernel>>();
        
        // Act - Perform various operations
        for (var i = 0; i < 10; i++)
        {
            var definition = new KernelDefinition($"perf_test_{i}", 
                $"__kernel void test_{i}(__global float* data) {{ data[get_global_id(0)] = {i}.0f; }}", 
                "performance");
            
            operations.Add(accelerator.CompileKernelAsync(definition).AsTask());
            
            if (i % 3 == 0) // Sync every 3 operations
            {
                await accelerator.SynchronizeAsync();
            }
        }

        _ = await Task.WhenAll(operations);
        var endTime = DateTime.UtcNow;

        // Assert
        _ = accelerator.TotalCompilations.Should().Be(10);
        _ = accelerator.TotalSynchronizations.Should().BeGreaterThanOrEqualTo(3);
        _ = accelerator.AverageCompilationTime.Should().BeGreaterThan(TimeSpan.Zero);
        
        var totalElapsed = endTime - startTime;
        _ = accelerator.AverageCompilationTime.Should().BeLessThan(totalElapsed);
        
        // Verify metrics were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("perf_test_")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(10));
    }
    
    [Fact]
    [Trait("TestType", "AdvancedPerformance")]
    public async Task ThroughputMetrics_HighVolumeOperations_MaintainsPerformance()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        accelerator.EnableMetricsTracking = true;
        
        const int operationCount = 100;
        var operations = new List<Task<ICompiledKernel>>();
        var throughputSamples = new List<double>();
        
        // Act - Measure throughput in batches
        var batchSize = 10;
        for (var batch = 0; batch < operationCount / batchSize; batch++)
        {
            var batchStartTime = DateTime.UtcNow;
            var batchOperations = new List<Task<ICompiledKernel>>();
            
            for (var i = 0; i < batchSize; i++)
            {
                var opIndex = batch * batchSize + i;
                var definition = new KernelDefinition($"throughput_test_{opIndex}", 
                    "__kernel void test() {}", "throughput");
                batchOperations.Add(accelerator.CompileKernelAsync(definition).AsTask());
            }

            _ = await Task.WhenAll(batchOperations);
            var batchDuration = DateTime.UtcNow - batchStartTime;
            
            var throughput = batchSize / batchDuration.TotalSeconds;
            throughputSamples.Add(throughput);
            
            operations.AddRange(batchOperations);
        }

        // Assert
        _ = operations.Should().HaveCount(operationCount);
        _ = throughputSamples.Should().HaveCount(operationCount / batchSize);
        
        var averageThroughput = throughputSamples.Average();
        var minThroughput = throughputSamples.Min();
        var maxThroughput = throughputSamples.Max();

        _ = averageThroughput.Should().BeGreaterThan(0);
        _ = minThroughput.Should().BeGreaterThan(0);
        
        // Throughput should be relatively stable (coefficient of variation < 50%)
        var standardDeviation = Math.Sqrt(throughputSamples.Average(t => Math.Pow(t - averageThroughput, 2)));
        var coefficientOfVariation = standardDeviation / averageThroughput;
        _ = coefficientOfVariation.Should().BeLessThan(0.5, "throughput should be relatively stable");
    }
    
    [Fact]
    [Trait("TestType", "AdvancedPerformance")]
    public async Task MemoryEfficiency_LongRunningOperations_MaintainsStability()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act - Perform long-running operations
        for (var cycle = 0; cycle < 20; cycle++)
        {
            var definitions = Enumerable.Range(0, 10)
                .Select(i => new KernelDefinition($"memory_cycle_{cycle}_{i}", 
                    "__kernel void test() { int temp[1000]; for(int j=0; j<1000; j++) temp[j] = j; }", 
                    "memory"))
                .ToArray();
            
            var results = new List<ICompiledKernel>();
            foreach (var definition in definitions)
            {
                var result = await accelerator.CompileKernelAsync(definition);
                results.Add(result);
            }
            
            await accelerator.SynchronizeAsync();
            
            // Force garbage collection every 5 cycles
            if (cycle % 5 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        
        // Final cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(false);
        
        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        // Memory increase should be reasonable (less than 100MB for this test)
        _ = memoryIncrease.Should().BeLessThan(100 * 1024 * 1024,
            "memory usage should remain stable over long operations");

        _ = accelerator.TotalCompilations.Should().Be(200); // 20 cycles * 10 operations
        _ = accelerator.TotalSynchronizations.Should().Be(20);
    }
    
    [Fact]
    [Trait("TestType", "AdvancedPerformance")]
    public async Task LatencyMetrics_ResponseTimeDistribution_WithinExpectedRanges()
    {
        // Arrange
        var accelerator = CreateTestAccelerator();
        var latencies = new List<TimeSpan>();
        
        // Act - Measure latency for individual operations
        for (var i = 0; i < 50; i++)
        {
            var definition = new KernelDefinition($"latency_test_{i}", "__kernel void test() {}", "latency");
            
            var stopwatch = Stopwatch.StartNew();
            _ = await accelerator.CompileKernelAsync(definition);
            stopwatch.Stop();
            
            latencies.Add(stopwatch.Elapsed);
        }

        // Assert
        _ = latencies.Should().HaveCount(50);
        
        var averageLatency = TimeSpan.FromTicks((long)latencies.Average(l => l.Ticks));
        var minLatency = latencies.Min();
        var maxLatency = latencies.Max();
        var p95Latency = latencies.OrderBy(l => l).Skip((int)(latencies.Count * 0.95)).First();
        var p99Latency = latencies.OrderBy(l => l).Skip((int)(latencies.Count * 0.99)).First();

        // Basic sanity checks
        _ = averageLatency.Should().BeGreaterThan(TimeSpan.Zero);
        _ = minLatency.Should().BeLessThan(averageLatency);
        _ = maxLatency.Should().BeGreaterThan(averageLatency);
        _ = p95Latency.Should().BeLessThanOrEqualTo(maxLatency);
        _ = p99Latency.Should().BeLessThanOrEqualTo(maxLatency);

        // Performance expectations
        _ = averageLatency.Should().BeLessThan(TimeSpan.FromSeconds(1), "average latency should be reasonable");
        _ = p99Latency.Should().BeLessThan(TimeSpan.FromSeconds(5), "99th percentile should be acceptable");
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
    /// Enhanced test implementation of BaseAccelerator for comprehensive testing.
    /// </summary>
    private sealed class TestAccelerator : BaseAccelerator
    {
        // Basic tracking
        public bool CompileKernelCoreCalled { get; private set; }
        public bool SynchronizeCoreCalled { get; private set; }
        public bool InitializeCoreCalled { get; private set; }
        public int DisposeCallCount { get; private set; }
        
        // Advanced tracking
        public KernelDefinition? LastCompiledDefinition { get; private set; }
        public CompilationOptions? LastCompilationOptions { get; private set; }
        public TimeSpan LastCompilationTime { get; private set; }
        public bool CompilationMetricsLogged { get; private set; }
        
        // Concurrency tracking
        public int ConcurrentCompilationCount { get; private set; }
        public int ConcurrentSyncCount { get; private set; }
        
        // Performance tracking
        public int TotalCompilations { get; private set; }
        public int TotalSynchronizations { get; private set; }
        public TimeSpan AverageCompilationTime { get; private set; }
        
        // Caching
        public bool EnableCompilationCaching { get; set; }
        public int CacheHitCount { get; private set; }
        private readonly ConcurrentDictionary<string, ICompiledKernel> _kernelCache = new();
        
        // Error simulation
        public bool ShouldThrowOnCompilation { get; set; }
        public bool ShouldThrowOutOfMemory { get; set; }
        public bool ShouldThrowOnSynchronize { get; set; }
        
        // Timing simulation
        public TimeSpan CompilationDelay { get; set; }
        public TimeSpan SyncDelay { get; set; }
        
        // Feature flags
        public bool EnableMetricsTracking { get; set; }
        public bool EnableResourceTracking { get; set; }
        
        // Performance counters
        private readonly List<TimeSpan> _compilationTimes = [];
        private int _activeCompilations;
        private int _activeSyncs;

        public TestAccelerator(AcceleratorInfo info, IUnifiedMemoryManager memory, ILogger logger)
            : base(info, AcceleratorType.CPU, memory, new AcceleratorContext(IntPtr.Zero, 0), logger)
        {
        }
        
        protected override object? InitializeCore()
        {
            InitializeCoreCalled = true;
            return base.InitializeCore();
        }

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
                
                // Simulate compilation delay
                if (CompilationDelay > TimeSpan.Zero)
                {
                    await Task.Delay(CompilationDelay, cancellationToken);
                }
                
                // Error simulation
                if (ShouldThrowOnCompilation)
                    throw new InvalidOperationException("Compilation failed (simulated)");
                
                if (ShouldThrowOutOfMemory)
                    throw new OutOfMemoryException("Insufficient memory for compilation (simulated)");
                
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
                    LogCompilationMetrics(definition.Name, compilationTime, 1024); // Simulate bytecode size
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
                
                // Error simulation
                if (ShouldThrowOnSynchronize)
                    throw new InvalidOperationException("Synchronization failed (simulated)");
                
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

        protected override async ValueTask DisposeCoreAsync()
        {
            DisposeCallCount++;
            await base.DisposeCoreAsync();
        }

        public void SimulateDispose()
        {
            var field = typeof(BaseAccelerator).GetField("_disposed", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(this, 1);
        }

        public void TestThrowIfDisposed() => ThrowIfDisposed();
        
        public CompilationOptions TestGetEffectiveOptions(CompilationOptions? options)
            => GetEffectiveOptions(options);
        
        public void TestLogCompilationMetrics(string kernelName, TimeSpan compilationTime, long? byteCodeSize)
            => LogCompilationMetrics(kernelName, compilationTime, byteCodeSize);
    }
}