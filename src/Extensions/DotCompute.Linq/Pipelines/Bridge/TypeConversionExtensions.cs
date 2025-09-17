// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DotCompute.Linq.Analysis;
using DotCompute.Linq.Compilation.Analysis;
using DotCompute.Linq.Types;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Operators.Parameters;
using LinqParameterDirection = DotCompute.Linq.Operators.Parameters.ParameterDirection;
using AbstractionsParameterDirection = DotCompute.Abstractions.Kernels.ParameterDirection;
using AbstractionsMemoryPattern = DotCompute.Abstractions.Types.MemoryAccessPattern;
using PipelineMemoryPattern = DotCompute.Linq.Compilation.Analysis.MemoryAccessPattern;

namespace DotCompute.Linq.Pipelines.Bridge;

/// <summary>
/// Provides high-performance, zero-overhead type conversion adapters for the DotCompute pipeline system.
/// This class enables seamless interoperability between different type systems while maintaining
/// compile-time type safety and runtime efficiency.
/// </summary>
/// <remarks>
/// All conversion methods are marked as aggressive inline to eliminate method call overhead
/// and enable the JIT compiler to optimize conversions to near-zero cost operations.
/// The conversions handle null cases gracefully and provide bidirectional mapping capabilities.
/// </remarks>
public static class TypeConversionExtensions
{
    #region PipelineOperatorInfo ↔ OperatorInfo Conversions

    /// <summary>
    /// Converts a PipelineOperatorInfo to an OperatorInfo for backend execution.
    /// </summary>
    /// <param name="pipelineInfo">The pipeline operator information to convert</param>
    /// <param name="targetBackend">The target backend type for the conversion</param>
    /// <returns>An OperatorInfo instance optimized for the target backend</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DotCompute.Linq.Analysis.OperatorInfo? ToOperatorInfo(this PipelineOperatorInfo? pipelineInfo, BackendType targetBackend = BackendType.CPU)
    {
        if (pipelineInfo == null)
            return null;

        // Map string-based operator name to ExpressionType
        var operatorType = MapOperatorNameToExpressionType(pipelineInfo.Name);

        // Extract operand types from input types
        var operandTypes = pipelineInfo.InputTypes?.ToArray() ?? Array.Empty<Type>();

        // Create backend-optimized operator info
        var operatorInfo = targetBackend switch
        {
            BackendType.CUDA => DotCompute.Linq.Analysis.OperatorInfo.ForCUDA(operatorType, operandTypes, pipelineInfo.OutputType),
            BackendType.CPU => DotCompute.Linq.Analysis.OperatorInfo.ForCPU(operatorType, operandTypes, pipelineInfo.OutputType),
            _ => DotCompute.Linq.Analysis.OperatorInfo.ForCPU(operatorType, operandTypes, pipelineInfo.OutputType) // Default to CPU
        };

        // Apply additional optimizations based on pipeline information
        return EnhanceOperatorInfo(operatorInfo, pipelineInfo);
    }

    /// <summary>
    /// Converts an OperatorInfo back to a PipelineOperatorInfo for pipeline analysis.
    /// </summary>
    /// <param name="operatorInfo">The operator information to convert</param>
    /// <returns>A PipelineOperatorInfo instance suitable for pipeline processing</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PipelineOperatorInfo? ToPipelineOperatorInfo(this DotCompute.Linq.Analysis.OperatorInfo? operatorInfo)
    {
        if (operatorInfo == null)
            return null;

        return new PipelineOperatorInfo
        {
            Name = operatorInfo.OperatorType.ToString(),
            OperatorType = operatorInfo.OperandTypes?.FirstOrDefault() ?? operatorInfo.ResultType,
            ComplexityScore = MapComplexityToScore(operatorInfo.Complexity),
            CanParallelize = operatorInfo.SupportsVectorization,
            MemoryRequirement = EstimateMemoryRequirement(operatorInfo),
            InputTypes = operatorInfo.OperandTypes?.ToList() ?? new List<Type>(),
            OutputType = operatorInfo.ResultType,
            SupportsGpu = operatorInfo.Backend == BackendType.CUDA || operatorInfo.Backend == BackendType.Metal,
            SupportsCpu = operatorInfo.Backend == BackendType.CPU,
            IsNativelySupported = operatorInfo.IsNativelySupported,
            Implementation = operatorInfo.Implementation.ToString(),
            PerformanceCost = CalculatePerformanceCost(operatorInfo.Performance),
            Accuracy = operatorInfo.Accuracy?.NumericalStability ?? 1.0,
            Metadata = CreateMetadataDictionary(operatorInfo)
        };
    }

