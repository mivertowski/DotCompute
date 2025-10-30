// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Security.Enums;

namespace DotCompute.Core.Security.Models.Internal;

/// <summary>
/// Internal tracking record for sanitized memory allocations.
/// Contains comprehensive allocation metadata for security monitoring.
/// </summary>
internal sealed class SanitizedAllocation
{
    /// <summary>
    /// Gets or sets the base address of the allocation (including guard bytes).
    /// </summary>
    public required IntPtr BaseAddress { get; init; }

    /// <summary>
    /// Gets or sets the user-accessible memory address.
    /// </summary>
    public required IntPtr UserAddress { get; init; }

    /// <summary>
    /// Gets or sets the size requested by the user.
    /// </summary>
    public required nuint RequestedSize { get; init; }

    /// <summary>
    /// Gets or sets the total allocated size including guards and metadata.
    /// </summary>
    public required nuint TotalSize { get; init; }

    /// <summary>
    /// Gets or sets the data classification level.
    /// </summary>
    public required DataClassification Classification { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier for this allocation.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets or sets the allocation timestamp.
    /// </summary>
    public required DateTimeOffset AllocationTime { get; init; }

    /// <summary>
    /// Gets or sets the call site information.
    /// </summary>
    public required AllocationCallSite CallSite { get; init; }

    /// <summary>
    /// Gets or sets the canary value for corruption detection.
    /// </summary>
    public ulong CanaryValue { get; init; }

    /// <summary>
    /// Gets or sets the number of times this memory has been accessed.
    /// </summary>
    public long AccessCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last access.
    /// </summary>
    public DateTimeOffset LastAccessTime { get; set; }
}
