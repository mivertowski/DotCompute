// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Call site information for memory allocations.
/// Captures debugging information about where an allocation occurred.
/// </summary>
public sealed class AllocationCallSite
{
    /// <summary>
    /// Gets or sets the method name where allocation occurred.
    /// </summary>
    public required string Method { get; init; }

    /// <summary>
    /// Gets or sets the source file name.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// Gets or sets the line number in the source file.
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// Returns a string representation of the call site.
    /// </summary>
    public override string ToString()
        => $"{Method} at {File}:{Line}";
}
