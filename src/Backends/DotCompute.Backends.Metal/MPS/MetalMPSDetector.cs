// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.MPS;

/// <summary>
/// Intelligent detector that analyzes kernel definitions to determine if they can be
/// accelerated using Metal Performance Shaders instead of custom MSL kernels.
/// Provides 3-10x speedup for standard operations like matrix multiply, convolution, etc.
/// </summary>
public sealed class MetalMPSDetector
{
    private readonly ILogger _logger;
    private readonly MPSCapabilities _capabilities;

    public MetalMPSDetector(ILogger logger, MPSCapabilities capabilities)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capabilities = capabilities;
    }

    /// <summary>
    /// Analyzes a kernel definition to determine if it can be executed using MPS.
    /// </summary>
    /// <param name="kernel">The kernel definition to analyze</param>
    /// <param name="mpsOp">The detected MPS operation type if supported</param>
    /// <returns>True if MPS can be used, false otherwise</returns>
    public bool CanUseMPS(KernelDefinition kernel, out MPSOperationType mpsOp)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        mpsOp = MPSOperationType.Unknown;

        // Extract kernel name and source
        var name = kernel.Name?.ToLowerInvariant() ?? string.Empty;
        var source = kernel.Source ?? kernel.Code ?? string.Empty;
        var sourceLower = source.ToLowerInvariant();

        // Matrix operations - highest priority (3-4x speedup)
        if (IsMatrixMultiply(name, sourceLower))
        {
            if (!_capabilities.HasBLAS)
            {
                _logger.LogTrace("Matrix multiply detected but MPS BLAS not available");
                return false;
            }
            mpsOp = MPSOperationType.MatrixMultiplication;
            _logger.LogDebug("Detected matrix multiply operation - MPS can provide 3-4x speedup");
            return true;
        }

        if (IsMatrixVectorMultiply(name, sourceLower))
        {
            if (!_capabilities.HasBLAS)
            {
                return false;
            }
            mpsOp = MPSOperationType.MatrixVectorMultiplication;
            _logger.LogDebug("Detected matrix-vector multiply operation - MPS available");
            return true;
        }

        // Convolution operations (3-5x speedup for CNN)
        if (IsConvolution2D(name, sourceLower))
        {
            if (!_capabilities.HasCNN)
            {
                _logger.LogTrace("Convolution detected but MPS CNN not available");
                return false;
            }
            mpsOp = MPSOperationType.Convolution2D;
            _logger.LogDebug("Detected 2D convolution - MPS can provide 3-5x speedup");
            return true;
        }

        if (IsMaxPooling(name, sourceLower))
        {
            if (!_capabilities.HasCNN)
            {
                return false;
            }
            mpsOp = MPSOperationType.MaxPooling2D;
            _logger.LogDebug("Detected max pooling operation - MPS available");
            return true;
        }

        // Element-wise operations (2-3x speedup for large arrays)
        if (IsElementWiseAdd(name, sourceLower))
        {
            mpsOp = MPSOperationType.ElementWiseAdd;
            _logger.LogDebug("Detected element-wise addition - MPS available");
            return true;
        }

        if (IsElementWiseMultiply(name, sourceLower))
        {
            mpsOp = MPSOperationType.ElementWiseMultiply;
            _logger.LogDebug("Detected element-wise multiplication - MPS available");
            return true;
        }

        // Neural network operations (2-4x speedup)
        if (IsReLU(name, sourceLower))
        {
            if (!_capabilities.HasNeuralNetwork)
            {
                return false;
            }
            mpsOp = MPSOperationType.ReLU;
            _logger.LogDebug("Detected ReLU activation - MPS available");
            return true;
        }

        if (IsSigmoid(name, sourceLower))
        {
            if (!_capabilities.HasNeuralNetwork)
            {
                return false;
            }
            mpsOp = MPSOperationType.Sigmoid;
            _logger.LogDebug("Detected Sigmoid activation - MPS available");
            return true;
        }

        if (IsTanh(name, sourceLower))
        {
            if (!_capabilities.HasNeuralNetwork)
            {
                return false;
            }
            mpsOp = MPSOperationType.Tanh;
            _logger.LogDebug("Detected Tanh activation - MPS available");
            return true;
        }

        if (IsBatchNormalization(name, sourceLower))
        {
            if (!_capabilities.HasNeuralNetwork)
            {
                return false;
            }
            mpsOp = MPSOperationType.BatchNormalization;
            _logger.LogDebug("Detected batch normalization - MPS available");
            return true;
        }

        // Reduction operations
        if (IsReduction(name, sourceLower, out var reductionType))
        {
            mpsOp = reductionType;
            _logger.LogDebug("Detected reduction operation: {Type} - MPS available", reductionType);
            return true;
        }

        // Image processing operations
        if (IsImageConversion(name, sourceLower))
        {
            mpsOp = MPSOperationType.ImageConversion;
            _logger.LogDebug("Detected image conversion - MPS available");
            return true;
        }

        if (IsGaussianBlur(name, sourceLower))
        {
            mpsOp = MPSOperationType.GaussianBlur;
            _logger.LogDebug("Detected Gaussian blur - MPS available");
            return true;
        }

        _logger.LogTrace("No MPS operation detected for kernel: {Name}", kernel.Name);
        return false;
    }

    #region Detection Methods

    private static bool IsMatrixMultiply(string name, string source)
    {
        // Check name patterns
        if (name.Contains("matmul") || name.Contains("matrixmultiply") ||
            name.Contains("gemm") || name.Contains("matrix_multiply"))
        {
            return true;
        }

        // Check source patterns (looking for matrix multiply structure)
        return (source.Contains("matrix") || source.Contains("gemm")) &&
               (source.Contains("multiply") || source.Contains("product")) &&
               (source.Contains("for") || source.Contains("loop")) &&
               source.Contains("sum");
    }

    private static bool IsMatrixVectorMultiply(string name, string source)
    {
        if (name.Contains("matvec") || name.Contains("matrix_vector") ||
            name.Contains("gemv"))
        {
            return true;
        }

        return source.Contains("matrix") && source.Contains("vector") &&
               source.Contains("multiply");
    }

    private static bool IsConvolution2D(string name, string source)
    {
        if (name.Contains("conv2d") || name.Contains("convolution") ||
            name.Contains("convolve"))
        {
            return true;
        }

        return source.Contains("convol") &&
               (source.Contains("kernel") || source.Contains("filter")) &&
               (source.Contains("stride") || source.Contains("padding"));
    }

    private static bool IsMaxPooling(string name, string source)
    {
        if (name.Contains("maxpool") || name.Contains("max_pool"))
        {
            return true;
        }

        return source.Contains("pool") &&
               (source.Contains("max") || source.Contains("maximum"));
    }

    private static bool IsElementWiseAdd(string name, string source)
    {
        if (name.Contains("add") || name.Contains("plus") || name.Contains("sum"))
        {
            // Make sure it's not a reduction sum
            if (name.Contains("reduce") || source.Contains("reduce"))
            {
                return false;
            }

            return source.Contains("[") && source.Contains("]") &&
                   (source.Contains("+") || source.Contains("add"));
        }

        return false;
    }

    private static bool IsElementWiseMultiply(string name, string source)
    {
        if (name.Contains("multiply") || name.Contains("mul") || name.Contains("product"))
        {
            // Exclude matrix multiply
            if (name.Contains("matrix") || name.Contains("mat"))
            {
                return false;
            }

            return source.Contains("[") && source.Contains("]") &&
                   (source.Contains("*") || source.Contains("multiply"));
        }

        return false;
    }

    private static bool IsReLU(string name, string source)
    {
        return name.Contains("relu") ||
               (source.Contains("relu") || (source.Contains("max") && source.Contains("0")));
    }

    private static bool IsSigmoid(string name, string source)
    {
        return name.Contains("sigmoid") ||
               (source.Contains("sigmoid") || (source.Contains("exp") && source.Contains("1.0")));
    }

    private static bool IsTanh(string name, string source)
    {
        return name.Contains("tanh") || source.Contains("tanh");
    }

    private static bool IsBatchNormalization(string name, string source)
    {
        return name.Contains("batchnorm") || name.Contains("batch_norm") ||
               (source.Contains("batch") && source.Contains("norm") &&
                source.Contains("mean") && source.Contains("variance"));
    }

    private static bool IsReduction(string name, string source, out MPSOperationType reductionType)
    {
        reductionType = MPSOperationType.Unknown;

        if (name.Contains("reduce") || source.Contains("reduce"))
        {
            if (name.Contains("sum") || source.Contains("sum"))
            {
                reductionType = MPSOperationType.ReduceSum;
                return true;
            }
            if (name.Contains("max") || source.Contains("max"))
            {
                reductionType = MPSOperationType.ReduceMax;
                return true;
            }
            if (name.Contains("min") || source.Contains("min"))
            {
                reductionType = MPSOperationType.ReduceMin;
                return true;
            }
        }

        return false;
    }

    private static bool IsImageConversion(string name, string source)
    {
        return (name.Contains("image") || name.Contains("img")) &&
               (name.Contains("convert") || name.Contains("transform") ||
                source.Contains("rgba") || source.Contains("bgra"));
    }

    private static bool IsGaussianBlur(string name, string source)
    {
        return (name.Contains("gaussian") && name.Contains("blur")) ||
               (name.Contains("blur") && source.Contains("gaussian")) ||
               (source.Contains("blur") && source.Contains("kernel") && source.Contains("weight"));
    }

    #endregion
}

