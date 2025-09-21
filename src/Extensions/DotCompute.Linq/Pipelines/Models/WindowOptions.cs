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
    /// Padding mode for handling boundary conditions.
    public PaddingMode PaddingMode { get; set; } = PaddingMode.None;
    /// Whether to enable result caching for repeated window operations.
    public bool EnableResultCaching { get; set; } = true;
    /// Operation timeout in milliseconds.
    public int TimeoutMs { get; set; } = 30000;
    /// Whether to use GPU acceleration for window operations.
    public bool EnableGpuAcceleration { get; set; } = false; // Window ops are often CPU-friendly
    /// Buffer size for intermediate results during window processing.
    public int BufferSize { get; set; } = 4096;
}
/// Padding modes for handling boundary conditions in window operations.
public enum PaddingMode
    /// No padding - skip incomplete windows at boundaries.
    None,
    /// Pad with zeros for incomplete windows.
    Zero,
    /// Repeat the boundary value for incomplete windows.
    Replicate,
    /// Reflect values across the boundary for incomplete windows.
    Reflect
