// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Context for loop generation.
/// </summary>
public sealed class LoopContext
{
    /// <summary>
    /// Gets or sets the loop index variable name.
    /// </summary>
    public string IndexVariable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the loop limit variable name.
    /// </summary>
    public string LimitVariable { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the loop body code.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the loop start value.
    /// </summary>
    public int StartValue { get; set; }

    /// <summary>
    /// Gets or sets the loop increment value.
    /// </summary>
    public int Increment { get; set; } = 1;

    /// <summary>
    /// Gets or sets the optimization options.
    /// </summary>
    public LoopOptimizationOptions Options { get; set; } = new();
}
