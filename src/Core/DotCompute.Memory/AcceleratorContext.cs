// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Memory;

/// <summary>
/// Represents the context for accelerator operations
/// </summary>
public class AcceleratorContext
{
    /// <summary>
    /// Gets the device identifier
    /// </summary>
    public int DeviceId { get; init; }

    /// <summary>
    /// Gets the stream or queue for async operations
    /// </summary>
    public IntPtr Stream { get; init; }

    /// <summary>
    /// Gets the accelerator type
    /// </summary>
    public AcceleratorType Type { get; init; }

    /// <summary>
    /// Gets custom context data
    /// </summary>
    public object? CustomContext { get; init; }
}

/// <summary>
/// Accelerator type enumeration
/// </summary>
public enum AcceleratorType
{
    /// <summary>
    /// CPU accelerator
    /// </summary>
    CPU,

    /// <summary>
    /// NVIDIA CUDA GPU
    /// </summary>
    CUDA,

    /// <summary>
    /// Apple Metal GPU
    /// </summary>
    Metal,

    /// <summary>
    /// AMD ROCm GPU
    /// </summary>
    ROCm,

    /// <summary>
    /// OpenCL device
    /// </summary>
    OpenCL
}
