// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// Represents a CUDA stream for asynchronous operations.
/// </summary>
public struct CudaStream : IEquatable<CudaStream>
{
    /// <summary>
    /// Gets the stream handle.
    /// </summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Gets a value indicating whether this is the default stream.
    /// </summary>
    public bool IsDefault => Handle == IntPtr.Zero;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaStream"/> struct.
    /// </summary>
    /// <param name="handle">The stream handle.</param>
    public CudaStream(IntPtr handle)
    {
        Handle = handle;
    }

    /// <summary>
    /// Gets the default CUDA stream.
    /// </summary>
    public static CudaStream Default => new(IntPtr.Zero);

    /// <summary>
    /// Creates a new non-blocking stream.
    /// </summary>
    /// <returns>A new CUDA stream.</returns>
    public static CudaStream CreateNonBlocking() => new(IntPtr.Zero); // Actual implementation would call CUDA API

    /// <inheritdoc/>
    public bool Equals(CudaStream other) => Handle == other.Handle;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CudaStream stream && Equals(stream);

    /// <inheritdoc/>
    public override int GetHashCode() => Handle.GetHashCode();

    /// <summary>
    /// Determines whether two <see cref="CudaStream"/> instances are equal.
    /// </summary>
    public static bool operator ==(CudaStream left, CudaStream right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="CudaStream"/> instances are not equal.
    /// </summary>
    public static bool operator !=(CudaStream left, CudaStream right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"CudaStream({Handle:X})";
}