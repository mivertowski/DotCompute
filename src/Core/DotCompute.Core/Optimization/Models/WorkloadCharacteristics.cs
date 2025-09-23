// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Optimization.Models;

/// <summary>
/// Characteristics of a computational workload used for backend selection.
/// </summary>
public class WorkloadCharacteristics
{
    /// <summary>Size of data being processed in bytes</summary>
    public long DataSize { get; set; }

    /// <summary>Compute intensity from 0.0 (low) to 1.0 (high)</summary>
    public double ComputeIntensity { get; set; }

    /// <summary>Memory intensity from 0.0 (low) to 1.0 (high)</summary>
    public double MemoryIntensity { get; set; }

    /// <summary>Parallelism level from 0.0 (sequential) to 1.0 (highly parallel)</summary>
    public double ParallelismLevel { get; set; }

    /// <summary>Expected number of operations</summary>
    public long OperationCount { get; set; }

    /// <summary>Gets or sets the operation count as an integer for compatibility.</summary>
    public int OperationCountInt => (int)Math.Min(OperationCount, int.MaxValue);

    /// <summary>Gets or sets the parallelism potential from 0.0 (no parallel potential) to 1.0 (highly parallel).</summary>
    public double ParallelismPotential { get; set; }

    /// <summary>Gets or sets the hardware capabilities for this workload.</summary>
    public object? Hardware { get; set; }

    /// <summary>Memory access pattern classification</summary>
    public MemoryAccessPattern AccessPattern { get; set; }

    /// <summary>Additional custom characteristics</summary>
    public Dictionary<string, object> CustomCharacteristics { get; set; } = [];

    /// <summary>Gets or sets optimization hints for this workload.</summary>
    public List<string> OptimizationHints { get; set; } = [];
}