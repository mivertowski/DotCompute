// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Memory;

/// <summary>
/// Specifies the access mode for mapping memory buffers
/// </summary>
public enum MapMode
{
    /// <summary>
    /// Map for read-only access
    /// </summary>
    Read,

    /// <summary>
    /// Map for write-only access
    /// </summary>
    Write,

    /// <summary>
    /// Map for read-write access
    /// </summary>
    ReadWrite
}
