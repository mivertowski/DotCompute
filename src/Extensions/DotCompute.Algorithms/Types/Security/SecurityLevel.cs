
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Security;

/// <summary>
/// Defines security levels for algorithm plugin validation.
/// </summary>
public enum SecurityLevel
{
    /// <summary>
    /// Low security - minimal validation.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium security - standard validation (default).
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High security - strict validation including digital signatures.
    /// </summary>
    High = 2,

    /// <summary>
    /// Maximum security - all validation checks enabled.
    /// </summary>
    Maximum = 3
}
