// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Results of method body analysis.
/// </summary>
public sealed class MethodBodyAnalysis
{
    /// <summary>
    /// Gets or sets the method name.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the method has a body.
    /// </summary>
    public bool HasBody { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method is expression-bodied.
    /// </summary>
    public bool IsExpressionBodied { get; set; }

    /// <summary>
    /// Gets or sets the statement count.
    /// </summary>
    public int StatementCount { get; set; }

    /// <summary>
    /// Gets or sets the loop count.
    /// </summary>
    public int LoopCount { get; set; }

    /// <summary>
    /// Gets or sets the conditional count.
    /// </summary>
    public int ConditionalCount { get; set; }

    /// <summary>
    /// Gets or sets the try-catch count.
    /// </summary>
    public int TryCatchCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method has async operations.
    /// </summary>
    public bool HasAsyncOperations { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the method has LINQ operations.
    /// </summary>
    public bool HasLinqOperations { get; set; }
}
