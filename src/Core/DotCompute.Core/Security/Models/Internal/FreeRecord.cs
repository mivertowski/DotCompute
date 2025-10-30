// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Models.Internal;

/// <summary>
/// Internal record of freed memory for double-free detection.
/// Maintains history of deallocated memory to prevent double-free attacks.
/// </summary>
internal sealed class FreeRecord
{
    /// <summary>
    /// Gets or sets the memory address that was freed.
    /// </summary>
    public required IntPtr Address { get; init; }

    /// <summary>
    /// Gets or sets the size of the freed memory.
    /// </summary>
    public required nuint Size { get; init; }

    /// <summary>
    /// Gets or sets the time when the memory was freed.
    /// </summary>
    public required DateTimeOffset FreeTime { get; init; }

    /// <summary>
    /// Gets or sets the call site where the free operation occurred.
    /// </summary>
    public required AllocationCallSite CallSite { get; init; }
}
