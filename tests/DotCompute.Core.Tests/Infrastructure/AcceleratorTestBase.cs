// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests.Infrastructure;

/// <summary>
/// Base class for accelerator tests providing common testing infrastructure.
/// </summary>
public abstract class AcceleratorTestBase : IAsyncLifetime
{
    protected ITestOutputHelper Output { get; }
    protected List<IAccelerator> CreatedAccelerators { get; } = new();

    protected AcceleratorTestBase(ITestOutputHelper output)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// Creates an accelerator for testing with automatic disposal tracking.
    /// </summary>
    protected virtual IAccelerator CreateAccelerator()
    {
        var accelerator = CreateAcceleratorCore();
        CreatedAccelerators.Add(accelerator);
        return accelerator;
    }

    /// <summary>
    /// Override in derived classes to create specific accelerator types.
    /// </summary>
    protected abstract IAccelerator CreateAcceleratorCore();

    /// <summary>
    /// Logs a test message with caller information.
    /// </summary>
    protected void Log(string message, [CallerMemberName] string? testName = null)
    {
        Output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{testName}] {message}");
    }

    /// <summary>
    /// Executes an action and measures its execution time.
    /// </summary>
    protected async Task<TimeSpan> MeasureAsync(Func<Task> action, string operationName)
    {
        Log($"Starting {operationName}");
        var start = DateTime.UtcNow;
        
        try
        {
            await action();
            var elapsed = DateTime.UtcNow - start;
            Log($"Completed {operationName} in {elapsed.TotalMilliseconds:F2}ms");
            return elapsed;
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - start;
            Log($"Failed {operationName} after {elapsed.TotalMilliseconds:F2}ms: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Asserts that an async action throws the expected exception.
    /// </summary>
    protected async Task AssertThrowsAsync<TException>(Func<Task> action, string? expectedMessage = null)
        where TException : Exception
    {
        var exception = await Assert.ThrowsAsync<TException>(action);
        
        if (!string.IsNullOrEmpty(expectedMessage))
        {
            exception.Message.Should().Contain(expectedMessage);
        }
    }

    /// <summary>
    /// Creates a test buffer with specified size.
    /// </summary>
    protected IBuffer<T> CreateBuffer<T>(int size, IAccelerator? accelerator = null)
        where T : unmanaged
    {
        accelerator ??= CreateAccelerator();
        return accelerator.Memory.Allocate<T>(size);
    }

    /// <summary>
    /// Creates and initializes a test buffer with data.
    /// </summary>
    protected async Task<IBuffer<T>> CreateBufferWithDataAsync<T>(T[] data, IAccelerator? accelerator = null)
        where T : unmanaged
    {
        accelerator ??= CreateAccelerator();
        var buffer = accelerator.Memory.Allocate<T>(data.Length);
        await buffer.CopyFromAsync(data);
        return buffer;
    }

    /// <summary>
    /// Validates accelerator information.
    /// </summary>
    protected void ValidateAcceleratorInfo(AcceleratorInfo info)
    {
        info.Should().NotBeNull();
        info.Name.Should().NotBeNullOrWhiteSpace();
        info.Vendor.Should().NotBeNullOrWhiteSpace();
        info.DriverVersion.Should().NotBeNullOrWhiteSpace();
        info.Type.Should().BeDefined();
        info.ComputeCapability.Should().BeGreaterThan(0);
        info.MaxThreadsPerBlock.Should().BeGreaterThan(0);
        info.MaxSharedMemoryPerBlock.Should().BeGreaterThan(0);
        info.TotalMemory.Should().BeGreaterThan(0);
        info.AvailableMemory.Should().BeGreaterThan(0);
        info.AvailableMemory.Should().BeLessThanOrEqualTo(info.TotalMemory);
    }

    /// <summary>
    /// Validates kernel compilation result.
    /// </summary>
    protected void ValidateCompiledKernel<TDelegate>(ICompiledKernel<TDelegate> kernel)
        where TDelegate : Delegate
    {
        kernel.Should().NotBeNull();
        kernel.Id.Should().NotBeEmpty();
        kernel.Info.Should().NotBeNull();
        kernel.Info.Name.Should().NotBeNullOrWhiteSpace();
        kernel.Info.RegisterCount.Should().BeGreaterThanOrEqualTo(0);
        kernel.Info.SharedMemorySize.Should().BeGreaterThanOrEqualTo(0);
        kernel.Delegate.Should().NotBeNull();
    }

    public virtual Task InitializeAsync()
    {
        Log("Test initialization started");
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        Log($"Disposing {CreatedAccelerators.Count} accelerators");
        
        foreach (var accelerator in CreatedAccelerators)
        {
            try
            {
                await accelerator.DisposeAsync();
            }
            catch (Exception ex)
            {
                Log($"Failed to dispose accelerator: {ex.Message}");
            }
        }
        
        CreatedAccelerators.Clear();
        Log("Test cleanup completed");
    }
}

/// <summary>
/// Base class for CPU accelerator tests.
/// </summary>
public abstract class CpuAcceleratorTestBase : AcceleratorTestBase
{
    protected CpuAcceleratorTestBase(ITestOutputHelper output) : base(output)
    {
    }

    protected override IAccelerator CreateAcceleratorCore()
    {
        // Create a test CPU accelerator implementation
        return new TestCpuAccelerator();
    }
}

/// <summary>
/// Base class for GPU accelerator tests.
/// </summary>
public abstract class GpuAcceleratorTestBase : AcceleratorTestBase
{
    protected GpuAcceleratorTestBase(ITestOutputHelper output) : base(output)
    {
    }

    protected override IAccelerator CreateAcceleratorCore()
    {
        // Check if GPU is available
        if (!IsGpuAvailable())
        {
            throw new SkipException("GPU not available");
        }
        
        // Create a test GPU accelerator implementation
        return new TestGpuAccelerator();
    }

    /// <summary>
    /// Skips test if no GPU is available.
    /// </summary>
    protected void RequireGpu()
    {
        if (!IsGpuAvailable())
        {
            throw new SkipException("GPU not available");
        }
    }

    protected virtual bool IsGpuAvailable()
    {
        try
        {
            // Basic GPU availability check - could be enhanced with actual GPU detection
            return Environment.Is64BitOperatingSystem && 
                   (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS());
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Custom exception for skipping tests.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string reason) : base(reason)
    {
    }
}

/// <summary>
/// Test implementation of CPU accelerator for unit testing.
/// </summary>
internal sealed class TestCpuAccelerator : IAccelerator
{
    private bool _disposed;

    public string Name => "TestCPU";
    
    public AcceleratorInfo Info => new AcceleratorInfo
    {
        Name = "TestCPU",
        DeviceType = AcceleratorType.CPU,
        ComputeCapability = new Version(1, 0),
        MaxComputeUnits = Environment.ProcessorCount,
        MaxWorkGroupSize = 1024,
        MaxMemoryAllocationSize = 1024 * 1024 * 1024L, // 1GB
        LocalMemorySize = 32 * 1024, // 32KB
        IsUnifiedMemory = true,
        SupportedExtensions = new[] { "test-extension" }
    };

    public IMemoryManager Memory { get; }

    public TestCpuAccelerator()
    {
        Memory = new TestMemoryManager();
    }

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestCpuAccelerator));

        // Return a test kernel implementation
        var kernel = new TestCompiledKernel(definition.Name);
        return ValueTask.FromResult<ICompiledKernel>(kernel);
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestCpuAccelerator));

        // CPU synchronization is immediate
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        
        _disposed = true;
        Memory?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test implementation of GPU accelerator for unit testing.
