// <copyright file="ComplexityMetricsAdapter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Core.Analysis;

namespace DotCompute.Refactored.Adapters;

/// <summary>
/// Adapter class that provides backward compatibility for legacy ComplexityMetrics classes.
/// Converts between different ComplexityMetrics implementations found across the codebase.
/// </summary>
public static class ComplexityMetricsAdapter
{
    /// <summary>
    /// Converts a legacy PipelineComplexityMetrics to UnifiedComplexityMetrics.
    /// </summary>
    public static UnifiedComplexityMetrics FromPipelineComplexity(dynamic legacyMetrics)
    {
        return UnifiedComplexityMetrics.Builder()
            .WithComputationalComplexity(GetPropertyValue<int>(legacyMetrics, "ComputationalComplexity", "ComputeComplexity", "TotalComplexity"))
            .WithMemoryComplexity(GetPropertyValue<int>(legacyMetrics, "MemoryComplexity"))
            .WithOperationCount(GetPropertyValue<long>(legacyMetrics, "OperationCount", "OperationsCount"))
            .WithMemoryUsage(GetPropertyValue<long>(legacyMetrics, "EstimatedMemoryUsage", "MemoryUsage"))
            .WithParallelizationPotential(GetPropertyValue<double>(legacyMetrics, "ParallelizationPotential"))
            .Build();
    }

    /// <summary>
    /// Converts a legacy simple ComplexityMetrics to UnifiedComplexityMetrics.
    /// </summary>
    public static UnifiedComplexityMetrics FromSimpleComplexity(dynamic legacyMetrics)
    {
        return UnifiedComplexityMetrics.Builder()
            .WithComputationalComplexity(GetPropertyValue<int>(legacyMetrics, "ComputationalComplexity"))
            .WithMemoryComplexity(GetPropertyValue<int>(legacyMetrics, "MemoryComplexity"))
            .WithOperationCount(GetPropertyValue<long>(legacyMetrics, "OperationsCount", "OperationCount"))
            .Build();
    }

    /// <summary>
    /// Converts UnifiedComplexityMetrics back to a legacy PipelineComplexityMetrics-like object.
    /// </summary>
    public static dynamic ToPipelineComplexity(UnifiedComplexityMetrics unified)
    {
        return new
        {
            TotalComplexity = unified.OverallComplexity,
            OverallComplexity = unified.OverallComplexity,
            OperationCount = unified.OperationCount,
            EstimatedMemoryUsage = unified.MemoryUsage,
            MemoryUsage = unified.MemoryUsage,
            ParallelizationPotential = unified.ParallelizationPotential,
            GpuRecommended = unified.GpuRecommended,
            ComplexityByCategory = unified.ComplexityByCategory,
            MemoryComplexity = unified.MemoryComplexity,
            ComputeComplexity = unified.ComputeComplexity,
            ComputationalComplexity = unified.ComputationalComplexity,
            CommunicationComplexity = unified.CommunicationComplexity,
            ParallelizationComplexity = unified.ParallelizationComplexity
        };
    }

    /// <summary>
    /// Converts UnifiedComplexityMetrics back to a legacy simple ComplexityMetrics-like object.
    /// </summary>
    public static dynamic ToSimpleComplexity(UnifiedComplexityMetrics unified)
    {
        return new
        {
            ComputationalComplexity = unified.ComputationalComplexity,
            MemoryComplexity = unified.MemoryComplexity,
            ParallelizationComplexity = unified.ParallelizationComplexity,
            OverallComplexity = unified.OverallComplexity,
            OperationsCount = unified.OperationCount,
            OperationCount = unified.OperationCount
        };
    }

