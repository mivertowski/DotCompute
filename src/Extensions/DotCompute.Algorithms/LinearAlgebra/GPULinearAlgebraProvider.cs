
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#nullable disable

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Algorithms.LinearAlgebra.Components;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using LinearAlgebraOp = DotCompute.Algorithms.LinearAlgebraOperation;

namespace DotCompute.Algorithms.LinearAlgebra
{

    /// <summary>
    /// Provides GPU-accelerated linear algebra operations with automatic kernel selection and optimization.
    /// This class orchestrates specialized components for matrix operations, vector operations, solvers, and optimization strategies.
    /// </summary>
    public sealed partial class GPULinearAlgebraProvider : IDisposable
    {
        private readonly ILogger<GPULinearAlgebraProvider> _logger;
        private readonly GpuMatrixOperations _matrixOps;
        private readonly GpuVectorOperations _vectorOps;
        private readonly GpuSolverOperations _solverOps;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the GPULinearAlgebraProvider.
        /// </summary>
        /// <param name="kernelManager">Kernel manager for compilation and execution.</param>
        /// <param name="logger">Logger instance.</param>
        public GPULinearAlgebraProvider(IKernelManager kernelManager, ILogger<GPULinearAlgebraProvider> logger)
        {
            _ = kernelManager ?? throw new ArgumentNullException(nameof(kernelManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize specialized components with kernel manager
            _matrixOps = new GpuMatrixOperations(kernelManager);
            _vectorOps = new GpuVectorOperations();
            _solverOps = new GpuSolverOperations(_matrixOps);
        }

        /// <summary>
        /// Performs GPU-accelerated matrix multiplication with automatic optimization.
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result matrix.</returns>
        public async Task<Matrix> MultiplyAsync(Matrix a, Matrix b, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            GpuOptimizationStrategies.ValidateMatrixMultiplication(a, b);

            var matrixProperties = GpuOptimizationStrategies.AnalyzeMatrixProperties(a, b);
            var hardwareInfo = GpuOptimizationStrategies.GetHardwareInfo(accelerator);
            var config = GpuOptimizationStrategies.GetOptimalKernelConfig(LinearAlgebraOp.MatrixMultiply, matrixProperties, hardwareInfo);

            try
            {
                return await _matrixOps.MultiplyAsync(a, b, accelerator, config, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogGpuMatrixMultiplicationFailed(ex);
                return await GpuOptimizationStrategies.FallbackMatrixMultiplyAsync(a, b, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs GPU-accelerated QR decomposition using Householder transformations.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Q and R matrices.</returns>
        public async Task<(Matrix Q, Matrix R)> QRDecompositionAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            var matrixProperties = GpuOptimizationStrategies.AnalyzeMatrixProperties(matrix);
            var hardwareInfo = GpuOptimizationStrategies.GetHardwareInfo(accelerator);

            // For small matrices, CPU might be faster due to GPU overhead
            if (matrix.Size < GpuOptimizationStrategies.GetGPUThreshold(accelerator))
            {
                return await GpuOptimizationStrategies.FallbackQRDecompositionAsync(matrix, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await _matrixOps.QRDecompositionAsync(matrix, accelerator, matrixProperties, hardwareInfo, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogGpuQrDecompositionFailed(ex);
                return await GpuOptimizationStrategies.FallbackQRDecompositionAsync(matrix, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs GPU-accelerated SVD using Jacobi iterations.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>U, S, and VT matrices.</returns>
        public async Task<(Matrix U, Matrix S, Matrix VT)> SVDAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(matrix);

            var matrixProperties = GpuOptimizationStrategies.AnalyzeMatrixProperties(matrix);
            var hardwareInfo = GpuOptimizationStrategies.GetHardwareInfo(accelerator);

            if (matrix.Size < GpuOptimizationStrategies.GetGPUThreshold(accelerator))
            {
                return await GpuOptimizationStrategies.FallbackSVDAsync(matrix, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return await _matrixOps.SVDAsync(matrix, accelerator, matrixProperties, hardwareInfo, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogGpuSvdFailed(ex);
                return await GpuOptimizationStrategies.FallbackSVDAsync(matrix, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Solves a linear system using GPU-accelerated methods.
        /// </summary>
        /// <param name="a">Coefficient matrix.</param>
        /// <param name="b">Right-hand side.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="method">Solution method.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector.</returns>
        public async Task<Matrix> SolveAsync(Matrix a, Matrix b, IAccelerator accelerator, LinearSystemSolver method = LinearSystemSolver.Auto, CancellationToken cancellationToken = default)
        {
            GpuOptimizationStrategies.ValidateLinearSystem(a, b);

            var matrixProperties = GpuOptimizationStrategies.AnalyzeMatrixProperties(a);
            var selectedMethod = method == LinearSystemSolver.Auto ? GpuOptimizationStrategies.SelectOptimalSolver(matrixProperties) : method;

            try
            {
                return selectedMethod switch
                {
                    LinearSystemSolver.LU => await GpuSolverOperations.SolveLUAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false),
                    LinearSystemSolver.Cholesky => await GpuSolverOperations.SolveCholeskyAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false),
                    LinearSystemSolver.QR => await _solverOps.SolveQRAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false),
                    LinearSystemSolver.ConjugateGradient => await _solverOps.SolveIterativeAsync(a, b, accelerator, cancellationToken).ConfigureAwait(false),
                    _ => throw new ArgumentException($"Unknown solver method: {selectedMethod}")
                };
            }
            catch (Exception ex)
            {
                LogGpuLinearSystemSolverFailed(ex);
                return await GpuOptimizationStrategies.FallbackSolveAsync(a, b, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Disposes resources used by the provider.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _matrixOps?.Dispose();
                _vectorOps?.Dispose();
                _solverOps?.Dispose();
                _disposed = true;
            }
        }

        #region LoggerMessage Delegates

        [LoggerMessage(Level = LogLevel.Warning, Message = "GPU matrix multiplication failed, falling back to CPU")]
        private partial void LogGpuMatrixMultiplicationFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "GPU QR decomposition failed, falling back to CPU")]
        private partial void LogGpuQrDecompositionFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "GPU SVD failed, falling back to CPU")]
        private partial void LogGpuSvdFailed(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "GPU linear system solver failed, falling back to CPU")]
        private partial void LogGpuLinearSystemSolverFailed(Exception ex);

        #endregion
    }

    #region Supporting Enums

    /// <summary>
    /// Available linear system solvers.
    /// </summary>
    public enum LinearSystemSolver
    {
        Auto,
        LU,
        Cholesky,
        QR,
        ConjugateGradient
    }

    #endregion


} // namespace DotCompute.Algorithms.LinearAlgebra
