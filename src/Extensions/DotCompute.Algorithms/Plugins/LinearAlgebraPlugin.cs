// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Types.LinearAlgebra;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Plugins
{

    /// <summary>
    /// Plugin for linear algebra operations on matrices and vectors.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="LinearAlgebraPlugin"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance.</param>
    public sealed class LinearAlgebraPlugin(ILogger<LinearAlgebraPlugin> logger) : AlgorithmPluginBase(logger)
{

        /// <inheritdoc/>
        public override string Id => "com.dotcompute.algorithms.linalg";

    /// <inheritdoc/>
    public override string Name => "Linear Algebra Operations";

    /// <inheritdoc/>
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public override string Description => "Provides high-performance linear algebra operations including matrix multiplication, decomposition, and solving linear systems.";

    /// <inheritdoc/>
    public override AcceleratorType[] SupportedAcceleratorTypes => [
        AcceleratorType.CPU,
        AcceleratorType.CUDA,
        AcceleratorType.ROCm,
        AcceleratorType.OneAPI
    ];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedOperations => [
        "MatrixMultiplication",
        "MatrixAddition",
        "MatrixSubtraction",
        "MatrixTranspose",
        "MatrixInverse",
        "LUDecomposition",
        "QRDecomposition",
        "CholeskyDecomposition",
        "SVD",
        "EigenDecomposition",
        "LinearSolve"
    ];

    /// <inheritdoc/>
    public override Type[] InputTypes => [typeof(string), typeof(Matrix[])];

    /// <inheritdoc/>
    public override Type OutputType => typeof(Matrix);

    /// <inheritdoc/>
    protected override async Task<object> OnExecuteAsync(object[] inputs, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        if (inputs.Length < 2)
        {
            throw new ArgumentException("Expected operation name and matrix array.");
        }

        var operation = (string)inputs[0];
        var matrices = (Matrix[])inputs[1];

        return operation.ToUpperInvariant() switch
        {
            "MULTIPLY" or "MATMUL" => await MultiplyAsync(matrices, cancellationToken).ConfigureAwait(false),
            "ADD" => await AddAsync(matrices, cancellationToken).ConfigureAwait(false),
            "SUBTRACT" or "SUB" => await SubtractAsync(matrices, cancellationToken).ConfigureAwait(false),
            "TRANSPOSE" => await TransposeAsync(matrices[0], cancellationToken).ConfigureAwait(false),
            "INVERSE" or "INV" => await InverseAsync(matrices[0], cancellationToken).ConfigureAwait(false),
            "DETERMINANT" or "DET" => await DeterminantAsync(matrices[0], cancellationToken).ConfigureAwait(false),
            "LU" => await LUDecompositionAsync(matrices[0], cancellationToken).ConfigureAwait(false),
            "SOLVE" => await SolveAsync(matrices[0], matrices[1], cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Operation '{operation}' is not supported.")
        };
    }

    /// <inheritdoc/>
    public override bool ValidateInputs(object[] inputs)
    {
        if (!base.ValidateInputs(inputs))
        {
            return false;
        }

        if (inputs.Length < 2)
        {
            return false;
        }

        if (inputs[0] is not string operation || string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        if (inputs[1] is not Matrix[] matrices || matrices.Length == 0)
        {
            return false;
        }

        // Validate based on operation
        return operation.ToUpperInvariant() switch
        {
            "MULTIPLY" or "MATMUL" => matrices.Length >= 2 && matrices[0].Columns == matrices[1].Rows,
            "ADD" or "SUBTRACT" or "SUB" => matrices.Length >= 2 && matrices[0].Rows == matrices[1].Rows && matrices[0].Columns == matrices[1].Columns,
            "TRANSPOSE" => matrices.Length >= 1,
            "INVERSE" or "INV" => matrices.Length >= 1 && matrices[0].IsSquare,
            "DETERMINANT" or "DET" => matrices.Length >= 1 && matrices[0].IsSquare,
            "LU" => matrices.Length >= 1 && matrices[0].IsSquare,
            "SOLVE" => matrices.Length >= 2 && matrices[0].IsSquare && matrices[0].Rows == matrices[1].Rows,
            _ => false
        };
    }

    /// <inheritdoc/>
    public override long EstimateMemoryRequirement(int[] inputSizes)
    {
        if (inputSizes.Length < 2)
        {
            return 0;
        }

        // Assume square matrices for estimation
        var n = inputSizes[0];

        // Memory for input matrices + result + workspace
        return n * n * sizeof(float) * 4 + 4096; // Extra for overhead
    }

    /// <inheritdoc/>
    public override AlgorithmPerformanceProfile GetPerformanceProfile()
    {
        return new AlgorithmPerformanceProfile
        {
            Complexity = "O(n³) for multiplication, O(n²) for addition",
            IsParallelizable = true,
            OptimalParallelism = Environment.ProcessorCount * 2,
            IsMemoryBound = true,
            IsComputeBound = true,
            EstimatedFlops = 2, // 2n³ for matrix multiplication
            Metadata = new Dictionary<string, object>
            {
                ["BlockSize"] = 64,
                ["CacheOptimized"] = true,
                ["SimdAccelerated"] = true,
                ["SupportsTiling"] = true
            }
        };
    }

    private async Task<Matrix> MultiplyAsync(Matrix[] matrices, CancellationToken cancellationToken)
    {
        if (matrices.Length < 2)
        {
            throw new ArgumentException("Matrix multiplication requires at least 2 matrices.");
        }

        var result = await MatrixMath.MultiplyAsync(matrices[0], matrices[1], Accelerator, cancellationToken).ConfigureAwait(false);

        // Chain multiply if more than 2 matrices
        for (var i = 2; i < matrices.Length; i++)
        {
            result = await MatrixMath.MultiplyAsync(result, matrices[i], Accelerator, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<Matrix> AddAsync(Matrix[] matrices, CancellationToken cancellationToken)
    {
        if (matrices.Length < 2)
        {
            throw new ArgumentException("Matrix addition requires at least 2 matrices.");
        }

        var result = await MatrixMath.AddAsync(matrices[0], matrices[1], Accelerator, cancellationToken).ConfigureAwait(false);

        // Chain add if more than 2 matrices
        for (var i = 2; i < matrices.Length; i++)
        {
            result = await MatrixMath.AddAsync(result, matrices[i], Accelerator, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task<Matrix> SubtractAsync(Matrix[] matrices, CancellationToken cancellationToken)
    {
        if (matrices.Length < 2)
        {
            throw new ArgumentException("Matrix subtraction requires at least 2 matrices.");
        }

        return await MatrixMath.SubtractAsync(matrices[0], matrices[1], Accelerator, cancellationToken).ConfigureAwait(false);
    }

        private async Task<Matrix> TransposeAsync(Matrix matrix, CancellationToken cancellationToken) => await MatrixMath.TransposeAsync(matrix, Accelerator, cancellationToken).ConfigureAwait(false);

        private async Task<Matrix> InverseAsync(Matrix matrix, CancellationToken cancellationToken) => await MatrixMath.InverseAsync(matrix, Accelerator, cancellationToken).ConfigureAwait(false);

        private async Task<object> DeterminantAsync(Matrix matrix, CancellationToken cancellationToken)
    {
        var det = await MatrixMath.DeterminantAsync(matrix, Accelerator, cancellationToken).ConfigureAwait(false);
        return det; // Box the float as object
    }

    private async Task<object> LUDecompositionAsync(Matrix matrix, CancellationToken cancellationToken)
    {
        var (l, u, p) = await MatrixMath.LUDecompositionAsync(matrix, Accelerator, cancellationToken).ConfigureAwait(false);
        return new { L = l, U = u, P = p }; // Return as anonymous object
    }

        private async Task<Matrix> SolveAsync(Matrix a, Matrix b, CancellationToken cancellationToken) => await MatrixMath.SolveAsync(a, b, Accelerator, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        protected override ValueTask OnDisposeAsync()
            // Nothing to dispose currently
            => ValueTask.CompletedTask;
    }
}
