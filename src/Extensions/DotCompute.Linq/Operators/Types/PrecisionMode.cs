// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Operators;

/// <summary>
/// Defines precision modes for kernel operations.
/// </summary>
public enum PrecisionMode
{
    /// <summary>
    /// Default precision mode.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Single precision (32-bit float).
    /// </summary>
    Single = 1,

    /// <summary>
    /// Double precision (64-bit float).
    /// </summary>
    Double = 2,

    /// <summary>
    /// Half precision (16-bit float).
    /// </summary>
    Half = 3,

    /// <summary>
    /// Mixed precision mode.
    /// </summary>
    Mixed = 4
}