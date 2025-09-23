// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Defines error severity levels.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Information level - not an error.
    /// </summary>
    Info,

    /// <summary>
    /// Warning level - potential issue.
    /// </summary>
    Warning,

    /// <summary>
    /// Error level - operation failed.
    /// </summary>
    Error,

    /// <summary>
    /// Critical level - system failure.
    /// </summary>
    Critical
}

// BottleneckType moved to DotCompute.Abstractions.Types.BottleneckType
// This duplicate enum has been removed to avoid conflicts.