// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Backends.OpenCL.Infrastructure;

/// <summary>
/// Represents a RAII (Resource Acquisition Is Initialization) handle for OpenCL resources.
/// Provides automatic cleanup on disposal following the IAsyncDisposable pattern.
/// </summary>
/// <typeparam name="T">The type of resource being managed.</typeparam>
/// <remarks>
/// This class implements a robust resource management pattern for OpenCL resources:
/// <list type="bullet">
/// <item><description>Automatic resource cleanup on disposal</description></item>
/// <item><description>Support for both sync and async disposal</description></item>
/// <item><description>Thread-safe disposal using Interlocked operations</description></item>
/// <item><description>Optional cleanup action customization</description></item>
/// <item><description>Finalizer to ensure cleanup in case of missed disposal</description></item>
/// </list>
///
/// Usage example:
/// <code>
/// await using var handle = new OpenCLResourceHandle&lt;IntPtr&gt;(
///     resource: bufferPtr,
///     cleanup: ptr => clReleaseMemObject(ptr)
/// );
/// // Resource automatically cleaned up when handle goes out of scope
/// </code>
/// </remarks>
public sealed class OpenCLResourceHandle<T> : IAsyncDisposable, IDisposable
{
    private T? _resource;
    private readonly Action<T>? _cleanup;
    private readonly Func<T, ValueTask>? _asyncCleanup;
    private int _isDisposed;

    /// <summary>
    /// Gets the managed resource.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the handle has been disposed.</exception>
    public T Resource
    {
        get
        {
            ThrowIfDisposed();
            return _resource!;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this handle has been disposed.
    /// </summary>
    public bool IsDisposed => Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLResourceHandle{T}"/> class with synchronous cleanup.
    /// </summary>
    /// <param name="resource">The resource to manage.</param>
    /// <param name="cleanup">The cleanup action to execute on disposal. If null, no cleanup is performed.</param>
    public OpenCLResourceHandle(T resource, Action<T>? cleanup = null)
    {
        _resource = resource;
        _cleanup = cleanup;
        _asyncCleanup = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLResourceHandle{T}"/> class with asynchronous cleanup.
    /// </summary>
    /// <param name="resource">The resource to manage.</param>
    /// <param name="asyncCleanup">The async cleanup action to execute on disposal. If null, no cleanup is performed.</param>
    public OpenCLResourceHandle(T resource, Func<T, ValueTask>? asyncCleanup)
    {
        _resource = resource;
        _cleanup = null;
        _asyncCleanup = asyncCleanup;
    }

    /// <summary>
    /// Disposes the handle and releases the managed resource asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        try
        {
            if (_resource != null)
            {
                if (_asyncCleanup != null)
                {
                    await _asyncCleanup(_resource).ConfigureAwait(false);
                }
                else if (_cleanup != null)
                {
                    _cleanup(_resource);
                }
                else if (_resource is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (_resource is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        finally
        {
            _resource = default;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Disposes the handle and releases the managed resource synchronously.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            return;
        }

        try
        {
            if (_resource != null)
            {
                if (_cleanup != null)
                {
                    _cleanup(_resource);
                }
                else if (_asyncCleanup != null)
                {
                    // Synchronous disposal must block on async cleanup when no sync alternative exists.
                    // This is unavoidable in the IDisposable pattern when async cleanup is the only option.
                    // Suppressed VSTHRD002 as this is a necessary pattern for IDisposable compatibility.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    _asyncCleanup(_resource).AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                }
                else if (_resource is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        finally
        {
            _resource = default;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Finalizer to ensure resource cleanup in case of missed disposal.
    /// </summary>
    ~OpenCLResourceHandle()
    {
        Dispose();
    }

    /// <summary>
    /// Implicitly converts the handle to its resource value.
    /// </summary>
    /// <param name="handle">The handle to convert.</param>
    /// <returns>The managed resource.</returns>
    public static implicit operator T(OpenCLResourceHandle<T> handle)
    {
        return handle.Resource;
    }

    /// <summary>
    /// Converts the handle to its resource value.
    /// This is an alternate to the implicit operator for CA2225 compliance.
    /// </summary>
    /// <returns>The managed resource.</returns>
    public T ToT()
    {
        return Resource;
    }

    /// <summary>
    /// Creates a resource handle from a resource value.
    /// This is an alternate to the implicit operator for CA2225 compliance.
    /// </summary>
    /// <param name="resource">The resource to wrap.</param>
    /// <returns>A new resource handle.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Factory method pattern is appropriate here for alternate conversion method.")]
    public static OpenCLResourceHandle<T> FromOpenCLResourceHandle(T resource)
    {
        return new OpenCLResourceHandle<T>(resource);
    }

    /// <summary>
    /// Throws an exception if the handle has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the handle has been disposed.</exception>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, typeof(OpenCLResourceHandle<T>));
    }

    /// <summary>
    /// Creates a new resource handle with synchronous cleanup.
    /// </summary>
    /// <param name="resource">The resource to manage.</param>
    /// <param name="cleanup">The cleanup action to execute on disposal.</param>
    /// <returns>A new resource handle.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Factory method pattern is standard for resource handles and improves usability.")]
    public static OpenCLResourceHandle<T> Create(T resource, Action<T> cleanup)
    {
        return new OpenCLResourceHandle<T>(resource, cleanup);
    }

    /// <summary>
    /// Creates a new resource handle with asynchronous cleanup.
    /// </summary>
    /// <param name="resource">The resource to manage.</param>
    /// <param name="asyncCleanup">The async cleanup action to execute on disposal.</param>
    /// <returns>A new resource handle.</returns>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Factory method pattern is standard for resource handles and improves usability.")]
    [SuppressMessage("Naming", "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "Method creates a handle that performs async cleanup, not an async operation itself.")]
    public static OpenCLResourceHandle<T> CreateAsync(T resource, Func<T, ValueTask> asyncCleanup)
    {
        return new OpenCLResourceHandle<T>(resource, asyncCleanup);
    }
}
