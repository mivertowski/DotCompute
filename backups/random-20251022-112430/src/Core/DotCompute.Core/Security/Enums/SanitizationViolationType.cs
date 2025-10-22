// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Enums;

/// <summary>
/// Types of memory sanitization violations that can be detected.
/// Each type represents a different class of memory safety issue.
/// </summary>
public enum SanitizationViolationType
{
    /// <summary>
    /// Attempt to access memory after it has been freed.
    /// </summary>
    UseAfterFree,

    /// <summary>
    /// Attempt to free the same memory location twice.
    /// </summary>
    DoubleFree,

    /// <summary>
    /// Access beyond allocated memory boundaries.
    /// </summary>
    BoundsViolation,

    /// <summary>
    /// Memory corruption detected through canary values or checksums.
    /// </summary>
    CorruptionDetected,

    /// <summary>
    /// Unauthorized access to protected memory regions.
    /// </summary>
    UnauthorizedAccess
}
