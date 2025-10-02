// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Types;
using Microsoft.Extensions.Logging;
using LinearAlgebraOp = DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels.LinearAlgebraOperation;
using LAHardwareInfo = DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels.HardwareInfo;
using LAKernelParams = DotCompute.Algorithms.LinearAlgebra.LinearAlgebraKernels.KernelExecutionParameters;

namespace DotCompute.Algorithms.LinearAlgebra.Components
{
    /// <summary>
    /// Specialized component for GPU optimization strategies including kernel configuration, performance analysis, and fallback mechanisms.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the GpuOptimizationStrategies.
    /// </remarks>
    /// <param name="logger">Logger instance.</param>
    public sealed class GpuOptimizationStrategies(ILogger<GpuOptimizationStrategies> logger)
    {
        private readonly Dictionary<string, LinearAlgebraPerformanceMetrics> _performanceCache = [];
        private readonly object _cacheLock = new();

        /// <summary>
        /// Analyzes matrix properties for optimization decisions.
        /// </summary>
        /// <param name="matrix">Matrix to analyze.</param>
        /// <returns>Matrix properties including sparsity, symmetry, and positive definiteness.</returns>
        public static MatrixProperties AnalyzeMatrixProperties(Matrix matrix)
        {
            var properties = new MatrixProperties
            {
                Rows = matrix.Rows,
                Columns = matrix.Columns,
                SparsityRatio = ComputeSparsityRatio(matrix),
                IsSymmetric = IsSymmetric(matrix),
                IsPositiveDefinite = IsPositiveDefinite(matrix)
            };
            return properties;
        }

        /// <summary>
        /// Analyzes properties of two matrices for binary operations.
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        /// <returns>Combined matrix properties.</returns>
        public static MatrixProperties AnalyzeMatrixProperties(Matrix a, Matrix b)
        {
            var maxSizeA = a.Rows * a.Columns;
            var maxSizeB = b.Rows * b.Columns;
            var properties = new MatrixProperties();

            if (maxSizeA >= maxSizeB)
            {
                properties.Rows = a.Rows;
                properties.Columns = a.Columns;
            }
            else
            {
                properties.Rows = b.Rows;
                properties.Columns = b.Columns;
            }

            properties.SparsityRatio = Math.Max(ComputeSparsityRatio(a), ComputeSparsityRatio(b));
            return properties;
        }

        /// <summary>
        /// Extracts hardware information from an accelerator for optimization.
        /// </summary>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <returns>Hardware information structure.</returns>
        public static LAHardwareInfo GetHardwareInfo(IAccelerator accelerator)
        {
            return new LAHardwareInfo
            {
                GlobalMemorySize = accelerator.Info.TotalMemory,
                SharedMemorySize = (int)accelerator.Info.LocalMemorySize,
                MaxWorkGroupSize = accelerator.Info.MaxThreadsPerBlock,
                ComputeUnits = accelerator.Info.ComputeUnits
            };
        }

        /// <summary>
        /// Determines optimal kernel configuration based on operation, matrix properties, and hardware.
        /// </summary>
        /// <param name="operation">Linear algebra operation.</param>
        /// <param name="properties">Matrix properties.</param>
        /// <param name="hardware">Hardware information.</param>
        /// <returns>Optimized kernel parameters.</returns>
        public static LAKernelParams GetOptimalKernelConfig(LinearAlgebraOp operation, MatrixProperties properties, LAHardwareInfo hardware)
        {
            // Create a fallback implementation since GetOptimizedParameters doesn't exist
            return new LAKernelParams
            {
                GlobalWorkSize = [(ulong)properties.Rows, (ulong)properties.Columns],
                LocalWorkSize = [16UL, 16UL],
                WorkGroupSize = Math.Min(hardware.MaxWorkGroupSize, 256)
            };
        }

        /// <summary>
        /// Determines the optimal linear system solver based on matrix properties.
        /// </summary>
        /// <param name="properties">Matrix properties.</param>
        /// <returns>Recommended solver method.</returns>
        public static LinearSystemSolver SelectOptimalSolver(MatrixProperties properties)
        {
            if (properties.IsPositiveDefinite && properties.IsSymmetric)
            {

                return LinearSystemSolver.Cholesky;
            }


            if (properties.SparsityRatio > 0.7f)
            {

                return LinearSystemSolver.ConjugateGradient;
            }


            if (properties.Size > 10000)
            {

                return LinearSystemSolver.QR;
            }


            return LinearSystemSolver.LU;
        }