/// <summary>
/// Extended set of MPS operation types supporting standard compute patterns.
/// </summary>
public enum MPSOperationType
{
    /// <summary>Unknown or unsupported operation</summary>
    Unknown = 0,

    // BLAS Operations (3-4x speedup)
    /// <summary>Matrix multiplication: C = alpha * (A * B) + beta * C</summary>
    MatrixMultiplication,
    /// <summary>Matrix-vector multiplication: y = alpha * (A * x) + beta * y</summary>
    MatrixVectorMultiplication,

    // CNN Operations (3-5x speedup)
    /// <summary>2D Convolution with stride and padding</summary>
    Convolution2D,
    /// <summary>2D Max pooling operation</summary>
    MaxPooling2D,

    // Element-wise Operations (2-3x speedup)
    /// <summary>Element-wise addition of arrays</summary>
    ElementWiseAdd,
    /// <summary>Element-wise multiplication of arrays</summary>
    ElementWiseMultiply,

    // Neural Network Operations (2-4x speedup)
    /// <summary>ReLU activation: y = max(0, x)</summary>
    ReLU,
    /// <summary>Sigmoid activation: y = 1 / (1 + exp(-x))</summary>
    Sigmoid,
    /// <summary>Tanh activation: y = tanh(x)</summary>
    Tanh,
    /// <summary>Batch normalization operation</summary>
    BatchNormalization,

