// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// Represents the extent (dimensions) of a CUDA array or texture.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CudaExtent : IEquatable<CudaExtent>
{
    /// <summary>
    /// Gets or sets the width in elements.
    /// </summary>
    public ulong Width { get; set; }

    /// <summary>
    /// Gets or sets the height in elements.
    /// </summary>
    public ulong Height { get; set; }

    /// <summary>
    /// Gets or sets the depth in elements.
    /// </summary>
    public ulong Depth { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaExtent"/> struct.
    /// </summary>
    /// <param name="width">The width in elements.</param>
    /// <param name="height">The height in elements.</param>
    /// <param name="depth">The depth in elements.</param>
    public CudaExtent(ulong width, ulong height = 0, ulong depth = 0)
    {
        Width = width;
        Height = height;
        Depth = depth;
    }

    /// <summary>
    /// Creates a 1D extent.
    /// </summary>
    /// <param name="width">The width in elements.</param>
    /// <returns>A new <see cref="CudaExtent"/>.</returns>
    public static CudaExtent Make1D(ulong width) => new(width);

    /// <summary>
    /// Creates a 2D extent.
    /// </summary>
    /// <param name="width">The width in elements.</param>
    /// <param name="height">The height in elements.</param>
    /// <returns>A new <see cref="CudaExtent"/>.</returns>
    public static CudaExtent Make2D(ulong width, ulong height) => new(width, height);

    /// <summary>
    /// Creates a 3D extent.
    /// </summary>
    /// <param name="width">The width in elements.</param>
    /// <param name="height">The height in elements.</param>
    /// <param name="depth">The depth in elements.</param>
    /// <returns>A new <see cref="CudaExtent"/>.</returns>
    public static CudaExtent Make3D(ulong width, ulong height, ulong depth) => new(width, height, depth);

    /// <summary>
    /// Gets the total number of elements.
    /// </summary>
    public ulong TotalElements => Width * Math.Max(1, Height) * Math.Max(1, Depth);

    /// <summary>
    /// Gets the number of dimensions (1, 2, or 3).
    /// </summary>
    public int Dimensions => Depth > 0 ? 3 : Height > 0 ? 2 : 1;

    /// <inheritdoc/>
    public bool Equals(CudaExtent other) => Width == other.Width && Height == other.Height && Depth == other.Depth;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CudaExtent extent && Equals(extent);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Height, Depth);

    /// <summary>
    /// Determines whether two <see cref="CudaExtent"/> instances are equal.
    /// </summary>
    public static bool operator ==(CudaExtent left, CudaExtent right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="CudaExtent"/> instances are not equal.
    /// </summary>
    public static bool operator !=(CudaExtent left, CudaExtent right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => Dimensions switch
    {
        1 => $"({Width})",
        2 => $"({Width}, {Height})",
        3 => $"({Width}, {Height}, {Depth})",
        _ => $"({Width}, {Height}, {Depth})"
    };
}