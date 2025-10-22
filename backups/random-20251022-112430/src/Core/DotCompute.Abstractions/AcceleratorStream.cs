// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Abstractions
{


    /// <summary>
    /// Represents an execution stream on an accelerator for asynchronous command submission.
    /// Streams allow overlapping of computation and data transfers.
    /// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - "Stream" is appropriate for GPU/compute stream concept
    public abstract class ComputeStream : IAsyncDisposable
#pragma warning restore CA1711
    {
        /// <summary>
        /// Gets the unique identifier for this stream.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the accelerator this stream belongs to.
        /// </summary>
        public abstract IAccelerator Accelerator { get; }

        /// <summary>
        /// Gets whether this stream is the default stream for the accelerator.
        /// </summary>
        public abstract bool IsDefault { get; }

        /// <summary>
        /// Gets the native handle for this stream.
        /// </summary>
        public abstract IntPtr Handle { get; }

        /// <summary>
        /// Gets whether this stream supports asynchronous operations.
        /// </summary>
        public abstract bool SupportsAsync { get; }

        /// <summary>
        /// Enqueues a kernel launch on this stream.
        /// </summary>
        /// <param name="kernel">The compiled kernel to launch.</param>
        /// <param name="gridDim">The grid dimensions.</param>
        /// <param name="blockDim">The block dimensions.</param>
        /// <param name="arguments">The kernel arguments.</param>
        /// <returns>A task representing the kernel execution.</returns>
        public abstract ValueTask LaunchKernelAsync(
            ICompiledKernel kernel,
            Dim3 gridDim,
            Dim3 blockDim,
            params object[] arguments);

        /// <summary>
        /// Enqueues a memory copy operation on this stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="count">The number of elements to copy.</param>
        /// <returns>A task representing the copy operation.</returns>
        public abstract ValueTask CopyAsync<T>(
            IUnifiedMemoryBuffer source,
            IUnifiedMemoryBuffer destination,
            int count) where T : unmanaged;

        /// <summary>
        /// Enqueues a memory fill operation on this stream.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="buffer">The buffer to fill.</param>
        /// <param name="value">The value to fill with.</param>
        /// <param name="count">The number of elements to fill.</param>
        /// <returns>A task representing the fill operation.</returns>
        public abstract ValueTask FillAsync<T>(
            IUnifiedMemoryBuffer buffer,
            T value,
            int count) where T : unmanaged;

        /// <summary>
        /// Synchronizes this stream, waiting for all enqueued operations to complete.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the synchronization.</returns>
        public abstract ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an event that can be used for synchronization.
        /// </summary>
        /// <returns>A new accelerator event.</returns>
        public abstract AcceleratorEvent CreateEvent();

        /// <summary>
        /// Records an event on this stream.
        /// </summary>
        /// <param name="event">The event to record.</param>
        /// <returns>A task representing the record operation.</returns>
        [SuppressMessage("Naming", "CA1716:In virtual/interface member ComputeStream.RecordEventAsync(AcceleratorEvent), rename parameter event", Justification = "Parameter name semantically correct; languages can escape reserved keywords")]
        public abstract ValueTask RecordEventAsync(AcceleratorEvent @event);

        /// <summary>
        /// Waits for an event on this stream.
        /// </summary>
        /// <param name="acceleratorEvent">The event to wait for.</param>
        /// <returns>A task representing the wait operation.</returns>
        public abstract ValueTask WaitForEventAsync(AcceleratorEvent acceleratorEvent);

        /// <summary>
        /// Adds a callback to be called when all currently enqueued operations complete.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <returns>A task representing the callback scheduling.</returns>
        public abstract ValueTask AddCallbackAsync(Action callback);

        /// <summary>
        /// Gets whether this stream has completed all enqueued operations.
        /// </summary>
        /// <returns>True if all operations are complete, false otherwise.</returns>
        public abstract bool IsCompleted { get; }

        /// <summary>
        /// Disposes of this stream asynchronously.
        /// </summary>
        public abstract ValueTask DisposeAsync();
    }

    /// <summary>
    /// Represents an event for stream synchronization.
    /// </summary>
    public abstract class AcceleratorEvent : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Gets the native handle for this event.
        /// </summary>
        public abstract IntPtr Handle { get; }

        /// <summary>
        /// Gets whether this event has been recorded.
        /// </summary>
        public abstract bool IsRecorded { get; }

        /// <summary>
        /// Gets whether this event has completed.
        /// </summary>
        public abstract bool IsCompleted { get; }

        /// <summary>
        /// Synchronously waits for this event to complete.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>True if the event completed within the timeout, false otherwise.</returns>
        public abstract bool Wait(TimeSpan timeout);

        /// <summary>
        /// Asynchronously waits for this event to complete.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes when the event is signaled.</returns>
        public abstract ValueTask WaitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the elapsed time between this event and another event.
        /// </summary>
        /// <param name="startEvent">The start event.</param>
        /// <returns>The elapsed time, or null if not available.</returns>
        public abstract TimeSpan? GetElapsedTime(AcceleratorEvent startEvent);

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources - override in derived classes
                }

                // Dispose unmanaged resources - override in derived classes

                _disposed = true;
            }
        }

        /// <summary>
        /// Disposes of this event.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
