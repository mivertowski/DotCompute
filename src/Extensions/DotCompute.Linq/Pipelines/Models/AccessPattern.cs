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
    /// Random access pattern - data accessed in unpredictable order.
    Random,
    /// Strided access pattern - data accessed with fixed stride.
    Strided,
    /// Coalesced access pattern - optimal GPU memory access pattern.
    Coalesced,
    /// Scattered access pattern - highly irregular memory access.
    Scattered
}
/// Pipeline characteristics that include access pattern information.
public class PipelineCharacteristics
    /// Gets or sets the memory access pattern for this pipeline.
    public AccessPattern AccessPattern { get; set; } = AccessPattern.Sequential;
    /// Gets or sets the memory intensity from 0.0 to 1.0.
    public double MemoryIntensity { get; set; }
    /// Gets or sets the compute intensity from 0.0 to 1.0.
    public double ComputeIntensity { get; set; }
    /// Gets or sets the parallelism level from 0.0 to 1.0.
    public double ParallelismLevel { get; set; }
    /// Gets or sets optimization hints for this pipeline.
    public List<string> OptimizationHints { get; set; } = [];
    /// Gets or sets the estimated data size in bytes.
    public long EstimatedDataSize { get; set; }
    /// Gets or sets whether complex access patterns are detected.
    public bool HasComplexAccessPatterns { get; set; }
