// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Infrastructure;

/// <summary>
/// Abstract base class for all OpenCL resource managers providing common initialization,
/// disposal, and state management patterns.
/// </summary>
/// <remarks>
/// <para>
/// This class provides production-grade resource management for OpenCL resources including:
/// <list type="bullet">
/// <item><description>Thread-safe initialization and cleanup</description></item>
/// <item><description>State validation and health checking</description></item>
/// <item><description>Resource tracking with statistics</description></item>
/// <item><description>Proper async disposal patterns</description></item>
/// <item><description>Comprehensive error handling</description></item>
/// </list>
/// </para>
/// <para>
/// Derived classes should override the protected virtual methods to implement specific
/// resource management logic while inheriting common patterns for state management and disposal.
/// </para>
/// <para>
/// Thread Safety: This class is thread-safe. All public methods can be called concurrently
/// from multiple threads. Derived classes must maintain this guarantee.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyResourceManager : OpenCLResourceManager
/// {
///     protected override async ValueTask InitializeResourcesAsync(CancellationToken cancellationToken)
///     {
///         // Initialize your resources here
///         await CreateDeviceResources(cancellationToken);
///     }
///
///     protected override async ValueTask CleanupResourcesAsync()
///     {
///         // Cleanup your resources here
///         await ReleaseDeviceResources();
///     }
/// }
/// </code>
/// </example>
public abstract class OpenCLResourceManager : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _stateLock;
    private volatile ResourceState _state;
    private volatile bool _disposed;
    private readonly DateTimeOffset _createdAt;

    // Resource tracking statistics
    private long _totalOperations;
    private long _successfulOperations;
    private long _failedOperations;
    private DateTimeOffset? _initializedAt;
    private DateTimeOffset? _disposedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLResourceManager"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> is <c>null</c>.
    /// </exception>
    protected OpenCLResourceManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateLock = new SemaphoreSlim(1, 1);
        _state = ResourceState.Created;
        _createdAt = DateTimeOffset.UtcNow;

        _logger.LogDebug(
            "{ManagerType} created at {CreatedAt}",
            GetType().Name, _createdAt);
    }

    /// <summary>
    /// Gets the logger instance for derived classes.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets the current state of the resource manager.
    /// </summary>
    /// <value>
    /// The current resource state (Created, Initializing, Ready, Faulted, Disposing, Disposed).
    /// </value>
    public ResourceState State => _state;

    /// <summary>
    /// Gets a value indicating whether this manager has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes the resource manager and all managed resources asynchronously.
    /// </summary>
    /// <param name="cancellationToken">
    /// A cancellation token to cancel the initialization operation.
    /// </param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the manager has already been initialized or disposed.
    /// </exception>
    /// <exception cref="Exception">
    /// Thrown when resource initialization fails.
    /// </exception>
    /// <remarks>
    /// This method ensures thread-safe initialization and can only be called once.
    /// Subsequent calls will throw <see cref="InvalidOperationException"/>.
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state != ResourceState.Created)
            {
                throw new InvalidOperationException(
                    $"Cannot initialize {GetType().Name} in state {_state}. " +
                    "Initialization can only be performed once from the Created state.");
            }

            _state = ResourceState.Initializing;
            _logger.LogInformation(
                "{ManagerType} initializing resources",
                GetType().Name);

            try
            {
                await InitializeResourcesAsync(cancellationToken).ConfigureAwait(false);

                _state = ResourceState.Ready;
                _initializedAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "{ManagerType} initialization completed in {InitializationTime:F2}ms",
                    GetType().Name,
                    (_initializedAt.Value - _createdAt).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _state = ResourceState.Faulted;
                _logger.LogError(
                    ex,
                    "{ManagerType} initialization failed: {ErrorMessage}",
                    GetType().Name, ex.Message);
                throw;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Validates the current state of the manager and ensures it is ready for operations.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the validation operation.</param>
    /// <returns>
    /// A task that represents the asynchronous validation operation.
    /// Returns <c>true</c> if validation succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method is safe to call at any time and will not throw exceptions.
    /// It performs health checks on managed resources and updates internal state.
    /// </remarks>
    public async Task<bool> ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _state != ResourceState.Ready)
        {
            return false;
        }

        try
        {
            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var isValid = await ValidateStateAsync(cancellationToken).ConfigureAwait(false);

                if (!isValid)
                {
                    _state = ResourceState.Faulted;
                    _logger.LogWarning(
                        "{ManagerType} state validation failed",
                        GetType().Name);
                }

                return isValid;
            }
            finally
            {
                _stateLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "{ManagerType} validation threw exception: {ErrorMessage}",
                GetType().Name, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets comprehensive statistics about the resource manager's operations.
    /// </summary>
    /// <returns>
    /// A <see cref="ResourceManagerStatistics"/> object containing operation counts,
    /// timing information, and health metrics.
    /// </returns>
    public ResourceManagerStatistics GetStatistics()
    {
        var now = DateTimeOffset.UtcNow;
        var uptime = _initializedAt.HasValue ? now - _initializedAt.Value : TimeSpan.Zero;

        return new ResourceManagerStatistics
        {
            ManagerType = GetType().Name,
            State = _state,
            CreatedAt = _createdAt,
            InitializedAt = _initializedAt,
            Uptime = uptime,
            TotalOperations = Interlocked.Read(ref _totalOperations),
            SuccessfulOperations = Interlocked.Read(ref _successfulOperations),
            FailedOperations = Interlocked.Read(ref _failedOperations),
            SuccessRate = CalculateSuccessRate(),
            IsHealthy = _state == ResourceState.Ready && !_disposed
        };
    }

    /// <summary>
    /// Records a successful operation for statistics tracking.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and uses atomic operations for statistics updates.
    /// </remarks>
    protected void RecordSuccess()
    {
        Interlocked.Increment(ref _totalOperations);
        Interlocked.Increment(ref _successfulOperations);
    }

    /// <summary>
    /// Records a failed operation for statistics tracking.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and uses atomic operations for statistics updates.
    /// </remarks>
    protected void RecordFailure()
    {
        Interlocked.Increment(ref _totalOperations);
        Interlocked.Increment(ref _failedOperations);
    }

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if this manager has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the manager has been disposed.
    /// </exception>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the manager is not in the Ready state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the manager is not ready for operations.
    /// </exception>
    protected void ThrowIfNotReady()
    {
        ThrowIfDisposed();

        if (_state != ResourceState.Ready)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} is not ready for operations (current state: {_state}). " +
                "Call InitializeAsync() first.");
        }
    }

    /// <summary>
    /// Initializes resources specific to the derived class.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the initialization.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    /// <remarks>
    /// Derived classes should override this method to perform their specific initialization logic.
    /// This method is called within a lock, so implementations should be thread-safe.
    /// </remarks>
    protected virtual ValueTask InitializeResourcesAsync(CancellationToken cancellationToken)
    {
        // Default implementation does nothing - derived classes override as needed
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cleans up resources specific to the derived class.
    /// </summary>
    /// <returns>A task that represents the asynchronous cleanup operation.</returns>
    /// <remarks>
    /// Derived classes should override this method to perform their specific cleanup logic.
    /// This method is called within a lock during disposal, so implementations should be thread-safe.
    /// </remarks>
    protected virtual ValueTask CleanupResourcesAsync()
    {
        // Default implementation does nothing - derived classes override as needed
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Validates the state of resources specific to the derived class.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the validation.</param>
    /// <returns>
    /// A task that represents the asynchronous validation operation.
    /// Returns <c>true</c> if resources are valid; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Derived classes should override this method to perform their specific validation logic.
    /// This method is called within a lock, so implementations should be thread-safe.
    /// </remarks>
    protected virtual ValueTask<bool> ValidateStateAsync(CancellationToken cancellationToken)
    {
        // Default implementation assumes resources are valid
        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Asynchronously disposes the resource manager and all managed resources.
    /// </summary>
    /// <returns>A task that represents the asynchronous disposal operation.</returns>
    /// <remarks>
    /// This method is safe to call multiple times. Subsequent calls after the first
    /// disposal will be no-ops.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _state = ResourceState.Disposing;
            _logger.LogInformation(
                "{ManagerType} disposing resources",
                GetType().Name);

            try
            {
                await CleanupResourcesAsync().ConfigureAwait(false);

                _disposed = true;
                _state = ResourceState.Disposed;
                _disposedAt = DateTimeOffset.UtcNow;

                var stats = GetStatistics();
                _logger.LogInformation(
                    "{ManagerType} disposed. Lifetime: {Uptime:F2}s, Operations: {TotalOps} " +
                    "(Success: {SuccessOps}, Failed: {FailedOps}, Rate: {SuccessRate:P2})",
                    GetType().Name,
                    stats.Uptime.TotalSeconds,
                    stats.TotalOperations,
                    stats.SuccessfulOperations,
                    stats.FailedOperations,
                    stats.SuccessRate);
            }
            catch (Exception ex)
            {
                _disposed = true;
                _state = ResourceState.Disposed;
                _logger.LogError(
                    ex,
                    "{ManagerType} disposal encountered errors: {ErrorMessage}",
                    GetType().Name, ex.Message);
            }
        }
        finally
        {
            _stateLock.Release();
            _stateLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private double CalculateSuccessRate()
    {
        var total = Interlocked.Read(ref _totalOperations);
        if (total == 0)
        {
            return 0.0;
        }

        var successful = Interlocked.Read(ref _successfulOperations);
        return (double)successful / total;
    }
}

/// <summary>
/// Represents the state of a resource manager.
/// </summary>
public enum ResourceState
{
    /// <summary>
    /// The manager has been created but not yet initialized.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The manager is currently initializing resources.
    /// </summary>
    Initializing = 1,

    /// <summary>
    /// The manager is initialized and ready for operations.
    /// </summary>
    Ready = 2,

    /// <summary>
    /// The manager encountered a fatal error and cannot perform operations.
    /// </summary>
    Faulted = 3,

    /// <summary>
    /// The manager is currently disposing resources.
    /// </summary>
    Disposing = 4,

    /// <summary>
    /// The manager has been disposed and cannot be used.
    /// </summary>
    Disposed = 5
}

/// <summary>
/// Statistics about a resource manager's operations and health.
/// </summary>
public sealed record ResourceManagerStatistics
{
    /// <summary>
    /// Gets the type name of the resource manager.
    /// </summary>
    public required string ManagerType { get; init; }

    /// <summary>
    /// Gets the current state of the manager.
    /// </summary>
    public ResourceState State { get; init; }

    /// <summary>
    /// Gets the timestamp when the manager was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when the manager was initialized, if applicable.
    /// </summary>
    public DateTimeOffset? InitializedAt { get; init; }

    /// <summary>
    /// Gets the uptime since initialization.
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// Gets the total number of operations performed.
    /// </summary>
    public long TotalOperations { get; init; }

    /// <summary>
    /// Gets the number of successful operations.
    /// </summary>
    public long SuccessfulOperations { get; init; }

    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    public long FailedOperations { get; init; }

    /// <summary>
    /// Gets the success rate as a value between 0.0 and 1.0.
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the manager is in a healthy state.
    /// </summary>
    public bool IsHealthy { get; init; }
}
