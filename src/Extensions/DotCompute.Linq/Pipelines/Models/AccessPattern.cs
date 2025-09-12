// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Pipelines.Models;

/// <summary>
/// Memory access pattern classifications for pipeline operations.
/// </summary>
public enum AccessPattern
{
    /// <summary>
    /// Sequential access pattern - data accessed in order.
    /// </summary>
    Sequential,

    /// <summary>
    /// Random access pattern - data accessed in unpredictable order.
    /// </summary>
    Random,

    /// <summary>
    /// Strided access pattern - data accessed with fixed stride.
    /// </summary>
    Strided,

    /// <summary>
    /// Coalesced access pattern - optimal GPU memory access pattern.
    /// </summary>
    Coalesced,

    /// <summary>
    /// Scattered access pattern - highly irregular memory access.
    /// </summary>
    Scattered
}

/// <summary>
/// Pipeline characteristics that include access pattern information.
/// </summary>
public class PipelineCharacteristics
{
    /// <summary>
    /// Gets or sets the memory access pattern for this pipeline.
    /// </summary>
    public AccessPattern AccessPattern { get; set; } = AccessPattern.Sequential;

    /// <summary>
    /// Gets or sets the memory intensity from 0.0 to 1.0.
    /// </summary>
    public double MemoryIntensity { get; set; }

    /// <summary>
    /// Gets or sets the compute intensity from 0.0 to 1.0.
    /// </summary>
    public double ComputeIntensity { get; set; }

    /// <summary>
    /// Gets or sets the parallelism level from 0.0 to 1.0.
    /// </summary>
    public double ParallelismLevel { get; set; }

    /// <summary>
    /// Gets or sets optimization hints for this pipeline.
    /// </summary>
    public List<string> OptimizationHints { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated data size in bytes.
    /// </summary>
    public long EstimatedDataSize { get; set; }

    /// <summary>
    /// Gets or sets whether complex access patterns are detected.
    /// </summary>
    public bool HasComplexAccessPatterns { get; set; }
}