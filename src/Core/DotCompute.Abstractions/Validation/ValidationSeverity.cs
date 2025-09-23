// <copyright file="ValidationSeverity.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Validation;

/// <summary>
/// Defines severity levels for validation issues.
/// Used across all validation systems to provide consistent severity reporting.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message that provides context or suggestions.
    /// Does not prevent compilation or execution.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that indicates potential issues but does not prevent execution.
    /// Should be addressed to ensure optimal performance or reliability.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents successful compilation or execution.
    /// Must be addressed before the operation can proceed.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that indicates severe issues requiring immediate attention.
    /// May indicate potential system instability or data loss.
    /// </summary>
    Critical
}