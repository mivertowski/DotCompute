// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Enums;

/// <summary>
/// Data classification levels for secure wiping and protection policies.
/// Higher values indicate more sensitive data requiring stronger protection.
/// </summary>
public enum DataClassification
{
    /// <summary>
    /// Public data requiring no special protection.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Internal data for company use only.
    /// </summary>
    Internal = 1,

    /// <summary>
    /// Sensitive data requiring basic protection.
    /// </summary>
    Sensitive = 2,

    /// <summary>
    /// Confidential data requiring enhanced protection.
    /// </summary>
    Confidential = 3,

    /// <summary>
    /// Secret data requiring strong protection.
    /// </summary>
    Secret = 4,

    /// <summary>
    /// Top secret data requiring maximum protection.
    /// </summary>
    TopSecret = 5
}
