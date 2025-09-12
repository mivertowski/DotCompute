// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Types;
using System.Linq.Expressions;
using DotCompute.Linq.Compilation.Analysis;

namespace DotCompute.Linq.Analysis
{
    /// <summary>
    /// Provides information about a specific operator for backend execution optimization.
    /// </summary>
    /// <remarks>
    /// This class contains detailed metadata about an operator that helps the compiler
    /// and runtime make informed decisions about code generation, backend selection,
    /// and optimization strategies.
    /// </remarks>
    public sealed class OperatorInfo
    {
        /// <summary>
        /// Gets the operator type this information describes.
        /// </summary>
        public ExpressionType OperatorType { get; init; }

        /// <summary>
        /// Gets the operand types for this operator.
        /// </summary>
        public Type[] OperandTypes { get; init; } = [];

        /// <summary>
        /// Gets the result type of the operator.
        /// </summary>
        public Type ResultType { get; init; } = typeof(object);

        /// <summary>
        /// Gets the target backend for this operator information.
        /// </summary>
        public BackendType Backend { get; init; }

        /// <summary>
        /// Gets the implementation method used by the backend.
        /// </summary>
        public ImplementationMethod Implementation { get; init; }

        /// <summary>
        /// Gets whether this operator is natively supported by the backend.
        /// </summary>
        public bool IsNativelySupported { get; init; }

        /// <summary>
        /// Gets the performance characteristics of this operator on the backend.
        /// </summary>
        public PerformanceCharacteristics Performance { get; init; } = new();

        /// <summary>
        /// Gets the accuracy information for this operator.
        /// </summary>
        public AccuracyInfo Accuracy { get; init; } = new();

        /// <summary>
        /// Gets alternative implementations if the native one is not supported.
        /// </summary>
        public IReadOnlyList<string> AlternativeImplementations { get; init; } = [];

        /// <summary>
        /// Gets optimization hints specific to this operator on the backend.
        /// </summary>
        public IReadOnlyList<DotCompute.Linq.Types.OptimizationHint> OptimizationHints { get; init; } = [];

        /// <summary>
        /// Gets additional metadata about this operator.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; init; } =

            new Dictionary<string, object>();

        /// <summary>
        /// Gets the computational complexity of this operator.
        /// </summary>
        public ComputationalComplexity Complexity { get; init; }

        /// <summary>
        /// Gets whether the operator is commutative (a op b = b op a).
        /// </summary>
        public bool IsCommutative { get; init; }

        /// <summary>
        /// Gets whether the operator is associative ((a op b) op c = a op (b op c)).
        /// </summary>
        public bool IsAssociative { get; init; }

        /// <summary>
        /// Gets whether the operator is distributive over other operations.
        /// </summary>
        public bool IsDistributive { get; init; }

        /// <summary>
        /// Gets the memory access pattern for this operator.
        /// </summary>
        public MemoryAccessPattern MemoryAccessPattern { get; init; }

        /// <summary>
        /// Gets whether this operator supports vectorization.
        /// </summary>
        public bool SupportsVectorization { get; init; }

        /// <summary>
        /// Gets the optimal vector width for this operator on the backend.
        /// </summary>
        public int OptimalVectorWidth { get; init; } = 1;

        /// <summary>
        /// Creates operator information for a CPU backend.
        /// </summary>
        /// <param name="operatorType">The operator type.</param>
        /// <param name="operandTypes">The operand types.</param>
        /// <param name="resultType">The result type.</param>
        /// <returns>CPU-optimized operator information.</returns>
        public static OperatorInfo ForCPU(ExpressionType operatorType, Type[] operandTypes, Type resultType)
        {
            return new OperatorInfo
            {
                OperatorType = operatorType,
                OperandTypes = operandTypes,
                ResultType = resultType,
                Backend = BackendType.CPU,
                Implementation = ImplementationMethod.Intrinsic,
                IsNativelySupported = true,
                SupportsVectorization = IsVectorizableOperator(operatorType),
                OptimalVectorWidth = GetOptimalVectorWidth(operandTypes.FirstOrDefault() ?? resultType),
                Performance = new PerformanceCharacteristics
                {
                    Throughput = 1e9, // 1 GOPS baseline
                    Latency = 1e-9,   // 1 nanosecond baseline
                    MemoryBandwidth = 50e9, // 50 GB/s typical
                    ComputeIntensity = 1.0
                }
            };
        }

        /// <summary>
        /// Creates operator information for a CUDA backend.
        /// </summary>
        /// <param name="operatorType">The operator type.</param>
        /// <param name="operandTypes">The operand types.</param>
        /// <param name="resultType">The result type.</param>
        /// <returns>CUDA-optimized operator information.</returns>
        public static OperatorInfo ForCUDA(ExpressionType operatorType, Type[] operandTypes, Type resultType)
        {
            return new OperatorInfo
            {
                OperatorType = operatorType,
                OperandTypes = operandTypes,
                ResultType = resultType,
                Backend = BackendType.CUDA,
                Implementation = ImplementationMethod.Native,
                IsNativelySupported = IsCudaSupported(operatorType, operandTypes),
                SupportsVectorization = true,
                OptimalVectorWidth = 32, // Warp size
                Performance = new PerformanceCharacteristics
                {
                    Throughput = 10e12, // 10 TOPS baseline
                    Latency = 1e-6,     // 1 microsecond baseline
                    MemoryBandwidth = 900e9, // ~900 GB/s for modern GPUs
                    ComputeIntensity = 10.0
                }
            };
        }

        private static bool IsVectorizableOperator(ExpressionType operatorType)
        {
            return operatorType switch
            {
                ExpressionType.Add or ExpressionType.Subtract or
                ExpressionType.Multiply or ExpressionType.Divide or
                ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr or
                ExpressionType.Equal or ExpressionType.NotEqual or
                ExpressionType.LessThan or ExpressionType.LessThanOrEqual or
                ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual => true,
                _ => false
            };
        }

        private static int GetOptimalVectorWidth(Type type)
        {
            return type switch
            {
                _ when type == typeof(float) => 8,  // 256-bit / 32-bit = 8
                _ when type == typeof(double) => 4, // 256-bit / 64-bit = 4
                _ when type == typeof(int) => 8,    // 256-bit / 32-bit = 8
                _ when type == typeof(long) => 4,   // 256-bit / 64-bit = 4
                _ when type == typeof(byte) => 32,  // 256-bit / 8-bit = 32
                _ when type == typeof(short) => 16, // 256-bit / 16-bit = 16
                _ => 1
            };
        }

        private static bool IsCudaSupported(ExpressionType operatorType, Type[] operandTypes)
        {
            // Most mathematical and logical operations are supported on CUDA
            // Complex operations like function calls would need individual analysis
            return operatorType switch
            {
                ExpressionType.Add or ExpressionType.Subtract or
                ExpressionType.Multiply or ExpressionType.Divide or
                ExpressionType.Modulo or ExpressionType.Power or
                ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr or
                ExpressionType.Not or ExpressionType.Negate or
                ExpressionType.Equal or ExpressionType.NotEqual or
                ExpressionType.LessThan or ExpressionType.LessThanOrEqual or
                ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or
                ExpressionType.Conditional => true,
                _ => false
            };
        }
    }
}