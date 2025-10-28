// <copyright file="UnifiedComplexityMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Analysis;

namespace DotCompute.Core.Analysis;

/// <summary>
/// Unified complexity metrics implementation that consolidates all complexity metric properties
/// from across the codebase into a single, comprehensive type.
/// </summary>
public record UnifiedComplexityMetrics : IAdvancedComplexityMetrics
{
    /// <summary>
    /// Gets or sets the computational complexity.
    /// </summary>
    /// <value>The computational complexity.</value>
    // Core properties from IComplexityMetrics
    public int ComputationalComplexity { get; init; }
    /// <summary>
    /// Gets or sets the memory complexity.
    /// </summary>
    /// <value>The memory complexity.</value>
    public int MemoryComplexity { get; init; }
    /// <summary>
    /// Gets or sets the parallelization complexity.
    /// </summary>
    /// <value>The parallelization complexity.</value>
    public int ParallelizationComplexity { get; init; }
    /// <summary>
    /// Gets or sets the overall complexity.
    /// </summary>
    /// <value>The overall complexity.</value>
    public int OverallComplexity { get; init; }
    /// <summary>
    /// Gets or sets the operation count.
    /// </summary>
    /// <value>The operation count.</value>
    public long OperationCount { get; init; }
    /// <summary>
    /// Gets or sets the memory usage.
    /// </summary>
    /// <value>The memory usage.</value>
    public long MemoryUsage { get; init; }
    /// <summary>
    /// Gets or sets the parallelization potential.
    /// </summary>
    /// <value>The parallelization potential.</value>
    public double ParallelizationPotential { get; init; } = 1.0;
    /// <summary>
    /// Gets or sets the cache efficiency.
    /// </summary>
    /// <value>The cache efficiency.</value>
    public double CacheEfficiency { get; init; } = 0.8;
    /// <summary>
    /// Gets or sets the complexity factor.
    /// </summary>
    /// <value>The complexity factor.</value>
    public double ComplexityFactor { get; init; } = 1.0;
    /// <summary>
    /// Gets or sets a value indicating whether data dependent.
    /// </summary>
    /// <value>The is data dependent.</value>
    public bool IsDataDependent { get; init; }
    /// <summary>
    /// Gets or sets the computational complexity class.
    /// </summary>
    /// <value>The computational complexity class.</value>

