// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Backends.CPU.Intrinsics;

namespace DotCompute.Backends.CPU.Kernels.Simd;

/// <summary>
/// SIMD optimization engine responsible for execution strategy selection,
/// performance optimization, and workload analysis.
/// </summary>
public sealed class SimdOptimizationEngine(SimdSummary capabilities, ExecutorConfiguration config)
{
    private readonly SimdSummary _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    private readonly ExecutorConfiguration _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Determines the optimal execution strategy based on workload characteristics and hardware capabilities.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="elementCount">Number of elements to process.</param>
    /// <param name="context">Execution context with thread-local information.</param>
    /// <returns>Optimal execution strategy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimdExecutionStrategy DetermineExecutionStrategy<T>(long elementCount, ExecutionContext context) where T : unmanaged
    {
        // Quick exit for small workloads
        if (elementCount < _config.MinElementsForVectorization)
        {
            return SimdExecutionStrategy.Scalar;
        }

        // Analyze workload characteristics
        var workloadProfile = AnalyzeWorkload<T>(elementCount, context);

        // Select strategy based on capabilities and workload
        return SelectOptimalStrategy(workloadProfile, elementCount);
    }

    /// <summary>
    /// Analyzes workload characteristics to guide optimization decisions.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="elementCount">Number of elements.</param>
    /// <param name="context">Execution context.</param>
    /// <returns>Workload analysis profile.</returns>
    public static WorkloadProfile AnalyzeWorkload<T>(long elementCount, ExecutionContext context) where T : unmanaged
    {
        var profile = new WorkloadProfile
        {
            ElementType = typeof(T),
            ElementCount = elementCount,
            ElementSizeBytes = System.Runtime.InteropServices.Marshal.SizeOf<T>(),
            TotalDataSizeBytes = elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>(),
            IsAligned = IsMemoryAligned<T>(),
            CacheEfficiency = EstimateCacheEfficiency(elementCount, typeof(T)),
            VectorizationPotential = EstimateVectorizationPotential<T>(elementCount)
        };

        // Consider historical performance if available
        if (context.ThreadExecutions > 0)
        {
            profile.HistoricalPerformance = AnalyzeHistoricalPerformance(context);
        }

        return profile;
    }

    /// <summary>
    /// Selects the optimal execution strategy based on workload analysis.
    /// </summary>
    /// <param name="profile">Workload profile.</param>
    /// <param name="elementCount">Number of elements.</param>
    /// <returns>Selected execution strategy.</returns>
    public SimdExecutionStrategy SelectOptimalStrategy(WorkloadProfile profile, long elementCount)
    {
        // Priority order: AVX-512 > AVX2 > SSE > NEON > Scalar

        // AVX-512 strategy
        if (_capabilities.SupportsAvx512 &&
            elementCount >= _config.MinElementsForAvx512 &&
            profile.VectorizationPotential >= 0.8 &&
            IsTypeOptimalForAvx512(profile.ElementType))
        {
            return SimdExecutionStrategy.Avx512;
        }

        // AVX2 strategy
        if (_capabilities.SupportsAvx2 &&
            elementCount >= _config.MinElementsForAvx2 &&
            profile.VectorizationPotential >= 0.6)
        {
            return SimdExecutionStrategy.Avx2;
        }

        // SSE strategy
        if (_capabilities.SupportsSse2 &&
            profile.VectorizationPotential >= 0.4)
        {
            return SimdExecutionStrategy.Sse;
        }

        // ARM NEON strategy
        if (_capabilities.SupportsAdvSimd &&
            profile.VectorizationPotential >= 0.4)
        {
            return SimdExecutionStrategy.Neon;
        }

        // Fallback to scalar
        return SimdExecutionStrategy.Scalar;
    }

    /// <summary>
    /// Estimates cache efficiency for the given workload.
    /// </summary>
    /// <param name="elementCount">Number of elements.</param>
    /// <param name="elementType">Type of elements.</param>
    /// <returns>Cache efficiency estimate (0.0 to 1.0).</returns>
    public static double EstimateCacheEfficiency(long elementCount, Type elementType)
    {
        var elementSize = System.Runtime.InteropServices.Marshal.SizeOf(elementType);
        var totalBytes = elementCount * elementSize;

        // Rough cache size estimates (in bytes)
        const long L1_CACHE_SIZE = 32 * 1024;      // 32KB typical L1
        const long L2_CACHE_SIZE = 256 * 1024;     // 256KB typical L2
        const long L3_CACHE_SIZE = 8 * 1024 * 1024; // 8MB typical L3

        if (totalBytes <= L1_CACHE_SIZE)
        {
            return 1.0; // Excellent cache efficiency
        }
        else if (totalBytes <= L2_CACHE_SIZE)
        {
            return 0.8; // Good cache efficiency
        }
        else if (totalBytes <= L3_CACHE_SIZE)
        {
            return 0.6; // Moderate cache efficiency
        }
        else
        {
            // Memory-bound workload
            var efficiency = Math.Max(0.2, 1.0 - (double)totalBytes / (L3_CACHE_SIZE * 4));
            return efficiency;
        }
    }

    /// <summary>
    /// Estimates vectorization potential for the workload.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="elementCount">Number of elements.</param>
    /// <returns>Vectorization potential (0.0 to 1.0).</returns>
    public static double EstimateVectorizationPotential<T>(long elementCount) where T : unmanaged
    {
        var elementSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();

        // Base potential based on element count
        var countPotential = Math.Min(1.0, (double)elementCount / 1000.0);

        // Type-specific adjustments
        var typePotential = elementSize switch
        {
            1 => 1.0,  // byte - excellent for vectorization
            2 => 0.95, // short - very good
            4 => 0.9,  // int/float - good
            8 => 0.8,  // long/double - still good
            _ => 0.5   // larger types - limited benefit
        };

        // Memory alignment bonus
        var alignmentBonus = IsMemoryAligned<T>() ? 1.0 : 0.8;

        return countPotential * typePotential * alignmentBonus;
    }

