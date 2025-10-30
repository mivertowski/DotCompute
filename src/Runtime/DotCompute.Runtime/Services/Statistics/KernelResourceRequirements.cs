// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Accelerators;

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Kernel resource requirements
/// </summary>
public class KernelResourceRequirements
{
    /// <summary>
    /// Gets the required memory in bytes
    /// </summary>
    public long RequiredMemoryBytes { get; init; }

    /// <summary>
    /// Gets the required shared memory in bytes
    /// </summary>
    public long RequiredSharedMemoryBytes { get; init; }

    /// <summary>
    /// Gets the required compute capability
    /// </summary>
    public Version? RequiredComputeCapability { get; init; }

    /// <summary>
    /// Gets the required accelerator features
    /// </summary>
    public AcceleratorFeature RequiredFeatures { get; init; }

    /// <summary>
    /// Gets the estimated execution time
    /// </summary>
    public TimeSpan? EstimatedExecutionTime { get; init; }

    /// <summary>
    /// Gets additional resource metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