    // Reduction Operations (2-3x speedup)
    /// <summary>Sum reduction across array</summary>
    ReduceSum,
    /// <summary>Maximum value reduction</summary>
    ReduceMax,
    /// <summary>Minimum value reduction</summary>
    ReduceMin,

    // Image Processing Operations (2-4x speedup)
    /// <summary>Image format conversion (RGBA, BGRA, etc.)</summary>
    ImageConversion,
    /// <summary>Gaussian blur filter</summary>
    GaussianBlur,

    // Legacy compatibility
    /// <summary>Matrix multiply (alias for MatrixMultiplication)</summary>
    MatrixMultiply = MatrixMultiplication,
    /// <summary>Convolution (alias for Convolution2D)</summary>
    Convolution = Convolution2D,
    /// <summary>Activation (generic, maps to ReLU)</summary>
    Activation = ReLU
}

/// <summary>
/// Performance metrics comparing MPS execution vs custom kernel execution.
/// </summary>
public sealed record MPSPerformanceMetrics
{
    /// <summary>Time taken by MPS execution</summary>
    public TimeSpan MPSExecutionTime { get; init; }

    /// <summary>Time taken by custom kernel (if measured)</summary>
    public TimeSpan CustomKernelTime { get; init; }

    /// <summary>Speedup factor (CustomTime / MPSTime)</summary>
    public double SpeedupFactor { get; init; }

    /// <summary>Whether MPS was faster than custom kernel</summary>
    public bool MPSWasFaster { get; init; }

    /// <summary>Operation type that was executed</summary>
    public MPSOperationType OperationType { get; init; }

    /// <summary>Data size processed</summary>
    public int DataSize { get; init; }

    /// <summary>Device GPU family</summary>
    public string GPUFamily { get; init; } = string.Empty;

    /// <summary>Timestamp when measurement was taken</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a performance metric from execution times.
    /// </summary>
    public static MPSPerformanceMetrics Create(
        MPSOperationType operationType,
        TimeSpan mpsTime,
        TimeSpan customTime,
        int dataSize,
        string gpuFamily)
    {
        var speedup = customTime.TotalMilliseconds / mpsTime.TotalMilliseconds;
        return new MPSPerformanceMetrics
        {
            OperationType = operationType,
            MPSExecutionTime = mpsTime,
            CustomKernelTime = customTime,
            SpeedupFactor = speedup,
            MPSWasFaster = speedup > 1.0,
            DataSize = dataSize,
            GPUFamily = gpuFamily
        };
    }
}
