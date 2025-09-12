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

    /// <summary>
    /// Gets whether the expression is compatible with GPU execution.
    /// </summary>
    public bool IsGpuCompatible { get; init; }

    /// <summary>
    /// Gets whether the expression is compatible with CPU execution.
    /// </summary>
    public bool IsCpuCompatible { get; init; } = true;

    /// <summary>
    /// Gets the complexity score of the expression.
    /// </summary>
    public int ComplexityScore => ComplexityMetrics.OverallComplexity;

    /// <summary>
    /// Gets the estimated memory requirement.
    /// </summary>
    public long MemoryRequirement => ComplexityMetrics.MemoryUsage;

    /// <summary>
    /// Gets the parallelization potential score.
    /// </summary>
    public int ParallelizationPotential => (int)ComplexityMetrics.ParallelizationPotential;

    /// <summary>
    /// Gets the estimated memory usage.
    /// </summary>
    public long EstimatedMemoryUsage => ComplexityMetrics.MemoryUsage;

    /// <summary>
    /// Gets whether the expression is parallelizable.
    /// </summary>
    public bool IsParallelizable => ComplexityMetrics.ParallelizationPotential > 0.5;

    /// <summary>
    /// Gets whether the expression is suitable for GPU execution.
    /// </summary>
    public bool IsGpuSuitable => IsGpuCompatible && ComplexityMetrics.ParallelizationPotential > 0.7;

    /// <summary>
    /// Gets optimization recommendations.
    /// </summary>
    public IReadOnlyList<string> Recommendations => OptimizationHints
        .Select(h => h.Description ?? h.ToString() ?? string.Empty)
        .ToList();

    /// <summary>
    /// Gets identified bottlenecks.
    /// </summary>
    public IReadOnlyList<string> Bottlenecks => ParallelizationInfo.Bottlenecks ?? new List<string>();

    // Legacy compatibility properties for backward compatibility with existing visitor pattern
    private List<string> _supportedOperationsInternal = new();
    private List<Type> _parameterTypesInternal = new();

    /// <summary>
    /// Gets the supported operations (backward compatibility).
    /// </summary>
    public IReadOnlyList<string> SupportedOperations => _supportedOperationsInternal.AsReadOnly();

    /// <summary>
    /// Gets the parameter types (backward compatibility).
    /// </summary>
    public IReadOnlyList<Type> ParameterTypes => _parameterTypesInternal.AsReadOnly();

    /// <summary>
    /// Gets the internal supported operations list for visitor pattern.
    /// </summary>
    internal List<string> SupportedOperationsInternal => _supportedOperationsInternal;

    /// <summary>
    /// Gets the internal parameter types list for visitor pattern.
    /// </summary>
    internal List<Type> ParameterTypesInternal => _parameterTypesInternal;

    /// <summary>
    /// Gets or sets the output type of the expression (backward compatibility).
    /// </summary>
    public Type OutputType { get; set; } = typeof(object);

    /// <summary>
    /// Gets the analysis timestamp.
    /// </summary>
    public DateTimeOffset AnalysisTimestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets operator information for the expression (compatibility property).
    /// </summary>
    public IReadOnlyList<PipelineOperatorInfo> OperatorInfo => OperatorChain;

    /// <summary>
    /// Gets metadata about the analysis.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}

// TypeUsageInfo and DependencyInfo are already defined in ITypeAnalyzer.cs
// DependencyType enum might also be defined elsewhere


// ParallelizationInfo is defined in PipelineAnalysisTypes.cs to avoid duplication