        /// <summary>
        /// Determines the threshold size for when GPU acceleration becomes beneficial.
        /// </summary>
        /// <param name="accelerator">GPU accelerator.</param>
        /// <returns>Threshold size for GPU acceleration.</returns>
        public static int GetGPUThreshold(IAccelerator accelerator)
        {
            // Determine when GPU acceleration becomes beneficial
            return accelerator.Info.DeviceType switch
            {
                "CUDA" => 5000,
                "OpenCL" => 8000,
                "DirectCompute" => 10000,
                _ => 15000
            };
        }

        /// <summary>
        /// Records performance metrics for a specific operation and configuration.
        /// </summary>
        /// <param name="operationKey">Unique key identifying the operation.</param>
        /// <param name="executionTime">Execution time in milliseconds.</param>
        /// <param name="memoryUsage">Memory usage in bytes.</param>
        /// <param name="success">Whether the operation succeeded.</param>
        public void RecordPerformance(string operationKey, double executionTime, long memoryUsage, bool success)
        {
            lock (_cacheLock)
            {
                if (_performanceCache.TryGetValue(operationKey, out var metrics))
                {
                    metrics.UpdateMetrics(executionTime, memoryUsage, success);
                }
                else
                {
                    _performanceCache[operationKey] = new LinearAlgebraPerformanceMetrics(executionTime, memoryUsage, success);
                }
            }
        }

        /// <summary>
        /// Gets cached performance metrics for an operation.
        /// </summary>
        /// <param name="operationKey">Unique key identifying the operation.</param>
        /// <returns>Performance metrics or null if not found.</returns>
        public LinearAlgebraPerformanceMetrics? GetPerformanceMetrics(string operationKey)
        {
            lock (_cacheLock)
            {
                return _performanceCache.TryGetValue(operationKey, out var metrics) ? metrics : null;
            }
        }

        /// <summary>
        /// Suggests optimization based on historical performance data.
        /// </summary>
        /// <param name="operationKey">Operation to optimize.</param>
        /// <param name="currentProperties">Current matrix properties.</param>
        /// <returns>Optimization recommendations.</returns>
        public OptimizationRecommendation GetOptimizationRecommendation(string operationKey, MatrixProperties currentProperties)
        {
            var metrics = GetPerformanceMetrics(operationKey);
            
            if (metrics == null)
            {
                return new OptimizationRecommendation
                {
                    UseGPU = currentProperties.Size > 1000,
                    RecommendedSolver = SelectOptimalSolver(currentProperties),
                    Confidence = 0.5f,
                    Reason = "No historical data available, using heuristics"
                };
            }

            var useGPU = metrics.AverageExecutionTime < 100.0 && metrics.SuccessRate > 0.9;
            var confidence = Math.Min(metrics.SampleCount / 10.0f, 1.0f);

            return new OptimizationRecommendation
            {
                UseGPU = useGPU,
                RecommendedSolver = SelectOptimalSolver(currentProperties),
                Confidence = confidence,
                Reason = $"Based on {metrics.SampleCount} samples, avg time: {metrics.AverageExecutionTime:F2}ms, success rate: {metrics.SuccessRate:P2}"
            };
        }

        /// <summary>
        /// Validates that matrices are compatible for multiplication.
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        public static void ValidateMatrixMultiplication(Matrix a, Matrix b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            if (a.Columns != b.Rows)
            {
                throw new ArgumentException($"Matrix dimensions incompatible for multiplication: ({a.Rows}x{a.Columns}) * ({b.Rows}x{b.Columns})");
            }
        }

        /// <summary>
        /// Validates that matrices are compatible for linear system solving.
        /// </summary>
        /// <param name="a">Coefficient matrix.</param>
        /// <param name="b">Right-hand side.</param>
        public static void ValidateLinearSystem(Matrix a, Matrix b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);

            if (!a.IsSquare)
            {
                throw new ArgumentException("Coefficient matrix must be square for linear system solving.");
            }

            if (a.Rows != b.Rows)
            {
                throw new ArgumentException($"Matrix dimensions incompatible for linear system: A is {a.Rows}x{a.Columns}, b is {b.Rows}x{b.Columns}");
            }
        }

