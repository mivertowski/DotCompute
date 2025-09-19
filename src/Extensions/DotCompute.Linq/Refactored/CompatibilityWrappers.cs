// <copyright file="CompatibilityWrappers.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using DotCompute.Core.Analysis;
using DotCompute.Core.Kernels;
using DotCompute.Refactored.Adapters;

namespace DotCompute.Linq.Refactored;

/// <summary>
/// Compatibility extensions for seamless migration to unified types.
/// </summary>
public static class CompatibilityExtensions
{
    /// <summary>
    /// Implicit conversion from any ComplexityMetrics to UnifiedComplexityMetrics.
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
    /// Implicit conversion from any GeneratedKernel to UnifiedGeneratedKernel.
    /// </summary>
    public static UnifiedGeneratedKernel ToUnifiedKernel(this object generatedKernel)
    {
        return generatedKernel switch
        {
            UnifiedGeneratedKernel unified => unified,
            null => throw new ArgumentNullException(nameof(generatedKernel)),
            _ => GeneratedKernelAdapter.FromAny(generatedKernel)
        };
    }

    /// <summary>
    /// Conversion from any OperatorType enum to UnifiedOperatorType.
    /// </summary>
    public static UnifiedOperatorType ToUnifiedOperatorType(this Enum operatorType)
    {
        return OperatorTypeAdapter.FromEnum(operatorType);
    }

    /// <summary>
    /// Conversion from string to UnifiedOperatorType.
    /// </summary>
    public static UnifiedOperatorType ToUnifiedOperatorType(this string operatorTypeString)
    {
        return OperatorTypeAdapter.FromString(operatorTypeString);
    }
}

/// <summary>
/// Legacy type aliases for backward compatibility. These classes provide wrapper functionality
/// during the transition to unified types. New code should use the unified types directly.
/// </summary>
public static class LegacyTypeAliases
{
    /// <summary>
    /// Creates a ComplexityMetrics wrapper for backward compatibility.
    /// </summary>
    [Obsolete("Use DotCompute.Core.Analysis.UnifiedComplexityMetrics directly.", false)]
    public static UnifiedComplexityMetrics CreateComplexityMetrics(
        double computationalComplexity = 1.0,
        long operationCount = 1000,
        long memoryUsage = 8192)
    {
        return UnifiedComplexityMetrics.Builder()
            .WithComputationalComplexity((int)computationalComplexity)
            .WithOperationCount(operationCount)
            .WithMemoryUsage(memoryUsage)
            .Build();
    }

    /// <summary>
    /// Creates a GeneratedKernel wrapper for backward compatibility.
    /// </summary>
    [Obsolete("Use DotCompute.Core.Kernels.UnifiedGeneratedKernel directly.", false)]
    public static UnifiedGeneratedKernel CreateGeneratedKernel(
        string name, 
        string sourceCode, 
        string language = "C", 
        string targetBackend = "CPU", 
        string entryPoint = "main")
    {
        return new UnifiedGeneratedKernel(name, sourceCode, language, targetBackend, entryPoint);
    }
}