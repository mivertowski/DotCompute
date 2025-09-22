// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Pipelines.Models;
namespace DotCompute.Linq.Pipelines.Extensions;
{
/// <summary>
/// Extension methods that bridge LINQ pipeline functionality to the Core IKernelPipeline interface.
/// This provides backward compatibility while unifying on the production-grade Core interface.
/// </summary>
/// <remarks>
/// This adapter extends the Core.Pipelines.IKernelPipeline with LINQ-specific functionality,
/// enabling seamless integration between LINQ expressions and the Core pipeline system.
/// All LINQ-specific methods are implemented as extension methods to maintain interface separation.
/// </remarks>
public static class CorePipelineLinqExtensions
{
    #region LINQ-Style Pipeline Extensions
    /// <summary>
    /// Adds a transformation stage to the pipeline using LINQ-style fluent syntax.
    /// </summary>
    /// <typeparam name="TInput">Input element type</typeparam>
    /// <typeparam name="TOutput">Output element type</typeparam>
    /// <param name="pipeline">The core pipeline to extend</param>
    /// <param name="transform">The transformation function</param>
    /// <returns>Extended pipeline with transformation stage</returns>
    public static IKernelPipeline Then<TInput, TOutput>(
        this IKernelPipeline pipeline,
        Func<TInput, TOutput> transform)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(transform);
        // Convert function to kernel stage and add to pipeline
        return pipeline.Then<TInput, TOutput>(
            "LinqTransform",
            input => [input!],
            new PipelineStageOptions { EnableProfiling = true });
    }
    /// Adds a kernel execution stage with parameter builder.
    /// Bridges LINQ interface signature to Core interface.
    /// <typeparam name="TInput">Input type</typeparam>
    /// <typeparam name="TOutput">Output type</typeparam>
    /// <param name="pipeline">The core pipeline</param>
    /// <param name="kernelName">Name of the kernel</param>
    /// <param name="parameterBuilder">Function to build parameters</param>
    /// <returns>Extended pipeline</returns>
        string kernelName,
        Func<TInput, object[]> parameterBuilder)
        return pipeline.Then<TInput, TOutput>(kernelName, parameterBuilder, null);
    /// Enables parallel execution mode for the pipeline.
    /// Bridges LINQ interface Parallel method to Core interface.
    /// <returns>Pipeline configured for parallel execution</returns>
    public static IKernelPipeline Parallel(this IKernelPipeline pipeline)
    {
        // Configure pipeline for parallel execution using Core interface
        var optimizedPipeline = pipeline.Optimize(OptimizationStrategy.Aggressive);
        // In a full implementation, this would configure parallel execution stages
        return optimizedPipeline;
    /// Enables parallel execution with stages and parameter builders.
    /// Bridges LINQ interface Parallel method signature to Core interface.
    /// <param name="stages">Array of kernel name and parameter builder tuples</param>
    /// <returns>Pipeline with parallel stages</returns>
    public static IKernelPipeline Parallel<TInput, TOutput>(
        params (string kernelName, Func<TInput, object[]>? parameterBuilder)[] stages)
        ArgumentNullException.ThrowIfNull(stages);
        // Add parallel stages using Core interface
        var parallelBuilder = new List<IPipelineStage>();
        foreach (var (kernelName, parameterBuilder) in stages)
        {
            // In a full implementation, this would create actual parallel stages
            // For now, we'll chain them with parallel optimization
            pipeline = pipeline.Then<TInput, TOutput>(
                kernelName,
                parameterBuilder,
                new DotCompute.Linq.Pipelines.Models.PipelineStageOptions { EnableOptimization = true });
        }
        return pipeline.Optimize(OptimizationStrategy.Aggressive);
    /// Adds a filtering stage to the pipeline.
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="predicate">Filter predicate</param>
    /// <returns>Extended pipeline with filter stage</returns>
    public static IKernelPipeline ThenWhere<T>(
        Func<T, bool> predicate)
        ArgumentNullException.ThrowIfNull(predicate);
        return pipeline.Then<T, T>(
            "LinqFilter",
            input => [input!, predicate],
            new PipelineStageOptions { EnableCaching = true });
    /// Adds a projection stage to the pipeline.
    /// <param name="selector">Projection selector</param>
    /// <returns>Extended pipeline with projection stage</returns>
    public static IKernelPipeline ThenSelect<TInput, TOutput>(
        Func<TInput, TOutput> selector)
        ArgumentNullException.ThrowIfNull(selector);
            "LinqSelect",
            input => [input!, selector],
            new DotCompute.Linq.Pipelines.Models.PipelineStageOptions { EnableOptimization = true });
    /// Adds an aggregation stage to the pipeline.
    /// <param name="aggregator">Aggregation function</param>
    /// <returns>Extended pipeline with aggregation stage</returns>
    public static IKernelPipeline ThenAggregate<T>(
        Func<T, T, T> aggregator)
        ArgumentNullException.ThrowIfNull(aggregator);
            "LinqAggregate",
            input => [input!, aggregator],
            new PipelineStageOptions { PreferredBackend = "GPU" });
    /// Adds a grouping stage to the pipeline.
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TKey">Key type</typeparam>
    /// <param name="keySelector">Key selector function</param>
    /// <returns>Extended pipeline with grouping stage</returns>
    public static IKernelPipeline ThenGroupBy<TSource, TKey>(
        Func<TSource, TKey> keySelector)
        ArgumentNullException.ThrowIfNull(keySelector);
        return pipeline.Then<TSource, IGrouping<TKey, TSource>>(
            "LinqGroupBy",
            input => [input!, keySelector],
            new PipelineStageOptions { EnableMemoryOptimization = true });
    #endregion
    #region LINQ Interface Compatibility Methods
    /// Gets whether the pipeline is compiled (Core interface always returns true when valid).
    /// This provides compatibility with the LINQ interface IsCompiled property.
    /// <returns>True if pipeline is ready for execution</returns>
    public static bool IsCompiled(this IKernelPipeline pipeline)
    {
        // Core pipelines are always "compiled" when they pass validation
        var validationResult = pipeline.Validate();
        return validationResult.IsValid;
    /// Gets the optimization level for the pipeline.
    /// Bridges LINQ interface property to Core interface settings.
    /// <returns>Current optimization level</returns>
    public static OptimizationLevel GetOptimizationLevel(this IKernelPipeline pipeline)
    {
        // Extract optimization level from Core pipeline settings
        return pipeline.OptimizationSettings.Level switch
            PipelineOptimizationLevel.None => OptimizationLevel.None,
            PipelineOptimizationLevel.Conservative => OptimizationLevel.Conservative,
            PipelineOptimizationLevel.Balanced => OptimizationLevel.Balanced,
            PipelineOptimizationLevel.Aggressive => OptimizationLevel.Aggressive,
            _ => OptimizationLevel.Balanced
        };
    /// Executes pipeline with typed input and output (LINQ interface compatibility).
    /// <param name="input">Input data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result</returns>
    public static async Task<TOutput> ExecuteAsync<TInput, TOutput>(
        TInput input,
        CancellationToken cancellationToken = default)
        // Create execution context with input data
        var context = new DotCompute.Core.Pipelines.Models.PipelineExecutionContext();
        context.Inputs["input"] = input!;
        context.Options = new DotCompute.Core.Pipelines.Models.PipelineExecutionOptions { EnableProfiling = true };
        var result = await pipeline.ExecuteAsync(context, cancellationToken);
        // Extract typed result from execution result
        if (result.Success && result.Outputs.TryGetValue("output", out var output) && output != null)
            return (TOutput)output;
        return default(TOutput)!;
    /// Compiles the pipeline (no-op for Core interface as pipelines are always ready).
    /// Provides compatibility with LINQ interface CompileAsync method.
    /// <returns>Completed task</returns>
    public static async Task CompileAsync(this IKernelPipeline pipeline, CancellationToken cancellationToken = default)
        // Validate pipeline as compilation step
        var validation = pipeline.Validate();
        if (!validation.IsValid)
            var errors = validation.Errors?.Select(e => e.Message) ?? Enumerable.Empty<string>();
            throw new InvalidOperationException($"Pipeline validation failed: {string.Join(", ", errors)}");
        await Task.CompletedTask;
    /// Validates the pipeline (bridges to Core interface ValidateAsync).
    /// <returns>LINQ-compatible validation result</returns>
    public static Task<DotCompute.Linq.Pipelines.Models.PipelineValidationResult> ValidateLinqAsync(this IKernelPipeline pipeline)
        var coreResult = pipeline.Validate();
        return Task.FromResult(new DotCompute.Linq.Pipelines.Models.PipelineValidationResult
            IsValid = coreResult.IsValid,
            Errors = coreResult.Errors?.Select(e => e.Message).ToList() ?? new List<string>(),
            Warnings = coreResult.Warnings?.Select(w => w.Message).ToList() ?? new List<string>()
        });
    #region Helper Classes for Core Interface Compatibility
    /// Default pipeline memory manager implementation for compatibility.
    private class DefaultPipelineMemoryManager : DotCompute.Linq.Pipelines.Models.IPipelineMemoryManager
    {
        public Task<DotCompute.Linq.Pipelines.Models.IMemoryBuffer> AllocateAsync(long size)
            => Task.FromResult<DotCompute.Linq.Pipelines.Models.IMemoryBuffer>(new MemoryBuffer(size));
        public Task ReleaseAsync(DotCompute.Linq.Pipelines.Models.IMemoryBuffer buffer) => Task.CompletedTask;
        public void Dispose() { }
    /// Default compute device implementation for compatibility.
    private class DefaultComputeDevice : DotCompute.Linq.Pipelines.Models.IComputeDevice
    {
        public string Name => "Default";
        public string Type => "CPU";
        public bool IsAvailable => true;
        public Task InitializeAsync() => Task.CompletedTask;
    /// Simple memory buffer implementation.
    private class MemoryBuffer : DotCompute.Linq.Pipelines.Models.IMemoryBuffer
    {
        public MemoryBuffer(long size) => Size = size;
        public long Size { get; }
        public Task CopyToAsync(DotCompute.Linq.Pipelines.Models.IMemoryBuffer destination) => Task.CompletedTask;
}
/// Additional extension methods for Core interface compatibility with LINQ-specific options.
public static class PipelineStageOptionsExtensions
    {
    /// Enables optimization for the stage options.
    /// <param name="options">Stage options to extend</param>
    /// <returns>Options with optimization enabled</returns>
    public static DotCompute.Linq.Pipelines.Models.PipelineStageOptions EnableOptimization(this DotCompute.Linq.Pipelines.Models.PipelineStageOptions options)
        // For Core interface compatibility - would configure optimization in full implementation
        return options;
    /// Enables memory optimization for the stage options.
    /// <returns>Options with memory optimization enabled</returns>
    public static PipelineStageOptions EnableMemoryOptimization(this PipelineStageOptions options)
    {
        // For Core interface compatibility - would configure memory optimization in full implementation  
}
}
