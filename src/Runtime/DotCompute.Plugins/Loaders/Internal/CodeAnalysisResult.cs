// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Result of code analysis.
/// </summary>
internal class CodeAnalysisResult
{
    /// <summary>
    /// Gets or sets the suspicious patterns.
    /// </summary>
    /// <value>The suspicious patterns.</value>
    public IList<SuspiciousCodePattern> SuspiciousPatterns { get; } = [];
    /// <summary>
    /// Gets or sets the analysis errors.
    /// </summary>
    /// <value>The analysis errors.</value>
    public IList<string> AnalysisErrors { get; } = [];
}
