// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Device.Interfaces
{
    /// <summary>
    /// Represents a command queue for scheduling and executing operations on a compute device.
    /// Command queues provide ordered execution of kernels, memory operations, and synchronization primitives.
    /// </summary>
    public interface ICommandQueue : IAsyncDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this command queue.
        /// </summary>
        /// <value>A string uniquely identifying this command queue.</value>
        public string Id { get; }

        /// <summary>
        /// Gets the compute device associated with this command queue.
        /// </summary>
        /// <value>The device that will execute commands from this queue.</value>
        public IComputeDevice Device { get; }

        /// <summary>
        /// Enqueues a compiled kernel for execution with the specified execution context.
        /// </summary>
        /// <param name="kernel">The compiled kernel to execute.</param>
        /// <param name="context">The execution context containing parameters and work group configuration.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous enqueue operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when kernel or context is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the queue is in an invalid state for execution.</exception>
        public ValueTask EnqueueKernelAsync(
            ICompiledKernel kernel,
            KernelExecutionContext context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Enqueues a memory copy operation between device memory allocations.
        /// </summary>
        /// <param name="source">The source device memory allocation.</param>
        /// <param name="destination">The destination device memory allocation.</param>
        /// <param name="sourceOffset">The byte offset in source memory where copying should begin.</param>
        /// <param name="destinationOffset">The byte offset in destination memory where copying should begin.</param>
        /// <param name="sizeInBytes">The number of bytes to copy, or null to copy all remaining data.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous enqueue operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source or destination is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when offsets or size are invalid.</exception>
        public ValueTask EnqueueCopyAsync(
            IDeviceMemory source,
            IDeviceMemory destination,
            long sourceOffset = 0,
            long destinationOffset = 0,
            long? sizeInBytes = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Enqueues a barrier that ensures all previously enqueued operations complete
        /// before any subsequently enqueued operations begin.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous enqueue operation.</returns>
        /// <remarks>
        /// Barriers are useful for enforcing ordering constraints and ensuring data consistency
        /// between dependent operations.
        /// </remarks>
        public ValueTask EnqueueBarrierAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Waits for all currently enqueued operations to complete execution.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous finish operation.</returns>
        /// <remarks>
        /// This method blocks until all commands in the queue have finished executing.
        /// New commands can still be enqueued while waiting.
        /// </remarks>
        public ValueTask FinishAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Flushes the command queue, ensuring all enqueued operations are submitted for execution.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous flush operation.</returns>
        /// <remarks>
        /// Flushing does not wait for operations to complete, it only ensures they are
        /// submitted to the device for execution.
        /// </remarks>
        public ValueTask FlushAsync(CancellationToken cancellationToken = default);
    }
}