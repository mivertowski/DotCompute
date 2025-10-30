// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Kernels.Compilation;

/// <summary>
/// Abstract representation of a managed compiled kernel.
/// This interface provides a contract for kernel execution statistics and lifecycle management.
/// </summary>
public abstract class ManagedCompiledKernel : IAsyncDisposable
{
    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    /// <value>The unique name identifier for this kernel.</value>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the target device.
    /// </summary>
    /// <value>The accelerator device this kernel was compiled for.</value>
    public abstract IAccelerator Device { get; }

    /// <summary>
    /// Gets the compiled kernel implementation.
    /// </summary>
    /// <value>The compiled kernel implementation.</value>
    public abstract ICompiledKernel Kernel { get; }

    /// <summary>
    /// Gets the compilation timestamp.
    /// </summary>
    /// <value>The date and time when this kernel was compiled.</value>
    public abstract DateTimeOffset CompilationTime { get; }

    /// <summary>
    /// Gets the number of times this kernel has been executed.
    /// </summary>
    /// <value>The total execution count since compilation.</value>
    public abstract long ExecutionCount { get; }

    /// <summary>
    /// Gets the total execution time across all invocations.
    /// </summary>
    /// <value>The cumulative execution time for all kernel invocations.</value>
    public abstract TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the average execution time per invocation.
    /// </summary>
    /// <value>The mean execution time, or zero if no executions have occurred.</value>
    public abstract TimeSpan AverageExecutionTime { get; }

    /// <summary>
    /// Records an execution of this kernel for performance tracking.
    /// Thread-safe method to update execution statistics.
    /// </summary>
    /// <param name="executionTimeMs">The execution time in milliseconds for this invocation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when executionTimeMs is negative.</exception>
    public abstract void RecordExecution(double executionTimeMs);

    /// <summary>
    /// Asynchronously disposes the managed kernel and its resources.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public abstract ValueTask DisposeAsync();
}
