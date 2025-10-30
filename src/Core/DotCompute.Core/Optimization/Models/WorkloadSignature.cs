// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization.Enums;

namespace DotCompute.Core.Optimization.Models;

/// <summary>
/// Unique signature for a workload pattern used for performance history tracking.
/// </summary>
public class WorkloadSignature : IEquatable<WorkloadSignature>
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the data size.
    /// </summary>
    /// <value>The data size.</value>
    public long DataSize { get; set; }
    /// <summary>
    /// Gets or sets the compute intensity.
    /// </summary>
    /// <value>The compute intensity.</value>
    public double ComputeIntensity { get; set; }
    /// <summary>
    /// Gets or sets the memory intensity.
    /// </summary>
    /// <value>The memory intensity.</value>
    public double MemoryIntensity { get; set; }
    /// <summary>
    /// Gets or sets the parallelism level.
    /// </summary>
    /// <value>The parallelism level.</value>
    public double ParallelismLevel { get; set; }
    /// <summary>
    /// Gets or sets the workload pattern.
    /// </summary>
    /// <value>The workload pattern.</value>
    public WorkloadPattern WorkloadPattern { get; set; }
    /// <summary>
    /// Determines equals.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns>The result of the operation.</returns>

    public bool Equals(WorkloadSignature? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return KernelName == other.KernelName &&
               DataSizeBucket(DataSize) == DataSizeBucket(other.DataSize) &&
               Math.Abs(ComputeIntensity - other.ComputeIntensity) < 0.1 &&
               Math.Abs(MemoryIntensity - other.MemoryIntensity) < 0.1 &&
               Math.Abs(ParallelismLevel - other.ParallelismLevel) < 0.1 &&
               WorkloadPattern == other.WorkloadPattern;
    }
    /// <summary>
    /// Determines equals.
    /// </summary>
    /// <param name="obj">The obj.</param>
    /// <returns>The result of the operation.</returns>

    public override bool Equals(object? obj) => Equals(obj as WorkloadSignature);
    /// <summary>
    /// Gets the hash code.
    /// </summary>
    /// <returns>The hash code.</returns>

    public override int GetHashCode() => HashCode.Combine(
        KernelName,
        DataSizeBucket(DataSize),
        ((int)(ComputeIntensity * 10)),
        ((int)(MemoryIntensity * 10)),
        ((int)(ParallelismLevel * 10)),
        WorkloadPattern);

    private static int DataSizeBucket(long size) => size switch
    {
        < 1024 => 0,           // < 1KB
        < 1024 * 1024 => 1,    // < 1MB
        < 10 * 1024 * 1024 => 2, // < 10MB
        < 100 * 1024 * 1024 => 3, // < 100MB
        _ => 4                 // >= 100MB
    };
}
