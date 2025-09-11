// <copyright file="NumericTypeAnalyzer.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace DotCompute.Linq.Compilation.Analysis;

/// <summary>
/// Type analyzer for numeric types (int, float, double, etc.).
/// </summary>
/// <typeparam name="T">The numeric type to analyze.</typeparam>
public class NumericTypeAnalyzer<T> : ITypeAnalyzer
    where T : struct, IComparable<T>, IEquatable<T>
{
    /// <inheritdoc/>
    public Type TargetType => typeof(T);

    /// <inheritdoc/>
    public TypeUsageInfo AnalyzeUsage(Expression expression, AnalysisContext context)
    {
        var usageCount = CountTypeUsage(expression);
        var memoryPattern = DetermineMemoryPattern(expression);
        var requiresSpecialization = DetermineSpecializationNeed(expression);

        return new TypeUsageInfo
        {
            Type = TargetType,
            UsageFrequency = usageCount,
            RequiresSpecialization = requiresSpecialization,
            MemoryPattern = memoryPattern,
            SupportsSimd = SupportsVectorization(),
            EstimatedSize = Marshal.SizeOf<T>(),
            Hints = GetOptimizationHints(expression).ToList()
        };
    }

    /// <inheritdoc/>
    public bool SupportsVectorization()
    {
        return TargetType == typeof(int) ||
               TargetType == typeof(float) ||
               TargetType == typeof(double) ||
               TargetType == typeof(long) ||
               TargetType == typeof(short) ||
               TargetType == typeof(byte);
    }

    /// <inheritdoc/>
    public int GetOptimalAlignment()
    {
        var size = Marshal.SizeOf<T>();
        
        // Align to next power of 2, but at least 4 bytes
        var alignment = Math.Max(4, 1);
        while (alignment < size)
        {
            alignment *= 2;
        }
        
        // Cap at 32 bytes for cache line efficiency
        return Math.Min(alignment, 32);
    }

    /// <inheritdoc/>
    public double EstimateOperationComplexity(ExpressionType operation)
    {
        var baseComplexity = GetBaseComplexity();
        
        return operation switch
        {
            ExpressionType.Add or ExpressionType.Subtract => baseComplexity,
            ExpressionType.Multiply => baseComplexity * 2,
            ExpressionType.Divide => baseComplexity * 4,
            ExpressionType.Modulo => baseComplexity * 4,
            ExpressionType.Power => baseComplexity * 8,
            ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr => baseComplexity * 0.5,
            ExpressionType.LeftShift or ExpressionType.RightShift => baseComplexity * 0.5,
            ExpressionType.Equal or ExpressionType.NotEqual => baseComplexity,
            ExpressionType.LessThan or ExpressionType.LessThanOrEqual => baseComplexity,
            ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual => baseComplexity,
            _ => baseComplexity
        };
    }

    /// <inheritdoc/>
    public IEnumerable<OptimizationHint> GetOptimizationHints(Expression expression)
    {
        var hints = new List<OptimizationHint>();

        if (SupportsVectorization())
        {
            hints.Add(new OptimizationHint(
                OptimizationHintType.Vectorization,
                $"Type {TargetType.Name} supports SIMD vectorization",
                OptimizationImpact.High));
        }

        if (IsFloatingPoint())
        {
            hints.Add(new OptimizationHint(
                OptimizationHintType.Performance,
                "Consider using fast math options for floating-point operations",
                OptimizationImpact.Medium));
        }

        if (Marshal.SizeOf<T>() <= 8)
        {
            hints.Add(new OptimizationHint(
                OptimizationHintType.CacheOptimization,
                "Small numeric type benefits from cache-friendly access patterns",
                OptimizationImpact.Medium));
        }

        return hints;
    }

    private double GetBaseComplexity()
    {
        return TargetType switch
        {
            Type t when t == typeof(float) => 1.0,
            Type t when t == typeof(double) => 1.2,
            Type t when t == typeof(int) => 0.8,
            Type t when t == typeof(long) => 1.0,
            Type t when t == typeof(short) => 0.6,
            Type t when t == typeof(byte) => 0.4,
            Type t when t == typeof(decimal) => 4.0,
            _ => 1.0
        };
    }

    private bool IsFloatingPoint()
    {
        return TargetType == typeof(float) || 
               TargetType == typeof(double) || 
               TargetType == typeof(decimal);
    }

    private int CountTypeUsage(Expression expression)
    {
        var count = 0;
        var visitor = new TypeUsageVisitor(TargetType, () => count++);
        visitor.Visit(expression);
        return count;
    }

    private MemoryUsagePattern DetermineMemoryPattern(Expression expression)
    {
        // Simple heuristic - could be more sophisticated
        return expression switch
        {
            ConstantExpression => MemoryUsagePattern.ReadOnly,
            ParameterExpression => MemoryUsagePattern.ReadWrite,
            _ => MemoryUsagePattern.Sequential
        };
    }

    private bool DetermineSpecializationNeed(Expression expression)
    {
        // Numeric types often benefit from specialization
        return IsFloatingPoint() || Marshal.SizeOf<T>() >= 8;
    }
}

/// <summary>
/// Visitor that counts usage of a specific type in an expression tree.
/// </summary>
internal class TypeUsageVisitor : ExpressionVisitor
{
    private readonly Type _targetType;
    private readonly Action _onTypeFound;

    public TypeUsageVisitor(Type targetType, Action onTypeFound)
    {
        _targetType = targetType;
        _onTypeFound = onTypeFound;
    }

    public override Expression? Visit(Expression? node)
    {
        if (node?.Type == _targetType)
        {
            _onTypeFound();
        }
        
        return base.Visit(node);
    }
}