    // Advanced properties from IAdvancedComplexityMetrics
    public ComplexityClass ComputationalComplexityClass { get; init; } = ComplexityClass.Constant;
    /// <summary>
    /// Gets or sets the space complexity.
    /// </summary>
    /// <value>The space complexity.</value>
    public ComplexityClass SpaceComplexity { get; init; } = ComplexityClass.Constant;
    /// <summary>
    /// Gets or sets the memory accesses.
    /// </summary>
    /// <value>The memory accesses.</value>
    public long MemoryAccesses { get; init; }
    /// <summary>
    /// Gets or sets the operation complexity.
    /// </summary>
    /// <value>The operation complexity.</value>
    public Dictionary<string, double> OperationComplexity { get; init; } = [];
    /// <summary>
    /// Gets or sets the memory access patterns.
    /// </summary>
    /// <value>The memory access patterns.</value>
    public IReadOnlyList<MemoryAccessComplexity> MemoryAccessPatterns { get; init; } = [];
    /// <summary>
    /// Gets or sets the worst case scenario.
    /// </summary>
    /// <value>The worst case scenario.</value>
    public string WorstCaseScenario { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the estimated memory usage.
    /// </summary>
    /// <value>The estimated memory usage.</value>

    // Additional unified properties from all sources
    public long EstimatedMemoryUsage { get; init; }
    /// <summary>
    /// Gets or sets the operations count.
    /// </summary>
    /// <value>The operations count.</value>
    public long OperationsCount { get; init; } // Alias for OperationCount
    /// <summary>
    /// Gets or sets the total complexity.
    /// </summary>
    /// <value>The total complexity.</value>
    public int TotalComplexity { get; init; } // Alias for OverallComplexity
    /// <summary>
    /// Gets or sets the locality factor.
    /// </summary>
    /// <value>The locality factor.</value>
    public double LocalityFactor { get; init; } = 0.5;
    /// <summary>
    /// Gets or sets the gpu recommended.
    /// </summary>
    /// <value>The gpu recommended.</value>
    public bool GpuRecommended { get; init; }
    /// <summary>
    /// Gets or sets the complexity by category.
    /// </summary>
    /// <value>The complexity by category.</value>
    public Dictionary<string, int> ComplexityByCategory { get; init; } = [];
    /// <summary>
    /// Gets or sets the communication complexity.
    /// </summary>
    /// <value>The communication complexity.</value>
    public int CommunicationComplexity { get; init; }
    /// <summary>
    /// Gets or sets the memory bound.
    /// </summary>
    /// <value>The memory bound.</value>

    // Computed properties
    public bool MemoryBound => MemoryAccesses > OperationCount * 2 || EstimatedMemoryUsage > OperationCount * 8;
    /// <summary>
    /// Gets or sets a value indicating whether benefit from shared memory.
    /// </summary>
    /// <value>The can benefit from shared memory.</value>
    public bool CanBenefitFromSharedMemory => MemoryBound && ParallelizationPotential > 0.5;
    /// <summary>
    /// Gets or sets the compute complexity.
    /// </summary>
    /// <value>The compute complexity.</value>
    public double ComputeComplexity => ComplexityFactor * (1.0 + OperationCount / 1000000.0);
    /// <summary>
    /// Gets or sets the complexity score.
    /// </summary>
    /// <value>The complexity score.</value>

    public double ComplexityScore => Math.Min(10.0, ComputationalComplexityClass switch
    {
        ComplexityClass.Constant => 1.0,
        ComplexityClass.Logarithmic => 2.0,
        ComplexityClass.Linear => 3.0,
        ComplexityClass.Linearithmic => 4.0,
        ComplexityClass.Quadratic => 6.0,
        ComplexityClass.Cubic => 8.0,
        ComplexityClass.Exponential => 10.0,
        ComplexityClass.Factorial => 10.0,
        _ => 3.0
    } * ComplexityFactor);

    // Interface implementations (read-only access)
    IReadOnlyDictionary<string, double> IAdvancedComplexityMetrics.OperationComplexity => OperationComplexity;
    IReadOnlyList<MemoryAccessComplexity> IAdvancedComplexityMetrics.MemoryAccessPatterns => MemoryAccessPatterns;

    /// <summary>
    /// Creates a builder for constructing UnifiedComplexityMetrics instances.
    /// </summary>
    public static UnifiedComplexityMetricsBuilder Builder() => new();
}

/// <summary>
/// Builder for creating UnifiedComplexityMetrics instances with fluent API.
/// </summary>
public class UnifiedComplexityMetricsBuilder
{
    private readonly Dictionary<string, object> _properties = [];
    /// <summary>
    /// Gets with computational complexity.
    /// </summary>
    /// <param name="complexity">The complexity.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder WithComputationalComplexity(int complexity)
    {
        _properties[nameof(UnifiedComplexityMetrics.ComputationalComplexity)] = complexity;
        return this;
    }
    /// <summary>
    /// Gets with computational complexity class.
    /// </summary>
    /// <param name="complexityClass">The complexity class.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder WithComputationalComplexityClass(ComplexityClass complexityClass)
    {
        _properties[nameof(UnifiedComplexityMetrics.ComputationalComplexityClass)] = complexityClass;
        return this;
    }
    /// <summary>
    /// Gets with memory complexity.
    /// </summary>
    /// <param name="complexity">The complexity.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder WithMemoryComplexity(int complexity)
    {
        _properties[nameof(UnifiedComplexityMetrics.MemoryComplexity)] = complexity;
        return this;
    }
    /// <summary>
    /// Gets with operation count.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder WithOperationCount(long count)
    {
        _properties[nameof(UnifiedComplexityMetrics.OperationCount)] = count;
        _properties[nameof(UnifiedComplexityMetrics.OperationsCount)] = count;
        return this;
    }
    /// <summary>
    /// Gets with memory usage.
    /// </summary>
    /// <param name="usage">The usage.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder WithMemoryUsage(long usage)
    {
        _properties[nameof(UnifiedComplexityMetrics.MemoryUsage)] = usage;
        _properties[nameof(UnifiedComplexityMetrics.EstimatedMemoryUsage)] = usage;
        return this;
    }
    /// <summary>
    /// Gets with parallelization potential.
    /// </summary>
    /// <param name="potential">The potential.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder WithParallelizationPotential(double potential)
    {
        _properties[nameof(UnifiedComplexityMetrics.ParallelizationPotential)] = potential;
        return this;
    }
    /// <summary>
    /// Gets with cache efficiency.
    /// </summary>
    /// <param name="efficiency">The efficiency.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder WithCacheEfficiency(double efficiency)
    {
        _properties[nameof(UnifiedComplexityMetrics.CacheEfficiency)] = efficiency;
        return this;
    }
    /// <summary>
    /// Gets add operation complexity.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="complexity">The complexity.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder AddOperationComplexity(string operation, double complexity)
    {
        if (!_properties.TryGetValue(nameof(UnifiedComplexityMetrics.OperationComplexity), out var existing))
        {
            existing = new Dictionary<string, double>();
            _properties[nameof(UnifiedComplexityMetrics.OperationComplexity)] = existing;
        }
        ((Dictionary<string, double>)existing)[operation] = complexity;
        return this;
    }
    /// <summary>
    /// Gets add memory access pattern.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetricsBuilder AddMemoryAccessPattern(MemoryAccessComplexity pattern)
    {
        if (!_properties.TryGetValue(nameof(UnifiedComplexityMetrics.MemoryAccessPatterns), out var existing))
        {
            existing = new List<MemoryAccessComplexity>();
            _properties[nameof(UnifiedComplexityMetrics.MemoryAccessPatterns)] = existing;
        }
        ((List<MemoryAccessComplexity>)existing).Add(pattern);
        return this;
    }
    /// <summary>
    /// Gets build.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public UnifiedComplexityMetrics Build()
    {
        // Calculate overall complexity if not explicitly set
        if (!_properties.ContainsKey(nameof(UnifiedComplexityMetrics.OverallComplexity)))
        {
            var computational = _properties.TryGetValue(nameof(UnifiedComplexityMetrics.ComputationalComplexity), out var comp) ? (int)comp : 0;
            var memory = _properties.TryGetValue(nameof(UnifiedComplexityMetrics.MemoryComplexity), out var mem) ? (int)mem : 0;
            var parallel = _properties.TryGetValue(nameof(UnifiedComplexityMetrics.ParallelizationComplexity), out var par) ? (int)par : 0;
            _properties[nameof(UnifiedComplexityMetrics.OverallComplexity)] = Math.Max(computational, Math.Max(memory, parallel));
            _properties[nameof(UnifiedComplexityMetrics.TotalComplexity)] = _properties[nameof(UnifiedComplexityMetrics.OverallComplexity)];
        }

        // Use reflection to create the record with all properties
        return new UnifiedComplexityMetrics
        {
            ComputationalComplexity = GetProperty<int>(nameof(UnifiedComplexityMetrics.ComputationalComplexity)),
            MemoryComplexity = GetProperty<int>(nameof(UnifiedComplexityMetrics.MemoryComplexity)),
            ParallelizationComplexity = GetProperty<int>(nameof(UnifiedComplexityMetrics.ParallelizationComplexity)),
            OverallComplexity = GetProperty<int>(nameof(UnifiedComplexityMetrics.OverallComplexity)),
            OperationCount = GetProperty<long>(nameof(UnifiedComplexityMetrics.OperationCount)),
            MemoryUsage = GetProperty<long>(nameof(UnifiedComplexityMetrics.MemoryUsage)),
            ParallelizationPotential = GetProperty(nameof(UnifiedComplexityMetrics.ParallelizationPotential), 1.0),
            CacheEfficiency = GetProperty(nameof(UnifiedComplexityMetrics.CacheEfficiency), 0.8),
            ComplexityFactor = GetProperty(nameof(UnifiedComplexityMetrics.ComplexityFactor), 1.0),
            IsDataDependent = GetProperty<bool>(nameof(UnifiedComplexityMetrics.IsDataDependent)),
            ComputationalComplexityClass = GetProperty(nameof(UnifiedComplexityMetrics.ComputationalComplexityClass), ComplexityClass.Constant),
            SpaceComplexity = GetProperty(nameof(UnifiedComplexityMetrics.SpaceComplexity), ComplexityClass.Constant),
            MemoryAccesses = GetProperty<long>(nameof(UnifiedComplexityMetrics.MemoryAccesses)),
            OperationComplexity = GetProperty<Dictionary<string, double>>(nameof(UnifiedComplexityMetrics.OperationComplexity)) ?? [],
            MemoryAccessPatterns = GetProperty<List<MemoryAccessComplexity>>(nameof(UnifiedComplexityMetrics.MemoryAccessPatterns)) ?? [],
            WorstCaseScenario = GetProperty(nameof(UnifiedComplexityMetrics.WorstCaseScenario), string.Empty),
            EstimatedMemoryUsage = GetProperty<long>(nameof(UnifiedComplexityMetrics.EstimatedMemoryUsage)),
            OperationsCount = GetProperty<long>(nameof(UnifiedComplexityMetrics.OperationsCount)),
            TotalComplexity = GetProperty<int>(nameof(UnifiedComplexityMetrics.TotalComplexity)),
            LocalityFactor = GetProperty(nameof(UnifiedComplexityMetrics.LocalityFactor), 0.5),
            GpuRecommended = GetProperty<bool>(nameof(UnifiedComplexityMetrics.GpuRecommended)),
            ComplexityByCategory = GetProperty<Dictionary<string, int>>(nameof(UnifiedComplexityMetrics.ComplexityByCategory)) ?? [],
            CommunicationComplexity = GetProperty<int>(nameof(UnifiedComplexityMetrics.CommunicationComplexity))
        };
    }

    private T GetProperty<T>(string name, T defaultValue = default!)
    {
        if (_properties.TryGetValue(name, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }
}