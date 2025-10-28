// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Pool of command queues for efficient reuse.
/// Provides a simplified interface delegating to OpenCLStreamManager for actual pooling.
/// This class exists for backward compatibility and convenience.
/// </summary>
public sealed class OpenCLStreamPool : IAsyncDisposable
{
    private readonly OpenCLStreamManager _manager;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLStreamPool"/> class.
    /// </summary>
    /// <param name="manager">The stream manager that handles actual queue pooling.</param>
    /// <exception cref="ArgumentNullException">Thrown if manager is null.</exception>
    internal OpenCLStreamPool(OpenCLStreamManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>
    /// Acquires a command queue from the pool with specified properties.
    /// This is a convenience wrapper around the stream manager's acquire method.
    /// </summary>
    /// <param name="properties">Queue properties specifying execution mode and profiling.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A handle to the acquired command queue with automatic cleanup.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public async ValueTask<QueueHandle> AcquireAsync(
        QueueProperties properties,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await _manager.AcquireQueueAsync(properties, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires a command queue from the pool with default in-order execution properties.
    /// Provides a simplified interface for common use cases.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A handle to the acquired command queue with automatic cleanup.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public async ValueTask<QueueHandle> AcquireAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var properties = new QueueProperties
        {
            InOrderExecution = true,
            EnableProfiling = false,
            Priority = QueuePriority.Normal
        };

        return await _manager.AcquireQueueAsync(properties, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets pool usage statistics for monitoring and optimization.
    /// </summary>
    /// <returns>Current statistics snapshot.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public QueueStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _manager.GetStatistics();
    }

    /// <summary>
    /// Asynchronously disposes the pool, delegating cleanup to the stream manager.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _manager.DisposeAsync().ConfigureAwait(false);
    }
}
