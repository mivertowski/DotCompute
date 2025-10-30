// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
            return feature.ToUpper(CultureInfo.InvariantCulture) switch
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
        /// Creates a new compute execution context for asynchronous operations.
        /// For CUDA accelerators, this creates a CUDA stream.
        /// For other accelerators, returns a stub implementation.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>A compute execution context for the accelerator.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic method lookup for accelerator-specific CreateStream method is intentional")]
        public static IComputeExecution CreateStream(this IAccelerator accelerator)
        {
            // Check if the accelerator is a specific implementation that supports streams
            if (accelerator.GetType().Name.Contains("Cuda", StringComparison.OrdinalIgnoreCase))
            {
                // For CUDA accelerators, try to use reflection to call the native CreateStream method
                var method = accelerator.GetType().GetMethod("CreateStream");
                if (method != null)
                {
                    return (IComputeExecution)method.Invoke(accelerator, null)!;
                }
            }

            // For other accelerators or if reflection fails, return a stub implementation
            return new StubComputeExecution();
        }

        /// <summary>
        /// Gets performance metrics for the accelerator.
        /// This provides a consistent interface for performance monitoring across all accelerator types.
        /// </summary>
        /// <param name="accelerator">The accelerator instance.</param>
        /// <returns>Performance metrics for the accelerator.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic method lookup for accelerator-specific GetPerformanceMetrics method is intentional")]
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
    /// Represents a compute execution context for asynchronous operations.
    /// </summary>
    public interface IComputeExecution : IDisposable
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
    /// Extension methods for IComputeExecution to provide additional functionality.
    /// </summary>
    public static class ComputeExecutionExtensions
    {
        /// <summary>
        /// Begins capturing operations in this execution context for graph construction.
        /// This is primarily used for CUDA graph functionality.
        /// </summary>
        /// <param name="stream">The compute execution context.</param>
        /// <returns>A task representing the begin capture operation.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic method lookup for stream-specific BeginCapture method is intentional")]
        public static ValueTask BeginCaptureAsync(this IComputeExecution stream)
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
        /// Ends capturing operations in this execution context and returns the captured graph.
        /// This is primarily used for CUDA graph functionality.
        /// </summary>
        /// <param name="stream">The compute execution context.</param>
        /// <returns>A task representing the end capture operation, returning the captured graph.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Dynamic method lookup for stream-specific EndCapture method is intentional")]
        public static ValueTask<object?> EndCaptureAsync(this IComputeExecution stream)
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

                    return new ValueTask<object?>(valueTaskNonNull.AsTask().ContinueWith(t => (object?)t.Result, TaskScheduler.Default));
                }


                if (result is Task<object> taskNonNull)
                {

                    return new ValueTask<object?>(taskNonNull.ContinueWith(t => (object?)t.Result, TaskScheduler.Default));
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
    /// Stub implementation of IComputeExecution for accelerators that don't support execution contexts.
    /// </summary>
    internal class StubComputeExecution : IComputeExecution
    {
        /// <summary>
        /// Performs synchronize.
        /// </summary>
        public void Synchronize()
        {
            // No-op for stub implementation
        }
        /// <summary>
        /// Gets synchronize asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The result of the operation.</returns>

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
        /// <summary>
        /// Gets or sets the kernel executions.
        /// </summary>
        /// <value>The kernel executions.</value>
        public long KernelExecutions => 0;
        /// <summary>
        /// Gets or sets the memory transfers.
        /// </summary>
        /// <value>The memory transfers.</value>
        public long MemoryTransfers => 0;
        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        /// <value>The total execution time.</value>
        public TimeSpan TotalExecutionTime => TimeSpan.Zero;
        /// <summary>
        /// Gets or sets the average kernel time.
        /// </summary>
        /// <value>The average kernel time.</value>
        public TimeSpan AverageKernelTime => TimeSpan.Zero;
        /// <summary>
        /// Gets or sets the total bytes transferred.
        /// </summary>
        /// <value>The total bytes transferred.</value>
        public long TotalBytesTransferred => 0;
    }
}
