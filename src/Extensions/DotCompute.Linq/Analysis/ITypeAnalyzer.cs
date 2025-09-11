// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Types;

namespace DotCompute.Linq.Analysis
{
    /// <summary>
    /// Defines the interface for analyzing types in expressions for compute optimization.
    /// </summary>
    /// <remarks>
    /// Type analysis is crucial for determining optimal memory layout, vectorization
    /// opportunities, and backend-specific optimizations. This analyzer examines type
    /// characteristics to guide code generation and execution strategies.
    /// </remarks>
    public interface ITypeAnalyzer
    {
        /// <summary>
        /// Analyzes a type for compute characteristics and optimization opportunities.
        /// </summary>
        /// <param name="type">The type to analyze.</param>
        /// <returns>A type analysis result containing optimization information.</returns>
        TypeAnalysisResult AnalyzeType(Type type);

        /// <summary>
        /// Determines if a type is suitable for vectorization.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>Vectorization information for the type.</returns>
        VectorizationInfo GetVectorizationInfo(Type type);

        /// <summary>
        /// Gets memory layout information for a type.
        /// </summary>
        /// <param name="type">The type to analyze.</param>
        /// <returns>Memory layout characteristics.</returns>
        MemoryLayoutInfo GetMemoryLayout(Type type);

        /// <summary>
        /// Determines the optimal data type for a given expression context.
        /// </summary>
        /// <param name="expression">The expression context.</param>
        /// <param name="backend">The target backend.</param>
        /// <returns>The recommended data type for optimal performance.</returns>
        Type GetOptimalDataType(Expression expression, BackendType backend);

        /// <summary>
        /// Analyzes type compatibility between different backends.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="sourceBackend">The source backend.</param>
        /// <param name="targetBackend">The target backend.</param>
        /// <returns>Compatibility analysis result.</returns>
        TypeCompatibilityResult AnalyzeCompatibility(Type type, BackendType sourceBackend, BackendType targetBackend);

        /// <summary>
        /// Gets conversion information for transforming between types.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <param name="targetType">The target type.</param>
        /// <returns>Type conversion information.</returns>
        TypeConversionInfo GetConversionInfo(Type sourceType, Type targetType);
    }

    /// <summary>
    /// Represents the result of type analysis for compute optimization.
    /// </summary>
    public sealed class TypeAnalysisResult
    {
        /// <summary>
        /// Gets or sets the analyzed type.
        /// </summary>
        public required Type AnalyzedType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the type is compute-friendly.
        /// </summary>
        public bool IsComputeFriendly { get; set; }

        /// <summary>
        /// Gets or sets the size of the type in bytes.
        /// </summary>
        public int SizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets the alignment requirements in bytes.
        /// </summary>
        public int AlignmentRequirement { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the type supports vectorization.
        /// </summary>
        public bool SupportsVectorization { get; set; }

        /// <summary>
        /// Gets or sets the vectorization width (number of elements per vector).
        /// </summary>
        public int VectorizationWidth { get; set; }

        /// <summary>
        /// Gets or sets the backend compatibility flags.
        /// </summary>
        public BackendCompatibilityFlags BackendCompatibility { get; set; }

        /// <summary>
        /// Gets or sets optimization hints specific to this type.
        /// </summary>
        public List<OptimizationHint> OptimizationHints { get; set; } = [];

        /// <summary>
        /// Gets or sets any limitations or constraints for this type.
        /// </summary>
        public List<string> Limitations { get; set; } = [];
    }

    /// <summary>
    /// Represents vectorization information for a type.
    /// </summary>
    public sealed class VectorizationInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether vectorization is supported.
        /// </summary>
        public bool IsSupported { get; set; }

        /// <summary>
        /// Gets or sets the optimal vector width.
        /// </summary>
        public int OptimalWidth { get; set; }

        /// <summary>
        /// Gets or sets the maximum supported vector width.
        /// </summary>
        public int MaxWidth { get; set; }

        /// <summary>
        /// Gets or sets the vector instruction set to use.
        /// </summary>
        public string? InstructionSet { get; set; }

        /// <summary>
        /// Gets or sets the expected performance improvement factor.
        /// </summary>
        public double PerformanceGain { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets constraints on vectorization.
        /// </summary>
        public List<string> Constraints { get; set; } = [];
    }

