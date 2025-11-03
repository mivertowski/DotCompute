using System;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Defines the computational intensity of a workload.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Used for backend selection and optimization strategy decisions.
/// </remarks>
public enum ComputeIntensity
{
    /// <summary>
    /// Low computational intensity, may not benefit from GPU acceleration.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium computational intensity, benefits from parallel execution.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High computational intensity, strong candidate for GPU acceleration.
    /// </summary>
    High = 2,

    /// <summary>
    /// Very high computational intensity, optimal for GPU execution.
    /// </summary>
    VeryHigh = 3
}
