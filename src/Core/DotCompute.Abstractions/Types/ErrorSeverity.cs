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

/// <summary>
/// Defines bottleneck types for performance analysis.
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// No bottleneck detected.
    /// </summary>
    None,

    /// <summary>
    /// CPU processing is the bottleneck.
    /// </summary>
    Cpu,

    /// <summary>
    /// Memory bandwidth is the bottleneck.
    /// </summary>
    Memory,

    /// <summary>
    /// I/O operations are the bottleneck.
    /// </summary>
    Io,

    /// <summary>
    /// GPU processing is the bottleneck.
    /// </summary>
    Gpu,

    /// <summary>
    /// Network communication is the bottleneck.
    /// </summary>
    Network,

    /// <summary>
    /// Data transfer between devices is the bottleneck.
    /// </summary>
    DataTransfer
}