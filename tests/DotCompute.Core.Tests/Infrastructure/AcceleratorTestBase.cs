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
        // This will be implemented when CPU backend is available
        throw new NotImplementedException("CPU accelerator not yet implemented");
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
        // This will be implemented when GPU backends are available
        throw new NotImplementedException("GPU accelerator not yet implemented");
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
        // Will be implemented with actual GPU detection
        return false;
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