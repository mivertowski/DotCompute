// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Kernels.LinearAlgebra;

namespace DotCompute.Algorithms.Kernels;

/// <summary>
/// Advanced GPU kernels for specialized linear algebra operations.
/// This class serves as a unified entry point for all advanced linear algebra kernels.
///
/// Individual kernel implementations are organized into specialized classes:
/// - <see cref="SparseMatrixKernels"/> for sparse matrix operations (CSR format)
/// - <see cref="LAPACKKernels"/> for iterative solvers (CG, BiCGSTAB)
/// - <see cref="EigenKernels"/> for eigenvalue computations (power method, inverse power method)
/// - <see cref="BLASKernels"/> for basic linear algebra operations (GEMM, Tensor Core operations)
/// - <see cref="DecompositionKernels"/> for matrix decompositions (QR, LU, Cholesky)
/// - <see cref="AdvancedKernelTypes"/> for supporting types and enumerations
/// </summary>
public static class AdvancedLinearAlgebraKernels
{
    #region Unified Access to Specialized Kernels

    /// <summary>
    /// Gets all sparse matrix operation kernels.
    /// </summary>
    public static class SparseMatrix => SparseMatrixKernels;

    /// <summary>
    /// Gets all LAPACK-style iterative solver kernels.
    /// </summary>
    public static class Solvers => LAPACKKernels;

    /// <summary>
    /// Gets all eigenvalue computation kernels.
    /// </summary>
    public static class Eigenvalues => EigenKernels;

    /// <summary>
    /// Gets all BLAS operation kernels.
    /// </summary>
    public static class BLAS => BLASKernels;

    /// <summary>
    /// Gets all matrix decomposition kernels.
    /// </summary>
    public static class Decomposition => DecompositionKernels;

    #endregion

    #region Kernel Selection Helper

    /// <summary>
    /// Selects the optimal kernel based on operation type and hardware capabilities.
    /// </summary>
    /// <param name="operation">The type of operation to perform.</param>
    /// <param name="matrixProps">Properties of the input matrices.</param>
    /// <param name="hardwareInfo">Information about the target hardware.</param>
    /// <returns>Configuration for the optimal kernel to use.</returns>
    public static KernelConfiguration SelectOptimalKernel(
        AdvancedLinearAlgebraOperation operation,
        MatrixProperties matrixProps,
        HardwareInfo hardwareInfo)
    {
        var config = new KernelConfiguration();

        // Configure based on operation type
        switch (operation)
        {
            case AdvancedLinearAlgebraOperation.SparseMatrixVector:
                config.UseSpecializedSparseKernels = matrixProps.SparsityRatio > 0.5f;
                config.OptimalBlockSize = 32;
                break;

            case AdvancedLinearAlgebraOperation.TensorCoreGEMM:
                config.UseTensorCores = hardwareInfo.SupportsTensorCores;
                config.Precision = "mixed";
                config.TileSize = 16;
                break;

            case AdvancedLinearAlgebraOperation.ConjugateGradient:
                config.UseMemoryTiling = matrixProps.Size > 1000000;
                config.OptimalBlockSize = 64;
                break;

            case AdvancedLinearAlgebraOperation.PowerMethod:
                config.Precision = matrixProps.RequiresHighPrecision ? "double" : "single";
                config.OptimalBlockSize = 128;
                break;

            case AdvancedLinearAlgebraOperation.ParallelQR:
            case AdvancedLinearAlgebraOperation.ParallelCholesky:
            case AdvancedLinearAlgebraOperation.AtomicLU:
                config.UseMemoryTiling = true;
                config.OptimalBlockSize = hardwareInfo.WarpSize ?? 32;
                break;

            default:
                // Use default configuration
                break;
        }

        // Apply hardware-specific optimizations
        if (hardwareInfo.SupportsTensorCores && matrixProps.Size > 256)
        {
            config.UseTensorCores = true;
            config.TileSize = 16;
        }

        if (hardwareInfo.SupportsDoublePrecision && matrixProps.RequiresHighPrecision)
        {
            config.Precision = "double";
        }

        return config;
    }

    /// <summary>
    /// Estimates the optimal block size for tiled operations based on hardware characteristics.
    /// </summary>
    /// <param name="hardwareInfo">Hardware information.</param>
    /// <param name="matrixSize">Size of the matrix being processed.</param>
    /// <returns>Recommended block size.</returns>
    public static int EstimateOptimalBlockSize(HardwareInfo hardwareInfo, long matrixSize)
    {
        // Base block size on warp size and memory hierarchy
        var baseBlockSize = hardwareInfo.WarpSize ?? 32;

        if (hardwareInfo.GlobalMemorySize > 0)
        {
            // Adjust based on available memory
            var memoryPerBlock = hardwareInfo.GlobalMemorySize / 1024; // Conservative estimate
            if (memoryPerBlock < 1024 * 1024) // Less than 1MB per block
            {
                baseBlockSize = Math.Min(baseBlockSize, 16);
            }
        }

        // Adjust based on matrix size
        if (matrixSize < 1000)
        {
            baseBlockSize = Math.Min(baseBlockSize, 16);
        }
        else if (matrixSize > 100000)
        {
            baseBlockSize = Math.Max(baseBlockSize, 64);
        }

        return baseBlockSize;
    }

    #endregion

    #region Legacy Compatibility

    // For backward compatibility, expose direct kernel strings from specialized classes

    /// <summary>
    /// OpenCL kernel for Compressed Sparse Row (CSR) matrix-vector multiplication.
    /// </summary>
    [Obsolete("Use SparseMatrixKernels.OpenCLSparseMatrixVectorKernel instead")]
    public static string OpenCLSparseMatrixVectorKernel => SparseMatrixKernels.OpenCLSparseMatrixVectorKernel;

    /// <summary>
    /// CUDA kernel for sparse matrix operations with warp-level optimizations.
    /// </summary>
    [Obsolete("Use SparseMatrixKernels.CUDASparseMatrixKernel instead")]
    public static string CUDASparseMatrixKernel => SparseMatrixKernels.CUDASparseMatrixKernel;

    /// <summary>
    /// OpenCL kernel for Conjugate Gradient iteration.
    /// </summary>
    [Obsolete("Use LAPACKKernels.OpenCLConjugateGradientKernel instead")]
    public static string OpenCLConjugateGradientKernel => LAPACKKernels.OpenCLConjugateGradientKernel;

    /// <summary>
    /// CUDA kernel for BiCGSTAB iteration with preconditioning.
    /// </summary>
    [Obsolete("Use LAPACKKernels.CUDABiCGSTABKernel instead")]
    public static string CUDABiCGSTABKernel => LAPACKKernels.CUDABiCGSTABKernel;

    #endregion
}