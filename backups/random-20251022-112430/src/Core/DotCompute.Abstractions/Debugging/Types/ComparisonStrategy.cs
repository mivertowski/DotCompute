// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Strategies for comparing execution results.
/// </summary>
public enum ComparisonStrategy
{
    Exact,
    Tolerance,
    Statistical,
    Relative
}
