// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using DotCompute.Abstractions;

namespace DotCompute.Core.Extensions
{
    /// <summary>
    /// Extension methods for IAccelerator to provide backward compatibility
    /// and additional properties required by tests and legacy code.
    /// </summary>
    public static class IAcceleratorExtensions
    {
        /// <summary>
        /// Determines if the accelerator has been disposed.
        /// Since IAccelerator implements IAsyncDisposable, we can't directly check disposal status.
        /// This provides a reasonable assumption for test compatibility.
        /// </summary>
        public static bool IsDisposed(this IAccelerator accelerator)
        {
            // For most implementations, we can assume the accelerator is not disposed
            // if we can access its properties without throwing
            try
            {
                var _ = accelerator.Info;
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
            catch
            {
                return false; // If other exceptions occur, assume not disposed
            }
        }

        /// <summary>
        /// Creates a new compute stream for asynchronous operations.
        /// For CUDA accelerators, this creates a CUDA stream.
        /// For other accelerators, returns a stub implementation.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>A compute stream for the accelerator.</returns>
        public static IComputeStream CreateStream(this IAccelerator accelerator)
        {
            // Check if the accelerator is a specific implementation that supports streams
            if (accelerator.GetType().Name.Contains("Cuda", StringComparison.OrdinalIgnoreCase))
            {
                // For CUDA accelerators, try to use reflection to call the native CreateStream method
                var method = accelerator.GetType().GetMethod("CreateStream");
                if (method != null)
                {
                    return (IComputeStream)method.Invoke(accelerator, null)!;
                }
            }

            // For other accelerators or if reflection fails, return a stub stream
            return new StubComputeStream();
        }

        /// <summary>
        /// Gets performance metrics for the accelerator.
        /// This provides a consistent interface for performance monitoring across all accelerator types.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>Performance metrics for the accelerator.</returns>
        public static IPerformanceMetrics GetPerformanceMetrics(this IAccelerator accelerator)
        {
            // Check if the accelerator has a native GetPerformanceMetrics method
            var method = accelerator.GetType().GetMethod("GetPerformanceMetrics");
            if (method != null)
            {
                return (IPerformanceMetrics)method.Invoke(accelerator, null)!;
            }

            // Return a stub implementation for accelerators without native performance metrics
            return new StubPerformanceMetrics();
        }
    }

    /// <summary>
    /// Represents a compute stream for asynchronous operations.
    /// </summary>
    public interface IComputeStream : IDisposable
    {
        /// <summary>
        /// Synchronizes the stream, waiting for all operations to complete.
        /// </summary>
        void Synchronize();

        /// <summary>
        /// Asynchronously synchronizes the stream.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the synchronization operation.</returns>
        ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents performance metrics for an accelerator.
    /// </summary>
    public interface IPerformanceMetrics
    {
        /// <summary>
        /// Gets the number of kernel executions performed.
        /// </summary>
        long KernelExecutions { get; }

        /// <summary>
        /// Gets the number of memory transfer operations performed.
        /// </summary>
        long MemoryTransfers { get; }

        /// <summary>
        /// Gets the total execution time for all operations.
        /// </summary>
        TimeSpan TotalExecutionTime { get; }

        /// <summary>
        /// Gets the average kernel execution time.
        /// </summary>
        TimeSpan AverageKernelTime { get; }

        /// <summary>
        /// Gets the total bytes transferred to/from device memory.
        /// </summary>
        long TotalBytesTransferred { get; }
    }

    /// <summary>
    /// Stub implementation of IComputeStream for accelerators that don't support streams.
    /// </summary>
    internal class StubComputeStream : IComputeStream
    {
        public void Synchronize()
        {
            // No-op for stub implementation
        }

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            // No-op for stub implementation
        }
    }

    /// <summary>
    /// Stub implementation of IPerformanceMetrics for accelerators without native metrics.
    /// </summary>
    internal class StubPerformanceMetrics : IPerformanceMetrics
    {
        public long KernelExecutions => 0;
        public long MemoryTransfers => 0;
        public TimeSpan TotalExecutionTime => TimeSpan.Zero;
        public TimeSpan AverageKernelTime => TimeSpan.Zero;
        public long TotalBytesTransferred => 0;
    }
}