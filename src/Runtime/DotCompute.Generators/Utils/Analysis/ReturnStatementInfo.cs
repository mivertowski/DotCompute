// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Information about a return statement.
/// </summary>
public sealed class ReturnStatementInfo
{
    /// <summary>
    /// Gets or sets the return expression.
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is an expression-bodied return.
    /// </summary>
    public bool IsExpressionBodied { get; set; }

    /// <summary>
    /// Gets or sets the line number of the return statement.
    /// </summary>
    public int LineNumber { get; set; }
}