    /// <summary>
    /// Batch converts a collection of PipelineOperatorInfo to OperatorInfo instances.
    /// </summary>
    /// <param name="pipelineInfos">The collection of pipeline operator information</param>
    /// <param name="targetBackend">The target backend for all conversions</param>
    /// <returns>A collection of converted OperatorInfo instances</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<DotCompute.Linq.Analysis.OperatorInfo> ToOperatorInfos(this IEnumerable<PipelineOperatorInfo>? pipelineInfos, BackendType targetBackend = BackendType.CPU)
    {
        if (pipelineInfos == null)
            yield break;

        foreach (var pipelineInfo in pipelineInfos)
        {
            var operatorInfo = pipelineInfo.ToOperatorInfo(targetBackend);
            if (operatorInfo != null)
                yield return operatorInfo;
        }
    }

    /// <summary>
    /// Batch converts a collection of OperatorInfo to PipelineOperatorInfo instances.
    /// </summary>
    /// <param name="operatorInfos">The collection of operator information</param>
    /// <returns>A collection of converted PipelineOperatorInfo instances</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<PipelineOperatorInfo> ToPipelineOperatorInfos(this IEnumerable<DotCompute.Linq.Analysis.OperatorInfo>? operatorInfos)
    {
        if (operatorInfos == null)
            yield break;

        foreach (var operatorInfo in operatorInfos)
        {
            var pipelineInfo = operatorInfo.ToPipelineOperatorInfo();
            if (pipelineInfo != null)
                yield return pipelineInfo;
        }
    }

    #endregion

    #region MemoryAccessPattern Enum Mappings

    /// <summary>
    /// Converts an Abstractions MemoryAccessPattern to a Pipeline MemoryAccessPattern.
    /// </summary>
    /// <param name="abstractionsPattern">The abstractions memory access pattern</param>
    /// <returns>The corresponding pipeline memory access pattern</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PipelineMemoryPattern ToPipelinePattern(this AbstractionsMemoryPattern abstractionsPattern)
    {
        return abstractionsPattern switch
        {
            AbstractionsMemoryPattern.Sequential => PipelineMemoryPattern.Sequential,
            AbstractionsMemoryPattern.Strided => PipelineMemoryPattern.Strided,
            AbstractionsMemoryPattern.Coalesced => PipelineMemoryPattern.Coalesced,
            AbstractionsMemoryPattern.Random => PipelineMemoryPattern.Random,
            AbstractionsMemoryPattern.Mixed => PipelineMemoryPattern.Random, // Map to closest equivalent
            AbstractionsMemoryPattern.ScatterGather => PipelineMemoryPattern.Random, // Map to closest equivalent
            AbstractionsMemoryPattern.Broadcast => PipelineMemoryPattern.Sequential, // Map to closest equivalent
            _ => PipelineMemoryPattern.Sequential // Default fallback
        };
    }

