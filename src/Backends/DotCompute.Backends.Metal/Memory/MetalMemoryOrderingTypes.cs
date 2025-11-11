// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Statistics for Metal memory ordering operations.
/// </summary>
public sealed class MetalMemoryOrderingStatistics
{
    /// <summary>
    /// Gets or sets the total number of fences inserted.
    /// </summary>
    public long TotalFencesInserted { get; set; }

    /// <summary>
    /// Gets or sets the number of device-scope fences.
    /// </summary>
    public long DeviceFences { get; set; }

    /// <summary>
    /// Gets or sets the number of threadgroup-scope fences.
    /// </summary>
    public long ThreadgroupFences { get; set; }

    /// <summary>
    /// Gets or sets the number of texture-scope fences.
    /// </summary>
    public long TextureFences { get; set; }

    /// <summary>
    /// Gets or sets the current consistency model.
    /// </summary>
    public string ConsistencyModel { get; set; } = "Relaxed";

    /// <summary>
    /// Gets or sets whether causal ordering is enabled.
    /// </summary>
    public bool CausalOrderingEnabled { get; set; }

    /// <summary>
    /// Gets or sets the estimated performance multiplier.
    /// </summary>
    public double PerformanceMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Gets when these statistics were last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configuration for Metal memory ordering provider.
/// </summary>
public sealed class MetalMemoryOrderingConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable memory ordering profiling.
    /// Default: false (minimal overhead).
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets whether to validate fence insertions.
    /// Default: true (ensures correctness).
    /// </summary>
    public bool ValidateFenceInsertions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to optimize fence placement.
    /// Default: true (removes redundant fences).
    /// </summary>
    public bool OptimizeFencePlacement { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum fence optimization passes.
    /// Default: 3 (balance between optimization and compile time).
    /// </summary>
    public int MaxOptimizationPasses { get; set; } = 3;
}
