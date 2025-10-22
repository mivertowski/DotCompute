// <copyright file="CudaGraphCaptureMode.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Types;

/// <summary>
/// Defines the capture modes for CUDA graph recording.
/// Different modes provide varying levels of thread safety and flexibility.
/// </summary>
public enum CudaGraphCaptureMode
{
    /// <summary>
    /// Global capture mode.
    /// All CUDA operations in the process are captured into the graph.
    /// Not thread-safe, suitable for single-threaded applications.
    /// </summary>
    Global = 0,

    /// <summary>
    /// Thread-local capture mode.
    /// Only CUDA operations from the capturing thread are included in the graph.
    /// Thread-safe, suitable for multi-threaded applications.
    /// </summary>
    ThreadLocal = 1,

    /// <summary>
    /// Relaxed capture mode.
    /// Provides more flexibility in graph construction with automatic synchronization.
    /// Allows capturing of operations that would normally be invalid in strict modes.
    /// Best for complex graph patterns with conditional execution.
    /// </summary>
    Relaxed = 2
}