// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Options for method body extraction.
/// </summary>
public sealed class ExtractionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to preserve original formatting.
    /// </summary>
    public bool PreserveFormatting { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to include comments.
    /// </summary>
    public bool IncludeComments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to expand expression bodies.
    /// </summary>
    public bool ExpandExpressionBodies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to normalize whitespace.
    /// </summary>
    public bool NormalizeWhitespace { get; set; }

    /// <summary>
    /// Gets or sets the indent size.
    /// </summary>
    public int IndentSize { get; set; } = 4;
}
