// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Timing;

/// <summary>
/// Lightweight CUDA event timer for high-precision GPU execution timing.
/// </summary>
/// <remarks>
/// <para>
/// This static utility provides simple async timing operations using CUDA events
/// without requiring dependency injection infrastructure. Designed for use during
/// kernel compilation, validation, and lightweight profiling scenarios.
/// </para>
/// <para>
/// For comprehensive timing with statistical analysis, use <see cref="Execution.CudaEventManager"/> instead.
/// </para>
/// <para>
/// Timing resolution:
/// - CC 6.0+: ~1 nanosecond (hardware event timestamping)
/// - CC 5.x: ~500 nanoseconds
/// </para>
/// </remarks>
public static class CudaEventTimer
{
    /// <summary>
    /// Creates a CUDA event for high-precision timing.
    /// </summary>
    /// <param name="cudaContext">The CUDA context to make current before creating the event.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes with the created event handle.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when event creation fails or context cannot be set.
    /// </exception>
    /// <remarks>
    /// Creates an event with default flags optimized for timing measurements.
    /// Events created with this method support cudaEventElapsedTime().
    /// </remarks>
    public static async Task<IntPtr> CreateEventAsync(
        IntPtr cudaContext,
        CancellationToken cancellationToken = default)
    {
        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(cudaContext));
        }

        return await Task.Run(() =>
        {
            // Make context current (CUDA contexts are thread-local)
            var setContextResult = Native.CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set CUDA context: {setContextResult}");
            }

            // Create timing event (default flags enable timing)
            var eventPtr = IntPtr.Zero;
            var createResult = Native.CudaRuntime.cudaEventCreate(ref eventPtr);
            if (createResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to create CUDA event: {createResult}");
            }

            return eventPtr;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records a CUDA event in the specified stream.
    /// </summary>
    /// <param name="eventPtr">The event handle to record.</param>
    /// <param name="stream">The CUDA stream to record the event in.</param>
    /// <param name="cudaContext">The CUDA context to make current before recording.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the event is recorded.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when event recording fails or context cannot be set.
    /// </exception>
    /// <remarks>
    /// The event will be recorded after all prior work in the stream completes.
    /// Use this to mark start/end points for timing measurements.
    /// </remarks>
    public static async Task RecordEventAsync(
        IntPtr eventPtr,
        CudaStream stream,
        IntPtr cudaContext,
        CancellationToken cancellationToken = default)
    {
        if (eventPtr == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(eventPtr));
        }
        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(cudaContext));
        }

        await Task.Run(() =>
        {
            // Make context current
            var setContextResult = Native.CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set CUDA context: {setContextResult}");
            }

            // Record event on stream
            var recordResult = Native.CudaRuntime.cudaEventRecord(eventPtr, stream.Handle);
            if (recordResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to record CUDA event: {recordResult}");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronizes with a CUDA event, waiting for it to complete.
    /// </summary>
    /// <param name="eventPtr">The event handle to synchronize with.</param>
    /// <param name="cudaContext">The CUDA context to make current before synchronizing.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the event is synchronized.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when event synchronization fails or context cannot be set.
    /// </exception>
    /// <remarks>
    /// Blocks until all prior work in the stream (up to the event) completes.
    /// Required before calling ElapsedTimeAsync() to ensure accurate measurements.
    /// </remarks>
    public static async Task SynchronizeEventAsync(
        IntPtr eventPtr,
        IntPtr cudaContext,
        CancellationToken cancellationToken = default)
    {
        if (eventPtr == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(eventPtr));
        }
        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(cudaContext));
        }

        await Task.Run(() =>
        {
            // Make context current
            var setContextResult = Native.CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set CUDA context: {setContextResult}");
            }

            // Synchronize event
            var syncResult = Native.CudaRuntime.cudaEventSynchronize(eventPtr);
            if (syncResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to synchronize CUDA event: {syncResult}");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculates elapsed time between two CUDA events in milliseconds.
    /// </summary>
    /// <param name="startEvent">The start event handle.</param>
    /// <param name="endEvent">The end event handle.</param>
    /// <param name="cudaContext">The CUDA context to make current before measuring.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes with the elapsed time in milliseconds.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when elapsed time calculation fails or context cannot be set.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Both events must be created with timing enabled (default for CreateEventAsync).
    /// The end event should be synchronized before calling this method for accurate results.
    /// </para>
    /// <para>
    /// Resolution:
    /// - CC 6.0+: ~0.5 milliseconds (hardware limitation)
    /// - CC 5.x: ~1 millisecond
    /// </para>
    /// </remarks>
    public static async Task<float> ElapsedTimeAsync(
        IntPtr startEvent,
        IntPtr endEvent,
        IntPtr cudaContext,
        CancellationToken cancellationToken = default)
    {
        if (startEvent == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(startEvent));
        }
        if (endEvent == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(endEvent));
        }
        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(cudaContext));
        }

        return await Task.Run(() =>
        {
            // Make context current
            var setContextResult = Native.CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set CUDA context: {setContextResult}");
            }

            // Calculate elapsed time
            var milliseconds = 0f;
            var elapsedResult = Native.CudaRuntime.cudaEventElapsedTime(ref milliseconds, startEvent, endEvent);
            if (elapsedResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to calculate elapsed time: {elapsedResult}");
            }

            return milliseconds;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Destroys a CUDA event and releases resources.
    /// </summary>
    /// <param name="eventPtr">The event handle to destroy.</param>
    /// <param name="cudaContext">The CUDA context to make current before destroying.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes when the event is destroyed.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when event destruction fails or context cannot be set.
    /// </exception>
    /// <remarks>
    /// The event should be synchronized before destruction to avoid undefined behavior.
    /// </remarks>
    public static async Task DestroyEventAsync(
        IntPtr eventPtr,
        IntPtr cudaContext,
        CancellationToken cancellationToken = default)
    {
        if (eventPtr == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(eventPtr));
        }
        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(cudaContext));
        }

        await Task.Run(() =>
        {
            // Make context current
            var setContextResult = Native.CudaRuntime.cuCtxSetCurrent(cudaContext);
            if (setContextResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set CUDA context: {setContextResult}");
            }

            // Destroy event
            var destroyResult = Native.CudaRuntime.cudaEventDestroy(eventPtr);
            if (destroyResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to destroy CUDA event: {destroyResult}");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Measures the execution time of a GPU operation using CUDA events.
    /// </summary>
    /// <param name="operation">The asynchronous operation to measure (receives stream handle).</param>
    /// <param name="stream">The CUDA stream to record events in.</param>
    /// <param name="cudaContext">The CUDA context to make current.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task that completes with the elapsed time in milliseconds.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when timing fails or context cannot be set.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is a convenience method that:
    /// 1. Creates start and end events
    /// 2. Records start event
    /// 3. Executes the operation
    /// 4. Records end event
    /// 5. Synchronizes end event
    /// 6. Calculates elapsed time
    /// 7. Cleans up events
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var kernelTime = await CudaEventTimer.MeasureAsync(
    ///     async streamHandle => await kernel.LaunchAsync(gridDim, blockDim, streamHandle),
    ///     stream,
    ///     context);
    /// Console.WriteLine($"Kernel executed in {kernelTime:F3} ms");
    /// </code>
    /// </para>
    /// </remarks>
    public static async Task<float> MeasureAsync(
        Func<IntPtr, Task> operation,
        CudaStream stream,
        IntPtr cudaContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (cudaContext == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(cudaContext));
        }

        // Create timing events
        var startEvent = await CreateEventAsync(cudaContext, cancellationToken).ConfigureAwait(false);
        var endEvent = await CreateEventAsync(cudaContext, cancellationToken).ConfigureAwait(false);

        try
        {
            // Record start event
            await RecordEventAsync(startEvent, stream, cudaContext, cancellationToken).ConfigureAwait(false);

            // Execute operation
            await operation(stream.Handle).ConfigureAwait(false);

            // Record end event
            await RecordEventAsync(endEvent, stream, cudaContext, cancellationToken).ConfigureAwait(false);

            // Synchronize to ensure completion
            await SynchronizeEventAsync(endEvent, cudaContext, cancellationToken).ConfigureAwait(false);

            // Calculate elapsed time
            return await ElapsedTimeAsync(startEvent, endEvent, cudaContext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Clean up events
            try
            {
                await DestroyEventAsync(startEvent, cudaContext, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore cleanup errors
            }

            try
            {
                await DestroyEventAsync(endEvent, cudaContext, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
