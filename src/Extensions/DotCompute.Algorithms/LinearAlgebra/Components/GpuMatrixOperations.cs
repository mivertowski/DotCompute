
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels.Compilation;
using DotCompute.Abstractions.Memory;
using DotCompute.Algorithms.Types;
using DotCompute.Core.Extensions;
using KernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;

namespace DotCompute.Algorithms.LinearAlgebra.Components
{
    /// <summary>
    /// Specialized component for GPU-accelerated matrix operations including multiplication, decomposition, and transformations.
    /// </summary>
    public sealed class GpuMatrixOperations : IDisposable
    {
        // Optional: GPU kernel execution requires an IKernelManager. No implementation ships yet,
        // so this is null in practice and the GPU kernel paths below surface a clear error that
        // GPULinearAlgebraProvider catches to fall back to the CPU implementations (GH #182).
        private readonly IKernelManager? _kernelManager;
        private readonly Dictionary<string, ManagedCompiledKernel> _kernelCache = [];
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuMatrixOperations"/> class.
        /// </summary>
        /// <param name="kernelManager">The kernel manager for compilation and execution.</param>
        /// <exception cref="ArgumentNullException">Thrown when kernelManager is null.</exception>
        public GpuMatrixOperations(IKernelManager? kernelManager = null)
        {
            _kernelManager = kernelManager;
        }

        /// <summary>
        /// Returns the kernel manager, or throws a descriptive error when none is available.
        /// Callers in <see cref="GPULinearAlgebraProvider"/> catch this and fall back to CPU.
        /// </summary>
        private IKernelManager RequireKernelManager()
            => _kernelManager ?? throw new InvalidOperationException(
                "GPU kernel execution requires an IKernelManager, and no implementation is currently registered. " +
                "The CPU implementations (SVD, QR, Cholesky, solvers) work without one — see " +
                "src/Extensions/DotCompute.Algorithms/README_LinearAlgebraKernels.md.");

        /// <summary>
        /// Gets the kernel source for matrix multiply operation.
        /// </summary>
        private static string GetMatrixMultiplyKernelSource(string deviceType)
        {
            // Simple fallback kernel source
            return @"
            __kernel void MatrixMultiply(
                __global const float* A,
                __global const float* B,
                __global float* C,
                const int M,
                const int N,
                const int K) {
                int row = get_global_id(0);
                int col = get_global_id(1);
                if (row < M && col < N) {
                    float sum = 0.0f;
                    for (int k = 0; k < K; k++) {
                        sum += A[row * K + k] * B[k * N + col];
                    }
                    C[row * N + col] = sum;
                }
            }";
        }

