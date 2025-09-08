// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Defines the direction of data flow for kernel parameters.
/// </summary>
public enum ParameterDirection
{
    /// <summary>
    /// Input parameter - data flows into the kernel.
    /// </summary>
    In,

    /// <summary>
    /// Output parameter - data flows out of the kernel.
    /// </summary>
    Out,

    /// <summary>
    /// Input/output parameter - data flows both into and out of the kernel.
    /// </summary>
    InOut
}