    /// <summary>
    /// Creates a UnifiedComplexityMetrics from any object with complexity properties.
    /// Uses reflection to extract available properties.
    /// </summary>
    public static UnifiedComplexityMetrics FromAny(object legacyMetrics)
    {
        if (legacyMetrics == null)
        {

            throw new ArgumentNullException(nameof(legacyMetrics));
        }


        var builder = UnifiedComplexityMetrics.Builder();
        var type = legacyMetrics.GetType();

        // Try to map common properties
        var propertyMappings = new Dictionary<string, Action<object>>
        {
            ["ComputationalComplexity"] = value => builder.WithComputationalComplexity(Convert.ToInt32(value)),
            ["ComputeComplexity"] = value => builder.WithComputationalComplexity(Convert.ToInt32(value)),
            ["TotalComplexity"] = value => builder.WithComputationalComplexity(Convert.ToInt32(value)),
            ["MemoryComplexity"] = value => builder.WithMemoryComplexity(Convert.ToInt32(value)),
            ["OperationCount"] = value => builder.WithOperationCount(Convert.ToInt64(value)),
            ["OperationsCount"] = value => builder.WithOperationCount(Convert.ToInt64(value)),
            ["MemoryUsage"] = value => builder.WithMemoryUsage(Convert.ToInt64(value)),
            ["EstimatedMemoryUsage"] = value => builder.WithMemoryUsage(Convert.ToInt64(value)),
            ["ParallelizationPotential"] = value => builder.WithParallelizationPotential(Convert.ToDouble(value)),
            ["CacheEfficiency"] = value => builder.WithCacheEfficiency(Convert.ToDouble(value))
        };

        foreach (var property in type.GetProperties())
        {
            if (propertyMappings.TryGetValue(property.Name, out var mapper))
            {
                var value = property.GetValue(legacyMetrics);
                if (value != null)
                {
                    try
                    {
                        mapper(value);
                    }
                    catch (Exception)
                    {
                        // Ignore conversion errors for optional properties
                    }
                }
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Helper method to get property values with fallbacks.
    /// </summary>
    private static T GetPropertyValue<T>(dynamic obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var value = GetPropertyValue(obj, propertyName);
                if (value != null)
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception)
            {
                // Continue to next property name
            }
        }

        return default(T)!;
    }

    /// <summary>
    /// Helper method to get a property value using reflection.
    /// </summary>
    private static object? GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null)
        {
            return null;
        }


        var type = obj.GetType();
        var property = type.GetProperty(propertyName);
        return property?.GetValue(obj);
    }
}

/// <summary>
/// Extension methods to provide seamless conversion between complexity metrics types.
/// </summary>
public static class ComplexityMetricsExtensions
{
    /// <summary>
    /// Converts any complexity metrics object to UnifiedComplexityMetrics.
    /// </summary>
    public static UnifiedComplexityMetrics ToUnified(this object complexityMetrics)
    {
        return complexityMetrics switch
        {
            UnifiedComplexityMetrics unified => unified,
            null => throw new ArgumentNullException(nameof(complexityMetrics)),
            _ => ComplexityMetricsAdapter.FromAny(complexityMetrics)
        };
    }

    /// <summary>
    /// Converts UnifiedComplexityMetrics to a legacy format.
    /// </summary>
    public static dynamic ToLegacyPipeline(this UnifiedComplexityMetrics unified)
    {
        return ComplexityMetricsAdapter.ToPipelineComplexity(unified);
    }

    /// <summary>
    /// Converts UnifiedComplexityMetrics to a simple legacy format.
    /// </summary>
    public static dynamic ToLegacySimple(this UnifiedComplexityMetrics unified)
    {
        return ComplexityMetricsAdapter.ToSimpleComplexity(unified);
    }

    /// <summary>
    /// Merges two complexity metrics objects, with the second taking precedence for conflicts.
    /// </summary>
    public static UnifiedComplexityMetrics Merge(this UnifiedComplexityMetrics first, UnifiedComplexityMetrics second)
    {
        return UnifiedComplexityMetrics.Builder()
            .WithComputationalComplexity(second.ComputationalComplexity != 0 ? second.ComputationalComplexity : first.ComputationalComplexity)
            .WithMemoryComplexity(second.MemoryComplexity != 0 ? second.MemoryComplexity : first.MemoryComplexity)
            .WithOperationCount(second.OperationCount != 0 ? second.OperationCount : first.OperationCount)
            .WithMemoryUsage(second.MemoryUsage != 0 ? second.MemoryUsage : first.MemoryUsage)
            .WithParallelizationPotential(second.ParallelizationPotential != 1.0 ? second.ParallelizationPotential : first.ParallelizationPotential)
            .WithCacheEfficiency(second.CacheEfficiency != 0.8 ? second.CacheEfficiency : first.CacheEfficiency)
            .Build();
    }

    /// <summary>
    /// Creates a summary of the complexity metrics for reporting.
    /// </summary>
    public static string GetSummary(this UnifiedComplexityMetrics metrics)
    {
        return $"Complexity: {metrics.ComplexityScore:F2}/10, " +
               $"Operations: {metrics.OperationCount:N0}, " +
               $"Memory: {FormatBytes(metrics.MemoryUsage)}, " +
               $"Parallel Potential: {metrics.ParallelizationPotential:P0}, " +
               $"Cache Efficiency: {metrics.CacheEfficiency:P0}";
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F1} GB",
            >= MB => $"{bytes / (double)MB:F1} MB",
            >= KB => $"{bytes / (double)KB:F1} KB",
            _ => $"{bytes} bytes"
        };
    }
}