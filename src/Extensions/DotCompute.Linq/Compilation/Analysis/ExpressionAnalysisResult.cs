// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Linq.Pipelines.Analysis;
using PipelineOperatorInfo = DotCompute.Linq.Pipelines.Analysis.OperatorInfo;
using PipelineComplexityMetrics = DotCompute.Linq.Pipelines.Analysis.ComplexityMetrics;
// TypeUsageInfo and DependencyInfo are defined in ITypeAnalyzer.cs within the same namespace
// Use the MemoryAccessPattern from ITypeAnalyzer to maintain consistency
namespace DotCompute.Linq.Compilation.Analysis;
/// <summary>
/// Represents the result of analyzing a LINQ expression for compilation optimization.
/// </summary>
public sealed record ExpressionAnalysisResult(
    string OperationSignature,
    IReadOnlyList<PipelineOperatorInfo> OperatorChain,
    IReadOnlyDictionary<Type, TypeUsageInfo> TypeUsage,
    IReadOnlyList<DependencyInfo> Dependencies,
    PipelineComplexityMetrics ComplexityMetrics,
    ParallelizationInfo ParallelizationInfo,
    GlobalMemoryAccessPattern MemoryAccessPattern,
    IReadOnlyList<OptimizationHint> OptimizationHints
)
{
    /// <summary>
    /// Gets whether the expression can be compiled.
    /// </summary>
    public bool IsCompilable { get; init; } = true;
    /// Gets whether the expression is compatible with GPU execution.
    public bool IsGpuCompatible { get; init; }
    /// Gets whether the expression is compatible with CPU execution.
    public bool IsCpuCompatible { get; init; } = true;
    /// Gets the complexity score of the expression.
    public int ComplexityScore => ComplexityMetrics.OverallComplexity;
    /// Gets the estimated memory requirement.
    public long MemoryRequirement => ComplexityMetrics.MemoryUsage;
    /// Gets the parallelization potential score.
    public int ParallelizationPotential => (int)ComplexityMetrics.ParallelizationPotential;
    /// Gets the estimated memory usage.
    public long EstimatedMemoryUsage => ComplexityMetrics.MemoryUsage;
    /// Gets whether the expression is parallelizable.
    public bool IsParallelizable => ComplexityMetrics.ParallelizationPotential > 0.5;
    /// Gets whether the expression is suitable for GPU execution.
    public bool IsGpuSuitable => IsGpuCompatible && ComplexityMetrics.ParallelizationPotential > 0.7;
    /// Gets optimization recommendations.
    public IReadOnlyList<string> Recommendations => OptimizationHints
        .Select(h => h.Description ?? h.ToString() ?? string.Empty)
        .ToList();
    /// Gets identified bottlenecks.
    public IReadOnlyList<string> Bottlenecks => ParallelizationInfo.Bottlenecks ?? [];
    // Legacy compatibility properties for backward compatibility with existing visitor pattern
    private List<string> _supportedOperationsInternal = [];
    private List<Type> _parameterTypesInternal = [];
    /// Gets the supported operations (backward compatibility).
    public IReadOnlyList<string> SupportedOperations => _supportedOperationsInternal.AsReadOnly();
    /// Gets the parameter types (backward compatibility).
    public IReadOnlyList<Type> ParameterTypes => _parameterTypesInternal.AsReadOnly();
    /// Gets the internal supported operations list for visitor pattern.
    internal List<string> SupportedOperationsInternal => _supportedOperationsInternal;
    /// Gets the internal parameter types list for visitor pattern.
    internal List<Type> ParameterTypesInternal => _parameterTypesInternal;
    /// Gets or sets the output type of the expression (backward compatibility).
    public Type OutputType { get; set; } = typeof(object);
    /// Gets the analysis timestamp.
    public DateTimeOffset AnalysisTimestamp { get; init; } = DateTimeOffset.UtcNow;
    /// Gets operator information for the expression (compatibility property).
    public IReadOnlyList<PipelineOperatorInfo> OperatorInfo => OperatorChain;
    /// Gets metadata about the analysis.
    public Dictionary<string, object> Metadata { get; init; } = [];
}
// TypeUsageInfo and DependencyInfo are already defined in ITypeAnalyzer.cs
// DependencyType enum might also be defined elsewhere
// ParallelizationInfo is defined in PipelineAnalysisTypes.cs to avoid duplication
