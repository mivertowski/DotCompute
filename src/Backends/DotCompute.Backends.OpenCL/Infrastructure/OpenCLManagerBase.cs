// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Infrastructure;

/// <summary>
/// Abstract base class for all OpenCL resource managers.
/// Provides common infrastructure for lifecycle management, disposal, and validation.
/// Implements the template method pattern for consistent resource management across OpenCL backends.
/// </summary>
/// <remarks>
/// <para>
/// This class establishes the foundation for all OpenCL manager implementations by providing:
/// </para>
/// <list type="bullet">
/// <item>Standardized disposal patterns following async dispose best practices</item>
/// <item>Context and device tracking for all operations</item>
/// <item>Consistent logging infrastructure</item>
/// <item>Thread-safe disposal state management</item>
/// <item>Template methods for customizable cleanup logic</item>
/// </list>
/// <para>
/// Derived classes should implement <see cref="DisposeAsyncCore"/> to perform specific
/// cleanup operations while relying on this base class for consistent disposal semantics.
/// </para>
/// </remarks>
public abstract class OpenCLManagerBase : IAsyncDisposable
{
    /// <summary>
    /// Gets the OpenCL context associated with this manager.
    /// All operations performed by this manager use this context.
    /// </summary>
    protected OpenCLContext Context { get; }

    /// <summary>
    /// Gets the OpenCL device ID associated with this manager.
    /// Specifies which device resources are allocated on and operations execute on.
    /// </summary>
    protected OpenCLDeviceId DeviceId { get; }

    /// <summary>
    /// Gets the logger instance for diagnostic information.
    /// Derived classes should use this for consistent logging across the OpenCL backend.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets whether this manager has been disposed.
    /// Used for disposal state checking and double-disposal prevention.
    /// </summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>
    /// Lock object for thread-safe disposal.
    /// Ensures disposal operations are properly synchronized.
    /// </summary>
    private readonly object _disposeLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLManagerBase"/> class.
    /// </summary>
    /// <param name="context">The OpenCL context for resource management. Cannot be null.</param>
    /// <param name="deviceId">The device ID for operations. Must be valid.</param>
    /// <param name="logger">Logger for diagnostic information. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="context"/> or <paramref name="logger"/> is null.
    /// </exception>
    protected OpenCLManagerBase(
        OpenCLContext context,
        OpenCLDeviceId deviceId,
        ILogger logger)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DeviceId = deviceId;
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Logger.LogDebug(
            "Initializing {ManagerType} for device {DeviceId}",
            GetType().Name,
            deviceId.Handle);
    }

    /// <summary>
    /// Asynchronously disposes the manager and releases all associated resources.
    /// Implements the async dispose pattern with double-disposal protection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method follows the standard async dispose pattern:
    /// </para>
    /// <list type="number">
    /// <item>Checks if already disposed (double-disposal protection)</item>
    /// <item>Calls <see cref="DisposeAsyncCore"/> for derived class cleanup</item>
    /// <item>Sets disposed flag and suppresses finalization</item>
    /// </list>
    /// <para>
    /// The disposal is thread-safe and idempotent. Multiple calls to DisposeAsync
    /// will only execute cleanup logic once.
    /// </para>
    /// </remarks>
    /// <returns>A ValueTask representing the async disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        lock (_disposeLock)
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
        }

        Logger.LogDebug("Disposing {ManagerType}", GetType().Name);

        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Logger.LogInformation("{ManagerType} disposed successfully", GetType().Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during {ManagerType} disposal", GetType().Name);
            throw;
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// When overridden in a derived class, performs the actual disposal of resources.
    /// This method is called by <see cref="DisposeAsync"/> after disposal state checks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived classes should implement this method to:
    /// </para>
    /// <list type="bullet">
    /// <item>Release OpenCL resources (buffers, programs, kernels, etc.)</item>
    /// <item>Clear collections and caches</item>
    /// <item>Cancel pending operations</item>
    /// <item>Dispose of owned disposable objects</item>
    /// </list>
    /// <para>
    /// This method is guaranteed to be called only once, even in concurrent scenarios.
    /// Implementations should handle exceptions gracefully and log appropriately.
    /// </para>
    /// </remarks>
    /// <returns>A ValueTask representing the async cleanup operation.</returns>
    protected abstract ValueTask DisposeAsyncCore();

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if this manager has been disposed.
    /// Should be called at the beginning of all public methods that require the manager to be operational.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method provides consistent disposal checking across all manager implementations.
    /// It uses the modern .NET pattern with <see cref="ObjectDisposedException.ThrowIf(bool, object)"/>.
    /// </para>
    /// <example>
    /// Typical usage in derived class methods:
    /// <code>
    /// public async ValueTask&lt;TResource&gt; AcquireResourceAsync()
    /// {
    ///     ThrowIfDisposed();
    ///     // ... rest of method implementation
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the manager has been disposed and operations can no longer be performed.
    /// </exception>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    /// <summary>
    /// Validates that the OpenCL context is still valid and operational.
    /// Can be called by derived classes to ensure context health before critical operations.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the context has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the context is in an invalid state.</exception>
    protected void ValidateContext()
    {
        ThrowIfDisposed();

        if (Context.IsDisposed)
        {
            throw new InvalidOperationException(
                $"OpenCL context has been disposed. Manager '{GetType().Name}' cannot operate on a disposed context.");
        }
    }

    /// <summary>
    /// Logs an operation start with standardized formatting.
    /// Helper method for consistent operation tracking across managers.
    /// </summary>
    /// <param name="operationName">Name of the operation being started.</param>
    /// <param name="additionalData">Optional additional data to include in log.</param>
    protected void LogOperationStart(string operationName, object? additionalData = null)
    {
        if (additionalData != null)
        {
            Logger.LogDebug(
                "{ManagerType}.{Operation} starting - {Data}",
                GetType().Name,
                operationName,
                additionalData);
        }
        else
        {
            Logger.LogDebug(
                "{ManagerType}.{Operation} starting",
                GetType().Name,
                operationName);
        }
    }

    /// <summary>
    /// Logs an operation completion with standardized formatting.
    /// Helper method for consistent operation tracking across managers.
    /// </summary>
    /// <param name="operationName">Name of the operation that completed.</param>
    /// <param name="additionalData">Optional additional data to include in log.</param>
    protected void LogOperationComplete(string operationName, object? additionalData = null)
    {
        if (additionalData != null)
        {
            Logger.LogDebug(
                "{ManagerType}.{Operation} completed - {Data}",
                GetType().Name,
                operationName,
                additionalData);
        }
        else
        {
            Logger.LogDebug(
                "{ManagerType}.{Operation} completed",
                GetType().Name,
                operationName);
        }
    }

    /// <summary>
    /// Logs an operation failure with exception details and standardized formatting.
    /// Helper method for consistent error tracking across managers.
    /// </summary>
    /// <param name="operationName">Name of the operation that failed.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="additionalData">Optional additional data to include in log.</param>
    protected void LogOperationError(string operationName, Exception exception, object? additionalData = null)
    {
        if (additionalData != null)
        {
            Logger.LogError(
                exception,
                "{ManagerType}.{Operation} failed - {Data}",
                GetType().Name,
                operationName,
                additionalData);
        }
        else
        {
            Logger.LogError(
                exception,
                "{ManagerType}.{Operation} failed",
                GetType().Name,
                operationName);
        }
    }
}
