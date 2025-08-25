// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Result of code analysis.
/// </summary>
internal class CodeAnalysisResult
{
    public List<SuspiciousCodePattern> SuspiciousPatterns { get; } = [];
    public List<string> AnalysisErrors { get; } = [];
}