// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Interfaces.Pipelines;

/// <summary>
/// Interface for pipeline optimization services that analyze and improve pipeline performance.
/// Provides methods for static analysis, runtime optimization, and performance tuning.
/// </summary>
public interface IPipelineOptimizer
{
    /// <summary>
    /// Analyzes a pipeline and identifies optimization opportunities.
    /// </summary>
    /// <param name="pipeline">The pipeline to analyze</param>
    /// <param name="cancellationToken">Token to cancel the analysis</param>
    /// <returns>Task containing analysis results with optimization recommendations</returns>
    public Task<PipelineAnalysisResult> AnalyzeAsync(IKernelPipeline pipeline, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes a pipeline based on specified optimization types and settings.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="optimizationTypes">Types of optimizations to apply</param>
    /// <param name="settings">Configuration settings for optimization</param>
    /// <param name="cancellationToken">Token to cancel the optimization</param>
    /// <returns>Task containing the optimized pipeline</returns>
    public Task<IKernelPipeline> OptimizeAsync(
        IKernelPipeline pipeline,
        OptimizationType optimizationTypes,
        PipelineOptimizationSettings? settings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs kernel fusion optimization to reduce memory transfers and improve performance.
    /// </summary>
    /// <param name="pipeline">The pipeline to apply kernel fusion to</param>
    /// <param name="fusionCriteria">Criteria for determining which kernels can be fused</param>
    /// <param name="cancellationToken">Token to cancel the fusion process</param>
    /// <returns>Task containing the pipeline with fused kernels</returns>
    public Task<IKernelPipeline> ApplyKernelFusionAsync(
        IKernelPipeline pipeline,
        FusionCriteria fusionCriteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes memory access patterns and buffer management for better performance.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize memory access for</param>
    /// <param name="memoryConstraints">Memory constraints and preferences</param>
    /// <param name="cancellationToken">Token to cancel the optimization</param>
    /// <returns>Task containing the memory-optimized pipeline</returns>
    public Task<IKernelPipeline> OptimizeMemoryAccessAsync(
        IKernelPipeline pipeline,
        MemoryConstraints memoryConstraints,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies backend-specific optimizations based on target compute capabilities.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="targetBackends">Target backends to optimize for</param>
    /// <param name="cancellationToken">Token to cancel the optimization</param>
    /// <returns>Task containing the backend-optimized pipeline</returns>
    public Task<IKernelPipeline> OptimizeForBackendsAsync(
        IKernelPipeline pipeline,
        IEnumerable<string> targetBackends,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes pipeline parallelization and concurrency for maximum throughput.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="parallelismGoals">Goals and constraints for parallelization</param>
    /// <param name="cancellationToken">Token to cancel the optimization</param>
    /// <returns>Task containing the parallelism-optimized pipeline</returns>
    public Task<IKernelPipeline> OptimizeParallelismAsync(
        IKernelPipeline pipeline,
        ParallelismGoals parallelismGoals,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies loop optimizations including unrolling, vectorization, and blocking.
    /// </summary>
    /// <param name="pipeline">The pipeline to apply loop optimizations to</param>
    /// <param name="loopOptimizations">Specific loop optimization techniques to apply</param>
    /// <param name="cancellationToken">Token to cancel the optimization</param>
    /// <returns>Task containing the loop-optimized pipeline</returns>
    public Task<IKernelPipeline> ApplyLoopOptimizationsAsync(
        IKernelPipeline pipeline,
        LoopOptimizations loopOptimizations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs data layout optimizations for better cache performance and memory coalescing.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize data layout for</param>
    /// <param name="layoutPreferences">Preferences for data layout optimization</param>
    /// <param name="cancellationToken">Token to cancel the optimization</param>
    /// <returns>Task containing the data-layout-optimized pipeline</returns>
    public Task<IKernelPipeline> OptimizeDataLayoutAsync(
        IKernelPipeline pipeline,
        DataLayoutPreferences layoutPreferences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that optimizations maintain pipeline correctness and functional equivalence.
    /// </summary>
    /// <param name="originalPipeline">The original pipeline before optimization</param>
    /// <param name="optimizedPipeline">The optimized pipeline to validate</param>
    /// <param name="testInputs">Test inputs for validation</param>
    /// <param name="cancellationToken">Token to cancel the validation</param>
    /// <returns>Task containing validation results</returns>
    public Task<OptimizationValidationResult> ValidateOptimizationAsync(
        IKernelPipeline originalPipeline,
        IKernelPipeline optimizedPipeline,
        IEnumerable<object[]> testInputs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the performance impact of proposed optimizations without applying them.
    /// </summary>
    /// <param name="pipeline">The pipeline to estimate optimization impact for</param>
    /// <param name="proposedOptimizations">Optimizations to estimate impact for</param>
    /// <param name="cancellationToken">Token to cancel the estimation</param>
    /// <returns>Task containing performance impact estimates</returns>
    public Task<OptimizationImpactEstimate> EstimateOptimizationImpactAsync(
        IKernelPipeline pipeline,
        IEnumerable<OptimizationType> proposedOptimizations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a custom optimization pass with user-defined optimization logic.
    /// </summary>
    /// <param name="optimizationName">Name for the custom optimization</param>
    /// <param name="optimizationLogic">Function that performs the optimization</param>
    /// <returns>A custom optimization pass that can be applied to pipelines</returns>
    public IOptimizationPass CreateCustomOptimization(
        string optimizationName,
        Func<IKernelPipeline, Task<IKernelPipeline>> optimizationLogic);

    /// <summary>
    /// Registers a new optimization strategy that can be used in future optimizations.
    /// </summary>
    /// <param name="strategyName">Name of the optimization strategy</param>
    /// <param name="strategy">The optimization strategy implementation</param>
    public void RegisterOptimizationStrategy(string strategyName, IOptimizationStrategy strategy);

    /// <summary>
    /// Gets available optimization strategies for a specific optimization type.
    /// </summary>
    /// <param name="optimizationType">Type of optimization to get strategies for</param>
    /// <returns>Collection of available optimization strategies</returns>
    public IEnumerable<IOptimizationStrategy> GetAvailableStrategies(OptimizationType optimizationType);
}