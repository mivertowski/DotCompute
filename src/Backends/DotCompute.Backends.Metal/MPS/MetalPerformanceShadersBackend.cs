// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.MPS;

/// <summary>
/// Metal Performance Shaders backend for optimized ML and linear algebra operations.
/// Provides 3x+ speedup for matrix operations compared to custom kernels.
/// </summary>
public sealed class MetalPerformanceShadersBackend : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalPerformanceShadersBackend> _logger;
    private readonly MPSCapabilities _capabilities;
    private bool _disposed;

    /// <summary>
    /// Gets the capabilities supported by MPS on this device.
    /// </summary>
    public MPSCapabilities Capabilities => _capabilities;

    public MetalPerformanceShadersBackend(IntPtr device, ILogger<MetalPerformanceShadersBackend> logger)
    {
        if (device == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(device), "Metal device handle cannot be null");
        }

        ArgumentNullException.ThrowIfNull(logger);

        _device = device;
        _logger = logger;

        // Query MPS capabilities
        _capabilities = MetalMPSNative.QueryMPSCapabilities(device);

        _logger.LogInformation("MPS Backend initialized - BLAS: {BLAS}, CNN: {CNN}, Neural: {Neural}",
            _capabilities.HasBLAS,
            _capabilities.HasCNN,
            _capabilities.HasNeuralNetwork);
    }

    /// <summary>
    /// Performs matrix multiplication: C = alpha * (A * B) + beta * C
    /// Uses MPSMatrixMultiplication for optimal performance.
    /// </summary>
    public void MatrixMultiply(
        ReadOnlySpan<float> a, int rowsA, int colsA,
        ReadOnlySpan<float> b, int rowsB, int colsB,
        Span<float> c, int rowsC, int colsC,
        float alpha = 1.0f, float beta = 0.0f,
        bool transposeA = false, bool transposeB = false)
    {
        ThrowIfDisposed();

        if (!_capabilities.HasBLAS)
        {
            throw new NotSupportedException("MPS BLAS operations not supported on this device");
        }

        // Validate dimensions
        var innerDimA = transposeA ? rowsA : colsA;
        var innerDimB = transposeB ? colsB : rowsB;
        var outerDimA = transposeA ? colsA : rowsA;
        var outerDimB = transposeB ? rowsB : colsB;

        if (innerDimA != innerDimB)
        {
            throw new ArgumentException($"Inner dimensions must match: A={innerDimA}, B={innerDimB}");
        }

        if (rowsC != outerDimA || colsC != outerDimB)
        {
            throw new ArgumentException($"Result dimensions must be [{outerDimA}x{outerDimB}], got [{rowsC}x{colsC}]");
        }

        unsafe
        {
            fixed (float* pA = a)
            fixed (float* pB = b)
            fixed (float* pC = c)
            {
                var result = MetalMPSNative.MPSMatrixMultiply(
                    _device,
                    (IntPtr)pA, rowsA, colsA, transposeA,
                    (IntPtr)pB, rowsB, colsB, transposeB,
                    (IntPtr)pC, rowsC, colsC,
                    alpha, beta);

                if (!result)
                {
                    throw new InvalidOperationException("MPS matrix multiplication failed");
                }
            }
        }

        _logger.LogTrace("MPS MatrixMultiply completed: [{RowsA}x{ColsA}] * [{RowsB}x{ColsB}] = [{RowsC}x{ColsC}]",
            rowsA, colsA, rowsB, colsB, rowsC, colsC);
    }

    /// <summary>
    /// Performs matrix-vector multiplication: y = alpha * (A * x) + beta * y
    /// Uses MPSMatrixVectorMultiplication for optimal performance.
    /// </summary>
    public void MatrixVectorMultiply(
        ReadOnlySpan<float> matrix, int rows, int cols,
        ReadOnlySpan<float> vector,
        Span<float> result,
        float alpha = 1.0f, float beta = 0.0f,
        bool transpose = false)
    {
        ThrowIfDisposed();

        if (!_capabilities.HasBLAS)
        {
            throw new NotSupportedException("MPS BLAS operations not supported on this device");
        }

        // Validate dimensions
        var expectedVectorLen = transpose ? rows : cols;
        var expectedResultLen = transpose ? cols : rows;

        if (vector.Length != expectedVectorLen)
        {
            throw new ArgumentException($"Vector length must be {expectedVectorLen}, got {vector.Length}");
        }

        if (result.Length != expectedResultLen)
        {
            throw new ArgumentException($"Result length must be {expectedResultLen}, got {result.Length}");
        }

        unsafe
        {
            fixed (float* pMatrix = matrix)
            fixed (float* pVector = vector)
            fixed (float* pResult = result)
            {
                var success = MetalMPSNative.MPSMatrixVectorMultiply(
                    _device,
                    (IntPtr)pMatrix, rows, cols, transpose,
                    (IntPtr)pVector, vector.Length,
                    (IntPtr)pResult, result.Length,
                    alpha, beta);

                if (!success)
                {
                    throw new InvalidOperationException("MPS matrix-vector multiplication failed");
                }
            }
        }

        _logger.LogTrace("MPS MatrixVectorMultiply completed: [{Rows}x{Cols}] * [{VecLen}] = [{ResLen}]",
            rows, cols, vector.Length, result.Length);
    }

    /// <summary>
    /// Performs 2D convolution operation.
    /// Uses MPSCNNConvolution for optimal performance.
    /// </summary>
    public void Convolution2D(
        ReadOnlySpan<float> input, int inputHeight, int inputWidth, int inputChannels,
        ReadOnlySpan<float> kernel, int kernelHeight, int kernelWidth, int outputChannels,
        Span<float> output, int outputHeight, int outputWidth,
        int strideY = 1, int strideX = 1,
        int paddingY = 0, int paddingX = 0)
    {
        ThrowIfDisposed();

        if (!_capabilities.HasCNN)
        {
            throw new NotSupportedException("MPS CNN operations not supported on this device");
        }

        // Validate dimensions
        var expectedInputSize = inputHeight * inputWidth * inputChannels;
        var expectedKernelSize = kernelHeight * kernelWidth * inputChannels * outputChannels;
        var expectedOutputSize = outputHeight * outputWidth * outputChannels;

        if (input.Length != expectedInputSize)
        {
            throw new ArgumentException($"Input size must be {expectedInputSize}, got {input.Length}");
        }

        if (kernel.Length != expectedKernelSize)
        {
            throw new ArgumentException($"Kernel size must be {expectedKernelSize}, got {kernel.Length}");
        }

        if (output.Length != expectedOutputSize)
        {
            throw new ArgumentException($"Output size must be {expectedOutputSize}, got {output.Length}");
        }

        unsafe
        {
            fixed (float* pInput = input)
            fixed (float* pKernel = kernel)
            fixed (float* pOutput = output)
            {
                var success = MetalMPSNative.MPSConvolution2D(
                    _device,
                    (IntPtr)pInput, inputHeight, inputWidth, inputChannels,
                    (IntPtr)pKernel, kernelHeight, kernelWidth, outputChannels,
                    (IntPtr)pOutput, outputHeight, outputWidth,
                    strideY, strideX, paddingY, paddingX);

                if (!success)
                {
                    throw new InvalidOperationException("MPS convolution failed");
                }
            }
        }

        _logger.LogTrace("MPS Convolution2D completed: [{IH}x{IW}x{IC}] * [{KH}x{KW}x{OC}] = [{OH}x{OW}x{OC}]",
            inputHeight, inputWidth, inputChannels,
            kernelHeight, kernelWidth, outputChannels,
            outputHeight, outputWidth, outputChannels);
    }

    /// <summary>
    /// Applies ReLU activation function: y = max(0, x)
    /// Uses MPSCNNNeuronReLU for optimal performance.
    /// </summary>
    public void ReLU(ReadOnlySpan<float> input, Span<float> output)
    {
        ThrowIfDisposed();

        if (!_capabilities.HasNeuralNetwork)
        {
            throw new NotSupportedException("MPS neural network operations not supported on this device");
        }

        if (input.Length != output.Length)
        {
            throw new ArgumentException("Input and output must have same length");
        }

        unsafe
        {
            fixed (float* pInput = input)
            fixed (float* pOutput = output)
            {
                var success = MetalMPSNative.MPSNeuronReLU(
                    _device,
                    (IntPtr)pInput,
                    (IntPtr)pOutput,
                    input.Length);

                if (!success)
                {
                    throw new InvalidOperationException("MPS ReLU activation failed");
                }
            }
        }

        _logger.LogTrace("MPS ReLU completed: {Count} elements", input.Length);
    }

    /// <summary>
    /// Performs batch normalization.
    /// Uses MPSCNNBatchNormalization for optimal performance.
    /// </summary>
    public void BatchNormalization(
        ReadOnlySpan<float> input,
        ReadOnlySpan<float> gamma,
        ReadOnlySpan<float> beta,
        ReadOnlySpan<float> mean,
        ReadOnlySpan<float> variance,
        Span<float> output,
        float epsilon = 1e-5f)
    {
        ThrowIfDisposed();

        if (!_capabilities.HasNeuralNetwork)
        {
            throw new NotSupportedException("MPS neural network operations not supported on this device");
        }

        if (input.Length != output.Length)
        {
            throw new ArgumentException("Input and output must have same length");
        }

        var channels = gamma.Length;
        if (beta.Length != channels || mean.Length != channels || variance.Length != channels)
        {
            throw new ArgumentException("All normalization parameters must have same channel count");
        }

        unsafe
        {
            fixed (float* pInput = input)
            fixed (float* pGamma = gamma)
            fixed (float* pBeta = beta)
            fixed (float* pMean = mean)
            fixed (float* pVariance = variance)
            fixed (float* pOutput = output)
            {
                var success = MetalMPSNative.MPSBatchNormalization(
                    _device,
                    (IntPtr)pInput,
                    (IntPtr)pGamma,
                    (IntPtr)pBeta,
                    (IntPtr)pMean,
                    (IntPtr)pVariance,
                    (IntPtr)pOutput,
                    input.Length,
                    channels,
                    epsilon);

                if (!success)
                {
                    throw new InvalidOperationException("MPS batch normalization failed");
                }
            }
        }

        _logger.LogTrace("MPS BatchNormalization completed: {Count} elements, {Channels} channels",
            input.Length, channels);
    }

    /// <summary>
    /// Determines if MPS should be used for a given operation type and size.
    /// Custom kernels may be faster for small operations.
    /// </summary>
    public static bool ShouldUseMPS(MPSOperationType operationType, int dataSize, MPSCapabilities capabilities)
    {
        // MPS has overhead, only use for larger operations
        const int MinBLASSize = 256;       // 16x16 matrix
        const int MinCNNSize = 1024;       // 32x32 image
        const int MinNeuralSize = 1024;    // 1K elements

        return operationType switch
        {
            MPSOperationType.MatrixMultiply when capabilities.HasBLAS => dataSize >= MinBLASSize,
            MPSOperationType.MatrixVectorMultiplication when capabilities.HasBLAS => dataSize >= MinBLASSize,
            MPSOperationType.Convolution when capabilities.HasCNN => dataSize >= MinCNNSize,
            MPSOperationType.Activation when capabilities.HasNeuralNetwork => dataSize >= MinNeuralSize,
            MPSOperationType.BatchNormalization when capabilities.HasNeuralNetwork => dataSize >= MinNeuralSize,
            _ => false
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogDebug("Disposing MPS Backend");
            _disposed = true;
        }
    }
}

