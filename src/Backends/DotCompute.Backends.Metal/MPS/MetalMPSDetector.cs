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
        var name = kernel.Name?.ToUpperInvariant() ?? string.Empty;
        var source = kernel.Source ?? kernel.Code ?? string.Empty;
        var sourceLower = source.ToUpperInvariant();

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
        if (name.Contains("MATMUL", StringComparison.Ordinal) || name.Contains("MATRIXMULTIPLY", StringComparison.Ordinal) ||
            name.Contains("GEMM", StringComparison.Ordinal) || name.Contains("MATRIX_MULTIPLY", StringComparison.Ordinal))
        {
            return true;
        }

        // Check source patterns (looking for matrix multiply structure)
        return (source.Contains("MATRIX", StringComparison.Ordinal) || source.Contains("GEMM", StringComparison.Ordinal)) &&
               (source.Contains("MULTIPLY", StringComparison.Ordinal) || source.Contains("PRODUCT", StringComparison.Ordinal)) &&
               (source.Contains("FOR", StringComparison.Ordinal) || source.Contains("LOOP", StringComparison.Ordinal)) &&
               source.Contains("SUM", StringComparison.Ordinal);
    }

    private static bool IsMatrixVectorMultiply(string name, string source)
    {
        if (name.Contains("MATVEC", StringComparison.Ordinal) || name.Contains("MATRIX_VECTOR", StringComparison.Ordinal) ||
            name.Contains("GEMV", StringComparison.Ordinal))
        {
            return true;
        }

        return source.Contains("MATRIX", StringComparison.Ordinal) && source.Contains("VECTOR", StringComparison.Ordinal) &&
               source.Contains("MULTIPLY", StringComparison.Ordinal);
    }

    private static bool IsConvolution2D(string name, string source)
    {
        if (name.Contains("CONV2D", StringComparison.Ordinal) || name.Contains("CONVOLUTION", StringComparison.Ordinal) ||
            name.Contains("CONVOLVE", StringComparison.Ordinal))
        {
            return true;
        }

        return source.Contains("CONVOL", StringComparison.Ordinal) &&
               (source.Contains("KERNEL", StringComparison.Ordinal) || source.Contains("FILTER", StringComparison.Ordinal)) &&
               (source.Contains("STRIDE", StringComparison.Ordinal) || source.Contains("PADDING", StringComparison.Ordinal));
    }

    private static bool IsMaxPooling(string name, string source)
    {
        if (name.Contains("MAXPOOL", StringComparison.Ordinal) || name.Contains("MAX_POOL", StringComparison.Ordinal))
        {
            return true;
        }

        return source.Contains("POOL", StringComparison.Ordinal) &&
               (source.Contains("MAX", StringComparison.Ordinal) || source.Contains("MAXIMUM", StringComparison.Ordinal));
    }

    private static bool IsElementWiseAdd(string name, string source)
    {
        if (name.Contains("ADD", StringComparison.Ordinal) || name.Contains("PLUS", StringComparison.Ordinal) || name.Contains("SUM", StringComparison.Ordinal))
        {
            // Make sure it's not a reduction sum
            if (name.Contains("REDUCE", StringComparison.Ordinal) || source.Contains("REDUCE", StringComparison.Ordinal))
            {
                return false;
            }

            return source.Contains('[', StringComparison.Ordinal) && source.Contains(']', StringComparison.Ordinal) &&
                   (source.Contains('+', StringComparison.Ordinal) || source.Contains("ADD", StringComparison.Ordinal));
        }

        return false;
    }

    private static bool IsElementWiseMultiply(string name, string source)
    {
        if (name.Contains("MULTIPLY", StringComparison.Ordinal) || name.Contains("MUL", StringComparison.Ordinal) || name.Contains("PRODUCT", StringComparison.Ordinal))
        {
            // Exclude matrix multiply
            if (name.Contains("MATRIX", StringComparison.Ordinal) || name.Contains("MAT", StringComparison.Ordinal))
            {
                return false;
            }

            return source.Contains('[', StringComparison.Ordinal) && source.Contains(']', StringComparison.Ordinal) &&
                   (source.Contains('*', StringComparison.Ordinal) || source.Contains("MULTIPLY", StringComparison.Ordinal));
        }

        return false;
    }

    private static bool IsReLU(string name, string source)
    {
        return name.Contains("RELU", StringComparison.Ordinal) ||
               (source.Contains("RELU", StringComparison.Ordinal) || (source.Contains("MAX", StringComparison.Ordinal) && source.Contains('0', StringComparison.Ordinal)));
    }

    private static bool IsSigmoid(string name, string source)
    {
        return name.Contains("SIGMOID", StringComparison.Ordinal) ||
               (source.Contains("SIGMOID", StringComparison.Ordinal) || (source.Contains("EXP", StringComparison.Ordinal) && source.Contains("1.0", StringComparison.Ordinal)));
    }

    private static bool IsTanh(string name, string source)
    {
        return name.Contains("TANH", StringComparison.Ordinal) || source.Contains("TANH", StringComparison.Ordinal);
    }

    private static bool IsBatchNormalization(string name, string source)
    {
        return name.Contains("BATCHNORM", StringComparison.Ordinal) || name.Contains("BATCH_NORM", StringComparison.Ordinal) ||
               (source.Contains("BATCH", StringComparison.Ordinal) && source.Contains("NORM", StringComparison.Ordinal) &&
                source.Contains("MEAN", StringComparison.Ordinal) && source.Contains("VARIANCE", StringComparison.Ordinal));
    }

    private static bool IsReduction(string name, string source, out MPSOperationType reductionType)
    {
        reductionType = MPSOperationType.Unknown;

        if (name.Contains("REDUCE", StringComparison.Ordinal) || source.Contains("REDUCE", StringComparison.Ordinal))
        {
            if (name.Contains("SUM", StringComparison.Ordinal) || source.Contains("SUM", StringComparison.Ordinal))
            {
                reductionType = MPSOperationType.ReduceSum;
                return true;
            }
            if (name.Contains("MAX", StringComparison.Ordinal) || source.Contains("MAX", StringComparison.Ordinal))
            {
                reductionType = MPSOperationType.ReduceMax;
                return true;
            }
            if (name.Contains("MIN", StringComparison.Ordinal) || source.Contains("MIN", StringComparison.Ordinal))
            {
                reductionType = MPSOperationType.ReduceMin;
                return true;
            }
        }

        return false;
    }

    private static bool IsImageConversion(string name, string source)
    {
        return (name.Contains("IMAGE", StringComparison.Ordinal) || name.Contains("IMG", StringComparison.Ordinal)) &&
               (name.Contains("CONVERT", StringComparison.Ordinal) || name.Contains("TRANSFORM", StringComparison.Ordinal) ||
                source.Contains("RGBA", StringComparison.Ordinal) || source.Contains("BGRA", StringComparison.Ordinal));
    }

    private static bool IsGaussianBlur(string name, string source)
    {
        return (name.Contains("GAUSSIAN", StringComparison.Ordinal) && name.Contains("BLUR", StringComparison.Ordinal)) ||
               (name.Contains("BLUR", StringComparison.Ordinal) && source.Contains("GAUSSIAN", StringComparison.Ordinal)) ||
               (source.Contains("BLUR", StringComparison.Ordinal) && source.Contains("KERNEL", StringComparison.Ordinal) && source.Contains("WEIGHT", StringComparison.Ordinal));
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