/// </summary>
internal sealed class TestGpuAccelerator : IAccelerator
{
    private bool _disposed;

    public string Name => "TestGPU";
    
    public AcceleratorInfo Info => new AcceleratorInfo
    {
        Name = "TestGPU",
        DeviceType = AcceleratorType.GPU,
        ComputeCapability = new Version(8, 0),
        MaxComputeUnits = 108,
        MaxWorkGroupSize = 1024,
        MaxMemoryAllocationSize = 8L * 1024 * 1024 * 1024, // 8GB
        LocalMemorySize = 48 * 1024, // 48KB
        IsUnifiedMemory = false,
        SupportedExtensions = new[] { "gpu-test-extension", "opencl", "cuda" }
    };

    public IMemoryManager Memory { get; }

    public TestGpuAccelerator()
    {
        Memory = new TestMemoryManager();
    }

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = default,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestGpuAccelerator));

        // Simulate compilation delay
        await Task.Delay(10, cancellationToken);
        
        // Return a test kernel implementation
        var kernel = new TestCompiledKernel(definition.Name);
        return kernel;
    }

    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestGpuAccelerator));

        // Simulate GPU synchronization delay
        await Task.Delay(1, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        
        _disposed = true;
        Memory?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test implementation of memory manager for unit testing.
/// </summary>
internal sealed class TestMemoryManager : IMemoryManager
{
    private bool _disposed;

    public long TotalMemoryBytes => 1024 * 1024 * 1024L; // 1GB
    public long AvailableMemoryBytes => TotalMemoryBytes / 2;

    public IMemoryBuffer<T> AllocateBuffer<T>(long elementCount) where T : unmanaged
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestMemoryManager));

        return new TestMemoryBuffer<T>(elementCount);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// Test implementation of memory buffer for unit testing.
/// </summary>
internal sealed class TestMemoryBuffer<T> : IMemoryBuffer<T> where T : unmanaged
{
    private readonly T[] _data;
    private bool _disposed;

    public TestMemoryBuffer(long elementCount)
    {
        ElementCount = elementCount;
        SizeInBytes = elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        _data = new T[elementCount];
    }

    public long ElementCount { get; }
    public long SizeInBytes { get; }
    public IntPtr NativePointer => IntPtr.Zero; // Not supported in test implementation

    public void CopyFrom(ReadOnlySpan<T> source, long offset = 0)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestMemoryBuffer<T>));

        source.CopyTo(_data.AsSpan((int)offset));
    }

    public void CopyTo(Span<T> destination, long offset = 0, int? count = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestMemoryBuffer<T>));

        var sourceSpan = _data.AsSpan((int)offset, count ?? (int)(ElementCount - offset));
        sourceSpan.CopyTo(destination);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// Test implementation of compiled kernel for unit testing.
/// </summary>
internal sealed class TestCompiledKernel : ICompiledKernel
{
    private bool _disposed;

    public TestCompiledKernel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public void SetArgument<T>(int index, T value) where T : unmanaged
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestCompiledKernel));

        // Store argument for test purposes
    }

    public void SetArgument<T>(int index, IMemoryBuffer<T> buffer) where T : unmanaged
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestCompiledKernel));

        // Store buffer argument for test purposes
    }

    public void Execute(int[] globalWorkSize, int[]? localWorkSize = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TestCompiledKernel));

        // Simulate kernel execution
        System.Threading.Thread.Sleep(1);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}