    /// <summary>
    /// Converts a Pipeline MemoryAccessPattern to an Abstractions MemoryAccessPattern.
    /// </summary>
    /// <param name="pipelinePattern">The pipeline memory access pattern</param>
    /// <returns>The corresponding abstractions memory access pattern</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AbstractionsMemoryPattern ToAbstractionsPattern(this PipelineMemoryPattern pipelinePattern)
    {
        return pipelinePattern switch
        {
            PipelineMemoryPattern.Sequential => AbstractionsMemoryPattern.Sequential,
            PipelineMemoryPattern.Strided => AbstractionsMemoryPattern.Strided,
            PipelineMemoryPattern.Coalesced => AbstractionsMemoryPattern.Coalesced,
            PipelineMemoryPattern.Random => AbstractionsMemoryPattern.Random,
            _ => AbstractionsMemoryPattern.Sequential // Default fallback
        };
    }

    /// <summary>
    /// Converts a MemoryAccessType to the appropriate MemoryAccessPattern.
    /// </summary>
    /// <param name="accessType">The memory access type</param>
    /// <returns>The corresponding memory access pattern</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AbstractionsMemoryPattern ToMemoryAccessPattern(this MemoryAccessType accessType)
    {
        return accessType switch
        {
            MemoryAccessType.Sequential => AbstractionsMemoryPattern.Sequential,
            MemoryAccessType.Random => AbstractionsMemoryPattern.Random,
            MemoryAccessType.Strided => AbstractionsMemoryPattern.Strided,
            MemoryAccessType.Coalesced => AbstractionsMemoryPattern.Coalesced,
            _ => AbstractionsMemoryPattern.Sequential // Default fallback
        };
    }

    /// <summary>
    /// Converts a MemoryAccessPattern to the appropriate MemoryAccessType.
    /// </summary>
    /// <param name="pattern">The memory access pattern</param>
    /// <returns>The corresponding memory access type</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryAccessType ToMemoryAccessType(this AbstractionsMemoryPattern pattern)
    {
        return pattern switch
        {
            AbstractionsMemoryPattern.Sequential => MemoryAccessType.Sequential,
            AbstractionsMemoryPattern.Random => MemoryAccessType.Random,
            AbstractionsMemoryPattern.Strided => MemoryAccessType.Strided,
            AbstractionsMemoryPattern.Coalesced => MemoryAccessType.Coalesced,
            AbstractionsMemoryPattern.Mixed => MemoryAccessType.Random, // Map to closest equivalent
            AbstractionsMemoryPattern.ScatterGather => MemoryAccessType.Random, // Map to closest equivalent
            AbstractionsMemoryPattern.Broadcast => MemoryAccessType.Sequential, // Map to closest equivalent
            _ => MemoryAccessType.Sequential // Default fallback
        };
    }

    #endregion

    #region Parameter Direction Mappings

    /// <summary>
    /// Converts a LINQ ParameterDirection to an Abstractions ParameterDirection.
    /// </summary>
    /// <param name="linqDirection">The LINQ parameter direction</param>
    /// <returns>The corresponding abstractions parameter direction</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AbstractionsParameterDirection ToAbstractionsDirection(this LinqParameterDirection linqDirection)
    {
        return linqDirection switch
        {
            LinqParameterDirection.In => AbstractionsParameterDirection.In,
            LinqParameterDirection.Out => AbstractionsParameterDirection.Out,
            LinqParameterDirection.InOut => AbstractionsParameterDirection.InOut,
            // Note: Input and Output are aliases for In and Out respectively, so they're handled by the above cases
            _ => AbstractionsParameterDirection.In // Default fallback
        };
    }

    /// <summary>
    /// Converts an Abstractions ParameterDirection to a LINQ ParameterDirection.
    /// </summary>
    /// <param name="abstractionsDirection">The abstractions parameter direction</param>
    /// <returns>The corresponding LINQ parameter direction</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LinqParameterDirection ToLinqDirection(this AbstractionsParameterDirection abstractionsDirection)
    {
        return abstractionsDirection switch
        {
            AbstractionsParameterDirection.In => LinqParameterDirection.In,
            AbstractionsParameterDirection.Out => LinqParameterDirection.Out,
            AbstractionsParameterDirection.InOut => LinqParameterDirection.InOut,
            _ => LinqParameterDirection.In // Default fallback
        };
    }

