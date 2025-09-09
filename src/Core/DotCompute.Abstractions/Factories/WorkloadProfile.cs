// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Factories;

/// <summary>
/// Profile describing workload characteristics for optimal accelerator selection.
/// </summary>
public class WorkloadProfile
{
    /// <summary>
    /// Gets or sets whether the workload is compute-intensive.
    /// </summary>
    public bool IsComputeIntensive { get; set; }


    /// <summary>
    /// Gets or sets whether the workload is memory-intensive.
    /// </summary>
    public bool IsMemoryIntensive { get; set; }


    /// <summary>
    /// Gets or sets whether the workload requires real-time processing.
    /// </summary>
    public bool RequiresRealTime { get; set; }


    /// <summary>
    /// Gets or sets whether the workload uses machine learning operations.
    /// </summary>
    public bool UsesMachineLearning { get; set; }


    /// <summary>
    /// Gets or sets the expected level of parallelism.
    /// </summary>
    public int ExpectedParallelism { get; set; } = 1;


    /// <summary>
    /// Gets or sets the preferred backend names.
    /// </summary>
    public List<string> PreferredBackends { get; set; } = [];


    /// <summary>
    /// Gets or sets the memory requirements in bytes.
    /// </summary>
    public long MemoryRequirementBytes { get; set; }


    /// <summary>
    /// Gets or sets whether the workload requires double precision.
    /// </summary>
    public bool RequiresDoublePrecision { get; set; }
}