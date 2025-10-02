// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Optimization.Enums;

namespace DotCompute.Core.Optimization.Models;

/// <summary>
/// Unique signature for a workload pattern used for performance history tracking.
/// </summary>
public class WorkloadSignature : IEquatable<WorkloadSignature>
{
    public string KernelName { get; set; } = string.Empty;
    public long DataSize { get; set; }
    public double ComputeIntensity { get; set; }
    public double MemoryIntensity { get; set; }
    public double ParallelismLevel { get; set; }
    public WorkloadPattern WorkloadPattern { get; set; }

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

    public override bool Equals(object? obj) => Equals(obj as WorkloadSignature);

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