    /// <summary>
    /// Represents memory layout information for a type.
    /// </summary>
    public sealed class MemoryLayoutInfo
    {
        /// <summary>
        /// Gets or sets the total size in bytes.
        /// </summary>
        public int Size { get; set; }

        /// <summary>
        /// Gets or sets the alignment requirement.
        /// </summary>
        public int Alignment { get; set; }

        /// <summary>
        /// Gets or sets the stride between elements in arrays.
        /// </summary>
        public int Stride { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the layout is contiguous.
        /// </summary>
        public bool IsContiguous { get; set; }

        /// <summary>
        /// Gets or sets padding information.
        /// </summary>
        public int Padding { get; set; }

        /// <summary>
        /// Gets or sets cache line utilization efficiency.
        /// </summary>
        public double CacheEfficiency { get; set; }
    }

    /// <summary>
    /// Represents type compatibility analysis between backends.
    /// </summary>
    public sealed class TypeCompatibilityResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the type is compatible.
        /// </summary>
        public bool IsCompatible { get; set; }

        /// <summary>
        /// Gets or sets whether automatic conversion is available.
        /// </summary>
        public bool HasAutoConversion { get; set; }

        /// <summary>
        /// Gets or sets the conversion cost (0.0 = free, 1.0 = expensive).
        /// </summary>
        public double ConversionCost { get; set; }

        /// <summary>
        /// Gets or sets required conversion steps.
        /// </summary>
        public List<string> ConversionSteps { get; set; } = [];

        /// <summary>
        /// Gets or sets compatibility warnings.
        /// </summary>
        public List<string> Warnings { get; set; } = [];
    }

    /// <summary>
    /// Represents type conversion information.
    /// </summary>
    public sealed class TypeConversionInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether conversion is possible.
        /// </summary>
        public bool IsConversionPossible { get; set; }

        /// <summary>
        /// Gets or sets whether the conversion is lossless.
        /// </summary>
        public bool IsLossless { get; set; }

        /// <summary>
        /// Gets or sets the conversion method to use.
        /// </summary>
        public ConversionMethod Method { get; set; }

        /// <summary>
        /// Gets or sets the performance cost of conversion.
        /// </summary>
        public double PerformanceCost { get; set; }

        /// <summary>
        /// Gets or sets the conversion expression if available.
        /// </summary>
        public Expression? ConversionExpression { get; set; }
    }

    /// <summary>
    /// Defines backend compatibility flags.
    /// </summary>
    [Flags]
    public enum BackendCompatibilityFlags
    {
        /// <summary>
        /// No backend compatibility.
        /// </summary>
        None = 0,

        /// <summary>
        /// Compatible with CPU backend.
        /// </summary>
        CPU = 1 << 0,

        /// <summary>
        /// Compatible with CUDA backend.
        /// </summary>
        CUDA = 1 << 1,

        /// <summary>
        /// Compatible with OpenCL backend.
        /// </summary>
        OpenCL = 1 << 2,

        /// <summary>
        /// Compatible with Metal backend.
        /// </summary>
        Metal = 1 << 3,

        /// <summary>
        /// Compatible with Vulkan backend.
        /// </summary>
        Vulkan = 1 << 4,

        /// <summary>
        /// Compatible with DirectCompute backend.
        /// </summary>
        DirectCompute = 1 << 5,

        /// <summary>
        /// Compatible with all backends.
        /// </summary>
        All = CPU | CUDA | OpenCL | Metal | Vulkan | DirectCompute
    }

    /// <summary>
    /// Defines conversion methods for type transformations.
    /// </summary>
    public enum ConversionMethod
    {
        /// <summary>
        /// No conversion possible.
        /// </summary>
        None,

        /// <summary>
        /// Direct cast or implicit conversion.
        /// </summary>
        Direct,

        /// <summary>
        /// Explicit cast with possible precision loss.
        /// </summary>
        Cast,

        /// <summary>
        /// Function call or method invocation.
        /// </summary>
        Function,

        /// <summary>
        /// Custom conversion routine.
        /// </summary>
        Custom,

        /// <summary>
        /// Bitwise reinterpretation.
        /// </summary>
        Reinterpret
    }
}