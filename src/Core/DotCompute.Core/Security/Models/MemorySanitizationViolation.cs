// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security.Enums;

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Memory sanitization violation information.
/// Contains details about detected memory safety violations.
/// </summary>
public sealed class MemorySanitizationViolation
{
    /// <summary>
    /// Gets or sets the type of violation detected.
    /// </summary>
    public required SanitizationViolationType ViolationType { get; init; }

    /// <summary>
    /// Gets or sets the memory address where the violation occurred.
    /// </summary>
    public required IntPtr Address { get; init; }

    /// <summary>
    /// Gets or sets the size of the memory operation that caused the violation.
    /// </summary>
    public nuint Size { get; init; }

    /// <summary>
    /// Gets or sets the operation that triggered the violation.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets or sets a detailed description of the violation.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Returns a string representation of the violation.
    /// </summary>
    public override string ToString()
        => $"{ViolationType} at {Address:X} during {Operation}: {Description}";
}
