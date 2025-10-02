// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Simd;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// Intelligent instruction set selection based on workload characteristics
/// </summary>
public sealed class SimdInstructionSelector(SimdSummary capabilities, ILogger logger) : IDisposable
{
    private readonly SimdSummary _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ExecutorConfiguration _config = ExecutorConfiguration.Default;
    private volatile bool _disposed;

    /// <summary>
    /// Determines the optimal SIMD execution strategy based on workload characteristics
    /// </summary>
    public SimdExecutionStrategy DetermineExecutionStrategy<T>(
        long elementCount,
        ExecutionContext context) where T : unmanaged
    {
        // Quick exit for small workloads
        if (elementCount < _config.MinElementsForVectorization)
        {
            return SimdExecutionStrategy.Scalar;
        }

        // Analyze workload characteristics
        var workloadProfile = AnalyzeWorkload<T>(elementCount, context);

        // Select optimal strategy based on capabilities and workload
        return SelectOptimalStrategy<T>(workloadProfile, context);
    }

    /// <summary>
    /// Analyzes workload characteristics to guide strategy selection
    /// </summary>
    private static WorkloadProfile AnalyzeWorkload<T>(long elementCount, ExecutionContext context) where T : unmanaged
    {
        var profile = new WorkloadProfile
        {
            ElementCount = elementCount,
            ElementSize = Unsafe.SizeOf<T>(),
            DataSize = elementCount * Unsafe.SizeOf<T>(),
            IsFloatingPoint = typeof(T) == typeof(float) || typeof(T) == typeof(double),
            IsLargeDataset = elementCount > 100_000,
            RequiresHighPrecision = typeof(T) == typeof(double),
            ThreadExecutions = context.ThreadExecutions
        };

        // Estimate computational intensity
        profile.ComputationalIntensity = EstimateComputationalIntensity(profile);

        // Analyze memory access patterns
        profile.MemoryBandwidthRequirement = EstimateMemoryBandwidth(profile);

        return profile;
    }

    /// <summary>
    /// Selects the optimal SIMD strategy based on workload profile
    /// </summary>
    private SimdExecutionStrategy SelectOptimalStrategy<T>(
        WorkloadProfile profile,
        ExecutionContext context) where T : unmanaged
    {
        // Strategy selection logic based on multiple factors
        var strategy = EvaluateStrategies<T>(profile);

        _logger.LogDebug("Selected SIMD strategy {Strategy} for workload: {ElementCount} elements, {DataSize} bytes",
            strategy, profile.ElementCount, profile.DataSize);

        return strategy;
    }

