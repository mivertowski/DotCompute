// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Abstractions;

/// <summary>
/// Extended memory buffer interface with synchronous operations and additional capabilities.
/// </summary>
public interface ISyncMemoryBuffer : IMemoryBuffer, IDisposable
{
    /// <summary>
    /// Gets a pointer to the host memory, if available.
    /// </summary>
    unsafe void* GetHostPointer();

    /// <summary>
    /// Gets a span over the buffer memory, if available.
    /// </summary>
    unsafe Span<T> AsSpan<T>() where T : unmanaged;

    /// <summary>
    /// Creates a slice of this buffer.
    /// </summary>
    ISyncMemoryBuffer Slice(long offset, long length);

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    bool IsDisposed { get; }
}