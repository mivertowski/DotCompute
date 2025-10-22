// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Intrinsics;
using DotCompute.Backends.CPU.Kernels.Generators.InstructionSets;
using DotCompute.Backends.CPU.Kernels.Models;

namespace DotCompute.Backends.CPU.Kernels.Generators;

/// <summary>
/// Generates optimized SIMD code for kernel execution using Native AOT-compatible approaches.
/// This factory class selects the appropriate SIMD implementation based on hardware capabilities.
/// </summary>
internal sealed class SimdCodeGenerator(SimdSummary simdCapabilities)
{
    private readonly Dictionary<string, SimdKernelExecutor> _executorCache = [];
    private readonly SimdSummary _simdCapabilities = simdCapabilities ?? throw new ArgumentNullException(nameof(simdCapabilities));

    /// <summary>
    /// Gets or creates a vectorized kernel executor based on hardware capabilities and execution plan.
    /// </summary>
    /// <param name="definition">The kernel definition containing metadata.</param>
    /// <param name="executionPlan">The execution plan with vectorization parameters.</param>
    /// <returns>A specialized SIMD kernel executor.</returns>
    public SimdKernelExecutor GetOrCreateVectorizedKernel(
        KernelDefinition definition,
        KernelExecutionPlan executionPlan)
    {
        var cacheKey = $"{definition.Name}_{executionPlan.VectorWidth}_{executionPlan.VectorizationFactor}";

        if (_executorCache.TryGetValue(cacheKey, out var cachedExecutor))
        {
            return cachedExecutor;
        }

        var executor = CreateSimdExecutor(definition, executionPlan);
        _executorCache[cacheKey] = executor;
        return executor;
    }

    /// <summary>
    /// Creates the appropriate SIMD executor based on vector width and hardware capabilities.
    /// </summary>
    /// <param name="definition">The kernel definition.</param>
    /// <param name="executionPlan">The execution plan.</param>
    /// <returns>A specialized SIMD kernel executor.</returns>
    private SimdKernelExecutor CreateSimdExecutor(
        KernelDefinition definition,
        KernelExecutionPlan executionPlan)
    {
        // Select the appropriate SIMD implementation based on capabilities and execution plan
        return executionPlan.VectorWidth switch
        {
            512 when _simdCapabilities.SupportsAvx512 => new Avx512KernelExecutor(definition, executionPlan),
            256 when _simdCapabilities.SupportsAvx2 => new Avx2KernelExecutor(definition, executionPlan),
            // ARM NEON support for cross-platform deployment
            128 when _simdCapabilities.SupportsAdvSimd => new NeonKernelExecutor(definition, executionPlan),
            128 when _simdCapabilities.SupportsSse2 => new SseKernelExecutor(definition, executionPlan),
            _ => new ScalarKernelExecutor(definition, executionPlan)
        };
    }

    /// <summary>
    /// Gets the cached executor count for monitoring purposes.
    /// </summary>
    public int CachedExecutorCount => _executorCache.Count;

    /// <summary>
    /// Clears the executor cache to free memory.
    /// </summary>
    public void ClearCache() => _executorCache.Clear();
}