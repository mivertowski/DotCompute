// Copyright (c) 2025 DotCompute Project Contributors
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Memory;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Tests.Common.Mocks;

/// <summary>
/// Comprehensive mock accelerator that consolidates all mock implementations.
/// Provides configurable behavior for different testing scenarios.
/// </summary>
public sealed class ConsolidatedMockAccelerator : IAccelerator
{
    private readonly AcceleratorInfo _info;
    private readonly Mock<IUnifiedMemoryManager> _memoryManagerMock;
    private readonly Mock<ICompiledKernel> _compiledKernelMock;
    private readonly List<Action> _disposeActions = new();
    private bool _disposed;

    public AcceleratorInfo Info => _info;
    public AcceleratorType Type { get; }
    public string DeviceType => _info.DeviceType;
    public IUnifiedMemoryManager Memory => _memoryManagerMock.Object;
    public IUnifiedMemoryManager MemoryManager => Memory;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "AcceleratorContext() constructor is not equivalent to default")]
    public AcceleratorContext Context { get; } = new();
    public bool IsAvailable => !_disposed;

    // Configuration properties for testing scenarios
    public bool ShouldFailCompilation { get; set; }
    public bool ShouldFailExecution { get; set; }
    public bool ShouldFailMemoryAllocation { get; set; }
    public TimeSpan ExecutionDelay { get; set; } = TimeSpan.Zero;
    public Exception? CompilationException { get; set; }
    public Exception? ExecutionException { get; set; }
    public Exception? MemoryException { get; set; }

    // Advanced tracking properties for BaseAccelerator testing
    public bool CompileKernelCoreCalled { get; private set; }
    public bool SynchronizeCoreCalled { get; private set; }
    public bool InitializeCoreCalled { get; private set; }
    public int DisposeCallCount { get; private set; }
    public KernelDefinition? LastCompiledDefinition { get; private set; }
    public CompilationOptions? LastCompilationOptions { get; private set; }
    public TimeSpan LastCompilationTime { get; private set; }
    public int TotalCompilations { get; private set; }
    public int TotalSynchronizations { get; private set; }
    public bool EnableCompilationCaching { get; set; } = true;
    public int CacheHitCount { get; private set; }
    private readonly ConcurrentDictionary<string, ICompiledKernel> _kernelCache = new();

    /// <summary>
    /// Creates a mock CPU accelerator with realistic specifications.
    /// </summary>
    public static ConsolidatedMockAccelerator CreateCpuMock()
    {
        var info = new AcceleratorInfo
        {
            Id = "mock_cpu_consolidated",
            Name = "Consolidated Mock CPU",
            DeviceType = "CPU",
            Vendor = "Mock Corporation",
            DriverVersion = "1.0.0",
            TotalMemory = 16L * 1024 * 1024 * 1024, // 16 GB
            AvailableMemory = 12L * 1024 * 1024 * 1024, // 12 GB available
            MaxSharedMemoryPerBlock = 64 * 1024, // 64 KB L1 cache
            MaxMemoryAllocationSize = 8L * 1024 * 1024 * 1024, // 8 GB max single allocation
            LocalMemorySize = 32 * 1024, // 32 KB L1 cache per core
            IsUnifiedMemory = true,
            MaxThreadsPerBlock = Environment.ProcessorCount,
            MaxComputeUnits = Environment.ProcessorCount,
            GlobalMemorySize = 16L * 1024 * 1024 * 1024,
            SupportsFloat64 = true,
            SupportsInt64 = true,
            ComputeCapability = new Version(1, 0) // Generic CPU version
        };

        return new ConsolidatedMockAccelerator(info, AcceleratorType.CPU);
    }

    /// <summary>
    /// Creates a mock GPU accelerator with RTX 2000 Ada specifications.
    /// </summary>
    public static ConsolidatedMockAccelerator CreateGpuMock()
    {
        var info = new AcceleratorInfo
        {
            Id = "mock_gpu_consolidated",
            Name = "Consolidated Mock GPU (RTX 2000 Ada)",
            DeviceType = "GPU",
            Vendor = "NVIDIA",
            DriverVersion = "581.15",
            TotalMemory = 24L * 1024 * 1024 * 1024, // 24 GB GDDR6
            AvailableMemory = 20L * 1024 * 1024 * 1024, // 20 GB available
            MaxSharedMemoryPerBlock = 48 * 1024, // 48 KB shared memory per SM
            MaxMemoryAllocationSize = 12L * 1024 * 1024 * 1024, // 12 GB max allocation
            LocalMemorySize = 48 * 1024, // 48 KB shared memory
            IsUnifiedMemory = false,
            MaxThreadsPerBlock = 1024, // CUDA max threads per block
            ComputeCapability = new Version(8, 9), // Ada Lovelace architecture
            MaxComputeUnits = 102, // Streaming multiprocessors
            GlobalMemorySize = 24L * 1024 * 1024 * 1024,
            SupportsFloat64 = true,
            SupportsInt64 = true
        };

        return new ConsolidatedMockAccelerator(info, AcceleratorType.GPU);
    }

    /// <summary>
    /// Creates a mock Metal accelerator for Apple Silicon testing.
    /// </summary>
    public static ConsolidatedMockAccelerator CreateMetalMock()
    {
        var info = new AcceleratorInfo
        {
            Id = "mock_metal_consolidated",
            Name = "Consolidated Mock Metal GPU",
            DeviceType = "Metal",
            Vendor = "Apple",
            DriverVersion = "3.1",
            TotalMemory = 32L * 1024 * 1024 * 1024, // 32 GB unified memory
            AvailableMemory = 28L * 1024 * 1024 * 1024, // 28 GB available
            MaxSharedMemoryPerBlock = 32 * 1024, // 32 KB threadgroup memory
            MaxMemoryAllocationSize = 16L * 1024 * 1024 * 1024, // 16 GB max allocation
            LocalMemorySize = 32 * 1024, // 32 KB per compute unit
            IsUnifiedMemory = true, // Apple Silicon unified memory
            MaxThreadsPerBlock = 1024, // Metal max threads per threadgroup
            ComputeCapability = new Version(3, 1), // Metal 3.1
            MaxComputeUnits = 38, // M2 Max GPU cores (example)
            GlobalMemorySize = 32L * 1024 * 1024 * 1024,
            SupportsFloat64 = true,
            SupportsInt64 = true
        };

        return new ConsolidatedMockAccelerator(info, AcceleratorType.Metal);
    }

    private ConsolidatedMockAccelerator(AcceleratorInfo info, AcceleratorType type)
    {
        _info = info;
        Type = type;
        _memoryManagerMock = CreateMemoryManagerMock();
        _compiledKernelMock = CreateCompiledKernelMock();
    }

    private Mock<IUnifiedMemoryManager> CreateMemoryManagerMock()
    {
        var mock = new Mock<IUnifiedMemoryManager>();

        // Setup memory allocation with failure simulation for common types
        SetupAllocateAsync<float>(mock);
        SetupAllocateAsync<int>(mock);
        SetupAllocateAsync<double>(mock);
        SetupAllocateAsync<byte>(mock);

        // Setup memory statistics
        mock.Setup(m => m.Statistics)
            .Returns(() => new DotCompute.Memory.MemoryStatistics());

        // Setup memory properties
        mock.Setup(m => m.TotalAvailableMemory)
            .Returns(() => ShouldFailMemoryAllocation ? 0L : _info.AvailableMemory);

        mock.Setup(m => m.CurrentAllocatedMemory)
            .Returns(() => ShouldFailMemoryAllocation ? _info.TotalMemory : 0L);

        mock.Setup(m => m.MaxAllocationSize)
            .Returns(() => _info.MaxMemoryAllocationSize);

        return mock;
    }

    private void SetupAllocateAsync<T>(Mock<IUnifiedMemoryManager> mock) where T : unmanaged
    {
        mock.Setup(m => m.AllocateAsync<T>(It.IsAny<int>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .Returns<int, MemoryOptions, CancellationToken>((size, options, ct) =>
            {
                if (ShouldFailMemoryAllocation)
                {
                    var exception = MemoryException ?? new InvalidOperationException("Mock memory allocation failure");
                    return ValueTask.FromException<IUnifiedMemoryBuffer<T>>(exception);
                }

                var buffer = Mock.Of<IUnifiedMemoryBuffer<T>>(b =>
                    b.SizeInBytes == size * System.Runtime.InteropServices.Marshal.SizeOf<T>() &&
                    b.State == BufferState.Allocated &&
                    b.IsDisposed == false);

                return ValueTask.FromResult(buffer);
            });
    }

    private Mock<ICompiledKernel> CreateCompiledKernelMock()
    {
        var mock = new Mock<ICompiledKernel>();

        mock.Setup(k => k.Id).Returns(Guid.NewGuid());
        mock.Setup(k => k.Name).Returns($"MockKernel_{Type}");

        // Setup execution with failure and delay simulation
        mock.Setup(k => k.ExecuteAsync(It.IsAny<KernelArguments>(), It.IsAny<CancellationToken>()))
            .Returns<KernelArguments, CancellationToken>(async (args, ct) =>
            {
                if (ShouldFailExecution)
                {
                    var exception = ExecutionException ?? new InvalidOperationException("Mock kernel execution failure");
                    throw exception;
                }

                if (ExecutionDelay > TimeSpan.Zero)
                {
                    await Task.Delay(ExecutionDelay, ct);
                }
            });

        return mock;
    }

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Track call information
        CompileKernelCoreCalled = true;
        LastCompiledDefinition = definition;
        LastCompilationOptions = options;
        TotalCompilations++;

        // Check for cached kernel if caching is enabled
        var kernelKey = $"{definition.Name}_{definition.GetHashCode()}";
        if (EnableCompilationCaching && _kernelCache.TryGetValue(kernelKey, out var cachedKernel))
        {
            CacheHitCount++;
            LastCompilationTime = stopwatch.Elapsed;
            return ValueTask.FromResult(cachedKernel);
        }

        if (ShouldFailCompilation)
        {
            var exception = CompilationException ?? new InvalidOperationException("Mock kernel compilation failure");
            return ValueTask.FromException<ICompiledKernel>(exception);
        }

        // Cache the compiled kernel
        if (EnableCompilationCaching)
        {
            _kernelCache[kernelKey] = _compiledKernelMock.Object;
        }

        LastCompilationTime = stopwatch.Elapsed;
        return ValueTask.FromResult(_compiledKernelMock.Object);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        SynchronizeCoreCalled = true;
        TotalSynchronizations++;

        return ExecutionDelay > TimeSpan.Zero
            ? new ValueTask(Task.Delay(ExecutionDelay, cancellationToken))
            : ValueTask.CompletedTask;
    }

    #region Testing Utilities

    /// <summary>
    /// Configures the mock to simulate compilation failures.
    /// </summary>
    public ConsolidatedMockAccelerator SimulateCompilationFailure(Exception? exception = null)
    {
        ShouldFailCompilation = true;
        CompilationException = exception;
        return this;
    }

    /// <summary>
    /// Configures the mock to simulate execution failures.
    /// </summary>
    public ConsolidatedMockAccelerator SimulateExecutionFailure(Exception? exception = null)
    {
        ShouldFailExecution = true;
        ExecutionException = exception;
        return this;
    }

    /// <summary>
    /// Configures the mock to simulate memory allocation failures.
    /// </summary>
    public ConsolidatedMockAccelerator SimulateMemoryFailure(Exception? exception = null)
    {
        ShouldFailMemoryAllocation = true;
        MemoryException = exception;
        return this;
    }

    /// <summary>
    /// Configures the mock to simulate slow operations.
    /// </summary>
    public ConsolidatedMockAccelerator SimulateSlowOperations(TimeSpan delay)
    {
        ExecutionDelay = delay;
        return this;
    }

    /// <summary>
    /// Resets all failure simulations to normal operation.
    /// </summary>
    public ConsolidatedMockAccelerator ResetToNormalOperation()
    {
        ShouldFailCompilation = false;
        ShouldFailExecution = false;
        ShouldFailMemoryAllocation = false;
        ExecutionDelay = TimeSpan.Zero;
        CompilationException = null;
        ExecutionException = null;
        MemoryException = null;
        return this;
    }

    /// <summary>
    /// Resets all tracking state to initial values.
    /// </summary>
    public ConsolidatedMockAccelerator ResetTrackingState()
    {
        CompileKernelCoreCalled = false;
        SynchronizeCoreCalled = false;
        InitializeCoreCalled = false;
        DisposeCallCount = 0;
        LastCompiledDefinition = null;
        LastCompilationOptions = null;
        LastCompilationTime = TimeSpan.Zero;
        TotalCompilations = 0;
        TotalSynchronizations = 0;
        CacheHitCount = 0;
        _kernelCache.Clear();
        return this;
    }

    /// <summary>
    /// Gets the underlying memory manager mock for advanced testing scenarios.
    /// </summary>
    public Mock<IUnifiedMemoryManager> GetMemoryManagerMock() => _memoryManagerMock;

    /// <summary>
    /// Gets the underlying compiled kernel mock for advanced testing scenarios.
    /// </summary>
    public Mock<ICompiledKernel> GetCompiledKernelMock() => _compiledKernelMock;

    #endregion

    #region Disposal

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        DisposeCallCount++;

        // Execute any registered dispose actions
        foreach (var action in _disposeActions)
        {
            try
            {
                action();
            }
            catch
            {
                // Ignore disposal exceptions in mocks
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Registers an action to be executed during disposal for cleanup testing.
    /// </summary>
    public void RegisterDisposeAction(Action action)
    {
        _disposeActions.Add(action);
    }

    #endregion
}

/// <summary>
/// Factory for creating collections of mock accelerators for multi-backend testing.
/// </summary>
public static class MockAcceleratorFactory
{
    /// <summary>
    /// Creates a standard collection of mock accelerators (CPU + GPU).
    /// </summary>
    public static List<IAccelerator> CreateStandardCollection()
    {
        return new List<IAccelerator>
        {
            ConsolidatedMockAccelerator.CreateCpuMock(),
            ConsolidatedMockAccelerator.CreateGpuMock()
        };
    }

    /// <summary>
    /// Creates a comprehensive collection including all accelerator types.
    /// </summary>
    public static List<IAccelerator> CreateComprehensiveCollection()
    {
        return new List<IAccelerator>
        {
            ConsolidatedMockAccelerator.CreateCpuMock(),
            ConsolidatedMockAccelerator.CreateGpuMock(),
            ConsolidatedMockAccelerator.CreateMetalMock()
        };
    }

    /// <summary>
    /// Creates a collection with one accelerator configured to fail for testing error scenarios.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2000:Dispose objects before losing scope", Justification = "Factory method returns objects for caller to manage")]
    public static List<IAccelerator> CreateFailureTestCollection()
    {
        return new List<IAccelerator>
        {
            ConsolidatedMockAccelerator.CreateCpuMock(),
            ConsolidatedMockAccelerator.CreateGpuMock().SimulateCompilationFailure()
        };
    }

    /// <summary>
    /// Creates CPU-only collection for testing fallback scenarios.
    /// </summary>
    public static List<IAccelerator> CreateCpuOnlyCollection()
    {
        return new List<IAccelerator>
        {
            ConsolidatedMockAccelerator.CreateCpuMock()
        };
    }
}