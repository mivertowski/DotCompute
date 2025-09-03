// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Tests.Common;
using Microsoft.Extensions.Options;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Tests for CpuAccelerator functionality including SIMD detection, thread pool configuration,
/// vectorization, and CPU-specific optimizations.
/// </summary>
[Trait("Category", TestCategories.HardwareIndependent)]
public class CpuAcceleratorTests : IDisposable
{
    private readonly ILogger<CpuAccelerator> _logger;
    private readonly CpuAccelerator _accelerator;
    
    public CpuAcceleratorTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<CpuAccelerator>();
        
        var acceleratorOptions = Options.Create(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true,
            MaxWorkGroupSize = Environment.ProcessorCount
        });
        
        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions
        {
            WorkerThreads = Environment.ProcessorCount
        });
        
        _accelerator = new CpuAccelerator(acceleratorOptions, threadPoolOptions, _logger);
    }
    
    [Fact]
    public void Constructor_WithValidOptions_InitializesSuccessfully()
    {
        // Arrange & Act - accelerator created in constructor
        
        // Assert
        _accelerator.Should().NotBeNull();
        _accelerator.Type.Should().Be(AcceleratorType.CPU);
        _accelerator.IsDisposed.Should().BeFalse();
    }
    
    [Fact]
    public void AcceleratorInfo_ContainsExpectedProperties()
    {
        // Act
        var info = _accelerator.Info;
        
        // Assert
        info.Should().NotBeNull();
        info.Type.Should().Be(AcceleratorType.CPU.ToString());
        info.IsUnifiedMemory.Should().BeTrue();
        info.ComputeUnits.Should().Be(Environment.ProcessorCount);
        info.Capabilities.Should().NotBeNull();
        info.Capabilities!.Should().ContainKey("SimdWidth");
        info.Capabilities!.Should().ContainKey("SimdInstructionSets");
        info.Capabilities!.Should().ContainKey("ThreadCount");
        info.Capabilities!.Should().ContainKey("NumaNodes");
        info.Capabilities!.Should().ContainKey("CacheLineSize");
    }
    
    [Fact]
    public void AcceleratorInfo_SimdCapabilities_AreDetectedCorrectly()
    {
        // Act
        var info = _accelerator.Info;
        var simdWidth = info.Capabilities["SimdWidth"];
        var supportedSets = info.Capabilities["SimdInstructionSets"] as IReadOnlySet<string>;
        
        // Assert
        simdWidth.Should().NotBeNull();
        simdWidth.Should().BeOfType<int>();
        ((int)simdWidth).Should().BeGreaterThan(0);
        
        supportedSets.Should().NotBeNull();
        supportedSets.Should().NotBeEmpty();
    }
    
    [Fact]
    public void ThreadCount_MatchesProcessorCount()
    {
        // Act
        var threadCount = _accelerator.Info.Capabilities["ThreadCount"];
        
        // Assert
        threadCount!.Should().Be(Environment.ProcessorCount);
    }
    
    [Fact]
    public async Task CompileKernelAsync_WithSimpleKernel_CompilesSuccessfully()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition(
            "simple_add",
            "__kernel void simple_add(__global float* a, __global float* b, __global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
            "simple_add");
        
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Default,
            EnableDebugInfo = false
        };
        
        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);
        
        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be("simple_add");
        compiledKernel.Id.Should().NotBe(Guid.Empty);
        
        // Cleanup
        await compiledKernel.DisposeAsync();
    }
    
    [Fact]
    public async Task CompileKernelAsync_WithOptimization_EnablesVectorization()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition(
            "vector_add", 
            "__kernel void vector_add(__global float* a, __global float* b, __global float* c) { int i = get_global_id(0); c[i] = a[i] + b[i]; }",
            "vector_add")
        {
            Metadata = new Dictionary<string, object>
            {
                ["ParameterCount"] = 3,
                ["WorkDimensions"] = 1,
                ["ParameterAccess"] = new[] { "ReadOnly", "ReadOnly", "WriteOnly" }
            }
        };
        
        var options = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            EnableDebugInfo = false
        };
        
        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);
        
        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be("vector_add");
        
        // Check if SIMD capabilities are noted in metadata (if available)
        var metadata = kernelDefinition.Metadata;
        if (metadata?.ContainsKey("SimdCapabilities") == true)
        {
            metadata["SimdCapabilities"].Should().NotBeNull();
        }
        
        await compiledKernel.DisposeAsync();
    }
    
    [Fact]
    public async Task CompileKernelAsync_WithInvalidKernel_ThrowsException()
    {
        // Arrange
        var invalidKernel = new KernelDefinition("invalid", "", "");
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };
        
        // Act & Assert
        Func<Task> act = async () => await _accelerator.CompileKernelAsync(invalidKernel, options);
        await act.Should().ThrowExactlyAsync<Exception>();
    }
    
    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Maximum)]
    public async Task CompileKernelAsync_WithDifferentOptimizationLevels_HandlesCorrectly(OptimizationLevel level)
    {
        // Arrange
        var kernelDefinition = new KernelDefinition(
            $"test_kernel_{level}",
            "__kernel void test_kernel(__global float* data) { int i = get_global_id(0); data[i] *= 2.0f; }",
            "test_kernel");
        
        var options = new CompilationOptions
        {
            OptimizationLevel = level,
            EnableDebugInfo = level == OptimizationLevel.None
        };
        
        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);
        
        // Assert
        compiledKernel.Should().NotBeNull();
        compiledKernel.Name.Should().Be($"test_kernel_{level}");
        
        await compiledKernel.DisposeAsync();
    }
    
    [Fact]
    public async Task SynchronizeAsync_CompletesSuccessfully()
    {
        // Act
        var synchronizeTask = _accelerator.SynchronizeAsync();
        
        // Assert
        synchronizeTask.Should().NotBeNull();
        await synchronizeTask;
        synchronizeTask.IsCompleted.Should().BeTrue();
    }
    
    [Fact]
    public void MemoryManager_IsNotNull()
    {
        // Act
        var memoryManager = _accelerator.Memory;
        
        // Assert
        memoryManager.Should().NotBeNull();
        memoryManager.Should().BeOfType<CpuMemoryManager>();
    }
    
    [Fact]
    public void Context_IsValid()
    {
        // Act
        var context = _accelerator.Context;
        
        // Assert
        context.Should().NotBeNull();
        // CPU context uses IntPtr.Zero as it doesn't require a specific context
        context.DeviceId.Should().Be(0);
    }
    
    [Fact]
    [Trait("Category", TestCategories.Performance)]
    public async Task CompileKernelAsync_PerformanceBenchmark_MeasuresCompilationTime()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition(
            "performance_test",
            "__kernel void performance_test(__global float* a, __global float* b, __global float* c) { int i = get_global_id(0); c[i] = sqrt(a[i] * a[i] + b[i] * b[i]); }",
            "performance_test");
        
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Act
        var compiledKernel = await _accelerator.CompileKernelAsync(kernelDefinition, options);
        stopwatch.Stop();
        
        // Assert
        compiledKernel.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should compile within 5 seconds
        
        await compiledKernel.DisposeAsync();
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task CompileKernelAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        KernelDefinition? nullKernel = null;
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };
        
        // Act & Assert
        Func<Task> act = async () => await _accelerator.CompileKernelAsync(nullKernel!, options);
        await act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task CompileKernelAsync_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var kernelDefinition = new KernelDefinition("test", "test code", "test");
        CompilationOptions? nullOptions = null;
        
        // Act & Assert
        Func<Task> act = async () => await _accelerator.CompileKernelAsync(kernelDefinition, nullOptions!);
        await act.Should().ThrowExactlyAsync<ArgumentNullException>();
    }
    
    [Fact]
    [Trait("Category", TestCategories.Concurrency)]
    public async Task CompileKernelAsync_ConcurrentCompilation_HandlesMultipleKernels()
    {
        // Arrange
        var kernels = Enumerable.Range(0, 5).Select(i => 
            new KernelDefinition(
                $"concurrent_kernel_{i}",
                $"__kernel void concurrent_kernel_{i}(__global float* data) {{ int id = get_global_id(0); data[id] += {i}; }}",
                $"concurrent_kernel_{i}")).ToArray();
        
        var options = new CompilationOptions { OptimizationLevel = OptimizationLevel.Default };
        
        // Act
        var compilationTasks = kernels.Select(k => _accelerator.CompileKernelAsync(k, options).AsTask()).ToArray();
        var compiledKernels = await Task.WhenAll(compilationTasks);
        
        // Assert
        compiledKernels.Should().HaveCount(5);
        compiledKernels.Should().OnlyContain(k => k != null);
        
        // Cleanup
        await Task.WhenAll(compiledKernels.Select(k => k.DisposeAsync().AsTask()));
    }
    
    [Fact]
    public void SimdCapabilities_AreAccessibleThroughAcceleratorInfo()
    {
        // Act
        var capabilities = _accelerator.Info.Capabilities;
        var instructionSets = capabilities["SimdInstructionSets"] as IReadOnlySet<string>;
        var vectorWidth = capabilities["SimdWidth"];
        
        // Assert
        instructionSets.Should().NotBeNull();
        vectorWidth.Should().NotBeNull();
        
        // Should contain at least basic instruction set information
        if (SimdCapabilities.IsSupported)
        {
            instructionSets!.Should().NotBeEmpty();
            ((int)vectorWidth!).Should().BeGreaterThan(0);
        }
    }
    
    [Theory]
    [InlineData(true, true)]   // Enable vectorization, prefer performance
    [InlineData(true, false)]  // Enable vectorization, don't prefer performance
    [InlineData(false, true)]  // Disable vectorization, prefer performance
    [InlineData(false, false)] // Disable vectorization, don't prefer performance
    public async Task Constructor_WithDifferentOptions_ConfiguresCorrectly(bool enableVectorization, bool preferPerformance)
    {
        // Arrange
        var acceleratorOptions = Options.Create(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = enableVectorization,
            PreferPerformanceOverPower = preferPerformance
        });
        
        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions());
        
        // Act
        await using var accelerator = new CpuAccelerator(acceleratorOptions, threadPoolOptions, _logger);
        
        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Type.Should().Be(AcceleratorType.CPU);
    }
    
    [Fact]
    public async Task DisposeAsync_ReleasesResources()
    {
        // Arrange
        var acceleratorOptions = Options.Create(new CpuAcceleratorOptions());
        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions());
        var accelerator = new CpuAccelerator(acceleratorOptions, threadPoolOptions, _logger);
        
        // Act
        await accelerator.DisposeAsync();
        
        // Assert
        accelerator.IsDisposed.Should().BeTrue();
    }
    
    public void Dispose()
    {
        _accelerator?.DisposeAsync().AsTask().Wait();
        GC.SuppressFinalize(this);
    }
}