// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.Metal.MPS;

/// <summary>
/// Native P/Invoke wrappers for Metal Performance Shaders operations.
/// </summary>
internal static partial class MetalMPSNative
{
    private const string LibraryName = "libDotComputeMetal";

    #region MPS Capabilities

    /// <summary>
    /// Queries MPS capabilities for the given device.
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_QueryMPSCapabilities")]
    public static partial MPSCapabilities QueryMPSCapabilities(IntPtr device);

    #endregion

    #region MPS Matrix Descriptors

    /// <summary>
    /// Creates a matrix descriptor for MPS operations.
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_CreateMatrixDescriptor")]
    public static partial IntPtr CreateMatrixDescriptor(int rows, int columns, MPSDataType dataType);

    /// <summary>
    /// Releases a matrix descriptor.
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_ReleaseMatrixDescriptor")]
    public static partial void ReleaseMatrixDescriptor(IntPtr descriptor);

    #endregion

    #region MPS BLAS Operations

    /// <summary>
    /// Performs matrix multiplication using MPS: C = alpha * (A * B) + beta * C
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSMatrixMultiply")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSMatrixMultiply(
        IntPtr device,
        IntPtr matrixA, int rowsA, int colsA, [MarshalAs(UnmanagedType.Bool)] bool transposeA,
        IntPtr matrixB, int rowsB, int colsB, [MarshalAs(UnmanagedType.Bool)] bool transposeB,
        IntPtr matrixC, int rowsC, int colsC,
        float alpha, float beta);

    /// <summary>
    /// Performs matrix-vector multiplication using MPS: y = alpha * (A * x) + beta * y
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSMatrixVectorMultiply")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSMatrixVectorMultiply(
        IntPtr device,
        IntPtr matrix, int rows, int cols, [MarshalAs(UnmanagedType.Bool)] bool transpose,
        IntPtr vector, int vectorLength,
        IntPtr result, int resultLength,
        float alpha, float beta);

    #endregion

    #region MPS CNN Operations

    /// <summary>
    /// Performs 2D convolution using MPS.
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSConvolution2D")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSConvolution2D(
        IntPtr device,
        IntPtr input, int inputHeight, int inputWidth, int inputChannels,
        IntPtr kernel, int kernelHeight, int kernelWidth, int outputChannels,
        IntPtr output, int outputHeight, int outputWidth,
        int strideY, int strideX,
        int paddingY, int paddingX);

    /// <summary>
    /// Performs max pooling using MPS.
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSMaxPooling2D")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSMaxPooling2D(
        IntPtr device,
        IntPtr input, int inputHeight, int inputWidth, int channels,
        IntPtr output, int outputHeight, int outputWidth,
        int poolSizeY, int poolSizeX,
        int strideY, int strideX);

    #endregion

    #region MPS Neural Network Operations

    /// <summary>
    /// Applies ReLU activation using MPS: y = max(0, x)
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSNeuronReLU")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSNeuronReLU(
        IntPtr device,
        IntPtr input,
        IntPtr output,
        int count);

    /// <summary>
    /// Applies sigmoid activation using MPS: y = 1 / (1 + exp(-x))
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSNeuronSigmoid")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSNeuronSigmoid(
        IntPtr device,
        IntPtr input,
        IntPtr output,
        int count);

    /// <summary>
    /// Applies tanh activation using MPS: y = tanh(x)
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSNeuronTanh")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSNeuronTanh(
        IntPtr device,
        IntPtr input,
        IntPtr output,
        int count);

    /// <summary>
    /// Performs batch normalization using MPS.
    /// </summary>
    [LibraryImport(LibraryName, SetLastError = false, EntryPoint = "DCMetal_MPSBatchNormalization")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool MPSBatchNormalization(
        IntPtr device,
        IntPtr input,
        IntPtr gamma,
        IntPtr beta,
        IntPtr mean,
        IntPtr variance,
        IntPtr output,
        int count,
        int channels,
        float epsilon);

    #endregion
}

#region MPS Native Structures

/// <summary>
/// Data types supported by MPS.
/// </summary>
internal enum MPSDataType
{
    Float32 = 0x10000020,
    Float16 = 0x10000010,
    Int32 = 0x20000020,
    Int16 = 0x20000010,
    Int8 = 0x20000008
}

#endregion
