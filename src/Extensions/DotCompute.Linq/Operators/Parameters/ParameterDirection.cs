// <copyright file="ParameterDirection.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Linq.Operators.Parameters;
/// <summary>
/// Defines the direction of data flow for kernel parameters.
/// </summary>
public enum ParameterDirection
{
    /// <summary>
    /// Input parameter - data flows into the kernel.
    /// </summary>
    In,
    /// Output parameter - data flows out of the kernel.
    Out,
    /// Input/output parameter - data flows both into and out of the kernel.
    InOut,
    /// Input parameter (alias for In).
    Input = In,
    /// Output parameter (alias for Out).
    Output = Out
}
