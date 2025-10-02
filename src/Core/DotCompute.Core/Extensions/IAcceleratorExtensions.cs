// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Extensions
{
    /// <summary>
    /// Extension methods for IAccelerator to provide backward compatibility
    /// and additional properties required by tests and legacy code.
    /// </summary>
    public static class IAcceleratorExtensions
    {
        /// <summary>
        /// Gets the accelerator name from the Info property.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>The accelerator name.</returns>
        public static string Name(this IAccelerator accelerator)
        {
            ArgumentNullException.ThrowIfNull(accelerator);
            return accelerator.Info!.Name;
        }

        /// <summary>
        /// Gets the accelerator description by combining vendor and device type.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>The accelerator description.</returns>
        public static string Description(this IAccelerator accelerator)
        {
            ArgumentNullException.ThrowIfNull(accelerator);
            return $"{accelerator.Info!.Vendor} {accelerator.Info!.DeviceType}";
        }

        /// <summary>
        /// Gets whether the accelerator is available for use.
        /// This is a simplified check that assumes the accelerator is available if it can be accessed.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>True if the accelerator is available, false otherwise.</returns>
        public static bool IsAvailable(this IAccelerator accelerator)
        {
            ArgumentNullException.ThrowIfNull(accelerator);
            try
            {
                var _ = accelerator.Info;
                return !accelerator.IsDisposed();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the accelerator supports a specific feature.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <param name="feature">The feature to check.</param>
        /// <returns>True if the feature is supported, false otherwise.</returns>
        public static bool SupportsFeature(this IAccelerator accelerator, string feature)
        {
            ArgumentNullException.ThrowIfNull(accelerator);
            ArgumentException.ThrowIfNullOrEmpty(feature);

            // Check if the accelerator has capabilities defined
            if (accelerator.Info!.Capabilities?.ContainsKey(feature) == true)
            {
                var value = accelerator.Info!.Capabilities[feature];
                return value is bool supported && supported;
            }

            // Default feature support based on accelerator type
            return feature.ToLowerInvariant() switch
            {
                "unified_memory" => accelerator.Info!.IsUnifiedMemory,
                "double_precision" => accelerator.Info!.SupportsFloat64,
                "int64" => accelerator.Info!.SupportsInt64,
                "float64" => accelerator.Info!.SupportsFloat64,
                _ => false
            };
        }

        /// <summary>
        /// Gets memory information for the accelerator.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>A task returning memory information.</returns>
        public static ValueTask<MemoryInfo> GetMemoryInfoAsync(this IAccelerator accelerator)
        {
            ArgumentNullException.ThrowIfNull(accelerator);

            var memoryInfo = new MemoryInfo
            {
                TotalMemory = accelerator.Info!.TotalMemory,
                AvailableMemory = accelerator.Info!.AvailableMemory,
                UsedMemory = accelerator.Info!.TotalMemory - accelerator.Info!.AvailableMemory
            };

            return ValueTask.FromResult(memoryInfo);
        }
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
        public void Synchronize();

        /// <summary>
        /// Asynchronously synchronizes the stream.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the synchronization operation.</returns>
        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extension methods for IComputeStream to provide additional functionality.
    /// </summary>
    public static class ComputeStreamExtensions
    {
        /// <summary>
        /// Begins capturing operations in this stream for graph construction.
        /// This is primarily used for CUDA graph functionality.
        /// </summary>
        /// <param name="stream">The compute stream.</param>
        /// <returns>A task representing the begin capture operation.</returns>
        public static ValueTask BeginCaptureAsync(this IComputeStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            // For CUDA streams, try to use reflection to call the native BeginCapture method

            var method = stream.GetType().GetMethod("BeginCapture");
            if (method != null)
            {
                var result = method.Invoke(stream, null);
                if (result is ValueTask valueTask)
                {
                    return valueTask;
                }


                if (result is Task task)
                {

                    return new ValueTask(task);
                }

            }

            // For other stream types, return completed task

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Ends capturing operations in this stream and returns the captured graph.
        /// This is primarily used for CUDA graph functionality.
        /// </summary>
        /// <param name="stream">The compute stream.</param>
        /// <returns>A task representing the end capture operation, returning the captured graph.</returns>
        public static ValueTask<object?> EndCaptureAsync(this IComputeStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            // For CUDA streams, try to use reflection to call the native EndCapture method

            var method = stream.GetType().GetMethod("EndCapture");
            if (method != null)
            {
                var result = method.Invoke(stream, null);
                if (result is ValueTask<object?> valueTask)
                {
                    return valueTask;
                }


                if (result is Task<object?> task)
                {

                    return new ValueTask<object?>(task);
                }


                if (result is ValueTask<object> valueTaskNonNull)
                {

                    return new ValueTask<object?>(valueTaskNonNull.AsTask().ContinueWith(t => (object?)t.Result));
                }


                if (result is Task<object> taskNonNull)
                {

                    return new ValueTask<object?>(taskNonNull.ContinueWith(t => (object?)t.Result));
                }

            }

            // For other stream types, return null

            return ValueTask.FromResult<object?>(null);
        }
    }

    /// <summary>
    /// Represents performance metrics for an accelerator.
    /// </summary>
    public interface IPerformanceMetrics
    {
        /// <summary>
        /// Gets the number of kernel executions performed.
        /// </summary>
        public long KernelExecutions { get; }

        /// <summary>
        /// Gets the number of memory transfer operations performed.
        /// </summary>
        public long MemoryTransfers { get; }

        /// <summary>
        /// Gets the total execution time for all operations.
        /// </summary>
        public TimeSpan TotalExecutionTime { get; }

        /// <summary>
        /// Gets the average kernel execution time.
        /// </summary>
        public TimeSpan AverageKernelTime { get; }

        /// <summary>
        /// Gets the total bytes transferred to/from device memory.
        /// </summary>
        public long TotalBytesTransferred { get; }
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

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

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