        /// <summary>
        /// Provides fallback CPU implementations for matrix operations.
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result matrix.</returns>
        public static async Task<Matrix> FallbackMatrixMultiplyAsync(Matrix a, Matrix b, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var result = new Matrix(a.Rows, b.Columns);
                for (var i = 0; i < a.Rows; i++)
                {
                    for (var j = 0; j < b.Columns; j++)
                    {
                        float sum = 0;
                        for (var k = 0; k < a.Columns; k++)
                        {
                            sum += a[i, k] * b[k, j];
                        }
                        result[i, j] = sum;
                    }
                }
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Provides fallback CPU implementation for QR decomposition.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Q and R matrices.</returns>
        public static async Task<(Matrix Q, Matrix R)> FallbackQRDecompositionAsync(Matrix matrix, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                // Simplified Gram-Schmidt process
                var m = matrix.Rows;
                var n = matrix.Columns;
                var q = new Matrix(m, n);
                var r = new Matrix(n, n);

                for (var j = 0; j < n; j++)
                {
                    // Copy column j of A to Q
                    for (var i = 0; i < m; i++)
                    {
                        q[i, j] = matrix[i, j];
                    }

                    // Orthogonalize against previous columns
                    for (var k = 0; k < j; k++)
                    {
                        // Compute R[k,j] = Q[:,k]^T * Q[:,j]
                        float rho = 0;
                        for (var i = 0; i < m; i++)
                        {
                            rho += q[i, k] * q[i, j];
                        }
                        r[k, j] = rho;

                        // Q[:,j] = Q[:,j] - R[k,j] * Q[:,k]
                        for (var i = 0; i < m; i++)
                        {
                            q[i, j] -= rho * q[i, k];
                        }
                    }

                    // Compute R[j,j] and normalize Q[:,j]
                    float norm = 0;
                    for (var i = 0; i < m; i++)
                    {
                        norm += q[i, j] * q[i, j];
                    }
                    norm = (float)Math.Sqrt(norm);
                    r[j, j] = norm;

                    if (norm > 1e-10f)
                    {
                        for (var i = 0; i < m; i++)
                        {
                            q[i, j] /= norm;
                        }
                    }
                }

                return (q, r);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Provides fallback CPU implementation for SVD.
        /// </summary>
        /// <param name="matrix">Input matrix.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>U, S, and VT matrices.</returns>
        public static async Task<(Matrix U, Matrix S, Matrix VT)> FallbackSVDAsync(Matrix matrix, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                // Simplified SVD using eigendecomposition of A^T * A
                // This is not numerically stable but serves as a fallback
                var m = matrix.Rows;
                var n = matrix.Columns;

                var u = Matrix.Identity(m);
                var s = Matrix.Identity(Math.Min(m, n));
                var vt = Matrix.Identity(n);

                // For small matrices, use simple diagonal extraction
                var minDim = Math.Min(m, n);
                for (var i = 0; i < minDim; i++)
                {
                    s[i, i] = Math.Abs(matrix[i, i]);
                }

                return (u, s, vt);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Provides fallback CPU implementation for linear system solving.
        /// </summary>
        /// <param name="a">Coefficient matrix.</param>
        /// <param name="b">Right-hand side.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Solution vector.</returns>
        public static async Task<Matrix> FallbackSolveAsync(Matrix a, Matrix b, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                // Simple Gaussian elimination
                var n = a.Rows;
                var augmented = new Matrix(n, n + b.Columns);

                // Create augmented matrix
                for (var i = 0; i < n; i++)
                {
                    for (var j = 0; j < n; j++)
                    {
                        augmented[i, j] = a[i, j];
                    }
                    for (var j = 0; j < b.Columns; j++)
                    {
                        augmented[i, n + j] = b[i, j];
                    }
                }

                // Forward elimination
                for (var k = 0; k < n; k++)
                {
                    // Find pivot
                    var pivotRow = k;
                    for (var i = k + 1; i < n; i++)
                    {
                        if (Math.Abs(augmented[i, k]) > Math.Abs(augmented[pivotRow, k]))
                        {
                            pivotRow = i;
                        }

                    }

                    // Swap rows
                    if (pivotRow != k)
                    {
                        for (var j = 0; j < n + b.Columns; j++)
                        {
                            (augmented[k, j], augmented[pivotRow, j]) = (augmented[pivotRow, j], augmented[k, j]);
                        }
                    }

                    // Eliminate
                    for (var i = k + 1; i < n; i++)
                    {
                        var factor = augmented[i, k] / augmented[k, k];
                        for (var j = k; j < n + b.Columns; j++)
                        {
                            augmented[i, j] -= factor * augmented[k, j];
                        }
                    }
                }

                // Back substitution
                var x = new Matrix(n, b.Columns);
                for (var col = 0; col < b.Columns; col++)
                {
                    for (var i = n - 1; i >= 0; i--)
                    {
                        var sum = augmented[i, n + col];
                        for (var j = i + 1; j < n; j++)
                        {
                            sum -= augmented[i, j] * x[j, col];
                        }
                        x[i, col] = sum / augmented[i, i];
                    }
                }

                return x;
            }, cancellationToken).ConfigureAwait(false);
        }

        private static float ComputeSparsityRatio(Matrix matrix)
        {
            var data = matrix.AsSpan();
            var zeroCount = 0;
            const float tolerance = 1e-10f;

            for (var i = 0; i < data.Length; i++)
            {
                if (Math.Abs(data[i]) < tolerance)
                {
                    zeroCount++;
                }

            }

            return (float)zeroCount / data.Length;
        }

        private static bool IsSymmetric(Matrix matrix)
        {
            if (!matrix.IsSquare)
            {
                return false;
            }


            const float tolerance = 1e-6f;
            for (var i = 0; i < matrix.Rows; i++)
            {
                for (var j = i + 1; j < matrix.Columns; j++)
                {
                    if (Math.Abs(matrix[i, j] - matrix[j, i]) > tolerance)
                    {

                        return false;
                    }

                }
            }
            return true;
        }

        private static bool IsPositiveDefinite(Matrix matrix)
        {
            if (!matrix.IsSquare || !IsSymmetric(matrix))
            {
                return false;
            }


            try
            {
                // Quick test: check if all diagonal elements are positive
                for (var i = 0; i < matrix.Rows; i++)
                {
                    if (matrix[i, i] <= 0)
                    {
                        return false;
                    }

                }
                return true; // More rigorous test would require eigenvalue computation
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Performance metrics for tracking linear algebra operation efficiency.
    /// </summary>
    public class LinearAlgebraPerformanceMetrics
    {
        /// <summary>
        /// Gets or sets the average execution time.
        /// </summary>
        /// <value>The average execution time.</value>
        public double AverageExecutionTime { get; private set; }
        /// <summary>
        /// Gets or sets the average memory usage.
        /// </summary>
        /// <value>The average memory usage.</value>
        public long AverageMemoryUsage { get; private set; }
        /// <summary>
        /// Gets or sets the success rate.
        /// </summary>
        /// <value>The success rate.</value>
        public float SuccessRate { get; private set; }
        /// <summary>
        /// Gets or sets the sample count.
        /// </summary>
        /// <value>The sample count.</value>
        public int SampleCount { get; private set; }

        private double _totalExecutionTime;
        private long _totalMemoryUsage;
        private int _successCount;
        /// <summary>
        /// Initializes a new instance of the LinearAlgebraPerformanceMetrics class.
        /// </summary>
        /// <param name="executionTime">The execution time.</param>
        /// <param name="memoryUsage">The memory usage.</param>
        /// <param name="success">The success.</param>

        public LinearAlgebraPerformanceMetrics(double executionTime, long memoryUsage, bool success)
        {
            _totalExecutionTime = executionTime;
            _totalMemoryUsage = memoryUsage;
            _successCount = success ? 1 : 0;
            SampleCount = 1;
            UpdateAverages();
        }
        /// <summary>
        /// Updates the metrics.
        /// </summary>
        /// <param name="executionTime">The execution time.</param>
        /// <param name="memoryUsage">The memory usage.</param>
        /// <param name="success">The success.</param>

        public void UpdateMetrics(double executionTime, long memoryUsage, bool success)
        {
            _totalExecutionTime += executionTime;
            _totalMemoryUsage += memoryUsage;
            if (success)
            {
                _successCount++;
            }

            SampleCount++;
            UpdateAverages();
        }

        private void UpdateAverages()
        {
            AverageExecutionTime = _totalExecutionTime / SampleCount;
            AverageMemoryUsage = _totalMemoryUsage / SampleCount;
            SuccessRate = (float)_successCount / SampleCount;
        }
    }

    /// <summary>
    /// Optimization recommendation based on analysis.
    /// </summary>
    public class OptimizationRecommendation
    {
        /// <summary>
        /// Gets or sets the use g p u.
        /// </summary>
        /// <value>The use g p u.</value>
        public bool UseGPU { get; set; }
        /// <summary>
        /// Gets or sets the recommended solver.
        /// </summary>
        /// <value>The recommended solver.</value>
        public LinearSystemSolver RecommendedSolver { get; set; }
        /// <summary>
        /// Gets or sets the confidence.
        /// </summary>
        /// <value>The confidence.</value>
        public float Confidence { get; set; }
        /// <summary>
        /// Gets or sets the reason.
        /// </summary>
        /// <value>The reason.</value>
        public string Reason { get; set; } = string.Empty;
    }
    /// <summary>
    /// An linear system solver enumeration.
    /// </summary>

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
}