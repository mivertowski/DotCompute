// <copyright file="ManagedCompiledKernel.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Execution.Kernels;
using DotCompute.Core.Execution.Statistics;
namespace DotCompute.Core.Execution;

/// <summary>
/// Managed wrapper for compiled kernels that provides additional metadata and lifecycle management.
/// Tracks execution statistics, compilation information, and performance metrics.
/// </summary>
public sealed class ManagedCompiledKernel : IAsyncDisposable
{
    private bool _disposed;
    private long _executionCount;
    private double _totalExecutionTimeMs;

    /// <summary>
    /// Initializes a new instance of the ManagedCompiledKernel class.
    /// </summary>
    /// <param name="name">The name of the kernel.</param>
    /// <param name="device">The target accelerator device.</param>
    /// <param name="kernel">The compiled kernel instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ManagedCompiledKernel(string name, IAccelerator device, CompiledKernel kernel)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Kernel = new CompiledKernelWrapper(kernel);
        CompilationTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    /// <value>The unique name identifier for this kernel.</value>
    public string Name { get; }

    /// <summary>
    /// Gets the target device.
    /// </summary>
    /// <value>The accelerator device this kernel was compiled for.</value>
    public IAccelerator Device { get; }

    /// <summary>
    /// Gets the compiled kernel wrapper.
    /// </summary>
    /// <value>The wrapped compiled kernel implementation.</value>
    public ICompiledKernel Kernel { get; }

    /// <summary>
    /// Gets the compilation timestamp.
    /// </summary>
    /// <value>The date and time when this kernel was compiled.</value>
    public DateTimeOffset CompilationTime { get; }

    /// <summary>
    /// Gets the number of times this kernel has been executed.
    /// </summary>
    /// <value>The total execution count since compilation.</value>
    public long ExecutionCount => _executionCount;

    /// <summary>
    /// Gets the total execution time across all invocations.
    /// </summary>
    /// <value>The cumulative execution time for all kernel invocations.</value>
    public TimeSpan TotalExecutionTime => TimeSpan.FromMilliseconds(_totalExecutionTimeMs);

    /// <summary>
    /// Gets the average execution time per invocation.
    /// </summary>
    /// <value>The mean execution time, or zero if no executions have occurred.</value>
    public TimeSpan AverageExecutionTime => _executionCount > 0
        ? TimeSpan.FromMilliseconds(_totalExecutionTimeMs / _executionCount)
        : TimeSpan.Zero;

    /// <summary>
    /// Gets the execution frequency in executions per minute.
    /// </summary>
    /// <value>The rate of kernel execution based on compilation time and execution count.</value>
    public double ExecutionFrequency
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - CompilationTime;
            return elapsed.TotalMinutes > 0 ? ExecutionCount / elapsed.TotalMinutes : 0;
        }
    }

    /// <summary>
    /// Records an execution of this kernel for performance tracking.
    /// Thread-safe method to update execution statistics.
    /// </summary>
    /// <param name="executionTimeMs">The execution time in milliseconds for this invocation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when executionTimeMs is negative.</exception>
    public void RecordExecution(double executionTimeMs)
    {
        if (executionTimeMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(executionTimeMs), "Execution time cannot be negative");
        }

        _ = Interlocked.Increment(ref _executionCount);

        // Thread-safe update of total execution time
        var current = Interlocked.Exchange(ref _totalExecutionTimeMs, 0);
        var newTotal = current + executionTimeMs;
        while (Interlocked.CompareExchange(ref _totalExecutionTimeMs, newTotal, 0) != 0)
        {
            current = Interlocked.Exchange(ref _totalExecutionTimeMs, 0);
            newTotal = current + executionTimeMs;
        }
    }

    /// <summary>
    /// Gets comprehensive performance statistics for this kernel.
    /// </summary>
    /// <returns>Performance statistics including execution metrics and device information.</returns>
    public KernelPerformanceStatistics GetPerformanceStatistics()
    {
        return new KernelPerformanceStatistics
        {
            KernelName = Name,
            DeviceId = Device.Info.Id,
            ExecutionCount = _executionCount,
            TotalExecutionTimeMs = _totalExecutionTimeMs,
            AverageExecutionTimeMs = _executionCount > 0 ? _totalExecutionTimeMs / _executionCount : 0,
            CompilationTime = CompilationTime
        };
    }

    /// <summary>
    /// Asynchronously disposes the managed kernel and its resources.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (Kernel is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (Kernel is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }
}