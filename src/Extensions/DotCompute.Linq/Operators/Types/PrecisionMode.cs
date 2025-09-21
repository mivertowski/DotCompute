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
    /// Single precision (32-bit float).
    Single = 1,
    /// Double precision (64-bit float).
    Double = 2,
    /// Half precision (16-bit float).
    Half = 3,
    /// Mixed precision mode.
    Mixed = 4
}