    /// <summary>
    /// Batch converts a collection of LINQ parameter directions to Abstractions parameter directions.
    /// </summary>
    /// <param name="linqDirections">The collection of LINQ parameter directions</param>
    /// <returns>A collection of converted abstractions parameter directions</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<AbstractionsParameterDirection> ToAbstractionsDirections(this IEnumerable<LinqParameterDirection>? linqDirections)
    {
        if (linqDirections == null)
            yield break;

        foreach (var direction in linqDirections)
        {
            yield return direction.ToAbstractionsDirection();
        }
    }

    /// <summary>
    /// Batch converts a collection of Abstractions parameter directions to LINQ parameter directions.
    /// </summary>
    /// <param name="abstractionsDirections">The collection of abstractions parameter directions</param>
    /// <returns>A collection of converted LINQ parameter directions</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<LinqParameterDirection> ToLinqDirections(this IEnumerable<AbstractionsParameterDirection>? abstractionsDirections)
    {
        if (abstractionsDirections == null)
            yield break;

        foreach (var direction in abstractionsDirections)
        {
            yield return direction.ToLinqDirection();
        }
    }

    #endregion

    #region Pipeline Interface Bridge Adapters

    /// <summary>
    /// Converts a DataAccessPattern to a MemoryAccessPattern.
    /// </summary>
    /// <param name="dataPattern">The data access pattern</param>
    /// <returns>The corresponding memory access pattern</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AbstractionsMemoryPattern ToMemoryAccessPattern(this DataAccessPattern dataPattern)
    {
        return dataPattern switch
        {
            DataAccessPattern.Sequential => AbstractionsMemoryPattern.Sequential,
            DataAccessPattern.Random => AbstractionsMemoryPattern.Random,
            DataAccessPattern.Streaming => AbstractionsMemoryPattern.Sequential, // Map to closest equivalent
            DataAccessPattern.Sparse => AbstractionsMemoryPattern.ScatterGather,
            DataAccessPattern.CacheFriendly => AbstractionsMemoryPattern.Sequential,
            _ => AbstractionsMemoryPattern.Sequential // Default fallback
        };
    }

    /// <summary>
    /// Converts a MemoryAccessPattern to a DataAccessPattern.
    /// </summary>
    /// <param name="memoryPattern">The memory access pattern</param>
    /// <returns>The corresponding data access pattern</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DataAccessPattern ToDataAccessPattern(this AbstractionsMemoryPattern memoryPattern)
    {
        return memoryPattern switch
        {
            AbstractionsMemoryPattern.Sequential => DataAccessPattern.Sequential,
            AbstractionsMemoryPattern.Random => DataAccessPattern.Random,
            AbstractionsMemoryPattern.Strided => DataAccessPattern.Sequential, // Map to closest equivalent
            AbstractionsMemoryPattern.Coalesced => DataAccessPattern.CacheFriendly,
            AbstractionsMemoryPattern.Mixed => DataAccessPattern.Random,
            AbstractionsMemoryPattern.ScatterGather => DataAccessPattern.Sparse,
            AbstractionsMemoryPattern.Broadcast => DataAccessPattern.CacheFriendly,
            _ => DataAccessPattern.Sequential // Default fallback
        };
    }

    /// <summary>
    /// Creates a unified MemoryAccessCharacteristics from pipeline analysis data.
    /// </summary>
    /// <param name="globalPattern">The global memory access pattern</param>
    /// <returns>A unified memory access characteristics instance</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryAccessCharacteristics ToMemoryAccessCharacteristics(this GlobalMemoryAccessPattern? globalPattern)
    {
        if (globalPattern == null)
        {
            return new MemoryAccessCharacteristics
            {
                PrimaryPattern = DataAccessPattern.Sequential,
                LocalityScore = 0.5,
                CacheHitRatio = 0.5,
                BandwidthUtilization = 0.5,
                SupportsCoalescing = false,
                PreferredAlignment = 16
            };
        }

        return new MemoryAccessCharacteristics
        {
            PrimaryPattern = globalPattern.AccessType.ToMemoryAccessPattern().ToDataAccessPattern(),
            LocalityScore = globalPattern.LocalityFactor,
            CacheHitRatio = globalPattern.CacheEfficiency,
            BandwidthUtilization = globalPattern.BandwidthUtilization,
            SupportsCoalescing = globalPattern.IsCoalesced,
            PreferredAlignment = Math.Max(16, globalPattern.StridePattern) // Ensure minimum alignment
        };
    }

