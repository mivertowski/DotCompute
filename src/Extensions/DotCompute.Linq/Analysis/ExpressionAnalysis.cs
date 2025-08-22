// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Expression analysis result containing GPU compatibility information.
/// </summary>
/// <remarks>
/// This class encapsulates the results of analyzing a LINQ expression tree
/// for GPU compatibility, including support status, operation types,
/// and complexity estimates.
/// </remarks>
internal sealed class ExpressionAnalysis
{
    /// <summary>
    /// Gets or sets whether the expression can execute on GPU.
    /// </summary>
    /// <value>
    /// <c>true</c> if the expression is compatible with GPU execution; otherwise, <c>false</c>.
    /// </value>
    public bool CanExecuteOnGPU { get; set; }

    /// <summary>
    /// Gets or sets the reason why the expression cannot execute on GPU.
    /// </summary>
    /// <value>
    /// A human-readable explanation of incompatibility, or <c>null</c> if compatible.
    /// </value>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the type of operation represented by the expression.
    /// </summary>
    /// <value>
    /// The operation type (e.g., "Map", "Filter", "Reduce"), or <c>null</c> if undetermined.
    /// </value>
    /// <remarks>
    /// This categorization helps the GPU compiler choose the most appropriate
    /// kernel generation strategy.
    /// </remarks>
    public string? OperationType { get; set; }

    /// <summary>
    /// Gets or sets the input types used in the expression.
    /// </summary>
    /// <value>
    /// An array of types representing the inputs to the expression, or <c>null</c> if none detected.
    /// </value>
    public Type[]? InputTypes { get; set; }

    /// <summary>
    /// Gets or sets the output type of the expression.
    /// </summary>
    /// <value>
    /// The type that the expression will produce, or <c>null</c> if undetermined.
    /// </value>
    public Type? OutputType { get; set; }

    /// <summary>
    /// Gets or sets the estimated complexity of the expression.
    /// </summary>
    /// <value>
    /// A numeric estimate of computational complexity, with higher values indicating more complex operations.
    /// </value>
    /// <remarks>
    /// This metric helps determine whether GPU execution would be beneficial
    /// compared to CPU execution for the given workload.
    /// </remarks>
    public int EstimatedComplexity { get; set; }
}