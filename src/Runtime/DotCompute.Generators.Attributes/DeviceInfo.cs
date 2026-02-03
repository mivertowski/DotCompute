// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

/// <summary>
/// Provides information about the compute device and selected backend.
/// </summary>
/// <remarks>
/// This type is returned by orchestrator device detection to help
/// consumers understand what compute capabilities are available.
/// </remarks>
public sealed class DeviceInfo
{
    /// <summary>
    /// Gets a value indicating whether a GPU is available for compute.
    /// </summary>
    public bool HasGpu { get; init; }

    /// <summary>
    /// Gets the preferred compute backend based on hardware detection.
    /// </summary>
    public ComputeBackend PreferredBackend { get; init; }

    /// <summary>
    /// Gets the name of the compute device (GPU model or CPU name).
    /// </summary>
    public string DeviceName { get; init; } = "Unknown";

    /// <summary>
    /// Gets the total memory available on the device in bytes.
    /// </summary>
    public long TotalMemory { get; init; }

    /// <summary>
    /// Gets the compute capability or driver version string.
    /// </summary>
    public string? ComputeCapability { get; init; }

    /// <summary>
    /// Gets the maximum number of concurrent threads supported.
    /// </summary>
    public int MaxThreads { get; init; } = 1;

    /// <summary>
    /// Gets a value indicating whether unified memory is supported.
    /// </summary>
    public bool SupportsUnifiedMemory { get; init; }
}