    #endregion

    #region Type Safety and Validation Extensions

    /// <summary>
    /// Validates that a conversion result is not null and throws a descriptive exception if it is.
    /// </summary>
    /// <typeparam name="T">The type being validated</typeparam>
    /// <param name="value">The value to validate</param>
    /// <param name="operation">The operation that was performed</param>
    /// <returns>The validated non-null value</returns>
    /// <exception cref="InvalidOperationException">Thrown if the value is null</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ValidateConversionResult<T>(this T? value, string operation) where T : class
    {
        return value ?? throw new InvalidOperationException($"Type conversion failed during {operation}: result was null");
    }

    /// <summary>
    /// Validates that a conversion result is not null and provides a fallback value if it is.
    /// </summary>
    /// <typeparam name="T">The type being validated</typeparam>
    /// <param name="value">The value to validate</param>
    /// <param name="fallback">The fallback value to use if the original is null</param>
    /// <returns>The validated value or the fallback</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ValidateConversionResult<T>(this T? value, T fallback) where T : class
    {
        return value ?? fallback;
    }

    /// <summary>
    /// Safely converts a value with null checking and error handling.
    /// </summary>
    /// <typeparam name="TSource">The source type</typeparam>
    /// <typeparam name="TTarget">The target type</typeparam>
    /// <param name="source">The source value to convert</param>
    /// <param name="converter">The conversion function</param>
    /// <returns>The converted value or null if the source is null</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTarget? SafeConvert<TSource, TTarget>(this TSource? source, Func<TSource, TTarget> converter) 
        where TSource : class 
        where TTarget : class
    {
        return source == null ? null : converter(source);
    }

    #endregion

