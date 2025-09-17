// <copyright file="UnifiedComplexityMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Abstractions.Analysis;

namespace DotCompute.Core.Analysis;

/// <summary>
/// Unified complexity metrics implementation that consolidates all complexity metric properties
/// from across the codebase into a single, comprehensive type.
/// </summary>
public record UnifiedComplexityMetrics : IAdvancedComplexityMetrics
{
    // Core properties from IComplexityMetrics
    public int ComputationalComplexity { get; init; }
    public int MemoryComplexity { get; init; }
    public int ParallelizationComplexity { get; init; }
    public int OverallComplexity { get; init; }
    public long OperationCount { get; init; }
    public long MemoryUsage { get; init; }
    public double ParallelizationPotential { get; init; } = 1.0;
    public double CacheEfficiency { get; init; } = 0.8;
    public double ComplexityFactor { get; init; } = 1.0;
    public bool IsDataDependent { get; init; }

    // Advanced properties from IAdvancedComplexityMetrics
    public ComplexityClass ComputationalComplexityClass { get; init; } = ComplexityClass.Constant;
    public ComplexityClass SpaceComplexity { get; init; } = ComplexityClass.Constant;
    public long MemoryAccesses { get; init; }
    public Dictionary<string, double> OperationComplexity { get; init; } = new();
    public List<MemoryAccessComplexity> MemoryAccessPatterns { get; init; } = new();
    public string WorstCaseScenario { get; init; } = string.Empty;

    // Additional unified properties from all sources
    public long EstimatedMemoryUsage { get; init; }
    public long OperationsCount { get; init; } // Alias for OperationCount
    public int TotalComplexity { get; init; } // Alias for OverallComplexity
    public double LocalityFactor { get; init; } = 0.5;
    public bool GpuRecommended { get; init; }
    public Dictionary<string, int> ComplexityByCategory { get; init; } = new();
    public int CommunicationComplexity { get; init; }

    // Computed properties
    public bool MemoryBound => MemoryAccesses > OperationCount * 2 || EstimatedMemoryUsage > OperationCount * 8;
    public bool CanBenefitFromSharedMemory => MemoryBound && ParallelizationPotential > 0.5;
    public double ComputeComplexity => ComplexityFactor * (1.0 + OperationCount / 1000000.0);

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
    private readonly Dictionary<string, object> _properties = new();

    public UnifiedComplexityMetricsBuilder WithComputationalComplexity(int complexity)
    {
        _properties[nameof(UnifiedComplexityMetrics.ComputationalComplexity)] = complexity;
        return this;
    }

    public UnifiedComplexityMetricsBuilder WithComputationalComplexityClass(ComplexityClass complexityClass)
    {
        _properties[nameof(UnifiedComplexityMetrics.ComputationalComplexityClass)] = complexityClass;
        return this;
    }

    public UnifiedComplexityMetricsBuilder WithMemoryComplexity(int complexity)
    {
        _properties[nameof(UnifiedComplexityMetrics.MemoryComplexity)] = complexity;
        return this;
    }

    public UnifiedComplexityMetricsBuilder WithOperationCount(long count)
    {
        _properties[nameof(UnifiedComplexityMetrics.OperationCount)] = count;
        _properties[nameof(UnifiedComplexityMetrics.OperationsCount)] = count;
        return this;
    }

    public UnifiedComplexityMetricsBuilder WithMemoryUsage(long usage)
    {
        _properties[nameof(UnifiedComplexityMetrics.MemoryUsage)] = usage;
        _properties[nameof(UnifiedComplexityMetrics.EstimatedMemoryUsage)] = usage;
        return this;
    }

    public UnifiedComplexityMetricsBuilder WithParallelizationPotential(double potential)
    {
        _properties[nameof(UnifiedComplexityMetrics.ParallelizationPotential)] = potential;
        return this;
    }

    public UnifiedComplexityMetricsBuilder WithCacheEfficiency(double efficiency)
    {
        _properties[nameof(UnifiedComplexityMetrics.CacheEfficiency)] = efficiency;
        return this;
    }

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
            ParallelizationPotential = GetProperty<double>(nameof(UnifiedComplexityMetrics.ParallelizationPotential), 1.0),
            CacheEfficiency = GetProperty<double>(nameof(UnifiedComplexityMetrics.CacheEfficiency), 0.8),
            ComplexityFactor = GetProperty<double>(nameof(UnifiedComplexityMetrics.ComplexityFactor), 1.0),
            IsDataDependent = GetProperty<bool>(nameof(UnifiedComplexityMetrics.IsDataDependent)),
            ComputationalComplexityClass = GetProperty<ComplexityClass>(nameof(UnifiedComplexityMetrics.ComputationalComplexityClass), ComplexityClass.Constant),
            SpaceComplexity = GetProperty<ComplexityClass>(nameof(UnifiedComplexityMetrics.SpaceComplexity), ComplexityClass.Constant),
            MemoryAccesses = GetProperty<long>(nameof(UnifiedComplexityMetrics.MemoryAccesses)),
            OperationComplexity = GetProperty<Dictionary<string, double>>(nameof(UnifiedComplexityMetrics.OperationComplexity)) ?? new(),
            MemoryAccessPatterns = GetProperty<List<MemoryAccessComplexity>>(nameof(UnifiedComplexityMetrics.MemoryAccessPatterns)) ?? new(),
            WorstCaseScenario = GetProperty<string>(nameof(UnifiedComplexityMetrics.WorstCaseScenario), string.Empty),
            EstimatedMemoryUsage = GetProperty<long>(nameof(UnifiedComplexityMetrics.EstimatedMemoryUsage)),
            OperationsCount = GetProperty<long>(nameof(UnifiedComplexityMetrics.OperationsCount)),
            TotalComplexity = GetProperty<int>(nameof(UnifiedComplexityMetrics.TotalComplexity)),
            LocalityFactor = GetProperty<double>(nameof(UnifiedComplexityMetrics.LocalityFactor), 0.5),
            GpuRecommended = GetProperty<bool>(nameof(UnifiedComplexityMetrics.GpuRecommended)),
            ComplexityByCategory = GetProperty<Dictionary<string, int>>(nameof(UnifiedComplexityMetrics.ComplexityByCategory)) ?? new(),
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