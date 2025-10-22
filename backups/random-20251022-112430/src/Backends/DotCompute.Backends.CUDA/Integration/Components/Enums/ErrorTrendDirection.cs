// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Integration.Components.Enums;
/// <summary>
/// An error trend direction enumeration.
/// </summary>

/// <summary>
/// Error trend direction indicators.
/// </summary>
public enum ErrorTrendDirection
{
    /// <summary>
    /// Error rate is improving (decreasing errors).
    /// </summary>
    Improving,

    /// <summary>
    /// Error rate is stable (no significant change).
    /// </summary>
    Stable,

    /// <summary>
    /// Error rate is deteriorating (increasing errors).
    /// </summary>
    Deteriorating
}