    /// <summary>
    /// Analyzes historical performance to inform future decisions.
    /// </summary>
    /// <param name="context">Execution context with historical data.</param>
    /// <returns>Historical performance analysis.</returns>
    public static HistoricalPerformanceAnalysis AnalyzeHistoricalPerformance(ExecutionContext context)
    {
        var analysis = new HistoricalPerformanceAnalysis
        {
            ExecutionCount = context.ThreadExecutions,
            LastExecutionTime = context.LastExecution,
            AveragePerformance = CalculateAveragePerformance(context),
            TrendDirection = AnalyzeTrend(context)
        };

        return analysis;
    }

    /// <summary>
    /// Determines if the type is optimal for AVX-512 operations.
    /// </summary>
    /// <param name="elementType">Element type to check.</param>
    /// <returns>True if type is optimal for AVX-512.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTypeOptimalForAvx512(Type elementType)
    {
        // AVX-512 is most beneficial for smaller types due to higher lane count
        return elementType == typeof(float) ||
               elementType == typeof(int) ||
               elementType == typeof(short) ||
               elementType == typeof(byte);
    }

    /// <summary>
    /// Checks if memory access is properly aligned for SIMD operations.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <returns>True if aligned access is likely.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMemoryAligned<T>() where T : unmanaged
    {
        var elementSize = System.Runtime.InteropServices.Marshal.SizeOf<T>();

        // Most .NET allocations are aligned to at least 8 bytes
        // Vector operations prefer 16, 32, or 64-byte alignment
        return elementSize <= 8 || (elementSize % 16) == 0;
    }

    /// <summary>
    /// Calculates performance metrics for optimization decisions.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <returns>Performance metrics.</returns>
    private static double CalculateAveragePerformance(ExecutionContext context)
    {
        // Placeholder implementation - would track actual performance metrics
        var timeSinceLastExecution = DateTimeOffset.UtcNow - context.LastExecution;

        // Assume better performance for recent, frequent executions
        if (timeSinceLastExecution.TotalMinutes < 1 && context.ThreadExecutions > 10)
        {
            return 0.9; // High performance
        }
        else if (timeSinceLastExecution.TotalMinutes < 5 && context.ThreadExecutions > 5)
        {
            return 0.7; // Good performance
        }
        else
        {
            return 0.5; // Average performance
        }
    }

    /// <summary>
    /// Analyzes performance trend direction.
    /// </summary>
    /// <param name="context">Execution context.</param>
    /// <returns>Trend direction.</returns>
    private static PerformanceTrend AnalyzeTrend(ExecutionContext context)
    {
        // Simplified trend analysis based on execution frequency
        var timeSinceLastExecution = DateTimeOffset.UtcNow - context.LastExecution;

        if (timeSinceLastExecution.TotalMinutes < 1)
        {
            return PerformanceTrend.Improving;
        }
        else if (timeSinceLastExecution.TotalMinutes < 10)
        {
            return PerformanceTrend.Stable;
        }
        else
        {
            return PerformanceTrend.Declining;
        }
    }
}

#region Supporting Types

/// <summary>
/// Workload analysis profile for optimization decisions.
/// </summary>
public sealed class WorkloadProfile
{
    public Type ElementType { get; init; } = typeof(object);
    public long ElementCount { get; init; }
    public int ElementSizeBytes { get; init; }
    public long TotalDataSizeBytes { get; init; }
    public bool IsAligned { get; init; }
    public double CacheEfficiency { get; init; }
    public double VectorizationPotential { get; init; }
    public HistoricalPerformanceAnalysis? HistoricalPerformance { get; set; }
}

/// <summary>
/// Historical performance analysis for adaptive optimization.
/// </summary>
public sealed class HistoricalPerformanceAnalysis
{
    public long ExecutionCount { get; init; }
    public DateTimeOffset LastExecutionTime { get; init; }
    public double AveragePerformance { get; init; }
    public PerformanceTrend TrendDirection { get; init; }
}

/// <summary>
/// Performance trend indicators.
/// </summary>
public enum PerformanceTrend
{
    Improving,
    Stable,
    Declining
}

/// <summary>
/// SIMD execution strategies based on available instruction sets.
/// </summary>
public enum SimdExecutionStrategy
{
    Scalar,
    Sse,
    Avx2,
    Avx512,
    Neon
}

/// <summary>
/// Execution context for thread-local optimizations.
/// </summary>
public sealed class ExecutionContext(SimdSummary capabilities)
{
    public SimdSummary Capabilities { get; } = capabilities;
    public long ThreadExecutions { get; set; }
    public DateTimeOffset LastExecution { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configuration for the SIMD executor.
/// </summary>
public sealed class ExecutorConfiguration
{
    public long MinElementsForVectorization { get; init; } = 32;
    public long MinElementsForAvx2 { get; init; } = 256;
    public long MinElementsForAvx512 { get; init; } = 1024;
    public bool EnablePrefetching { get; init; } = true;
    public bool EnableUnrolling { get; init; } = true;
    public int UnrollFactor { get; init; } = 4;

    public static ExecutorConfiguration Default => new();

    public static ExecutorConfiguration HighPerformance => new()
    {
        MinElementsForVectorization = 16,
        MinElementsForAvx2 = 128,
        MinElementsForAvx512 = 512,
        UnrollFactor = 8
    };
}


#endregion