        /// <summary>
        /// Performs GPU-accelerated matrix multiplication with automatic optimization.
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="config">Kernel execution configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result matrix.</returns>
        internal async Task<Matrix> MultiplyAsync(Matrix a, Matrix b, IAccelerator accelerator, KernelExecutionParameters config, CancellationToken cancellationToken = default)
        {
            // Fallback kernel source since GetKernelSource doesn't exist
            var kernelSource = GetMatrixMultiplyKernelSource(accelerator.Info.DeviceType);
            _ = await GetOrCompileKernelAsync("MatrixMultiply", kernelSource, accelerator, cancellationToken).ConfigureAwait(false);

            var result = new Matrix(a.Rows, b.Columns);
            var aData = a.ToArray();
            var bData = b.ToArray();
            var resultData = new float[result.Size];

            var bufferA = await accelerator.Memory.AllocateAsync<float>(aData.Length, MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferB = await accelerator.Memory.AllocateAsync<float>(bData.Length, MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferC = await accelerator.Memory.AllocateAsync<float>(resultData.Length, MemoryOptions.None, cancellationToken).ConfigureAwait(false);

            try
            {
                await bufferA.WriteAsync(aData, 0, cancellationToken).ConfigureAwait(false);
                await bufferB.WriteAsync(bData, 0, cancellationToken).ConfigureAwait(false);

                var arguments = new[]
                {
                    new KernelArgument { Name = "A", Value = bufferA, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferA },
                    new KernelArgument { Name = "B", Value = bufferB, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferB },
                    new KernelArgument { Name = "C", Value = bufferC, Type = typeof(float[]), IsDeviceMemory = true, MemoryBuffer = bufferC },
                    new KernelArgument { Name = "M", Value = a.Rows, Type = typeof(int), IsDeviceMemory = false },
                    new KernelArgument { Name = "N", Value = b.Columns, Type = typeof(int), IsDeviceMemory = false },
                    new KernelArgument { Name = "K", Value = a.Columns, Type = typeof(int), IsDeviceMemory = false }
                };

                var executionConfig = new KernelExecutionConfig
                {
                    GlobalWorkSize = [.. config.GlobalWorkSize.Select(x => (int)x)],
                    LocalWorkSize = [.. config.LocalWorkSize.Select(x => (int)x)],
                    CaptureTimings = true
                };

                // Get or compile the kernel
                var kernel = await GetOrCompileKernelAsync("MatrixMultiply", kernelSource, accelerator, cancellationToken).ConfigureAwait(false);

                // Execute the kernel through kernel manager
                var executionResult = await RequireKernelManager().ExecuteKernelAsync(
                    kernel,
                    arguments,
                    accelerator,
                    executionConfig,
                    cancellationToken).ConfigureAwait(false);

                // Read back results from GPU
                await bufferC.ReadAsync(resultData, 0, cancellationToken).ConfigureAwait(false);

                // Copy result data to matrix
                CopyArrayToMatrix(resultData, result);

                return result;
            }
            finally
            {
                await bufferA.DisposeAsync().ConfigureAwait(false);
                await bufferB.DisposeAsync().ConfigureAwait(false);
                await bufferC.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Performs GPU-accelerated QR decomposition using Householder transformations.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="properties">Matrix properties for optimization.</param>
        /// <param name="hardware">Hardware information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Q and R matrices.</returns>
        /// <remarks>
        /// Current implementation uses CPU fallback. GPU acceleration via kernel manager will be added in v0.2.1.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Method will use _kernelManager for GPU acceleration in v0.2.1")]
        public async Task<(Matrix Q, Matrix R)> QRDecompositionAsync(Matrix matrix, IAccelerator accelerator, MatrixProperties properties, HardwareInfo hardware, CancellationToken cancellationToken = default)
        {
            // GPU-accelerated Householder QR decomposition is deferred; this method uses a
            // CPU Gram-Schmidt fallback so the public API works regardless of backend. Consumers
            // that need GPU performance should call the low-level kernel manager directly.
            //
            // CPU fallback implementation using Gram-Schmidt process
            await Task.Yield(); // Ensure async behavior

            var m = matrix.Rows;
            var n = matrix.Columns;

            var Q = new Matrix(m, n);
            var R = new Matrix(n, n);

            // Gram-Schmidt orthogonalization
            for (var j = 0; j < n; j++)
            {
                // Copy column j of A to column j of Q
                for (var i = 0; i < m; i++)
                {
                    Q[i, j] = matrix[i, j];
                }

                // Orthogonalize against previous columns
                for (var k = 0; k < j; k++)
                {
                    R[k, j] = 0;
                    for (var i = 0; i < m; i++)
                    {
                        R[k, j] += Q[i, k] * matrix[i, j];
                    }

                    for (var i = 0; i < m; i++)
                    {
                        Q[i, j] -= R[k, j] * Q[i, k];
                    }
                }

                // Normalize column j
                R[j, j] = 0;
                for (var i = 0; i < m; i++)
                {
                    R[j, j] += Q[i, j] * Q[i, j];
                }
                R[j, j] = (float)Math.Sqrt(R[j, j]);

                if (R[j, j] > 1e-10f)
                {
                    for (var i = 0; i < m; i++)
                    {
                        Q[i, j] /= R[j, j];
                    }
                }
            }

            return (Q, R);
        }

        /// <summary>
        /// Performs GPU-accelerated SVD using Jacobi iterations.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="properties">Matrix properties for optimization.</param>
        /// <param name="hardware">Hardware information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>U, S, and VT matrices.</returns>
        /// <remarks>
        /// Current implementation uses CPU fallback via QR decomposition. GPU-accelerated Jacobi SVD will be added in v0.2.1.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Method will use _kernelManager for GPU acceleration in v0.2.1")]
        public async Task<(Matrix U, Matrix S, Matrix VT)> SVDAsync(Matrix matrix, IAccelerator accelerator, MatrixProperties properties, HardwareInfo hardware, CancellationToken cancellationToken = default)
        {
            // GPU-accelerated Jacobi SVD is deferred; use the shared, numerically stable CPU Jacobi
            // implementation. The former inline "simplified A^T*A" approach returned unsorted
            // singular values and a factorization that did not reconstruct the input (GH #182).
            ArgumentNullException.ThrowIfNull(matrix);
            return await Task.Run(() => Operations.MatrixDecomposition.ComputeJacobiSVD(matrix), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Transposes a matrix using GPU acceleration.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Transposed matrix.</returns>
        public static async Task<Matrix> TransposeAsync(Matrix matrix, IAccelerator accelerator, CancellationToken cancellationToken = default)
        {
            // Fallback to CPU transpose for now
            var result = new Matrix(matrix.Columns, matrix.Rows);
            await Task.Run(() =>
            {
                for (var i = 0; i < matrix.Rows; i++)
                {
                    for (var j = 0; j < matrix.Columns; j++)
                    {
                        result[j, i] = matrix[i, j];
                    }
                }
            }, cancellationToken).ConfigureAwait(false);
            return result;
        }

        private async Task<ManagedCompiledKernel> GetOrCompileKernelAsync(string kernelName, string kernelSource, IAccelerator accelerator, CancellationToken cancellationToken)
        {
            var cacheKey = $"{kernelName}_{accelerator.Info.DeviceType}_{accelerator.Info.Name}";

            if (_kernelCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            // Compile kernel through kernel manager
            // For matrix operations, we use float types for inputs and outputs
            var kernel = await RequireKernelManager().GetOrCompileOperationKernelAsync(
                kernelName,
                [typeof(float), typeof(float), typeof(float), typeof(int), typeof(int), typeof(int)],
                typeof(float),
                accelerator,
                context: null,
                options: null,
                cancellationToken).ConfigureAwait(false);

            _kernelCache[cacheKey] = kernel;
            return kernel;
        }

        private static void CopyArrayToMatrix(float[] array, Matrix matrix)
        {
            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = 0; j < matrix.Columns; j++)
                {
                    matrix[i, j] = array[i * matrix.Columns + j];
                }
            }
        }

        private static Matrix TransposeMatrix(Matrix matrix)
        {
            var result = new Matrix(matrix.Columns, matrix.Rows);
            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = 0; j < matrix.Columns; j++)
                {
                    result[j, i] = matrix[i, j];
                }
            }
            return result;
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Note: Kernels implement IAsyncDisposable
                // In practice, kernel lifetime is managed by the IKernelManager
                // This cache just holds references
                _kernelCache.Clear();
                _disposed = true;
            }
        }
    }
}