/// <summary>
/// Capabilities supported by Metal Performance Shaders on the current device.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MPSCapabilities : IEquatable<MPSCapabilities>
{
    /// <summary>
    /// Supports BLAS operations (matrix multiply, GEMM, etc.)
    /// </summary>
    public readonly byte SupportsBLAS; // 0 = false, 1 = true

    /// <summary>
    /// Supports CNN operations (convolution, pooling, etc.)
    /// </summary>
    public readonly byte SupportsCNN; // 0 = false, 1 = true

    /// <summary>
    /// Supports neural network primitives (activation, batch norm, etc.)
    /// </summary>
    public readonly byte SupportsNeuralNetwork; // 0 = false, 1 = true

    /// <summary>
    /// Metal GPU family version (e.g., 7 for Apple7/M1, 8 for Apple8/M2)
    /// </summary>
    public readonly int GPUFamilyVersion;

    // Helper properties for bool conversion
    public bool HasBLAS => SupportsBLAS != 0;
    public bool HasCNN => SupportsCNN != 0;
    public bool HasNeuralNetwork => SupportsNeuralNetwork != 0;
    public string GPUFamily => $"Apple{GPUFamilyVersion}";

    /// <summary>
    /// Determines whether the specified <see cref="MPSCapabilities"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The instance to compare with the current instance.</param>
    /// <returns>true if the specified instance is equal to the current instance; otherwise, false.</returns>
    public bool Equals(MPSCapabilities other)
    {
        return SupportsBLAS == other.SupportsBLAS &&
               SupportsCNN == other.SupportsCNN &&
               SupportsNeuralNetwork == other.SupportsNeuralNetwork &&
               GPUFamilyVersion == other.GPUFamilyVersion;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is MPSCapabilities other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this instance.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(SupportsBLAS, SupportsCNN, SupportsNeuralNetwork, GPUFamilyVersion);
    }

    /// <summary>
    /// Determines whether two specified instances of <see cref="MPSCapabilities"/> are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if left and right are equal; otherwise, false.</returns>
    public static bool operator ==(MPSCapabilities left, MPSCapabilities right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two specified instances of <see cref="MPSCapabilities"/> are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if left and right are not equal; otherwise, false.</returns>
    public static bool operator !=(MPSCapabilities left, MPSCapabilities right)
    {
        return !(left == right);
    }
}

// MPSOperationType moved to MetalMPSDetector.cs to avoid duplication