    /// <summary>
    /// Evaluates all available strategies and selects the best one
    /// </summary>
    private SimdExecutionStrategy EvaluateStrategies<T>(WorkloadProfile profile) where T : unmanaged
    {
        var scores = new Dictionary<SimdExecutionStrategy, double>();

        // Evaluate AVX-512
        if (_capabilities.SupportsAvx512 && profile.ElementCount >= _config.MinElementsForAvx512)
        {
            scores[SimdExecutionStrategy.Avx512] = EvaluateAvx512Score<T>(profile);
        }

        // Evaluate AVX2
        if (_capabilities.SupportsAvx2 && profile.ElementCount >= _config.MinElementsForAvx2)
        {
            scores[SimdExecutionStrategy.Avx2] = EvaluateAvx2Score<T>(profile);
        }

        // Evaluate SSE
        if (_capabilities.SupportsSse2)
        {
            scores[SimdExecutionStrategy.Sse] = EvaluateSseScore<T>(profile);
        }

        // Evaluate ARM NEON
        if (_capabilities.SupportsAdvSimd)
        {
            scores[SimdExecutionStrategy.Neon] = EvaluateNeonScore<T>(profile);
        }

        // Always evaluate scalar as fallback
        scores[SimdExecutionStrategy.Scalar] = EvaluateScalarScore<T>(profile);

        // Select strategy with highest score
        return scores.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    /// <summary>
    /// Evaluates AVX-512 suitability score
    /// </summary>
    private static double EvaluateAvx512Score<T>(WorkloadProfile profile) where T : unmanaged
    {
        var score = 100.0; // Base score for AVX-512

        // Favor AVX-512 for large datasets
        if (profile.IsLargeDataset)
        {
            score += 30.0;
        }

        // Favor for floating-point operations
        if (profile.IsFloatingPoint)
        {
            score += 20.0;
        }

        // Consider data type efficiency
        if (typeof(T) == typeof(float))
        {
            score += 25.0; // 16 floats per vector
        }
        else if (typeof(T) == typeof(double))
        {
            score += 15.0; // 8 doubles per vector
        }

        // Penalty for overhead on small datasets
        if (profile.ElementCount < 10000)
        {
            score -= 40.0;
        }

        // Consider computational intensity
        score += profile.ComputationalIntensity * 10.0;

        return Math.Max(0, score);
    }

    /// <summary>
    /// Evaluates AVX2 suitability score
    /// </summary>
    private static double EvaluateAvx2Score<T>(WorkloadProfile profile) where T : unmanaged
    {
        var score = 80.0; // Base score for AVX2

        // Good balance for medium to large datasets
        if (profile.ElementCount >= 1000 && profile.ElementCount <= 100_000)
        {
            score += 25.0;
        }

        // Favor for floating-point operations
        if (profile.IsFloatingPoint)
        {
            score += 15.0;
        }

        // Consider data type efficiency
        if (typeof(T) == typeof(float))
        {
            score += 20.0; // 8 floats per vector
        }
        else if (typeof(T) == typeof(double))
        {
            score += 10.0; // 4 doubles per vector
        }

        // Better for moderate computational intensity
        if (profile.ComputationalIntensity >= 0.3 && profile.ComputationalIntensity <= 0.8)
        {
            score += 15.0;
        }

        return Math.Max(0, score);
    }

    /// <summary>
    /// Evaluates SSE suitability score
    /// </summary>
    private static double EvaluateSseScore<T>(WorkloadProfile profile) where T : unmanaged
    {
        var score = 60.0; // Base score for SSE

        // Good for smaller datasets
        if (profile.ElementCount >= 100 && profile.ElementCount <= 10_000)
        {
            score += 20.0;
        }

        // Consider data type efficiency
        if (typeof(T) == typeof(float))
        {
            score += 15.0; // 4 floats per vector
        }
        else if (typeof(T) == typeof(double))
        {
            score += 10.0; // 2 doubles per vector
        }

        // Lower overhead makes it suitable for frequent small operations
        if (profile.ThreadExecutions > 100)
        {
            score += 10.0;
        }

        return Math.Max(0, score);
    }

    /// <summary>
    /// Evaluates ARM NEON suitability score
    /// </summary>
    private static double EvaluateNeonScore<T>(WorkloadProfile profile) where T : unmanaged
    {
        var score = 70.0; // Base score for NEON

        // NEON is efficient for ARM processors
        if (profile.IsFloatingPoint)
        {
            score += 20.0;
        }

        // Good for mobile/embedded scenarios with moderate datasets
        if (profile.ElementCount >= 500 && profile.ElementCount <= 50_000)
        {
            score += 15.0;
        }

        return Math.Max(0, score);
    }

    /// <summary>
    /// Evaluates scalar execution suitability score
    /// </summary>
    private static double EvaluateScalarScore<T>(WorkloadProfile profile) where T : unmanaged
    {
        var score = 20.0; // Base score for scalar

        // Favor scalar for very small datasets
        if (profile.ElementCount < 100)
        {
            score += 50.0;
        }

        // Scalar has no SIMD overhead
        if (profile.ComputationalIntensity < 0.2)
        {
            score += 20.0;
        }

        // Always available as fallback
        score += 10.0;

        return score;
    }

    /// <summary>
    /// Estimates computational intensity of the workload
    /// </summary>
    private static double EstimateComputationalIntensity(WorkloadProfile profile)
    {
        // Simple heuristic based on data size and operations
        var intensity = 0.5; // Base intensity

        if (profile.IsFloatingPoint)
        {
            intensity += 0.2; // Floating-point operations are more compute-intensive
        }

        if (profile.IsLargeDataset)
        {
            intensity += 0.1; // Large datasets benefit more from vectorization
        }

        if (profile.RequiresHighPrecision)
        {
            intensity += 0.1; // High precision suggests complex computations
        }

        return Math.Min(1.0, intensity);
    }

    /// <summary>
    /// Estimates memory bandwidth requirements
    /// </summary>
    private static double EstimateMemoryBandwidth(WorkloadProfile profile)
    {
        // Estimate bandwidth in GB/s based on data size and access patterns
        var bandwidth = profile.DataSize / (1024.0 * 1024.0 * 1024.0); // Convert to GB

        // Assume processing takes roughly 1 second for estimation
        return bandwidth;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebug("SIMD Instruction Selector disposed");
        }
    }
}

/// <summary>
/// Workload characteristics for strategy selection
/// </summary>
internal sealed class WorkloadProfile
{
    /// <summary>
    /// Gets or sets the element count.
    /// </summary>
    /// <value>The element count.</value>
    public long ElementCount { get; init; }
    /// <summary>
    /// Gets or sets the element size.
    /// </summary>
    /// <value>The element size.</value>
    public int ElementSize { get; init; }
    /// <summary>
    /// Gets or sets the data size.
    /// </summary>
    /// <value>The data size.</value>
    public long DataSize { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether floating point.
    /// </summary>
    /// <value>The is floating point.</value>
    public bool IsFloatingPoint { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether large dataset.
    /// </summary>
    /// <value>The is large dataset.</value>
    public bool IsLargeDataset { get; init; }
    /// <summary>
    /// Gets or sets the requires high precision.
    /// </summary>
    /// <value>The requires high precision.</value>
    public bool RequiresHighPrecision { get; init; }
    /// <summary>
    /// Gets or sets the thread executions.
    /// </summary>
    /// <value>The thread executions.</value>
    public long ThreadExecutions { get; init; }
    /// <summary>
    /// Gets or sets the computational intensity.
    /// </summary>
    /// <value>The computational intensity.</value>
    public double ComputationalIntensity { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth requirement.
    /// </summary>
    /// <value>The memory bandwidth requirement.</value>
    public double MemoryBandwidthRequirement { get; set; }
}