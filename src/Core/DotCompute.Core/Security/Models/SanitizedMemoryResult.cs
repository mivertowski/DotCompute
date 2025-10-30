// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security.Enums;

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Result of sanitized memory allocation operation.
/// Contains allocation details, security information, and operation status.
/// </summary>
public sealed class SanitizedMemoryResult
{
    /// <summary>
    /// Gets or sets the requested memory size.
    /// </summary>
    public nuint RequestedSize { get; init; }

    /// <summary>
    /// Gets or sets the data classification level.
    /// </summary>
    public DataClassification Classification { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier for this allocation.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets or sets the allocation timestamp.
    /// </summary>
    public DateTimeOffset AllocationTime { get; init; }

    /// <summary>
    /// Gets or sets the allocated memory address.
    /// </summary>
    public IntPtr Address { get; set; }

    /// <summary>
    /// Gets or sets the actual allocated size.
    /// </summary>
    public nuint ActualSize { get; set; }

    /// <summary>
    /// Gets or sets whether the allocation was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the error message if allocation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
