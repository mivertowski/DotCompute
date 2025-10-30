// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security.Enums;

namespace DotCompute.Core.Security.Models;

/// <summary>
/// Result of memory deallocation operation.
/// Contains deallocation details, security validation results, and cleanup status.
/// </summary>
public sealed class MemoryDeallocationResult
{
    /// <summary>
    /// Gets or sets the memory address that was deallocated.
    /// </summary>
    public IntPtr Address { get; init; }

    /// <summary>
    /// Gets or sets the deallocation timestamp.
    /// </summary>
    public DateTimeOffset DeallocationTime { get; init; }

    /// <summary>
    /// Gets or sets whether the deallocation was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the error message if deallocation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes that were freed.
    /// </summary>
    public nuint BytesFreed { get; set; }

    /// <summary>
    /// Gets or sets the security level of the deallocated data.
    /// </summary>
    public DataClassification SecurityLevel { get; set; }

    /// <summary>
    /// Gets or sets whether memory corruption was detected during deallocation.
    /// </summary>
    public bool CorruptionDetected { get; set; }
}
