// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Pipelines.Models;

/// <summary>
/// Configuration options for window operations (sliding window, tumbling window, etc.).
/// </summary>
public class WindowOptions
{
    /// <summary>
    /// Stride between window positions (1 = every element, 2 = every other element, etc.).
    /// </summary>
    public int Stride { get; set; } = 1;

    /// <summary>
    /// Padding mode for handling boundary conditions.
    /// </summary>
    public PaddingMode PaddingMode { get; set; } = PaddingMode.None;

    /// <summary>
    /// Whether to enable result caching for repeated window operations.
    /// </summary>
    public bool EnableResultCaching { get; set; } = true;

    /// <summary>
    /// Operation timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Whether to use GPU acceleration for window operations.
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = false; // Window ops are often CPU-friendly

    /// <summary>
    /// Buffer size for intermediate results during window processing.
    /// </summary>
    public int BufferSize { get; set; } = 4096;
}

/// <summary>
/// Padding modes for handling boundary conditions in window operations.
/// </summary>
public enum PaddingMode
{
    /// <summary>
    /// No padding - skip incomplete windows at boundaries.
    /// </summary>
    None,

    /// <summary>
    /// Pad with zeros for incomplete windows.
    /// </summary>
    Zero,

    /// <summary>
    /// Repeat the boundary value for incomplete windows.
    /// </summary>
    Replicate,

    /// <summary>
    /// Reflect values across the boundary for incomplete windows.
    /// </summary>
    Reflect
}