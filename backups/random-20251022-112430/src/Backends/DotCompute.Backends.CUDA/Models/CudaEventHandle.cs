// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Handle for managed CUDA events with automatic cleanup.
    /// </summary>
    public sealed class CudaEventHandle : IDisposable
    {
        private readonly CudaEventManager _manager;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the CudaEventHandle class.
        /// </summary>
        /// <param name="eventId">The unique event identifier.</param>
        /// <param name="eventHandle">The native CUDA event handle.</param>
        /// <param name="manager">The event manager that owns this event.</param>
        /// <param name="type">The type of event.</param>
        internal CudaEventHandle(EventId eventId, IntPtr eventHandle, CudaEventManager manager, CudaEventType type)
        {
            EventId = eventId;
            Handle = eventHandle;
            Type = type;
            _manager = manager;
        }

        /// <summary>
        /// Gets the unique identifier for this event.
        /// </summary>
        public EventId EventId { get; }

        /// <summary>
        /// Gets the native CUDA event handle.
        /// </summary>
        public IntPtr Handle { get; }

        /// <summary>
        /// Gets the type of this event.
        /// </summary>
        public CudaEventType Type { get; }

        /// <summary>
        /// Disposes the event handle and returns it to the pool.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _manager.ReturnEventToPool(EventId);
            }
        }
    }
}