    #region Private Helper Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ExpressionType MapOperatorNameToExpressionType(string operatorName)
    {
        return operatorName.ToUpperInvariant() switch
        {
            "ADD" or "ADDITION" => ExpressionType.Add,
            "SUB" or "SUBTRACT" or "SUBTRACTION" => ExpressionType.Subtract,
            "MUL" or "MULTIPLY" or "MULTIPLICATION" => ExpressionType.Multiply,
            "DIV" or "DIVIDE" or "DIVISION" => ExpressionType.Divide,
            "MOD" or "MODULO" => ExpressionType.Modulo,
            "POW" or "POWER" => ExpressionType.Power,
            "AND" or "BITWISEAND" => ExpressionType.And,
            "OR" or "BITWISEOR" => ExpressionType.Or,
            "XOR" or "EXCLUSIVEOR" => ExpressionType.ExclusiveOr,
            "NOT" or "BITWISENOT" => ExpressionType.Not,
            "EQ" or "EQUAL" => ExpressionType.Equal,
            "NE" or "NOTEQUAL" => ExpressionType.NotEqual,
            "LT" or "LESSTHAN" => ExpressionType.LessThan,
            "LE" or "LESSTHANOREQUAL" => ExpressionType.LessThanOrEqual,
            "GT" or "GREATERTHAN" => ExpressionType.GreaterThan,
            "GE" or "GREATERTHANOREQUAL" => ExpressionType.GreaterThanOrEqual,
            "NEG" or "NEGATE" => ExpressionType.Negate,
            "COND" or "CONDITIONAL" => ExpressionType.Conditional,
            _ => ExpressionType.Call // Default to method call for unknown operators
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MapComplexityToScore(ComputationalComplexity complexity)
    {
        return complexity switch
        {
            ComputationalComplexity.Constant => 1,
            ComputationalComplexity.Logarithmic => 2,
            ComputationalComplexity.Linear => 3,
            ComputationalComplexity.Linearithmic => 4,
            ComputationalComplexity.Quadratic => 5,
            ComputationalComplexity.Cubic => 6,
            ComputationalComplexity.Exponential => 10,
            _ => 3 // Default to linear complexity
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long EstimateMemoryRequirement(DotCompute.Linq.Analysis.OperatorInfo operatorInfo)
    {
        // Estimate based on operand types and vector width
        var baseSize = 0L;
        if (operatorInfo.OperandTypes != null)
        {
            foreach (var type in operatorInfo.OperandTypes)
            {
                baseSize += EstimateTypeSize(type);
            }
        }

        baseSize += EstimateTypeSize(operatorInfo.ResultType);
        return baseSize * operatorInfo.OptimalVectorWidth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long EstimateTypeSize(Type type)
    {
        return type switch
        {
            _ when type == typeof(byte) => 1,
            _ when type == typeof(short) => 2,
            _ when type == typeof(int) => 4,
            _ when type == typeof(long) => 8,
            _ when type == typeof(float) => 4,
            _ when type == typeof(double) => 8,
            _ when type == typeof(decimal) => 16,
            _ => 8 // Default assumption
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculatePerformanceCost(PerformanceCharacteristics performance)
    {
        // Calculate normalized cost based on throughput and latency
        if (performance.Throughput > 0)
        {
            return Math.Max(0.1, performance.Latency / (performance.Throughput * 1e-9));
        }
        return 1.0; // Default cost
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, object> CreateMetadataDictionary(DotCompute.Linq.Analysis.OperatorInfo operatorInfo)
    {
        var metadata = new Dictionary<string, object>
        {
            ["Backend"] = operatorInfo.Backend.ToString(),
            ["Implementation"] = operatorInfo.Implementation.ToString(),
            ["SupportsVectorization"] = operatorInfo.SupportsVectorization,
            ["OptimalVectorWidth"] = operatorInfo.OptimalVectorWidth,
            ["IsCommutative"] = operatorInfo.IsCommutative,
            ["IsAssociative"] = operatorInfo.IsAssociative,
            ["IsDistributive"] = operatorInfo.IsDistributive
        };

        // Add performance metrics
        if (operatorInfo.Performance != null)
        {
            metadata["Throughput"] = operatorInfo.Performance.Throughput;
            metadata["Latency"] = operatorInfo.Performance.Latency;
            metadata["MemoryBandwidth"] = operatorInfo.Performance.MemoryBandwidth;
            metadata["ComputeIntensity"] = operatorInfo.Performance.ComputeIntensity;
        }

        // Add accuracy information
        if (operatorInfo.Accuracy != null)
        {
            metadata["NumericalStability"] = operatorInfo.Accuracy.NumericalStability;
            metadata["ErrorBounds"] = operatorInfo.Accuracy.ErrorBounds;
        }

        // Add existing metadata
        if (operatorInfo.Metadata != null)
        {
            foreach (var kvp in operatorInfo.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        return metadata;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DotCompute.Linq.Analysis.OperatorInfo EnhanceOperatorInfo(DotCompute.Linq.Analysis.OperatorInfo baseInfo, PipelineOperatorInfo pipelineInfo)
    {
        // Create an enhanced version with pipeline-specific optimizations
        return new DotCompute.Linq.Analysis.OperatorInfo
        {
            OperatorType = baseInfo.OperatorType,
            OperandTypes = baseInfo.OperandTypes,
            ResultType = baseInfo.ResultType,
            Backend = baseInfo.Backend,
            Implementation = baseInfo.Implementation,
            IsNativelySupported = pipelineInfo.IsNativelySupported && baseInfo.IsNativelySupported,
            Performance = EnhancePerformanceCharacteristics(baseInfo.Performance, pipelineInfo),
            Accuracy = baseInfo.Accuracy,
            AlternativeImplementations = baseInfo.AlternativeImplementations,
            OptimizationHints = baseInfo.OptimizationHints,
            Metadata = CombineMetadata(baseInfo.Metadata, pipelineInfo.Metadata),
            Complexity = baseInfo.Complexity,
            IsCommutative = baseInfo.IsCommutative,
            IsAssociative = baseInfo.IsAssociative,
            IsDistributive = baseInfo.IsDistributive,
            MemoryAccessPattern = baseInfo.MemoryAccessPattern,
            SupportsVectorization = baseInfo.SupportsVectorization && pipelineInfo.CanParallelize,
            OptimalVectorWidth = baseInfo.OptimalVectorWidth
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PerformanceCharacteristics EnhancePerformanceCharacteristics(PerformanceCharacteristics basePerf, PipelineOperatorInfo pipelineInfo)
    {
        return new PerformanceCharacteristics
        {
            Throughput = basePerf.Throughput * (1.0 / Math.Max(0.1, pipelineInfo.PerformanceCost)),
            Latency = basePerf.Latency * Math.Max(0.1, pipelineInfo.PerformanceCost),
            MemoryBandwidth = basePerf.MemoryBandwidth,
            ComputeIntensity = basePerf.ComputeIntensity,
            CacheEfficiency = basePerf.CacheEfficiency,
            ParallelEfficiency = basePerf.ParallelEfficiency * (pipelineInfo.CanParallelize ? 1.0 : 0.1),
            ScalabilityFactor = basePerf.ScalabilityFactor,
            PowerEfficiency = basePerf.PowerEfficiency
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IReadOnlyDictionary<string, object> CombineMetadata(IReadOnlyDictionary<string, object>? baseMetadata, Dictionary<string, object>? pipelineMetadata)
    {
        var combined = new Dictionary<string, object>();

        if (baseMetadata != null)
        {
            foreach (var kvp in baseMetadata)
            {
                combined[kvp.Key] = kvp.Value;
            }
        }

        if (pipelineMetadata != null)
        {
            foreach (var kvp in pipelineMetadata)
            {
                combined[$"Pipeline_{kvp.Key}"] = kvp.Value;
            }
        }

        return combined;
    }

    #endregion

    #region Missing Helper Methods

    /// <summary>
    /// Converts operator info to the compilation analysis type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DotCompute.Linq.Analysis.OperatorInfo ConvertToOperatorInfo(this PipelineOperatorInfo pipelineInfo)
    {
        return pipelineInfo.ToOperatorInfo(BackendType.CPU) ?? new DotCompute.Linq.Analysis.OperatorInfo
        {
            OperatorType = ExpressionType.Call,
            ResultType = pipelineInfo.OutputType
        };
    }

    /// <summary>
    /// Converts memory access pattern between different type systems.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DotCompute.Core.Optimization.MemoryAccessPattern ConvertMemoryAccessPattern(DotCompute.Linq.Pipelines.Models.AccessPattern accessPattern)
    {
        return accessPattern switch
        {
            DotCompute.Linq.Pipelines.Models.AccessPattern.Sequential => DotCompute.Core.Optimization.MemoryAccessPattern.Sequential,
            DotCompute.Linq.Pipelines.Models.AccessPattern.Random => DotCompute.Core.Optimization.MemoryAccessPattern.Random,
            DotCompute.Linq.Pipelines.Models.AccessPattern.Strided => DotCompute.Core.Optimization.MemoryAccessPattern.Strided,
            DotCompute.Linq.Pipelines.Models.AccessPattern.Coalesced => DotCompute.Core.Optimization.MemoryAccessPattern.Coalesced,
            DotCompute.Linq.Pipelines.Models.AccessPattern.Scattered => DotCompute.Core.Optimization.MemoryAccessPattern.Scattered,
            _ => DotCompute.Core.Optimization.MemoryAccessPattern.Sequential
        };
    }

    #endregion
}