// <copyright file="WarningSeverity.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Kernels.Validation;

/// <summary>
/// Defines warning severity levels for kernel validation warnings.
/// Used to categorize warnings by their potential impact on kernel execution.
/// </summary>
public enum WarningSeverity
{
    /// <summary>
    /// Informational message. No impact on functionality or performance.
    /// Provides helpful information about the kernel or compilation process.
    /// </summary>
    Info,

    /// <summary>
    /// Standard warning. May indicate suboptimal code or minor issues.
    /// Should be reviewed but doesn't necessarily require immediate action.
    /// </summary>
    Warning,

    /// <summary>
    /// Serious warning. Indicates significant issues that may affect correctness or performance.
    /// Should be addressed before production deployment.
    /// </summary>
    Serious
}