// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Execution;

/// <summary>
/// Pool of OpenCL events for efficient reuse.
/// Delegates to <see cref="OpenCLEventManager"/> for actual pooling and management.
/// Provides a simplified interface for event acquisition with automatic cleanup.
/// </summary>
public sealed class OpenCLEventPool : IAsyncDisposable
{
    private readonly OpenCLEventManager _manager;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLEventPool"/> class.
    /// </summary>
    /// <param name="manager">The event manager that handles the underlying event lifecycle.</param>
    /// <exception cref="ArgumentNullException">Thrown if manager is null.</exception>
    public OpenCLEventPool(OpenCLEventManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>
    /// Acquires an event from the pool with specified properties.
    /// Events are automatically returned to the pool when disposed.
    /// </summary>
    /// <param name="properties">Properties for the event (profiling, user event, etc.).</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A handle to the acquired event with RAII cleanup semantics.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public async ValueTask<EventHandle> AcquireAsync(
        EventProperties properties,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _manager.AcquireEventAsync(properties, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires a simple event from the pool with default properties.
    /// Convenience method for non-profiling, non-user events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A handle to the acquired event with RAII cleanup semantics.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public async ValueTask<EventHandle> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var defaultProperties = new EventProperties
        {
            EnableProfiling = false,
            IsUserEvent = false
        };

        return await AcquireAsync(defaultProperties, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires an event with profiling enabled.
    /// Profiling events allow timing measurements but cannot be pooled.
    /// </summary>
    /// <param name="name">Optional descriptive name for the profiling event.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A handle to the acquired profiling event with RAII cleanup semantics.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public async ValueTask<EventHandle> AcquireProfilingEventAsync(
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var properties = new EventProperties
        {
            EnableProfiling = true,
            IsUserEvent = false,
            Name = name
        };

        return await AcquireAsync(properties, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets comprehensive statistics about event usage and pool performance.
    /// Useful for monitoring and optimization.
    /// </summary>
    /// <returns>Statistics snapshot showing pool hit rates, active events, etc.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the pool has been disposed.</exception>
    public EventStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _manager.GetStatistics();
    }

    /// <summary>
    /// Asynchronously disposes the event pool and underlying manager.
    /// All active and pooled events are properly cleaned up.
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
