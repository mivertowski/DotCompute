// <copyright file="UnifiedTypeFactory.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Core.Analysis;
using DotCompute.Core.Kernels;
using DotCompute.Refactored.Adapters;
using DotCompute.Abstractions.Analysis;
using IComplexityMetrics = DotCompute.Abstractions.Analysis.IComplexityMetrics;
namespace DotCompute.Refactored.Extensions;
{
/// <summary>
/// Factory class for creating unified types with intelligent defaults and cross-cutting concerns.
/// </summary>
public static class UnifiedTypeFactory
{
    /// <summary>
    /// Creates complexity metrics with intelligent defaults based on operator types.
    /// </summary>
    public static class ComplexityMetrics
    {
        /// <summary>
        /// Creates basic complexity metrics for simple operations.
        /// </summary>
        public static UnifiedComplexityMetrics CreateBasic(
        {
            UnifiedOperatorType operatorType,
            long operationCount = 1000,
            long memoryUsage = 8192)
        {
            var complexity = operatorType.GetEstimatedComplexity();
            var parallelPotential = operatorType.SupportsParallelExecution() ? 0.9 : 0.1;
            return UnifiedComplexityMetrics.Builder()
                .WithComputationalComplexity(complexity)
                .WithComputationalComplexityClass(GetComplexityClass(operatorType))
                .WithOperationCount(operationCount)
                .WithMemoryUsage(memoryUsage)
                .WithParallelizationPotential(parallelPotential)
                .WithCacheEfficiency(GetCacheEfficiency(operatorType))
                .Build();
        }
        /// Creates advanced complexity metrics for complex pipeline operations.
        public static UnifiedComplexityMetrics CreateAdvanced(
            IEnumerable<UnifiedOperatorType> operatorTypes,
            long totalOperations,
            long totalMemory,
            Dictionary<string, double>? operationBreakdown = null)
            var operators = operatorTypes.ToList();
            var maxComplexity = operators.Max(op => op.GetEstimatedComplexity());
            var avgParallelPotential = operators.Average(op => op.SupportsParallelExecution() ? 0.8 : 0.2);
            var complexityClass = GetOverallComplexityClass(operators);
            var builder = UnifiedComplexityMetrics.Builder()
                .WithComputationalComplexity(maxComplexity)
                .WithComputationalComplexityClass(complexityClass)
                .WithOperationCount(totalOperations)
                .WithMemoryUsage(totalMemory)
                .WithParallelizationPotential(avgParallelPotential)
                .WithCacheEfficiency(EstimateCacheEfficiency(operators));
            // Add operation breakdown if provided
            if (operationBreakdown != null)
            {
                foreach (var kvp in operationBreakdown)
                {
                    builder.AddOperationComplexity(kvp.Key, kvp.Value);
                }
            }
            // Add memory access patterns based on operators
            foreach (var pattern in EstimateMemoryAccessPatterns(operators))
                builder.AddMemoryAccessPattern(pattern);
            return builder.Build();
        /// Creates complexity metrics for GPU operations.
        public static UnifiedComplexityMetrics CreateGpuOptimized(
            long operationCount,
            long memoryUsage,
            bool useSharedMemory = true)
                .WithComputationalComplexity(operatorType.GetEstimatedComplexity())
                .WithParallelizationPotential(0.95) // High for GPU
                .WithCacheEfficiency(useSharedMemory ? 0.9 : 0.6)
                .AddMemoryAccessPattern(new MemoryAccessComplexity
                    Pattern = MemoryAccessPattern.Coalesced,
                    Frequency = (int)(operationCount / 1000),
                    ComplexityImpact = 0.1,
                    RegionSize = memoryUsage
                })
        private static DotCompute.Abstractions.Analysis.ComplexityClass GetComplexityClass(UnifiedOperatorType operatorType)
            return operatorType switch
                UnifiedOperatorType.Add or UnifiedOperatorType.Subtract or 
                UnifiedOperatorType.Multiply or UnifiedOperatorType.Equal => ComplexityClass.Constant,
                UnifiedOperatorType.OrderBy or UnifiedOperatorType.Sort => ComplexityClass.Linearithmic,
                UnifiedOperatorType.GroupBy => ComplexityClass.Linear,
                UnifiedOperatorType.Join => ComplexityClass.Quadratic,
                _ => ComplexityClass.Linear
            };
        private static ComplexityClass GetOverallComplexityClass(IEnumerable<UnifiedOperatorType> operators)
        {
            var classes = operators.Select(GetComplexityClass);
            return classes.Max();
        private static double GetCacheEfficiency(UnifiedOperatorType operatorType)
                UnifiedOperatorType.Where or UnifiedOperatorType.Select => 0.8,
                UnifiedOperatorType.OrderBy or UnifiedOperatorType.Sort => 0.4,
                UnifiedOperatorType.Join => 0.3,
                UnifiedOperatorType.GroupBy => 0.6,
                _ => 0.7
        private static double EstimateCacheEfficiency(IEnumerable<UnifiedOperatorType> operators)
            return operators.Average(GetCacheEfficiency);
        private static IEnumerable<MemoryAccessComplexity> EstimateMemoryAccessPatterns(IEnumerable<UnifiedOperatorType> operators)
            var patterns = new List<MemoryAccessComplexity>();
            foreach (var op in operators)
                var pattern = op switch
                    UnifiedOperatorType.Where or UnifiedOperatorType.Select => MemoryAccessPattern.Sequential,
                    UnifiedOperatorType.Join => MemoryAccessPattern.Random,
                    UnifiedOperatorType.OrderBy => MemoryAccessPattern.Strided,
                    _ => MemoryAccessPattern.Sequential
                };
                patterns.Add(new MemoryAccessComplexity
                    Pattern = pattern,
                    Frequency = 100,
                    ComplexityImpact = pattern == MemoryAccessPattern.Random ? 0.8 : 0.2,
                    RegionSize = 65536
                });
            return patterns;
    }
    /// Creates generated kernels with best practices and intelligent defaults.
    public static class GeneratedKernel
    {
        /// Creates a basic CPU kernel.
        public static UnifiedGeneratedKernel CreateCpu(
        {
            string name,
            string sourceCode,
            params (string name, Type type, bool isPointer)[] parameters)
            var kernel = UnifiedGeneratedKernelFactory.CreateCpuKernel(name, sourceCode);
            foreach (var (paramName, paramType, isPointer) in parameters)
                kernel.AddParameter(new UnifiedKernelParameter(paramName, paramType, isPointer));
            // Add default CPU optimizations
            kernel.AddOptimizations(new[]
                "SIMD_Vectorization",
                "Loop_Unrolling",
                "Cache_Optimization"
            });
            return kernel;
        /// Creates a basic GPU kernel with CUDA optimizations.
        public static UnifiedGeneratedKernel CreateGpu(
        {
            string cudaSource,
            var kernel = UnifiedGeneratedKernelFactory.CreateGpuKernel(name, cudaSource);
            // Add default GPU optimizations
                "Memory_Coalescing",
                "Shared_Memory_Usage",
                "Occupancy_Optimization",
                "Warp_Efficiency"
            // Add GPU-specific metadata
            kernel.AddMetadata("PreferredBlockSize", 256)
                  .AddMetadata("MinBlocksPerSM", 2)
                  .AddMetadata("SharedMemoryUsage", 8192)
                  .AddMetadata("RegisterUsage", 32);
        /// Creates a kernel optimized for LINQ expressions.
        public static UnifiedGeneratedKernel CreateLinqOptimized(
            UnifiedOperatorType primaryOperator,
            UnifiedComplexityMetrics? complexityMetrics = null)
            var targetBackend = complexityMetrics?.GpuRecommended == true ? "CUDA" : "CPU";
            var language = targetBackend == "CUDA" ? "CUDA" : "C";
            var kernel = new UnifiedGeneratedKernel(name, sourceCode, language, targetBackend);
            // Add operator-specific optimizations
            var optimizations = GetLinqOptimizations(primaryOperator);
            kernel.AddOptimizations(optimizations);
            // Add LINQ-specific metadata
            kernel.AddMetadata("PrimaryOperator", primaryOperator.ToString())
                  .AddMetadata("LinqOptimized", true)
                  .AddMetadata("ParallelExecution", primaryOperator.SupportsParallelExecution());
            // Add complexity analysis if provided
            if (complexityMetrics != null)
                var analysisResult = new SimpleExpressionAnalysisResult(complexityMetrics);
                kernel.WithAnalysis(analysisResult);
        /// Creates a multi-backend kernel that can run on both CPU and GPU.
        public static UnifiedGeneratedKernel CreateMultiBackend(
            string cpuSource,
            string gpuSource,
            UnifiedComplexityMetrics complexity)
            var preferGpu = complexity.GpuRecommended || complexity.ParallelizationPotential > 0.7;
            var primaryBackend = preferGpu ? "CUDA" : "CPU";
            var primarySource = preferGpu ? gpuSource : cpuSource;
            var kernel = new UnifiedGeneratedKernel(name, primarySource, 
                preferGpu ? "CUDA" : "C", primaryBackend);
            // Store alternative implementations
            kernel.AddMetadata("CpuSource", cpuSource)
                  .AddMetadata("GpuSource", gpuSource)
                  .AddMetadata("MultiBackend", true)
                  .AddMetadata("PreferGpu", preferGpu);
            // Add complexity analysis
            var analysisResult = new SimpleExpressionAnalysisResult(complexity);
            kernel.WithAnalysis(analysisResult);
        private static IEnumerable<string> GetLinqOptimizations(UnifiedOperatorType operatorType)
            var baseOptimizations = new List<string> { "Expression_Tree_Optimization", "Type_Inference" };
            var specificOptimizations = operatorType switch
                UnifiedOperatorType.Where or UnifiedOperatorType.Filter =>
                    new[] { "Predicate_Fusion", "Short_Circuit_Evaluation" },
                UnifiedOperatorType.Select or UnifiedOperatorType.Projection =>
                    new[] { "Projection_Elimination", "Column_Pruning" },
                UnifiedOperatorType.OrderBy or UnifiedOperatorType.Sort =>
                    new[] { "Adaptive_Sorting", "Key_Extraction_Optimization" },
                UnifiedOperatorType.GroupBy =>
                    new[] { "Hash_Grouping", "Incremental_Aggregation" },
                UnifiedOperatorType.Join =>
                    new[] { "Hash_Join", "Sort_Merge_Join", "Index_Usage" },
                _ => Array.Empty<string>()
            return baseOptimizations.Concat(specificOptimizations);
    /// Simple implementation of IExpressionAnalysisResult for factory use.
    private class SimpleExpressionAnalysisResult : IExpressionAnalysisResult
    {
        public SimpleExpressionAnalysisResult(UnifiedComplexityMetrics complexityMetrics)
            ComplexityMetrics = complexityMetrics;
            AnalysisTimestamp = DateTimeOffset.UtcNow;
        public DateTimeOffset AnalysisTimestamp { get; }
        public IComplexityMetrics ComplexityMetrics { get; }
}
/// Fluent builder extensions for creating complex unified types.
public static class UnifiedTypeBuilderExtensions
    {
    /// Creates a complexity analysis pipeline for multiple operations.
    public static ComplexityAnalysisPipeline CreateComplexityPipeline(this UnifiedOperatorType[] operators)
        {
        return new ComplexityAnalysisPipeline(operators);
    /// Creates a kernel compilation pipeline for multi-stage kernels.
    public static KernelCompilationPipeline CreateKernelPipeline(this string baseName)
        return new KernelCompilationPipeline(baseName);
/// Builder for complex complexity analysis scenarios.
public class ComplexityAnalysisPipeline
    {
    private readonly List<UnifiedOperatorType> _operators;
    private long _totalOperations = 10000;
    private long _totalMemory = 65536;
    private readonly Dictionary<string, double> _operationBreakdown = [];
    public ComplexityAnalysisPipeline(IEnumerable<UnifiedOperatorType> operators)
        _operators = operators.ToList();
    public ComplexityAnalysisPipeline WithTotalOperations(long operations)
        _totalOperations = operations;
        return this;
    public ComplexityAnalysisPipeline WithTotalMemory(long memory)
        _totalMemory = memory;
    public ComplexityAnalysisPipeline AddOperationBreakdown(string operation, double complexity)
        _operationBreakdown[operation] = complexity;
    public UnifiedComplexityMetrics Build()
        return UnifiedTypeFactory.ComplexityMetrics.CreateAdvanced(
            _operators, _totalOperations, _totalMemory, _operationBreakdown);
/// Builder for complex kernel compilation scenarios.
public class KernelCompilationPipeline
    {
    private readonly string _baseName;
    private string _cpuSource = "";
    private string _gpuSource = "";
    private UnifiedComplexityMetrics? _complexity;
    private readonly List<(string name, Type type, bool isPointer)> _parameters = [];
    public KernelCompilationPipeline(string baseName)
        _baseName = baseName;
    public KernelCompilationPipeline WithCpuSource(string source)
        _cpuSource = source;
    public KernelCompilationPipeline WithGpuSource(string source)
        _gpuSource = source;
    public KernelCompilationPipeline WithComplexity(UnifiedComplexityMetrics complexity)
        _complexity = complexity;
    public KernelCompilationPipeline AddParameter<T>(string name, bool isPointer = false)
        _parameters.Add((name, typeof(T), isPointer));
    public UnifiedGeneratedKernel Build()
        if (_complexity == null)
            throw new InvalidOperationException("Complexity metrics must be provided for multi-backend kernels.");
        var kernel = UnifiedTypeFactory.GeneratedKernel.CreateMultiBackend(
            _baseName, _cpuSource, _gpuSource, _complexity);
        foreach (var (name, type, isPointer) in _parameters)
            kernel.AddParameter(new UnifiedKernelParameter(name, type, isPointer));
        return kernel;
